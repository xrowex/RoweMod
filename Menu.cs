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
using Harmony;
using UnityEngine.Rendering;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Character.Scripts;
//using Il2CppMashBox.Core.Common_Utils.Extension_Methods;
using Il2CppModIOBrowser;
using Il2CppPlayFab.ClientModels;
using static rowemod.Mods.Custom;

namespace rowemod
{
    public static class Menu
    {
        public enum Tab
        {
            Physics,
            Tricks,
            Character,
            Bike,
            BikeMaterials,
            Drone,
            Misc,
            Camera,
            Graphics,
            Marker
        }

// Menu Variables
        public static bool isOpen = false;
        public static Rect windowRect = new Rect(500f, 150f, 900f, 800f);
        public static Vector2 previousWindowPosition;
        public static Vector2 scrollPosition = Vector2.zero;
        public static bool isDraggingWindow = false;
        public static Tab currentTab;
        public static Tab previousTab;
        public static Slot currentSlot;
        public static bool inModelsTab = false;

// This will ensure all tabs are the same width and stretch across the menu width
        public static float tabWidth = (windowRect.width - 60) / Enum.GetValues(typeof(Tab)).Length;

// Scroll variables
        public static float scrollOffset = 0f;
        public static float scrollViewHeight = 9999999f; // Will be updated dynamically
        public static float viewHeight = 0f;

// Style variables
        public static GUIStyle windowStyle;

/*public static GUIStyle buttonStyle;*/
        public static GUIStyle labelStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle horizontalSliderStyle;
        public static GUIStyle horizontalSliderThumbStyle;
        public static GUIStyle coloredBoxStyle;
        public static GUIStyle highQualityButtonStyle;

// Flag to check if styles are initialized
        public static bool stylesInitialized = false;

// Dictionaries to store slider text values, etc.
        static Dictionary<string, string> sliderTextValues = new Dictionary<string, string>();
        static Dictionary<string, bool> sliderFieldJustFocused = new Dictionary<string, bool>();

        public static Dictionary<Slot, GameObject> slotGameObjects = new Dictionary<Slot, GameObject>();


        public static List<UnityEngine.Camera> cachedCameras;
        public static List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData> cachedHDRCameras;
        public static List<UnityEngine.Rendering.Volume> cachedVolumes;
        private static int selectedTAAIndex = 0; // Default index for TAA quality


//-------------------------------------------------------------------
// MENU & TAB LOGIC
//-------------------------------------------------------------------

        public static void DrawMenu(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));

            GUILayout.BeginVertical();
            GUILayout.Space(5);
            DrawTabs();
            GUILayout.Space(5);
            DrawScrollableContent();
            GUILayout.EndVertical();
        }

        static void DrawTabContent()
        {
            switch (currentTab)
            {
                case Tab.Physics:
                    Mods.Physics.Update();
                    GUILayout.Box("Toggles", coloredBoxStyle, GUILayout.Height(25),
                        GUILayout.ExpandWidth(true)); // Colored separator
                    Toggle("Spin Assist", ref bSpinAssist); 
                    Toggle("Drifting", ref bDriftAbility);
                    //GUILayout.Box("Hops", coloredBoxStyle, GUILayout.Height(25),
                        //GUILayout.ExpandWidth(true)); // Colored separator
                    Slider("Gravity", ref gravity, 0f, 30f);
                    //Slider("Hop Force", ref hopForce, 0f, 25f);
                    Slider("Small Hop Force", ref smallHopForce, 0f, 25f); /*
                                       Slider("Side Hop Force", ref sideHopPower, 0.1f, 5.0f);*/
                    GUILayout.Box("Pump/Spin", coloredBoxStyle, GUILayout.Height(25),
                        GUILayout.ExpandWidth(true)); // Colored separator
                    Slider("Pump Force", ref pumpForce, 0f, 30f);
                    Slider("Spin Speed Multiplier", ref spinTorque, 0f, 10f);
                    Slider("Steer Damping", ref steerDamp, 1f, 5f);
                    //Slider("Quick Spin Multiplier", ref quickSpinMultiplier, 1.0f, 10.0f);
                    GUILayout.Box("Manuals", coloredBoxStyle, GUILayout.Height(25),
                        GUILayout.ExpandWidth(true)); // Colored separator
                    Slider("Max Nose Manual Angle", ref noseManualAngle, 10f, 90f);
                    Slider("Max Manual Angle", ref manualAngle, 10f, 90f);
                    break;

                    /*case Tab.Bike:
                    PartTweaker.drawPartTweaker();
                    break;*/

                case Tab.Tricks:
                    TrickMods.DrawTrickMenu();
                    break;

                case Tab.Character:
                    Custom.DrawCharacterTab();
                    break;

                case Tab.BikeMaterials:
                    BikeMaterialsLoader.DrawBikeMaterialsTabUI();
                    break;

                case Tab.Drone:
                    Mods.Misc.Update();
                    Toggle("Toggle Drone Body", ref droneBodyToggle);
                    Toggle("Toggle Drone Sound", ref droneEmitterToggle);
                    Slider("Drone Mass", ref droneMass, 2f, 1000f);
                    break;

                case Tab.Misc:
                    Mods.Misc.Update();
                    /*Toggle("Show HUD", ref bShowInstructions);
                    if (InstructionCanvas != null)
                    InstructionCanvas.SetActive(bShowInstructions);*/
                   
                    Toggle("Never Bail", ref bNeverBail);
                    Toggle("Vibration", ref bVibration);
                    if (hapticFeedBack != null)
                        hapticFeedBack.SetActive(bVibration);

                    GUILayout.Box("", coloredBoxStyle, GUILayout.Height(5), GUILayout.ExpandWidth(true));

                    Toggle("Hide Helmet", ref bHideHelmet);
                    Slider("Slo Motion Timer", ref sloMoTimer, 1.0f, 10.0f);

                    GUILayout.Box("", coloredBoxStyle, GUILayout.Height(5), GUILayout.ExpandWidth(true));
                    Slider("Menu Color R", ref menuAccentR, 0f, 1f);
                    Slider("Menu Color G", ref menuAccentG, 0f, 1f);
                    Slider("Menu Color B", ref menuAccentB, 0f, 1f);


                    if (GUILayout.Button("<b>Set Menu Color</b>", highQualityButtonStyle))
                    {
                        InitializeStyles();
                    }

                    break;

                //case Tab.Dropper:
                //    GUILayout.Label("Object Dropper / Transform Manipulation", labelStyle);
                //    break;
                /*
                                case Tab.Camera:
                                    Mods.Camera.Update();
                                    GUILayout.Box("Old Camera", coloredBoxStyle, GUILayout.Height(30), GUILayout.ExpandWidth(true));

                                    Toggle("Use Old Camera", ref bUseOldCam);
                                    if (bUseOldCam)
                                    {
                                        Slider("Camera Rotation Speed", ref camLerp, 0f, 5f);
                                        Slider("Field Of View", ref fovValue, 1f, 360f);
                                        Slider("Camera Offset X", ref camOffset.x, -10f, 10f);
                                        Slider("Camera Offset Y", ref camOffset.y, -10f, 10f);
                                        Slider("Camera Offset Z", ref camOffset.z, -30f, 30f);
                                    }


                                   GUILayout.Box("First Person Cam [WIP]", coloredBoxStyle, GUILayout.Height(30), GUILayout.ExpandWidth(true));
                                   Toggle("First Person Camera", ref bFpvCamera);
                                   if (bFpvCamera)
                                   {
                                       Slider("First Person X", ref fpvOffset.x, -2f, 2f);
                                       Slider("First Person Y", ref fpvOffset.y, -2f, 2f);
                                       Slider("First Person Z", ref fpvOffset.z, -2f, 2f);
                                       Slider("First Person Rotation X", ref fpvRotation.x, -180f, 180f);
                                       //Slider("First Person Rotation Y", ref fpvRotation.y, -180f, 180f);
                                       Slider("First Person Rotation Z", ref fpvRotation.z, -180f, 180f);
                                   }
                                   GUILayout.Box("Third Person Cam", coloredBoxStyle, GUILayout.Height(30), GUILayout.ExpandWidth(true));
                                   Slider("Cam FOV", ref tpFovValue, 10f, 120f);
                                   Slider("Camera AutoCenter Speed",ref tpRecenterSpeed, 0f, 1000f);
                                   Slider("Camera Pitch", ref tpCameraPitch, -25f, 25f);
                                   Slider("Camera Offset X", ref tpCameraOffset.x, -1f,1f);
                                   Slider("Camera Offset Y", ref tpCameraOffset.y, -1f, 1f);
                                   Slider("Camera Offset Z", ref tpCameraOffset.z, -6f, 0f);

                break;
                */
                case Tab.Graphics:
                    try
                    {
                        DrawGraphicsSettings();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"An error occurred in Graphics Tab: {ex.Message}");
                    }

                    break;
                case Tab.Marker:
                    GUILayout.Box("Session Markers", coloredBoxStyle, GUILayout.Height(25),
                        GUILayout.ExpandWidth(true));

                    foreach (GameObject marker in sessionMarkers)
                    {
                        if (marker != null && GUILayout.Button(marker.name, highQualityButtonStyle))
                        {
                            ReplaceSessionMarkerWithPrefab(marker);
                            Config.customSessionMarker = marker.name;
                        }
                    }

                    GUILayout.Box("Current Selected Marker: " + (Config.customSessionMarker ?? "None"), labelStyle);
                    break;
            }
        }


//-------------------------------------------------------------------
// SCROLL & TABS
//-------------------------------------------------------------------

        static void DrawScrollableContent()
        {
            HandleScrolling();

            float titleBarHeight = 25f;
            float tabsHeight = 35f;
            float spacingAfterTabs = 10f;

            float visibleAreaY = titleBarHeight + tabsHeight + spacingAfterTabs;
            float visibleAreaHeight = windowRect.height - visibleAreaY - 20f;

            viewHeight = visibleAreaHeight;

            Rect visibleArea = new Rect(0, visibleAreaY, windowRect.width, visibleAreaHeight);

            GUI.BeginGroup(visibleArea);
            GUILayout.BeginArea(new Rect(0, -scrollOffset, windowRect.width, scrollViewHeight));

            GUILayout.BeginHorizontal();
            GUILayout.Space(15); // Left padding
            GUILayout.BeginVertical();

            DrawTabContent();

            GUILayout.EndVertical();
            GUILayout.Space(15); // Right padding
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
            GUI.EndGroup();
        }

        static void DrawTabs()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<b>Physics</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.Physics);
            }

            if (GUILayout.Button("<b>Tricks</b>", highQualityButtonStyle)) { SetCurrentTab(Tab.Tricks); }
//if (GUILayout.Button("<b>Bike</b>", highQualityButtonStyle)) { SetCurrentTab(Tab.Bike); }
            if (GUILayout.Button("<b>Character</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.Character);
            }

            if (GUILayout.Button("<b>BikeMaterials</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.BikeMaterials);
            }

            if (GUILayout.Button("<b>Drone</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.Drone);
            }

            if (GUILayout.Button("<b>Misc</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.Misc);
            }

            if (GUILayout.Button("<b>Camera</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.Camera);
            }

            if (GUILayout.Button("<b>Graphics</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.Graphics);
            }

            if (GUILayout.Button("<b>Marker</b>", highQualityButtonStyle))
            {
                SetCurrentTab(Tab.Marker);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(50);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<b>Snap</b>", highQualityButtonStyle, GUILayout.Width(80)))
            {
                if (customizableEntity != null)
                    customizableEntity.RelaySnap();
                Log.Msg("RelaySnap() called...");
            }

            GUILayout.EndHorizontal();
            if (GUILayout.Button("<b>RESET</b>", highQualityButtonStyle, GUILayout.Width(80)))
            {
                switch (currentTab)
                {
                    case Tab.Physics:
                        ResetPhysicsTab();
                        break;
                    case Tab.Character:
                        ResetCharacterTab();
                        break;
                    case Tab.Bike:
                        ResetBikeTab();
                        break;
                    case Tab.BikeMaterials:
                        ResetBikeMaterialsTab();
                        break;
                    case Tab.Misc:
                        ResetMiscTab();
                        break;
                    case Tab.Camera:
                        ResetCameraTab();
                        break;
                    case Tab.Graphics:
                        ResetGraphicsTab();
                        break;
                }

                ResetSliderTextValues();
            }


            GUILayout.EndHorizontal();
        }

        public static void HandleScrolling()
        {
            if (Event.current.type == EventType.ScrollWheel)
            {
                float scrollAmount = Event.current.delta.y * 10f;
                scrollOffset += scrollAmount;
                Event.current.Use();
            }

            scrollOffset = Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, scrollViewHeight - viewHeight));
        }

//-------------------------------------------------------------------
// TAB SWITCHING
//-------------------------------------------------------------------

        static void SetCurrentTab(Tab newTab)
        {
            if (currentTab != newTab)
            {
                scrollOffset = 0;
                previousTab = currentTab;
                currentTab = newTab;
            }
        }

//-------------------------------------------------------------------
// RESET SLIDER TEXT VALUES
//-------------------------------------------------------------------

        public static void ResetSliderTextValues()
        {
            sliderTextValues.Clear();
            sliderFieldJustFocused.Clear();

// Physics Tab Sliders
            sliderTextValues["Gravity"] = gravity.ToString("F2");
            //sliderTextValues["Hop Force"] = hopForce.ToString("F2");
            sliderTextValues["Small Hop Force"] = smallHopForce.ToString("F2");
            sliderTextValues["Side Hop Force"] = sideHopPower.ToString("F2");
            sliderTextValues["Pump Force"] = pumpForce.ToString("F2");
            sliderTextValues["Spin Speed Multiplier"] = spinTorque.ToString("F2");
            sliderTextValues["Steer Damping"] = steerDamp.ToString("F2");
            sliderTextValues["Quick Spin Multiplier"] = quickSpinMultiplier.ToString("F2");
            sliderTextValues["Max Nose Manual Angle"] = noseManualAngle.ToString("F2");
            sliderTextValues["Max Manual Angle"] = manualAngle.ToString("F2");

// Misc Tab Sliders
            sliderTextValues["Slo Motion Timer"] = sloMoTimer.ToString("F2");
            sliderTextValues["Drone Mass"] = droneMass.ToString("F2");
            sliderTextValues["Menu Color R"] = menuAccentR.ToString("F2");
            sliderTextValues["Menu Color G"] = menuAccentG.ToString("F2");
            sliderTextValues["Menu Color B"] = menuAccentB.ToString("F2");

// Camera Tab Sliders
            sliderTextValues["Camera Rotation Speed"] = camLerp.ToString("F2");
            sliderTextValues["Field Of View"] = fovValue.ToString("F2");
            sliderTextValues["Camera Offset X"] = camOffset.x.ToString("F2");
            sliderTextValues["Camera Offset Y"] = camOffset.y.ToString("F2");
            sliderTextValues["Camera Offset Z"] = camOffset.z.ToString("F2");

//First Person
            sliderTextValues["First Person X"] = fpvOffset.x.ToString("F2");
            sliderTextValues["First Person Y"] = fpvOffset.y.ToString("F2");
            sliderTextValues["First Person Z"] = fpvOffset.z.ToString("F2");
            sliderTextValues["First Person Rotation X"] = fpvRotation.x.ToString("F2");
            sliderTextValues["First Person Rotation Y"] = fpvRotation.y.ToString("F2");
            sliderTextValues["First Person Rotation Z"] = fpvRotation.z.ToString("F2");

//Third Person
            sliderTextValues["Third Person Cam FOV"] = tpFovValue.ToString("F2");
            sliderTextValues["Camera AutoCenter Speed"] = tpRecenterSpeed.ToString("F2");
            sliderTextValues["Camera Pitch"] = tpCameraPitch.ToString("F2");
            sliderTextValues["Camera Offset X"] = tpCameraOffset.x.ToString("F2");
            sliderTextValues["Camera Offset Y"] = tpCameraOffset.y.ToString("F2");
            sliderTextValues["Camera Offset Z"] = tpCameraOffset.z.ToString("F2");
        }

//-------------------------------------------------------------------
// STYLES
//-------------------------------------------------------------------

        public static void InitializeStyles()
        {
// Window Style
            windowStyle = new GUIStyle(GUI.skin.window);
            Texture2D backgroundTexture = MakeTex(2, 2, new Color(0, 0, 0, 0.9f));
            Texture2D backgroundTextureSelected = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f));

// Define colors
            Color accentBaseColor = new Color(menuAccentR, menuAccentG, menuAccentB);
            Color accentHoverBaseColor = accentBaseColor * new Color(1.35f, 1.35f, 1.35f);

// Create a rounded button texture
            Texture2D roundedButtonNormal = MakeRoundedTex(20, 40, accentBaseColor, 10);
            Texture2D roundedButtonHover = MakeRoundedTex(20, 40, accentHoverBaseColor, 10);

            Texture2D accentColor = MakeTex(2, 2, accentBaseColor);
            Texture2D accentHoverColor = MakeTex(2, 2, accentHoverBaseColor);
            coloredBoxStyle = new GUIStyle(GUI.skin.box);
            coloredBoxStyle.normal.background = accentColor; // RGBA color
// Set background for all states
            windowStyle.normal.background = backgroundTexture;
            windowStyle.onNormal.background = backgroundTexture;
            windowStyle.hover.background = backgroundTexture;
            windowStyle.active.background = backgroundTextureSelected;
            windowStyle.focused.background = backgroundTexture;
            windowStyle.onFocused.background = backgroundTexture;
            windowStyle.onHover.background = backgroundTexture;
            windowStyle.onActive.background = backgroundTexture;

// Set text color for all states
            windowStyle.normal.textColor = Color.white;
            windowStyle.onNormal.textColor = Color.white;
            windowStyle.hover.textColor = Color.white;
            windowStyle.active.textColor = Color.white;
            windowStyle.focused.textColor = Color.white;
            windowStyle.onFocused.textColor = Color.white;
            windowStyle.onHover.textColor = Color.white;
            windowStyle.onActive.textColor = Color.white;

            windowStyle.fontSize = 16;
            windowStyle.fontStyle = FontStyle.Bold;

// Button Style
/*buttonStyle = new GUIStyle(GUI.skin.button);
           buttonStyle.normal.background = roundedButtonNormal;
           buttonStyle.hover.background = roundedButtonHover;
           buttonStyle.active.background = roundedButtonHover;
           buttonStyle.normal.textColor = Color.white;
           buttonStyle.hover.textColor = Color.yellow;
           buttonStyle.active.textColor = Color.green;
           buttonStyle.fontSize = 14;
           buttonStyle.fontStyle = FontStyle.Bold;
           buttonStyle.alignment = TextAnchor.MiddleCenter;*/

// Set border values for 9-slice scaling


// Label Style
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;

// Toggle Style
            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = Color.white;
            toggleStyle.onNormal.textColor = Color.white;
            toggleStyle.hover.textColor = Color.white;
            toggleStyle.fontSize = 12;

// Horizontal Slider Style
            horizontalSliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            horizontalSliderStyle.normal.background = backgroundTextureSelected;
            horizontalSliderStyle.hover.background = backgroundTextureSelected;
            horizontalSliderStyle.active.background = backgroundTextureSelected;
            horizontalSliderStyle.fixedHeight = 15;

// Horizontal Slider Thumb Style
            horizontalSliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
            horizontalSliderThumbStyle.normal.background = accentColor;
            horizontalSliderThumbStyle.hover.background = accentHoverColor;
            horizontalSliderThumbStyle.active.background = accentHoverColor;
            horizontalSliderThumbStyle.fixedWidth = 15;
            horizontalSliderThumbStyle.fixedHeight = 20;

// High-Quality Button Style
            highQualityButtonStyle = new GUIStyle(GUI.skin.button);
            highQualityButtonStyle.normal.background = roundedButtonNormal;
            highQualityButtonStyle.hover.background = roundedButtonHover;
            highQualityButtonStyle.active.background = roundedButtonHover;

            highQualityButtonStyle.normal.textColor = Color.white;
            highQualityButtonStyle.hover.textColor = Color.yellow;
            highQualityButtonStyle.active.textColor = Color.green;

            highQualityButtonStyle.fontSize = 16;
            highQualityButtonStyle.fontStyle = FontStyle.Bold;
            highQualityButtonStyle.alignment = TextAnchor.MiddleCenter;

            highQualityButtonStyle.border = new RectOffset(10, 10, 10, 10); // Adjust the values to match the rounding
        }

        public static void UpdateButtonStyle()
        {
            Color accentBaseColor = new Color(menuAccentR, menuAccentG, menuAccentB);
        }

        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public static Texture2D MakeRoundedTex(int width, int height, Color color, int radius)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] colors = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool insideCorner =
                        (x < radius && y < radius &&
                         Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) > radius) ||
                        (x > width - radius - 1 && y < radius &&
                         Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius)) > radius) ||
                        (x < radius && y > height - radius - 1 &&
                         Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1)) > radius) ||
                        (x > width - radius - 1 && y > height - radius - 1 && Vector2.Distance(new Vector2(x, y),
                            new Vector2(width - radius - 1, height - radius - 1)) > radius);

                    colors[y * width + x] = insideCorner ? new Color(0, 0, 0, 0) : color;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }
//-------------------------------------------------------------------
// GRAPHICS
//-------------------------------------------------------------------

        public static Resolution[] availableResolutions;
        public static int selectedResolutionIndex;
        public static bool isFullScreen;
        public static int selectedQualityIndex;

// New Variable to Store VSync State
        private static bool isVSyncEnabled = QualitySettings.vSyncCount > 0;

        public static void ApplyGraphicsSettings()
        {
            Resolution res = availableResolutions[selectedResolutionIndex];
            Screen.SetResolution(res.width, res.height, isFullScreen);
            QualitySettings.vSyncCount = isVSyncEnabled ? 1 : 0;
            QualitySettings.SetQualityLevel(selectedQualityIndex);
        }

        public static void DrawGraphicsSettings()
        {
            try
            {
/*GUILayout.Label("<b>Graphics Settings</b>", labelStyle);

               // Resolution
               GUILayout.Label("Resolution:", labelStyle);
               GUILayout.BeginHorizontal();
               if (GUILayout.Button("Previous Resolution", highQualityButtonStyle))
               {
                   selectedResolutionIndex--;
                   if (selectedResolutionIndex < 0)
                       selectedResolutionIndex = availableResolutions.Length - 1;
               }
               GUILayout.Label($"{availableResolutions[selectedResolutionIndex].width} x {availableResolutions[selectedResolutionIndex].height}", labelStyle);
               if (GUILayout.Button("Next Resolution", highQualityButtonStyle))
               {
                   selectedResolutionIndex++;
                   if (selectedResolutionIndex >= availableResolutions.Length)
                       selectedResolutionIndex = 0;
               }
               GUILayout.EndHorizontal();

               // Fullscreen Toggle
               isFullScreen = GUILayout.Toggle(isFullScreen, "Fullscreen", toggleStyle);
               // VSync Toggle
               isVSyncEnabled = GUILayout.Toggle(isVSyncEnabled, "VSync", toggleStyle);

               // Quality Level
               GUILayout.Label("Quality Level:", labelStyle);
               GUILayout.BeginHorizontal();
               if (GUILayout.Button("Decrease Quality", highQualityButtonStyle))
               {
                   selectedQualityIndex--;
                   if (selectedQualityIndex < 0)
                       selectedQualityIndex = QualitySettings.names.Length - 1;
               }
               GUILayout.Label(QualitySettings.names[selectedQualityIndex], labelStyle);
               if (GUILayout.Button("Increase Quality", highQualityButtonStyle))
               {
                   selectedQualityIndex++;
                   if (selectedQualityIndex >= QualitySettings.names.Length)
                       selectedQualityIndex = 0;
               }
               GUILayout.EndHorizontal();

               // Apply Button
               if (GUILayout.Button("Apply", highQualityButtonStyle))
               {
                   ApplyGraphicsSettings();
               }

               // TAA Quality Settings
               GUILayout.Label("Anti-Aliasing:", labelStyle);
               string[] taaOptions = { "None", "FXAA", "TAA", "SMAA" };

               for (int i = 0; i < taaOptions.Length; i++)
               {
                   if (GUILayout.Button(taaOptions[i], highQualityButtonStyle))
                   {
                       if (selectedTAAIndex != i)
                       {
                           selectedTAAIndex = i;
                           ApplyGlobalGraphicsSettings(selectedTAAIndex);
                       }
                   }
               }*/

// Display cached volume settings
                GUILayout.Label("<b>Volume Settings</b>", labelStyle);
                foreach (var volume in cachedVolumes)
                {
                    GUILayout.Label($"Volume: {volume.name}", labelStyle);
                    foreach (var component in volume.profile.components)
                    {
                        bool isActive = component.active;
                        bool newIsActive = GUILayout.Toggle(isActive, component.name, toggleStyle);
                        if (newIsActive != isActive)
                        {
                            component.active = newIsActive;
                        }
                    }
                }

                GUILayout.Box("", highQualityButtonStyle, GUILayout.Height(5),
                    GUILayout.ExpandWidth(true)); // Separator
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while rendering the graphics settings: {ex}");
            }
        }


        public static void ApplyGlobalGraphicsSettings(int taaQualityIndex)
        {
            if (cachedHDRCameras == null || cachedHDRCameras.Count == 0) return;

            foreach (var hdrpCamera in cachedHDRCameras)
            {
                switch (taaQualityIndex)
                {
                    case 0:
                        hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData
                            .AntialiasingMode.None;
                        break;
                    case 1:
                        hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData
                            .AntialiasingMode.FastApproximateAntialiasing;
                        break;
                    case 2:
                        hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData
                            .AntialiasingMode.TemporalAntialiasing;
                        break;
                    case 3:
                        hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData
                            .AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        break;
                }
            }
        }
//-------------------------------------------------------------------
// SLIDER & GUI METHODS
//-------------------------------------------------------------------

        public static void Slider(string label, ref float target, float min, float max, float sliderWidth = 400f,
            float defaultValue = 1f)
        {
// Unchanged slider GUI setup
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} = {target}", labelStyle);

            float newSliderValue = GUILayout.HorizontalSlider(target, min, max,
                horizontalSliderStyle, horizontalSliderThumbStyle,
                GUILayout.Width(sliderWidth));

            string controlName = "SliderTextField_" + label;
            GUI.SetNextControlName(controlName);


            if (!sliderTextValues.ContainsKey(label))
                sliderTextValues[label] = target.ToString("F2");
            if (!sliderFieldJustFocused.ContainsKey(label))
                sliderFieldJustFocused[label] = false;

            string currentText = sliderTextValues[label];
            string newText = GUILayout.TextField(currentText, GUILayout.Width(50));

            bool isFocused = (GUI.GetNameOfFocusedControl() == controlName);

            if (isFocused && !sliderFieldJustFocused[label])
            {
                newText = "";
                sliderFieldJustFocused[label] = true;
            }

            if (isFocused && (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed))
            {
                if (float.TryParse(newText, out float parsedValue))
                {
                    parsedValue = Mathf.Clamp(parsedValue, min, max);
                    target = parsedValue;
                    sliderTextValues[label] = parsedValue.ToString("F2");
                }
                else
                {
                    sliderTextValues[label] = target.ToString("F2");
                }

                GUI.FocusControl(null);
                sliderFieldJustFocused[label] = false;
            }
            else
            {
                sliderTextValues[label] = newText;
            }

            GUILayout.EndHorizontal();

            if (!isFocused && !Mathf.Approximately(target, newSliderValue))
            {
                target = newSliderValue;
                sliderTextValues[label] = target.ToString("F2");
            }
        }


        public static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Il2CppSystem.Object childObj in parent) // Using Il2CppSystem.Object
            {
                Transform child = childObj.TryCast<Transform>(); // Safe cast

                if (child != null && child.name == name)
                    return child;

                Transform found = FindDeepChild(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        public static bool Toggle(string text, ref bool value)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(value ? "✔" : " ", highQualityButtonStyle, GUILayout.Width(20)))
            {
                value = !value;
            }

            GUILayout.Label($"{text}");
            GUILayout.EndHorizontal();
            return value;
        }
    }
}