using System;
using System.Collections;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using rowemod.Utils;
using GameReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;

namespace rowemod.Mods
{
    public static class PieMenu
    {
        // Keep this as a small immutable value type. If entries gain mutable/reference-heavy fields,
        // switch to a class to avoid value-copy surprises.
        private struct PieEntry
        {
            public readonly string Label;
            public readonly string ActionId;
            public readonly Vector2 Direction;

            public PieEntry(string label, string actionId, Vector2 direction)
            {
                Label = label;
                ActionId = actionId;
                Direction = direction.normalized;
            }
        }

        // Directions are in GUI screen space: +X is right, +Y is down.
        // Right-stick input is converted into this same convention in GetSelectionVector().
        private static readonly PieEntry[] Entries =
        {
            new PieEntry("RoweMod", "rowemod", new Vector2(0f, -1f)),
            new PieEntry("Extra 2", "extra_2", new Vector2(0.707f, -0.707f)),
            new PieEntry("Replay", "extra_3", new Vector2(1f, 0f)),
            new PieEntry("Extra 4", "extra_4", new Vector2(0.707f, 0.707f)),
            new PieEntry("Extra 5", "extra_5", new Vector2(0f, 1f)),
            new PieEntry("Extra 6", "extra_6", new Vector2(-0.707f, 0.707f)),
            new PieEntry("Extra 7", "extra_7", new Vector2(-1f, 0f)),
            new PieEntry("Extra 8", "extra_8", new Vector2(-0.707f, -0.707f))
        };

        private const float SelectionDeadzone = 0.55f;
        private const float Radius = 150f;
        private const float CenterBoxWidth = 428f;
        private const float CenterBoxHeight = 236f;
        private const float HintWidth = 420f;
        private const float HintHeight = 28f;
        private const float HintOffsetBelowButtons = 54f;
        private const float NativeReplaySuppressWindow = 1.5f;
        private const float OpenHoldSeconds = 0.18f;
        private const int WheelTextureSize = 256;
        private const float WheelOuterRadius = 119f;
        private const float WheelInnerRadius = 66f;
        private const float WheelDividerHalfDegrees = 1.2f;
        private const float ColorComparisonEpsilon = 0.003f;
        private const float OpenCloseAnimationSeconds = 0.12f;
        private const float WheelEdgeAntialiasPixels = 1.6f;
        private const float DividerAntialiasDegrees = 1.4f;
        private const int VignetteTextureSize = 192;
        private const string PieLogoUrl = "https://github.com/xrowex/RoweModsLogo/raw/main/ROWMODSCERT.png";
        private const string PieLogoCacheFileName = "ROWMODSCERT.png";
        private const string RoweModEntryLogoUrl = "https://github.com/xrowex/RoweModsLogo/raw/main/rowemods.png";
        private const string RoweModEntryLogoCacheFileName = "rowemods.png";

        private static bool isOpen;
        private static bool isClosingAnimation;
        private static bool consumedInputThisFrame;
        private static bool waitForRightDpadRelease;
        private static bool isPieLogoLoading;
        private static bool hasPieLogoLoadFailed;
        private static bool isRoweModEntryLogoLoading;
        private static bool hasRoweModEntryLogoLoadFailed;
        private static float rightDpadHeldSince;
        private static float openAnimationStartedAt;
        private static float closeAnimationStartedAt;
        private static int selectedIndex = -1;
        private static int lastDiagnosticSelectedIndex = -2;
        private static float nextStickDiagnosticTime;
        private static Color selectedAccentColor;
        private static Color cachedWheelAccentColor;
        private static Texture2D darkTexture;
        private static Texture2D centerTexture;
        private static Texture2D pieLogoTexture;
        private static Texture2D roweModEntryLogoTexture;
        private static readonly Texture2D[] WheelTextures = new Texture2D[Entries.Length + 1];
        private static GUIStyle labelStyle;
        private static GUIStyle selectedLabelStyle;
        private static GUIStyle centerStyle;
        private static GUIStyle hintStyle;

        public static bool IsOpen => isOpen;
        public static bool ConsumedInputThisFrame => consumedInputThisFrame;

        public static void PreloadAssets()
        {
            EnsurePieLogoLoading();
            EnsureRoweModEntryLogoLoading();
        }

        public static void Cleanup()
        {
            isOpen = false;
            isClosingAnimation = false;
            waitForRightDpadRelease = false;
            selectedIndex = -1;
            lastDiagnosticSelectedIndex = -2;
            nextStickDiagnosticTime = 0f;
            ReplayInputPatch.CancelReplayOpenAuthorization();
            labelStyle = null;
            selectedLabelStyle = null;
            centerStyle = null;
            hintStyle = null;

            DestroyTexture(ref darkTexture);
            DestroyTexture(ref centerTexture);
            DestroyTexture(ref pieLogoTexture);
            DestroyTexture(ref roweModEntryLogoTexture);
            DestroyWheelTextures();
        }

        public static void Update()
        {
            consumedInputThisFrame = false;

            Gamepad gamepad = Gamepad.current;
            Keyboard keyboard = Keyboard.current;

            if (gamepad == null && keyboard == null)
                return;

            bool rightDpadPressed = gamepad?.dpad.right.isPressed == true;
            if (!rightDpadPressed)
            {
                waitForRightDpadRelease = false;
                rightDpadHeldSince = 0f;

                if (isOpen)
                {
                    consumedInputThisFrame = true;
                    ActivateSelectedOnRelease();
                }

                return;
            }

            consumedInputThisFrame = true;

            if (rightDpadHeldSince <= 0f)
                rightDpadHeldSince = Time.unscaledTime;

            if (waitForRightDpadRelease)
                return;

            if (!isOpen)
            {
                if (Time.unscaledTime - rightDpadHeldSince < OpenHoldSeconds)
                    return;

                Log.Msg($"[PieMenuDiag] Right D-pad held. pieOpen={isOpen} time={Time.unscaledTime:F2}.");
                StartNativeReplaySuppression();
                Open();
            }

            if (!isOpen || gamepad == null)
                return;

            if (keyboard?.escapeKey.wasPressedThisFrame == true || gamepad.buttonEast.wasPressedThisFrame)
            {
                CloseUntilRightDpadRelease();
                return;
            }

            Vector2 selection = GetSelectionVector(gamepad);
            if (selection.sqrMagnitude >= SelectionDeadzone * SelectionDeadzone)
            {
                consumedInputThisFrame = true;
                selectedIndex = GetNearestEntry(selection.normalized);
                LogStickDiagnostic(gamepad, selection);
            }
            else
            {
                selectedIndex = -1;
                lastDiagnosticSelectedIndex = -2;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                consumedInputThisFrame = true;
                ActivateSelected(true);
            }
        }

        public static void Draw()
        {
            if (!isOpen && !isClosingAnimation)
                return;

            EnsureStyles();

            float animationScale = GetAnimationScale();
            if (animationScale <= 0f)
                return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), darkTexture);

            Texture2D wheelTexture = GetWheelTexture();

            float wheelSize = Mathf.Min(Screen.width, Screen.height) * 0.58f;
            wheelSize = Mathf.Clamp(wheelSize, 320f, 520f) * animationScale;
            Rect wheelRect = new Rect(
                center.x - wheelSize * 0.5f,
                center.y - wheelSize * 0.5f,
                wheelSize,
                wheelSize);
            Rect shadowRect = new Rect(wheelRect.x + 10f, wheelRect.y + 14f, wheelRect.width, wheelRect.height);

            GUI.color = new Color(0f, 0f, 0f, 0.32f);
            GUI.DrawTexture(shadowRect, wheelTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(wheelRect, wheelTexture);

            float contentAlpha = Mathf.Clamp01(animationScale * 1.25f);
            Color previousColor = GUI.color;
            GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * contentAlpha);

            for (int i = 0; i < Entries.Length; i++)
            {
                PieEntry entry = Entries[i];
                Vector2 pos = center + entry.Direction * (wheelSize * 0.36f);
                Rect rect = new Rect(pos.x - 62f, pos.y - 13f, 124f, 26f);
                bool isSelected = selectedIndex >= 0 && i == selectedIndex;
                DrawEntryLabel(entry, rect, isSelected);
            }

            Rect centerRect = new Rect(
                center.x - CenterBoxWidth * 0.5f,
                center.y - CenterBoxHeight * 0.5f,
                CenterBoxWidth,
                CenterBoxHeight);
            DrawCenterLogo(centerRect);

            Rect hintRect = new Rect(
                center.x - HintWidth * 0.5f,
                center.y + (wheelSize * 0.5f) + HintOffsetBelowButtons,
                HintWidth,
                HintHeight);
            GUI.Label(hintRect, "Hold Right D-pad   Right stick selects   Release opens", hintStyle);
            GUI.color = previousColor;
        }

        private static void DrawEntryLabel(PieEntry entry, Rect rect, bool isSelected)
        {
            if (entry.ActionId != "rowemod")
            {
                GUI.Label(rect, entry.Label, isSelected ? selectedLabelStyle : labelStyle);
                return;
            }

            EnsureRoweModEntryLogoLoading();

            Texture2D logoTexture = roweModEntryLogoTexture != null
                ? roweModEntryLogoTexture
                : global::rowemod.Menu.logoTexture;

            if (logoTexture == null || logoTexture.width <= 0 || logoTexture.height <= 0)
                return;

            Rect logoFitRect = new Rect(rect.x - 8f, rect.y - 10f, rect.width + 16f, rect.height + 20f);
            float scale = Mathf.Min(logoFitRect.width / logoTexture.width, logoFitRect.height / logoTexture.height);
            float logoWidth = logoTexture.width * scale;
            float logoHeight = logoTexture.height * scale;
            Rect logoRect = new Rect(
                logoFitRect.x + ((logoFitRect.width - logoWidth) * 0.5f),
                logoFitRect.y + ((logoFitRect.height - logoHeight) * 0.5f),
                logoWidth,
                logoHeight);

            Color previousColor = GUI.color;
            GUI.color = isSelected ? Color.white : new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private static void DrawCenterLogo(Rect centerRect)
        {
            EnsurePieLogoLoading();

            Texture2D logoTexture = pieLogoTexture != null
                ? pieLogoTexture
                : global::rowemod.Menu.logoTexture;

            if (logoTexture == null || logoTexture.width <= 0 || logoTexture.height <= 0)
            {
                GUI.Box(centerRect, "ROWE\nMOD", centerStyle);
                return;
            }

            const float padding = 2f;
            Rect fitRect = new Rect(
                centerRect.x + padding,
                centerRect.y + padding,
                centerRect.width - (padding * 2f),
                centerRect.height - (padding * 2f));

            float scale = Mathf.Min(fitRect.width / logoTexture.width, fitRect.height / logoTexture.height);
            float logoWidth = logoTexture.width * scale;
            float logoHeight = logoTexture.height * scale;
            Rect logoRect = new Rect(
                fitRect.x + ((fitRect.width - logoWidth) * 0.5f),
                fitRect.y + ((fitRect.height - logoHeight) * 0.5f),
                logoWidth,
                logoHeight);

            Color previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private static void EnsurePieLogoLoading()
        {
            if (pieLogoTexture != null || isPieLogoLoading || hasPieLogoLoadFailed)
                return;

            if (TryLoadPieLogoFromCache())
                return;

            isPieLogoLoading = true;
            MelonCoroutines.Start(LoadPieLogo());
        }

        private static IEnumerator LoadPieLogo()
        {
            Log.Msg($"[PieMenu] Starting pie logo download from: {PieLogoUrl}");
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(PieLogoUrl);
            yield return www.SendWebRequest();

            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    hasPieLogoLoadFailed = true;
                    Log.Error($"[PieMenu] Pie logo download failed: {www.error}");
                    yield break;
                }

                pieLogoTexture = DownloadHandlerTexture.GetContent(www);
                if (pieLogoTexture == null)
                {
                    hasPieLogoLoadFailed = true;
                    Log.Error("[PieMenu] Pie logo download returned no texture.");
                }
                else
                {
                    Log.Msg($"[PieMenu] Pie logo loaded successfully. texture={pieLogoTexture.width}x{pieLogoTexture.height}");
                    SavePieLogoCache(www.downloadHandler.data);
                }
            }
            catch (Exception ex)
            {
                hasPieLogoLoadFailed = true;
                Log.Error($"[PieMenu] Error loading pie logo: {ex.Message}");
            }
            finally
            {
                isPieLogoLoading = false;
                www.Dispose();
            }
        }

        private static bool TryLoadPieLogoFromCache()
        {
            try
            {
                string cachePath = GetPieLogoCachePath();
                if (!File.Exists(cachePath))
                    return false;

                byte[] data = File.ReadAllBytes(cachePath);
                if (data == null || data.Length == 0)
                    return false;

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, data))
                {
                    UnityEngine.Object.Destroy(texture);
                    return false;
                }

                pieLogoTexture = texture;
                Log.Msg($"[PieMenu] Pie logo loaded from cache. texture={pieLogoTexture.width}x{pieLogoTexture.height}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[PieMenu] Failed to load cached pie logo: {ex.Message}");
                return false;
            }
        }

        private static void SavePieLogoCache(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            try
            {
                string cachePath = GetPieLogoCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllBytes(cachePath, data);
                Log.Msg($"[PieMenu] Pie logo cached at: {cachePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PieMenu] Failed to cache pie logo: {ex.Message}");
            }
        }

        private static string GetPieLogoCachePath()
        {
            return Path.Combine(MelonEnvironment.ModsDirectory, "RoweMod", "Cache", PieLogoCacheFileName);
        }

        private static void EnsureRoweModEntryLogoLoading()
        {
            if (roweModEntryLogoTexture != null || isRoweModEntryLogoLoading || hasRoweModEntryLogoLoadFailed)
                return;

            if (TryLoadRoweModEntryLogoFromCache())
                return;

            isRoweModEntryLogoLoading = true;
            MelonCoroutines.Start(LoadRoweModEntryLogo());
        }

        private static IEnumerator LoadRoweModEntryLogo()
        {
            Log.Msg($"[PieMenu] Starting RoweMod entry logo download from: {RoweModEntryLogoUrl}");
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(RoweModEntryLogoUrl);
            yield return www.SendWebRequest();

            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    hasRoweModEntryLogoLoadFailed = true;
                    Log.Error($"[PieMenu] RoweMod entry logo download failed: {www.error}");
                    yield break;
                }

                roweModEntryLogoTexture = DownloadHandlerTexture.GetContent(www);
                if (roweModEntryLogoTexture == null)
                {
                    hasRoweModEntryLogoLoadFailed = true;
                    Log.Error("[PieMenu] RoweMod entry logo download returned no texture.");
                }
                else
                {
                    Log.Msg($"[PieMenu] RoweMod entry logo loaded successfully. texture={roweModEntryLogoTexture.width}x{roweModEntryLogoTexture.height}");
                    SaveRoweModEntryLogoCache(www.downloadHandler.data);
                }
            }
            catch (Exception ex)
            {
                hasRoweModEntryLogoLoadFailed = true;
                Log.Error($"[PieMenu] Error loading RoweMod entry logo: {ex.Message}");
            }
            finally
            {
                isRoweModEntryLogoLoading = false;
                www.Dispose();
            }
        }

        private static bool TryLoadRoweModEntryLogoFromCache()
        {
            try
            {
                string cachePath = GetRoweModEntryLogoCachePath();
                if (!File.Exists(cachePath))
                    return false;

                byte[] data = File.ReadAllBytes(cachePath);
                if (data == null || data.Length == 0)
                    return false;

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, data))
                {
                    UnityEngine.Object.Destroy(texture);
                    return false;
                }

                roweModEntryLogoTexture = texture;
                Log.Msg($"[PieMenu] RoweMod entry logo loaded from cache. texture={roweModEntryLogoTexture.width}x{roweModEntryLogoTexture.height}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[PieMenu] Failed to load cached RoweMod entry logo: {ex.Message}");
                return false;
            }
        }

        private static void SaveRoweModEntryLogoCache(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            try
            {
                string cachePath = GetRoweModEntryLogoCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllBytes(cachePath, data);
                Log.Msg($"[PieMenu] RoweMod entry logo cached at: {cachePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PieMenu] Failed to cache RoweMod entry logo: {ex.Message}");
            }
        }

        private static string GetRoweModEntryLogoCachePath()
        {
            return Path.Combine(MelonEnvironment.ModsDirectory, "RoweMod", "Cache", RoweModEntryLogoCacheFileName);
        }

        private static void Open()
        {
            EnsurePieLogoLoading();
            EnsureRoweModEntryLogoLoading();
            selectedIndex = -1;
            lastDiagnosticSelectedIndex = -2;
            nextStickDiagnosticTime = 0f;
            isOpen = true;
            isClosingAnimation = false;
            openAnimationStartedAt = Time.unscaledTime;
            Log.Msg("[PieMenu] Opened.");
        }

        private static void Close()
        {
            if (!isOpen)
                return;

            isOpen = false;
            isClosingAnimation = true;
            closeAnimationStartedAt = Time.unscaledTime;
            Log.Msg("[PieMenu] Closed.");
        }

        private static float GetAnimationScale()
        {
            float progress;
            if (isOpen)
            {
                progress = Mathf.Clamp01((Time.unscaledTime - openAnimationStartedAt) / OpenCloseAnimationSeconds);
                return EaseOutBack(progress);
            }

            progress = Mathf.Clamp01((Time.unscaledTime - closeAnimationStartedAt) / OpenCloseAnimationSeconds);
            if (progress >= 1f)
            {
                isClosingAnimation = false;
                return 0f;
            }

            return 1f - EaseOutCubic(progress);
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - (inverse * inverse * inverse);
        }

        private static float EaseOutBack(float value)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float t = value - 1f;
            return 1f + (c3 * t * t * t) + (c1 * t * t);
        }

        private static Vector2 GetSelectionVector(Gamepad gamepad)
        {
            Vector2 stick = gamepad.rightStick.ReadValue();
            if (stick.sqrMagnitude >= SelectionDeadzone * SelectionDeadzone)
                return new Vector2(stick.x, -stick.y);

            return Vector2.zero;
        }

        private static void LogStickDiagnostic(Gamepad gamepad, Vector2 mappedSelection)
        {
            if (selectedIndex == lastDiagnosticSelectedIndex && Time.unscaledTime < nextStickDiagnosticTime)
                return;

            Vector2 rawStick = gamepad.rightStick.ReadValue();
            string selectedLabel = selectedIndex >= 0 && selectedIndex < Entries.Length
                ? Entries[selectedIndex].Label
                : "none";

            Log.Msg(
                $"[PieMenuDiag] Stick raw=({rawStick.x:F2}, {rawStick.y:F2}) mapped=({mappedSelection.x:F2}, {mappedSelection.y:F2}) index={selectedIndex} label={selectedLabel}.");

            lastDiagnosticSelectedIndex = selectedIndex;
            nextStickDiagnosticTime = Time.unscaledTime + 0.25f;
        }

        private static int GetNearestEntry(Vector2 direction)
        {
            int nearest = 0;
            float bestDot = float.MinValue;

            for (int i = 0; i < Entries.Length; i++)
            {
                float dot = Vector2.Dot(direction, Entries[i].Direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static void ActivateSelectedOnRelease()
        {
            if (selectedIndex < 0 || selectedIndex >= Entries.Length)
            {
                Log.Msg("[PieMenu] Closed without action because the right stick was centered.");
                Close();
                return;
            }

            ActivateSelected(false);
        }

        private static void ActivateSelected(bool waitForRelease)
        {
            if (selectedIndex < 0 || selectedIndex >= Entries.Length)
                return;

            PieEntry entry = Entries[selectedIndex];
            Log.Msg($"[PieMenu] Activating slice {selectedIndex}: '{entry.Label}' ({entry.ActionId}).");
            TriggerAction(entry);

            if (waitForRelease)
                CloseUntilRightDpadRelease();
            else
                Close();
        }

        private static void TriggerAction(PieEntry entry)
        {
            switch (entry.ActionId)
            {
                case "rowemod":
                    Main.OpenRoweModMenu();
                    Log.Msg("[PieMenu] Opened RoweMod menu from pie menu.");
                    break;
                case "extra_3":
                    OpenReplayFromPieMenu();
                    break;
                default:
                    // Wire gameplay actions here once the target buttons/actions are known.
                    Log.Msg($"[PieMenu] Triggered action '{entry.ActionId}' ({entry.Label}).");
                    break;
            }
        }

        private static void OpenReplayFromPieMenu()
        {
            try
            {
                GameReplaySystem replaySystem = GameReplaySystem.Instance;

                if (replaySystem == null)
                {
                    Log.Warning("[PieMenu] Could not open replay because ReplaySystem.Instance is null.");
                    return;
                }

                ReplayInputPatch.AuthorizeNextReplayOpen();
                replaySystem.CommandOpenReplay();
                ReplayInputPatch.MarkReplayOpenedFromPieMenu();
                Log.Msg("[PieMenu] Opened replay editor from pie menu.");
            }
            catch (System.Exception ex)
            {
                ReplayInputPatch.CancelReplayOpenAuthorization();
                Log.Error($"[PieMenu] Failed to open replay editor from pie menu: {ex.Message}");
            }
        }

        private static void StartNativeReplaySuppression()
        {
            ReplayInputPatch.BlockNativeReplayForSeconds(NativeReplaySuppressWindow);
        }

        private static void CloseUntilRightDpadRelease()
        {
            waitForRightDpadRelease = true;
            Close();
        }

        private static void EnsureStyles()
        {
            if (labelStyle == null)
            {
                BuildStyles();
                return;
            }

            RefreshAccentColor(GetSelectedAccentColor());
        }

        private static void BuildStyles()
        {
            darkTexture = BuildVignetteTexture();
            selectedAccentColor = GetSelectedAccentColor();
            centerTexture = MakeTexture(new Color(0.02f, 0.022f, 0.024f, 0.96f));

            labelStyle = BuildLabelStyle(new Color(0.12f, 0.115f, 0.105f, 1f), 13);
            selectedLabelStyle = BuildLabelStyle(Color.black, 14);
            centerStyle = BuildBoxStyle(centerTexture, Color.white, 18);
            centerStyle.fontStyle = FontStyle.Bold;

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = new Color(1f, 1f, 1f, 0.78f) }
            };
        }

        private static Color GetSelectedAccentColor()
        {
            return new Color(Config.misc.menuAccentR, Config.misc.menuAccentG, Config.misc.menuAccentB, 0.96f);
        }

        private static void RefreshAccentColor(Color configuredAccentColor)
        {
            if (ColorsApproximatelyEqual(configuredAccentColor, selectedAccentColor))
                return;

            selectedAccentColor = configuredAccentColor;
            DestroyWheelTextures();
        }

        private static GUIStyle BuildBoxStyle(Texture2D background, Color textColor, int fontSize)
        {
            return new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    background = background,
                    textColor = textColor
                }
            };
        }

        private static GUIStyle BuildLabelStyle(Color textColor, int fontSize)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = textColor }
            };

            return style;
        }

        private static Texture2D GetWheelTexture()
        {
            if (!ColorsApproximatelyEqual(cachedWheelAccentColor, selectedAccentColor))
            {
                cachedWheelAccentColor = selectedAccentColor;
                DestroyWheelTextures();
            }

            int cacheIndex = selectedIndex + 1;
            if (cacheIndex < 0 || cacheIndex >= WheelTextures.Length)
                cacheIndex = 0;

            if (WheelTextures[cacheIndex] == null)
                WheelTextures[cacheIndex] = BuildWheelTexture(selectedIndex, selectedAccentColor);

            return WheelTextures[cacheIndex];
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= ColorComparisonEpsilon
                   && Mathf.Abs(a.g - b.g) <= ColorComparisonEpsilon
                   && Mathf.Abs(a.b - b.b) <= ColorComparisonEpsilon
                   && Mathf.Abs(a.a - b.a) <= ColorComparisonEpsilon;
        }

        private static Texture2D BuildWheelTexture(int selectedEntryIndex, Color accentColor)
        {
            Texture2D texture = new Texture2D(WheelTextureSize, WheelTextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color inactive = new Color(0.96f, 0.95f, 0.92f, 1f);
            Color divider = new Color(0.025f, 0.025f, 0.025f, 1f);
            Color rim = new Color(0f, 0f, 0f, 0.45f);
            Color glow = Color.Lerp(accentColor, Color.white, 0.28f);
            float center = (WheelTextureSize - 1) * 0.5f;

            for (int y = 0; y < WheelTextureSize; y++)
            {
                for (int x = 0; x < WheelTextureSize; x++)
                {
                    float dx = x - center;
                    float dy = center - y;
                    float radius = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float ringAlpha = SmoothStep(WheelInnerRadius - WheelEdgeAntialiasPixels, WheelInnerRadius + WheelEdgeAntialiasPixels, radius)
                                      * (1f - SmoothStep(WheelOuterRadius - WheelEdgeAntialiasPixels, WheelOuterRadius + WheelEdgeAntialiasPixels, radius));

                    if (ringAlpha <= 0f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    Vector2 direction = new Vector2(dx, dy).normalized;
                    int entryIndex = GetNearestEntry(direction);
                    float entryAngle = GetEntryGuiAngleDegrees(entryIndex);
                    float angleDelta = Mathf.Abs(Mathf.DeltaAngle(angle, entryAngle));
                    float dividerDistance = Mathf.Abs(angleDelta - 22.5f);
                    float dividerBlend = 1f - SmoothStep(WheelDividerHalfDegrees, WheelDividerHalfDegrees + DividerAntialiasDegrees, dividerDistance);
                    float innerRimBlend = 1f - SmoothStep(0f, 2.8f, Mathf.Abs(radius - WheelInnerRadius));
                    float outerRimBlend = 1f - SmoothStep(0f, 2.8f, Mathf.Abs(radius - WheelOuterRadius));
                    float rimBlend = Mathf.Max(dividerBlend, Mathf.Max(innerRimBlend, outerRimBlend));

                    Color pixelColor;
                    if (entryIndex == selectedEntryIndex)
                    {
                        float edgePulse = Mathf.Clamp01((Mathf.Max(angleDelta, 22.5f - angleDelta) - 17f) / 5.5f);
                        float radialPulse = 1f - Mathf.Abs(Mathf.InverseLerp(WheelInnerRadius, WheelOuterRadius, radius) - 0.58f) * 1.8f;
                        float glowBlend = Mathf.Clamp01(Mathf.Max(edgePulse * 0.38f, radialPulse * 0.18f));
                        pixelColor = Color.Lerp(accentColor, glow, glowBlend);
                    }
                    else
                    {
                        float edge = Mathf.InverseLerp(WheelInnerRadius, WheelOuterRadius, radius);
                        pixelColor = Color.Lerp(inactive, rim, Mathf.Clamp01((edge - 0.94f) * 3f));
                    }

                    if (rimBlend > 0f)
                        pixelColor = Color.Lerp(pixelColor, divider, rimBlend);

                    pixelColor.a *= ringAlpha;
                    texture.SetPixel(x, y, pixelColor);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D BuildVignetteTexture()
        {
            Texture2D texture = new Texture2D(VignetteTextureSize, VignetteTextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (VignetteTextureSize - 1) * 0.5f;
            float maxRadius = center * 1.42f;
            for (int y = 0; y < VignetteTextureSize; y++)
            {
                for (int x = 0; x < VignetteTextureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy)) / maxRadius;
                    float vignette = SmoothStep(0.14f, 1f, distance);
                    float alpha = Mathf.Lerp(0.56f, 0.86f, vignette);
                    texture.SetPixel(x, y, new Color(0.008f, 0.009f, 0.011f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (Mathf.Approximately(edge0, edge1))
                return value < edge0 ? 0f : 1f;

            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - (2f * t));
        }

        private static float GetEntryGuiAngleDegrees(int entryIndex)
        {
            Vector2 direction = Entries[entryIndex].Direction;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DestroyWheelTextures()
        {
            for (int i = 0; i < WheelTextures.Length; i++)
            {
                Texture2D texture = WheelTextures[i];
                if (texture == null)
                    continue;

                UnityEngine.Object.Destroy(texture);
                WheelTextures[i] = null;
            }
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
                return;

            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
