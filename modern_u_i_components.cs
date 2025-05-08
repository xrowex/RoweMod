namespace rowemod.Utils
{
    public static class GUIHelpers
    {
        public static bool ModernToggle(Rect fullRect, string label, ref bool value, ref Dictionary<string, float> toggleAnimationState, GUIStyle labelStyle)
        {
            float width = 50f;
            float height = 25f;
            float knobSize = 20f;
            float padding = 2f;

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

        public static void ModernSlider(Rect fullRect, string label, ref float target, float min, float max, GUIStyle labelStyle)
        {
            float height = 25f;
            float labelWidth = 150f;
            float valueBoxWidth = 50f;
            float spacing = 15f;

            Rect labelRect = new Rect(fullRect.x, fullRect.y, labelWidth, height);
            Rect sliderRect = new Rect(fullRect.x + labelWidth + spacing, fullRect.y + 6, fullRect.width - labelWidth - valueBoxWidth - spacing * 4, height - 12f);
            Rect valueRect = new Rect(fullRect.x + labelWidth + spacing + sliderRect.width + spacing, fullRect.y, valueBoxWidth, height);

            // Label
            GUI.Label(labelRect, label, labelStyle);

            // Slider background & fill
            DrawSolidColorRect(sliderRect, new Color(0.25f, 0.25f, 0.25f));
            float percent = Mathf.InverseLerp(min, max, target);
            Rect fillRect = new Rect(sliderRect.x, sliderRect.y, sliderRect.width * percent, sliderRect.height);
            DrawSolidColorRect(fillRect, new Color(0.2f, 0.6f, 1f));

            // Thumb
            float thumbX = Mathf.Lerp(sliderRect.x, sliderRect.xMax - 10f, percent);
            Rect thumbRect = new Rect(thumbX - 5f, sliderRect.y - 2f, 10f, sliderRect.height + 4f);
            DrawSolidColorRect(thumbRect, Color.white);

            if (Event.current.type == EventType.MouseDown && sliderRect.Contains(Event.current.mousePosition))
            {
                float newPercent = Mathf.InverseLerp(sliderRect.x, sliderRect.xMax, Event.current.mousePosition.x);
                target = Mathf.Lerp(min, max, newPercent);
                Event.current.Use();
            }

            if (Event.current.type == EventType.MouseDrag && sliderRect.Contains(Event.current.mousePosition))
            {
                float newPercent = Mathf.InverseLerp(sliderRect.x, sliderRect.xMax, Event.current.mousePosition.x);
                target = Mathf.Lerp(min, max, newPercent);
                Event.current.Use();
            }

            // Value label
            GUI.Label(valueRect, target.ToString("0.00"), labelStyle);
        }

        private static void DrawSolidColorRect(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, GetWhiteTexture());
            GUI.color = oldColor;
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
    }
}