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
using UnityEngine.Rendering.HighDefinition;
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
            public bool fisheyeOpticsEnabled;
            public float fisheyeXMultiplier;
            public float fisheyeYMultiplier;
            public float fisheyeCenterX;
            public float fisheyeCenterY;
            public float fisheyeScale;
            public bool mk1Enabled;
            public float mk1PaniniDistance;
            public float mk1PaniniCrop;
            public float mk1ChromaticAberration;
            public float mk1FilmGrain;
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
        private static HDAdditionalLightData hdLightData;
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
        private const float PreferredOverlayWidth = 580f;
        private static Rect overlayRect = new Rect(24f, 120f, PreferredOverlayWidth, 650f);
        private static Vector2 overlayScroll;
        private static string status = "Off";
        private static string keyframeStatus = "Open Replay to audit camera tracks";
        private static GameObject matteObject;
        private static Image[] matteImages;
        private static RawImage framedVignetteImage;
        private static Texture2D framedVignetteTexture;
        private static Texture2D mk1VignetteTexture;
        private static int matteScreenWidth;
        private static int matteScreenHeight;
        private static int matteMode = -1;
        private static float matteOpacity = -1f;
        private static float matteVignette = -1f;
        private static bool matteMk1Enabled;
        private static string presetName = string.Empty;
        private static string selectedPreset = string.Empty;
        private static readonly List<string> presetCache = new List<string>();
        private static bool presetCacheLoaded;
        private static LensDistortion fisheyeLensDistortion;
        private static ClampedFloatParameter fisheyeXParameter;
        private static ClampedFloatParameter fisheyeYParameter;
        private static Vector2Parameter fisheyeCenterParameter;
        private static ClampedFloatParameter fisheyeScaleParameter;
        private static bool fisheyeOriginalCaptured;
        private static bool fisheyeOpticsApplied;
        private static bool fisheyePerformanceLogged;
        private static float nextFisheyeBindAttemptTime;
        private static float originalFisheyeXMultiplier;
        private static float originalFisheyeYMultiplier;
        private static Vector2 originalFisheyeCenter;
        private static float originalFisheyeScale;
        private static bool originalFisheyeXOverride;
        private static bool originalFisheyeYOverride;
        private static bool originalFisheyeCenterOverride;
        private static bool originalFisheyeScaleOverride;
        private static UnityEngine.Rendering.HighDefinition.Vignette framedVignette;
        private static ClampedFloatParameter framedVignetteIntensityParameter;
        private static float originalFramedVignetteIntensity;
        private static bool originalFramedVignetteIntensityOverride;
        private static bool framedVignetteOriginalCaptured;
        private static bool framedVignetteSuppressed;
        private static VolumeProfile mk1VolumeProfile;
        private static GameObject mk1VolumeObject;
        private static Volume mk1RuntimeVolume;
        private static PaniniProjection mk1PaniniProjection;
        private static ChromaticAberration mk1ChromaticAberration;
        private static FilmGrain mk1FilmGrain;
        private static bool mk1PaniniAdded;
        private static bool mk1ChromaticAdded;
        private static bool mk1FilmGrainAdded;
        private static bool mk1OriginalCaptured;
        private static bool mk1OpticsApplied;
        private static bool originalMk1PaniniActive;
        private static bool originalMk1ChromaticActive;
        private static bool originalMk1FilmGrainActive;
        private static float originalMk1PaniniDistance;
        private static float originalMk1PaniniCrop;
        private static float originalMk1ChromaticAberration;
        private static float originalMk1FilmGrain;
        private static float originalMk1FilmGrainResponse;
        private static bool originalMk1PaniniDistanceOverride;
        private static bool originalMk1PaniniCropOverride;
        private static bool originalMk1ChromaticOverride;
        private static bool originalMk1FilmGrainOverride;
        private static bool originalMk1FilmGrainResponseOverride;
        private static HDAdditionalCameraData mk1CameraData;
        private static FrameSettings originalMk1FrameSettings;
        private static FrameSettingsOverrideMask originalMk1FrameSettingsOverrideMask;
        private static bool originalMk1CustomRenderingSettings;
        private static bool mk1FrameSettingsCaptured;
        private static bool mk1FrameSettingsApplied;
        private static bool mk1FrameSettingsWarningLogged;
        private static bool mk1PerformanceLogged;

        public static string Status => status;

        private static Transform ResolveReplayCameraTransform(out string label)
        {
            Transform keyframedTransform = keyframeCamera == null ? null : keyframeCamera._animatedCameraTrans;
            if (IsUsableTransform(keyframedTransform))
            {
                label = "keyframed replay camera";
                return keyframedTransform;
            }

            ResolveReplayOutputCamera();

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
            nextFisheyeBindAttemptTime = 0f;
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

            if (replayActive)
            {
                ApplyLensSettings(Config.replaySettings, false);
                BindFisheyeOptics();
                ApplyFisheyeOptics();
                ApplyMk1Optics();
                ApplyFramedVignette();
                lastTimelineTime = CurrentTimelineTime();
            }

            // Framing is replay-only. Update the live overlay while Replay is open, but never
            // recreate it in gameplay after the native replay-exit GameEvent removes it.
            UpdateMatteOverlay(true);
        }

        public static void Update()
        {
            UpdateMatteOverlay(false);

            if (!replayActive)
                return;

            // The replay Volume is initialized asynchronously on some camera modes. Retry at a
            // low fixed rate only until it binds; a fixed lens profile has no steady-state poll.
            if ((!fisheyeOriginalCaptured || !framedVignetteOriginalCaptured) &&
                Time.unscaledTime >= nextFisheyeBindAttemptTime)
            {
                nextFisheyeBindAttemptTime = Time.unscaledTime + 0.25f;
                BindFisheyeOptics();
                ApplyFisheyeOptics();
                ApplyMk1Optics();
                ApplyFramedVignette();
            }

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

            float availableWidth = Mathf.Max(320f, Screen.width - 24f);
            overlayRect.width = Mathf.Min(PreferredOverlayWidth, availableWidth);
            overlayRect.x = Mathf.Clamp(overlayRect.x, 8f, Mathf.Max(8f, Screen.width - overlayRect.width - 8f));

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
            // HDRP receives this as candela. Keep the normal default modest, but
            // allow enough headroom for dark HDRI/night maps without editing config files.
            Menu.ModernSlider("Intensity", ref intensity, 0f, 1000f, controlPrefix + "intensity");
            changed |= !Mathf.Approximately(intensity, settings.cameraLightIntensity);
            settings.cameraLightIntensity = intensity;

            float range = settings.cameraLightRange;
            Menu.ModernSlider("Range", ref range, 1f, 50f, controlPrefix + "range");
            changed |= !Mathf.Approximately(range, settings.cameraLightRange);
            settings.cameraLightRange = range;

            if (Menu.ControllerButton(controlPrefix + "light_type",
                    settings.cameraLightType == 0 ? "Light Type: Spot" : "Light Type: Point", Menu.UiButtonStyle,
                    GUILayout.Height(36f * Menu.EffectiveUiScale)))
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

                if (Menu.ControllerButton(controlPrefix + "shadow_resolution",
                        "Shadow Resolution: " + ShadowResolutionLabel(settings.cameraLightShadowResolution),
                        Menu.UiButtonStyle, GUILayout.Height(36f * Menu.EffectiveUiScale)))
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

            GUILayout.Label("SKATE / BMX QUICK LENSES", Menu.UiMutedStyle);
            GUILayout.BeginHorizontal();
            if (Menu.ControllerButton("lens_mk1", "MK1 Death Lens", Menu.UiButtonStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(36f * Menu.EffectiveUiScale)))
                ApplyMk1QuickLook();
            if (Menu.ControllerButton("lens_vx1000", "VX1000 4:3", Menu.UiButtonStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(36f * Menu.EffectiveUiScale)))
                ApplyFisheyeQuickLook(true);
            if (Menu.ControllerButton("lens_clean_fisheye", "Clean Fisheye", Menu.UiButtonStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(36f * Menu.EffectiveUiScale)))
                ApplyFisheyeQuickLook(false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Menu.ControllerButton("lens_reset", "Reset", Menu.UiButtonStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(36f * Menu.EffectiveUiScale)))
                ResetFisheyeQuickLook();
            GUILayout.EndHorizontal();
            GUILayout.Label("MK1 recreates the Century Optics death lens: 125 degree horizontal-equivalent view, 4:3 crop, Panini shaping, MK1 edge mask, subtle fringe, and grain.", Menu.UiMutedWrappedStyle);

            settings.replayFov = DrawSlider("FOV / Long Lens", settings.replayFov, 5f, 140f, controlPrefix + "fov", ref changed);
            DrawTrackLabel("FOV", LensTrack.Fov);
            settings.replayTilt = DrawSlider("Camera Tilt", settings.replayTilt, -180f, 180f, controlPrefix + "tilt", ref changed);
            DrawTrackLabel("Tilt", LensTrack.Tilt);
            settings.replayFisheye = DrawSlider("Fisheye (%)", settings.replayFisheye, 0f, 100f, controlPrefix + "fisheye", ref changed);
            DrawTrackLabel("Fisheye", LensTrack.Fisheye);

            bool opticsEnabled = settings.replayFisheyeOpticsEnabled;
            Menu.ModernToggle("Realistic Fisheye Optics", ref opticsEnabled, controlPrefix + "fisheye_optics");
            changed |= opticsEnabled != settings.replayFisheyeOpticsEnabled;
            settings.replayFisheyeOpticsEnabled = opticsEnabled;
            if (opticsEnabled)
            {
                settings.replayFisheyeScale = DrawSlider("Edge Crop / Optical Zoom", settings.replayFisheyeScale, 1f, 1.6f, controlPrefix + "fisheye_scale", ref changed);
                GUILayout.Label(
                    $"Output FOV after crop: {CalculateOpticalZoomFov(settings.replayFov, settings.replayFisheyeScale):0.0} degrees",
                    Menu.UiMutedStyle);
                settings.replayFisheyeXMultiplier = DrawSlider("Horizontal Curve", settings.replayFisheyeXMultiplier, 0.75f, 1f, controlPrefix + "fisheye_x", ref changed);
                settings.replayFisheyeYMultiplier = DrawSlider("Vertical Curve", settings.replayFisheyeYMultiplier, 0.75f, 1f, controlPrefix + "fisheye_y", ref changed);
                settings.replayFisheyeCenterX = DrawSlider("Optical Center X", settings.replayFisheyeCenterX, -0.25f, 0.25f, controlPrefix + "fisheye_center_x", ref changed);
                settings.replayFisheyeCenterY = DrawSlider("Optical Center Y", settings.replayFisheyeCenterY, -0.25f, 0.25f, controlPrefix + "fisheye_center_y", ref changed);
                GUILayout.Label("Native HDRP lens distortion - no custom render pass", Menu.UiBadgeStyle);
            }
            else
                GUILayout.Label("Optical shaping bypassed; Fisheye (%) remains available.", Menu.UiMutedWrappedStyle);

            bool mk1Enabled = settings.replayMk1Enabled;
            Menu.ModernToggle("MK1 Lens Character", ref mk1Enabled, controlPrefix + "mk1_enabled");
            changed |= mk1Enabled != settings.replayMk1Enabled;
            settings.replayMk1Enabled = mk1Enabled;
            if (mk1Enabled)
            {
                settings.replayMk1PaniniDistance = DrawSlider("MK1 Projection", settings.replayMk1PaniniDistance, 0f, 0.6f, controlPrefix + "mk1_panini", ref changed);
                settings.replayMk1PaniniCrop = DrawSlider("Projection Crop", settings.replayMk1PaniniCrop, 0f, 1f, controlPrefix + "mk1_crop", ref changed);
                settings.replayMk1ChromaticAberration = DrawSlider("Edge Fringe", settings.replayMk1ChromaticAberration, 0f, 0.25f, controlPrefix + "mk1_fringe", ref changed);
                settings.replayMk1FilmGrain = DrawSlider("VX Grain", settings.replayMk1FilmGrain, 0f, 0.25f, controlPrefix + "mk1_grain", ref changed);
                GUILayout.Label("Native HDRP Panini: one projection pass; fringe and grain use the existing post stack.", Menu.UiMutedWrappedStyle);
            }

            settings.replayVignette = DrawSlider("Vignette (%)", settings.replayVignette, 0f, 100f, controlPrefix + "vignette", ref changed);
            DrawTrackLabel("Vignette", LensTrack.Vignette);

            if (Menu.ControllerButton(controlPrefix + "shake_mode",
                    "Shake: " + ShakeModeLabel(settings.replayShakeMode), Menu.UiButtonStyle,
                    GUILayout.Height(36f * Menu.EffectiveUiScale)))
            {
                settings.replayShakeMode = (settings.replayShakeMode + 1) % 4;
                changed = true;
            }
            DrawTrackLabel("Shake", LensTrack.Shake);

            if (changed)
                OnLensSettingsChanged();
        }

        private static void ApplyFisheyeQuickLook(bool vx1000)
        {
            ReplaySettings settings = Config.replaySettings;
            settings.replayFisheyeOpticsEnabled = true;
            settings.replayMk1Enabled = false;
            // Cinemachine's FOV is vertical. The skate-video reference is horizontal, so a
            // 125-degree VX view inside a 4:3 crop is 110.47 degrees in the native control.
            settings.replayFov = vx1000 ? HorizontalToVerticalFov(125f, 4f / 3f) : 112f;
            settings.replayFisheye = vx1000 ? 32f : 24f;
            // HDRP clamps both axis multipliers to 0..1. Slightly reducing X gives the VX
            // profile a little more vertical bend without asking the native parameter for an
            // unsupported Y value above one.
            settings.replayFisheyeXMultiplier = vx1000 ? 0.96f : 1f;
            settings.replayFisheyeYMultiplier = 1f;
            settings.replayFisheyeCenterX = 0f;
            settings.replayFisheyeCenterY = vx1000 ? -0.015f : 0f;
            settings.replayFisheyeScale = vx1000 ? 1.035f : 1.02f;
            settings.replayVignette = vx1000 ? 18f : 8f;
            settings.replayFramingMode = vx1000 ? 2 : 0;
            settings.replayMatteOpacity = 1f;
            settings.activeReplayLensPreset = string.Empty;
            selectedPreset = string.Empty;
            OnLensSettingsChanged();
        }

        private static void ApplyMk1QuickLook()
        {
            ReplaySettings settings = Config.replaySettings;
            settings.replayFisheyeOpticsEnabled = true;
            settings.replayMk1Enabled = true;
            settings.replayFov = HorizontalToVerticalFov(125f, 4f / 3f);
            settings.replayFisheye = 40f;
            settings.replayFisheyeXMultiplier = 0.88f;
            settings.replayFisheyeYMultiplier = 1f;
            settings.replayFisheyeCenterX = 0f;
            settings.replayFisheyeCenterY = -0.012f;
            settings.replayFisheyeScale = 1.06f;
            settings.replayMk1PaniniDistance = 0.25f;
            settings.replayMk1PaniniCrop = 0.65f;
            settings.replayMk1ChromaticAberration = 0.07f;
            settings.replayMk1FilmGrain = 0.06f;
            settings.replayVignette = 38f;
            settings.replayFramingMode = 2;
            settings.replayMatteOpacity = 1f;
            settings.activeReplayLensPreset = string.Empty;
            selectedPreset = string.Empty;
            OnLensSettingsChanged();
        }

        private static float HorizontalToVerticalFov(float horizontalDegrees, float aspect)
        {
            float halfHorizontalRadians = horizontalDegrees * Mathf.Deg2Rad * 0.5f;
            return 2f * Mathf.Atan(Mathf.Tan(halfHorizontalRadians) / aspect) * Mathf.Rad2Deg;
        }

        private static void ResetFisheyeQuickLook()
        {
            ReplaySettings settings = Config.replaySettings;
            settings.replayFov = 60f;
            settings.replayFisheye = 0f;
            settings.replayFisheyeOpticsEnabled = true;
            settings.replayMk1Enabled = false;
            settings.replayFisheyeXMultiplier = 1f;
            settings.replayFisheyeYMultiplier = 1f;
            settings.replayFisheyeCenterX = 0f;
            settings.replayFisheyeCenterY = 0f;
            settings.replayFisheyeScale = 1f;
            settings.replayVignette = 5f;
            settings.replayFramingMode = 0;
            settings.activeReplayLensPreset = string.Empty;
            selectedPreset = string.Empty;
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
            if (Menu.ControllerButton(controlPrefix + "framing_mode",
                    "Framing: " + FramingModeLabel(settings.replayFramingMode), Menu.UiButtonStyle,
                    GUILayout.Height(36f * Menu.EffectiveUiScale)))
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
            if (Menu.ControllerButton("keyframe_add", "Add Camera Key", Menu.UiButtonStyle,
                    GUILayout.Height(36f * Menu.EffectiveUiScale)))
                AddKeyAtNeedle();
            if (Menu.ControllerButton("keyframe_delete", "Delete Key", Menu.UiButtonStyle,
                    GUILayout.Height(36f * Menu.EffectiveUiScale)))
                DeleteKeyAtNeedle();
            if (Menu.ControllerButton("keyframe_delete_all", "Delete All", Menu.UiButtonStyle,
                    GUILayout.Height(36f * Menu.EffectiveUiScale)))
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
                if (Menu.ControllerButton($"replay_preset_{name}",
                        (selected ? "● " : string.Empty) + name, Menu.UiButtonStyle,
                        GUILayout.Height(36f * Menu.EffectiveUiScale)))
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
                if (selected && Menu.ControllerButton($"rename_replay_preset_{name}", "Rename", Menu.UiButtonStyle,
                        GUILayout.Width(82f * Menu.EffectiveUiScale), GUILayout.Height(36f * Menu.EffectiveUiScale)))
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
                if (Menu.ControllerButton($"delete_replay_preset_{name}", "Delete", Menu.UiButtonStyle,
                        GUILayout.Width(78f * Menu.EffectiveUiScale), GUILayout.Height(36f * Menu.EffectiveUiScale)) &&
                    ReplayLensPreset.Delete(name))
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

            // The Camera Lab is a vertical tool. Give its contents an explicit width and use no
            // horizontal scrollbar so long labels can never widen the window or create a bottom
            // scroll track.
            overlayScroll.x = 0f;
            overlayScroll = GUILayout.BeginScrollView(
                overlayScroll,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(260f, overlayRect.width - 58f)));
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
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            overlayScroll.x = 0f;
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
                    BindFisheyeOptics();
                    if (lensApplyPending)
                        ApplyLensSettings(Config.replaySettings, false);
                    else if (!nativeValuesCaptured)
                        CaptureNativeLensValues();

                    ApplyFisheyeOptics();
                    ApplyMk1Optics();
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

            RestoreFisheyeOptics(true);
            RestoreFramedVignette(true);
            RestoreMk1Optics(true);
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

        private static void BindFisheyeOptics()
        {
            LensDistortion candidate = null;
            bool resolvedFromProfile = false;
            // Prefer the component in the Volume profile that is active right now. The native
            // CameraSettingsControls cache can point at the previous replay mode's profile.
            VolumeProfile profile = cameraSettingsControls?.postProcessVolume?.profile;
            if (profile?.components != null)
            {
                for (int i = 0; i < profile.components.Count; i++)
                {
                    candidate = profile.components[i]?.TryCast<LensDistortion>();
                    if (candidate != null)
                    {
                        resolvedFromProfile = true;
                        break;
                    }
                }
            }

            BindFramedVignette(profile);
            BindMk1Optics(profile);

            candidate ??= cameraSettingsControls?.lensDistortion;

            if (candidate == null)
                return;

            if (fisheyeLensDistortion == candidate && fisheyeOriginalCaptured)
                return;

            RestoreFisheyeOptics(true);
            fisheyeLensDistortion = candidate;
            try
            {
                // Cache the four native parameter wrappers once. Fixed lens profiles then do no
                // per-frame RoweMod work; animated profiles touch only these cached parameters.
                fisheyeXParameter = candidate.xMultiplier;
                fisheyeYParameter = candidate.yMultiplier;
                fisheyeCenterParameter = candidate.center;
                fisheyeScaleParameter = candidate.scale;
                originalFisheyeXMultiplier = fisheyeXParameter.value;
                originalFisheyeYMultiplier = fisheyeYParameter.value;
                originalFisheyeCenter = fisheyeCenterParameter.value;
                originalFisheyeScale = fisheyeScaleParameter.value;
                originalFisheyeXOverride = fisheyeXParameter.overrideState;
                originalFisheyeYOverride = fisheyeYParameter.overrideState;
                originalFisheyeCenterOverride = fisheyeCenterParameter.overrideState;
                originalFisheyeScaleOverride = fisheyeScaleParameter.overrideState;
                fisheyeOriginalCaptured = true;
                fisheyeOpticsApplied = false;
                if (resolvedFromProfile)
                    Log.Msg("[ReplayFisheye] Resolved native LensDistortion from the replay Volume profile.");
            }
            catch (Exception ex)
            {
                fisheyeLensDistortion = null;
                fisheyeXParameter = null;
                fisheyeYParameter = null;
                fisheyeCenterParameter = null;
                fisheyeScaleParameter = null;
                fisheyeOriginalCaptured = false;
                Log.Warning("[ReplayFisheye] Could not capture native HDRP optics: " + ex.Message);
            }
        }

        private static void BindFramedVignette(VolumeProfile profile)
        {
            UnityEngine.Rendering.HighDefinition.Vignette candidate = null;
            if (profile?.components != null)
            {
                for (int i = 0; i < profile.components.Count; i++)
                {
                    candidate = profile.components[i]
                        ?.TryCast<UnityEngine.Rendering.HighDefinition.Vignette>();
                    if (candidate != null)
                        break;
                }
            }

            if (candidate == null ||
                (framedVignette == candidate && framedVignetteOriginalCaptured))
                return;

            RestoreFramedVignette(true);
            framedVignette = candidate;
            try
            {
                framedVignetteIntensityParameter = candidate.intensity;
                originalFramedVignetteIntensity = framedVignetteIntensityParameter.value;
                originalFramedVignetteIntensityOverride =
                    framedVignetteIntensityParameter.overrideState;
                framedVignetteOriginalCaptured = true;
                framedVignetteSuppressed = false;
                Log.Msg("[ReplayVignette] Bound to the active replay HDRP Vignette.");
            }
            catch (Exception ex)
            {
                framedVignette = null;
                framedVignetteIntensityParameter = null;
                framedVignetteOriginalCaptured = false;
                Log.Warning("[ReplayVignette] Could not capture native HDRP vignette: " + ex.Message);
            }
        }

        private static void BindMk1Optics(VolumeProfile profile)
        {
            ReplaySettings settings = Config.replaySettings;
            if (!settings.replayMk1Enabled)
            {
                if (mk1OriginalCaptured)
                    RestoreMk1Optics(true);
                return;
            }
            if (mk1RuntimeVolume != null && mk1VolumeProfile != null &&
                mk1OriginalCaptured &&
                mk1PaniniProjection != null && mk1ChromaticAberration != null &&
                mk1FilmGrain != null)
                return;

            RestoreMk1Optics(true);
            try
            {
                UnityCamera outputCamera = ResolveReplayOutputCamera();
                HDAdditionalCameraData cameraData =
                    outputCamera?.GetComponent<HDAdditionalCameraData>();
                if (outputCamera == null || cameraData == null)
                    throw new InvalidOperationException(
                        "the HDRP replay output camera was unavailable");

                int volumeLayer = FirstLayerInMask(cameraData.volumeLayerMask.value);
                if (volumeLayer < 0)
                    throw new InvalidOperationException(
                        "the replay output camera has an empty Volume Layer Mask");

                mk1VolumeObject = new GameObject("RoweMod MK1 Replay Volume")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = volumeLayer
                };
                mk1VolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                mk1VolumeProfile.name = "RoweMod MK1 Replay Profile";
                mk1VolumeProfile.hideFlags = HideFlags.HideAndDontSave;
                mk1RuntimeVolume = mk1VolumeObject.AddComponent<Volume>();
                mk1RuntimeVolume.isGlobal = true;
                mk1RuntimeVolume.priority = 10000f;
                mk1RuntimeVolume.weight = 1f;
                mk1RuntimeVolume.sharedProfile = mk1VolumeProfile;

                mk1PaniniProjection = mk1VolumeProfile
                    .Add(Il2CppType.Of<PaniniProjection>(), true)
                    ?.TryCast<PaniniProjection>();
                mk1ChromaticAberration = mk1VolumeProfile
                    .Add(Il2CppType.Of<ChromaticAberration>(), true)
                    ?.TryCast<ChromaticAberration>();
                mk1FilmGrain = mk1VolumeProfile
                    .Add(Il2CppType.Of<FilmGrain>(), true)
                    ?.TryCast<FilmGrain>();
                mk1PaniniAdded = mk1PaniniProjection != null;
                mk1ChromaticAdded = mk1ChromaticAberration != null;
                mk1FilmGrainAdded = mk1FilmGrain != null;

                if (mk1PaniniProjection == null || mk1ChromaticAberration == null ||
                    mk1FilmGrain == null)
                    throw new InvalidOperationException("one or more HDRP MK1 components were unavailable");

                originalMk1PaniniActive = mk1PaniniProjection.active;
                originalMk1ChromaticActive = mk1ChromaticAberration.active;
                originalMk1FilmGrainActive = mk1FilmGrain.active;
                originalMk1PaniniDistance = mk1PaniniProjection.distance.value;
                originalMk1PaniniCrop = mk1PaniniProjection.cropToFit.value;
                originalMk1ChromaticAberration = mk1ChromaticAberration.intensity.value;
                originalMk1FilmGrain = mk1FilmGrain.intensity.value;
                originalMk1FilmGrainResponse = mk1FilmGrain.response.value;
                originalMk1PaniniDistanceOverride = mk1PaniniProjection.distance.overrideState;
                originalMk1PaniniCropOverride = mk1PaniniProjection.cropToFit.overrideState;
                originalMk1ChromaticOverride = mk1ChromaticAberration.intensity.overrideState;
                originalMk1FilmGrainOverride = mk1FilmGrain.intensity.overrideState;
                originalMk1FilmGrainResponseOverride = mk1FilmGrain.response.overrideState;
                mk1OriginalCaptured = true;
                mk1OpticsApplied = false;
                bool renderedByCamera = VolumeManager.instance != null &&
                    VolumeManager.IsVolumeRenderedByCamera(
                        mk1RuntimeVolume,
                        outputCamera);
                Log.Msg(
                    "[ReplayMK1] Dedicated native HDRP replay Volume bound " +
                    $"(layer={volumeLayer}, renderedByCamera={renderedByCamera}, " +
                    $"added={mk1PaniniAdded}/{mk1ChromaticAdded}/{mk1FilmGrainAdded}).");
            }
            catch (Exception ex)
            {
                Log.Warning("[ReplayMK1] Could not bind the native HDRP character stack: " + ex.Message);
                RestoreMk1Optics(true);
            }
        }

        private static int FirstLayerInMask(int mask)
        {
            for (int layer = 0; layer < 32; layer++)
                if ((mask & (1 << layer)) != 0)
                    return layer;
            return -1;
        }

        private static T FindVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile?.components == null)
                return null;
            for (int i = 0; i < profile.components.Count; i++)
            {
                T candidate = profile.components[i]?.TryCast<T>();
                if (candidate != null)
                    return candidate;
            }
            return null;
        }

        private static void ApplyMk1Optics()
        {
            ReplaySettings settings = Config.replaySettings;
            if (!replayActive || !settings.replayMk1Enabled)
            {
                if (mk1OpticsApplied || mk1FrameSettingsApplied)
                    RestoreMk1Optics(false);
                return;
            }

            bool frameSettingsReady = ApplyMk1FrameSettings();

            if (!mk1OriginalCaptured)
                BindMk1Optics(cameraSettingsControls?.postProcessVolume?.profile);
            if (!mk1OriginalCaptured || mk1PaniniProjection == null ||
                mk1ChromaticAberration == null || mk1FilmGrain == null)
                return;

            try
            {
                if (mk1RuntimeVolume != null)
                {
                    mk1RuntimeVolume.enabled = true;
                    mk1RuntimeVolume.weight = 1f;
                }
                if (!mk1PaniniProjection.active)
                    mk1PaniniProjection.active = true;
                if (!mk1ChromaticAberration.active)
                    mk1ChromaticAberration.active = true;
                if (!mk1FilmGrain.active)
                    mk1FilmGrain.active = true;
                SetLensFloat(mk1PaniniProjection.distance, settings.replayMk1PaniniDistance);
                SetLensFloat(mk1PaniniProjection.cropToFit, settings.replayMk1PaniniCrop);
                SetLensFloat(
                    mk1ChromaticAberration.intensity,
                    settings.replayMk1ChromaticAberration);
                SetLensFloat(mk1FilmGrain.intensity, settings.replayMk1FilmGrain);
                SetLensFloat(mk1FilmGrain.response, 0.82f);
                mk1OpticsApplied = true;

                bool volumeRendered = mk1RuntimeVolume != null &&
                    VolumeManager.instance != null &&
                    VolumeManager.IsVolumeRenderedByCamera(
                        mk1RuntimeVolume,
                        ResolveReplayOutputCamera());
                if (frameSettingsReady && volumeRendered && !mk1PerformanceLogged)
                {
                    mk1PerformanceLogged = true;
                    Log.Msg(
                        "[ReplayMK1] Verified on output camera '" + mk1CameraData.gameObject.name +
                        "': Postprocess=on, Panini=on, ChromaticAberration=on, FilmGrain=on; " +
                        $"distance={mk1PaniniProjection.distance.value:0.###}, " +
                        $"crop={mk1PaniniProjection.cropToFit.value:0.###}, " +
                        $"fringe={mk1ChromaticAberration.intensity.value:0.###}, " +
                        $"grain={mk1FilmGrain.intensity.value:0.###}. One native projection pass, " +
                        "one 256px UI mask, dedicated replay Volume visible to camera, no " +
                        "RoweMod materials or render textures.");
                }
                else if (!volumeRendered)
                {
                    LogMk1FrameSettingsWarning(
                        "the dedicated MK1 Volume is not visible to the output camera");
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[ReplayMK1] Could not apply the native HDRP character stack: " + ex.Message);
                RestoreMk1Optics(true);
            }
        }

        private static bool ApplyMk1FrameSettings()
        {
            UnityCamera outputCamera = ResolveReplayOutputCamera();
            if (outputCamera == null || !outputCamera.isActiveAndEnabled)
            {
                LogMk1FrameSettingsWarning("the active Cinemachine output camera is unavailable");
                return false;
            }

            HDAdditionalCameraData outputCameraData =
                mk1FrameSettingsCaptured && mk1CameraData != null &&
                mk1CameraData.gameObject == outputCamera.gameObject
                    ? mk1CameraData
                    : outputCamera.GetComponent<HDAdditionalCameraData>();
            if (outputCameraData == null)
            {
                LogMk1FrameSettingsWarning(
                    $"output camera '{outputCamera.gameObject.name}' has no HDAdditionalCameraData");
                return false;
            }

            if (mk1FrameSettingsCaptured && mk1CameraData != outputCameraData)
                RestoreMk1CameraFrameSettings(true);

            if (!mk1FrameSettingsCaptured)
            {
                mk1CameraData = outputCameraData;
                originalMk1CustomRenderingSettings = outputCameraData.customRenderingSettings;
                originalMk1FrameSettings = outputCameraData.m_RenderingPathCustomFrameSettings;
                originalMk1FrameSettingsOverrideMask =
                    outputCameraData.renderingPathCustomFrameSettingsOverrideMask;
                mk1FrameSettingsCaptured = true;
            }

            // LateUpdate calls this every frame because the replay controller can replace camera
            // values after its playable runs. The normal path is read-only: only repair the four
            // flags if HDRP or the game actually changed one.
            if (AreMk1FrameSettingsEnabled(outputCameraData))
            {
                mk1FrameSettingsApplied = true;
                return true;
            }

            FrameSettings frameSettings = outputCameraData.m_RenderingPathCustomFrameSettings;
            FrameSettingsOverrideMask overrideMask =
                outputCameraData.renderingPathCustomFrameSettingsOverrideMask;
            SetMk1FrameSetting(
                ref frameSettings,
                ref overrideMask,
                FrameSettingsField.Postprocess);
            SetMk1FrameSetting(
                ref frameSettings,
                ref overrideMask,
                FrameSettingsField.PaniniProjection);
            SetMk1FrameSetting(
                ref frameSettings,
                ref overrideMask,
                FrameSettingsField.ChromaticAberration);
            SetMk1FrameSetting(
                ref frameSettings,
                ref overrideMask,
                FrameSettingsField.FilmGrain);

            outputCameraData.m_RenderingPathCustomFrameSettings = frameSettings;
            outputCameraData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;
            outputCameraData.customRenderingSettings = true;
            mk1FrameSettingsApplied = true;

            bool verified = AreMk1FrameSettingsEnabled(outputCameraData);
            if (!verified)
                LogMk1FrameSettingsWarning(
                    $"HDRP did not retain all requested overrides on '{outputCamera.gameObject.name}'");
            return verified;
        }

        private static bool AreMk1FrameSettingsEnabled(HDAdditionalCameraData cameraData)
        {
            if (cameraData == null || !cameraData.customRenderingSettings)
                return false;
            FrameSettings settings = cameraData.m_RenderingPathCustomFrameSettings;
            FrameSettingsOverrideMask mask =
                cameraData.renderingPathCustomFrameSettingsOverrideMask;
            return IsMk1FrameSettingEnabled(settings, mask, FrameSettingsField.Postprocess) &&
                IsMk1FrameSettingEnabled(settings, mask, FrameSettingsField.PaniniProjection) &&
                IsMk1FrameSettingEnabled(settings, mask, FrameSettingsField.ChromaticAberration) &&
                IsMk1FrameSettingEnabled(settings, mask, FrameSettingsField.FilmGrain);
        }

        private static void SetMk1FrameSetting(
            ref FrameSettings settings,
            ref FrameSettingsOverrideMask overrideMask,
            FrameSettingsField field)
        {
            settings.SetEnabled(field, true);
            BitArray128 mask = overrideMask.mask;
            mask[(uint)field] = true;
            overrideMask.mask = mask;
        }

        private static bool IsMk1FrameSettingEnabled(
            FrameSettings settings,
            FrameSettingsOverrideMask overrideMask,
            FrameSettingsField field) =>
            settings.IsEnabled(field) && overrideMask.mask[(uint)field];

        private static void LogMk1FrameSettingsWarning(string reason)
        {
            if (mk1FrameSettingsWarningLogged)
                return;
            mk1FrameSettingsWarningLogged = true;
            Log.Warning("[ReplayMK1] Camera frame settings are not verified: " + reason + ".");
        }

        private static void RestoreMk1CameraFrameSettings(bool clearBinding)
        {
            if (mk1FrameSettingsCaptured && mk1CameraData != null)
            {
                try
                {
                    mk1CameraData.m_RenderingPathCustomFrameSettings = originalMk1FrameSettings;
                    mk1CameraData.renderingPathCustomFrameSettingsOverrideMask =
                        originalMk1FrameSettingsOverrideMask;
                    mk1CameraData.customRenderingSettings =
                        originalMk1CustomRenderingSettings;
                }
                catch
                {
                    // The output camera can be destroyed before replay teardown completes.
                }
            }

            mk1FrameSettingsApplied = false;
            if (!clearBinding)
                return;

            mk1CameraData = null;
            mk1FrameSettingsCaptured = false;
            mk1FrameSettingsWarningLogged = false;
        }

        private static void RestoreMk1Optics(bool clearBinding)
        {
            RestoreMk1CameraFrameSettings(clearBinding);
            if (mk1OriginalCaptured)
            {
                try
                {
                    if (mk1RuntimeVolume != null && !clearBinding)
                    {
                        mk1RuntimeVolume.weight = 0f;
                        mk1RuntimeVolume.enabled = false;
                    }
                    if (mk1PaniniProjection != null)
                    {
                        if (mk1PaniniAdded && !clearBinding)
                            mk1PaniniProjection.active = false;
                        else if (!mk1PaniniAdded)
                        {
                            mk1PaniniProjection.active = originalMk1PaniniActive;
                            RestoreLensFloat(mk1PaniniProjection.distance, originalMk1PaniniDistance, originalMk1PaniniDistanceOverride);
                            RestoreLensFloat(mk1PaniniProjection.cropToFit, originalMk1PaniniCrop, originalMk1PaniniCropOverride);
                        }
                    }
                    if (mk1ChromaticAberration != null)
                    {
                        if (mk1ChromaticAdded && !clearBinding)
                            mk1ChromaticAberration.active = false;
                        else if (!mk1ChromaticAdded)
                        {
                            mk1ChromaticAberration.active = originalMk1ChromaticActive;
                            RestoreLensFloat(mk1ChromaticAberration.intensity, originalMk1ChromaticAberration, originalMk1ChromaticOverride);
                        }
                    }
                    if (mk1FilmGrain != null)
                    {
                        if (mk1FilmGrainAdded && !clearBinding)
                            mk1FilmGrain.active = false;
                        else if (!mk1FilmGrainAdded)
                        {
                            mk1FilmGrain.active = originalMk1FilmGrainActive;
                            RestoreLensFloat(mk1FilmGrain.intensity, originalMk1FilmGrain, originalMk1FilmGrainOverride);
                            RestoreLensFloat(mk1FilmGrain.response, originalMk1FilmGrainResponse, originalMk1FilmGrainResponseOverride);
                        }
                    }
                }
                catch
                {
                    // The active replay Volume may be destroyed before the close event arrives.
                }
            }

            if (clearBinding && mk1VolumeProfile != null)
            {
                try
                {
                    // Remove only overrides RoweMod created. Existing game overrides were restored
                    // above and remain owned by the replay Volume profile.
                    if (mk1PaniniAdded)
                        mk1VolumeProfile.Remove(Il2CppType.Of<PaniniProjection>());
                    if (mk1ChromaticAdded)
                        mk1VolumeProfile.Remove(Il2CppType.Of<ChromaticAberration>());
                    if (mk1FilmGrainAdded)
                        mk1VolumeProfile.Remove(Il2CppType.Of<FilmGrain>());
                }
                catch
                {
                    // The replay profile can already be gone during scene teardown.
                }
            }

            if (clearBinding)
            {
                try
                {
                    if (mk1VolumeObject != null)
                        UnityObject.Destroy(mk1VolumeObject);
                    if (mk1VolumeProfile != null)
                        UnityObject.Destroy(mk1VolumeProfile);
                }
                catch
                {
                    // Replay scene teardown may already have destroyed runtime-only objects.
                }
            }

            mk1OpticsApplied = false;
            if (!clearBinding)
                return;

            mk1VolumeProfile = null;
            mk1VolumeObject = null;
            mk1RuntimeVolume = null;
            mk1PaniniProjection = null;
            mk1ChromaticAberration = null;
            mk1FilmGrain = null;
            mk1PaniniAdded = false;
            mk1ChromaticAdded = false;
            mk1FilmGrainAdded = false;
            mk1OriginalCaptured = false;
            mk1FrameSettingsWarningLogged = false;
            mk1PerformanceLogged = false;
        }

        private static void ApplyFisheyeOptics()
        {
            if (!replayActive || !fisheyeOriginalCaptured || fisheyeLensDistortion == null)
                return;

            ReplaySettings settings = Config.replaySettings;
            if (!settings.replayFisheyeOpticsEnabled)
            {
                if (fisheyeOpticsApplied)
                    RestoreFisheyeOptics(false);
                return;
            }

            try
            {
                SetLensFloat(fisheyeXParameter, settings.replayFisheyeXMultiplier);
                SetLensFloat(fisheyeYParameter, settings.replayFisheyeYMultiplier);
                // HDRP stores the lens center in absolute screen UVs: (0.5, 0.5) is centered.
                // RoweMod exposes friendlier signed offsets, so convert them before touching the
                // native parameter. Writing (0, 0) directly puts the origin at a corner and
                // creates the large diagonal black arc visible in playback.
                Vector2 nativeCenter = new Vector2(
                    0.5f + settings.replayFisheyeCenterX,
                    0.5f + settings.replayFisheyeCenterY);
                SetLensVector(fisheyeCenterParameter, nativeCenter);
                // LensDistortion.scale is not honored consistently by the replay camera's
                // blended Volume stack. Keep the shader scale neutral and implement the crop on
                // the output camera projection below, where it is deterministic.
                SetLensFloat(fisheyeScaleParameter, 1f);
                fisheyeOpticsApplied = true;

                if (!fisheyePerformanceLogged)
                {
                    fisheyePerformanceLogged = true;
                    Log.Msg(
                        "[ReplayFisheye] Native HDRP UberPost optics active; " +
                        $"center=({nativeCenter.x:0.###}, {nativeCenter.y:0.###}), " +
                        $"optical zoom={settings.replayFisheyeScale:0.###}; " +
                        "RoweMod render passes=0, render textures=0, materials=0.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[ReplayFisheye] Could not apply native HDRP optics: " + ex.Message);
                RestoreFisheyeOptics(true);
            }
        }

        /// <summary>
        /// Runs after the replay controller has updated its Volume values. This performs four
        /// cached comparisons only while optics are enabled; it adds no render pass, texture, or
        /// material and writes only when the native game changed one of our requested values.
        /// </summary>
        public static void LateUpdate()
        {
            if (!replayActive)
                return;

            ReplaySettings settings = Config.replaySettings;
            ApplyCurrentLightSettings(settings);
            ApplyOpticalZoom(settings, settings.replayFisheyeOpticsEnabled);
            ApplyMk1Optics();
            ApplyFramedVignette();

            // Projection zoom is independent from HDRP binding. The curve controls below simply
            // skip this frame if the replay Volume is temporarily unavailable.
            if (!fisheyeOriginalCaptured || fisheyeLensDistortion == null)
                return;

            if (settings.replayFisheyeOpticsEnabled)
                ApplyFisheyeOptics();
            else
            {
                if (fisheyeOpticsApplied)
                    RestoreFisheyeOptics(false);
            }
        }

        private static void ApplyFramedVignette()
        {
            if (!framedVignetteOriginalCaptured || framedVignetteIntensityParameter == null)
                return;

            ReplaySettings settings = Config.replaySettings;
            bool useFramedVignette = replayActive && settings.replayFramingMode != 0;
            try
            {
                if (useFramedVignette)
                {
                    // The stock HDRP vignette is evaluated against the entire ultrawide output.
                    // Suppress it while a frame is active; the single UI quad below draws the same
                    // requested strength against the actual 16:9 or 4:3 content rectangle.
                    SetLensFloat(framedVignetteIntensityParameter, 0f);
                    framedVignetteSuppressed = true;
                }
                else if (framedVignetteSuppressed)
                {
                    SetLensFloat(
                        framedVignetteIntensityParameter,
                        Mathf.Clamp01(settings.replayVignette * 0.01f));
                    framedVignetteSuppressed = false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[ReplayVignette] Could not apply framed vignette: " + ex.Message);
                RestoreFramedVignette(true);
            }
        }

        private static void RestoreFramedVignette(bool clearBinding)
        {
            if (framedVignetteOriginalCaptured && framedVignetteIntensityParameter != null)
            {
                try
                {
                    RestoreLensFloat(
                        framedVignetteIntensityParameter,
                        originalFramedVignetteIntensity,
                        originalFramedVignetteIntensityOverride);
                }
                catch
                {
                    // Replay teardown can destroy the Volume before the close event reaches us.
                }
            }

            framedVignetteSuppressed = false;
            if (!clearBinding)
                return;

            framedVignette = null;
            framedVignetteIntensityParameter = null;
            framedVignetteOriginalCaptured = false;
        }

        private static void ApplyOpticalZoom(ReplaySettings settings, bool enabled)
        {
            UnityCamera outputCamera = ResolveReplayOutputCamera();
            if (outputCamera == null || !outputCamera.isActiveAndEnabled)
                return;

            float nativeFov = ReadSmart(cameraSettingsControls?._fovSmartData, settings.replayFov);
            float outputFov = enabled
                ? CalculateOpticalZoomFov(nativeFov, settings.replayFisheyeScale)
                : Mathf.Clamp(nativeFov, 5f, 179f);

            if (!Mathf.Approximately(outputCamera.fieldOfView, outputFov))
                outputCamera.fieldOfView = outputFov;
        }

        private static float CalculateOpticalZoomFov(float verticalFov, float zoom)
        {
            float safeFov = Mathf.Clamp(verticalFov, 5f, 179f);
            float safeZoom = Mathf.Clamp(zoom, 1f, 2f);
            float halfRadians = safeFov * Mathf.Deg2Rad * 0.5f;
            return Mathf.Clamp(
                2f * Mathf.Atan(Mathf.Tan(halfRadians) / safeZoom) * Mathf.Rad2Deg,
                5f,
                179f);
        }

        private static void RestoreFisheyeOptics(bool clearBinding)
        {
            if (fisheyeOriginalCaptured && fisheyeLensDistortion != null)
            {
                try
                {
                    RestoreLensFloat(fisheyeXParameter, originalFisheyeXMultiplier, originalFisheyeXOverride);
                    RestoreLensFloat(fisheyeYParameter, originalFisheyeYMultiplier, originalFisheyeYOverride);
                    RestoreLensVector(fisheyeCenterParameter, originalFisheyeCenter, originalFisheyeCenterOverride);
                    RestoreLensFloat(fisheyeScaleParameter, originalFisheyeScale, originalFisheyeScaleOverride);
                }
                catch
                {
                    // Replay teardown can destroy the Volume before the close event reaches us.
                }
            }

            fisheyeOpticsApplied = false;
            if (!clearBinding)
                return;

            fisheyeLensDistortion = null;
            fisheyeXParameter = null;
            fisheyeYParameter = null;
            fisheyeCenterParameter = null;
            fisheyeScaleParameter = null;
            fisheyeOriginalCaptured = false;
            fisheyePerformanceLogged = false;
        }

        private static void SetLensFloat(ClampedFloatParameter parameter, float value)
        {
            if (parameter == null)
                return;
            if (!parameter.overrideState)
                parameter.overrideState = true;
            if (!Mathf.Approximately(parameter.value, value))
                parameter.value = value;
        }

        private static void SetLensVector(Vector2Parameter parameter, Vector2 value)
        {
            if (parameter == null)
                return;
            if (!parameter.overrideState)
                parameter.overrideState = true;
            if ((parameter.value - value).sqrMagnitude > 0.0000001f)
                parameter.value = value;
        }

        private static void RestoreLensFloat(ClampedFloatParameter parameter, float value, bool overrideState)
        {
            if (parameter == null)
                return;
            parameter.value = value;
            parameter.overrideState = overrideState;
        }

        private static void RestoreLensVector(Vector2Parameter parameter, Vector2 value, bool overrideState)
        {
            if (parameter == null)
                return;
            parameter.value = value;
            parameter.overrideState = overrideState;
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
                fisheyeOpticsEnabled = s.replayFisheyeOpticsEnabled,
                fisheyeXMultiplier = s.replayFisheyeXMultiplier,
                fisheyeYMultiplier = s.replayFisheyeYMultiplier,
                fisheyeCenterX = s.replayFisheyeCenterX,
                fisheyeCenterY = s.replayFisheyeCenterY,
                fisheyeScale = s.replayFisheyeScale,
                mk1Enabled = s.replayMk1Enabled,
                mk1PaniniDistance = s.replayMk1PaniniDistance,
                mk1PaniniCrop = s.replayMk1PaniniCrop,
                mk1ChromaticAberration = s.replayMk1ChromaticAberration,
                mk1FilmGrain = s.replayMk1FilmGrain,
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
            value.fisheyeXMultiplier = Mathf.Lerp(a.fisheyeXMultiplier, b.fisheyeXMultiplier, t);
            value.fisheyeYMultiplier = Mathf.Lerp(a.fisheyeYMultiplier, b.fisheyeYMultiplier, t);
            value.fisheyeCenterX = Mathf.Lerp(a.fisheyeCenterX, b.fisheyeCenterX, t);
            value.fisheyeCenterY = Mathf.Lerp(a.fisheyeCenterY, b.fisheyeCenterY, t);
            value.fisheyeScale = Mathf.Lerp(a.fisheyeScale, b.fisheyeScale, t);
            value.mk1PaniniDistance = Mathf.Lerp(a.mk1PaniniDistance, b.mk1PaniniDistance, t);
            value.mk1PaniniCrop = Mathf.Lerp(a.mk1PaniniCrop, b.mk1PaniniCrop, t);
            value.mk1ChromaticAberration = Mathf.Lerp(a.mk1ChromaticAberration, b.mk1ChromaticAberration, t);
            value.mk1FilmGrain = Mathf.Lerp(a.mk1FilmGrain, b.mk1FilmGrain, t);
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
            s.replayFisheyeOpticsEnabled = value.fisheyeOpticsEnabled;
            s.replayFisheyeXMultiplier = value.fisheyeXMultiplier;
            s.replayFisheyeYMultiplier = value.fisheyeYMultiplier;
            s.replayFisheyeCenterX = value.fisheyeCenterX;
            s.replayFisheyeCenterY = value.fisheyeCenterY;
            s.replayFisheyeScale = value.fisheyeScale;
            s.replayMk1Enabled = value.mk1Enabled;
            s.replayMk1PaniniDistance = value.mk1PaniniDistance;
            s.replayMk1PaniniCrop = value.mk1PaniniCrop;
            s.replayMk1ChromaticAberration = value.mk1ChromaticAberration;
            s.replayMk1FilmGrain = value.mk1FilmGrain;
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
            ApplyFisheyeOptics();
            ApplyMk1Optics();
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
            {
                InheritPreviousKeyForEditing(timelineTime, "replay editor mode changed");
                BindFisheyeOptics();
                ApplyFisheyeOptics();
                ApplyMk1Optics();
            }
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
            s.replayFisheyeOpticsEnabled = value.fisheyeOpticsEnabled;
            s.replayFisheyeXMultiplier = value.fisheyeXMultiplier;
            s.replayFisheyeYMultiplier = value.fisheyeYMultiplier;
            s.replayFisheyeCenterX = value.fisheyeCenterX;
            s.replayFisheyeCenterY = value.fisheyeCenterY;
            s.replayFisheyeScale = value.fisheyeScale;
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
            ApplyFisheyeOptics();
            ApplyMk1Optics();
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
            bool frameSelected = s.replayFramingMode != 0;
            bool vignetteVisible = replayActive && frameSelected && s.replayVignette > 0.001f;
            bool visible = replayActive && frameSelected &&
                (s.replayMatteOpacity > 0.001f || vignetteVisible);
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
                matteMode == s.replayFramingMode &&
                Mathf.Approximately(matteOpacity, s.replayMatteOpacity) &&
                Mathf.Approximately(matteVignette, s.replayVignette) &&
                matteMk1Enabled == s.replayMk1Enabled)
                return;

            matteScreenWidth = Screen.width;
            matteScreenHeight = Screen.height;
            matteMode = s.replayFramingMode;
            matteOpacity = s.replayMatteOpacity;
            matteVignette = s.replayVignette;
            matteMk1Enabled = s.replayMk1Enabled;
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

            if (framedVignetteImage != null)
            {
                framedVignetteImage.enabled = vignetteVisible;
                framedVignetteImage.texture = s.replayMk1Enabled
                    ? mk1VignetteTexture
                    : framedVignetteTexture;
                framedVignetteImage.color = new Color(
                    0f,
                    0f,
                    0f,
                    Mathf.Clamp01(s.replayVignette * 0.01f));
                RectTransform vignetteRect = framedVignetteImage.rectTransform;
                vignetteRect.anchorMin = new Vector2(xMin, yMin);
                vignetteRect.anchorMax = new Vector2(xMax, yMax);
                vignetteRect.offsetMin = Vector2.zero;
                vignetteRect.offsetMax = Vector2.zero;
            }
        }

        private static void EnsureMatteOverlay()
        {
            if (matteObject != null)
                return;

            matteObject = new GameObject(MatteObjectName) { hideFlags = HideFlags.HideAndDontSave };
            Canvas canvas = matteObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;

            GameObject vignette = new GameObject("Framed Vignette")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            vignette.transform.SetParent(matteObject.transform, false);
            framedVignetteImage = vignette.AddComponent<RawImage>();
            framedVignetteImage.raycastTarget = false;
            framedVignetteTexture = CreateFramedVignetteTexture(false);
            mk1VignetteTexture = CreateFramedVignetteTexture(true);
            framedVignetteImage.texture = framedVignetteTexture;
            framedVignetteImage.enabled = false;

            // Create the matte bars after the vignette so they remain the topmost children and
            // cleanly cover everything outside the selected capture rectangle.
            matteImages = new Image[4];
            for (int i = 0; i < matteImages.Length; i++)
            {
                GameObject bar = new GameObject("Matte " + i) { hideFlags = HideFlags.HideAndDontSave };
                bar.transform.SetParent(matteObject.transform, false);
                matteImages[i] = bar.AddComponent<Image>();
                matteImages[i].raycastTarget = false;
            }
        }

        private static Texture2D CreateFramedVignetteTexture(bool mk1)
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = mk1 ? "RoweMod MK1 Lens Mask" : "RoweMod Framed Vignette",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = ((y + 0.5f) / size - 0.5f) * 2f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size - 0.5f) * 2f;
                    float radius;
                    float fade;
                    if (mk1)
                    {
                        // The MK1's huge front element produces a wider, side-heavy death-lens
                        // falloff instead of a perfectly circular software vignette. A tiny upward
                        // optical offset and darker lower corners keep the mask from looking like
                        // a generic centered oval while remaining resolution independent.
                        float shiftedY = ny + 0.035f;
                        radius = Mathf.Sqrt(
                            nx * nx / (0.94f * 0.94f) +
                            shiftedY * shiftedY / (1.12f * 1.12f));
                        float radial = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.InverseLerp(0.62f, 1.12f, radius));
                        float side = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, Mathf.Abs(nx)));
                        float lowerCorner = Mathf.Clamp01(-ny) * side * 0.16f;
                        fade = Mathf.Clamp01(Mathf.Max(radial, side * 0.58f) + lowerCorner);
                    }
                    else
                    {
                        radius = Mathf.Sqrt(nx * nx + ny * ny);
                        fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.18f, radius));
                    }
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(fade * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void SetMatte(Image image, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            image.enabled = color.a > 0.001f && xMax - xMin > 0.0001f && yMax - yMin > 0.0001f;
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
            if (framedVignetteTexture != null)
                UnityObject.Destroy(framedVignetteTexture);
            if (mk1VignetteTexture != null)
                UnityObject.Destroy(mk1VignetteTexture);
            matteObject = null;
            matteImages = null;
            framedVignetteImage = null;
            framedVignetteTexture = null;
            mk1VignetteTexture = null;
            matteScreenWidth = 0;
            matteScreenHeight = 0;
            matteMode = -1;
            matteOpacity = -1f;
            matteVignette = -1f;
            matteMk1Enabled = false;
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
            ResolveReplayOutputCamera();

            freeCamCollider = freeCam == null ? null : freeCam._thisCollider;
            RebindTargetTransform();
        }

        private static UnityCamera ResolveReplayOutputCamera()
        {
            if (recordableCamera != null)
            {
                CinemachineBrain recordableBrain = recordableCamera.cinemachineBrain;
                if (recordableBrain != null)
                {
                    cinematicsBrain = recordableBrain;
                    UnityCamera brainOutput = recordableBrain.OutputCamera;
                    if (brainOutput != null && brainOutput.isActiveAndEnabled)
                    {
                        replayOutputCamera = brainOutput;
                        return replayOutputCamera;
                    }
                }

                UnityCamera recordedCamera = recordableCamera.cam;
                if (recordedCamera != null && recordedCamera.isActiveAndEnabled)
                {
                    replayOutputCamera = recordedCamera;
                    return replayOutputCamera;
                }
            }

            if (cinematicsBrain != null)
            {
                UnityCamera brainOutput = cinematicsBrain.OutputCamera;
                if (brainOutput != null && brainOutput.isActiveAndEnabled)
                {
                    replayOutputCamera = brainOutput;
                    return replayOutputCamera;
                }
            }

            return replayOutputCamera;
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
            ApplyCurrentLightSettings(settings);
        }

        private static void ApplyCurrentLightSettings(ReplaySettings settings)
        {
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

            Transform lightTransform = lightObject.transform;
            if (lightTransform.parent != targetTransform)
                lightTransform.SetParent(targetTransform, false);

            Vector3 desiredPosition = new Vector3(
                settings.cameraLightOffsetX,
                settings.cameraLightOffsetY,
                settings.cameraLightOffsetZ);
            if ((lightTransform.localPosition - desiredPosition).sqrMagnitude > 0.0000001f)
                lightTransform.localPosition = desiredPosition;

            Quaternion desiredRotation = Quaternion.Euler(
                settings.cameraLightPitch,
                settings.cameraLightYaw,
                settings.cameraLightRoll);
            if (Quaternion.Angle(lightTransform.localRotation, desiredRotation) > 0.001f)
                lightTransform.localRotation = desiredRotation;

            LightType desiredType = settings.cameraLightType == 1 ? LightType.Point : LightType.Spot;
            if (lightComponent.type != desiredType)
                lightComponent.type = desiredType;
            if (!lightComponent.enabled)
                lightComponent.enabled = true;
            if (lightComponent.renderMode != LightRenderMode.ForcePixel)
                lightComponent.renderMode = LightRenderMode.ForcePixel;

            // HDRP owns punctual-light intensity through HDAdditionalLightData. Writing only the
            // legacy Light component lets HDRP's default (typically hundreds of lumens) replace
            // the small value displayed by Camera Lab. Candela keeps the slider direct and stable.
            if (hdLightData != null &&
                (hdLightData.lightUnit != LightUnit.Candela ||
                 !Mathf.Approximately(hdLightData.intensity, settings.cameraLightIntensity)))
                hdLightData.SetIntensity(settings.cameraLightIntensity, LightUnit.Candela);
            if (!Mathf.Approximately(lightComponent.intensity, settings.cameraLightIntensity))
                lightComponent.intensity = settings.cameraLightIntensity;

            if (!Mathf.Approximately(lightComponent.range, settings.cameraLightRange))
                lightComponent.range = settings.cameraLightRange;
            if (hdLightData != null && !Mathf.Approximately(hdLightData.range, settings.cameraLightRange))
                hdLightData.range = settings.cameraLightRange;
            if (!Mathf.Approximately(lightComponent.spotAngle, settings.cameraLightSpotAngle))
                lightComponent.spotAngle = settings.cameraLightSpotAngle;

            Color desiredColor = new Color(
                settings.cameraLightColorR,
                settings.cameraLightColorG,
                settings.cameraLightColorB,
                1f);
            if (!ApproximatelyColor(lightComponent.color, desiredColor))
                lightComponent.color = desiredColor;

            LightShadows desiredShadows = !settings.cameraLightShadows
                ? LightShadows.None
                : settings.cameraLightSoftShadows ? LightShadows.Soft : LightShadows.Hard;
            if (lightComponent.shadows != desiredShadows)
                lightComponent.shadows = desiredShadows;
            LightShadowResolution desiredResolution = ToShadowResolution(settings.cameraLightShadowResolution);
            if (lightComponent.shadowResolution != desiredResolution)
                lightComponent.shadowResolution = desiredResolution;
            if (!Mathf.Approximately(lightComponent.shadowStrength, settings.cameraLightShadowStrength))
                lightComponent.shadowStrength = settings.cameraLightShadowStrength;
            if (!Mathf.Approximately(lightComponent.shadowBias, settings.cameraLightShadowBias))
                lightComponent.shadowBias = settings.cameraLightShadowBias;
            if (!Mathf.Approximately(lightComponent.shadowNormalBias, settings.cameraLightShadowNormalBias))
                lightComponent.shadowNormalBias = settings.cameraLightShadowNormalBias;
            if (!Mathf.Approximately(lightComponent.shadowNearPlane, settings.cameraLightShadowNearPlane))
                lightComponent.shadowNearPlane = settings.cameraLightShadowNearPlane;
            if (!Mathf.Approximately(lightComponent.bounceIntensity, 0f))
                lightComponent.bounceIntensity = 0f;
            string desiredStatus = targetTransform == replayOutputCamera?.transform
                ? "Attached to Cinemachine replay camera"
                : "Attached to keyframed replay camera";
            if (!string.Equals(status, desiredStatus, StringComparison.Ordinal))
                status = desiredStatus;
        }

        private static bool ApproximatelyColor(Color a, Color b) =>
            Mathf.Approximately(a.r, b.r) &&
            Mathf.Approximately(a.g, b.g) &&
            Mathf.Approximately(a.b, b.b) &&
            Mathf.Approximately(a.a, b.a);

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
                hdLightData = lightObject.AddComponent<HDAdditionalLightData>();
            }

            if (lightComponent == null)
                lightComponent = lightObject.GetComponent<Light>();
            if (hdLightData == null)
                hdLightData = lightObject.GetComponent<HDAdditionalLightData>();
            if (hdLightData == null)
                hdLightData = lightObject.AddComponent<HDAdditionalLightData>();

            return lightComponent != null && hdLightData != null;
        }

        private static void DestroyLight()
        {
            if (lightObject != null)
                UnityObject.Destroy(lightObject);

            lightObject = null;
            lightComponent = null;
            hdLightData = null;
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
