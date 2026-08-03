using Il2CppMashBox.Addons.ReplaySystem;
using Il2CppMashBox.Addons.ReplaySystem.Replay_System;
using Il2CppMashBox.Addons.ReplaySystem.Replay_System.Key_Framing.KeyFramableDataTypes;
using Il2CppMashBox.Addons.ReplaySystem.Replay_System.Key_Framing.Playables;
using Il2CppMashBox.Core.Runtime.SmartData;
using Il2CppCinemachine;
using Il2CppInterop.Runtime;
using MelonLoader;
using rowemod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;
using UnityCamera = UnityEngine.Camera;
using GameReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;

namespace rowemod.Mods
{
    /// <summary>
    /// Replay-scoped local filming light. It binds only to the native transform driven by replay
    /// camera keyframes, so parented light motion remains entirely native after a short probe.
    /// </summary>
    public static class ReplayCameraLight
    {
        private const string LightObjectName = "RoweMod Replay Camera Light";
        private const string MatteObjectName = "RoweMod Replay Framing Mattes";
        private const int OverlayWindowId = 537219;
        private const int MaximumCustomKeys = 2048;
        private const float KeyTimeEpsilon = 0.0085f;

        private enum LensTrack
        {
            Fov,
            Tilt,
            Fisheye,
            Vignette,
            Shake,
            DofState,
            NearStart,
            NearEnd,
            FarStart,
            FarEnd,
            DofPhysical,
            Count
        }

        private struct CameraLabSnapshot
        {
            public float time;
            public float fov;
            public float tilt;
            public float fisheye;
            public float vignette;
            public int shakeMode;
            public bool dofEnabled;
            public bool dofPhysical;
            public float nearStart;
            public float nearEnd;
            public float farStart;
            public float farEnd;
            public int framingMode;
            public float matteOpacity;
            public bool lightEnabled;
            public int lightType;
            public float lightIntensity;
            public float lightRange;
            public float lightSpotAngle;
            public float lightR;
            public float lightG;
            public float lightB;
            public float lightX;
            public float lightY;
            public float lightZ;
            public float lightPitch;
            public float lightYaw;
            public float lightRoll;
            public bool lightShadows;
            public bool lightSoftShadows;
            public int lightShadowResolution;
            public float lightShadowStrength;
            public float lightShadowBias;
            public float lightShadowNormalBias;
            public float lightShadowNearPlane;
        }

        private static GameObject lightObject;
        private static Light lightComponent;
        private static RecordableCamera recordableCamera;
        private static FreeCam freeCam;
        private static PlayableKeyFrameDataReplayCameraTransformBehaviour keyframeCamera;
        private static CameraSettingsControls cameraSettingsControls;
        private static ReplayEditor replayEditor;
        private static KeyFrameDataGroupBehaviour keyFrameGroup;
        private static Il2CppSystem.Action keysUpdatedHandler;
        private static readonly bool[] nativeLensTracks = new bool[(int)LensTrack.Count];
        private static readonly List<CameraLabSnapshot> customKeys = new List<CameraLabSnapshot>(64);
        private static readonly HashSet<int> knownNativeKeyTimes = new HashSet<int>();
        private static int customKeyCursor;
        private static float lastTimelineTime = float.NaN;
        private static bool lensApplyPending;
        private static bool nativeValuesCaptured;
        private static bool cameraLabAudited;
        private static int lastReplayEditorStateHash;
        private static bool previousReplaySettingsVisible;
        private static float nextReplayModeCheckTime;
        private static CinemachineBrain cinematicsBrain;
        private static UnityCamera replayOutputCamera;
        private static Transform targetTransform;
        private static Collider freeCamCollider;
        private static bool freeCamColliderStateCaptured;
        private static bool originalFreeCamColliderEnabled;
        private static bool replayActive;
        private static bool replaySettingsVisible;
        private static int replayCameraLookupGeneration;
        private static int overlayTab;
        private static Rect overlayRect = new Rect(24f, 120f, 440f, 650f);
        private static Vector2 overlayScroll;
        private static string status = "Off";
        private static string keyframeStatus = "Open Replay to audit camera tracks";
        private static GameObject matteObject;
        private static Image[] matteImages;
        private static int matteScreenWidth;
        private static int matteScreenHeight;
        private static int matteMode = -1;
        private static float matteOpacity = -1f;
        private static string presetName = string.Empty;
        private static string selectedPreset = string.Empty;
        private static readonly List<string> presetCache = new List<string>();
        private static bool presetCacheLoaded;

        public static string Status => status;

        private static Transform ResolveReplayCameraTransform(out string label)
        {
            Transform keyframedTransform = keyframeCamera == null ? null : keyframeCamera._animatedCameraTrans;
            if (IsUsableTransform(keyframedTransform))
            {
                label = "keyframed replay camera";
                return keyframedTransform;
            }

            if (cinematicsBrain == null)
                cinematicsBrain = FindFirstLoaded<CinemachineBrain>();

            if (replayOutputCamera == null && cinematicsBrain != null)
                cinematicsBrain.gameObject.TryGetComponent(out replayOutputCamera);

            if (replayOutputCamera != null && replayOutputCamera.isActiveAndEnabled)
            {
                label = "Cinemachine replay output camera";
                return replayOutputCamera.transform;
            }

            // This is only called from the native open-Replay event's bounded lookup.
            UnityCamera mainCamera = UnityCamera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                replayOutputCamera = mainCamera;
                label = "active replay output camera";
                return mainCamera.transform;
            }

            label = "replay camera";
            return null;
        }

        public static void OnReplayOpened(object replayEditor)
        {
            replayActive = true;
            replaySettingsVisible = false;
            nativeValuesCaptured = false;
            lastTimelineTime = float.NaN;
            customKeyCursor = 0;
            customKeys.Clear();
            knownNativeKeyTimes.Clear();
            lastReplayEditorStateHash = 0;
            previousReplaySettingsVisible = false;
            nextReplayModeCheckTime = 0f;
            replayCameraLookupGeneration++;
            MelonCoroutines.Start(LocateReplayCameraFromOpenEvent(replayCameraLookupGeneration));
        }

        private static IEnumerator LocateReplayCameraFromOpenEvent(int generation)
        {
            const int attempts = 20;
            const float retryDelaySeconds = 0.1f;

            status = "Replay opened - locating Cinemachine camera";
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (!replayActive || generation != replayCameraLookupGeneration)
                    yield break;

                BindReplayObjects();
                Transform replayCameraTransform = ResolveReplayCameraTransform(out string cameraLabel);
                if (IsUsableTransform(replayCameraTransform))
                {
                    SetTargetTransform(replayCameraTransform, cameraLabel);
                    BindCameraLab();
                    ApplySettings();
                    if (cameraSettingsControls != null && keyFrameGroup != null && replayEditor != null)
                        yield break;
                }

                yield return new WaitForSecondsRealtime(retryDelaySeconds);
            }

            if (replayActive && generation == replayCameraLookupGeneration)
            {
                if (IsUsableTransform(targetTransform))
                    status = "Replay camera found; some Camera Lab controls unavailable";
                else
                    status = "Replay camera unavailable after open event";
            }
        }

        public static void OnReplaySettingsShown(object replayEditor)
        {
            replaySettingsVisible = true;
            ReplayEditor editor = replayEditor as ReplayEditor;
            if (editor != null)
                ReplayCameraLight.replayEditor = editor;
        }

        public static void OnReplaySettingsHidden()
        {
            replaySettingsVisible = false;
        }

        public static void OnReplayCameraKeyframePlaybackEntered(object instance)
        {
            keyframeCamera = instance as PlayableKeyFrameDataReplayCameraTransformBehaviour;
            if (!replayActive)
                return;

            RebindTargetTransform();
            ApplySettings();
        }

        /// <summary>
        /// The replay camera is initialized after ReplayEditor.ShowUI on some game paths. Bind to
        /// that native lifecycle event instead of polling for it each frame.
        /// </summary>
        public static void OnRecordableCameraReady(object instance)
        {
            RecordableCamera readyCamera = instance as RecordableCamera;
            if (readyCamera != null)
                recordableCamera = readyCamera;

            if (!replayActive)
                return;

            RebindTargetTransform();
            ApplySettings();
        }

        public static void OnSettingsChanged()
        {
            Config.NormalizeReplaySettings(Config.replaySettings);
            if (!replayActive)
            {
                status = Config.replaySettings.cameraLightEnabled
                    ? "Ready - open Replay"
                    : "Off";
                return;
            }

            ApplySettings();
        }

        public static void OnLensSettingsChanged()
        {
            Config.NormalizeReplaySettings(Config.replaySettings);
            lensApplyPending = true;
            Config.RequestSave();

            if (!replayActive)
                return;

            ApplyLensSettings(Config.replaySettings, false);
            lastTimelineTime = CurrentTimelineTime();
            UpdateMatteOverlay(true);
        }

        public static void Update()
        {
            if (!replayActive)
                return;

            if (replayEditor != null && replayEditor._settingsMenu != null)
                replaySettingsVisible = replayEditor._settingsMenu.activeInHierarchy;
            else
                replaySettingsVisible = false;

            float timelineTime = CurrentTimelineTime();
            bool settingsJustOpened = replaySettingsVisible && !previousReplaySettingsVisible;
            previousReplaySettingsVisible = replaySettingsVisible;
            if (settingsJustOpened)
                InheritPreviousKeyForEditing(timelineTime, "replay settings opened");

            if (Time.unscaledTime >= nextReplayModeCheckTime)
            {
                nextReplayModeCheckTime = Time.unscaledTime + 0.05f;
                DetectReplayEditorModeChange(timelineTime);
            }

            if (float.IsNaN(lastTimelineTime) || Mathf.Abs(timelineTime - lastTimelineTime) > 0.0001f)
            {
                SyncNativeTrackedValues();
                if (customKeys.Count > 0)
                    ApplyCustomTrack(timelineTime);
                lastTimelineTime = timelineTime;
            }

            if (matteScreenWidth != Screen.width || matteScreenHeight != Screen.height)
                UpdateMatteOverlay(true);
        }

        public static void OnSceneInitialized()
        {
            Cleanup();
        }

        public static void OnReplayClosed(string reason)
        {
            replaySettingsVisible = false;
            replayActive = false;
            replayCameraLookupGeneration++;
            RestoreFreeCamCollision();
            UnbindCameraLab();
            DestroyLight();
            DestroyMatteOverlay();
            ClearCachedObjects();
            status = Config.replaySettings.cameraLightEnabled
                ? "Ready - open Replay"
                : "Off";
            Log.Msg($"[ReplayLight] Released replay camera resources. reason={reason}");
        }

        public static void Cleanup()
        {
            replaySettingsVisible = false;
            replayActive = false;
            replayCameraLookupGeneration++;
            RestoreFreeCamCollision();
            UnbindCameraLab();
            DestroyLight();
            DestroyMatteOverlay();
            ClearCachedObjects();
            status = Config.replaySettings.cameraLightEnabled
                ? "Ready - open Replay"
                : "Off";
        }

        public static void DrawReplaySettingsOverlay()
        {
            if (!replaySettingsVisible || !replayActive)
                return;

            overlayRect = GUI.Window(
                OverlayWindowId,
                overlayRect,
                (GUI.WindowFunction)DrawOverlayWindow,
                "RoweMod Camera",
                Menu.windowStyle ?? GUI.skin.window);
        }

        /// <summary>Shared by the normal RoweMod Replay tab and the native replay settings overlay.</summary>
        public static void DrawLightControls(string controlPrefix)
        {
            ReplaySettings settings = Config.replaySettings;
            bool changed = false;

            bool enabled = settings.cameraLightEnabled;
            Menu.ModernToggle("Camera Light", ref enabled, controlPrefix + "enabled");
            changed |= enabled != settings.cameraLightEnabled;
            settings.cameraLightEnabled = enabled;

            float intensity = settings.cameraLightIntensity;
            Menu.ModernSlider("Intensity", ref intensity, 0f, 100f, controlPrefix + "intensity");
            changed |= !Mathf.Approximately(intensity, settings.cameraLightIntensity);
            settings.cameraLightIntensity = intensity;

            float range = settings.cameraLightRange;
            Menu.ModernSlider("Range", ref range, 1f, 50f, controlPrefix + "range");
            changed |= !Mathf.Approximately(range, settings.cameraLightRange);
            settings.cameraLightRange = range;

            if (GUILayout.Button(settings.cameraLightType == 0 ? "Light Type: Spot" : "Light Type: Point", Menu.UiButtonStyle))
            {
                settings.cameraLightType = settings.cameraLightType == 0 ? 1 : 0;
                changed = true;
            }

            if (settings.cameraLightType == 0)
            {
                float beamWidth = settings.cameraLightSpotAngle;
                Menu.ModernSlider("Beam Width", ref beamWidth, 10f, 179f, controlPrefix + "spot_angle");
                changed |= !Mathf.Approximately(beamWidth, settings.cameraLightSpotAngle);
                settings.cameraLightSpotAngle = beamWidth;
            }

            GUILayout.Label("Color", Menu.UiBadgeStyle);
            settings.cameraLightColorR = DrawSlider("Red", settings.cameraLightColorR, 0f, 1f, controlPrefix + "color_r", ref changed);
            settings.cameraLightColorG = DrawSlider("Green", settings.cameraLightColorG, 0f, 1f, controlPrefix + "color_g", ref changed);
            settings.cameraLightColorB = DrawSlider("Blue", settings.cameraLightColorB, 0f, 1f, controlPrefix + "color_b", ref changed);

            GUILayout.Label("Camera-relative Position", Menu.UiBadgeStyle);
            settings.cameraLightOffsetX = DrawSlider("X", settings.cameraLightOffsetX, -10f, 10f, controlPrefix + "offset_x", ref changed);
            settings.cameraLightOffsetY = DrawSlider("Y", settings.cameraLightOffsetY, -10f, 10f, controlPrefix + "offset_y", ref changed);
            settings.cameraLightOffsetZ = DrawSlider("Z", settings.cameraLightOffsetZ, -10f, 10f, controlPrefix + "offset_z", ref changed);

            GUILayout.Label("Camera-relative Rotation", Menu.UiBadgeStyle);
            settings.cameraLightPitch = DrawSlider("Pitch", settings.cameraLightPitch, -180f, 180f, controlPrefix + "pitch", ref changed);
            settings.cameraLightYaw = DrawSlider("Yaw", settings.cameraLightYaw, -180f, 180f, controlPrefix + "yaw", ref changed);
            settings.cameraLightRoll = DrawSlider("Roll", settings.cameraLightRoll, -180f, 180f, controlPrefix + "roll", ref changed);

            GUILayout.Label("Shadows", Menu.UiBadgeStyle);
            bool shadows = settings.cameraLightShadows;
            Menu.ModernToggle("Enable Shadows", ref shadows, controlPrefix + "shadows");
            changed |= shadows != settings.cameraLightShadows;
            settings.cameraLightShadows = shadows;

            if (settings.cameraLightShadows)
            {
                bool softShadows = settings.cameraLightSoftShadows;
                Menu.ModernToggle("Soft Shadows", ref softShadows, controlPrefix + "soft_shadows");
                changed |= softShadows != settings.cameraLightSoftShadows;
                settings.cameraLightSoftShadows = softShadows;

                if (GUILayout.Button("Shadow Resolution: " + ShadowResolutionLabel(settings.cameraLightShadowResolution), Menu.UiButtonStyle))
                {
                    settings.cameraLightShadowResolution = (settings.cameraLightShadowResolution + 1) % 5;
                    changed = true;
                }

                settings.cameraLightShadowStrength = DrawSlider("Shadow Strength", settings.cameraLightShadowStrength, 0f, 1f, controlPrefix + "shadow_strength", ref changed);
                settings.cameraLightShadowBias = DrawSlider("Shadow Bias", settings.cameraLightShadowBias, 0f, 2f, controlPrefix + "shadow_bias", ref changed);
                settings.cameraLightShadowNormalBias = DrawSlider("Normal Bias", settings.cameraLightShadowNormalBias, 0f, 3f, controlPrefix + "shadow_normal_bias", ref changed);
                settings.cameraLightShadowNearPlane = DrawSlider("Near Plane", settings.cameraLightShadowNearPlane, 0.01f, 10f, controlPrefix + "shadow_near_plane", ref changed);
            }

            if (changed)
            {
                Config.NormalizeReplaySettings(settings);
                Config.RequestSave();
                OnSettingsChanged();
            }
        }

        public static void DrawCameraControls(string controlPrefix)
        {
            bool changed = false;
            bool noCollision = Config.replaySettings.replayCameraNoCollision;
            Menu.ModernToggle("No Camera Collision", ref noCollision, controlPrefix + "no_collision");
            changed |= noCollision != Config.replaySettings.replayCameraNoCollision;
            Config.replaySettings.replayCameraNoCollision = noCollision;

            GUILayout.Label("Status: " + Status, Menu.UiBadgeStyle);
            GUILayout.Label(
                "The camera is resolved only after Replay opens. Native keyframe tracks stay authoritative; missing tracks use RoweMod's session-only Camera Lab keys.",
                Menu.UiMutedWrappedStyle);

            if (changed)
            {
                Config.RequestSave();
                OnSettingsChanged();
            }
        }

        public static void DrawLensControls(string controlPrefix)
        {
            ReplaySettings settings = Config.replaySettings;
            bool changed = false;
            settings.replayFov = DrawSlider("FOV / Long Lens", settings.replayFov, 5f, 120f, controlPrefix + "fov", ref changed);
            DrawTrackLabel("FOV", LensTrack.Fov);
            settings.replayTilt = DrawSlider("Camera Tilt", settings.replayTilt, -180f, 180f, controlPrefix + "tilt", ref changed);
            DrawTrackLabel("Tilt", LensTrack.Tilt);
            settings.replayFisheye = DrawSlider("Fisheye (%)", settings.replayFisheye, 0f, 100f, controlPrefix + "fisheye", ref changed);
            DrawTrackLabel("Fisheye", LensTrack.Fisheye);
            settings.replayVignette = DrawSlider("Vignette", settings.replayVignette, 0f, 1f, controlPrefix + "vignette", ref changed);
            DrawTrackLabel("Vignette", LensTrack.Vignette);

            if (GUILayout.Button("Shake: " + ShakeModeLabel(settings.replayShakeMode), Menu.UiButtonStyle))
            {
                settings.replayShakeMode = (settings.replayShakeMode + 1) % 4;
                changed = true;
            }
            DrawTrackLabel("Shake", LensTrack.Shake);

            if (changed)
                OnLensSettingsChanged();
        }

        public static void DrawDofControls(string controlPrefix)
        {
            ReplaySettings settings = Config.replaySettings;
            bool changed = false;
            bool enabled = settings.replayDofEnabled;
            Menu.ModernToggle("Depth of Field", ref enabled, controlPrefix + "enabled");
            changed |= enabled != settings.replayDofEnabled;
            settings.replayDofEnabled = enabled;
            DrawTrackLabel("DOF State", LensTrack.DofState);

            bool physical = settings.replayDofPhysicallyBased;
            Menu.ModernToggle("Physically Based", ref physical, controlPrefix + "physical");
            changed |= physical != settings.replayDofPhysicallyBased;
            settings.replayDofPhysicallyBased = physical;
            DrawTrackLabel("DOF Mode", LensTrack.DofPhysical);

            settings.replayNearFocusStart = DrawSlider("Near Focus Start", settings.replayNearFocusStart, 0f, 100f, controlPrefix + "near_start", ref changed);
            DrawTrackLabel("Near Start", LensTrack.NearStart);
            settings.replayNearFocusEnd = DrawSlider("Near Focus End", settings.replayNearFocusEnd, 0f, 100f, controlPrefix + "near_end", ref changed);
            DrawTrackLabel("Near End", LensTrack.NearEnd);
            settings.replayFarFocusStart = DrawSlider("Far Focus Start", settings.replayFarFocusStart, 0f, 200f, controlPrefix + "far_start", ref changed);
            DrawTrackLabel("Far Start", LensTrack.FarStart);
            settings.replayFarFocusEnd = DrawSlider("Far Focus End", settings.replayFarFocusEnd, 0f, 200f, controlPrefix + "far_end", ref changed);
            DrawTrackLabel("Far End", LensTrack.FarEnd);

            if (changed)
                OnLensSettingsChanged();
        }

        public static void DrawFramingControls(string controlPrefix)
        {
            ReplaySettings settings = Config.replaySettings;
            bool changed = false;
            if (GUILayout.Button("Framing: " + FramingModeLabel(settings.replayFramingMode), Menu.UiButtonStyle))
            {
                settings.replayFramingMode = (settings.replayFramingMode + 1) % 3;
                changed = true;
            }
            settings.replayMatteOpacity = DrawSlider("Black Matte Opacity", settings.replayMatteOpacity, 0f, 1f, controlPrefix + "opacity", ref changed);
            GUILayout.Label("Framing mattes do not stretch the image or change capture resolution. Framing values use RoweMod keyframes.", Menu.UiMutedWrappedStyle);
            if (changed)
                OnLensSettingsChanged();
        }

        public static void DrawKeyframeControls()
        {
            GUILayout.Label(keyframeStatus, Menu.UiMutedWrappedStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Camera Key", Menu.UiButtonStyle))
                AddKeyAtNeedle();
            if (GUILayout.Button("Delete Key", Menu.UiButtonStyle))
                DeleteKeyAtNeedle();
            if (GUILayout.Button("Delete All", Menu.UiButtonStyle))
                DeleteAllKeys();
            GUILayout.EndHorizontal();
            GUILayout.Label($"RoweMod session keys: {customKeys.Count}/{MaximumCustomKeys}", Menu.UiBadgeStyle);
        }

        public static void DrawPresetControls()
        {
            EnsurePresetCache();
            GUILayout.BeginHorizontal();
            presetName = GUILayout.TextField(presetName ?? string.Empty, 48, Menu.UiSearchFieldStyle, GUILayout.Height(24f));
            if (Menu.PrimaryButton("Save Current", GUILayout.Width(120f), GUILayout.Height(24f)))
            {
                ReplayLensPreset preset = ReplayLensPreset.FromSettings(presetName, Config.replaySettings);
                if (ReplayLensPreset.Save(preset))
                {
                    selectedPreset = preset.name;
                    Config.replaySettings.activeReplayLensPreset = preset.name;
                    RefreshPresetCache();
                    Config.RequestSave();
                }
            }
            GUILayout.EndHorizontal();

            for (int i = 0; i < presetCache.Count; i++)
            {
                string name = presetCache[i];
                GUILayout.BeginHorizontal();
                bool selected = string.Equals(selectedPreset, name, StringComparison.OrdinalIgnoreCase);
                if (GUILayout.Button((selected ? "● " : string.Empty) + name, Menu.UiButtonStyle))
                {
                    ReplayLensPreset preset = ReplayLensPreset.Load(name);
                    if (preset != null)
                    {
                        preset.ApplyTo(Config.replaySettings);
                        selectedPreset = name;
                        presetName = name;
                        OnLensSettingsChanged();
                        OnSettingsChanged();
                    }
                }
                if (selected && GUILayout.Button("Rename", Menu.UiButtonStyle, GUILayout.Width(72f)))
                {
                    if (ReplayLensPreset.Rename(name, presetName))
                    {
                        selectedPreset = presetName.Trim();
                        Config.replaySettings.activeReplayLensPreset = selectedPreset;
                        RefreshPresetCache();
                        Config.RequestSave();
                        GUILayout.EndHorizontal();
                        break;
                    }
                }
                if (GUILayout.Button("Delete", Menu.UiButtonStyle, GUILayout.Width(68f)) && ReplayLensPreset.Delete(name))
                {
                    if (selected)
                    {
                        selectedPreset = string.Empty;
                        Config.replaySettings.activeReplayLensPreset = string.Empty;
                    }
                    RefreshPresetCache();
                    Config.RequestSave();
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawOverlayWindow(int windowId)
        {
            // The generated July 23 wrappers cannot unstrip GUIContent.Temp(Il2CppStringArray),
            // which is used by GUILayout.Toolbar(string[]). Individual buttons avoid that
            // unsupported overload and are allocation-free after style initialization.
            Menu.BeginPanel();
            GUILayout.Label("ROWEMOD REPLAY", Menu.UiMutedStyle);
            Menu.DrawSectionTitle("Camera Lab", "Lens, depth of field, framing, light, and saved presets.");
            GUILayout.Space(4f);

            Menu.BeginToolbar();
            DrawOverlayTabButton("Lens", 0);
            DrawOverlayTabButton("DOF", 1);
            DrawOverlayTabButton("Frame", 2);
            DrawOverlayTabButton("Light", 3);
            DrawOverlayTabButton("Presets", 4);
            Menu.EndToolbar();

            GUILayout.Space(6f);
            Menu.DrawStatusBadge(status, GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);

            overlayScroll = GUILayout.BeginScrollView(overlayScroll);
            Menu.BeginAltPanel();
            if (overlayTab == 0)
                DrawLensControls("replay_overlay_lens_");
            else if (overlayTab == 1)
                DrawDofControls("replay_overlay_dof_");
            else if (overlayTab == 2)
                DrawFramingControls("replay_overlay_frame_");
            else if (overlayTab == 3)
                DrawLightControls("replay_overlay_light_");
            else
                DrawPresetControls();

            Menu.EndPanel();
            GUILayout.Space(8f);

            Menu.BeginAltPanel();
            Menu.DrawSectionTitle("Keyframes", "Replay timeline controls");
            DrawKeyframeControls();
            Menu.EndPanel();
            GUILayout.EndScrollView();
            Menu.EndPanel();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 32f));
        }

        private static void DrawOverlayTabButton(string label, int tab)
        {
            if (Menu.PillButton(label, overlayTab == tab, GUILayout.ExpandWidth(true), GUILayout.Height(25f)))
                overlayTab = tab;
        }

        private static float DrawSlider(string label, float value, float min, float max, string id, ref bool changed)
        {
            float original = value;
            Menu.ModernSlider(label, ref value, min, max, id);
            changed |= !Mathf.Approximately(original, value);
            return value;
        }

        private static void BindCameraLab()
        {
            cameraSettingsControls ??= FindFirstLoaded<CameraSettingsControls>();
            replayEditor ??= FindFirstLoaded<ReplayEditor>();
            keyFrameGroup ??= FindReplayKeyFrameGroup();

            try
            {
                if (!cameraLabAudited && cameraSettingsControls != null && keyFrameGroup != null)
                {
                    AuditNativeTracks();
                    cameraLabAudited = true;
                }
                else if (cameraSettingsControls == null || keyFrameGroup == null)
                {
                    AuditNativeTracks();
                }
                SubscribeKeyframeEvents();

                if (cameraSettingsControls != null)
                {
                    if (lensApplyPending)
                        ApplyLensSettings(Config.replaySettings, false);
                    else if (!nativeValuesCaptured)
                        CaptureNativeLensValues();
                }

                nativeValuesCaptured = true;
                UpdateMatteOverlay(true);
            }
            catch (Exception ex)
            {
                keyframeStatus = "Camera Lab audit failed; light remains available";
                Log.Warning("[ReplayCameraLab] Bind/audit failed: " + ex.Message);
            }
        }

        private static KeyFrameDataGroupBehaviour FindReplayKeyFrameGroup()
        {
            KeyFrameDataGroupBehaviour[] groups = Resources.FindObjectsOfTypeAll<KeyFrameDataGroupBehaviour>();
            if (groups == null)
                return null;

            KeyFrameDataGroupBehaviour best = null;
            int bestScore = -1;
            foreach (KeyFrameDataGroupBehaviour group in groups)
            {
                if (group == null || group.gameObject == null || !group.gameObject.scene.IsValid())
                    continue;

                int score = group.gameObject.activeInHierarchy ? 1 : 0;
                if (cameraSettingsControls != null && group._keyFrameDataBehaviours != null)
                {
                    for (int i = 0; i < group._keyFrameDataBehaviours.Count; i++)
                    {
                        KeyFrameDataSmartFloatBehaviour smartBehaviour =
                            group._keyFrameDataBehaviours[i]?.TryCast<KeyFrameDataSmartFloatBehaviour>();
                        SmartDataFloat smart = smartBehaviour?._smartDataFloat;
                        if (IsCameraSmartData(smart))
                            score += 10;
                    }
                }

                if (score > bestScore)
                {
                    best = group;
                    bestScore = score;
                }
            }

            return best;
        }

        private static bool IsCameraSmartData(SmartDataFloat smart)
        {
            if (smart == null || cameraSettingsControls == null)
                return false;
            return SameSmart(smart, cameraSettingsControls._fovSmartData) ||
                   SameSmart(smart, cameraSettingsControls._tiltAngleSmartData) ||
                   SameSmart(smart, cameraSettingsControls._fisheyeAmountSmartData) ||
                   SameSmart(smart, cameraSettingsControls._vignetteIntensitySmartData) ||
                   SameSmart(smart, cameraSettingsControls._cameraShakeSmartData) ||
                   SameSmart(smart, cameraSettingsControls._DOFStateSmartData) ||
                   SameSmart(smart, cameraSettingsControls._nearFocusStartSmartData) ||
                   SameSmart(smart, cameraSettingsControls._nearFocusEndSmartData) ||
                   SameSmart(smart, cameraSettingsControls._farFocusStartSmartData) ||
                   SameSmart(smart, cameraSettingsControls._farFocusEndSmartData) ||
                   SameSmart(smart, cameraSettingsControls._DOFPhysicallyBasedSmartData);
        }

        private static bool SameSmart(SmartDataFloat a, SmartDataFloat b) =>
            a != null && b != null && a.Pointer == b.Pointer;

        private static void UnbindCameraLab()
        {
            if (keyFrameGroup != null && keysUpdatedHandler != null)
            {
                try { keyFrameGroup.remove_OnKeysUpdated(keysUpdatedHandler); }
                catch { }
            }

            keyFrameGroup = null;
            cameraSettingsControls = null;
            replayEditor = null;
            keysUpdatedHandler = null;
            Array.Clear(nativeLensTracks, 0, nativeLensTracks.Length);
            knownNativeKeyTimes.Clear();
            customKeys.Clear();
            customKeyCursor = 0;
            lastTimelineTime = float.NaN;
            nativeValuesCaptured = false;
            cameraLabAudited = false;
            lastReplayEditorStateHash = 0;
            previousReplaySettingsVisible = false;
            nextReplayModeCheckTime = 0f;
            keyframeStatus = "Open Replay to audit camera tracks";
        }

        private static void SubscribeKeyframeEvents()
        {
            if (keyFrameGroup == null || keysUpdatedHandler != null)
                return;

            try
            {
                keysUpdatedHandler = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(HandleNativeKeysUpdated);
                keyFrameGroup.add_OnKeysUpdated(keysUpdatedHandler);
                ReadNativeKeyTimes(null);
            }
            catch (Exception ex)
            {
                keysUpdatedHandler = null;
                Log.Warning("[ReplayCameraLab] Could not subscribe to native key updates: " + ex.Message);
            }
        }

        private static void AuditNativeTracks()
        {
            Array.Clear(nativeLensTracks, 0, nativeLensTracks.Length);
            if (cameraSettingsControls == null || keyFrameGroup == null || keyFrameGroup._keyFrameDataBehaviours == null)
            {
                keyframeStatus = cameraSettingsControls == null
                    ? "Camera settings unavailable - light and framing can still use RoweMod keys"
                    : "Native keyframe group unavailable - Camera Lab keys only";
                return;
            }

            for (int i = 0; i < keyFrameGroup._keyFrameDataBehaviours.Count; i++)
            {
                KeyFrameDataSmartFloatBehaviour smartBehaviour =
                    keyFrameGroup._keyFrameDataBehaviours[i]?.TryCast<KeyFrameDataSmartFloatBehaviour>();
                if (smartBehaviour == null || smartBehaviour._smartDataFloat == null)
                    continue;

                SmartDataFloat smart = smartBehaviour._smartDataFloat;
                MarkNativeTrack(LensTrack.Fov, smart, cameraSettingsControls._fovSmartData);
                MarkNativeTrack(LensTrack.Tilt, smart, cameraSettingsControls._tiltAngleSmartData);
                MarkNativeTrack(LensTrack.Fisheye, smart, cameraSettingsControls._fisheyeAmountSmartData);
                MarkNativeTrack(LensTrack.Vignette, smart, cameraSettingsControls._vignetteIntensitySmartData);
                MarkNativeTrack(LensTrack.Shake, smart, cameraSettingsControls._cameraShakeSmartData);
                MarkNativeTrack(LensTrack.DofState, smart, cameraSettingsControls._DOFStateSmartData);
                MarkNativeTrack(LensTrack.NearStart, smart, cameraSettingsControls._nearFocusStartSmartData);
                MarkNativeTrack(LensTrack.NearEnd, smart, cameraSettingsControls._nearFocusEndSmartData);
                MarkNativeTrack(LensTrack.FarStart, smart, cameraSettingsControls._farFocusStartSmartData);
                MarkNativeTrack(LensTrack.FarEnd, smart, cameraSettingsControls._farFocusEndSmartData);
                MarkNativeTrack(LensTrack.DofPhysical, smart, cameraSettingsControls._DOFPhysicallyBasedSmartData);
            }

            int nativeCount = 0;
            for (int i = 0; i < nativeLensTracks.Length; i++)
                if (nativeLensTracks[i]) nativeCount++;
            keyframeStatus = $"Native camera tracks: {nativeCount}/{(int)LensTrack.Count}. Missing lens tracks, framing, and light use RoweMod session keys.";
            Log.Msg("[ReplayCameraLab] " + keyframeStatus);
        }

        private static void MarkNativeTrack(LensTrack track, SmartDataFloat candidate, SmartDataFloat target)
        {
            if (SameSmart(candidate, target))
                nativeLensTracks[(int)track] = true;
        }

        private static void HandleNativeKeysUpdated()
        {
            try
            {
                HashSet<int> currentTimes = new HashSet<int>();
                ReadNativeKeyTimes(currentTimes);
                float needle = CurrentTimelineTime();
                int needleKey = QuantizeTime(needle);
                bool added = false;

                foreach (int timeKey in currentTimes)
                {
                    if (knownNativeKeyTimes.Contains(timeKey))
                        continue;
                    AddOrReplaceCustomKey(timeKey / 1000f);
                    added = true;
                }

                if (!added && currentTimes.Contains(needleKey))
                    AddOrReplaceCustomKey(needle);

                for (int i = customKeys.Count - 1; i >= 0; i--)
                {
                    if (!currentTimes.Contains(QuantizeTime(customKeys[i].time)))
                        customKeys.RemoveAt(i);
                }

                knownNativeKeyTimes.Clear();
                foreach (int timeKey in currentTimes)
                    knownNativeKeyTimes.Add(timeKey);
                customKeyCursor = 0;
            }
            catch (Exception ex)
            {
                Log.Warning("[ReplayCameraLab] Could not mirror native key update: " + ex.Message);
            }
        }

        private static void ReadNativeKeyTimes(HashSet<int> destination)
        {
            if (keyFrameGroup == null)
                return;

            Il2CppSystem.Collections.Generic.List<float> timestamps = keyFrameGroup.GetAllTimeStamps();
            if (timestamps == null)
                return;

            for (int i = 0; i < timestamps.Count; i++)
            {
                int key = QuantizeTime(timestamps[i]);
                if (destination != null)
                    destination.Add(key);
                else
                    knownNativeKeyTimes.Add(key);
            }
        }

        private static void AddKeyAtNeedle()
        {
            if (!replayActive)
                return;

            float time = CurrentTimelineTime();
            if (keyFrameGroup != null)
            {
                keyFrameGroup.KeyFrameData();
                // OnKeysUpdated normally captures this. Replacing here is harmless and also
                // covers game builds that do not raise the callback on overwrite.
                AddOrReplaceCustomKey(time);
                knownNativeKeyTimes.Add(QuantizeTime(time));
            }
            else
            {
                AddOrReplaceCustomKey(time);
            }
        }

        private static void DeleteKeyAtNeedle()
        {
            if (!replayActive)
                return;

            float time = CurrentTimelineTime();
            if (keyFrameGroup != null)
                keyFrameGroup.DeleteKeysInRange(time - KeyTimeEpsilon, time + KeyTimeEpsilon);

            RemoveCustomKeysInRange(time - KeyTimeEpsilon, time + KeyTimeEpsilon);
        }

        private static void DeleteAllKeys()
        {
            if (!replayActive)
                return;
            if (keyFrameGroup != null)
                keyFrameGroup.DeleteAllKeys();
            customKeys.Clear();
            knownNativeKeyTimes.Clear();
            customKeyCursor = 0;
        }

        private static void RemoveCustomKeysInRange(float min, float max)
        {
            for (int i = customKeys.Count - 1; i >= 0; i--)
            {
                if (customKeys[i].time >= min && customKeys[i].time <= max)
                    customKeys.RemoveAt(i);
            }
            customKeyCursor = 0;
        }

        private static void AddOrReplaceCustomKey(float time)
        {
            // Capture the live SmartData values too, so keys authored through the native
            // replay settings UI are mirrored accurately instead of using stale RoweMod UI data.
            if (cameraSettingsControls != null)
                CaptureNativeLensValues();
            CameraLabSnapshot snapshot = CaptureSnapshot(time);
            for (int i = 0; i < customKeys.Count; i++)
            {
                if (Mathf.Abs(customKeys[i].time - time) <= KeyTimeEpsilon)
                {
                    customKeys[i] = snapshot;
                    return;
                }
                if (customKeys[i].time > time)
                {
                    if (customKeys.Count >= MaximumCustomKeys)
                        return;
                    customKeys.Insert(i, snapshot);
                    return;
                }
            }

            if (customKeys.Count < MaximumCustomKeys)
                customKeys.Add(snapshot);
        }

        private static CameraLabSnapshot CaptureSnapshot(float time)
        {
            ReplaySettings s = Config.replaySettings;
            return new CameraLabSnapshot
            {
                time = time,
                fov = s.replayFov,
                tilt = s.replayTilt,
                fisheye = s.replayFisheye,
                vignette = s.replayVignette,
                shakeMode = s.replayShakeMode,
                dofEnabled = s.replayDofEnabled,
                dofPhysical = s.replayDofPhysicallyBased,
                nearStart = s.replayNearFocusStart,
                nearEnd = s.replayNearFocusEnd,
                farStart = s.replayFarFocusStart,
                farEnd = s.replayFarFocusEnd,
                framingMode = s.replayFramingMode,
                matteOpacity = s.replayMatteOpacity,
                lightEnabled = s.cameraLightEnabled,
                lightType = s.cameraLightType,
                lightIntensity = s.cameraLightIntensity,
                lightRange = s.cameraLightRange,
                lightSpotAngle = s.cameraLightSpotAngle,
                lightR = s.cameraLightColorR,
                lightG = s.cameraLightColorG,
                lightB = s.cameraLightColorB,
                lightX = s.cameraLightOffsetX,
                lightY = s.cameraLightOffsetY,
                lightZ = s.cameraLightOffsetZ,
                lightPitch = s.cameraLightPitch,
                lightYaw = s.cameraLightYaw,
                lightRoll = s.cameraLightRoll,
                lightShadows = s.cameraLightShadows,
                lightSoftShadows = s.cameraLightSoftShadows,
                lightShadowResolution = s.cameraLightShadowResolution,
                lightShadowStrength = s.cameraLightShadowStrength,
                lightShadowBias = s.cameraLightShadowBias,
                lightShadowNormalBias = s.cameraLightShadowNormalBias,
                lightShadowNearPlane = s.cameraLightShadowNearPlane
            };
        }

        private static void ApplyCustomTrack(float time)
        {
            if (customKeys.Count == 0)
                return;

            if (time <= customKeys[0].time)
            {
                ApplySnapshot(customKeys[0]);
                customKeyCursor = 0;
                return;
            }

            int lastIndex = customKeys.Count - 1;
            if (time >= customKeys[lastIndex].time)
            {
                ApplySnapshot(customKeys[lastIndex]);
                customKeyCursor = Mathf.Max(0, lastIndex - 1);
                return;
            }

            if (customKeyCursor >= lastIndex || time < customKeys[customKeyCursor].time)
                customKeyCursor = FindKeyInterval(time);
            else
                while (customKeyCursor + 1 < lastIndex && time > customKeys[customKeyCursor + 1].time)
                    customKeyCursor++;

            CameraLabSnapshot from = customKeys[customKeyCursor];
            CameraLabSnapshot to = customKeys[customKeyCursor + 1];
            float t = Mathf.InverseLerp(from.time, to.time, time);
            ApplySnapshot(LerpSnapshot(from, to, t));
        }

        private static int FindKeyInterval(float time)
        {
            int low = 0;
            int high = customKeys.Count - 2;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (time < customKeys[mid].time)
                    high = mid - 1;
                else if (time > customKeys[mid + 1].time)
                    low = mid + 1;
                else
                    return mid;
            }
            return Mathf.Clamp(low, 0, customKeys.Count - 2);
        }

        private static CameraLabSnapshot LerpSnapshot(CameraLabSnapshot a, CameraLabSnapshot b, float t)
        {
            CameraLabSnapshot value = a;
            value.time = Mathf.Lerp(a.time, b.time, t);
            value.fov = Mathf.Lerp(a.fov, b.fov, t);
            value.tilt = Mathf.LerpAngle(a.tilt, b.tilt, t);
            value.fisheye = Mathf.Lerp(a.fisheye, b.fisheye, t);
            value.vignette = Mathf.Lerp(a.vignette, b.vignette, t);
            value.nearStart = Mathf.Lerp(a.nearStart, b.nearStart, t);
            value.nearEnd = Mathf.Lerp(a.nearEnd, b.nearEnd, t);
            value.farStart = Mathf.Lerp(a.farStart, b.farStart, t);
            value.farEnd = Mathf.Lerp(a.farEnd, b.farEnd, t);
            value.matteOpacity = Mathf.Lerp(a.matteOpacity, b.matteOpacity, t);
            value.lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t);
            value.lightRange = Mathf.Lerp(a.lightRange, b.lightRange, t);
            value.lightSpotAngle = Mathf.Lerp(a.lightSpotAngle, b.lightSpotAngle, t);
            value.lightR = Mathf.Lerp(a.lightR, b.lightR, t);
            value.lightG = Mathf.Lerp(a.lightG, b.lightG, t);
            value.lightB = Mathf.Lerp(a.lightB, b.lightB, t);
            value.lightX = Mathf.Lerp(a.lightX, b.lightX, t);
            value.lightY = Mathf.Lerp(a.lightY, b.lightY, t);
            value.lightZ = Mathf.Lerp(a.lightZ, b.lightZ, t);
            value.lightPitch = Mathf.LerpAngle(a.lightPitch, b.lightPitch, t);
            value.lightYaw = Mathf.LerpAngle(a.lightYaw, b.lightYaw, t);
            value.lightRoll = Mathf.LerpAngle(a.lightRoll, b.lightRoll, t);
            value.lightShadowStrength = Mathf.Lerp(a.lightShadowStrength, b.lightShadowStrength, t);
            value.lightShadowBias = Mathf.Lerp(a.lightShadowBias, b.lightShadowBias, t);
            value.lightShadowNormalBias = Mathf.Lerp(a.lightShadowNormalBias, b.lightShadowNormalBias, t);
            value.lightShadowNearPlane = Mathf.Lerp(a.lightShadowNearPlane, b.lightShadowNearPlane, t);
            return value;
        }

        private static void ApplySnapshot(CameraLabSnapshot value)
        {
            ReplaySettings s = Config.replaySettings;
            if (!IsNativeTrack(LensTrack.Fov)) s.replayFov = value.fov;
            if (!IsNativeTrack(LensTrack.Tilt)) s.replayTilt = value.tilt;
            if (!IsNativeTrack(LensTrack.Fisheye)) s.replayFisheye = value.fisheye;
            if (!IsNativeTrack(LensTrack.Vignette)) s.replayVignette = value.vignette;
            if (!IsNativeTrack(LensTrack.Shake)) s.replayShakeMode = value.shakeMode;
            if (!IsNativeTrack(LensTrack.DofState)) s.replayDofEnabled = value.dofEnabled;
            if (!IsNativeTrack(LensTrack.DofPhysical)) s.replayDofPhysicallyBased = value.dofPhysical;
            if (!IsNativeTrack(LensTrack.NearStart)) s.replayNearFocusStart = value.nearStart;
            if (!IsNativeTrack(LensTrack.NearEnd)) s.replayNearFocusEnd = value.nearEnd;
            if (!IsNativeTrack(LensTrack.FarStart)) s.replayFarFocusStart = value.farStart;
            if (!IsNativeTrack(LensTrack.FarEnd)) s.replayFarFocusEnd = value.farEnd;
            s.replayFramingMode = value.framingMode;
            s.replayMatteOpacity = value.matteOpacity;
            s.cameraLightEnabled = value.lightEnabled;
            s.cameraLightType = value.lightType;
            s.cameraLightIntensity = value.lightIntensity;
            s.cameraLightRange = value.lightRange;
            s.cameraLightSpotAngle = value.lightSpotAngle;
            s.cameraLightColorR = value.lightR;
            s.cameraLightColorG = value.lightG;
            s.cameraLightColorB = value.lightB;
            s.cameraLightOffsetX = value.lightX;
            s.cameraLightOffsetY = value.lightY;
            s.cameraLightOffsetZ = value.lightZ;
            s.cameraLightPitch = value.lightPitch;
            s.cameraLightYaw = value.lightYaw;
            s.cameraLightRoll = value.lightRoll;
            s.cameraLightShadows = value.lightShadows;
            s.cameraLightSoftShadows = value.lightSoftShadows;
            s.cameraLightShadowResolution = value.lightShadowResolution;
            s.cameraLightShadowStrength = value.lightShadowStrength;
            s.cameraLightShadowBias = value.lightShadowBias;
            s.cameraLightShadowNormalBias = value.lightShadowNormalBias;
            s.cameraLightShadowNearPlane = value.lightShadowNearPlane;
            Config.NormalizeReplaySettings(s);
            ApplyLensSettings(s, true);
            ApplySettings();
            UpdateMatteOverlay(false);
        }

        private static void DetectReplayEditorModeChange(float timelineTime)
        {
            Animator animator = replayEditor?._stateMachine;
            if (animator == null || !animator.isActiveAndEnabled)
                return;

            int currentHash;
            try
            {
                currentHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            }
            catch
            {
                return;
            }

            if (currentHash == 0)
                return;
            if (lastReplayEditorStateHash != 0 && currentHash != lastReplayEditorStateHash)
                InheritPreviousKeyForEditing(timelineTime, "replay editor mode changed");
            lastReplayEditorStateHash = currentHash;
        }

        private static void InheritPreviousKeyForEditing(float timelineTime, string reason)
        {
            if (customKeys.Count == 0)
                return;

            int previousIndex = -1;
            for (int i = customKeys.Count - 1; i >= 0; i--)
            {
                if (customKeys[i].time <= timelineTime + KeyTimeEpsilon)
                {
                    previousIndex = i;
                    break;
                }
            }

            if (previousIndex < 0)
                return;

            CameraLabSnapshot value = customKeys[previousIndex];
            ApplySnapshot(value);

            // Apply native-tracked lens values once as well. This specifically counters the
            // game's ChangeMode initialization resetting FOV/DOF before the next key is edited;
            // normal playback remains native-authoritative after this one-shot inheritance.
            ReplaySettings s = Config.replaySettings;
            s.replayFov = value.fov;
            s.replayTilt = value.tilt;
            s.replayFisheye = value.fisheye;
            s.replayVignette = value.vignette;
            s.replayShakeMode = value.shakeMode;
            s.replayDofEnabled = value.dofEnabled;
            s.replayDofPhysicallyBased = value.dofPhysical;
            s.replayNearFocusStart = value.nearStart;
            s.replayNearFocusEnd = value.nearEnd;
            s.replayFarFocusStart = value.farStart;
            s.replayFarFocusEnd = value.farEnd;
            Config.NormalizeReplaySettings(s);
            ApplyLensSettings(s, false);
            lastTimelineTime = timelineTime;
            Log.Msg($"[ReplayCameraLab] Inherited key at {value.time:0.###}s for {reason} at {timelineTime:0.###}s.");
        }

        private static bool IsNativeTrack(LensTrack track) => nativeLensTracks[(int)track];

        private static void CaptureNativeLensValues()
        {
            ReplaySettings s = Config.replaySettings;
            s.replayFov = ReadSmart(cameraSettingsControls._fovSmartData, cameraSettingsControls.fov);
            s.replayTilt = ReadSmart(cameraSettingsControls._tiltAngleSmartData, cameraSettingsControls.tiltAngle);
            s.replayFisheye = ReadSmart(cameraSettingsControls._fisheyeAmountSmartData, cameraSettingsControls.fisheyeAmount);
            s.replayVignette = ReadSmart(cameraSettingsControls._vignetteIntensitySmartData, cameraSettingsControls.vignetteIntensity);
            s.replayShakeMode = Mathf.RoundToInt(ReadSmart(cameraSettingsControls._cameraShakeSmartData, 0f));
            s.replayDofEnabled = ReadSmart(cameraSettingsControls._DOFStateSmartData, 0f) >= 0.5f;
            s.replayDofPhysicallyBased = ReadSmart(cameraSettingsControls._DOFPhysicallyBasedSmartData, 0f) >= 0.5f;
            s.replayNearFocusStart = ReadSmart(cameraSettingsControls._nearFocusStartSmartData, cameraSettingsControls.nearFocusStart);
            s.replayNearFocusEnd = ReadSmart(cameraSettingsControls._nearFocusEndSmartData, cameraSettingsControls.nearFocusEnd);
            s.replayFarFocusStart = ReadSmart(cameraSettingsControls._farFocusStartSmartData, cameraSettingsControls.farFocusStart);
            s.replayFarFocusEnd = ReadSmart(cameraSettingsControls._farFocusEndSmartData, cameraSettingsControls.farFocusEnd);
            Config.NormalizeReplaySettings(s);
        }

        private static void SyncNativeTrackedValues()
        {
            if (cameraSettingsControls == null)
                return;

            ReplaySettings s = Config.replaySettings;
            if (IsNativeTrack(LensTrack.Fov)) s.replayFov = ReadSmart(cameraSettingsControls._fovSmartData, s.replayFov);
            if (IsNativeTrack(LensTrack.Tilt)) s.replayTilt = ReadSmart(cameraSettingsControls._tiltAngleSmartData, s.replayTilt);
            if (IsNativeTrack(LensTrack.Fisheye)) s.replayFisheye = ReadSmart(cameraSettingsControls._fisheyeAmountSmartData, s.replayFisheye);
            if (IsNativeTrack(LensTrack.Vignette)) s.replayVignette = ReadSmart(cameraSettingsControls._vignetteIntensitySmartData, s.replayVignette);
            if (IsNativeTrack(LensTrack.Shake)) s.replayShakeMode = Mathf.RoundToInt(ReadSmart(cameraSettingsControls._cameraShakeSmartData, s.replayShakeMode));
            if (IsNativeTrack(LensTrack.DofState)) s.replayDofEnabled = ReadSmart(cameraSettingsControls._DOFStateSmartData, s.replayDofEnabled ? 1f : 0f) >= 0.5f;
            if (IsNativeTrack(LensTrack.DofPhysical)) s.replayDofPhysicallyBased = ReadSmart(cameraSettingsControls._DOFPhysicallyBasedSmartData, s.replayDofPhysicallyBased ? 1f : 0f) >= 0.5f;
            if (IsNativeTrack(LensTrack.NearStart)) s.replayNearFocusStart = ReadSmart(cameraSettingsControls._nearFocusStartSmartData, s.replayNearFocusStart);
            if (IsNativeTrack(LensTrack.NearEnd)) s.replayNearFocusEnd = ReadSmart(cameraSettingsControls._nearFocusEndSmartData, s.replayNearFocusEnd);
            if (IsNativeTrack(LensTrack.FarStart)) s.replayFarFocusStart = ReadSmart(cameraSettingsControls._farFocusStartSmartData, s.replayFarFocusStart);
            if (IsNativeTrack(LensTrack.FarEnd)) s.replayFarFocusEnd = ReadSmart(cameraSettingsControls._farFocusEndSmartData, s.replayFarFocusEnd);
            Config.NormalizeReplaySettings(s);
        }

        private static void ApplyLensSettings(ReplaySettings s, bool fallbackOnly)
        {
            if (cameraSettingsControls == null)
                return;

            SetLensValue(LensTrack.Fov, cameraSettingsControls._fovSmartData, s.replayFov, fallbackOnly, value => cameraSettingsControls.fov = value);
            SetLensValue(LensTrack.Tilt, cameraSettingsControls._tiltAngleSmartData, s.replayTilt, fallbackOnly, value => cameraSettingsControls.tiltAngle = value);
            SetLensValue(LensTrack.Fisheye, cameraSettingsControls._fisheyeAmountSmartData, s.replayFisheye, fallbackOnly, value => cameraSettingsControls.fisheyeAmount = value);
            SetLensValue(LensTrack.Vignette, cameraSettingsControls._vignetteIntensitySmartData, s.replayVignette, fallbackOnly, value => cameraSettingsControls.vignetteIntensity = value);
            SetLensValue(LensTrack.Shake, cameraSettingsControls._cameraShakeSmartData, s.replayShakeMode, fallbackOnly);
            SetLensValue(LensTrack.DofState, cameraSettingsControls._DOFStateSmartData, s.replayDofEnabled ? 1f : 0f, fallbackOnly);
            SetLensValue(LensTrack.DofPhysical, cameraSettingsControls._DOFPhysicallyBasedSmartData, s.replayDofPhysicallyBased ? 1f : 0f, fallbackOnly);
            SetLensValue(LensTrack.NearStart, cameraSettingsControls._nearFocusStartSmartData, s.replayNearFocusStart, fallbackOnly);
            SetLensValue(LensTrack.NearEnd, cameraSettingsControls._nearFocusEndSmartData, s.replayNearFocusEnd, fallbackOnly);
            SetLensValue(LensTrack.FarStart, cameraSettingsControls._farFocusStartSmartData, s.replayFarFocusStart, fallbackOnly);
            SetLensValue(LensTrack.FarEnd, cameraSettingsControls._farFocusEndSmartData, s.replayFarFocusEnd, fallbackOnly);
            lensApplyPending = false;
        }

        private static void SetLensValue(
            LensTrack track,
            SmartDataFloat smart,
            float value,
            bool fallbackOnly,
            Action<float> applyImmediate = null)
        {
            if (smart == null || (fallbackOnly && IsNativeTrack(track)))
                return;
            smart.Value = value;
            applyImmediate?.Invoke(value);
        }

        private static float ReadSmart(SmartDataFloat smart, float fallback)
        {
            if (smart == null)
                return fallback;
            float value = smart.Value;
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static float CurrentTimelineTime()
        {
            GameReplaySystem replay = GameReplaySystem.Instance;
            if (replay == null)
                return 0f;
            float time = replay.TimelineTime;
            return float.IsNaN(time) || float.IsInfinity(time) ? 0f : Mathf.Max(0f, time);
        }

        private static int QuantizeTime(float time) => Mathf.RoundToInt(time * 1000f);

        private static void UpdateMatteOverlay(bool force)
        {
            ReplaySettings s = Config.replaySettings;
            bool visible = replayActive && s.replayFramingMode != 0 && s.replayMatteOpacity > 0.001f;
            if (!visible)
            {
                if (matteObject != null)
                    matteObject.SetActive(false);
                return;
            }

            EnsureMatteOverlay();
            if (matteObject == null || matteImages == null)
                return;
            matteObject.SetActive(true);

            if (!force && matteScreenWidth == Screen.width && matteScreenHeight == Screen.height &&
                matteMode == s.replayFramingMode && Mathf.Approximately(matteOpacity, s.replayMatteOpacity))
                return;

            matteScreenWidth = Screen.width;
            matteScreenHeight = Screen.height;
            matteMode = s.replayFramingMode;
            matteOpacity = s.replayMatteOpacity;
            float targetAspect = s.replayFramingMode == 2 ? 4f / 3f : 16f / 9f;
            float screenAspect = Screen.height <= 0 ? targetAspect : Screen.width / (float)Screen.height;
            float xMin = 0f, xMax = 1f, yMin = 0f, yMax = 1f;
            if (screenAspect > targetAspect)
            {
                float contentWidth = targetAspect / screenAspect;
                xMin = (1f - contentWidth) * 0.5f;
                xMax = 1f - xMin;
            }
            else if (screenAspect < targetAspect)
            {
                float contentHeight = screenAspect / targetAspect;
                yMin = (1f - contentHeight) * 0.5f;
                yMax = 1f - yMin;
            }

            Color color = new Color(0f, 0f, 0f, s.replayMatteOpacity);
            SetMatte(matteImages[0], 0f, 0f, xMin, 1f, color);
            SetMatte(matteImages[1], xMax, 0f, 1f, 1f, color);
            SetMatte(matteImages[2], xMin, 0f, xMax, yMin, color);
            SetMatte(matteImages[3], xMin, yMax, xMax, 1f, color);
        }

        private static void EnsureMatteOverlay()
        {
            if (matteObject != null)
                return;

            matteObject = new GameObject(MatteObjectName) { hideFlags = HideFlags.HideAndDontSave };
            Canvas canvas = matteObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            matteImages = new Image[4];
            for (int i = 0; i < matteImages.Length; i++)
            {
                GameObject bar = new GameObject("Matte " + i) { hideFlags = HideFlags.HideAndDontSave };
                bar.transform.SetParent(matteObject.transform, false);
                matteImages[i] = bar.AddComponent<Image>();
                matteImages[i].raycastTarget = false;
            }
        }

        private static void SetMatte(Image image, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            image.color = color;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void DestroyMatteOverlay()
        {
            if (matteObject != null)
                UnityObject.Destroy(matteObject);
            matteObject = null;
            matteImages = null;
            matteScreenWidth = 0;
            matteScreenHeight = 0;
            matteMode = -1;
            matteOpacity = -1f;
        }

        private static void DrawTrackLabel(string label, LensTrack track)
        {
            string capability;
            if (cameraSettingsControls == null)
                capability = replayActive ? "Unavailable" : "Open Replay to audit";
            else if (GetTrackSmartData(track) == null)
                capability = "Unavailable";
            else
                capability = IsNativeTrack(track) ? "Native Keyframes" : "RoweMod Keyframes";
            GUILayout.Label(label + ": " + capability, Menu.UiMutedWrappedStyle);
        }

        private static SmartDataFloat GetTrackSmartData(LensTrack track)
        {
            if (cameraSettingsControls == null)
                return null;
            return track switch
            {
                LensTrack.Fov => cameraSettingsControls._fovSmartData,
                LensTrack.Tilt => cameraSettingsControls._tiltAngleSmartData,
                LensTrack.Fisheye => cameraSettingsControls._fisheyeAmountSmartData,
                LensTrack.Vignette => cameraSettingsControls._vignetteIntensitySmartData,
                LensTrack.Shake => cameraSettingsControls._cameraShakeSmartData,
                LensTrack.DofState => cameraSettingsControls._DOFStateSmartData,
                LensTrack.NearStart => cameraSettingsControls._nearFocusStartSmartData,
                LensTrack.NearEnd => cameraSettingsControls._nearFocusEndSmartData,
                LensTrack.FarStart => cameraSettingsControls._farFocusStartSmartData,
                LensTrack.FarEnd => cameraSettingsControls._farFocusEndSmartData,
                LensTrack.DofPhysical => cameraSettingsControls._DOFPhysicallyBasedSmartData,
                _ => null
            };
        }

        private static string ShakeModeLabel(int mode) => mode switch
        {
            1 => "Low",
            2 => "Medium",
            3 => "High",
            _ => "Off"
        };

        private static string FramingModeLabel(int mode) => mode switch
        {
            1 => "16:9 Mattes",
            2 => "4:3 Mattes",
            _ => "Off"
        };

        private static void EnsurePresetCache()
        {
            if (!presetCacheLoaded)
                RefreshPresetCache();
            if (string.IsNullOrEmpty(selectedPreset))
                selectedPreset = Config.replaySettings.activeReplayLensPreset ?? string.Empty;
            if (string.IsNullOrEmpty(presetName))
                presetName = selectedPreset;
        }

        private static void RefreshPresetCache()
        {
            presetCache.Clear();
            presetCache.AddRange(ReplayLensPreset.GetAvailablePresets());
            presetCacheLoaded = true;
        }

        private static void BindReplayObjects()
        {
            if (recordableCamera == null)
                recordableCamera = FindFirstLoaded<RecordableCamera>();
            if (freeCam == null)
                freeCam = FindFirstLoaded<FreeCam>();
            if (keyframeCamera == null)
                keyframeCamera = FindFirstLoaded<PlayableKeyFrameDataReplayCameraTransformBehaviour>();
            if (cinematicsBrain == null)
                cinematicsBrain = FindFirstLoaded<CinemachineBrain>();
            if (replayOutputCamera == null && cinematicsBrain != null)
                cinematicsBrain.gameObject.TryGetComponent(out replayOutputCamera);

            freeCamCollider = freeCam == null ? null : freeCam._thisCollider;
            RebindTargetTransform();
        }

        private static void RebindTargetTransform()
        {
            Transform keyframedTransform = keyframeCamera == null ? null : keyframeCamera._animatedCameraTrans;
            if (IsUsableTransform(keyframedTransform))
            {
                SetTargetTransform(keyframedTransform, "keyframed replay camera");
                return;
            }

            targetTransform = null;
        }

        private static void SetTargetTransform(Transform target, string label)
        {
            if (targetTransform == target)
                return;

            targetTransform = target;
            if (lightObject != null)
                lightObject.transform.SetParent(targetTransform, false);
            Log.Msg("[ReplayLight] Bound to " + label + ".");
        }

        private static void ApplySettings()
        {
            ReplaySettings settings = Config.replaySettings;
            Config.NormalizeReplaySettings(settings);
            ApplyCollisionSettings();

            if (!settings.cameraLightEnabled)
            {
                SetLightEnabled(false);
                status = "Off";
                return;
            }

            if (!IsUsableTransform(targetTransform))
            {
                status = "Replay camera unavailable";
                return;
            }

            if (!EnsureLight())
            {
                status = "Unable to create camera light";
                return;
            }

            lightObject.transform.SetParent(targetTransform, false);
            lightObject.transform.localPosition = new Vector3(
                settings.cameraLightOffsetX,
                settings.cameraLightOffsetY,
                settings.cameraLightOffsetZ);
            lightObject.transform.localRotation = Quaternion.Euler(
                settings.cameraLightPitch,
                settings.cameraLightYaw,
                settings.cameraLightRoll);

            lightComponent.type = settings.cameraLightType == 1 ? LightType.Point : LightType.Spot;
            lightComponent.enabled = true;
            lightComponent.renderMode = LightRenderMode.ForcePixel;
            lightComponent.intensity = settings.cameraLightIntensity;
            lightComponent.range = settings.cameraLightRange;
            lightComponent.spotAngle = settings.cameraLightSpotAngle;
            lightComponent.color = new Color(
                settings.cameraLightColorR,
                settings.cameraLightColorG,
                settings.cameraLightColorB,
                1f);
            lightComponent.shadows = !settings.cameraLightShadows
                ? LightShadows.None
                : settings.cameraLightSoftShadows ? LightShadows.Soft : LightShadows.Hard;
            lightComponent.shadowResolution = ToShadowResolution(settings.cameraLightShadowResolution);
            lightComponent.shadowStrength = settings.cameraLightShadowStrength;
            lightComponent.shadowBias = settings.cameraLightShadowBias;
            lightComponent.shadowNormalBias = settings.cameraLightShadowNormalBias;
            lightComponent.shadowNearPlane = settings.cameraLightShadowNearPlane;
            lightComponent.bounceIntensity = 0f;
            status = "Attached to " + (targetTransform == replayOutputCamera?.transform
                ? "Cinemachine replay camera"
                : "keyframed replay camera");
        }

        private static void ApplyCollisionSettings()
        {
            if (freeCamCollider == null && freeCam != null)
                freeCamCollider = freeCam._thisCollider;

            if (freeCamCollider == null)
                return;

            if (Config.replaySettings.replayCameraNoCollision)
            {
                if (!freeCamColliderStateCaptured)
                {
                    originalFreeCamColliderEnabled = freeCamCollider.enabled;
                    freeCamColliderStateCaptured = true;
                }

                freeCamCollider.enabled = false;
                return;
            }

            RestoreFreeCamCollision();
        }

        private static void RestoreFreeCamCollision()
        {
            if (freeCamColliderStateCaptured && freeCamCollider != null)
                freeCamCollider.enabled = originalFreeCamColliderEnabled;

            freeCamColliderStateCaptured = false;
            freeCamCollider = null;
        }

        private static bool EnsureLight()
        {
            if (lightObject == null)
            {
                lightObject = new GameObject(LightObjectName)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                lightComponent = lightObject.AddComponent<Light>();
            }

            if (lightComponent == null)
                lightComponent = lightObject.GetComponent<Light>();

            return lightComponent != null;
        }

        private static void DestroyLight()
        {
            if (lightObject != null)
                UnityObject.Destroy(lightObject);

            lightObject = null;
            lightComponent = null;
        }

        private static void SetLightEnabled(bool enabled)
        {
            if (lightComponent != null)
                lightComponent.enabled = enabled;
        }

        private static void ClearCachedObjects()
        {
            recordableCamera = null;
            freeCam = null;
            keyframeCamera = null;
            cinematicsBrain = null;
            replayOutputCamera = null;
            targetTransform = null;
        }

        private static T FindFirstLoaded<T>() where T : UnityObject
        {
            T[] found = Resources.FindObjectsOfTypeAll<T>();
            if (found == null)
                return null;

            foreach (T candidate in found)
            {
                Component component = candidate as Component;
                if (component != null && component.gameObject != null && component.gameObject.scene.IsValid())
                    return candidate;
            }

            return null;
        }

        private static bool IsUsableTransform(Transform transform)
        {
            return transform != null && transform.gameObject != null && transform.gameObject.activeInHierarchy;
        }

        private static LightShadowResolution ToShadowResolution(int setting)
        {
            return setting switch
            {
                1 => LightShadowResolution.Low,
                2 => LightShadowResolution.Medium,
                3 => LightShadowResolution.High,
                4 => LightShadowResolution.VeryHigh,
                _ => LightShadowResolution.FromQualitySettings
            };
        }

        private static string ShadowResolutionLabel(int setting)
        {
            return setting switch
            {
                1 => "Low",
                2 => "Medium",
                3 => "High",
                4 => "Very High",
                _ => "Use Game Quality"
            };
        }
    }
}
