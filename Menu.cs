using UnityEngine;
using static rowemod.Utils.Memory;
using static rowemod.Config;
using static rowemod.Mods.Misc;
using static rowemod.Mods.Custom;
using static rowemod.Mods.BikeMaterialsLoader;
using Il2CppMashBox.Addons.ContentManagment;
using rowemod.Utils;
using MelonLoader.Utils;
using UnityEngine.InputSystem;
using rowemod.Mods;
using UnityEngine.SceneManagement;
using HarmonyLib;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Character.Scripts;
using Il2CppPlayFab.ClientModels;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using rowemod.Challenges;

namespace rowemod
{
    public static partial class Menu
    {
        // Defining the tabs available in the menu
        public enum Tab
        {
            Physics,
            Camera,
            Replay,
            Bike,
            BikePoser,
            Grinds,
            RiderTools,
            Tricks,
            Character,
            BikeMaterials,
            MX,
            Misc,
            Graphics,
            Marker,
            Dropper,
            Multiplayer,
            Challenge,
            Debug
        }

        // Menu Variables
        public static bool isOpen = false;
        public static Rect windowRect = new Rect(500f, 150f, 1000f, 800f);
        public static Vector2 previousWindowPosition = Vector2.zero;
        public static Vector2 scrollPosition = Vector2.zero;
        public static bool inModelsTab = false;
        public static Tab currentTab;
        public static Slot currentSlot;
        public static float tabWidth => (windowRect.width - 60) / Enum.GetValues(typeof(Tab)).Length;
        public static float scrollOffset = 0f;
        public static float scrollViewHeight = 10000f;
        public static float viewHeight = 0f;
        private static string _lastScrollableContentError = string.Empty;
        private static float _nextScrollableContentErrorLogTime;

        private static float UiScale
        {
            get
            {
                float configuredScale = misc.menuScale > 0f ? misc.menuScale : 1f;
                float userScale = _appliedMenuScale > 0f ? _appliedMenuScale : configuredScale;
                float automaticScale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, Screen.height) / 1080f), 0.90f, 1.25f);
                return Mathf.Clamp(userScale * automaticScale, 0.80f, 1.55f);
            }
        }

        public static float EffectiveUiScale => UiScale;
        public static bool StylesNeedRefresh =>
            !stylesInitialized || _initializedScreenHeight != Screen.height;

        private static float UiOuterPadding => 20f * UiScale;
        private static float UiInnerPadding => 16f * UiScale;
        private static float UiControlSpacing => 8f * UiScale;
        private static float UiSectionSpacing => 16f * UiScale;
        private static float UiTitleBarHeight => 34f * UiScale;
        private static float UiResetButtonWidth => 112f * UiScale;
        private static float UiTabSpacing => 8f * UiScale;
        private static float UiSidebarWidth => 190f * UiScale;
        private static float UiLogoAreaHeight => 96f * UiScale;
        private static float UiContentHeaderHeight => 76f * UiScale;
        private static float UiNavButtonHeight => 40f * UiScale;
        private static float UiFoldoutHeight => 38f * UiScale;
        private static float UiRowHeight => 36f * UiScale;
        private static float UiContentBottomPadding => 24f * UiScale;

        private static readonly (string label, Tab tab)[] _visibleTabs =
        {
            ("Physics", Tab.Physics),
            ("Camera", Tab.Camera),
            ("Replay", Tab.Replay),
            ("Tricks", Tab.Tricks),
            ("Bike", Tab.Bike),
            ("Bike Poser", Tab.BikePoser),
            ("Grinds", Tab.Grinds),
            ("Rider Tools", Tab.RiderTools),
            ("Materials", Tab.BikeMaterials),
            ("Character", Tab.Character),
            ("Misc", Tab.Misc),
            ("Graphics", Tab.Graphics),
            ("Debug", Tab.Debug),
            ("Marker", Tab.Marker),
            ("Dropper", Tab.Dropper),
            ("MP", Tab.Multiplayer)
        };

        private static Vector2 _tabScrollPosition = Vector2.zero;
        
        private static bool isResizing = false;
        private static Vector2 resizeStartMouse;
        private static Vector2 resizeStartSize;
        private static readonly float resizeHandleSize = 20f;
        private static readonly float resizeHandleHitboxSize = 36f;
        private static int resizeControlId = 0;
        private static readonly int resizeControlHint = "RoweMod.MainWindowResize".GetHashCode();
        private static bool isDraggingWindow = false;
        private static Vector2 dragStartMouse;
        private static Vector2 dragStartWindowPosition;
        private static int dragControlId = 0;
        private static readonly int dragControlHint = "RoweMod.MainWindowDrag".GetHashCode();
        private static int activeSliderControlId = 0;

        // Style variables
        public static GUIStyle windowStyle;
        public static GUIStyle labelStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle horizontalSliderStyle;
        public static GUIStyle horizontalSliderThumbStyle;
        public static GUIStyle coloredBoxStyle;
        public static GUIStyle highQualityButtonStyle;
        public static GUIStyle redButtonStyle;
        public static GUIStyle activeTabButtonStyle;
        private static GUIStyle tricksActiveTabButtonStyle;
        public static GUIStyle sectionCardStyle;
        public static GUIStyle sectionHeaderStyle;
        public static GUIStyle tabButtonStyle;
        public static GUIStyle tabBarStyle;
        public static GUIStyle subtleLabelStyle;
        public static GUIStyle pageTitleStyle;
        private static GUIStyle pageEyebrowStyle;
        public static GUIStyle panelStyle;
        public static GUIStyle panelAltStyle;
        public static GUIStyle toolbarStyle;
        public static GUIStyle rowLabelStyle;
        public static GUIStyle rowMutedLabelStyle;
        public static GUIStyle rowButtonStyle;
        public static GUIStyle rowButtonSelectedStyle;
        public static GUIStyle pillButtonStyle;
        public static GUIStyle pillActiveButtonStyle;
        public static GUIStyle miniButtonStyle;
        public static GUIStyle badgeStyle;
        public static GUIStyle mutedWrappedLabelStyle;
        private static GUIStyle subTabButtonStyle;
        private static GUIStyle subTabActiveButtonStyle;
        // Texture caches for styles
        private static Texture2D backgroundTexture;
        private static Texture2D roundedButtonNormal;
        private static Texture2D roundedButtonHover;
        private static Texture2D activeTabBackground;
        private static Texture2D accentColorTexture;
        private static Texture2D tricksTabIndicatorTexture;
        private static Texture2D tabIndicatorTexture;
        private static Texture2D toggleCapsuleMaskTexture;
        private static Texture2D toggleKnobTexture;
        private static readonly List<Texture2D> generatedStyleTextures = new List<Texture2D>();

        private static Color uiAccentColor;
        private static Color uiAccentHoverColor;
        private static Color uiAccentSoftColor;
        private static Color uiBackgroundColor;
        private static Color uiSidebarColor;
        private static Color uiPanelColor;
        private static Color uiPanelHoverColor;
        private static Color uiPanelAltColor;
        private static Color uiBorderColor;
        private static Color uiTextPrimaryColor;
        private static Color uiTextMutedColor;
        private static Color uiAccentTextColor;
        private static Color uiDangerColor;
        private static Color uiDangerHoverColor;
        private static Color uiDangerActiveColor;
        
        public static GUIStyle textFieldStyle;
        public static GUIStyle searchFieldStyle;
        public static bool stylesInitialized = false;
        public static int styleRevision = 0;
        private static float _initializedUiScale = -1f;
        private static float _appliedMenuScale = -1f;
        private static int _initializedScreenHeight = -1;
        private static Font _uiFont;

        public static GUIStyle UiPanelStyle => panelStyle ?? sectionCardStyle ?? GUI.skin.box;
        public static GUIStyle UiPanelAltStyle => panelAltStyle ?? UiPanelStyle;
        public static GUIStyle UiHeaderStyle => sectionHeaderStyle ?? labelStyle ?? GUI.skin.label;
        public static GUIStyle UiMutedStyle => subtleLabelStyle ?? labelStyle ?? GUI.skin.label;
        public static GUIStyle UiMutedWrappedStyle => mutedWrappedLabelStyle ?? UiMutedStyle;
        public static GUIStyle UiButtonStyle => highQualityButtonStyle ?? GUI.skin.button;
        public static GUIStyle UiDangerButtonStyle => redButtonStyle ?? UiButtonStyle;
        public static GUIStyle UiPillStyle => pillButtonStyle ?? UiButtonStyle;
        public static GUIStyle UiPillActiveStyle => pillActiveButtonStyle ?? UiPillStyle;
        public static GUIStyle UiMiniButtonStyle => miniButtonStyle ?? UiButtonStyle;
        public static GUIStyle UiBadgeStyle => badgeStyle ?? GUI.skin.box;
        public static GUIStyle UiSearchFieldStyle => searchFieldStyle ?? textFieldStyle ?? GUI.skin.textField;
        public static GUIStyle UiRowButtonStyle => rowButtonStyle ?? UiButtonStyle;
        public static GUIStyle UiRowButtonSelectedStyle => rowButtonSelectedStyle ?? UiRowButtonStyle;
        public static GUIStyle UiRowLabelStyle => rowLabelStyle ?? labelStyle ?? GUI.skin.label;
        public static GUIStyle UiRowMutedLabelStyle => rowMutedLabelStyle ?? subtleLabelStyle ?? labelStyle ?? GUI.skin.label;

        // Dictionaries and caches
        public static Dictionary<Slot, GameObject> slotGameObjects = new Dictionary<Slot, GameObject>();
        public static List<UnityEngine.Camera> cachedCameras = new List<UnityEngine.Camera>();
        public static List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData> cachedHDRCameras = new List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        public static List<UnityEngine.Rendering.Volume> cachedVolumes = new List<UnityEngine.Rendering.Volume>();
        public static Texture2D logoTexture;
        public static bool HasLogoTexture => logoTexture != null;
        public static string LogoTextureDebugInfo => logoTexture == null ? "null" : $"{logoTexture.width}x{logoTexture.height}";
        private static float mxTopSpeed = 100f;
        private static float mxAcceleration = 10f;
        private static bool hasInitializedMxSettings = false;
        private static float mxChassisMass = 50f;
        private static float mxAccelerationForce = 10f;
        private static float mxTerminalVelocity = 80f;
        private const float DefaultComponentSliderMin = -15f;
        private const float DefaultComponentSliderMax = 15f;
        private const float GraphicsDebugLogIntervalSeconds = 3f;
        private static readonly Dictionary<string, float> _graphicsDebugLastLogTimes = new Dictionary<string, float>();
        private static readonly Dictionary<int, float> _cachedLightIntensityById = new Dictionary<int, float>();
        private static readonly List<(Volume volume, VolumeProfile profile, VolumeComponent component, int componentIndex)> _cachedExposureProfiles =
            new List<(Volume volume, VolumeProfile profile, VolumeComponent component, int componentIndex)>();
        private static bool _usingExposureFallback = false;
        private static float _nextExposureRescanTime = 0f;
        // Cache for circular knob texture
        private static Texture2D _circleTex;
        
        
        private static string fovInput = "60";
        private static float fovInputValue = 60f;

        public static bool isLogoLoaded = false;
        public static bool isLogoLoading = false;
        
        // Foldout states for Physics tab
        private static bool mxExpanded = true;
        private static bool challengeSettingsExpanded = true;
        private static bool _motorTuningNeedsRefresh = true;
        private static string _motorTuningStatusText = "Open Motor Tuning to load values.";
        private static readonly List<MotorTuningEntry> _motorTuningEntries = new List<MotorTuningEntry>();
        private static readonly Dictionary<string, MotorTuningConfigEntry> _motorTuningDefaults = new Dictionary<string, MotorTuningConfigEntry>();

        private sealed class MotorTuningEntry
        {
            public string VehicleName;
            public string ConfigKey;
            public MotorVehicleSettings VehicleSettings;
            public float DefaultForceFactor;
            public float DefaultMaxForce;
            public float DefaultMaxSpeed;
        }
        
        //-------------------------------------------------------------------
        // MENU & TAB LOGIC
        //-------------------------------------------------------------------

        public static void DrawMenu(int windowID)
        {
            BeginControllerNavigationFrame();
            try
            {
                using (new MenuSkinScope())
                {
                    HandleWindowResize();
                    DrawSidebar();
                    DrawTabs();
                    DrawContentHeader();
                    DrawScrollableContent();

                    DrawResizeHandle();
                    if (GUI.changed)
                        Config.MarkDirty();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawMenu: {ex.Message}");
            }
            finally
            {
                EndControllerNavigationFrame();
                // GUI.Window is composited above normal screen-level IMGUI. Draw the same
                // cached pointer once inside this window so it remains visible over controls;
                // the screen-level copy supplies any portion extending beyond the window edge.
                RoweModCursor.DrawInsideMenuWindow();
            }
        }

        private static void DrawResizeHandle()
        {
            Rect resizeRect = new Rect(windowRect.width - resizeHandleSize, windowRect.height - resizeHandleSize,
                resizeHandleSize, resizeHandleSize);
            DrawSolidColorRect(resizeRect, new Color(1f, 1f, 1f, 0.16f));
        }

        private static void HandleWindowResize()
        {
            Event e = Event.current;
            if (e == null)
                return;

            int controlId = GUIUtility.GetControlID(resizeControlHint, FocusType.Passive);
            EventType eventType = e.GetTypeForControl(controlId);
            Rect resizeHitbox = new Rect(windowRect.width - resizeHandleHitboxSize,
                windowRect.height - resizeHandleHitboxSize, resizeHandleHitboxSize, resizeHandleHitboxSize);

            if (eventType == EventType.MouseDown && e.button == 0 && resizeHitbox.Contains(e.mousePosition))
            {
                isResizing = true;
                resizeControlId = controlId;
                GUIUtility.hotControl = controlId;
                resizeStartMouse = e.mousePosition;
                resizeStartSize = new Vector2(windowRect.width, windowRect.height);
                e.Use();
                return;
            }

            if (!isResizing || resizeControlId != controlId)
                return;

            if (eventType == EventType.MouseDrag)
            {
                Vector2 delta = e.mousePosition - resizeStartMouse;
                windowRect.width = Mathf.Max(640f * UiScale, resizeStartSize.x + delta.x);
                windowRect.height = Mathf.Max(460f * UiScale, resizeStartSize.y + delta.y);
                ClampWindowToScreen();
                e.Use();
                return;
            }

            if (eventType == EventType.MouseUp || e.rawType == EventType.MouseUp)
            {
                isResizing = false;
                resizeControlId = 0;
                if (GUIUtility.hotControl == controlId)
                    GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        public static void HandleWindowDrag()
        {
            Event e = Event.current;
            if (e == null)
                return;

            int controlId = GUIUtility.GetControlID(dragControlHint, FocusType.Passive);
            EventType eventType = e.GetTypeForControl(controlId);
            Rect titleBarRect = new Rect(windowRect.x, windowRect.y, windowRect.width, UiTitleBarHeight);

            if (eventType == EventType.MouseDown && e.button == 0 && titleBarRect.Contains(e.mousePosition))
            {
                isDraggingWindow = true;
                dragControlId = controlId;
                GUIUtility.hotControl = controlId;
                dragStartMouse = e.mousePosition;
                dragStartWindowPosition = windowRect.position;
                e.Use();
                return;
            }

            if (!isDraggingWindow || dragControlId != controlId)
                return;

            if (eventType == EventType.MouseDrag)
            {
                Vector2 delta = e.mousePosition - dragStartMouse;
                windowRect.position = dragStartWindowPosition + delta;
                ClampWindowToScreen();
                e.Use();
                return;
            }

            if (eventType == EventType.MouseUp || e.rawType == EventType.MouseUp)
            {
                isDraggingWindow = false;
                dragControlId = 0;
                if (GUIUtility.hotControl == controlId)
                    GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private static void ClampWindowToScreen()
        {
            float minVisibleWidth = Mathf.Min(80f, windowRect.width);
            float minX = Mathf.Min(0f, minVisibleWidth - windowRect.width);
            float maxX = Mathf.Max(0f, Screen.width - minVisibleWidth);
            float maxY = Mathf.Max(0f, Screen.height - UiTitleBarHeight);

            windowRect.x = Mathf.Clamp(windowRect.x, minX, maxX);
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, maxY);
        }

        public static void DrawLogoRevealOverlay(float revealAmount, float alpha)
        {
            if (logoTexture == null || alpha <= 0f || revealAmount <= 0f)
                return;

            float clampedReveal = Mathf.Clamp01(revealAmount);
            float revealHeight = windowRect.height * clampedReveal;
            if (revealHeight <= 0.5f)
                return;

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

            Rect clipRect = new Rect(windowRect.x, windowRect.y, windowRect.width, revealHeight);
            GUI.BeginGroup(clipRect);
            Rect fittedRect = GetLogoWidthFitRect(0f, 0f, windowRect.width);
            GUI.DrawTexture(fittedRect, logoTexture, ScaleMode.StretchToFill, true);
            GUI.EndGroup();

            GUI.color = previousColor;
        }

        private static Rect GetLogoWidthFitRect(float x, float y, float width)
        {
            if (logoTexture == null || logoTexture.width <= 0 || logoTexture.height <= 0)
                return new Rect(x, y, width, 0f);

            float drawHeight = width * (logoTexture.height / (float)logoTexture.width);
            return new Rect(x, y, width, drawHeight);
        }

        private static void DrawLegacyTabContent()
        {
            try
            {
                switch (currentTab)
                {
                    case Tab.Physics:
                        bool previousGuiChanged = GUI.changed;
                        GUI.changed = false;

                        float physicsPaneHeight = GetContentPaneHeight(24f);
                        BeginTwoPane(physicsPaneHeight);

                        BeginPane("Physics", "Core riding helpers and bike force tuning.", GUILayout.Width(Mathf.Max(320f, windowRect.width * 0.42f)), GUILayout.Height(physicsPaneHeight));
                        ModernToggle("Spin Assist", ref physics.spinAssist);
                        ModernToggle("Grind Align Assist", ref physics.grindAlignAssist);
                        if (physics.grindAlignAssist)
                            Slider("Grind Assist Force Multiplier", ref physics.grindAssistStrength, 0.5f, 0f, 10f);
                        ModernToggle("Drifting", ref physics.driftAbility);
                        Slider("Gravity", ref physics.gravity, 12.5f, 0f, 30f);
                        Slider("Small Hop Force", ref physics.smallHopForce, 4.2f, 0f, 25f);

                        DrawSectionTitle("Global Speed");
                        GUILayout.Label("Used by vehicles that do not have per-vehicle motor tuning enabled.", UiMutedWrappedStyle);
                        Slider("Global Push Force", ref physics.bmxForceFactor, 0.07f, 0.05f, 2f);
                        Slider("Global Max Speed", ref physics.bmxMaxSpeed, 7.5f, 2f, 15f);
                        EndPane();

                        GUILayout.Space(8f);

                        BeginPane("Advanced", "Pump, spin, manuals, and runtime motor settings.", GUILayout.ExpandWidth(true), GUILayout.Height(physicsPaneHeight));
                        DrawSectionTitle("Pump / Spin");
                        Slider("Pump Force", ref physics.pumpForce, 1.5f, 1f, 5f);
                        Slider("Spin Speed Multiplier", ref physics.spinMultiplier, 1.0f, 0f, 10f);
                        Slider("Steer Damping", ref physics.steerDamp, 5f, 0f, 5f);

                        DrawSectionTitle("Manuals");
                        Slider("Max Nose Manual Angle", ref physics.noseManualAngle, 30f, 10f, 50f);
                        Slider("Max Manual Angle", ref physics.manualAngle, 30f, 10f, 50f);
                        ModernToggle("Nose Manual COM / Inertia Tuning", ref physics.noseManualTurnTuning);
                        if (physics.noseManualTurnTuning)
                        {
                            DrawSectionTitle("Nose Manual Center Of Mass");
                            GUILayout.Label("Offsets are local to the bike and restore after the physical nose pivot ends. Turn Lean shifts the rider sideways with steering input.", UiMutedWrappedStyle);
                            Slider("Chassis COM Forward", ref physics.noseManualChassisComForwardOffset, 0f, -1f, 1f);
                            Slider("Chassis COM Height", ref physics.noseManualChassisComVerticalOffset, 0f, -1f, 1f);
                            Slider("Rider COM Forward", ref physics.noseManualDriverComForwardOffset, 0f, -1f, 1f);
                            Slider("Rider COM Height", ref physics.noseManualDriverComVerticalOffset, 0f, -1f, 1f);
                            Slider("Rider COM Turn Lean", ref physics.noseManualComTurnLean, 0f, -0.5f, 0.5f);
                            Slider("Nose Rider Inertia", ref physics.noseManualDriverInertiaMultiplier, 1f, 0.25f, 3f);
                            ModernToggle("Debug Nose Manual Logs", ref physics.noseManualDebugLogging);
                        }

                        DrawSectionTitle("Motor Tuning");
                        GUILayout.Label("Per-vehicle tuning is opt-in. Disabled vehicles use the global speed settings on the left.", UiMutedWrappedStyle);
                        if (_motorTuningNeedsRefresh)
                            RefreshMotorTuningData();
                        DrawMotorTuningData();
                        EndPane();

                        EndTwoPane();

                        bool physicsSettingsChanged = GUI.changed;
                        GUI.changed |= previousGuiChanged;
                        if (physicsSettingsChanged)
                            Mods.Physics.Update();

                        break;
                    case Tab.Camera:
                        DrawCameraSettings();
                        break;
                    case Tab.Replay:
                        DrawReplaySettings();
                        break;
                    case Tab.Bike:
                        float bikePaneHeight = GetContentPaneHeight(24f);
                        BeginTwoPane(bikePaneHeight);
                        PartTweaker.DrawPartTweaker(GUILayout.Width(Mathf.Max(330f, windowRect.width * 0.46f)), GUILayout.Height(bikePaneHeight));
                        GUILayout.Space(8f);
                        PartTweaker.DrawPartSelectorUI(GUILayout.ExpandWidth(true), GUILayout.Height(bikePaneHeight));
                        EndTwoPane();
                        break;
                    case Tab.BikePoser:
                        BikePoseEditor.DrawTab();
                        break;
                    case Tab.Grinds:
                        GrindPoseEditor.DrawGrindPoseTab();
                        break;
                    case Tab.RiderTools:
                        RiderStyleEditor.DrawTab();
                        break;
                    case Tab.Tricks:
                        TrickMods.DrawTrickMenuPro();
                        break;

                    case Tab.Character:
                        Custom.DrawCharacterTab();
                        break;

                    case Tab.BikeMaterials:
                        BikeMaterialsLoader.DrawBikeMaterialsTabUI();
                        break;
                    /*
                    case Tab.MX:
                        if (!hasInitializedMxSettings)
                        {
                            Log.Msg("Initializing MX Vehicle Settings...");
                            MotorVehicleUtils.FindMxVehicleSettings();
                            var mx = MotorVehicleUtils.mxVehicleSettings;
                            if (mx != null)
                            {
                                mxTopSpeed = mx.TopSpeed;
                                mxChassisMass = mx.ChassisMass;
                                mxAccelerationForce = mx.AccelerationForce;
                                mxTerminalVelocity = mx.TerminalVelocity;
                                //Log.Msg($"MX Settings Found - Top Speed: {mxTopSpeed}, Acceleration: {mxAcceleration}");
                            }
                            else
                            {
                                Log.Error("No MX Vehicle Settings found during initialization.");
                            }
                            hasInitializedMxSettings = true;
                        }

                        if (MotorVehicleUtils.mxVehicleSettings != null)
                        {
                            mxExpanded = ModernFoldout("MX Vehicle Tuning", mxExpanded);
                            if (mxExpanded)
                            {
                                GUILayout.Space(10);
                                ModernSlider("Speed", ref mxTopSpeed, 5f, 30000f);
                                ModernSlider("Chassis Mass", ref mxChassisMass, 1f, 500f);
                                ModernSlider("Acceleration Force", ref mxAccelerationForce, 1f, 1000f);
                                ModernSlider("Top speed", ref mxTerminalVelocity, 10f, 3000f);
                            }

                            var mx = MotorVehicleUtils.mxVehicleSettings;
                            mx.TopSpeed = mxTopSpeed;
                            mx.ChassisMass = mxChassisMass;
                            mx.AccelerationForce = mxAccelerationForce;
                            mx.TerminalVelocity = mxTerminalVelocity;

                            //Log.Msg($"Updated MX Settings - Top Speed: {mxTopSpeed}, AccelerationForce: {mxAccelerationForce}");
                        }
                        else
                        {
                            GUILayout.Label("No MX vehicle found.", labelStyle);
                            Log.Error("No MX Vehicle available to display settings.");
                        }
                        break;
                        */


                    case Tab.Misc:
                        BeginPane("Drone", "Toggle drone visuals and physics settings.");
                        ModernToggle("Toggle Drone Body", ref misc.droneBodyToggle);
                        ModernToggle("Toggle Drone Sound", ref misc.droneEmitterToggle);
                        ModernToggle("Toggle Drone Colliders", ref misc.disableDroneCollider);
                        Slider("Drone Mass", ref misc.droneMass, 10f, 2f, 1000f);
                        EndPane();

                        BeginPane("General", "Gameplay helpers, cleanup actions, and menu accent color.");
                        ModernToggle("Skip Main Intro", ref Config.autoSkipIntro);
                        ModernToggle("No Bail", ref misc.neverBail);
                        bool boneBreakingEnabled = !misc.disableBoneBreaking;
                        ModernToggle("Bone Breaking", ref boneBreakingEnabled, "misc_bone_breaking");
                        bool disableBoneBreaking = !boneBreakingEnabled;
                        if (disableBoneBreaking != misc.disableBoneBreaking)
                        {
                            misc.disableBoneBreaking = disableBoneBreaking;
                            Mods.Misc.ApplyBoneBreakingState(true);
                        }
                        if (boneBreakingEnabled)
                        {
                            float previousBoneBreakingStrength = misc.boneBreakingStrength;
                            Slider("Bone Strength", ref misc.boneBreakingStrength, 1f, 0.25f, 5f);
                            if (!Mathf.Approximately(previousBoneBreakingStrength, misc.boneBreakingStrength))
                                Mods.Misc.ApplyBoneBreakingState(true);
                        }
                        ModernToggle("Disable Replay Cam Collider", ref misc.disableFreeCamCollider);

                        BeginToolbar();
                        if (DangerButton("Remove Skidmarks", GUILayout.Width(150f), GUILayout.Height(26f)))
                        {
                            Memory.RemoveSkidmarks();
                        }
                        if (SecondaryButton("Spawn Drift Car", GUILayout.Width(135f), GUILayout.Height(26f)))
                        {
                            SpawnDriftCarInFrontOfPlayer();
                        }
                        if (SecondaryButton("Spawn Drift Trike", GUILayout.Width(145f), GUILayout.Height(26f)))
                        {
                            SpawnDriftTrikeInFrontOfPlayer();
                        }
                        EndToolbar();

                        DrawSectionTitle("Menu Accent");
                        ModernSlider("Menu Color R", ref misc.menuAccentR, 0f, 1f);
                        ModernSlider("Menu Color G", ref misc.menuAccentG, 0f, 1f);
                        ModernSlider("Menu Color B", ref misc.menuAccentB, 0f, 1f);

                        if (PrimaryButton("Apply Menu Color", GUILayout.Width(150f), GUILayout.Height(26f)))
                        {
                            stylesInitialized = false;
                        }
                        EndPane();

                        break;

                    case Tab.Graphics:
                        DrawGraphicsSettings();
                        break;

                    case Tab.Debug:
                        DebugTools.DrawDebugTab();
                        break;

                    case Tab.Marker:
                        BeginPane("Session Markers", "Choose a marker prefab replacement for session markers.");
                        int count = 0;
                        foreach (GameObject marker in sessionMarkers.Where(m => m != null))
                        {
                            count++;
                            if (ControllerButton($"legacy_marker_{marker.name}", marker.name, UiRowButtonStyle,
                                    GUILayout.Height(36f * UiScale)))
                            {
                                ReplaceSessionMarkerWithPrefab(marker);
                                Config.misc.customSessionMarker = marker.name;
                            }
                        }

                        if (count == 0)
                            DrawEmptyState("No session markers found", "Load into gameplay or refresh marker data, then reopen this tab.");

                        GUILayout.Space(8f);
                        GUILayout.Label("Current Selected Marker: " + (Config.misc.customSessionMarker ?? "None"), UiMutedWrappedStyle);
                        EndPane();
                        break;
                    
                    // Adding case for the new Dropper tab
                    case Tab.Dropper:
                        ObjectDropper.DrawDropperTab();
                        break;
                    
                    case Tab.Multiplayer:
                        BeginPane("Player Labels", "Name tag visibility and multiplayer challenge controls.");
                        bool previousShowPlayerUserNameTargets = misc.showPlayerUserNameTargets;
                        ModernToggle("Show PlayerUserNameTarget", ref misc.showPlayerUserNameTargets, "mp_show_player_username_targets");
                        if (previousShowPlayerUserNameTargets != misc.showPlayerUserNameTargets)
                        {
                            ApplyPlayerUserNameTargetsVisibility(true);
                        }

                        BeginToolbar();
                        if (SecondaryButton("Refresh Player Name Targets", GUILayout.Width(205f), GUILayout.Height(26f)))
                        {
                            ApplyPlayerUserNameTargetsVisibility(true);
                        }

                        GUI.enabled = MultiplayerChallengeManager.FeatureEnabled;
                        if (PrimaryButton(MultiplayerChallengeManager.IsOpen ? "Close Challenge UI" : "Open Challenge UI", GUILayout.Width(160f), GUILayout.Height(26f)))
                        {
                            MultiplayerChallengeManager.ToggleWindow();
                        }
                        GUI.enabled = true;
                        EndToolbar();
                        if (!MultiplayerChallengeManager.FeatureEnabled)
                            GUILayout.Label("MP BIKE challenge work is disabled for now. The code is still kept for later.", UiMutedWrappedStyle);
                        EndPane();
                        break;
                    
                    
                    /*
                    case Tab.Challenge:
                        challengeSettingsExpanded = ModernFoldout("Challenge Settings", challengeSettingsExpanded);

                        if (challengeSettingsExpanded)
                        {
                            if (ModernButton("Spawn Challenge Area", 250f))
                            {
                                Vector3 spawnPos = Vector3.zero;
                                Quaternion spawnRot = Quaternion.identity;

                                if (Utils.Memory.physicsDrivenCharacter != null)
                                {
                                    spawnPos = Utils.Memory.physicsDrivenCharacter.transform.position;
                                    spawnRot = Utils.Memory.physicsDrivenCharacter.transform.rotation;
                                }
                                else if (UnityEngine.Camera.main != null)
                                {
                                    spawnPos = UnityEngine.Camera.main.transform.position + UnityEngine.Camera.main.transform.forward * 5f;
                                    spawnRot = UnityEngine.Camera.main.transform.rotation;
                                }

                                Challenges.ChallengeAreaManager.Create(
                                    spawnPos,
                                    new Vector3(Config.challengeSettings.challengeSizeX, Config.challengeSettings.challengeSizeY, Config.challengeSettings.challengeSizeZ),
                                    spawnRot
                                );
                                
                                // Apply visibility immediately
                                Challenges.ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
                            }

                            if (Challenges.ChallengeAreaManager.Active != null)
                            {
                                if (ModernButton("Destroy Challenge Area", 250f))
                                {
                                    Challenges.ChallengeAreaManager.DestroyActive();
                                }

                                if (ModernToggle("Visible", ref Config.challengeSettings.challengeVisible))
                                {
                                    Challenges.ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
                                }

                                GUILayout.Label("Size", labelStyle);
                                
                                float oldX = Config.challengeSettings.challengeSizeX;
                                float oldY = Config.challengeSettings.challengeSizeY;
                                float oldZ = Config.challengeSettings.challengeSizeZ;

                                ModernSlider("Width", ref Config.challengeSettings.challengeSizeX, 1f, 50f);
                                ModernSlider("Height", ref Config.challengeSettings.challengeSizeY, 1f, 50f);
                                ModernSlider("Depth", ref Config.challengeSettings.challengeSizeZ, 1f, 50f);

                                if (oldX != Config.challengeSettings.challengeSizeX || 
                                    oldY != Config.challengeSettings.challengeSizeY || 
                                    oldZ != Config.challengeSettings.challengeSizeZ)
                                {
                                    Challenges.ChallengeAreaManager.SetSize(new Vector3(
                                        Config.challengeSettings.challengeSizeX, 
                                        Config.challengeSettings.challengeSizeY, 
                                        Config.challengeSettings.challengeSizeZ));
                                }
                                
                                if (ModernButton("Teleport to Me", 200f))
                                {
                                    if (Utils.Memory.physicsDrivenCharacter != null)
                                    {
                                        Challenges.ChallengeAreaManager.SetPosition(Utils.Memory.physicsDrivenCharacter.transform.position);
                                        Challenges.ChallengeAreaManager.SetRotation(Utils.Memory.physicsDrivenCharacter.transform.rotation);
                                    }
                                }
                            }
                            else
                            {
                                GUILayout.Label("No active challenge area.", labelStyle);
                            }
                        }
                        break;
                        */
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawTabContent: {ex.Message}");
            }
        }

        //-------------------------------------------------------------------
        // SCROLL & TABS
        //-------------------------------------------------------------------

        private static float GetContentX()
        {
            return GetResponsiveSidebarWidth() + UiOuterPadding;
        }

        private static float GetContentWidth()
        {
            return Mathf.Max(220f, windowRect.width - GetContentX() - UiOuterPadding);
        }

        public static float ActiveContentWidth => GetContentWidth();

        private static Rect GetContentVisibleRect()
        {
            float y = UiTitleBarHeight + UiOuterPadding + GetNavigationHeaderHeight() + UiControlSpacing;
            float height = Mathf.Max(120f, windowRect.height - y - UiContentBottomPadding);
            return new Rect(GetContentX(), y, GetContentWidth(), height);
        }

        private static string GetCurrentTabLabel()
        {
            return GetCurrentPageLabel();
        }

        public static void SelectNextVisibleTab()
        {
            SelectArea(1);
        }

        public static void SelectPreviousVisibleTab()
        {
            SelectArea(-1);
        }

        public static void AdjustControllerScroll(float delta)
        {
            if (AdjustControllerFocusedScroll(delta))
                return;
            if (AdjustControllerOverlayScroll(delta))
                return;

            scrollOffset += delta;
            scrollOffset = Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, scrollViewHeight - viewHeight));
        }

        private static void SelectVisibleTab(int direction)
        {
            if (_visibleTabs.Length == 0)
                return;

            int currentIndex = GetVisibleTabIndex(currentTab);
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = (currentIndex + direction + _visibleTabs.Length) % _visibleTabs.Length;
            SetCurrentTab(_visibleTabs[nextIndex].tab);
            Log.Msg($"[ControllerMenu] Tab {currentIndex}->{nextIndex}: {_visibleTabs[nextIndex].label}.");
        }

        private static int GetVisibleTabIndex(Tab tab)
        {
            for (int i = 0; i < _visibleTabs.Length; i++)
            {
                if (_visibleTabs[i].tab == tab)
                    return i;
            }

            return -1;
        }

        private static void EnsureCurrentTabVisible()
        {
            EnsureSelectedAreaVisible();
        }

        private static void DrawSidebar()
        {
            float sidebarWidth = GetResponsiveSidebarWidth();
            Rect sidebarRect = new Rect(0f, 0f, sidebarWidth, windowRect.height);
            DrawSolidColorRect(sidebarRect, uiSidebarColor);

            Rect dividerRect = new Rect(sidebarWidth - 1f, UiTitleBarHeight, 1f, windowRect.height - UiTitleBarHeight);
            DrawSolidColorRect(dividerRect, uiBorderColor);

            DrawLogoHeader();
        }

        private static void DrawLogoHeader()
        {
            float sidebarWidth = GetResponsiveSidebarWidth();
            // Keep the brand mark in the actual upper-left corner of the menu. The version stays
            // beneath it so the drag title bar remains clear and the logo never becomes a sidebar heading.
            Rect logoAreaRect = new Rect(UiOuterPadding, 3f * UiScale,
                sidebarWidth - (UiOuterPadding * 2f), UiTitleBarHeight - (6f * UiScale));
            if (logoTexture != null && logoTexture.width > 0 && logoTexture.height > 0)
            {
                float maxLogoWidth = logoAreaRect.width;
                float maxLogoHeight = logoAreaRect.height;
                float scale = Mathf.Min(maxLogoWidth / logoTexture.width, maxLogoHeight / logoTexture.height);
                float logoWidth = logoTexture.width * scale;
                float logoHeight = logoTexture.height * scale;
                Rect logoRect = new Rect(logoAreaRect.x, logoAreaRect.y, logoWidth, logoHeight);

                Color previousColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
            }
            else
            {
                GUI.Label(logoAreaRect, "RoweMod", sectionHeaderStyle);
            }

            Rect versionRect = new Rect(UiOuterPadding, UiTitleBarHeight + (8f * UiScale),
                sidebarWidth - (UiOuterPadding * 2f), 20f * UiScale);
            GUI.Label(versionRect, $"v. {Main.ModVersion}", subtleLabelStyle);
        }

        private static void DrawContentHeader()
        {
            DrawNavigationHeader();
        }

        private static void HandleScrolling()
        {
            try
            {
                if (IsTrickMappingContentActive())
                {
                    scrollOffset = 0f;
                    return;
                }

                Event currentEvent = Event.current;
                if (currentEvent == null || currentEvent.type != EventType.ScrollWheel)
                    return;

                Rect contentRect = GetContentVisibleRect();

                if (contentRect.Contains(currentEvent.mousePosition))
                {
                    scrollOffset += currentEvent.delta.y * 14f;
                    scrollOffset = Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, scrollViewHeight - viewHeight));
                    currentEvent.Use();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in HandleScrolling: {ex.Message}");
            }
        }

        private static void DrawScrollableContent()
        {
            bool beganGroup = false;
            bool beganArea = false;
            bool beganVertical = false;
            try
            {
                HandleScrolling();
                Rect visibleArea = GetContentVisibleRect();
                float visibleAreaHeight = visibleArea.height;
                float contentWidth = visibleArea.width;
                viewHeight = visibleAreaHeight;
                GUI.BeginGroup(visibleArea);
                beganGroup = true;
                GUILayout.BeginArea(new Rect(0f, -scrollOffset, contentWidth, Mathf.Max(scrollViewHeight, visibleAreaHeight)));
                beganArea = true;
                GUILayout.BeginVertical(GUILayout.Width(contentWidth));
                beganVertical = true;
                GUILayout.Space(2f);
                SetControllerScrollableContext(true);
                DrawTabContent();
                SetControllerScrollableContext(false);
                GUILayout.Space(UiContentBottomPadding);

                // GUILayoutUtility.GetLastRect() is stripped from BMX Streets' IL2CPP player
                // and throws after an otherwise successful menu draw. A zero-height layout rect
                // gives us the same end-of-content Y position through the GetRect path already
                // used by every RoweMod control.
                Rect contentEndRect = GUILayoutUtility.GetRect(
                    0f,
                    0f,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(0f));

                if (Event.current.type == EventType.Repaint)
                    scrollViewHeight = Mathf.Max(visibleAreaHeight, contentEndRect.yMax + 2f);

                scrollOffset = Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, scrollViewHeight - viewHeight));
            }
            catch (Exception ex)
            {
                string fingerprint = $"{ex.GetType().FullName}: {ex.Message}";
                if (!string.Equals(_lastScrollableContentError, fingerprint, StringComparison.Ordinal) ||
                    Time.unscaledTime >= _nextScrollableContentErrorLogTime)
                {
                    _lastScrollableContentError = fingerprint;
                    _nextScrollableContentErrorLogTime = Time.unscaledTime + 5f;
                    Log.Error($"Error in DrawScrollableContent: {ex}");
                }
            }
            finally
            {
                SetControllerScrollableContext(false);
                if (beganVertical)
                    GUILayout.EndVertical();
                if (beganArea)
                    GUILayout.EndArea();
                if (beganGroup)
                    GUI.EndGroup();
            }
        }

        private static void DrawTabs()
        {
            DrawAreaNavigation();
        }

        public static void NotifyMenuOpened()
        {
            _motorTuningNeedsRefresh = true;
            EnsureNavigationInitialized();
            ResetControllerNavigation(false, true);
            if (_selectedPage == MenuPage.GraphicsSceneLights)
                RefreshSceneLightCache();
        }

        private static void ResetCurrentTab()
        {
            switch (currentTab)
            {
                case Tab.Physics:
                    ResetPhysicsTab();
                    RestoreMotorTuningDefaults();
                    _motorTuningNeedsRefresh = true;
                    Mods.Physics.Update();
                    break;
                case Tab.Camera:
                    Config.ResetCameraTab();
                    break;
                case Tab.Replay:
                    Config.ResetReplayTab();
                    ReplayCameraLight.OnSettingsChanged();
                    ReplayCameraLight.OnLensSettingsChanged();
                    break;
                case Tab.Tricks:
                    TrickMods.ResetCustomTricks();
                    break;
                case Tab.Bike:
                    Config.ResetBikeTab();
                    ReloadAssetsFromCachedBundles();
                    if (customizableEntity != null)
                    {
                        customizableEntity.EquipItems();
                        customizableEntity.EquipItems();
                    }
                    Memory.FindObjects(Memory.rMbCharacter);
                    PartTweaker.FindParts();
                    PartTweaker.UpdatePartTransforms();
                    Log.Msg("Bike Tab reset!");
                    break;
                case Tab.BikePoser:
                    BikePoseEditor.ResetTab();
                    break;
                case Tab.Grinds:
                    GrindPoseEditor.ResetAllPosesToDefault();
                    break;
                case Tab.RiderTools:
                    RiderStyleEditor.ResetAll();
                    break;
                case Tab.Character:
                    ResetCharacterTab();
                    break;
                case Tab.BikeMaterials:
                    ResetBikeMaterialsTab();
                    if (customizableEntity != null)
                    {
                        customizableEntity.EquipItems();
                        customizableEntity.EquipItems();
                    }
                    Memory.FindObjects(Memory.rMbCharacter);
                    BikeMaterialsLoader.Initialize();
                    BikeMaterialsLoader.ResetTabState();
                    break;
                case Tab.MX:
                    hasInitializedMxSettings = false;
                    break;
                case Tab.Misc:
                    ResetMiscTab();
                    ApplyConfiguredInterfaceScale();
                    Mods.Misc.Update();
                    break;
                case Tab.Graphics:
                    ResetGraphicsTab();
                    break;
                case Tab.Debug:
                    DebugTools.ResetTab();
                    break;
                case Tab.Marker:
                    Memory.ResetSessionMarkerToDefault();
                    break;
                case Tab.Dropper:
                    ObjectDropper.ResetTab();
                    Log.Msg("Dropper Tab reset!");
                    break;
                case Tab.Multiplayer:
                    misc.showPlayerUserNameTargets = true;
                    ApplyPlayerUserNameTargetsVisibility(true);
                    ResetChallengeSettings(false);
                    MultiplayerChallengeManager.ResetWindowState();
                    break;
                case Tab.Challenge:
                    ResetChallengeSettings(true);
                    break;
            }

            Config.Save();
        }

        private static void RestoreMotorTuningDefaults()
        {
            MotorVehicleSettings[] settings = Resources.FindObjectsOfTypeAll<MotorVehicleSettings>();
            if (settings == null)
                return;

            foreach (MotorVehicleSettings vehicleSettings in settings)
            {
                if (vehicleSettings == null || vehicleSettings.EngineSettings == null)
                    continue;

                string configKey = GetMotorTuningConfigKey(vehicleSettings);
                if (!_motorTuningDefaults.TryGetValue(configKey, out MotorTuningConfigEntry defaults) ||
                    defaults == null)
                {
                    continue;
                }

                vehicleSettings.EngineSettings._forceFactor = defaults.forceFactor;
                vehicleSettings.EngineSettings._maxForce = defaults.maxForce;
                vehicleSettings.EngineSettings._maxSpeed = defaults.maxSpeed;
            }

            _motorTuningEntries.Clear();
        }

        private static void ResetGraphicsTab()
        {
            GraphicsEnvironmentController.ResetToGameDefaults();
            Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
            if (lights != null)
            {
                CacheLightDefaults(lights);

                foreach (Light light in lights)
                {
                    if (light == null)
                        continue;

                    if (_cachedLightIntensityById.TryGetValue(light.GetInstanceID(), out float defaultIntensity))
                        light.intensity = defaultIntensity;
                }
            }
        }

        private static void ResetChallengeSettings(bool applySizeToActiveArea)
        {
            Config.ResetChallengeSettings();
            ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);

            if (applySizeToActiveArea || !MultiplayerChallengeManager.HasActiveChallenge)
            {
                ChallengeAreaManager.SetSize(new Vector3(
                    Config.challengeSettings.challengeSizeX,
                    Config.challengeSettings.challengeSizeY,
                    Config.challengeSettings.challengeSizeZ));
            }
        }

        //-------------------------------------------------------------------
        // TAB SWITCHING
        //-------------------------------------------------------------------

        private static void SetCurrentTab(Tab newTab)
        {
            if (currentTab != newTab)
            {
                if (currentTab == Tab.Grinds)
                {
                    GrindPoseEditor.OnGrindsTabExited();
                }

                if (currentTab == Tab.RiderTools)
                {
                    RiderStyleEditor.OnTabExited();
                }

                if (currentTab == Tab.Tricks)
                {
                    TrickMods.OnTricksTabExited();
                }

                if (currentTab == Tab.BikePoser)
                {
                    BikePoseEditor.OnTabExited();
                }

                if (newTab == Tab.Grinds)
                {
                    GrindPoseEditor.OnGrindsTabEntered();
                }

                if (newTab == Tab.RiderTools)
                {
                    RiderStyleEditor.OnTabEntered();
                }

                if (newTab == Tab.Tricks)
                {
                    TrickMods.OnTricksTabEntered();
                }

                if (newTab == Tab.BikePoser)
                {
                    BikePoseEditor.OnTabEntered();
                }

                scrollOffset = 0;
                scrollViewHeight = 10000f;
                currentTab = newTab;
                EnsureCurrentTabVisible();
            }
        }

        private sealed class MenuSkinScope : IDisposable
        {
            private readonly GUIStyle previousButton;
            private readonly GUIStyle previousBox;
            private readonly GUIStyle previousLabel;
            private readonly GUIStyle previousTextField;
            private readonly GUIStyle previousToggle;
            private readonly GUIStyle previousHorizontalSlider;
            private readonly GUIStyle previousHorizontalSliderThumb;

            public MenuSkinScope()
            {
                if (GUI.skin == null)
                    return;

                previousButton = GUI.skin.button;
                previousBox = GUI.skin.box;
                previousLabel = GUI.skin.label;
                previousTextField = GUI.skin.textField;
                previousToggle = GUI.skin.toggle;
                previousHorizontalSlider = GUI.skin.horizontalSlider;
                previousHorizontalSliderThumb = GUI.skin.horizontalSliderThumb;

                if (highQualityButtonStyle != null)
                    GUI.skin.button = highQualityButtonStyle;
                if (panelStyle != null)
                    GUI.skin.box = panelStyle;
                else if (sectionCardStyle != null)
                    GUI.skin.box = sectionCardStyle;
                if (labelStyle != null)
                    GUI.skin.label = labelStyle;
                if (textFieldStyle != null)
                    GUI.skin.textField = textFieldStyle;
                if (toggleStyle != null)
                    GUI.skin.toggle = toggleStyle;
                if (horizontalSliderStyle != null)
                    GUI.skin.horizontalSlider = horizontalSliderStyle;
                if (horizontalSliderThumbStyle != null)
                    GUI.skin.horizontalSliderThumb = horizontalSliderThumbStyle;
            }

            public void Dispose()
            {
                if (GUI.skin == null)
                    return;

                if (previousButton != null)
                    GUI.skin.button = previousButton;
                if (previousBox != null)
                    GUI.skin.box = previousBox;
                if (previousLabel != null)
                    GUI.skin.label = previousLabel;
                if (previousTextField != null)
                    GUI.skin.textField = previousTextField;
                if (previousToggle != null)
                    GUI.skin.toggle = previousToggle;
                if (previousHorizontalSlider != null)
                    GUI.skin.horizontalSlider = previousHorizontalSlider;
                if (previousHorizontalSliderThumb != null)
                    GUI.skin.horizontalSliderThumb = previousHorizontalSliderThumb;
            }
        }


        //-------------------------------------------------------------------
        // STYLES
        //-------------------------------------------------------------------

        private static Color GetAccessibleTextColor(Color background)
        {
            Color light = new Color(0.957f, 0.965f, 0.973f, 1f);
            Color dark = new Color(0.043f, 0.055f, 0.078f, 1f);
            float backgroundLuminance = GetRelativeLuminance(background);
            float lightContrast = (GetRelativeLuminance(light) + 0.05f) / (backgroundLuminance + 0.05f);
            float darkContrast = (backgroundLuminance + 0.05f) / (GetRelativeLuminance(dark) + 0.05f);
            return darkContrast >= lightContrast ? dark : light;
        }

        private static float GetRelativeLuminance(Color color)
        {
            return (0.2126f * LinearizeColorChannel(color.r)) +
                   (0.7152f * LinearizeColorChannel(color.g)) +
                   (0.0722f * LinearizeColorChannel(color.b));
        }

        private static float LinearizeColorChannel(float value)
        {
            value = Mathf.Clamp01(value);
            return value <= 0.04045f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        public static void InitializeStyles()
        {
            try
            {
                stylesInitialized = false;
                ReleaseGeneratedStyleTextures();

                if (_uiFont == null)
                {
                    try
                    {
                        _uiFont = Font.CreateDynamicFontFromOSFont("Segoe UI", Mathf.RoundToInt(14f * UiScale));
                    }
                    catch
                    {
                        _uiFont = null;
                    }
                }

                Color rawAccentColor = new Color(
                    Mathf.Clamp01(misc.menuAccentR),
                    Mathf.Clamp01(misc.menuAccentG),
                    Mathf.Clamp01(misc.menuAccentB),
                    1f);
                uiAccentColor = rawAccentColor;
                uiAccentHoverColor = Color.Lerp(uiAccentColor, Color.white, 0.12f);
                uiAccentSoftColor = Color.Lerp(
                    new Color(0.063f, 0.082f, 0.114f, 1f),
                    uiAccentColor,
                    0.18f);
                uiAccentTextColor = GetAccessibleTextColor(uiAccentColor);
                uiBackgroundColor = new Color(0.043f, 0.055f, 0.078f, 0.995f); // #0B0E14
                uiSidebarColor = new Color(0.051f, 0.067f, 0.094f, 0.995f);    // #0D1118
                uiPanelColor = new Color(0.078f, 0.102f, 0.137f, 0.995f);       // #141A23
                uiPanelHoverColor = new Color(0.102f, 0.141f, 0.188f, 1f);
                uiPanelAltColor = new Color(0.063f, 0.082f, 0.114f, 1f);        // #10151D
                uiBorderColor = new Color(0.165f, 0.204f, 0.259f, 0.96f);       // #2A3442
                uiTextPrimaryColor = new Color(0.957f, 0.965f, 0.973f, 1f);     // #F4F6F8
                uiTextMutedColor = new Color(0.667f, 0.710f, 0.765f, 1f);       // #AAB5C3
                uiDangerColor = new Color(0.345f, 0.102f, 0.137f, 0.98f);
                uiDangerHoverColor = new Color(0.435f, 0.133f, 0.176f, 1f);
                uiDangerActiveColor = new Color(0.275f, 0.075f, 0.106f, 1f);

                backgroundTexture = MakeStyleRoundedTex(64, 64, uiBackgroundColor, 14, 1, uiBorderColor);
                roundedButtonNormal = MakeStyleRoundedTex(48, 36, new Color(0.063f, 0.082f, 0.114f, 1f), 9, 1, uiBorderColor);
                roundedButtonHover = MakeStyleRoundedTex(48, 36, new Color(0.102f, 0.141f, 0.188f, 1f), 9, 1, new Color(0.27f, 0.34f, 0.43f, 1f));
                activeTabBackground = MakeStyleRoundedTex(48, 40,
                    Color.Lerp(uiPanelColor, uiAccentColor, 0.16f),
                    9,
                    1,
                    Color.Lerp(uiBorderColor, uiAccentColor, 0.48f));
                accentColorTexture = MakeStyleTex(2, 2, uiAccentColor);
                tabIndicatorTexture = MakeStyleTex(2, 2, uiAccentColor);
                tricksTabIndicatorTexture = MakeStyleTex(2, 2, new Color(0.24f, 0.82f, 0.42f, 1f));
                toggleCapsuleMaskTexture = MakeStyleCapsuleTex(96, 52, Color.white, 0, Color.clear);
                toggleKnobTexture = MakeStyleCircleTex(64, new Color(0.96f, 0.97f, 1f, 1f), 1, new Color(0f, 0f, 0f, 0.45f));
                _circleTex = toggleKnobTexture;

                windowStyle = new GUIStyle(GUI.skin.window);
                windowStyle.normal.background = backgroundTexture;
                windowStyle.onNormal.background = backgroundTexture;
                windowStyle.hover.background = backgroundTexture;
                windowStyle.active.background = backgroundTexture;
                windowStyle.normal.textColor = uiTextPrimaryColor;
                windowStyle.hover.textColor = uiTextPrimaryColor;
                windowStyle.active.textColor = uiTextPrimaryColor;
                windowStyle.font = _uiFont;
                windowStyle.fontSize = Mathf.RoundToInt(13f * UiScale);
                windowStyle.fontStyle = FontStyle.Bold;
                windowStyle.alignment = TextAnchor.UpperCenter;
                windowStyle.padding = new RectOffset((int)UiOuterPadding, (int)UiOuterPadding, (int)(UiTitleBarHeight + UiOuterPadding),
                    (int)UiOuterPadding);

                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.font = _uiFont;
                labelStyle.normal.textColor = uiTextPrimaryColor;
                labelStyle.hover.textColor = uiTextPrimaryColor;
                labelStyle.fontSize = Mathf.RoundToInt(14f * UiScale);
                labelStyle.fontStyle = FontStyle.Normal;
                labelStyle.richText = true;
                labelStyle.alignment = TextAnchor.MiddleLeft;

                subtleLabelStyle = new GUIStyle(labelStyle);
                subtleLabelStyle.normal.textColor = uiTextMutedColor;
                subtleLabelStyle.fontSize = Mathf.RoundToInt(12f * UiScale);

                sectionHeaderStyle = new GUIStyle(labelStyle);
                sectionHeaderStyle.fontStyle = FontStyle.Bold;
                sectionHeaderStyle.fontSize = Mathf.RoundToInt(16f * UiScale);

                pageTitleStyle = new GUIStyle(sectionHeaderStyle);
                pageTitleStyle.fontSize = Mathf.RoundToInt(28f * UiScale);
                pageTitleStyle.fontStyle = FontStyle.Bold;
                pageTitleStyle.alignment = TextAnchor.MiddleLeft;

                pageEyebrowStyle = new GUIStyle(labelStyle);
                pageEyebrowStyle.fontSize = Mathf.RoundToInt(11f * UiScale);
                pageEyebrowStyle.fontStyle = FontStyle.Bold;
                pageEyebrowStyle.normal.textColor = uiAccentColor;
                pageEyebrowStyle.alignment = TextAnchor.MiddleLeft;

                rowLabelStyle = new GUIStyle(labelStyle);
                rowLabelStyle.richText = true;
                rowLabelStyle.alignment = TextAnchor.MiddleLeft;
                rowLabelStyle.padding = new RectOffset(6, 6, 2, 2);

                rowMutedLabelStyle = new GUIStyle(rowLabelStyle);
                rowMutedLabelStyle.normal.textColor = uiTextMutedColor;
                rowMutedLabelStyle.fontSize = Mathf.RoundToInt(12f * UiScale);
                rowMutedLabelStyle.wordWrap = true;

                mutedWrappedLabelStyle = new GUIStyle(subtleLabelStyle);
                mutedWrappedLabelStyle.wordWrap = true;

                toggleStyle = new GUIStyle(GUI.skin.toggle);
                toggleStyle.font = _uiFont;
                toggleStyle.normal.textColor = uiTextPrimaryColor;
                toggleStyle.fontSize = Mathf.RoundToInt(14f * UiScale);

                horizontalSliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
                horizontalSliderStyle.normal.background = MakeStyleTex(2, 2, uiPanelAltColor);
                horizontalSliderStyle.fixedHeight = 7f;

                horizontalSliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
                horizontalSliderThumbStyle.normal.background = accentColorTexture;
                horizontalSliderThumbStyle.hover.background = MakeStyleTex(2, 2, uiAccentHoverColor);
                horizontalSliderThumbStyle.fixedWidth = 10f;
                horizontalSliderThumbStyle.fixedHeight = 14f;

                coloredBoxStyle = new GUIStyle(GUI.skin.box);
                coloredBoxStyle.normal.background = MakeStyleTex(2, 2, uiPanelAltColor);
                coloredBoxStyle.normal.textColor = uiTextPrimaryColor;
                coloredBoxStyle.font = _uiFont;
                coloredBoxStyle.fontSize = Mathf.RoundToInt(14f * UiScale);
                coloredBoxStyle.fixedHeight = UiFoldoutHeight;

                highQualityButtonStyle = new GUIStyle(GUI.skin.button);
                highQualityButtonStyle.normal.background = roundedButtonNormal;
                highQualityButtonStyle.hover.background = roundedButtonHover;
                highQualityButtonStyle.active.background = MakeStyleRoundedTex(48, 36, new Color(0.043f, 0.055f, 0.078f, 1f), 9, 1, uiBorderColor);
                highQualityButtonStyle.normal.textColor = uiTextPrimaryColor;
                highQualityButtonStyle.hover.textColor = uiTextPrimaryColor;
                highQualityButtonStyle.active.textColor = uiTextPrimaryColor;
                highQualityButtonStyle.font = _uiFont;
                highQualityButtonStyle.fontSize = Mathf.RoundToInt(13f * UiScale);
                highQualityButtonStyle.fontStyle = FontStyle.Normal;
                highQualityButtonStyle.alignment = TextAnchor.MiddleCenter;
                highQualityButtonStyle.border = new RectOffset(8, 8, 8, 8);
                highQualityButtonStyle.padding = new RectOffset(
                    Mathf.RoundToInt(14f * UiScale),
                    Mathf.RoundToInt(14f * UiScale),
                    Mathf.RoundToInt(7f * UiScale),
                    Mathf.RoundToInt(7f * UiScale));
                highQualityButtonStyle.richText = true;

                rowButtonStyle = new GUIStyle(highQualityButtonStyle);
                rowButtonStyle.alignment = TextAnchor.MiddleLeft;
                rowButtonStyle.padding = new RectOffset(
                    Mathf.RoundToInt(12f * UiScale),
                    Mathf.RoundToInt(12f * UiScale),
                    Mathf.RoundToInt(5f * UiScale),
                    Mathf.RoundToInt(5f * UiScale));
                rowButtonStyle.margin = new RectOffset(2, 2, 1, 1);

                rowButtonSelectedStyle = new GUIStyle(rowButtonStyle);
                rowButtonSelectedStyle.normal.background = activeTabBackground;
                rowButtonSelectedStyle.hover.background = activeTabBackground;
                rowButtonSelectedStyle.active.background = activeTabBackground;
                rowButtonSelectedStyle.normal.textColor = uiTextPrimaryColor;
                rowButtonSelectedStyle.hover.textColor = Color.white;
                rowButtonSelectedStyle.active.textColor = uiTextPrimaryColor;
                rowButtonSelectedStyle.fontStyle = FontStyle.Bold;

                pillButtonStyle = new GUIStyle(highQualityButtonStyle);
                pillButtonStyle.fontSize = Mathf.RoundToInt(13f * UiScale);
                pillButtonStyle.padding = new RectOffset(10, 10, 3, 3);
                pillButtonStyle.margin = new RectOffset(4, 0, 0, 0);
                pillButtonStyle.border = new RectOffset(7, 7, 7, 7);

                pillActiveButtonStyle = new GUIStyle(pillButtonStyle);
                pillActiveButtonStyle.normal.background = MakeStyleRoundedTex(48, 36, uiAccentColor, 9, 1,
                    Color.Lerp(uiAccentColor, Color.white, 0.20f));
                pillActiveButtonStyle.hover.background = MakeStyleRoundedTex(48, 36, uiAccentHoverColor, 9, 1,
                    Color.Lerp(uiAccentColor, Color.white, 0.40f));
                pillActiveButtonStyle.active.background = MakeStyleRoundedTex(48, 36,
                    Color.Lerp(uiAccentColor, Color.black, 0.14f), 9, 1, uiAccentColor);
                pillActiveButtonStyle.normal.textColor = uiAccentTextColor;
                pillActiveButtonStyle.hover.textColor = GetAccessibleTextColor(uiAccentHoverColor);
                pillActiveButtonStyle.active.textColor = uiAccentTextColor;
                pillActiveButtonStyle.fontStyle = FontStyle.Bold;

                miniButtonStyle = new GUIStyle(highQualityButtonStyle);
                miniButtonStyle.fontSize = Mathf.RoundToInt(12f * UiScale);
                miniButtonStyle.padding = new RectOffset(7, 7, 3, 3);
                miniButtonStyle.margin = new RectOffset(2, 2, 0, 0);

                tabButtonStyle = new GUIStyle(highQualityButtonStyle);
                Color sideTabBorder = Color.Lerp(uiBorderColor, uiSidebarColor, 0.42f);
                tabButtonStyle.normal.background = MakeStyleRoundedTex(48, 40,
                    Color.Lerp(uiSidebarColor, uiPanelAltColor, 0.68f), 9, 1, sideTabBorder);
                tabButtonStyle.hover.background = MakeStyleRoundedTex(48, 40,
                    uiPanelHoverColor, 9, 1, Color.Lerp(uiBorderColor, uiTextMutedColor, 0.18f));
                tabButtonStyle.active.background = MakeStyleRoundedTex(48, 40,
                    Color.Lerp(uiPanelHoverColor, Color.black, 0.12f), 9, 1, uiBorderColor);
                tabButtonStyle.normal.textColor = uiTextMutedColor;
                tabButtonStyle.hover.textColor = uiTextPrimaryColor;
                tabButtonStyle.active.textColor = uiTextPrimaryColor;
                tabButtonStyle.padding = new RectOffset(16, 10, 6, 6);
                tabButtonStyle.fontSize = Mathf.RoundToInt(14f * UiScale);
                tabButtonStyle.alignment = TextAnchor.MiddleLeft;

                activeTabButtonStyle = new GUIStyle(tabButtonStyle);
                activeTabButtonStyle.normal.background = activeTabBackground;
                activeTabButtonStyle.hover.background = activeTabBackground;
                activeTabButtonStyle.active.background = activeTabBackground;
                activeTabButtonStyle.normal.textColor = uiTextPrimaryColor;
                activeTabButtonStyle.hover.textColor = Color.white;
                activeTabButtonStyle.active.textColor = uiTextPrimaryColor;
                activeTabButtonStyle.fontStyle = FontStyle.Bold;
                tricksActiveTabButtonStyle = new GUIStyle(tabButtonStyle);
                tricksActiveTabButtonStyle.normal.background = activeTabBackground;
                tricksActiveTabButtonStyle.hover.background = activeTabBackground;
                tricksActiveTabButtonStyle.active.background = activeTabBackground;
                tricksActiveTabButtonStyle.normal.textColor = uiTextPrimaryColor;
                tricksActiveTabButtonStyle.hover.textColor = Color.white;
                tricksActiveTabButtonStyle.active.textColor = uiTextPrimaryColor;
                tricksActiveTabButtonStyle.fontStyle = FontStyle.Bold;

                subTabButtonStyle = new GUIStyle(tabButtonStyle);
                subTabButtonStyle.alignment = TextAnchor.MiddleCenter;
                subTabButtonStyle.padding = new RectOffset(12, 12, 8, 5);
                subTabButtonStyle.border = new RectOffset(9, 9, 9, 1);
                subTabButtonStyle.normal.background = MakeStyleTopRoundedTabTex(64, 42,
                    Color.Lerp(uiPanelAltColor, uiPanelColor, 0.18f), 10, 1, uiBorderColor);
                subTabButtonStyle.hover.background = MakeStyleTopRoundedTabTex(64, 42,
                    uiPanelHoverColor, 10, 1, Color.Lerp(uiBorderColor, uiTextMutedColor, 0.18f));
                subTabButtonStyle.active.background = MakeStyleTopRoundedTabTex(64, 42,
                    Color.Lerp(uiPanelHoverColor, Color.black, 0.10f), 10, 1, uiBorderColor);
                subTabButtonStyle.normal.textColor = uiTextMutedColor;
                subTabButtonStyle.hover.textColor = uiTextPrimaryColor;
                subTabButtonStyle.active.textColor = uiTextPrimaryColor;

                subTabActiveButtonStyle = new GUIStyle(subTabButtonStyle);
                Color activeSubTabColor = Color.Lerp(uiPanelColor, uiAccentColor, 0.13f);
                Color activeSubTabBorder = Color.Lerp(uiBorderColor, uiAccentColor, 0.52f);
                subTabActiveButtonStyle.normal.background = MakeStyleTopRoundedTabTex(64, 42,
                    activeSubTabColor, 10, 1, activeSubTabBorder);
                subTabActiveButtonStyle.hover.background = MakeStyleTopRoundedTabTex(64, 42,
                    Color.Lerp(activeSubTabColor, Color.white, 0.05f), 10, 1, uiAccentColor);
                subTabActiveButtonStyle.active.background = MakeStyleTopRoundedTabTex(64, 42,
                    Color.Lerp(activeSubTabColor, Color.black, 0.10f), 10, 1, activeSubTabBorder);
                subTabActiveButtonStyle.normal.textColor = uiTextPrimaryColor;
                subTabActiveButtonStyle.hover.textColor = Color.white;
                subTabActiveButtonStyle.active.textColor = uiTextPrimaryColor;
                subTabActiveButtonStyle.fontStyle = FontStyle.Bold;

                redButtonStyle = new GUIStyle(highQualityButtonStyle);
                Color dangerBorder = new Color(0.65f, 0.24f, 0.29f, 1f);
                redButtonStyle.normal.background = MakeStyleRoundedTex(48, 36, uiDangerColor, 9, 1, dangerBorder);
                redButtonStyle.hover.background = MakeStyleRoundedTex(48, 36, uiDangerHoverColor, 9, 1, Color.Lerp(dangerBorder, Color.white, 0.16f));
                redButtonStyle.active.background = MakeStyleRoundedTex(48, 36, uiDangerActiveColor, 9, 1, dangerBorder);
                redButtonStyle.normal.textColor = uiTextPrimaryColor;
                redButtonStyle.hover.textColor = uiTextPrimaryColor;
                redButtonStyle.active.textColor = uiTextPrimaryColor;

                sectionCardStyle = new GUIStyle(GUI.skin.box);
                sectionCardStyle.normal.background = MakeStyleRoundedTex(64, 64, uiPanelColor, 12, 1, uiBorderColor);
                sectionCardStyle.padding = new RectOffset((int)UiInnerPadding, (int)UiInnerPadding, (int)UiInnerPadding, (int)UiInnerPadding);
                sectionCardStyle.margin = new RectOffset(0, 0, 0, 0);
                sectionCardStyle.border = new RectOffset(12, 12, 12, 12);

                panelStyle = new GUIStyle(sectionCardStyle);
                panelStyle.padding = new RectOffset(
                    Mathf.RoundToInt(18f * UiScale),
                    Mathf.RoundToInt(18f * UiScale),
                    Mathf.RoundToInt(16f * UiScale),
                    Mathf.RoundToInt(18f * UiScale));
                panelStyle.margin = new RectOffset(0, 0, Mathf.RoundToInt(4f * UiScale), Mathf.RoundToInt(12f * UiScale));

                panelAltStyle = new GUIStyle(panelStyle);
                panelAltStyle.normal.background = MakeStyleRoundedTex(64, 64, uiPanelAltColor, 12, 1, uiBorderColor);

                tabBarStyle = new GUIStyle(GUI.skin.box);
                tabBarStyle.normal.background = MakeStyleRoundedTex(64, 36, uiPanelColor, 12, 1, uiBorderColor);
                tabBarStyle.border = new RectOffset(12, 12, 12, 12);
                tabBarStyle.padding = new RectOffset((int)UiInnerPadding, (int)UiInnerPadding, 4, 4);
                tabBarStyle.margin = new RectOffset(0, 0, 0, 0);

                toolbarStyle = new GUIStyle(tabBarStyle);
                toolbarStyle.padding = new RectOffset(
                    Mathf.RoundToInt(12f * UiScale),
                    Mathf.RoundToInt(12f * UiScale),
                    Mathf.RoundToInt(8f * UiScale),
                    Mathf.RoundToInt(8f * UiScale));
                toolbarStyle.margin = new RectOffset(0, 0, 0, Mathf.RoundToInt(8f * UiScale));

                badgeStyle = new GUIStyle(GUI.skin.box);
                badgeStyle.normal.background = MakeStyleRoundedTex(32, 22, uiPanelAltColor, 7, 1, uiBorderColor);
                badgeStyle.normal.textColor = uiTextMutedColor;
                badgeStyle.alignment = TextAnchor.MiddleCenter;
                badgeStyle.font = _uiFont;
                badgeStyle.fontSize = Mathf.RoundToInt(12f * UiScale);
                badgeStyle.fixedHeight = 26f * UiScale;
                badgeStyle.padding = new RectOffset(7, 7, 2, 2);
                badgeStyle.border = new RectOffset(7, 7, 7, 7);

                Texture2D textFieldBackground = MakeStyleRoundedTex(64, 24, new Color(0.04f, 0.047f, 0.062f, 1f), 7, 1, uiBorderColor);
                textFieldStyle = new GUIStyle(GUI.skin.textField);
                textFieldStyle.alignment = TextAnchor.MiddleCenter;
                textFieldStyle.fontSize = Mathf.RoundToInt(14f * UiScale);
                textFieldStyle.font = _uiFont ?? labelStyle.font;
                textFieldStyle.normal.textColor = uiTextPrimaryColor;
                textFieldStyle.focused.textColor = uiTextPrimaryColor;
                textFieldStyle.hover.textColor = uiTextPrimaryColor;
                textFieldStyle.active.textColor = uiTextPrimaryColor;
                textFieldStyle.normal.background = textFieldBackground;
                textFieldStyle.focused.background = textFieldBackground;
                textFieldStyle.hover.background = textFieldBackground;
                textFieldStyle.active.background = textFieldBackground;
                textFieldStyle.border = new RectOffset(4, 4, 4, 4);

                searchFieldStyle = new GUIStyle(textFieldStyle);
                searchFieldStyle.alignment = TextAnchor.MiddleLeft;
                searchFieldStyle.padding = new RectOffset(10, 10, 4, 4);
                searchFieldStyle.margin = new RectOffset(4, 4, 0, 0);

                InitializeControllerNavigationStyles();
                if (_appliedMenuScale <= 0f)
                    _appliedMenuScale = Mathf.Clamp(misc.menuScale > 0f ? misc.menuScale : 1f, 0.8f, 1.35f);
                _initializedUiScale = UiScale;
                _initializedScreenHeight = Screen.height;
                styleRevision++;
                stylesInitialized = true;
            }
            catch (Exception ex)
            {
                stylesInitialized = false;
                Log.Error($"Error in InitializeStyles: {ex.Message}");
            }
        }

        private static Texture2D TrackGeneratedStyleTexture(Texture2D texture)
        {
            if (texture != null)
                generatedStyleTextures.Add(texture);
            return texture;
        }

        private static Texture2D MakeStyleTex(int width, int height, Color color)
        {
            return TrackGeneratedStyleTexture(MakeTex(width, height, color));
        }

        private static Texture2D MakeStyleRoundedTex(int width, int height, Color fillColor, int radius,
            int borderThickness = 3, Color? borderColor = null)
        {
            return TrackGeneratedStyleTexture(
                MakeRoundedTex(width, height, fillColor, radius, borderThickness, borderColor));
        }

        private static Texture2D MakeStyleTopRoundedTabTex(int width, int height, Color fillColor, int radius,
            int borderThickness = 1, Color? borderColor = null)
        {
            return TrackGeneratedStyleTexture(
                MakeTopRoundedTabTex(width, height, fillColor, radius, borderThickness, borderColor));
        }

        private static Texture2D MakeStyleCapsuleTex(int width, int height, Color fillColor,
            int borderThickness = 2, Color? borderColor = null)
        {
            return TrackGeneratedStyleTexture(
                MakeCapsuleTex(width, height, fillColor, borderThickness, borderColor));
        }

        private static Texture2D MakeStyleCircleTex(int size, Color fillColor, int borderThickness = 1,
            Color? borderColor = null)
        {
            return TrackGeneratedStyleTexture(MakeCircleTex(size, fillColor, borderThickness, borderColor));
        }

        private static void ReleaseGeneratedStyleTextures()
        {
            for (int i = 0; i < generatedStyleTextures.Count; i++)
            {
                Texture2D texture = generatedStyleTextures[i];
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            generatedStyleTextures.Clear();
        }

        private static void ApplyConfiguredInterfaceScale()
        {
            _appliedMenuScale = Mathf.Clamp(misc.menuScale > 0f ? misc.menuScale : 1f, 0.8f, 1.35f);
            stylesInitialized = false;
        }

        public static Texture2D MakeTex(int width, int height, Color col)
        {
            try
            {
                Color[] pix = new Color[width * height];
                for (int i = 0; i < pix.Length; i++)
                    pix[i] = col;
                Texture2D result = new Texture2D(width, height);
                result.SetPixels(pix);
                result.Apply();
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in MakeTex: {ex.Message}");
                return null;
            }
        }

        public static Texture2D MakeRoundedTex(int width, int height, Color fillColor, int radius, int borderThickness = 3, Color? borderColor = null)
        {
            try
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color[] colors = new Color[width * height];

                Color finalBorderColor = borderColor ?? Color.white;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float distTL = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                        float distTR = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius));
                        float distBL = Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1));
                        float distBR = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1));

                        bool outsideCorner =
                            (x < radius && y < radius && distTL > radius) ||
                            (x > width - radius - 1 && y < radius && distTR > radius) ||
                            (x < radius && y > height - radius - 1 && distBL > radius) ||
                            (x > width - radius - 1 && y > height - radius - 1 && distBR > radius);

                        bool isBorder =
                            x < borderThickness || x >= width - borderThickness ||
                            y < borderThickness || y >= height - borderThickness;

                        colors[y * width + x] = outsideCorner
                            ? new Color(0, 0, 0, 0)
                            : isBorder
                                ? finalBorderColor
                                : fillColor;
                    }
                }

                tex.SetPixels(colors);
                tex.Apply();
                return tex;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in MakeRoundedTex: {ex.Message}");
                return null;
            }
        }

        // Create a capsule-shaped texture for toggle switches
        public static Texture2D MakeCapsuleTex(int width, int height, Color fillColor, int borderThickness = 2, Color? borderColor = null)
        {
            try
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color[] colors = new Color[width * height];

                Color finalBorderColor = borderColor ?? Color.black;
                // Use half the height as radius for capsule ends
                int radius = height / 2;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Check distance from left and right capsule ends
                        float distLeft = Vector2.Distance(new Vector2(x, y), new Vector2(radius, height / 2));
                        float distRight = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height / 2));

                        bool outsideCapsule =
                            (x < radius && distLeft > radius) ||
                            (x >= width - radius && distRight > radius);

                        bool isBorder =
                            x < borderThickness || x >= width - borderThickness ||
                            y < borderThickness || y >= height - borderThickness ||
                            (x < radius && distLeft > radius - borderThickness && distLeft <= radius) ||
                            (x >= width - radius && distRight > radius - borderThickness && distRight <= radius);

                        colors[y * width + x] = outsideCapsule
                            ? new Color(0, 0, 0, 0)
                            : isBorder
                                ? finalBorderColor
                                : fillColor;
                    }
                }

                tex.SetPixels(colors);
                tex.Apply();
                return tex;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in MakeCapsuleTex: {ex.Message}");
                return null;
            }
        }

        // Create a circular texture for toggle knobs
        public static Texture2D MakeCircleTex(int size, Color fillColor, int borderThickness = 1, Color? borderColor = null)
        {
            try
            {
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] colors = new Color[size * size];

                Color finalBorderColor = borderColor ?? Color.black;
                int radius = size / 2;
                Vector2 center = new Vector2(radius, radius);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        bool outsideCircle = distance > radius;
                        bool isBorder = !outsideCircle && distance > radius - borderThickness;

                        colors[y * size + x] = outsideCircle
                            ? new Color(0, 0, 0, 0)
                            : isBorder
                                ? finalBorderColor
                                : fillColor;
                    }
                }

                tex.SetPixels(colors);
                tex.Apply();
                return tex;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in MakeCircleTex: {ex.Message}");
                return null;
            }
        }
        
        //-------------------------------------------------------------------
        // GRAPHICS
        //-------------------------------------------------------------------

        private static void RefreshMotorTuningData()
        {
            _motorTuningEntries.Clear();
            _motorTuningNeedsRefresh = false;

            MotorVehicleSettings[] motorVehicleSettings = Resources.FindObjectsOfTypeAll<MotorVehicleSettings>();
            if (motorVehicleSettings != null)
            {
                vehicleSettingsInstances = motorVehicleSettings;
            }

            if (motorVehicleSettings == null || motorVehicleSettings.Length == 0)
            {
                _motorTuningStatusText = "No MotorVehicleSettings found.";
                return;
            }

            foreach (MotorVehicleSettings vehicleSettings in motorVehicleSettings)
            {
                if (vehicleSettings == null)
                    continue;

                try
                {
                    var engineSettings = vehicleSettings.EngineSettings;
                    if (engineSettings == null)
                        continue;

                    string configKey = GetMotorTuningConfigKey(vehicleSettings);
                    if (!_motorTuningDefaults.TryGetValue(configKey, out MotorTuningConfigEntry defaultEntry) || defaultEntry == null)
                    {
                        defaultEntry = new MotorTuningConfigEntry
                        {
                            enabled = false,
                            enabledMigrated = true,
                            forceFactor = engineSettings._forceFactor,
                            maxForce = engineSettings._maxForce,
                            maxSpeed = engineSettings._maxSpeed
                        };
                        _motorTuningDefaults[configKey] = defaultEntry;
                    }

                    if (!motorTuning.TryGetValue(configKey, out MotorTuningConfigEntry savedEntry) || savedEntry == null)
                    {
                        savedEntry = new MotorTuningConfigEntry
                        {
                            enabled = false,
                            enabledMigrated = true,
                            forceFactor = defaultEntry.forceFactor,
                            maxForce = defaultEntry.maxForce,
                            maxSpeed = defaultEntry.maxSpeed
                        };
                        motorTuning[configKey] = savedEntry;
                    }

                    MigrateMotorTuningEnabledFlag(savedEntry, defaultEntry);

                    if (savedEntry.enabled)
                    {
                        engineSettings._forceFactor = savedEntry.forceFactor;
                        engineSettings._maxForce = savedEntry.maxForce;
                        engineSettings._maxSpeed = savedEntry.maxSpeed;
                    }
                    else
                    {
                        engineSettings._forceFactor = physics.bmxForceFactor;
                        engineSettings._maxForce = defaultEntry.maxForce;
                        engineSettings._maxSpeed = physics.bmxMaxSpeed;
                    }

                    _motorTuningEntries.Add(new MotorTuningEntry
                    {
                        VehicleName = string.IsNullOrEmpty(vehicleSettings.name) ? $"MotorVehicleSettings {vehicleSettings.GetInstanceID()}" : vehicleSettings.name,
                        ConfigKey = configKey,
                        VehicleSettings = vehicleSettings,
                        DefaultForceFactor = defaultEntry.forceFactor,
                        DefaultMaxForce = defaultEntry.maxForce,
                        DefaultMaxSpeed = defaultEntry.maxSpeed
                    });
                }
                catch (Exception ex)
                {
                    Log.Error($"[MotorTuning] Failed to read EngineSettings on {vehicleSettings.name}: {ex.Message}");
                }
            }

            _motorTuningStatusText = _motorTuningEntries.Count > 0
                ? $"Loaded {_motorTuningEntries.Count} engine settings."
                : "No EngineSettings found on MotorVehicleSettings.";
        }

        private static void DrawMotorTuningData()
        {
            GUILayout.Label(_motorTuningStatusText, subtleLabelStyle);

            foreach (MotorTuningEntry entry in _motorTuningEntries)
            {
                if (entry == null || entry.VehicleSettings == null)
                    continue;

                var engineSettings = entry.VehicleSettings.EngineSettings;
                if (engineSettings == null)
                    continue;

                if (!motorTuning.TryGetValue(entry.ConfigKey, out MotorTuningConfigEntry tune) || tune == null)
                {
                    tune = new MotorTuningConfigEntry
                    {
                        enabled = false,
                        enabledMigrated = true,
                        forceFactor = entry.DefaultForceFactor,
                        maxForce = entry.DefaultMaxForce,
                        maxSpeed = entry.DefaultMaxSpeed
                    };
                    motorTuning[entry.ConfigKey] = tune;
                }

                GUILayout.Space(2f);
                GUILayout.Label(entry.VehicleName, labelStyle);

                bool enabled = tune.enabled;
                bool nextEnabled = ModernToggle("Use advanced motor tuning", ref enabled, $"motor_tuning_{entry.ConfigKey}_enabled");
                if (nextEnabled != tune.enabled)
                {
                    tune.enabled = nextEnabled;
                    if (tune.enabled)
                    {
                        engineSettings._forceFactor = tune.forceFactor;
                        engineSettings._maxForce = tune.maxForce;
                        engineSettings._maxSpeed = tune.maxSpeed;
                    }
                    else
                    {
                        engineSettings._forceFactor = physics.bmxForceFactor;
                        engineSettings._maxForce = entry.DefaultMaxForce;
                        engineSettings._maxSpeed = physics.bmxMaxSpeed;
                    }
                }

                if (!tune.enabled)
                {
                    GUILayout.Label(
                        $"Using global speed: push={physics.bmxForceFactor:0.##}, max speed={physics.bmxMaxSpeed:0.##}. Max force remains default ({entry.DefaultMaxForce:0.##}).",
                        UiMutedWrappedStyle);
                    continue;
                }

                float forceFactor = tune.forceFactor;
                float maxForce = tune.maxForce;
                float maxSpeed = tune.maxSpeed;
                float forceFactorSliderMax = Mathf.Max(5f, Mathf.Ceil((Mathf.Max(forceFactor, entry.DefaultForceFactor) + 0.5f) * 10f) / 10f);
                float maxForceSliderMax = Mathf.Max(500f, Mathf.Ceil((Mathf.Max(maxForce, entry.DefaultMaxForce) + 10f) / 10f) * 10f);
                float maxSpeedSliderMax = Mathf.Max(50f, Mathf.Ceil((Mathf.Max(maxSpeed, entry.DefaultMaxSpeed) + 1f) * 10f) / 10f);

                DrawSliderInternal("_forceFactor", ref forceFactor, 0f, forceFactorSliderMax, $"motor_tuning_{entry.ConfigKey}_forceFactor", true, entry.DefaultForceFactor);
                DrawSliderInternal("_maxForce", ref maxForce, 0f, maxForceSliderMax, $"motor_tuning_{entry.ConfigKey}_maxForce", true, entry.DefaultMaxForce);
                DrawSliderInternal("_maxSpeed", ref maxSpeed, 0f, maxSpeedSliderMax, $"motor_tuning_{entry.ConfigKey}_maxSpeed", true, entry.DefaultMaxSpeed);

                bool changed = !Mathf.Approximately(forceFactor, tune.forceFactor) ||
                               !Mathf.Approximately(maxForce, tune.maxForce) ||
                               !Mathf.Approximately(maxSpeed, tune.maxSpeed);
                if (!changed)
                    continue;

                tune.forceFactor = forceFactor;
                tune.maxForce = maxForce;
                tune.maxSpeed = maxSpeed;

                engineSettings._forceFactor = forceFactor;
                engineSettings._maxForce = maxForce;
                engineSettings._maxSpeed = maxSpeed;
            }
        }

        private static void MigrateMotorTuningEnabledFlag(MotorTuningConfigEntry savedEntry, MotorTuningConfigEntry defaultEntry)
        {
            if (savedEntry == null || defaultEntry == null || savedEntry.enabled)
                return;

            if (savedEntry.enabledMigrated)
                return;

            bool differsFromDefaults =
                !Mathf.Approximately(savedEntry.forceFactor, defaultEntry.forceFactor) ||
                !Mathf.Approximately(savedEntry.maxForce, defaultEntry.maxForce) ||
                !Mathf.Approximately(savedEntry.maxSpeed, defaultEntry.maxSpeed);

            if (differsFromDefaults)
                savedEntry.enabled = true;

            savedEntry.enabledMigrated = true;
        }

        private static string GetMotorTuningConfigKey(MotorVehicleSettings vehicleSettings)
        {
            if (vehicleSettings == null || string.IsNullOrEmpty(vehicleSettings.name))
                return "MotorVehicleSettings";

            return vehicleSettings.name;
        }

        public static void DrawCameraSettings()
        {
            BeginPane("Camera Controls", "Camera shortcuts that run during gameplay while the RoweMod menu is closed.");
            bool leftStickOffsetSwitch = Config.cameraSettings.leftStickOffsetSwitch;
            ModernToggle("Left Stick Tap Flips Camera Offset", ref leftStickOffsetSwitch, "camera_left_stick_offset_switch");
            if (leftStickOffsetSwitch != Config.cameraSettings.leftStickOffsetSwitch)
                Config.cameraSettings.leftStickOffsetSwitch = leftStickOffsetSwitch;
            GUILayout.Space(6f);
            GUILayout.Label(
                "Release LS before 0.5 seconds to flip the camera. Holding LS for 0.5 seconds is reserved for Bike-Only Stance and will not flip the camera.",
                UiMutedWrappedStyle);
            EndPane();
        }

        public static void DrawReplaySettings()
        {
            BeginPane(
                "Replay Camera",
                "Camera Lab binds only while Replay is open. Native tracks stay authoritative and missing tracks use session-only RoweMod keys.");
            ReplayCameraLight.DrawCameraControls("replay_tab_camera_");
            EndPane();

            BeginPane("Lens", "Long-lens zoom, tilt, fisheye, vignette, and shake controls.");
            ReplayCameraLight.DrawLensControls("replay_tab_lens_");
            EndPane();

            BeginPane("Depth of Field", "Keyframe the complete native near/far focus model.");
            ReplayCameraLight.DrawDofControls("replay_tab_dof_");
            EndPane();

            BeginPane("Framing", "Capture-safe aspect mattes that never change the game's output resolution.");
            ReplayCameraLight.DrawFramingControls("replay_tab_frame_");
            EndPane();

            BeginPane("Camera Light", "A high-quality replay-only local light attached to the active replay camera.");
            GUILayout.Space(8f);
            ReplayCameraLight.DrawLightControls("replay_tab_light_");
            EndPane();

            BeginPane("Keyframes", "The native Add/Delete commands also update RoweMod-owned camera, framing, and light tracks.");
            ReplayCameraLight.DrawKeyframeControls();
            EndPane();

            BeginPane("Replay Camera Presets", "Save lens, DoF, shake, framing, and the Rowe camera light. Collision remains a live safety setting.");
            ReplayCameraLight.DrawPresetControls();
            EndPane();
        }

        public static void DrawGraphicsSettings()
        {
            try
            {
                GraphicsEnvironmentController.DrawControls();
                DrawLightSettings();
                
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawGraphicsSettings: {ex.Message}");
            }
        }
        
        

        private static void DrawLightSettings()
        {
            Light[] lights = _sceneLightCache;
            int liveLightCount = 0;
            if (lights != null)
            {
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null)
                        liveLightCount++;
                }
            }
            if (liveLightCount == 0)
            {
                BeginPane("Light Settings", "Scene lights will appear here when the current scene exposes them.");
                DrawEmptyState("No editable lights found.", "Load a map or use Refresh Lights to scan the current scene.");
                EndPane();
                return;
            }

            BeginPane("Light Settings", "Adjust intensity for lights exposed by the current scene.");
            foreach (var light in lights)
            {
                if (light == null)
                    continue;

                int lightId = light.GetInstanceID();
                float defaultIntensity = _cachedLightIntensityById.TryGetValue(lightId, out float cachedDefault)
                    ? cachedDefault
                    : Mathf.Max(0f, light.intensity);
                float maxIntensity = Mathf.Max(0.01f, defaultIntensity * 2f);
                string lightName = string.IsNullOrEmpty(light.name) ? $"Light {light.GetInstanceID()}" : light.name;
                DrawSectionTitle(lightName, $"Default: {defaultIntensity:0.###}  Max: {maxIntensity:0.###}");
                float intensity = light.intensity;
                ModernSlider(
                    "Intensity",
                    ref intensity,
                    0f,
                    maxIntensity,
                    $"Light.{light.GetInstanceID()}.intensity");

                if (!Mathf.Approximately(intensity, light.intensity))
                {
                    light.intensity = intensity;
                }
            }
            EndPane();
        }

        private static void CacheLightDefaults(IEnumerable<Light> lights)
        {
            HashSet<int> currentLightIds = new HashSet<int>();
            foreach (var light in lights)
            {
                if (light == null)
                    continue;

                int lightId = light.GetInstanceID();
                currentLightIds.Add(lightId);
                if (!_cachedLightIntensityById.ContainsKey(lightId))
                    _cachedLightIntensityById[lightId] = Mathf.Max(0f, light.intensity);
            }

            if (_cachedLightIntensityById.Count == 0)
                return;

            var removedIds = _cachedLightIntensityById.Keys.Where(id => !currentLightIds.Contains(id)).ToArray();
            foreach (var removedId in removedIds)
                _cachedLightIntensityById.Remove(removedId);
        }

        


       

        

       

        //-------------------------------------------------------------------
        // SLIDER & GUI METHODS
        //-------------------------------------------------------------------

        public static bool BeginSectionCard(string label, ref bool expanded)
        {
            GUILayout.Space(UiSectionSpacing);
            GUILayout.BeginVertical(sectionCardStyle);
            expanded = ModernFoldout(label, expanded);
            if (expanded)
            {
                GUILayout.Space(UiControlSpacing);
            }
            return expanded;
        }

        public static void EndSectionCard()
        {
            GUILayout.EndVertical();
        }

        public static void BeginPanel(params GUILayoutOption[] options)
        {
            GUILayout.BeginVertical(UiPanelStyle, options);
        }

        public static void BeginAltPanel(params GUILayoutOption[] options)
        {
            GUILayout.BeginVertical(UiPanelAltStyle, options);
        }

        public static void EndPanel()
        {
            GUILayout.EndVertical();
        }

        public static void BeginToolbar(params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(toolbarStyle ?? UiPanelStyle, options);
        }

        public static void EndToolbar()
        {
            GUILayout.EndHorizontal();
        }

        public static void DrawSectionTitle(string title, string detail = null)
        {
            GUILayout.Label(title, UiHeaderStyle);
            if (!string.IsNullOrWhiteSpace(detail))
                GUILayout.Label(detail, UiMutedWrappedStyle);
        }

        public static bool MiniButton(string label, params GUILayoutOption[] options)
        {
            return ControllerButton(label, UiMiniButtonStyle, options);
        }

        private static Texture2D MakeTopRoundedTabTex(int width, int height, Color fillColor, int radius,
            int borderThickness = 1, Color? borderColor = null)
        {
            try
            {
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color[] colors = new Color[width * height];
                Color finalBorderColor = borderColor ?? Color.white;
                Vector2 topLeftCenter = new Vector2(radius, radius);
                Vector2 topRightCenter = new Vector2(width - radius - 1, radius);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float topLeftDistance = Vector2.Distance(new Vector2(x, y), topLeftCenter);
                        float topRightDistance = Vector2.Distance(new Vector2(x, y), topRightCenter);
                        bool outsideTopCorner =
                            (x < radius && y < radius && topLeftDistance > radius) ||
                            (x > width - radius - 1 && y < radius && topRightDistance > radius);
                        bool roundedBorder =
                            (x < radius && y < radius &&
                             topLeftDistance > radius - borderThickness && topLeftDistance <= radius) ||
                            (x > width - radius - 1 && y < radius &&
                             topRightDistance > radius - borderThickness && topRightDistance <= radius);
                        bool straightBorder = y < borderThickness || x < borderThickness ||
                                              x >= width - borderThickness;

                        colors[y * width + x] = outsideTopCorner
                            ? Color.clear
                            : roundedBorder || straightBorder
                                ? finalBorderColor
                                : fillColor;
                    }
                }

                texture.SetPixels(colors);
                texture.Apply();
                return texture;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in MakeTopRoundedTabTex: {ex.Message}");
                return null;
            }
        }

        public static bool PillButton(string label, bool active, params GUILayoutOption[] options)
        {
            return ControllerButton(label, active ? UiPillActiveStyle : UiPillStyle, options);
        }

        public static void DrawStatusBadge(string text, params GUILayoutOption[] options)
        {
            GUILayout.Label(text, UiBadgeStyle, options);
        }

        public static float GetContentPaneHeight(float reservedHeight = 0f)
        {
            return Mathf.Max(280f, viewHeight - reservedHeight);
        }

        public static void BeginPane(string title, string detail = null, params GUILayoutOption[] options)
        {
            BeginPanel(options);
            if (!string.IsNullOrWhiteSpace(title))
            {
                DrawSectionTitle(title, detail);
                GUILayout.Space(6f);
            }
        }

        public static void BeginAltPane(string title, string detail = null, params GUILayoutOption[] options)
        {
            BeginAltPanel(options);
            if (!string.IsNullOrWhiteSpace(title))
            {
                DrawSectionTitle(title, detail);
                GUILayout.Space(6f);
            }
        }

        public static void EndPane()
        {
            EndPanel();
        }

        public static void BeginTwoPane(float height)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(height));
        }

        public static void EndTwoPane()
        {
            GUILayout.EndHorizontal();
        }

        public static bool SearchRow(ref string value, float width = 240f, string label = "Search")
        {
            GUILayout.Label(label, UiMutedStyle, GUILayout.Width(54f));
            GUI.SetNextControlName($"{label}_SearchField");
            string nextValue = GUILayout.TextField(value ?? string.Empty, UiSearchFieldStyle, GUILayout.Width(width), GUILayout.Height(24f));
            bool changed = !string.Equals(nextValue, value ?? string.Empty, StringComparison.Ordinal);
            value = nextValue;

            if (!string.IsNullOrEmpty(value) && MiniButton("x", GUILayout.Width(24f), GUILayout.Height(24f)))
            {
                value = string.Empty;
                GUI.FocusControl(null);
                changed = true;
            }

            return changed;
        }

        public static bool PrimaryButton(string label, params GUILayoutOption[] options)
        {
            return ControllerButton(label, UiPillActiveStyle, options);
        }

        public static bool SecondaryButton(string label, params GUILayoutOption[] options)
        {
            return ControllerButton(label, UiButtonStyle, options);
        }

        public static bool DangerButton(string label, params GUILayoutOption[] options)
        {
            return ControllerButton(label, UiDangerButtonStyle, options);
        }

        public static void DrawEmptyState(string title, string detail = null)
        {
            GUILayout.FlexibleSpace();
            GUIStyle titleStyle = UiHeaderStyle;
            GUILayout.Label(title, titleStyle);
            if (!string.IsNullOrWhiteSpace(detail))
                GUILayout.Label(detail, UiMutedWrappedStyle);
            GUILayout.FlexibleSpace();
        }

        public static bool ModernFoldout(string label, bool expanded)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, UiFoldoutHeight, GUILayout.ExpandWidth(true), GUILayout.Height(UiFoldoutHeight));
            Event currentEvent = Event.current;
            bool isHovering = rect.Contains(currentEvent.mousePosition);
            string controllerId = RegisterControllerFoldout($"foldout_{label}", rect);

            Color baseColor = expanded ? uiPanelAltColor : uiPanelColor;
            Color backgroundColor = isHovering ? Color.Lerp(baseColor, uiPanelHoverColor, 0.6f) : baseColor;
            DrawSolidColorRect(rect, backgroundColor);

            if (expanded)
            {
                Rect accentRect = new Rect(rect.x + 1f, rect.y + (6f * UiScale),
                    4f * UiScale, rect.height - (12f * UiScale));
                DrawSolidColorRect(accentRect, uiAccentColor);
            }

            Rect dividerRect = new Rect(rect.x + (10f * UiScale), rect.yMax - 1f,
                rect.width - (20f * UiScale), 1f);
            DrawSolidColorRect(dividerRect, new Color(1f, 1f, 1f, 0.07f));

            Rect arrowRect = new Rect(rect.x + (12f * UiScale), rect.y, 18f * UiScale, rect.height);
            Rect labelRect = new Rect(rect.x + (34f * UiScale), rect.y,
                rect.width - (40f * UiScale), rect.height);
            GUI.Label(arrowRect, expanded ? "▾" : "▸", sectionHeaderStyle);
            GUI.Label(labelRect, label, sectionHeaderStyle);

            if ((currentEvent.type == EventType.MouseDown && rect.Contains(currentEvent.mousePosition)) ||
                ConsumeControllerActivation(controllerId))
            {
                expanded = !expanded;
                if (currentEvent.type == EventType.MouseDown)
                    currentEvent.Use();
            }

            DrawControllerFocusRing(controllerId, rect);

            return expanded;
        }

        public static void Slider(string label, ref float target, float defaultVal, float min, float max)
        {
            DrawSliderInternal(label, ref target, min, max, label, true, defaultVal);
        }

        private static void DrawSliderInternal(string label, ref float target, float min, float max, string sliderKey, bool showResetButton, float defaultValue)
        {
            float rowHeight = UiRowHeight;
            float valueBoxWidth = 68f * UiScale;
            float resetButtonWidth = showResetButton ? 72f * UiScale : 0f;
            float spacing = 8f * UiScale;
            float valueBorderSize = Mathf.Max(1f, 1.5f * UiScale);

            Rect fullRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));
            string controllerId = RegisterControllerSlider($"slider_{sliderKey}", fullRect);
            int controllerDirection = ConsumeControllerHorizontal(controllerId);
            if (controllerDirection != 0)
            {
                float step = GetControllerSliderStep(min, max);
                target = Mathf.Clamp(target + (controllerDirection * step), min, max);
                target = Mathf.Round(target * 1000f) / 1000f;
                _sliderTextInputs[sliderKey] = target.ToString("0.00");
            }

            float minSliderWidth = 90f * UiScale;
            float minLabelWidth = 110f * UiScale;
            float maxLabelWidth = 220f * UiScale;
            float availableForLabel = fullRect.width - minSliderWidth - valueBoxWidth - (spacing * 3f) - resetButtonWidth;
            float labelWidth = Mathf.Clamp(availableForLabel * 0.6f, minLabelWidth, maxLabelWidth);
            labelWidth = Mathf.Min(labelWidth, Mathf.Max(minLabelWidth, availableForLabel));

            float valueX = fullRect.xMax - valueBoxWidth - valueBorderSize - (showResetButton ? (resetButtonWidth + spacing) : 0f);
            float sliderX = fullRect.x + labelWidth + spacing;
            float sliderRight = valueX - spacing;
            float sliderWidth = Mathf.Max(minSliderWidth, sliderRight - sliderX);

            float trackHeight = 6f * UiScale;
            float trackY = fullRect.y + ((rowHeight - trackHeight) * 0.5f);
            Rect labelRect = new Rect(fullRect.x, fullRect.y, labelWidth, rowHeight);
            Rect sliderRect = new Rect(sliderX, trackY, sliderWidth, trackHeight);
            Rect valueRect = new Rect(valueX, fullRect.y, valueBoxWidth, rowHeight);
            Rect resetRect = new Rect(valueRect.xMax + spacing, fullRect.y, resetButtonWidth, rowHeight);

            GUI.Label(labelRect, label, labelStyle);
            DrawSolidColorRect(sliderRect, uiPanelAltColor);

            float percent = Mathf.InverseLerp(min, max, target);
            float fillWidth = Mathf.Clamp01(percent) * sliderRect.width;
            Rect fillRect = new Rect(sliderRect.x, sliderRect.y, fillWidth, sliderRect.height);
            DrawSolidColorRect(fillRect, uiAccentColor);

            float knobSize = 18f * UiScale;
            float knobX = sliderRect.x + fillWidth - (knobSize * 0.5f);
            Rect knobRect = new Rect(knobX, fullRect.y + ((rowHeight - knobSize) * 0.5f), knobSize, knobSize);
            if (_circleTex != null)
                GUI.DrawTexture(knobRect, _circleTex, ScaleMode.StretchToFill, true);
            else
                DrawSolidColorRect(knobRect, Color.white);

            int id = GUIUtility.GetControlID(sliderKey.GetHashCode(), FocusType.Passive, sliderRect);
            Event currentEvent = Event.current;

            switch (currentEvent.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                {
                    if (sliderRect.Contains(currentEvent.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        activeSliderControlId = id;
                        GUIUtility.keyboardControl = 0;
                        SetSliderValueFromMouse(ref target, min, max, sliderRect, currentEvent.mousePosition.x);
                        _sliderTextInputs[sliderKey] = target.ToString("0.00");
                        currentEvent.Use();
                    }
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl == id)
                    {
                        SetSliderValueFromMouse(ref target, min, max, sliderRect, currentEvent.mousePosition.x);
                        _sliderTextInputs[sliderKey] = target.ToString("0.00");
                        currentEvent.Use();
                    }
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        activeSliderControlId = 0;
                        currentEvent.Use();
                    }
                    break;
                }
            }

            Rect borderRect = new Rect(valueRect.x - valueBorderSize, valueRect.y - valueBorderSize,
                valueRect.width + valueBorderSize * 2f, valueRect.height + valueBorderSize * 2f);
            DrawSolidColorRect(borderRect, new Color(uiAccentColor.r, uiAccentColor.g, uiAccentColor.b, 0.5f));
            DrawSolidColorRect(valueRect, new Color(0.08f, 0.09f, 0.11f, 1f));

            if (!_sliderTextInputs.ContainsKey(sliderKey))
                _sliderTextInputs[sliderKey] = target.ToString("0.00");

            string controlName = $"SliderTextField_{sliderKey}";
            GUI.SetNextControlName(controlName);
            string newText = GUI.TextField(valueRect, _sliderTextInputs[sliderKey], textFieldStyle);
            _sliderTextInputs[sliderKey] = newText;

            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
            bool pressedEnter = Keyboard.current?.enterKey.wasPressedThisFrame == true ||
                               Keyboard.current?.numpadEnterKey.wasPressedThisFrame == true;

            if (isFocused && pressedEnter)
            {
                if (float.TryParse(newText, out float parsedValue))
                {
                    target = Mathf.Clamp(parsedValue, min, max);
                    _sliderTextInputs[sliderKey] = target.ToString("0.00");
                }
                else
                {
                    _sliderTextInputs[sliderKey] = target.ToString("0.00");
                }

                GUI.FocusControl(null);
            }
            else if (!isFocused && currentEvent.type == EventType.MouseDown && !valueRect.Contains(currentEvent.mousePosition))
            {
                if (float.TryParse(newText, out float parsedValue))
                {
                    target = Mathf.Clamp(parsedValue, min, max);
                    _sliderTextInputs[sliderKey] = target.ToString("0.00");
                }
                else
                {
                    _sliderTextInputs[sliderKey] = target.ToString("0.00");
                }
            }

            bool differsFromDefault = showResetButton && !Mathf.Approximately(target, defaultValue);
            if (differsFromDefault && ControllerButton(resetRect, $"slider_reset_{sliderKey}", "Reset", highQualityButtonStyle))
            {
                target = defaultValue;
                _sliderTextInputs[sliderKey] = target.ToString("0.00");
            }

            DrawControllerFocusRing(controllerId, fullRect);

            GUILayout.Space(UiControlSpacing);
        }

        /// <summary>
        /// Releases only IMGUI controls owned by the RoweMod menu. This prevents a menu that is
        /// closed mid-drag from retaining Unity's global hot control and blocking other IMGUI tools.
        /// </summary>
        public static void ReleaseInputCapture()
        {
            int hotControl = GUIUtility.hotControl;
            if (hotControl != 0 &&
                (hotControl == resizeControlId || hotControl == dragControlId || hotControl == activeSliderControlId))
            {
                GUIUtility.hotControl = 0;
            }

            isResizing = false;
            resizeControlId = 0;
            isDraggingWindow = false;
            dragControlId = 0;
            activeSliderControlId = 0;
            ResetControllerNavigation(false, true);
        }

        public static bool Toggle(string text, ref bool value)
        {
            try
            {
                GUILayout.BeginHorizontal();
                if (ControllerButton($"legacy_toggle_{text}", value ? "✔" : " ", highQualityButtonStyle,
                        GUILayout.Width(32f * UiScale), GUILayout.Height(32f * UiScale)))
                {
                    value = !value;
                }
                GUILayout.Label(text);
                GUILayout.EndHorizontal();
                return value;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in Toggle: {ex.Message}");
                return value;
            }
        }

        private static Dictionary<string, float> toggleAnimationState = new Dictionary<string, float>();

        public static bool ModernToggle(string label, ref bool value, string controlId = null)
        {
            string toggleKey = string.IsNullOrEmpty(controlId) ? label : controlId;

            float width = 52f * UiScale;
            float height = 28f * UiScale;
            float knobSize = 20f * UiScale;
            float padding = 4f * UiScale;
            Rect fullRect = GUILayoutUtility.GetRect(0f, UiRowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(UiRowHeight));
            string controllerTargetId = RegisterControllerToggle($"toggle_{toggleKey}", fullRect);
            Rect labelRect = new Rect(fullRect.x, fullRect.y, fullRect.width - width - (12f * UiScale), fullRect.height);
            Rect toggleRect = new Rect(fullRect.x + fullRect.width - width,
                fullRect.y + ((fullRect.height - height) * 0.5f), width, height);

            if (!toggleAnimationState.ContainsKey(toggleKey))
                toggleAnimationState[toggleKey] = value ? 1f : 0f;

            bool isHovering = fullRect.Contains(Event.current.mousePosition);
            bool clicked = Event.current.type == EventType.MouseDown &&
                           (toggleRect.Contains(Event.current.mousePosition) || labelRect.Contains(Event.current.mousePosition));
            bool controllerActivated = ConsumeControllerActivation(controllerTargetId);
            int controllerDirection = ConsumeControllerHorizontal(controllerTargetId);
            if (clicked || controllerActivated)
            {
                value = !value;
                if (clicked)
                    Event.current.Use();
            }
            else if (controllerDirection < 0)
                value = false;
            else if (controllerDirection > 0)
                value = true;

            float target = value ? 1f : 0f;
            toggleAnimationState[toggleKey] = Mathf.Lerp(toggleAnimationState[toggleKey], target, 0.24f);

            Color offColor = isHovering ? uiPanelHoverColor : uiPanelAltColor;
            Color onColor = isHovering ? uiAccentHoverColor : uiAccentColor;
            Color trackColor = Color.Lerp(offColor, onColor, toggleAnimationState[toggleKey]);

            Rect borderRect = new Rect(toggleRect.x, toggleRect.y, toggleRect.width, toggleRect.height);
            Rect fillRect = new Rect(toggleRect.x + 1f, toggleRect.y + 1f, toggleRect.width - 2f, toggleRect.height - 2f);
            DrawTintedTexture(borderRect, toggleCapsuleMaskTexture, new Color(1f, 1f, 1f, 0.18f));
            DrawTintedTexture(fillRect, toggleCapsuleMaskTexture, trackColor);

            float knobX = Mathf.Lerp(toggleRect.x + padding, toggleRect.x + toggleRect.width - knobSize - padding, toggleAnimationState[toggleKey]);
            Rect knobRect = new Rect(knobX, toggleRect.y + padding, knobSize, knobSize);
            DrawTintedTexture(knobRect, toggleKnobTexture, Color.white);

            GUI.Label(labelRect, label, labelStyle);
            DrawControllerFocusRing(controllerTargetId, fullRect);
            GUILayout.Space(UiControlSpacing);

            return value;
        }

        private static Texture2D _whiteTex;
        private static Texture2D GetWhiteTexture()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            return _whiteTex;
        }

        private static void DrawTintedTexture(Rect rect, Texture2D texture, Color tint)
        {
            if (texture == null)
            {
                DrawSolidColorRect(rect, tint);
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = previousColor;
        }

        private static void DrawSolidColorRect(Rect rect, Color color)
        {
            // Store the current GUI color
            Color oldColor = GUI.color;
            // Set the GUI color to the specified color
            GUI.color = color;
            // Draw the texture with the specified color
            GUI.DrawTexture(rect, GetWhiteTexture());
            // Restore the original GUI color
            GUI.color = oldColor;
        }

        private static Dictionary<string, string> _sliderTextInputs = new Dictionary<string, string>();

        public static void ModernSlider(string label, ref float target, float min, float max, string controlId = null)
        {
            string sliderKey = string.IsNullOrEmpty(controlId) ? label : controlId;
            DrawSliderInternal(label, ref target, min, max, sliderKey, false, 0f);
        }

        private static void SetSliderValueFromMouse(ref float target, float min, float max, Rect sliderRect,
            float mouseX)
        {
            float clampedX = Mathf.Clamp(mouseX, sliderRect.x, sliderRect.xMax);
            float t = Mathf.InverseLerp(sliderRect.x, sliderRect.xMax, clampedX);
            float raw = Mathf.Lerp(min, max, t);
            target = Mathf.Round(raw * 100f) / 100f;
        }

        public static bool ModernButton(string label, float width = 200f, float height = 30f)
        {
            return ControllerButton(label, label, highQualityButtonStyle,
                GUILayout.Width(width * UiScale), GUILayout.Height(Mathf.Max(36f * UiScale, height * UiScale)),
                GUILayout.ExpandWidth(false));
        }

        public static IEnumerator LoadRoweLogo()
        {
            if (logoTexture != null)
            {
                isLogoLoaded = true;
                Log.Msg($"[MenuLogo] Load request skipped: texture already present ({LogoTextureDebugInfo}).");
                yield break;
            }

            if (isLogoLoading)
            {
                Log.Msg("[MenuLogo] Load request skipped: already loading.");
                yield break;
            }

            isLogoLoading = true;
            isLogoLoaded = false;
            string url = "https://github.com/xrowex/RoweModsLogo/raw/main/rowemods.png";
            Log.Msg($"[MenuLogo] Starting logo download from: {url}");
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
            yield return www.SendWebRequest();

            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Log.Error($"Logo download failed: {www.error}");
                    isLogoLoaded = false;
                }
                else
                {
                    logoTexture = DownloadHandlerTexture.GetContent(www);
                    isLogoLoaded = logoTexture != null;
                    Log.Msg($"RoweMods logo loaded successfully. texture={LogoTextureDebugInfo}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in LoadRoweLogo: {ex.Message}");
                isLogoLoaded = false;
            }
            finally
            {
                isLogoLoading = false;
                www.Dispose();
            }
        }
        private static void ResetSliderUI()
        {
            _sliderTextInputs.Clear();
            toggleAnimationState.Clear();
            GUI.FocusControl(null);
        }
    }
}
