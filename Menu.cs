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
        public enum Tab
        {
            Physics,
            Bike,
            Tricks,
            Character,
            BikeMaterials,
            Drone,
            Misc,
            Graphics,
            Marker,
            //Premium
        }

        // Menu Variables
        public static bool isOpen = false;
        public static Rect windowRect = new Rect(500f, 150f, 900f, 800f);
        public static Vector2 previousWindowPosition = Vector2.zero;
        public static Vector2 scrollPosition = Vector2.zero;
        public static bool inModelsTab = false;
        public static Tab currentTab;
        public static Slot currentSlot;
        public static float tabWidth => (windowRect.width - 60) / Enum.GetValues(typeof(Tab)).Length;
        public static float scrollOffset = 0f;
        public static float scrollViewHeight = 10000f;
        public static float viewHeight = 0f;

        // Style variables
        public static GUIStyle windowStyle;
        public static GUIStyle labelStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle horizontalSliderStyle;
        public static GUIStyle horizontalSliderThumbStyle;
        public static GUIStyle coloredBoxStyle;
        public static GUIStyle highQualityButtonStyle;
        public static GUIStyle activeTabButtonStyle;
        public static bool stylesInitialized = false;

        // Dictionaries and caches
        private static readonly Dictionary<string, string> sliderTextValues = new Dictionary<string, string>();
        private static readonly Dictionary<string, bool> sliderFieldJustFocused = new Dictionary<string, bool>();
        public static Dictionary<Slot, GameObject> slotGameObjects = new Dictionary<Slot, GameObject>();
        public static List<UnityEngine.Camera> cachedCameras = new List<UnityEngine.Camera>();
        public static List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData> cachedHDRCameras = new List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        public static List<UnityEngine.Rendering.Volume> cachedVolumes = new List<UnityEngine.Rendering.Volume>();
        private static Texture2D logoTexture;


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
                        ModernToggle("Spin Assist", ref bSpinAssist);
                        ModernToggle("Drifting", ref bDriftAbility);
                        ModernSlider("Gravity", ref gravity, 0f, 30f);
                        ModernSlider("Small Hop Force", ref smallHopForce, 0f, 25f);
                        GUILayout.Box("Pump/Spin", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        ModernSlider("Pump Force", ref pumpForce, 0f, 30f);
                        ModernSlider("Spin Speed Multiplier", ref spinTorque, 0f, 10f);
                        ModernSlider("Steer Damping", ref steerDamp, 1f, 5f);
                        GUILayout.Box("Manuals", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        ModernSlider("Max Nose Manual Angle", ref noseManualAngle, 10f, 50f);
                        ModernSlider("Max Manual Angle", ref manualAngle, 10f, 50f);
                        GUILayout.Box("Other", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        break;
                    case Tab.Bike:
                        PartTweaker.DrawPartTweaker();

                        GUILayout.BeginHorizontal();
                        GUILayout.Box("Bike Parts", coloredBoxStyle, GUILayout.Height(coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));
                        GUILayout.EndHorizontal();

                        Memory.DrawBmxBarsSelector();
                        //Memory.DrawBmxFramesSelector();
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

                    case Tab.Drone:
                        Mods.Misc.Update();
                        ModernToggle("Toggle Drone Body", ref droneBodyToggle);
                        ModernToggle("Toggle Drone Sound", ref droneEmitterToggle);
                        ModernSlider("Drone Mass", ref droneMass, 2f, 1000f);
                        break;

                    case Tab.Misc:
                        Mods.Misc.Update();
                        ModernToggle("No Bail", ref bNeverBail);
                        ModernToggle("Vibration", ref bVibration);
                        if (hapticFeedBack != null)
                            hapticFeedBack.SetActive(bVibration);
                        ModernToggle("Hide Helmet", ref bHideHelmet);
                        /*GUILayout.Box("", coloredBoxStyle, GUILayout.Height(5), GUILayout.ExpandWidth(true));
                        Slider("Menu Color R", ref menuAccentR, 0f, 1f);
                        Slider("Menu Color G", ref menuAccentG, 0f, 1f);
                        Slider("Menu Color B", ref menuAccentB, 0f, 1f);
                        if (GUILayout.Button("<b>Set Menu Color</b>", highQualityButtonStyle))
                        {
                            InitializeStyles();
                        }*/
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
                                Config.customSessionMarker = marker.name;
                            }
                        }
                        GUILayout.Box("Current Selected Marker: " + (Config.customSessionMarker ?? "None"), labelStyle);
                        break;
                    /*case Tab.Premium:
                        


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
            try
            {
                GUILayout.BeginHorizontal();

                float tabHeight = 30f; // Matches rounded button height
                float tabWidth = 80f; // Adjust as needed for spacing

                // Define tab labels and corresponding enum values
                (string label, Tab tab)[] tabs = new[]
                {
                    ("Physics", Tab.Physics),
                    //("Tricks", Tab.Tricks),
                    ("Bike", Tab.Bike),
                    ("Materials", Tab.BikeMaterials),
                    ("Character", Tab.Character),
                    ("Drone", Tab.Drone),
                    ("Misc", Tab.Misc),
                    ("Graphics", Tab.Graphics),
                    ("Marker", Tab.Marker)
                };

                foreach (var (label, tab) in tabs)
                {
                    GUIStyle buttonStyle = currentTab == tab ? activeTabButtonStyle : highQualityButtonStyle;
                    if (GUILayout.Button($"<b>{label}</b>", buttonStyle, GUILayout.Width(tabWidth), GUILayout.Height(tabHeight)))
                    {
                        SetCurrentTab(tab);
                    }
                }

                GUILayout.Space(100);
                GUILayout.BeginVertical();
                /*if (GUILayout.Button("<b>Snap</b>", highQualityButtonStyle, GUILayout.Width(tabWidth-15), GUILayout.Height(tabHeight-15)))
                {
                    if (customizableEntity != null)
                    {
                        customizableEntity.RelaySnap();
                        Log.Msg("RelaySnap() called...");
                    }
                }*/

                if (GUILayout.Button("<b>RESET\nTAB</b>", highQualityButtonStyle, GUILayout.Width(tabWidth - 15), GUILayout.Height(tabHeight)))
                {
                    switch (currentTab)
                    {
                        case Tab.Physics: ResetPhysicsTab(); break;
                        case Tab.Bike:
                            LoadAllAssetBundles();
                            Log.Msg("Bike Tab reset!");
                                break;
                        case Tab.Character: ResetCharacterTab(); break;
                        case Tab.BikeMaterials:
                            CategorizeEquipSlots(equipSlotVehicles);
                            ResetBikeMaterialsTab(); 
                            break;
                        case Tab.Misc: 
                            ResetMiscTab(); 
                            break;
                        case Tab.Marker: 
                                Memory.ReloadAssetsFromCachedBundles();
                            break;
                    }
                    ResetSliderTextValues();
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in DrawTabs: {ex.Message}");
            }
        }


        public static void HandleScrolling()
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
        // RESET SLIDER TEXT VALUES
        //-------------------------------------------------------------------

        public static void ResetSliderTextValues()
        {
            try
            {
                sliderTextValues.Clear();
                sliderFieldJustFocused.Clear();

                // Physics Tab Sliders
                sliderTextValues["Gravity"] = gravity.ToString("F2");
                sliderTextValues["Small Hop Force"] = smallHopForce.ToString("F2");
                sliderTextValues["Pump Force"] = pumpForce.ToString("F2");
                sliderTextValues["Spin Speed Multiplier"] = spinTorque.ToString("F2");
                sliderTextValues["Steer Damping"] = steerDamp.ToString("F2");
                sliderTextValues["Max Nose Manual Angle"] = noseManualAngle.ToString("F2");
                sliderTextValues["Max Manual Angle"] = manualAngle.ToString("F2");

                // Misc Tab Sliders
                sliderTextValues["Slo Motion Timer"] = sloMoTimer.ToString("F2");
                sliderTextValues["Drone Mass"] = droneMass.ToString("F2");
                sliderTextValues["Menu Color R"] = menuAccentR.ToString("F2");
                sliderTextValues["Menu Color G"] = menuAccentG.ToString("F2");
                sliderTextValues["Menu Color B"] = menuAccentB.ToString("F2");
            }
            catch (Exception ex)
            {
                Log.Error($"Error in ResetSliderTextValues: {ex.Message}");
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

                // Window Style
                windowStyle = new GUIStyle(GUI.skin.window);
                //Texture2D backgroundTexture = MakeTex(2, 2, new Color(0, 0, 0, 0.9f));
                
                Color accentBaseColor = new Color(0.2f, 0.6f, 1f); // Same as toggle
                Color accentHoverBaseColor = new Color(0.3f, 0.7f, 1.2f); // Slightly lighter on hover
                Color activeTabColor = new Color(0.4f, 0.8f, 1.4f); // Brighter for active tab
                
                Texture2D backgroundTexture = MakeRoundedTex(900, 800, new Color(0, 0, 0, 0.9f), 10, 4, accentBaseColor);

                Texture2D backgroundTextureSelected = MakeTex(2, 2, new Color(1f, 1f, 1f, 1f));
                
                //Color accentBaseColor = new Color(0.8176f, 0.3078f, 0.0412f);
               // Color accentHoverBaseColor = accentBaseColor * 1.35f;
                //Color activeTabColor = accentBaseColor * 2f; // Slightly different for active tab
                
                

                
                Texture2D roundedButtonNormal = MakeRoundedTex(20, 40, accentBaseColor, 10,1,Color.black);
                Texture2D roundedButtonHover = MakeRoundedTex(20, 40, accentHoverBaseColor, 10, 1, Color.black);
                Texture2D activeTabBackground = MakeRoundedTex(20, 40, activeTabColor, 10, 1, Color.black);
                Texture2D accentColor = MakeTex(2, 2, accentBaseColor);

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
                coloredBoxStyle.fontSize = 13;
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

        public static void Slider(string label, ref float target, float min, float max, float sliderWidth = 400f, float defaultValue = 1f)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{label} = {target:F2}", labelStyle);
                float newSliderValue = GUILayout.HorizontalSlider(target, min, max, horizontalSliderStyle, horizontalSliderThumbStyle, GUILayout.Width(sliderWidth));
                string controlName = "SliderTextField_" + label;
                GUI.SetNextControlName(controlName);

                if (!sliderTextValues.ContainsKey(label))
                    sliderTextValues[label] = target.ToString("F2");
                if (!sliderFieldJustFocused.ContainsKey(label))
                    sliderFieldJustFocused[label] = false;

                string currentText = sliderTextValues[label];
                string newText = GUILayout.TextField(currentText, GUILayout.Width(50));
                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;

                if (isFocused && !sliderFieldJustFocused[label])
                {
                    newText = "";
                    sliderFieldJustFocused[label] = true;
                }

                if (isFocused && (Keyboard.current?.enterKey.isPressed == true || Keyboard.current?.numpadEnterKey.isPressed == true))
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
            catch (Exception ex)
            {
                Log.Error($"Error in Slider: {ex.Message}");
            }
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
            float width = 50f;
            float height = 25f;
            float knobSize = 20f;
            float padding = 2f;

            Rect fullRect = GUILayoutUtility.GetRect(windowRect.width - 30f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));

            Rect labelRect = new Rect(fullRect.x, fullRect.y, fullRect.width - width - 10f, height);
            Rect toggleRect = new Rect(fullRect.x + fullRect.width - width, fullRect.y, width, height);

            if (!toggleAnimationState.ContainsKey(label))
                toggleAnimationState[label] = value ? 1f : 0f;

            if (Event.current.type == EventType.MouseDown && toggleRect.Contains(Event.current.mousePosition))
            {
                value = !value;
                Event.current.Use();
            }

            // Animate
            float target = value ? 1f : 0f;
            toggleAnimationState[label] = Mathf.Lerp(toggleAnimationState[label], target, 0.2f);

            // Draw background
            Color onColor = new Color(0.2f, 0.6f, 1f);
            Color offColor = new Color(0.3f, 0.3f, 0.3f);
            DrawSolidColorRect(toggleRect, Color.Lerp(offColor, onColor, toggleAnimationState[label]));

            // Draw knob
            float knobX = Mathf.Lerp(toggleRect.x + padding, toggleRect.x + toggleRect.width - knobSize - padding, toggleAnimationState[label]);
            Rect knobRect = new Rect(knobX, toggleRect.y + padding, knobSize, knobSize);
            DrawSolidColorRect(knobRect, Color.white);

            // Draw label on the left
            GUI.Label(labelRect, label, labelStyle);

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
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, GetWhiteTexture());
            GUI.color = oldColor;
        }


        private static string _activeSliderLabel = null;

        public static void ModernSlider(string label, ref float target, float min, float max)
        {
            float height = 25f;
            float labelWidth = 150f;
            float valueBoxWidth = 50f;
            float spacing = 15f;
            float sliderWidth = Menu.windowRect.width - labelWidth - valueBoxWidth - spacing * 4;

            Rect fullRect = GUILayoutUtility.GetRect(Menu.windowRect.width - 30f, height, GUILayout.ExpandWidth(true),
                GUILayout.Height(height));

            Rect labelRect = new Rect(fullRect.x, fullRect.y, labelWidth, height);
            Rect sliderRect = new Rect(fullRect.x + labelWidth + spacing, fullRect.y + 6, sliderWidth, height - 12f);
            Rect valueRect = new Rect(fullRect.x + labelWidth + spacing + sliderWidth + spacing, fullRect.y,
                valueBoxWidth, height);

            GUI.Label(labelRect, label, Menu.labelStyle);

            // Slider background and fill
            DrawSolidColorRect(sliderRect, new Color(0.25f, 0.25f, 0.25f));
            float percent = Mathf.InverseLerp(min, max, target);
            Rect fillRect = new Rect(sliderRect.x, sliderRect.y, sliderRect.width * percent, sliderRect.height);
            DrawSolidColorRect(fillRect, new Color(0.2f, 0.6f, 1f));

            // Thumb
            float thumbX = Mathf.Lerp(sliderRect.x, sliderRect.xMax - 10f, percent);
            Rect thumbRect = new Rect(thumbX - 5f, sliderRect.y - 2f, 10f, sliderRect.height + 4f);
            DrawSolidColorRect(thumbRect, Color.white);

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

                target = Mathf.Round(rawValue * 10f) / 10f; 

                e.Use();
            }

            // Value box
            string valueStr = target.ToString("0.00");
            float borderSize = 2f;
            Rect borderRect = new Rect(valueRect.x - borderSize, valueRect.y - borderSize,
                valueRect.width + borderSize * 2, valueRect.height + borderSize * 2);
            DrawSolidColorRect(borderRect, new Color(0.2f, 0.6f, 1f));
            DrawSolidColorRect(valueRect, Color.black);

            GUIStyle valueLabelStyle = new GUIStyle(Menu.labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(valueRect, valueStr, valueLabelStyle);
        }



        public static bool ModernButton(string label, float width = 200f, float height = 30f)
        {
            Rect buttonRect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false), GUILayout.Height(height));

            // Detect hover state
            bool isHovering = buttonRect.Contains(Event.current.mousePosition);

            // Colors
            Color baseColor = new Color(0.2f, 0.6f, 1f);      // Accent blue
            Color hoverColor = new Color(0.3f, 0.7f, 1.2f);   // Brighter blue
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
    }
}