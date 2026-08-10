using UnityEngine;
using UnityEngine.InputSystem;

namespace rowemod.Mods
{
    /// <summary>
    /// A cached IMGUI cursor owned by RoweMod. BMX Streets can change the operating-system
    /// cursor during gameplay transitions, so the native cursor remains hidden while this is drawn.
    /// </summary>
    public static class RoweModCursor
    {
        private const int TextureWidth = 28;
        private const int TextureHeight = 38;
        private static readonly Vector2[] CursorPolygon =
        {
            new Vector2(1f, 1f),
            new Vector2(1f, 28f),
            new Vector2(8f, 21f),
            new Vector2(14f, 35f),
            new Vector2(21f, 32f),
            new Vector2(15f, 19f),
            new Vector2(26f, 19f)
        };

        private static Texture2D cursorTexture;

        public static void EnforceNativeCursorHidden()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        public static void Draw()
        {
            if (!Menu.isOpen || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            EnsureTexture();
            if (cursorTexture == null)
                return;

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            DrawAt(guiPosition);
        }

        public static void DrawInsideMenuWindow()
        {
            if (!Menu.isOpen || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            EnsureTexture();
            if (cursorTexture == null)
                return;

            // During a GUI.Window callback Unity has already transformed the current event into
            // window-local coordinates, which keeps this copy pixel-aligned with Draw().
            DrawAt(Event.current.mousePosition);
        }

        private static void DrawAt(Vector2 guiPosition)
        {
            float scale = Mathf.Clamp(Menu.EffectiveUiScale, 0.9f, 1.4f);
            Rect cursorRect = new Rect(
                guiPosition.x - (1f * scale),
                guiPosition.y - (1f * scale),
                TextureWidth * scale,
                TextureHeight * scale);

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = -10000;
            GUI.color = Color.white;
            GUI.DrawTexture(cursorRect, cursorTexture, ScaleMode.StretchToFill, true);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        public static void Cleanup()
        {
            if (cursorTexture == null)
                return;

            UnityEngine.Object.Destroy(cursorTexture);
            cursorTexture = null;
        }

        private static void EnsureTexture()
        {
            if (cursorTexture != null)
                return;

            bool[,] shape = new bool[TextureWidth, TextureHeight];
            for (int y = 0; y < TextureHeight; y++)
            {
                for (int x = 0; x < TextureWidth; x++)
                    shape[x, y] = IsInsidePolygon(x + 0.5f, y + 0.5f);
            }

            Color[] pixels = new Color[TextureWidth * TextureHeight];
            Color outline = new Color(0.025f, 0.035f, 0.05f, 1f);
            Color fill = new Color(0.97f, 0.98f, 1f, 1f);
            Color accent = new Color(1f, 0.36f, 0.12f, 1f);
            Color shadow = new Color(0f, 0f, 0f, 0.42f);

            for (int y = 0; y < TextureHeight; y++)
            {
                for (int x = 0; x < TextureWidth; x++)
                {
                    Color color = Color.clear;
                    if (!shape[x, y] && IsShapePixel(shape, x - 2, y - 2))
                    {
                        color = shadow;
                    }
                    else if (shape[x, y])
                    {
                        bool edge = !IsShapePixel(shape, x - 1, y) ||
                                    !IsShapePixel(shape, x + 1, y) ||
                                    !IsShapePixel(shape, x, y - 1) ||
                                    !IsShapePixel(shape, x, y + 1);
                        color = edge ? outline : (y >= 22 && x >= 8 ? accent : fill);
                    }

                    // Texture pixel rows start at the bottom; polygon coordinates start at the top.
                    int textureY = TextureHeight - 1 - y;
                    pixels[(textureY * TextureWidth) + x] = color;
                }
            }

            cursorTexture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                name = "RoweMod Owned Cursor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            cursorTexture.SetPixels(pixels);
            cursorTexture.Apply(false, true);
        }

        private static bool IsShapePixel(bool[,] shape, int x, int y)
        {
            return x >= 0 && x < TextureWidth && y >= 0 && y < TextureHeight && shape[x, y];
        }

        private static bool IsInsidePolygon(float x, float y)
        {
            bool inside = false;
            int previous = CursorPolygon.Length - 1;
            for (int current = 0; current < CursorPolygon.Length; current++)
            {
                Vector2 a = CursorPolygon[current];
                Vector2 b = CursorPolygon[previous];
                bool crosses = (a.y > y) != (b.y > y) &&
                               x < ((b.x - a.x) * (y - a.y) / (b.y - a.y)) + a.x;
                if (crosses)
                    inside = !inside;

                previous = current;
            }

            return inside;
        }
    }
}
