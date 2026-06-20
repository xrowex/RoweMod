using UnityEngine;
using UnityEngine.InputSystem;
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
        // Stick/D-pad input is converted to this same space in GetSelectionVector().
        private static readonly PieEntry[] Entries =
        {
            new PieEntry("Extra 1", "extra_1", new Vector2(0f, -1f)),
            new PieEntry("Extra 2", "extra_2", new Vector2(0.707f, -0.707f)),
            new PieEntry("Replay", "extra_3", new Vector2(1f, 0f)),
            new PieEntry("Extra 4", "extra_4", new Vector2(0.707f, 0.707f)),
            new PieEntry("Extra 5", "extra_5", new Vector2(0f, 1f)),
            new PieEntry("Extra 6", "extra_6", new Vector2(-0.707f, 0.707f)),
            new PieEntry("Extra 7", "extra_7", new Vector2(-1f, 0f)),
            new PieEntry("Extra 8", "extra_8", new Vector2(-0.707f, -0.707f))
        };

        private const float ToggleCooldown = 0.25f;
        private const float SelectionDeadzone = 0.35f;
        private const float Radius = 150f;
        private const float ButtonWidth = 116f;
        private const float ButtonHeight = 42f;
        private const float CenterBoxWidth = 164f;
        private const float CenterBoxHeight = 72f;
        private const float HintWidth = 420f;
        private const float HintHeight = 28f;
        private const float HintOffsetBelowButtons = 54f;

        private static bool isOpen;
        private static bool consumedInputThisFrame;
        private static int selectedIndex = -1;
        private static float nextToggleTime;
        private static Color selectedAccentColor;
        private static Texture2D darkTexture;
        private static Texture2D buttonTexture;
        private static Texture2D selectedTexture;
        private static Texture2D centerTexture;
        private static GUIStyle buttonStyle;
        private static GUIStyle selectedButtonStyle;
        private static GUIStyle centerStyle;
        private static GUIStyle hintStyle;

        public static bool IsOpen => isOpen;
        public static bool ConsumedInputThisFrame => consumedInputThisFrame;

        public static void Cleanup()
        {
            isOpen = false;
            selectedIndex = -1;
            buttonStyle = null;
            selectedButtonStyle = null;
            centerStyle = null;
            hintStyle = null;

            DestroyTexture(ref darkTexture);
            DestroyTexture(ref buttonTexture);
            DestroyTexture(ref selectedTexture);
            DestroyTexture(ref centerTexture);
        }

        public static void Update()
        {
            consumedInputThisFrame = false;

            Gamepad gamepad = Gamepad.current;
            Keyboard keyboard = Keyboard.current;

            if (gamepad == null && keyboard == null)
                return;

            bool togglePressed = gamepad?.dpad.right.wasPressedThisFrame == true;

            if (keyboard?.escapeKey.wasPressedThisFrame == true && isOpen)
            {
                consumedInputThisFrame = true;
                Close();
                return;
            }

            if (togglePressed && Time.unscaledTime >= nextToggleTime)
            {
                consumedInputThisFrame = true;
                Log.Msg($"[PieMenuDiag] Right D-pad pressed. pieOpen={isOpen} time={Time.unscaledTime:F2}.");
                if (isOpen)
                    Close();
                else
                    Open();

                nextToggleTime = Time.unscaledTime + ToggleCooldown;
                return;
            }

            if (!isOpen || gamepad == null)
                return;

            if (gamepad.buttonEast.wasPressedThisFrame)
            {
                consumedInputThisFrame = true;
                Close();
                return;
            }

            Vector2 selection = GetSelectionVector(gamepad);
            if (selection.sqrMagnitude >= SelectionDeadzone * SelectionDeadzone)
            {
                consumedInputThisFrame = true;
                selectedIndex = GetNearestEntry(selection.normalized);
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                consumedInputThisFrame = true;
                ActivateSelected();
            }
        }

        public static void Draw()
        {
            if (!isOpen)
                return;

            EnsureStyles();

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), darkTexture);

            for (int i = 0; i < Entries.Length; i++)
            {
                PieEntry entry = Entries[i];
                Vector2 pos = center + entry.Direction * Radius;
                Rect rect = new Rect(pos.x - ButtonWidth * 0.5f, pos.y - ButtonHeight * 0.5f, ButtonWidth, ButtonHeight);
                bool isSelected = selectedIndex >= 0 && i == selectedIndex;
                GUI.Box(rect, entry.Label, isSelected ? selectedButtonStyle : buttonStyle);
            }

            Rect centerRect = new Rect(
                center.x - CenterBoxWidth * 0.5f,
                center.y - CenterBoxHeight * 0.5f,
                CenterBoxWidth,
                CenterBoxHeight);
            GUI.Box(centerRect, "EXTRA\nACTIONS", centerStyle);

            Rect hintRect = new Rect(
                center.x - HintWidth * 0.5f,
                center.y + Radius + HintOffsetBelowButtons,
                HintWidth,
                HintHeight);
            GUI.Label(hintRect, "Left stick / D-pad selects   A triggers   B closes", hintStyle);
        }

        private static void Open()
        {
            selectedIndex = -1;
            isOpen = true;
            Log.Msg("[PieMenu] Opened.");
        }

        private static void Close()
        {
            isOpen = false;
            Log.Msg("[PieMenu] Closed.");
        }

        private static Vector2 GetSelectionVector(Gamepad gamepad)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude >= SelectionDeadzone * SelectionDeadzone)
                // Unity input uses +Y up; IMGUI screen space uses +Y down.
                return new Vector2(stick.x, -stick.y);

            Vector2 dpad = gamepad.dpad.ReadValue();
            if (dpad.sqrMagnitude >= SelectionDeadzone * SelectionDeadzone)
                // Keep D-pad selection in the same screen-space convention as Entries.
                return new Vector2(dpad.x, -dpad.y);

            return Vector2.zero;
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

        private static void ActivateSelected()
        {
            if (selectedIndex < 0 || selectedIndex >= Entries.Length)
                return;

            PieEntry entry = Entries[selectedIndex];
            TriggerAction(entry);
            Close();
        }

        private static void TriggerAction(PieEntry entry)
        {
            switch (entry.ActionId)
            {
                case "extra_3":
                    OpenReplayFromExtra3();
                    break;
                default:
                    // Wire additional gameplay actions here once the target buttons/actions are known.
                    Log.Msg($"[PieMenu] Triggered action '{entry.ActionId}' ({entry.Label}).");
                    break;
            }
        }

        private static void OpenReplayFromExtra3()
        {
            try
            {
                GameReplaySystem replaySystem = GameReplaySystem.Instance;

                if (replaySystem == null)
                {
                    Log.Warning("[PieMenu] Extra 3 could not open replay because ReplaySystem.Instance is null.");
                    return;
                }

                replaySystem.CommandOpenReplay();
                Log.Msg("[PieMenu] Extra 3 requested replay open.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[PieMenu] Extra 3 failed to open replay: {ex.Message}");
            }
        }

        private static void EnsureStyles()
        {
            if (buttonStyle == null)
            {
                BuildStyles();
                return;
            }

            RefreshSelectedStyle(GetSelectedAccentColor());
        }

        private static void BuildStyles()
        {
            darkTexture = MakeTexture(new Color(0f, 0f, 0f, 0.42f));
            buttonTexture = MakeTexture(new Color(0.04f, 0.05f, 0.05f, 0.92f));
            selectedAccentColor = GetSelectedAccentColor();
            selectedTexture = MakeTexture(selectedAccentColor);
            centerTexture = MakeTexture(new Color(0.01f, 0.01f, 0.01f, 0.95f));

            buttonStyle = BuildBoxStyle(buttonTexture, Color.white, 16);
            selectedButtonStyle = BuildBoxStyle(selectedTexture, Color.black, 17);
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

        private static void RefreshSelectedStyle(Color configuredAccentColor)
        {
            if (configuredAccentColor == selectedAccentColor)
                return;

            selectedAccentColor = configuredAccentColor;
            DestroyTexture(ref selectedTexture);
            selectedTexture = MakeTexture(selectedAccentColor);

            if (selectedButtonStyle != null)
                selectedButtonStyle.normal.background = selectedTexture;
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

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
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
