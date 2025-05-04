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
using Il2CppModIOBrowser;
using Il2CppPlayFab.ClientModels;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace rowemod
{
    public static class Menu
    {
        public enum Tab
        {
            Physics,
            Tricks,
            Character,
            BikeMaterials,
            Drone,
            Misc,
            Graphics,
            Marker
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
        private static bool stylesInitialized = false;

        // Dictionaries and caches
        private static readonly Dictionary<string, string> sliderTextValues = new Dictionary<string, string>();
        private static readonly Dictionary<string, bool> sliderFieldJustFocused = new Dictionary<string, bool>();
        public static Dictionary<Slot, GameObject> slotGameObjects = new Dictionary<Slot, GameObject>();
        public static List<UnityEngine.Camera> cachedCameras = new List<UnityEngine.Camera>();
        public static List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData> cachedHDRCameras = new List<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        public static List<UnityEngine.Rendering.Volume> cachedVolumes = new List<UnityEngine.Rendering.Volume>();
        private static Texture2D logoTexture;

        // Graphics settings
        public static Resolution[] availableResolutions;
        public static int selectedResolutionIndex;
        public static bool isFullScreen;
        public static int selectedQualityIndex;
        private static bool isVSyncEnabled = QualitySettings.vSyncCount > 0;

        //-------------------------------------------------------------------
        // MENU & TAB LOGIC
        //-------------------------------------------------------------------

        public static void DrawMenu(int windowID)
        {
            try
            {
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
                        GUILayout.Box("Toggles", coloredBoxStyle, GUILayout.Height(25), GUILayout.ExpandWidth(true));
                        Toggle("Spin Assist", ref bSpinAssist);
                        Toggle("Drifting", ref bDriftAbility);
                        Slider("Gravity", ref gravity, 0f, 30f);
                        Slider("Small Hop Force", ref smallHopForce, 0f, 25f);
                        GUILayout.Box("Pump/Spin", coloredBoxStyle, GUILayout.Height(25), GUILayout.ExpandWidth(true));
                        Slider("Pump Force", ref pumpForce, 0f, 30f);
                        Slider("Spin Speed Multiplier", ref spinTorque, 0f, 10f);
                        Slider("Steer Damping", ref steerDamp, 1f, 5f);
                        GUILayout.Box("Manuals", coloredBoxStyle, GUILayout.Height(25), GUILayout.ExpandWidth(true));
                        Slider("Max Nose Manual Angle", ref noseManualAngle, 10f, 90f);
                        Slider("Max Manual Angle", ref manualAngle, 10f, 90f);
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
                        Toggle("Toggle Drone Body", ref droneBodyToggle);
                        Toggle("Toggle Drone Sound", ref droneEmitterToggle);
                        Slider("Drone Mass", ref droneMass, 2f, 1000f);
                        break;

                    case Tab.Misc:
                        Mods.Misc.Update();
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

                    case Tab.Graphics:
                        DrawGraphicsSettings();
                        break;

                    case Tab.Marker:
                        GUILayout.Box("Session Markers", coloredBoxStyle, GUILayout.Height(25), GUILayout.ExpandWidth(true));
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
                if (logoTexture != null)
                {
                    GUILayout.Label(logoTexture, GUILayout.Width(150), GUILayout.Height(50));
                }
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("<b>Physics</b>", highQualityButtonStyle)) SetCurrentTab(Tab.Physics);
                if (GUILayout.Button("<b>Tricks</b>", highQualityButtonStyle)) SetCurrentTab(Tab.Tricks);
                if (GUILayout.Button("<b>Character</b>", highQualityButtonStyle)) SetCurrentTab(Tab.Character);
                if (GUILayout.Button("<b>BikeMaterials</b>", highQualityButtonStyle)) SetCurrentTab(Tab.BikeMaterials);
                if (GUILayout.Button("<b>Drone</b>", highQualityButtonStyle)) SetCurrentTab(Tab.Drone);
                if (GUILayout.Button("<b>Misc</b>", highQualityButtonStyle)) SetCurrentTab(Tab.Misc);
                if (GUILayout.Button("<b>Graphics</b>", highQualityButtonStyle)) SetCurrentTab(Tab.Graphics);
                if (GUILayout.Button("<b>Marker</b>", highQualityButtonStyle)) SetCurrentTab(Tab.Marker);

                GUILayout.Space(50);
                if (GUILayout.Button("<b>Snap</b>", highQualityButtonStyle, GUILayout.Width(80)))
                {
                    if (customizableEntity != null)
                    {
                        customizableEntity.RelaySnap();
                        Log.Msg("RelaySnap() called...");
                    }
                }
                if (GUILayout.Button("<b>RESET</b>", highQualityButtonStyle, GUILayout.Width(80)))
                {
                    switch (currentTab)
                    {
                        case Tab.Physics: ResetPhysicsTab(); break;
                        case Tab.Character: ResetCharacterTab(); break;
                        case Tab.BikeMaterials: ResetBikeMaterialsTab(); break;
                        case Tab.Misc: ResetMiscTab(); break;
                        case Tab.Graphics: ResetGraphicsTab(); break;
                    }
                    ResetSliderTextValues();
                }
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
                if (stylesInitialized) return;
                stylesInitialized = true;

                // Window Style
                windowStyle = new GUIStyle(GUI.skin.window);
                Texture2D backgroundTexture = MakeTex(2, 2, new Color(0, 0, 0, 0.9f));
                Texture2D backgroundTextureSelected = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1f));
                Color accentBaseColor = new Color(menuAccentR, menuAccentG, menuAccentB);
                Color accentHoverBaseColor = accentBaseColor * 1.35f;
                Texture2D roundedButtonNormal = MakeRoundedTex(20, 40, accentBaseColor, 10);
                Texture2D roundedButtonHover = MakeRoundedTex(20, 40, accentHoverBaseColor, 10);
                Texture2D accentColor = MakeTex(2, 2, accentBaseColor);

                windowStyle.normal.background = backgroundTexture;
                windowStyle.onNormal.background = backgroundTexture;
                windowStyle.hover.background = backgroundTexture;
                windowStyle.active.background = backgroundTextureSelected;
                windowStyle.normal.textColor = Color.white;
                windowStyle.fontSize = 16;
                windowStyle.fontStyle = FontStyle.Bold;

                // Label Style
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.normal.textColor = Color.white;
                labelStyle.fontSize = 12;

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
                highQualityButtonStyle.border = new RectOffset(10, 10, 10, 10);
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

        public static Texture2D MakeRoundedTex(int width, int height, Color color, int radius)
        {
            try
            {
                Texture2D tex = new Texture2D(width, height);
                Color[] colors = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bool insideCorner =
                            (x < radius && y < radius && Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius)) > radius) ||
                            (x > width - radius - 1 && y < radius && Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, radius)) > radius) ||
                            (x < radius && y > height - radius - 1 && Vector2.Distance(new Vector2(x, y), new Vector2(radius, height - radius - 1)) > radius) ||
                            (x > width - radius - 1 && y > height - radius - 1 && Vector2.Distance(new Vector2(x, y), new Vector2(width - radius - 1, height - radius - 1)) > radius);
                        colors[y * width + x] = insideCorner ? new Color(0, 0, 0, 0) : color;
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

        public static void ApplyGraphicsSettings()
        {
            try
            {
                if (availableResolutions == null || availableResolutions.Length == 0) return;
                Resolution res = availableResolutions[selectedResolutionIndex];
                Screen.SetResolution(res.width, res.height, isFullScreen);
                QualitySettings.vSyncCount = isVSyncEnabled ? 1 : 0;
                QualitySettings.SetQualityLevel(selectedQualityIndex);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in ApplyGraphicsSettings: {ex.Message}");
            }
        }

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

        public static void ApplyGlobalGraphicsSettings(int taaQualityIndex)
        {
            try
            {
                if (cachedHDRCameras == null || cachedHDRCameras.Count == 0) return;
                foreach (var hdrpCamera in cachedHDRCameras)
                {
                    if (hdrpCamera == null) continue;
                    switch (taaQualityIndex)
                    {
                        case 0: hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.None; break;
                        case 1: hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing; break;
                        case 2: hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing; break;
                        case 3: hdrpCamera.antialiasing = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing; break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in ApplyGlobalGraphicsSettings: {ex.Message}");
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

        public static Transform FindDeepChild(Transform parent, string name)
        {
            try
            {
                if (parent == null) return null;
                foreach (Transform child in parent)
                {
                    if (child != null && child.name == name)
                        return child;
                    Transform found = FindDeepChild(child, name);
                    if (found != null)
                        return found;
                }
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Error in FindDeepChild: {ex.Message}");
                return null;
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