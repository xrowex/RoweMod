using UnityEngine;
using static rowemod.Utils.Memory;
using static rowemod.Config;
using static rowemod.Mods.Misc;
using static rowemod.Mods.Custom;
using static rowemod.Mods.BikeMaterialsLoader;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMashBox.Addons.ContentManagment;
using Il2CppMashBox.Addons.NetworkingFusion;
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
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using rowemod.Challenges;

namespace rowemod
{
    public static class Menu
    {
        // Defining the tabs available in the menu
        public enum Tab
        {
            Physics,
            Bike,
            BikePoser,
            Grinds,
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

        private const float UiOuterPadding = 14f;
        private const float UiInnerPadding = 10f;
        private const float UiControlSpacing = 8f;
        private const float UiSectionSpacing = 12f;
        private const float UiTitleBarHeight = 25f;
        private const float UiResetButtonWidth = 94f;
        private const float UiTabSpacing = 6f;
        private const float UiSidebarWidth = 146f;
        private const float UiLogoAreaHeight = 72f;
        private const float UiContentHeaderHeight = 42f;
        private const float UiNavButtonHeight = 32f;
        private const float UiFoldoutHeight = 30f;
        private const float UiRowHeight = 26f;
        private const float UiContentBottomPadding = 20f;

        private static readonly (string label, Tab tab)[] _visibleTabs =
        {
            ("Physics", Tab.Physics),
            ("Tricks", Tab.Tricks),
            ("Bike", Tab.Bike),
            ("Bike Poser", Tab.BikePoser),
            ("Grinds", Tab.Grinds),
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
        public static GUIStyle sectionCardStyle;
        public static GUIStyle sectionHeaderStyle;
        public static GUIStyle tabButtonStyle;
        public static GUIStyle tabBarStyle;
        public static GUIStyle subtleLabelStyle;
        public static GUIStyle pageTitleStyle;
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
        // Texture caches for styles
        private static Texture2D backgroundTexture;
        private static Texture2D roundedButtonNormal;
        private static Texture2D roundedButtonHover;
        private static Texture2D activeTabBackground;
        private static Texture2D accentColorTexture;
        private static Texture2D tabIndicatorTexture;
        private static Texture2D toggleCapsuleMaskTexture;
        private static Texture2D toggleKnobTexture;

        private static Color uiAccentColor;
        private static Color uiAccentHoverColor;
        private static Color uiAccentSoftColor;
        private static Color uiBackgroundColor;
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
        private static string _mpKickStatus = "Host/master only. Join or host a multiplayer session to manage players.";
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
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawMenu: {ex.Message}");
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
                windowRect.width = Mathf.Max(600f, resizeStartSize.x + delta.x);
                windowRect.height = Mathf.Max(420f, resizeStartSize.y + delta.y);
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

        private static void DrawTabContent()
        {
            try
            {
                switch (currentTab)
                {
                    case Tab.Physics:
                        Mods.Physics.Update();

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

                        DrawSectionTitle("Speed");
                        Slider("Push Force", ref physics.bmxForceFactor, 0.07f, 0.05f, 2f);
                        Slider("Max Speed", ref physics.bmxMaxSpeed, 7.5f, 2f, 15f);
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

                        DrawSectionTitle("Motor Tuning");
                        if (_motorTuningNeedsRefresh)
                            RefreshMotorTuningData();
                        DrawMotorTuningData();
                        EndPane();

                        EndTwoPane();

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

                        BeginPane("Players", "Kick tools for detected network players.");
                        DrawMultiplayerPlayerKickList();
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
                            if (GUILayout.Button(marker.name, UiRowButtonStyle, GUILayout.Height(26f)))
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

                        if (PrimaryButton(MultiplayerChallengeManager.IsOpen ? "Close Challenge UI" : "Open Challenge UI", GUILayout.Width(160f), GUILayout.Height(26f)))
                        {
                            MultiplayerChallengeManager.ToggleWindow();
                        }
                        EndToolbar();
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
            return UiSidebarWidth + UiOuterPadding;
        }

        private static float GetContentWidth()
        {
            return Mathf.Max(220f, windowRect.width - GetContentX() - UiOuterPadding);
        }

        private static Rect GetContentVisibleRect()
        {
            float y = UiTitleBarHeight + UiOuterPadding + UiContentHeaderHeight + UiControlSpacing;
            float height = Mathf.Max(120f, windowRect.height - y - UiContentBottomPadding);
            return new Rect(GetContentX(), y, GetContentWidth(), height);
        }

        private static string GetCurrentTabLabel()
        {
            for (int i = 0; i < _visibleTabs.Length; i++)
            {
                if (_visibleTabs[i].tab == currentTab)
                    return _visibleTabs[i].label;
            }

            return currentTab.ToString();
        }

        public static void SelectNextVisibleTab()
        {
            SelectVisibleTab(1);
        }

        public static void SelectPreviousVisibleTab()
        {
            SelectVisibleTab(-1);
        }

        public static void AdjustControllerScroll(float delta)
        {
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
            int currentIndex = GetVisibleTabIndex(currentTab);
            if (currentIndex < 0)
                return;

            float navTop = UiTitleBarHeight + UiLogoAreaHeight + UiOuterPadding;
            float navHeight = Mathf.Max(120f, windowRect.height - navTop - UiOuterPadding);
            float totalTabsHeight = (_visibleTabs.Length * UiNavButtonHeight) + ((_visibleTabs.Length - 1) * UiTabSpacing);
            float maxScroll = Mathf.Max(0f, totalTabsHeight - navHeight);

            float tabTop = currentIndex * (UiNavButtonHeight + UiTabSpacing);
            float tabBottom = tabTop + UiNavButtonHeight;

            if (tabTop < _tabScrollPosition.y)
            {
                _tabScrollPosition.y = tabTop;
            }
            else if (tabBottom > _tabScrollPosition.y + navHeight)
            {
                _tabScrollPosition.y = tabBottom - navHeight;
            }

            _tabScrollPosition.y = Mathf.Clamp(_tabScrollPosition.y, 0f, maxScroll);
        }

        private static void DrawSidebar()
        {
            Rect sidebarRect = new Rect(0f, 0f, UiSidebarWidth, windowRect.height);
            DrawSolidColorRect(sidebarRect, new Color(0.035f, 0.04f, 0.055f, 0.82f));

            Rect dividerRect = new Rect(UiSidebarWidth - 1f, UiTitleBarHeight, 1f, windowRect.height - UiTitleBarHeight);
            DrawSolidColorRect(dividerRect, uiBorderColor);

            DrawLogoHeader();
        }

        private static void DrawLogoHeader()
        {
            Rect logoAreaRect = new Rect(UiOuterPadding, UiTitleBarHeight + 10f, UiSidebarWidth - (UiOuterPadding * 2f), UiLogoAreaHeight - 10f);
            if (logoTexture != null && logoTexture.width > 0 && logoTexture.height > 0)
            {
                float maxLogoWidth = logoAreaRect.width;
                float maxLogoHeight = 46f;
                float scale = Mathf.Min(maxLogoWidth / logoTexture.width, maxLogoHeight / logoTexture.height);
                float logoWidth = logoTexture.width * scale;
                float logoHeight = logoTexture.height * scale;
                Rect logoRect = new Rect(logoAreaRect.x, logoAreaRect.y + 2f, logoWidth, logoHeight);

                Color previousColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
                GUI.color = previousColor;
            }
            else
            {
                GUI.Label(logoAreaRect, "RoweMod", sectionHeaderStyle);
            }

            Rect versionRect = new Rect(logoAreaRect.x, logoAreaRect.yMax - 18f, logoAreaRect.width, 18f);
            GUI.Label(versionRect, $"v. {Main.ModVersion}", subtleLabelStyle);
        }

        private static void DrawContentHeader()
        {
            Rect headerRect = new Rect(GetContentX(), UiTitleBarHeight + UiOuterPadding, GetContentWidth(), UiContentHeaderHeight);
            GUI.Box(headerRect, GUIContent.none, tabBarStyle);

            Rect titleRect = new Rect(headerRect.x + UiInnerPadding, headerRect.y, headerRect.width - UiResetButtonWidth - (UiInnerPadding * 3f), headerRect.height);
            GUI.Label(titleRect, GetCurrentTabLabel(), pageTitleStyle ?? sectionHeaderStyle);

            Rect resetButtonRect = new Rect(headerRect.xMax - UiInnerPadding - UiResetButtonWidth, headerRect.y + 6f,
                UiResetButtonWidth, headerRect.height - 12f);
            if (GUI.Button(resetButtonRect, "Reset Tab", redButtonStyle ?? highQualityButtonStyle))
            {
                ResetCurrentTab();
                ResetSliderUI();
            }
        }

        private static void HandleScrolling()
        {
            try
            {
                Event currentEvent = Event.current;
                if (currentEvent == null || currentEvent.type != EventType.ScrollWheel)
                    return;

                if (currentTab == Tab.Tricks)
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
                DrawTabContent();
                GUILayout.Space(UiContentBottomPadding);

                scrollOffset = Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, scrollViewHeight - viewHeight));
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawScrollableContent: {ex.Message}");
            }
            finally
            {
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
            float navTop = UiTitleBarHeight + UiLogoAreaHeight + UiOuterPadding;
            float navWidth = UiSidebarWidth - (UiOuterPadding * 2f);
            float navHeight = Mathf.Max(120f, windowRect.height - navTop - UiOuterPadding);
            Rect navRect = new Rect(UiOuterPadding, navTop, navWidth, navHeight);
            float totalTabsHeight = (_visibleTabs.Length * UiNavButtonHeight) + ((_visibleTabs.Length - 1) * UiTabSpacing);
            bool tabsOverflow = totalTabsHeight > navRect.height;
            float tabWidth = tabsOverflow ? navWidth - 14f : navWidth;
            Rect viewRect = new Rect(0f, 0f, tabWidth, Mathf.Max(totalTabsHeight, navRect.height));

            if (Event.current.type == EventType.ScrollWheel && navRect.Contains(Event.current.mousePosition))
            {
                _tabScrollPosition.y += Event.current.delta.y * 22f;
                Event.current.Use();
            }

            _tabScrollPosition = GUI.BeginScrollView(navRect, _tabScrollPosition, viewRect, false, tabsOverflow);
            _tabScrollPosition.y = Mathf.Clamp(_tabScrollPosition.y, 0f, Mathf.Max(0f, viewRect.height - navRect.height));

            float tabY = 0f;
            for (int i = 0; i < _visibleTabs.Length; i++)
            {
                var (label, tab) = _visibleTabs[i];
                Rect tabRect = new Rect(0f, tabY, tabWidth, UiNavButtonHeight);
                bool isSelected = currentTab == tab;

                if (GUI.Button(tabRect, label, isSelected ? activeTabButtonStyle : tabButtonStyle))
                {
                    SetCurrentTab(tab);
                }

                if (isSelected)
                {
                    Rect indicatorRect = new Rect(tabRect.x + 1f, tabRect.y + 7f, 3f, tabRect.height - 14f);
                    GUI.DrawTexture(indicatorRect, tabIndicatorTexture);
                }

                tabY += UiNavButtonHeight + UiTabSpacing;
            }

            GUI.EndScrollView();
        }

        public static void NotifyMenuOpened()
        {
            _motorTuningNeedsRefresh = true;
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
                    stylesInitialized = false;
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
                    _mpKickStatus = "Host/master only. Join or host a multiplayer session to manage players.";
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

        private static void DrawMultiplayerPlayerKickList()
        {
            NetworkPlayer[] players;
            try
            {
                players = UnityEngine.Object.FindObjectsOfType<NetworkPlayer>(true);
            }
            catch (Exception ex)
            {
                DrawEmptyState("Could not read network players", ex.Message);
                return;
            }

            if (players == null || players.Length == 0)
            {
                DrawEmptyState("No network players found.", _mpKickStatus);
                return;
            }

            bool anyKickAuthority = false;
            foreach (NetworkPlayer player in players)
            {
                if (player == null)
                    continue;

                if (RunnerCanKick(SafeGetRunner(player)))
                {
                    anyKickAuthority = true;
                    break;
                }
            }

            GUILayout.Label(anyKickAuthority
                ? "Kick sends a Fusion disconnect request for that player."
                : "Kick controls require host/server or shared-mode master authority.", UiMutedWrappedStyle);

            foreach (NetworkPlayer player in players)
            {
                if (player == null)
                    continue;

                DrawMultiplayerPlayerKickRow(player);
            }

            if (!string.IsNullOrWhiteSpace(_mpKickStatus))
            {
                GUILayout.Space(4f);
                GUILayout.Label(_mpKickStatus, UiMutedWrappedStyle);
            }
        }

        private static void DrawMultiplayerPlayerKickRow(NetworkPlayer player)
        {
            string displayName = ResolveNetworkPlayerDisplayName(player);
            bool isLocalPlayer = IsLocalNetworkPlayerForMenu(player);
            Il2CppFusion.NetworkRunner runner = SafeGetRunner(player);
            bool hasKickAuthority = RunnerCanKick(runner);
            bool hasValidPlayerRef = TryGetNetworkPlayerRef(player, out Il2CppFusion.PlayerRef playerRef);
            bool canKick = !isLocalPlayer && hasKickAuthority && hasValidPlayerRef;

            GUILayout.BeginHorizontal(UiPanelAltStyle);
            GUILayout.Label(isLocalPlayer ? $"{displayName} (You)" : displayName, UiRowLabelStyle, GUILayout.MinWidth(220f));

            string status = GetNetworkPlayerKickStatus(isLocalPlayer, runner, hasKickAuthority, hasValidPlayerRef, playerRef);
            GUILayout.Label(status, UiRowMutedLabelStyle, GUILayout.MinWidth(160f));

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && canKick;
            if (DangerButton("Kick", GUILayout.Width(86f), GUILayout.Height(24f)))
            {
                TryKickNetworkPlayer(player);
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
        }

        private static string GetNetworkPlayerKickStatus(bool isLocalPlayer, Il2CppFusion.NetworkRunner runner, bool hasKickAuthority, bool hasValidPlayerRef, Il2CppFusion.PlayerRef playerRef)
        {
            if (isLocalPlayer)
                return "local";

            if (runner == null)
                return "no runner";

            if (!IsRunnerInSession(runner))
                return "not in session";

            if (!hasKickAuthority)
                return "host only";

            if (!hasValidPlayerRef)
                return "invalid ref";

            return $"ref {playerRef.RawEncoded}";
        }

        private static bool TryKickNetworkPlayer(NetworkPlayer player)
        {
            if (player == null)
            {
                _mpKickStatus = "Kick failed: no player selected.";
                return false;
            }

            string displayName = ResolveNetworkPlayerDisplayName(player);
            if (IsLocalNetworkPlayerForMenu(player))
            {
                _mpKickStatus = "Kick blocked: refusing to disconnect the local player.";
                return false;
            }

            Il2CppFusion.NetworkRunner runner = SafeGetRunner(player);
            if (!RunnerCanKick(runner))
            {
                _mpKickStatus = "Kick failed: host/server or shared-mode master authority required.";
                return false;
            }

            if (!TryGetNetworkPlayerRef(player, out Il2CppFusion.PlayerRef playerRef))
            {
                _mpKickStatus = "Kick failed: invalid player reference.";
                return false;
            }

            try
            {
                runner.Disconnect(playerRef, new Il2CppStructArray<byte>(0));
                _mpKickStatus = $"Kick requested for {displayName}.";
                Log.Msg($"[MP] Kick requested for {displayName} (PlayerRef {playerRef.RawEncoded}).");
                return true;
            }
            catch (Exception ex)
            {
                _mpKickStatus = $"Kick failed: {ex.Message}";
                Log.Warning($"[MP] Kick failed for {displayName}: {ex}");
                return false;
            }
        }

        private static Il2CppFusion.NetworkRunner SafeGetRunner(NetworkPlayer player)
        {
            try
            {
                return player != null ? player.Runner : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool RunnerCanKick(Il2CppFusion.NetworkRunner runner)
        {
            try
            {
                return runner != null &&
                       runner.IsRunning &&
                       runner.IsInSession &&
                       (runner.IsServer || runner.IsSharedModeMasterClient);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRunnerInSession(Il2CppFusion.NetworkRunner runner)
        {
            try
            {
                return runner != null && runner.IsRunning && runner.IsInSession;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetNetworkPlayerRef(NetworkPlayer player, out Il2CppFusion.PlayerRef playerRef)
        {
            playerRef = Il2CppFusion.PlayerRef.Invalid;
            if (player == null)
                return false;

            try
            {
                playerRef = player.PlayerReference;
                return playerRef.IsRealPlayer;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLocalNetworkPlayerForMenu(NetworkPlayer player)
        {
            if (player == null)
                return false;

            try
            {
                if (player.IsLocalPlayer)
                    return true;
            }
            catch
            {
                // Fall back to reflected fields/properties below.
            }

            return TryGetBoolMemberForMenu(player, "_isLocal", out bool isLocal) && isLocal;
        }

        private static string ResolveNetworkPlayerDisplayName(NetworkPlayer player)
        {
            if (player == null)
                return "Unknown Player";

            string name = TryGetStringMemberForMenu(player, "DisplayName") ??
                          TryGetStringMemberForMenu(player, "PlayerName") ??
                          TryGetStringMemberForMenu(player, "UserName") ??
                          TryGetStringMemberForMenu(player, "Username") ??
                          TryGetStringMemberForMenu(player, "NickName") ??
                          TryGetStringMemberForMenu(player, "Nickname") ??
                          TryGetStringMemberForMenu(player, "Name") ??
                          TryGetStringMemberForMenu(player, "_displayName") ??
                          TryGetStringMemberForMenu(player, "_playerName") ??
                          TryGetStringMemberForMenu(player, "_userName") ??
                          TryGetStringMemberForMenu(player, "_username") ??
                          TryGetStringMemberForMenu(player, "_nickName") ??
                          TryGetStringMemberForMenu(player, "_nickname");

            if (IsUsableNetworkPlayerText(name))
                return name.Trim();

            if (IsLocalNetworkPlayerForMenu(player))
            {
                string steamName = GetSteamPersonaNameForMenu();
                if (IsUsableNetworkPlayerText(steamName))
                    return steamName.Trim();
            }

            try
            {
                string gameObjectName = player.gameObject != null ? player.gameObject.name : null;
                if (IsUsableNetworkPlayerText(gameObjectName))
                    return gameObjectName.Trim();
            }
            catch
            {
                // Ignore unavailable IL2CPP game object data.
            }

            return $"Player {Math.Abs(player.GetInstanceID())}";
        }

        private static bool TryGetBoolMemberForMenu(object target, string memberName, out bool value)
        {
            value = false;
            object raw = TryGetMemberValueForMenu(target, memberName);
            if (raw is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            return raw != null && bool.TryParse(raw.ToString(), out value);
        }

        private static string TryGetStringMemberForMenu(object target, string memberName)
        {
            return ExtractReadableNetworkPlayerText(TryGetMemberValueForMenu(target, memberName));
        }

        private static object TryGetMemberValueForMenu(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = target.GetType();

            try
            {
                PropertyInfo property = type.GetProperty(memberName, flags);
                if (property != null && property.CanRead)
                    return property.GetValue(target, null);
            }
            catch
            {
                // Ignore reflected IL2CPP property access failures.
            }

            try
            {
                FieldInfo field = type.GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(target);
            }
            catch
            {
                // Ignore reflected IL2CPP field access failures.
            }

            return null;
        }

        private static string ExtractReadableNetworkPlayerText(object value)
        {
            if (value == null)
                return null;

            if (value is string directString)
                return IsUsableNetworkPlayerText(directString) ? directString.Trim() : null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string[] memberNames =
            {
                "Value",
                "String",
                "Text",
                "Name",
                "_value",
                "_string",
                "_text",
                "_name",
                "m_Value",
                "m_String",
                "m_Text"
            };

            Type type = value.GetType();
            foreach (string memberName in memberNames)
            {
                try
                {
                    PropertyInfo property = type.GetProperty(memberName, flags);
                    if (property != null && property.CanRead)
                    {
                        string nestedValue = ExtractReadableNetworkPlayerText(property.GetValue(value, null));
                        if (IsUsableNetworkPlayerText(nestedValue))
                            return nestedValue.Trim();
                    }
                }
                catch
                {
                    // Continue trying other reflected members.
                }

                try
                {
                    FieldInfo field = type.GetField(memberName, flags);
                    if (field != null)
                    {
                        string nestedValue = ExtractReadableNetworkPlayerText(field.GetValue(value));
                        if (IsUsableNetworkPlayerText(nestedValue))
                            return nestedValue.Trim();
                    }
                }
                catch
                {
                    // Continue trying other reflected members.
                }
            }

            string text = value.ToString();
            return IsUsableNetworkPlayerText(text) ? text.Trim() : null;
        }

        private static string GetSteamPersonaNameForMenu()
        {
            try
            {
                string personaName = Il2CppSteamworks.SteamFriends.GetPersonaName();
                return IsUsableNetworkPlayerText(personaName) ? personaName.Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUsableNetworkPlayerText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();
            return !string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(trimmed, "(null)", StringComparison.OrdinalIgnoreCase) &&
                   !trimmed.StartsWith("Il2Cpp", StringComparison.Ordinal) &&
                   !trimmed.StartsWith("System.", StringComparison.Ordinal) &&
                   !trimmed.Contains("NetworkString`") &&
                   !trimmed.Contains("`1[") &&
                   !trimmed.Contains("[Il2Cpp");
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

        public static void InitializeStyles()
        {
            try
            {
                stylesInitialized = true;
                styleRevision++;

                Color rawAccentColor = new Color(
                    Mathf.Clamp01(misc.menuAccentR),
                    Mathf.Clamp01(misc.menuAccentG),
                    Mathf.Clamp01(misc.menuAccentB),
                    1f);
                float accentLuma = (rawAccentColor.r * 0.299f) + (rawAccentColor.g * 0.587f) + (rawAccentColor.b * 0.114f);
                uiAccentColor = rawAccentColor;
                uiAccentHoverColor = Color.Lerp(uiAccentColor, Color.white, 0.18f);
                uiAccentSoftColor = new Color(uiAccentColor.r, uiAccentColor.g, uiAccentColor.b, 0.36f);
                uiAccentTextColor = accentLuma < 0.45f
                    ? Color.Lerp(uiAccentColor, Color.white, 0.68f)
                    : Color.white;
                uiBackgroundColor = new Color(0.025f, 0.027f, 0.032f, 0.988f);
                uiPanelColor = new Color(0.043f, 0.046f, 0.054f, 0.965f);
                uiPanelHoverColor = new Color(0.074f, 0.077f, 0.088f, 0.98f);
                uiPanelAltColor = new Color(0.057f, 0.06f, 0.07f, 0.97f);
                uiBorderColor = new Color(1f, 1f, 1f, 0.085f);
                uiTextPrimaryColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                uiTextMutedColor = new Color(0.66f, 0.66f, 0.67f, 1f);
                uiDangerColor = new Color(0.48f, 0.21f, 0.24f, 0.96f);
                uiDangerHoverColor = new Color(0.58f, 0.25f, 0.28f, 0.98f);
                uiDangerActiveColor = new Color(0.38f, 0.15f, 0.18f, 0.98f);

                backgroundTexture = MakeRoundedTex(64, 64, uiBackgroundColor, 11, 1, uiBorderColor);
                roundedButtonNormal = MakeRoundedTex(40, 28, new Color(0.075f, 0.077f, 0.086f, 0.97f), 7, 1, uiBorderColor);
                roundedButtonHover = MakeRoundedTex(40, 28, new Color(0.105f, 0.108f, 0.12f, 0.99f), 7, 1, new Color(1f, 1f, 1f, 0.13f));
                activeTabBackground = MakeRoundedTex(40, 32, new Color(uiAccentColor.r * 0.62f, uiAccentColor.g * 0.43f, uiAccentColor.b * 0.31f, 0.88f), 8, 1, new Color(uiAccentColor.r, uiAccentColor.g, uiAccentColor.b, 0.76f));
                accentColorTexture = MakeTex(2, 2, uiAccentColor);
                tabIndicatorTexture = MakeTex(2, 2, uiAccentColor);
                toggleCapsuleMaskTexture = MakeCapsuleTex(96, 52, Color.white, 0, Color.clear);
                toggleKnobTexture = MakeCircleTex(64, new Color(0.96f, 0.97f, 1f, 1f), 1, new Color(0f, 0f, 0f, 0.45f));
                _circleTex = toggleKnobTexture;

                windowStyle = new GUIStyle(GUI.skin.window);
                windowStyle.normal.background = backgroundTexture;
                windowStyle.onNormal.background = backgroundTexture;
                windowStyle.hover.background = backgroundTexture;
                windowStyle.active.background = backgroundTexture;
                windowStyle.normal.textColor = uiTextPrimaryColor;
                windowStyle.hover.textColor = uiTextPrimaryColor;
                windowStyle.active.textColor = uiTextPrimaryColor;
                windowStyle.fontSize = 15;
                windowStyle.fontStyle = FontStyle.Bold;
                windowStyle.alignment = TextAnchor.UpperCenter;
                windowStyle.padding = new RectOffset((int)UiOuterPadding, (int)UiOuterPadding, (int)(UiTitleBarHeight + UiOuterPadding),
                    (int)UiOuterPadding);

                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = uiTextPrimaryColor;
                labelStyle.hover.textColor = uiTextPrimaryColor;
                labelStyle.fontSize = 13;
                labelStyle.fontStyle = FontStyle.Normal;
                labelStyle.richText = true;
                labelStyle.alignment = TextAnchor.MiddleLeft;

                subtleLabelStyle = new GUIStyle(labelStyle);
                subtleLabelStyle.normal.textColor = uiTextMutedColor;
                subtleLabelStyle.fontSize = 12;

                sectionHeaderStyle = new GUIStyle(labelStyle);
                sectionHeaderStyle.fontStyle = FontStyle.Bold;
                sectionHeaderStyle.fontSize = 13;

                pageTitleStyle = new GUIStyle(sectionHeaderStyle);
                pageTitleStyle.fontSize = 14;
                pageTitleStyle.alignment = TextAnchor.MiddleLeft;

                rowLabelStyle = new GUIStyle(labelStyle);
                rowLabelStyle.richText = true;
                rowLabelStyle.alignment = TextAnchor.MiddleLeft;
                rowLabelStyle.padding = new RectOffset(6, 6, 2, 2);

                rowMutedLabelStyle = new GUIStyle(rowLabelStyle);
                rowMutedLabelStyle.normal.textColor = uiTextMutedColor;
                rowMutedLabelStyle.fontSize = 12;
                rowMutedLabelStyle.wordWrap = true;

                mutedWrappedLabelStyle = new GUIStyle(subtleLabelStyle);
                mutedWrappedLabelStyle.wordWrap = true;

                toggleStyle = new GUIStyle(GUI.skin.toggle);
                toggleStyle.normal.textColor = uiTextPrimaryColor;
                toggleStyle.fontSize = 12;

                horizontalSliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
                horizontalSliderStyle.normal.background = MakeTex(2, 2, uiPanelAltColor);
                horizontalSliderStyle.fixedHeight = 7f;

                horizontalSliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
                horizontalSliderThumbStyle.normal.background = accentColorTexture;
                horizontalSliderThumbStyle.hover.background = MakeTex(2, 2, uiAccentHoverColor);
                horizontalSliderThumbStyle.fixedWidth = 10f;
                horizontalSliderThumbStyle.fixedHeight = 14f;

                coloredBoxStyle = new GUIStyle(GUI.skin.box);
                coloredBoxStyle.normal.background = MakeTex(2, 2, uiPanelAltColor);
                coloredBoxStyle.normal.textColor = uiTextPrimaryColor;
                coloredBoxStyle.fontSize = 13;
                coloredBoxStyle.fixedHeight = UiFoldoutHeight;

                highQualityButtonStyle = new GUIStyle(GUI.skin.button);
                highQualityButtonStyle.normal.background = roundedButtonNormal;
                highQualityButtonStyle.hover.background = roundedButtonHover;
                highQualityButtonStyle.active.background = MakeRoundedTex(40, 28, new Color(0.05f, 0.055f, 0.066f, 0.98f), 7, 1, new Color(1f, 1f, 1f, 0.08f));
                highQualityButtonStyle.normal.textColor = uiTextPrimaryColor;
                highQualityButtonStyle.hover.textColor = uiTextPrimaryColor;
                highQualityButtonStyle.active.textColor = uiTextPrimaryColor;
                highQualityButtonStyle.fontSize = 12;
                highQualityButtonStyle.fontStyle = FontStyle.Normal;
                highQualityButtonStyle.alignment = TextAnchor.MiddleCenter;
                highQualityButtonStyle.border = new RectOffset(7, 7, 7, 7);
                highQualityButtonStyle.padding = new RectOffset(12, 12, 6, 6);
                highQualityButtonStyle.richText = true;

                rowButtonStyle = new GUIStyle(highQualityButtonStyle);
                rowButtonStyle.alignment = TextAnchor.MiddleLeft;
                rowButtonStyle.padding = new RectOffset(10, 10, 4, 4);
                rowButtonStyle.margin = new RectOffset(2, 2, 1, 1);

                rowButtonSelectedStyle = new GUIStyle(rowButtonStyle);
                rowButtonSelectedStyle.normal.background = activeTabBackground;
                rowButtonSelectedStyle.hover.background = activeTabBackground;
                rowButtonSelectedStyle.active.background = activeTabBackground;
                rowButtonSelectedStyle.normal.textColor = uiAccentTextColor;
                rowButtonSelectedStyle.hover.textColor = uiAccentTextColor;
                rowButtonSelectedStyle.active.textColor = uiAccentTextColor;
                rowButtonSelectedStyle.fontStyle = FontStyle.Bold;

                pillButtonStyle = new GUIStyle(highQualityButtonStyle);
                pillButtonStyle.fontSize = 11;
                pillButtonStyle.padding = new RectOffset(10, 10, 3, 3);
                pillButtonStyle.margin = new RectOffset(4, 0, 0, 0);
                pillButtonStyle.border = new RectOffset(7, 7, 7, 7);

                pillActiveButtonStyle = new GUIStyle(pillButtonStyle);
                pillActiveButtonStyle.normal.background = activeTabBackground;
                pillActiveButtonStyle.hover.background = MakeRoundedTex(40, 28, new Color(uiAccentColor.r * 0.68f, uiAccentColor.g * 0.5f, uiAccentColor.b * 0.38f, 0.9f), 7, 1, uiAccentColor);
                pillActiveButtonStyle.active.background = MakeRoundedTex(40, 28, new Color(uiAccentColor.r * 0.52f, uiAccentColor.g * 0.38f, uiAccentColor.b * 0.28f, 0.94f), 7, 1, uiAccentHoverColor);
                pillActiveButtonStyle.normal.textColor = Color.white;
                pillActiveButtonStyle.hover.textColor = Color.white;
                pillActiveButtonStyle.active.textColor = Color.white;
                pillActiveButtonStyle.fontStyle = FontStyle.Bold;

                miniButtonStyle = new GUIStyle(highQualityButtonStyle);
                miniButtonStyle.fontSize = 11;
                miniButtonStyle.padding = new RectOffset(7, 7, 3, 3);
                miniButtonStyle.margin = new RectOffset(2, 2, 0, 0);

                tabButtonStyle = new GUIStyle(highQualityButtonStyle);
                tabButtonStyle.normal.background = MakeRoundedTex(40, 32, new Color(0f, 0f, 0f, 0f), 8, 1, new Color(0f, 0f, 0f, 0f));
                tabButtonStyle.hover.background = MakeRoundedTex(40, 32, new Color(1f, 1f, 1f, 0.04f), 8, 1, new Color(1f, 1f, 1f, 0.08f));
                tabButtonStyle.active.background = MakeRoundedTex(40, 32, new Color(1f, 1f, 1f, 0.06f), 8, 1, new Color(1f, 1f, 1f, 0.1f));
                tabButtonStyle.padding = new RectOffset(16, 10, 6, 6);
                tabButtonStyle.fontSize = 12;
                tabButtonStyle.alignment = TextAnchor.MiddleLeft;

                activeTabButtonStyle = new GUIStyle(tabButtonStyle);
                activeTabButtonStyle.normal.background = activeTabBackground;
                activeTabButtonStyle.hover.background = activeTabBackground;
                activeTabButtonStyle.active.background = activeTabBackground;
                activeTabButtonStyle.normal.textColor = uiAccentTextColor;
                activeTabButtonStyle.hover.textColor = uiAccentTextColor;
                activeTabButtonStyle.active.textColor = uiAccentTextColor;
                activeTabButtonStyle.fontStyle = FontStyle.Bold;

                redButtonStyle = new GUIStyle(highQualityButtonStyle);
                redButtonStyle.normal.background = MakeRoundedTex(40, 28, uiDangerColor, 7, 1, uiBorderColor);
                redButtonStyle.hover.background = MakeRoundedTex(40, 28, uiDangerHoverColor, 7, 1, uiBorderColor);
                redButtonStyle.active.background = MakeRoundedTex(40, 28, uiDangerActiveColor, 7, 1, uiBorderColor);
                redButtonStyle.normal.textColor = uiTextPrimaryColor;
                redButtonStyle.hover.textColor = uiTextPrimaryColor;
                redButtonStyle.active.textColor = uiTextPrimaryColor;

                sectionCardStyle = new GUIStyle(GUI.skin.box);
                sectionCardStyle.normal.background = MakeRoundedTex(64, 64, uiPanelColor, 9, 1, uiBorderColor);
                sectionCardStyle.padding = new RectOffset((int)UiInnerPadding, (int)UiInnerPadding, (int)UiInnerPadding, (int)UiInnerPadding);
                sectionCardStyle.margin = new RectOffset(0, 0, 0, 0);
                sectionCardStyle.border = new RectOffset(9, 9, 9, 9);

                panelStyle = new GUIStyle(sectionCardStyle);
                panelStyle.padding = new RectOffset(12, 12, 10, 12);
                panelStyle.margin = new RectOffset(0, 0, 4, 8);

                panelAltStyle = new GUIStyle(panelStyle);
                panelAltStyle.normal.background = MakeRoundedTex(64, 64, uiPanelAltColor, 9, 1, uiBorderColor);

                tabBarStyle = new GUIStyle(GUI.skin.box);
                tabBarStyle.normal.background = MakeRoundedTex(64, 36, uiPanelColor, 9, 1, uiBorderColor);
                tabBarStyle.border = new RectOffset(9, 9, 9, 9);
                tabBarStyle.padding = new RectOffset((int)UiInnerPadding, (int)UiInnerPadding, 4, 4);
                tabBarStyle.margin = new RectOffset(0, 0, 0, 0);

                toolbarStyle = new GUIStyle(tabBarStyle);
                toolbarStyle.padding = new RectOffset(10, 10, 7, 7);
                toolbarStyle.margin = new RectOffset(0, 0, 0, 8);

                badgeStyle = new GUIStyle(GUI.skin.box);
                badgeStyle.normal.background = MakeRoundedTex(32, 22, uiPanelAltColor, 7, 1, uiBorderColor);
                badgeStyle.normal.textColor = uiTextMutedColor;
                badgeStyle.alignment = TextAnchor.MiddleCenter;
                badgeStyle.fontSize = 11;
                badgeStyle.fixedHeight = 21f;
                badgeStyle.padding = new RectOffset(7, 7, 2, 2);
                badgeStyle.border = new RectOffset(7, 7, 7, 7);

                Texture2D textFieldBackground = MakeRoundedTex(64, 24, new Color(0.032f, 0.033f, 0.038f, 0.99f), 6, 1, new Color(1f, 1f, 1f, 0.115f));
                textFieldStyle = new GUIStyle(GUI.skin.textField);
                textFieldStyle.alignment = TextAnchor.MiddleCenter;
                textFieldStyle.fontSize = 12;
                textFieldStyle.font = labelStyle.font;
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
            }
            catch (Exception ex)
            {
                Log.Error($"Error in InitializeStyles: {ex.Message}");
            }
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
                            forceFactor = defaultEntry.forceFactor,
                            maxForce = defaultEntry.maxForce,
                            maxSpeed = defaultEntry.maxSpeed
                        };
                        motorTuning[configKey] = savedEntry;
                    }

                    engineSettings._forceFactor = savedEntry.forceFactor;
                    engineSettings._maxForce = savedEntry.maxForce;
                    engineSettings._maxSpeed = savedEntry.maxSpeed;

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
                        forceFactor = entry.DefaultForceFactor,
                        maxForce = entry.DefaultMaxForce,
                        maxSpeed = entry.DefaultMaxSpeed
                    };
                    motorTuning[entry.ConfigKey] = tune;
                }

                GUILayout.Space(2f);
                GUILayout.Label(entry.VehicleName, labelStyle);

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

        private static string GetMotorTuningConfigKey(MotorVehicleSettings vehicleSettings)
        {
            if (vehicleSettings == null || string.IsNullOrEmpty(vehicleSettings.name))
                return "MotorVehicleSettings";

            return vehicleSettings.name;
        }

        public static void DrawGraphicsSettings()
        {
            try
            {
                DrawLightSettings();
                
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawGraphicsSettings: {ex.Message}");
            }
        }
        
        

        private static void DrawLightSettings()
        {
            Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
            if (lights == null || lights.Length == 0)
            {
                BeginPane("Light Settings", "Scene lights will appear here when the current scene exposes them.");
                DrawEmptyState("No editable lights found.");
                EndPane();
                return;
            }

            CacheLightDefaults(lights);

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
            return GUILayout.Button(label, UiMiniButtonStyle, options);
        }

        public static bool PillButton(string label, bool active, params GUILayoutOption[] options)
        {
            return GUILayout.Button(label, active ? UiPillActiveStyle : UiPillStyle, options);
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
            return GUILayout.Button(label, UiPillActiveStyle, options);
        }

        public static bool SecondaryButton(string label, params GUILayoutOption[] options)
        {
            return GUILayout.Button(label, UiButtonStyle, options);
        }

        public static bool DangerButton(string label, params GUILayoutOption[] options)
        {
            return GUILayout.Button(label, UiDangerButtonStyle, options);
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

            Color baseColor = expanded ? uiPanelAltColor : uiPanelColor;
            Color backgroundColor = isHovering ? Color.Lerp(baseColor, uiPanelHoverColor, 0.6f) : baseColor;
            DrawSolidColorRect(rect, backgroundColor);

            if (expanded)
            {
                Rect accentRect = new Rect(rect.x + 1f, rect.y + 4f, 3f, rect.height - 8f);
                DrawSolidColorRect(accentRect, uiAccentColor);
            }

            Rect dividerRect = new Rect(rect.x + 8f, rect.yMax - 1f, rect.width - 16f, 1f);
            DrawSolidColorRect(dividerRect, new Color(1f, 1f, 1f, 0.07f));

            Rect arrowRect = new Rect(rect.x + 10f, rect.y, 16f, rect.height);
            Rect labelRect = new Rect(rect.x + 28f, rect.y, rect.width - 32f, rect.height);
            GUI.Label(arrowRect, expanded ? "▾" : "▸", sectionHeaderStyle);
            GUI.Label(labelRect, label, sectionHeaderStyle);

            if (currentEvent.type == EventType.MouseDown && rect.Contains(currentEvent.mousePosition))
            {
                expanded = !expanded;
                currentEvent.Use();
            }

            return expanded;
        }

        public static void Slider(string label, ref float target, float defaultVal, float min, float max)
        {
            DrawSliderInternal(label, ref target, min, max, label, true, defaultVal);
        }

        private static void DrawSliderInternal(string label, ref float target, float min, float max, string sliderKey, bool showResetButton, float defaultValue)
        {
            float rowHeight = UiRowHeight;
            float valueBoxWidth = 56f;
            float resetButtonWidth = showResetButton ? 62f : 0f;
            float spacing = 8f;
            float valueBorderSize = 1.5f;

            Rect fullRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight));

            float minSliderWidth = 70f;
            float minLabelWidth = 94f;
            float maxLabelWidth = 180f;
            float availableForLabel = fullRect.width - minSliderWidth - valueBoxWidth - (spacing * 3f) - resetButtonWidth;
            float labelWidth = Mathf.Clamp(availableForLabel * 0.6f, minLabelWidth, maxLabelWidth);
            labelWidth = Mathf.Min(labelWidth, Mathf.Max(minLabelWidth, availableForLabel));

            float valueX = fullRect.xMax - valueBoxWidth - valueBorderSize - (showResetButton ? (resetButtonWidth + spacing) : 0f);
            float sliderX = fullRect.x + labelWidth + spacing;
            float sliderRight = valueX - spacing;
            float sliderWidth = Mathf.Max(minSliderWidth, sliderRight - sliderX);

            float trackHeight = 6f;
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

            float knobSize = 12f;
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

            if (showResetButton && GUI.Button(resetRect, "Reset", highQualityButtonStyle))
            {
                target = defaultValue;
                _sliderTextInputs[sliderKey] = target.ToString("0.00");
            }

            GUILayout.Space(UiControlSpacing);
        }

        public static bool Toggle(string text, ref bool value)
        {
            try
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(value ? "✔" : " ", highQualityButtonStyle, GUILayout.Width(20)))
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

            float width = 48f;
            float height = 24f;
            float knobSize = 18f;
            float padding = 3f;
            Rect fullRect = GUILayoutUtility.GetRect(0f, UiRowHeight, GUILayout.ExpandWidth(true), GUILayout.Height(UiRowHeight));
            Rect labelRect = new Rect(fullRect.x, fullRect.y, fullRect.width - width - 10f, fullRect.height);
            Rect toggleRect = new Rect(fullRect.x + fullRect.width - width,
                fullRect.y + ((fullRect.height - height) * 0.5f), width, height);

            if (!toggleAnimationState.ContainsKey(toggleKey))
                toggleAnimationState[toggleKey] = value ? 1f : 0f;

            bool isHovering = fullRect.Contains(Event.current.mousePosition);
            bool clicked = Event.current.type == EventType.MouseDown &&
                           (toggleRect.Contains(Event.current.mousePosition) || labelRect.Contains(Event.current.mousePosition));
            if (clicked)
            {
                value = !value;
                Event.current.Use();
            }

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
            Rect buttonRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false), GUILayout.Height(height));
            return GUI.Button(buttonRect, label, highQualityButtonStyle);
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
