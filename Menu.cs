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
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Character.Scripts;
using Il2CppPlayFab.ClientModels;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;

namespace rowemod
{
    public static class Menu
    {
        // Defining the tabs available in the menu
        public enum Tab
        {
            Physics,
            Bike,
            Tricks,
            Character,
            BikeMaterials,
            MX,
            Misc,
            Graphics,
            Marker,
            Dropper,
            Challenge // Added Challenge tab for future use
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
        
        private static bool isResizing = false;
        private static Vector2 resizeStartMouse;
        private static Vector2 resizeStartSize;
        private static readonly float resizeHandleSize = 20f;

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
        // Texture caches for styles
        private static Texture2D backgroundTexture;
        private static Texture2D roundedButtonNormal;
        private static Texture2D roundedButtonHover;
        private static Texture2D activeTabBackground;
        private static Texture2D accentColorTexture;
        private static Texture2D sliderBackground;
        private static Texture2D sliderFill;
        private static Texture2D sliderThumb;
        private static Texture2D toggleBackground;
        
        public static GUIStyle textFieldStyle;
        public static bool stylesInitialized = false;

        // Dictionaries and caches
        public static Dictionary<Slot, GameObject> slotGameObjects = new Dictionary<Slot, GameObject>();
        public static List<UnityEngine.Camera> cachedCameras = new List<UnityEngine.Camera>();
        public static List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData> cachedHDRCameras = new List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        public static List<UnityEngine.Rendering.Volume> cachedVolumes = new List<UnityEngine.Rendering.Volume>();
        private static Texture2D logoTexture;
        private static float mxTopSpeed = 100f;
        private static float mxAcceleration = 10f;
        private static bool hasInitializedMxSettings = false;
        private static float mxChassisMass = 50f;
        private static float mxAccelerationForce = 10f;
        private static float mxTerminalVelocity = 80f;
        // Cache for circular knob texture
        private static Texture2D _circleTex;
        
        
        private static string fovInput = "60";
        private static float fovInputValue = 60f;


        //-------------------------------------------------------------------
        // MENU & TAB LOGIC
        //-------------------------------------------------------------------

        public static void DrawMenu(int windowID)
        {
            try
            {
                if (logoTexture != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.1f); // 10% opacity
                    GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), logoTexture, ScaleMode.ScaleAndCrop, true);
                    GUI.color = Color.white; // Reset alpha
                }
                
                GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));
                GUILayout.BeginVertical();
                GUILayout.Space(5);
                DrawTabs();
                GUILayout.Space(5);
                DrawScrollableContent();
                GUILayout.EndVertical();
                
                // Resize handle area (bottom-right corner)
                Rect resizeRect = new Rect(windowRect.width - resizeHandleSize, windowRect.height - resizeHandleSize, resizeHandleSize, resizeHandleSize);
                GUI.DrawTexture(resizeRect, MakeTex(1, 1, new Color(1f, 1f, 1f, 0.2f))); // Optional: visible handle


                Event e = Event.current;
                if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition))
                {
                    isResizing = true;
                    resizeStartMouse = e.mousePosition;
                    resizeStartSize = new Vector2(windowRect.width, windowRect.height);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && isResizing)
                {
                    isResizing = false;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && isResizing)
                {
                    Vector2 delta = e.mousePosition - resizeStartMouse;
                    windowRect.width = Mathf.Max(600f, resizeStartSize.x + delta.x); // Clamp min size
                    windowRect.height = Mathf.Max(400f, resizeStartSize.y + delta.y);
                    e.Use();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawMenu: {ex.Message}");
            }
        }

        private static void DrawTabContent()
        {
            try
            {
                switch (currentTab)
                {
                    case Tab.Physics:
                        Mods.Physics.Update();
                        GUILayout.Box("Physics", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        ModernToggle("Spin Assist", ref physics.spinAssist);
                        ModernToggle("Drifting", ref physics.driftAbility);
                        Slider("Gravity", ref physics.gravity,12.5f, 0f, 30f);
                        Slider("Small Hop Force", ref physics.smallHopForce,4.2f, 0f, 25f);
                        GUILayout.Box("Speed", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        Slider("Push Force", ref physics.bmxForceFactor,0.07f, 0.05f, 2f);
                        Slider("Max Speed",ref physics.bmxMaxSpeed,7f, 2f, 15f);
                        GUILayout.Box("Pump/Spin", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        Slider("Pump Force", ref physics.pumpForce,1.0f, 1f, 5f);
                        Slider("Spin Speed Multiplier", ref physics.spinMultiplier,1.0f, 0f, 10f);
                        Slider("Steer Damping", ref physics.steerDamp,5f, 1f, 5f);
                        GUILayout.Box("Manuals", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        Slider("Max Nose Manual Angle", ref physics.noseManualAngle, 30f, 10f, 50f);
                        Slider("Max Manual Angle", ref physics.manualAngle,30f, 10f, 50f);
                        break;
                    case Tab.Bike:
                        PartTweaker.DrawPartTweaker();


                        PartTweaker.DrawPartSelectorUI();
                        break;
                    case Tab.Tricks:
                        TrickMods.DrawTrickMenu();
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
                            GUILayout.Box("MX Vehicle Tuning", coloredBoxStyle);

                            GUILayout.Space(10);
                            ModernSlider("Speed", ref mxTopSpeed, 5f, 30000f);
                            ModernSlider("Chassis Mass", ref mxChassisMass, 1f, 500f);
                            ModernSlider("Acceleration Force", ref mxAccelerationForce, 1f, 1000f);
                            ModernSlider("Top speed", ref mxTerminalVelocity, 10f, 3000f);

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
                        GUILayout.Box("Drone", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        ModernToggle("Toggle Drone Body", ref misc.droneBodyToggle);
                        ModernToggle("Toggle Drone Sound", ref misc.droneEmitterToggle);
                        ModernToggle("Toggle Drone Colliders", ref misc.disableDroneCollider);
                        Slider("Drone Mass", ref misc.droneMass, 5f, 2f, 1000f);
                        GUILayout.Box("Other", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        ModernToggle("No Bail", ref misc.neverBail);
                        ModernToggle("Disable Replay Cam Collider", ref misc.disableFreeCamCollider);
                        ModernSlider("Menu Color R", ref misc.menuAccentR,  0f, 1f);
                        ModernSlider("Menu Color G", ref misc.menuAccentG, 0f, 1f);
                        ModernSlider("Menu Color B", ref misc.menuAccentB, 0f, 1f);
                        if (GUILayout.Button("<b>Set Menu Color</b>", highQualityButtonStyle))
                        {
                            stylesInitialized = false;
                        }
                        break;

                    case Tab.Graphics:
                        DrawGraphicsSettings();
                        break;

                    case Tab.Marker:
                        GUILayout.Box("Session Markers", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        foreach (GameObject marker in sessionMarkers.Where(m => m != null))
                        {
                            if (GUILayout.Button(marker.name, highQualityButtonStyle))
                            {
                                ReplaceSessionMarkerWithPrefab(marker);
                                Config.misc.customSessionMarker = marker.name;
                            }
                        }
                        GUILayout.Box("Current Selected Marker: " + (Config.misc.customSessionMarker ?? "None"), labelStyle);
                        break;
                    // Adding case for the new Dropper tab
                    case Tab.Dropper:
                        ObjectDropper.DrawDropperTab();
                        break;
                    
                    /*case Tab.Challenge:
                        GUILayout.Box("8Bitt Challenge", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        GUILayout.Label("This tab is reserved for future challenges.", labelStyle);
                        // Placeholder for future challenge content
                        break;*/
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

        private static void HandleScrolling()
        {
            try
            {
                if (Event.current?.type == EventType.ScrollWheel)
                {
                    scrollOffset += Event.current.delta.y * 10f;
                    scrollOffset = Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, scrollViewHeight - viewHeight));
                    Event.current.Use();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in HandleScrolling: {ex.Message}");
            }
        }

        private static void DrawScrollableContent()
        {
            try
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
                GUILayout.Space(15);
                GUILayout.BeginVertical();
                DrawTabContent();
                GUILayout.EndVertical();
                GUILayout.Space(15);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                GUI.EndGroup();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawScrollableContent: {ex.Message}");
            }
        }

        private static void DrawTabs()
        {

            // Begin a group to position all tabs precisely, increased height to prevent clipping
            GUI.BeginGroup(new Rect(0, 0, windowRect.width, 60f));

            float tabHeight = 30f;
            float yPosition = 30f;

            


            // Define tab labels and corresponding enum values
            (string label, Tab tab)[] tabs = new[]
            {
                ("Physics", Tab.Physics),
                ("Tricks", Tab.Tricks),
                ("Bike", Tab.Bike),
                ("Materials", Tab.BikeMaterials),
                //("MX", Tab.MX),
                ("Character", Tab.Character),
                ("Misc", Tab.Misc),
                ("Graphics", Tab.Graphics),
                ("Marker", Tab.Marker),
                ("Dropper", Tab.Dropper),
                //("Challenge", Tab.Challenge) // Added Challenge tab
            };
            
            float totalPadding = 10f * (tabs.Length + 1); // 10px padding between buttons and edges
            float dynamicTabWidth = (windowRect.width - totalPadding - 90f); // leave room for reset button
            dynamicTabWidth = dynamicTabWidth / tabs.Length;
            
            
            // Draw tab buttons with precise Rect positioning
            for (int i = 0; i < tabs.Length; i++)
            {
                var (label, tab) = tabs[i];
                GUIStyle buttonStyle = currentTab == tab ? activeTabButtonStyle : highQualityButtonStyle;

                float xPos = 10f + i * (dynamicTabWidth + 10f);
                Rect tabRect = new Rect(xPos, yPosition, dynamicTabWidth, tabHeight);

                if (GUI.Button(tabRect, $"<b>{label}</b>", buttonStyle))
                {
                    SetCurrentTab(tab);
                }
            }


            // Draw "Reset Tab" button in top-right corner
            float resetButtonWidth = 80f;
            float resetButtonHeight = 30f;
            Rect resetButtonRect = new Rect(
                windowRect.width - 80f - 10f, // 80f width, 10px margin
                yPosition,
                80f,
                tabHeight
            );
            if (GUI.Button(resetButtonRect, "<b>RESET\nTAB</b>", redButtonStyle))
            {
                switch (currentTab)
                {
                    case Tab.Physics:
                        ResetPhysicsTab();
                        break;

                    case Tab.Bike:
                        //LoadAllAssetBundles();
                        Config.ResetBikeTab();
                        PartTweaker.FindParts();
                        ReloadAssetsFromCachedBundles();
                        customizableEntity.EquipItems();
                        customizableEntity.EquipItems();
                        Log.Msg("Bike Tab reset!");
                        break;

                    case Tab.Character:
                        ResetCharacterTab();
                        break;

                    case Tab.BikeMaterials:
                        CategorizeEquipSlots(equipSlotVehicles);
                        ResetBikeMaterialsTab();
                        break;
                    case Tab.MX:
                        hasInitializedMxSettings = false;
                        break;
                    case Tab.Misc:
                        ResetMiscTab();
                        break;

                    case Tab.Marker:
                        ReloadAssetsFromCachedBundles();
                        break;

                    case Tab.Dropper:
                        ObjectDropper.ResetTab();
                        Log.Msg("Dropper Tab reset!");
                        break;
                }

                ResetSliderUI();
            }

            GUI.EndGroup();
        }

        //-------------------------------------------------------------------
        // TAB SWITCHING
        //-------------------------------------------------------------------

        private static void SetCurrentTab(Tab newTab)
        {
            if (currentTab != newTab)
            {
                scrollOffset = 0;
                currentTab = newTab;
                InitializeStyles(); // Refresh styles to update active tab highlight
            }
        }


        //-------------------------------------------------------------------
        // STYLES
        //-------------------------------------------------------------------

        public static void InitializeStyles()
        {
            try
            {
                // Removed stylesInitialized check to allow style updates
                stylesInitialized = true;

                // Define base accent color from Config
                Color accentBaseColor = new Color(misc.menuAccentR, misc.menuAccentG, misc.menuAccentB);
                // Derive hover color (brighter: increase RGB by 0.1, clamp to 1)
                Color accentHoverBaseColor = new Color(
                    Mathf.Min(accentBaseColor.r + 0.1f, 1f),
                    Mathf.Min(accentBaseColor.g + 0.1f, 1f),
                    Mathf.Min(accentBaseColor.b + 0.1f, 1f)
                );
                // Derive active tab color (even brighter: increase by 0.2)
                Color activeTabColor = new Color(
                    Mathf.Min(accentBaseColor.r + 0.2f, 1f),
                    Mathf.Min(accentBaseColor.g + 0.2f, 1f),
                    Mathf.Min(accentBaseColor.b + 0.2f, 1f)
                );

                // Create textures for styles
                Texture2D backgroundTexture = MakeRoundedTex(900, 800, new Color(0, 0, 0, 0.9f), 10, 4, accentBaseColor);
                Texture2D backgroundTextureSelected = MakeTex(2, 2, new Color(1f, 1f, 1f, 1f));
                Texture2D roundedButtonNormal = MakeRoundedTex(20, 40, accentBaseColor, 10, 1, Color.black);
                Texture2D roundedButtonHover = MakeRoundedTex(20, 40, accentHoverBaseColor, 10, 1, Color.black);
                Texture2D activeTabBackground = MakeRoundedTex(20, 40, activeTabColor, 10, 1, Color.black);
                Texture2D accentColor = MakeTex(2, 2, accentBaseColor);

                // Window Style
                windowStyle = new GUIStyle(GUI.skin.window);
                windowStyle.normal.background = backgroundTexture;
                windowStyle.onNormal.background = backgroundTexture;
                windowStyle.hover.background = backgroundTexture;
                windowStyle.hover.textColor = Color.white;
                windowStyle.active.background = backgroundTextureSelected;
                windowStyle.normal.textColor = Color.white;
                windowStyle.fontSize = 16;
                windowStyle.fontStyle = FontStyle.Bold;

                // Label Styles
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = Color.white;
                labelStyle.hover.textColor = Color.white;
                labelStyle.fontSize = 15;

                // Toggle Style
                toggleStyle = new GUIStyle(GUI.skin.toggle);
                toggleStyle.normal.textColor = Color.white;
                toggleStyle.fontSize = 12;

                // Horizontal Slider Style
                horizontalSliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
                horizontalSliderStyle.normal.background = backgroundTextureSelected;
                horizontalSliderStyle.fixedHeight = 15;

                // Horizontal Slider Thumb Style
                horizontalSliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
                horizontalSliderThumbStyle.normal.background = accentColor;
                horizontalSliderThumbStyle.hover.background = MakeTex(2, 2, accentHoverBaseColor);
                horizontalSliderThumbStyle.fixedWidth = 15;
                horizontalSliderThumbStyle.fixedHeight = 20;

                // Colored Box Style
                coloredBoxStyle = new GUIStyle(GUI.skin.box);
                coloredBoxStyle.normal.background = accentColor;
                coloredBoxStyle.normal.textColor = Color.black;
                coloredBoxStyle.fontSize = 14;
                coloredBoxStyle.fixedWidth = 140;
                coloredBoxStyle.fixedHeight = 24;

                // High-Quality Button Style
                highQualityButtonStyle = new GUIStyle(GUI.skin.button);
                highQualityButtonStyle.normal.background = roundedButtonNormal;
                highQualityButtonStyle.hover.background = roundedButtonHover;
                highQualityButtonStyle.active.background = roundedButtonHover;
                highQualityButtonStyle.normal.textColor = Color.white;
                highQualityButtonStyle.hover.textColor = Color.yellow;
                highQualityButtonStyle.active.textColor = Color.green;
                highQualityButtonStyle.fontSize = 12;
                highQualityButtonStyle.fontStyle = FontStyle.Bold;
                highQualityButtonStyle.alignment = TextAnchor.MiddleCenter;
                highQualityButtonStyle.border = new RectOffset(10, 10, 10, 10);
                
                
                
                

                // Active Tab Button Style
                activeTabButtonStyle = new GUIStyle(highQualityButtonStyle);
                activeTabButtonStyle.normal.background = activeTabBackground;
                activeTabButtonStyle.hover.background = roundedButtonHover; // Same hover as normal
                activeTabButtonStyle.active.background = activeTabBackground;
                activeTabButtonStyle.normal.textColor = Color.white;
                activeTabButtonStyle.hover.textColor = Color.yellow;
                activeTabButtonStyle.active.textColor = Color.green;
                
                // Red Button Style
                redButtonStyle = new GUIStyle(highQualityButtonStyle);
                redButtonStyle.normal.background = MakeRoundedTex(20, 40, new Color(0.8f, 0.2f, 0.2f), 10, 1, Color.black);
                redButtonStyle.hover.background = MakeRoundedTex(20, 40, new Color(1f, 0.3f, 0.3f), 10, 1, Color.black);
                redButtonStyle.active.background = MakeRoundedTex(20, 40, new Color(0.6f, 0.1f, 0.1f), 10, 1, Color.black);
                redButtonStyle.normal.textColor = Color.white;
                redButtonStyle.hover.textColor = Color.yellow;
                redButtonStyle.active.textColor = Color.green;
                
                textFieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = labelStyle.fontSize,
                    font = labelStyle.font,
                    normal = { textColor = Color.white, background = MakeTex(2, 2, Color.black) },
                    focused = { textColor = Color.white, background = MakeTex(2, 2, Color.black) },
                    hover = { textColor = Color.white, background = MakeTex(2, 2, Color.black) },
                    active = { textColor = Color.white, background = MakeTex(2, 2, Color.black) }
                };
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

        public static void DrawGraphicsSettings()
        {
            try
            {
                GUILayout.Label("<b>Volume Settings</b>", labelStyle);
                foreach (var volume in cachedVolumes)
                {
                    if (volume == null || volume.profile == null) continue;
                    GUILayout.Label($"Volume: {volume.name}", labelStyle);
                    foreach (var component in volume.profile.components)
                    {
                        if (component == null) continue;
                        bool isActive = component.active;
                        bool newIsActive = GUILayout.Toggle(isActive, component.name, toggleStyle);
                        if (newIsActive != isActive)
                        {
                            component.active = newIsActive;
                        }
                    }
                }
                GUILayout.Box("", highQualityButtonStyle, GUILayout.Height(5), GUILayout.ExpandWidth(true));
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawGraphicsSettings: {ex.Message}");
            }
        }

        //-------------------------------------------------------------------
        // SLIDER & GUI METHODS
        //-------------------------------------------------------------------

       public static void Slider(string label, ref float target, float defaultVal, float min, float max)
        {
            // Define dimensions for the slider UI
            float height = 25f;
            float labelWidth = 150f;
            float valueBoxWidth = 50f;
            float buttonWidth = 60f; // Width for the Default button
            float spacing = 15f;
            float sliderWidth = windowRect.width - labelWidth - valueBoxWidth - buttonWidth - spacing * 5;
            float thumbWidth = 10f; // Width of the thumb rectangle

            // Create the layout rectangle for the slider
            Rect fullRect = GUILayoutUtility.GetRect(
                windowRect.width - 30f, height,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(height)
            );

            // Define rectangles for label, slider, value box, and default button
            Rect labelRect = new Rect(fullRect.x, fullRect.y, labelWidth, height);
            Rect sliderRect = new Rect(fullRect.x + labelWidth + spacing, fullRect.y + 6, sliderWidth, height - 12f);
            Rect valueRect = new Rect(fullRect.x + labelWidth + spacing + sliderWidth + spacing, fullRect.y, valueBoxWidth, height);
            Rect buttonRect = new Rect(fullRect.x + labelWidth + spacing + sliderWidth + spacing * 2 + valueBoxWidth, fullRect.y, buttonWidth, height);

            // Draw the label
            GUI.Label(labelRect, label, labelStyle);

            // Cache slider textures
            if (sliderBackground == null)
                sliderBackground = MakeTex((int)sliderRect.width, (int)sliderRect.height, new Color(0.25f, 0.25f, 0.25f));
            if (sliderFill == null)
                sliderFill = MakeTex((int)sliderRect.width, (int)sliderRect.height, new Color(misc.menuAccentR, misc.menuAccentG, misc.menuAccentB));
            if (sliderThumb == null)
                sliderThumb = MakeTex((int)thumbWidth, (int)(sliderRect.height + 4f), Color.white);

            GUI.DrawTexture(sliderRect, sliderBackground);

            float percent = Mathf.InverseLerp(min, max, target);
            float fillWidth = percent == 1f ? sliderRect.width : percent * sliderRect.width;
            Rect fillRect = new Rect(sliderRect.x, sliderRect.y, fillWidth, sliderRect.height);
            GUI.DrawTexture(fillRect, sliderFill);

            float thumbX = sliderRect.x + fillWidth;
            Rect thumbRect = new Rect(thumbX - thumbWidth, sliderRect.y - 2f, thumbWidth, sliderRect.height + 4f);
            GUI.DrawTexture(thumbRect, sliderThumb);

            Event e = Event.current;

            if (e.type == EventType.MouseDown && sliderRect.Contains(e.mousePosition))
            {
                _activeSliderLabel = label;
                e.Use();
            }

            if (e.type == EventType.MouseUp && _activeSliderLabel == label)
            {
                _activeSliderLabel = null;
                e.Use();
            }

            if (e.type == EventType.MouseDrag && _activeSliderLabel == label)
            {
                float clampedX = Mathf.Clamp(e.mousePosition.x, sliderRect.x, sliderRect.xMax);
                float newPercent = Mathf.InverseLerp(sliderRect.x, sliderRect.xMax, clampedX);
                float rawValue = Mathf.Lerp(min, max, newPercent);

                target = Mathf.Round(rawValue * 100f) / 100f;
                _sliderTextInputs[label] = target.ToString("0.00");
                e.Use();
            }

            // Draw value box with text input
            float borderSize = 2f;
            Rect borderRect = new Rect(valueRect.x - borderSize, valueRect.y - borderSize, valueRect.width + borderSize * 2, valueRect.height + borderSize * 2);
            DrawSolidColorRect(borderRect, new Color(misc.menuAccentR, misc.menuAccentG, misc.menuAccentB));
            DrawSolidColorRect(valueRect, Color.black);

            // Initialize text input if not set
            if (!_sliderTextInputs.ContainsKey(label))
            {
                _sliderTextInputs[label] = target.ToString("0.00");
            }

            // Set control name for focus tracking
            string controlName = $"SliderTextField_{label}";
            GUI.SetNextControlName(controlName);

            // Draw text field
            string newText = GUI.TextField(valueRect, _sliderTextInputs[label], textFieldStyle);
            _sliderTextInputs[label] = newText;
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
            if (isFocused && (Keyboard.current?.enterKey.wasPressedThisFrame == true || Keyboard.current?.numpadEnterKey.wasPressedThisFrame == true))
            {
                if (float.TryParse(newText, out float parsedValue))
                {
                    // Clamp to min/max
                    target = Mathf.Clamp(parsedValue, min, max);
                    _sliderTextInputs[label] = target.ToString("0.00");
                    Log.Msg($"Updated {label} to {target} via text input.");
                }
                else
                {
                    _sliderTextInputs[label] = target.ToString("0.00");
                    Log.Warning($"Invalid input for {label}: '{newText}'. Reverted to {target}.");
                }
                GUI.FocusControl(null);
            }
            else if (!isFocused && e.type == EventType.MouseDown && !valueRect.Contains(e.mousePosition))
            {
                // On losing focus (click outside), validate and clamp
                if (float.TryParse(newText, out float parsedValue))
                {
                    target = Mathf.Clamp(parsedValue, min, max);
                    _sliderTextInputs[label] = target.ToString("0.00");
                }
                else
                {
                    _sliderTextInputs[label] = target.ToString("0.00");
                }
            }

            // Draw Default button and handle click
            if (GUI.Button(buttonRect, "RESET", highQualityButtonStyle))
            {
                target = defaultVal;
                _sliderTextInputs[label] = target.ToString("0.00");
            }
            GUILayout.Space(10);
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

        public static bool ModernToggle(string label, ref bool value)
        {
            // Define toggle dimensions
            float width = 50f;
            float height = 25f;
            float knobSize = 18f;
            float padding = 3.5f;

            // Create layout rectangle
            Rect fullRect = GUILayoutUtility.GetRect(windowRect.width - 30f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));

            // Define label and toggle rectangles
            Rect labelRect = new Rect(fullRect.x, fullRect.y, fullRect.width - width - 10f, height);
            Rect toggleRect = new Rect(fullRect.x + fullRect.width - width, fullRect.y, width, height);

            // Initialize animation state
            if (!toggleAnimationState.ContainsKey(label))
                toggleAnimationState[label] = value ? 1f : 0f;

            // Handle click events
            if (Event.current.type == EventType.MouseDown && toggleRect.Contains(Event.current.mousePosition))
            {
                value = !value;
                Event.current.Use();
            }

            // Animate toggle
            float target = value ? 1f : 0f;
            toggleAnimationState[label] = Mathf.Lerp(toggleAnimationState[label], target, 0.2f);

            // Draw capsule-shaped background
            Color onColor = new Color(misc.menuAccentR, misc.menuAccentG, misc.menuAccentB); // Use Config accent color
            Color offColor = new Color(0.3f, 0.3f, 0.3f);
            Texture2D toggleTex = MakeCapsuleTex((int)width, (int)height, Color.Lerp(offColor, onColor, toggleAnimationState[label]), 2, Color.black);
            GUI.DrawTexture(toggleRect, toggleTex);

            // Draw circular knob using a circle texture
            float knobX = Mathf.Lerp(toggleRect.x + padding, toggleRect.x + toggleRect.width - knobSize - padding, toggleAnimationState[label]);
            Rect knobRect = new Rect(knobX, toggleRect.y + padding, knobSize, knobSize);
            if (_circleTex == null)
                _circleTex = MakeCircleTex((int)knobSize, Color.white, 1, Color.black);
            GUI.DrawTexture(knobRect, _circleTex);

            // Draw label
            GUI.Label(labelRect, label, labelStyle);
            GUILayout.Space(10); // Add spacing after toggle

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

        private static string _activeSliderLabel = null;
        // Dictionary to store text input for each slider
        private static Dictionary<string, string> _sliderTextInputs = new Dictionary<string, string>();

        public static void ModernSlider(string label, ref float target, float min, float max)
        {
            // Define dimensions for the slider UI
            float height = 25f;
            float labelWidth = 150f;
            float valueBoxWidth = 50f;
            float spacing = 15f;
            float sliderWidth = Menu.windowRect.width - labelWidth - valueBoxWidth - spacing * 4;
            float thumbWidth = 10f; // Width of the thumb rectangle

            // Create the layout rectangle for the slider
            Rect fullRect = GUILayoutUtility.GetRect(
                Menu.windowRect.width - 30f, height,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(height)
            );

            // Define rectangles for label, slider, and value box
            Rect labelRect = new Rect(fullRect.x, fullRect.y, labelWidth, height);
            Rect sliderRect = new Rect(fullRect.x + labelWidth + spacing, fullRect.y + 6, sliderWidth, height - 12f);
            Rect valueRect = new Rect(fullRect.x + labelWidth + spacing + sliderWidth + spacing, fullRect.y, valueBoxWidth, height);

            // Draw the label
            GUI.Label(labelRect, label, Menu.labelStyle);

            // Draw slider background
            DrawSolidColorRect(sliderRect, new Color(0.25f, 0.25f, 0.25f));

            // Calculate fill width to cover the entire track at max position
            float percent = Mathf.InverseLerp(min, max, target);
            float fillWidth = percent == 1f ? sliderRect.width : (percent * sliderRect.width); // Full width at max
            Rect fillRect = new Rect(sliderRect.x, sliderRect.y, fillWidth, sliderRect.height);
            DrawSolidColorRect(fillRect, new Color(misc.menuAccentR, misc.menuAccentG, misc.menuAccentB)); // Use Config accent color

            // Draw thumb, right-aligned to the fill
            float thumbX = sliderRect.x + fillWidth; // Right edge of fill
            Rect thumbRect = new Rect(thumbX - thumbWidth, sliderRect.y - 2f, thumbWidth, sliderRect.height + 4f);
            DrawSolidColorRect(thumbRect, Color.white); // Use white for thumb

            // Handle slider drag events
            Event e = Event.current;

            if (e.type == EventType.MouseDown && sliderRect.Contains(e.mousePosition))
            {
                _activeSliderLabel = label;
                e.Use();
            }

            if (e.type == EventType.MouseUp && _activeSliderLabel == label)
            {
                _activeSliderLabel = null;
                e.Use();
            }

            if (e.type == EventType.MouseDrag && _activeSliderLabel == label)
            {
                float clampedX = Mathf.Clamp(e.mousePosition.x, sliderRect.x, sliderRect.xMax);
                float newPercent = Mathf.InverseLerp(sliderRect.x, sliderRect.xMax, clampedX);
                float rawValue = Mathf.Lerp(min, max, newPercent);

                target = Mathf.Round(rawValue * 100f) / 100f;
                // Update text input to reflect slider change
                _sliderTextInputs[label] = target.ToString("0.00"); // Comment: Sync text field with slider value
                e.Use();
            }

            // Draw value box with text input
            float borderSize = 2f;
            Rect borderRect = new Rect(valueRect.x - borderSize, valueRect.y - borderSize, valueRect.width + borderSize * 2, valueRect.height + borderSize * 2);
            DrawSolidColorRect(borderRect, new Color(misc.menuAccentR, misc.menuAccentG, misc.menuAccentB)); // Use Config accent color
            DrawSolidColorRect(valueRect, Color.black);

            // Initialize text input if not set
            if (!_sliderTextInputs.ContainsKey(label))
            {
                _sliderTextInputs[label] = target.ToString("0.00"); // Comment: Set initial text to current value
            }

            // Set control name for focus tracking
            string controlName = $"SliderTextField_{label}";
            GUI.SetNextControlName(controlName);

            // Draw text field
            string newText = GUI.TextField(valueRect, _sliderTextInputs[label], textFieldStyle);
            _sliderTextInputs[label] = newText; // Comment: Update stored text with user input

            // Handle text input submission
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
            if (isFocused && (Keyboard.current?.enterKey.wasPressedThisFrame == true || Keyboard.current?.numpadEnterKey.wasPressedThisFrame == true))
            {
                if (float.TryParse(newText, out float parsedValue))
                {
                    // Clamp to min/max
                    parsedValue = Mathf.Clamp(parsedValue, min, max);
                    target = parsedValue;
                    _sliderTextInputs[label] = target.ToString("0.00"); // Comment: Update text to clamped value
                    Log.Msg($"Updated {label} to {target} via text input.");
                }
                else
                {
                    _sliderTextInputs[label] = target.ToString("0.00"); // Comment: Revert to last valid value on invalid input
                    Log.Warning($"Invalid input for {label}: '{newText}'. Reverted to {target}.");
                }
                GUI.FocusControl(null); // Comment: Clear focus after submission
            }
            else if (!isFocused && e.type == EventType.MouseDown && !valueRect.Contains(e.mousePosition))
            {
                // On losing focus (click outside), validate and clamp
                if (float.TryParse(newText, out float parsedValue))
                {
                    parsedValue = Mathf.Clamp(parsedValue, min, max);
                    target = parsedValue;
                    _sliderTextInputs[label] = target.ToString("0.00"); // Comment: Update text to clamped value on focus loss
                }
                else
                {
                    _sliderTextInputs[label] = target.ToString("0.00"); // Comment: Revert to last valid value on invalid input
                }
            }
            GUILayout.Space(10); // Add spacing after slider
        }
        public static bool ModernButton(string label, float width = 200f, float height = 30f)
        {
            Rect buttonRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false), GUILayout.Height(height));

            // Detect hover state
            bool isHovering = buttonRect.Contains(Event.current.mousePosition);

            // Colors
            Color baseColor = new Color(misc.menuAccentR, misc.menuAccentG, misc.menuAccentB); // Use Config accent color
            Color hoverColor = new Color(
                Mathf.Min(baseColor.r + 0.1f, 1f),
                Mathf.Min(baseColor.g + 0.1f, 1f),
                Mathf.Min(baseColor.b + 0.1f, 1f)
            );
            Color backgroundColor = isHovering ? hoverColor : baseColor;

            // Draw rounded background
            DrawSolidColorRect(buttonRect, backgroundColor);

            // Draw label
            GUIStyle style = new GUIStyle(labelStyle);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;

            GUI.Label(buttonRect, label, style);

            // Handle click
            if (Event.current.type == EventType.MouseDown && buttonRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        public static IEnumerator LoadRoweLogo()
        {
            string url = "https://github.com/xrowex/RoweModsLogo/raw/main/rowemods.png";
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
            yield return www.SendWebRequest();

            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Log.Error($"Logo download failed: {www.error}");
                }
                else
                {
                    logoTexture = DownloadHandlerTexture.GetContent(www);
                    Log.Msg("RoweMods logo loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in LoadRoweLogo: {ex.Message}");
            }
            finally
            {
                www.Dispose();
            }
        }
        private static void ResetSliderUI()
        {
            _activeSliderLabel = null;
            _sliderTextInputs.Clear();         // clear all slider text caches
            toggleAnimationState.Clear();      // optional: reset toggle animation positions
            GUI.FocusControl(null);            // drop focus so text fields refresh
        }
    }
}