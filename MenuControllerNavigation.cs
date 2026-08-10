using System;
using System.Collections.Generic;
using rowemod.Utils;
using UnityEngine;

namespace rowemod
{
    public static partial class Menu
    {
        private enum ControllerTargetKind
        {
            Button,
            Toggle,
            Slider,
            Foldout
        }

        private struct ControllerTarget
        {
            public string Id;
            public ControllerTargetKind Kind;
            public bool IsScrollable;
            public float ContentTop;
            public float ContentBottom;
            public float ScrollOffset;
            public float ScrollViewHeight;
            public float ScrollMax;
            public Action<float> ScrollSetter;
        }

        private static readonly List<ControllerTarget> ControllerTargets = new List<ControllerTarget>();
        private static readonly Dictionary<string, int> ControllerTargetOccurrences =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private static string _controllerFocusedId;
        private static bool _controllerNavigationActive;
        private static bool _controllerActivateRequested;
        private static int _controllerHorizontalRequested;
        private static int _controllerRequestFrame = -1;
        private static bool _registeringScrollableControls;
        private static bool _controllerOverlayActive;
        private static string _controllerScopeOverride;
        private static float _controllerOverlayScrollOffset;
        private static float _controllerOverlayViewHeight;
        private static Action<float> _controllerOverlayScrollSetter;
        private static bool _suppressControllerFocusRepair;
        private static bool _controllerScrollRegionActive;
        private static float _controllerScrollRegionOffset;
        private static float _controllerScrollRegionViewHeight;
        private static float _controllerScrollRegionMax;
        private static Action<float> _controllerScrollRegionSetter;
        private static bool _controllerScrollRegionUsesLogicalRows;
        private static float _controllerLogicalRowLastY;
        private static float _controllerLogicalRowTop;
        private static float _controllerLogicalNextRowTop;
        private static GUIStyle controllerFocusStyle;

        public static bool IsControllerNavigationActive => _controllerNavigationActive;
        public static bool IsControllerOverlayActive => _controllerOverlayActive;
        public static int ControllerTargetCount => ControllerTargets.Count;
        public static string ControllerFocusedTargetId => _controllerFocusedId ?? "none";

        private static void BeginControllerNavigationFrame()
        {
            _suppressControllerFocusRepair = _controllerOverlayActive &&
                                             !string.IsNullOrEmpty(_controllerFocusedId) &&
                                             _controllerFocusedId.StartsWith("overlay:", StringComparison.Ordinal);
            ControllerTargets.Clear();
            ControllerTargetOccurrences.Clear();
            _controllerOverlayActive = false;
            _controllerScopeOverride = null;
            _controllerOverlayScrollSetter = null;
            _registeringScrollableControls = false;
            EndControllerScrollRegion();

            Event current = Event.current;
            if (current != null && current.type == EventType.MouseDown)
            {
                _controllerNavigationActive = false;
                _controllerActivateRequested = false;
                _controllerHorizontalRequested = 0;
            }
        }

        public static void BeginControllerOverlayFrame(string scope, float scrollOffset, float visibleHeight,
            Action<float> scrollSetter)
        {
            ControllerTargets.Clear();
            ControllerTargetOccurrences.Clear();
            _registeringScrollableControls = false;
            _controllerOverlayActive = true;
            _suppressControllerFocusRepair = false;
            _controllerScopeOverride = string.IsNullOrWhiteSpace(scope) ? "popup" : scope;
            _controllerOverlayScrollOffset = Mathf.Max(0f, scrollOffset);
            _controllerOverlayViewHeight = Mathf.Max(0f, visibleHeight);
            _controllerOverlayScrollSetter = scrollSetter;

            string expectedPrefix = $"overlay:{_controllerScopeOverride}:";
            if (string.IsNullOrEmpty(_controllerFocusedId) ||
                !_controllerFocusedId.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                _controllerFocusedId = null;
            }
        }

        public static void EndControllerOverlayFrame()
        {
            _registeringScrollableControls = false;
            EndControllerNavigationFrame();
            _controllerScopeOverride = null;
        }

        private static void EndControllerNavigationFrame()
        {
            if (!_suppressControllerFocusRepair && _controllerNavigationActive && ControllerTargets.Count > 0 &&
                FindControllerTargetIndex(_controllerFocusedId) < 0)
            {
                _controllerFocusedId = ControllerTargets[0].Id;
                EnsureControllerTargetVisible(ControllerTargets[0]);
            }

            if (_controllerRequestFrame >= 0 && Time.frameCount - _controllerRequestFrame > 2)
            {
                _controllerActivateRequested = false;
                _controllerHorizontalRequested = 0;
                _controllerRequestFrame = -1;
            }
        }

        public static void SetControllerScrollableContext(bool active)
        {
            _registeringScrollableControls = active;
        }

        public static void BeginControllerScrollRegion(float scrollOffset, float visibleHeight,
            float contentHeight, Action<float> scrollSetter, bool useLogicalRows = false)
        {
            _controllerScrollRegionActive = scrollSetter != null && visibleHeight > 0f;
            _controllerScrollRegionOffset = Mathf.Max(0f, scrollOffset);
            _controllerScrollRegionViewHeight = Mathf.Max(0f, visibleHeight);
            _controllerScrollRegionMax = Mathf.Max(0f, contentHeight - visibleHeight);
            _controllerScrollRegionSetter = scrollSetter;
            _controllerScrollRegionUsesLogicalRows = useLogicalRows;
            _controllerLogicalRowLastY = float.NaN;
            _controllerLogicalRowTop = 0f;
            _controllerLogicalNextRowTop = 0f;
        }

        public static void EndControllerScrollRegion()
        {
            _controllerScrollRegionActive = false;
            _controllerScrollRegionOffset = 0f;
            _controllerScrollRegionViewHeight = 0f;
            _controllerScrollRegionMax = 0f;
            _controllerScrollRegionSetter = null;
            _controllerScrollRegionUsesLogicalRows = false;
            _controllerLogicalRowLastY = float.NaN;
            _controllerLogicalRowTop = 0f;
            _controllerLogicalNextRowTop = 0f;
        }

        public static void ResetControllerNavigation(bool preserveControllerMode = true,
            bool preserveFocusedTarget = false)
        {
            if (!preserveFocusedTarget)
                _controllerFocusedId = null;
            _controllerActivateRequested = false;
            _controllerHorizontalRequested = 0;
            _controllerRequestFrame = -1;
            _suppressControllerFocusRepair = false;
            EndControllerScrollRegion();
            ControllerTargets.Clear();
            ControllerTargetOccurrences.Clear();

            if (!preserveControllerMode)
                _controllerNavigationActive = false;
        }

        public static void MoveControllerFocus(int direction)
        {
            MoveControllerFocus(direction, true, float.PositiveInfinity);
        }

        public static void MoveControllerOverlayFocus(int direction, float maximumAutoScrollStep)
        {
            MoveControllerFocus(direction, false, Mathf.Max(0f, maximumAutoScrollStep));
        }

        private static void MoveControllerFocus(int direction, bool wrap, float maximumAutoScrollStep)
        {
            _controllerNavigationActive = true;
            if (direction == 0 || ControllerTargets.Count == 0)
                return;

            int currentIndex = FindControllerTargetIndex(_controllerFocusedId);
            if (currentIndex < 0)
                currentIndex = direction > 0 ? -1 : 0;

            int nextIndex = currentIndex + direction;
            nextIndex = wrap
                ? (nextIndex + ControllerTargets.Count) % ControllerTargets.Count
                : Mathf.Clamp(nextIndex, 0, ControllerTargets.Count - 1);
            ControllerTarget nextTarget = ControllerTargets[nextIndex];
            _controllerFocusedId = nextTarget.Id;
            _controllerActivateRequested = false;
            _controllerHorizontalRequested = 0;
            if (nextTarget.Id.StartsWith("overlay:vehicle-tuning:", StringComparison.Ordinal))
            {
                Log.Msg(
                    $"[VehicleTuningController] focus {currentIndex}->{nextIndex}/{ControllerTargets.Count - 1}; " +
                    $"id={nextTarget.Id}; row={nextTarget.ContentTop:0}-{nextTarget.ContentBottom:0}; " +
                    $"scroll={nextTarget.ScrollOffset:0}/{nextTarget.ScrollMax:0}; view={nextTarget.ScrollViewHeight:0}.");
            }
            EnsureControllerTargetVisible(nextTarget, maximumAutoScrollStep);
        }

        public static void RequestControllerActivation()
        {
            _controllerNavigationActive = true;
            if (ControllerTargets.Count == 0)
                return;

            if (FindControllerTargetIndex(_controllerFocusedId) < 0)
                _controllerFocusedId = ControllerTargets[0].Id;

            _controllerActivateRequested = true;
            _controllerRequestFrame = Time.frameCount;
        }

        public static void AdjustControllerFocusedControl(int direction)
        {
            _controllerNavigationActive = true;
            if (direction == 0 || ControllerTargets.Count == 0)
                return;

            int targetIndex = FindControllerTargetIndex(_controllerFocusedId);
            if (targetIndex < 0)
            {
                _controllerFocusedId = ControllerTargets[0].Id;
                targetIndex = 0;
            }

            ControllerTarget target = ControllerTargets[targetIndex];
            if (target.Kind == ControllerTargetKind.Slider || target.Kind == ControllerTargetKind.Toggle)
            {
                _controllerHorizontalRequested = direction > 0 ? 1 : -1;
                _controllerRequestFrame = Time.frameCount;
                EnsureControllerTargetVisible(target);
                return;
            }

            MoveControllerFocus(direction);
        }

        private static int FindControllerTargetIndex(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return -1;

            for (int i = 0; i < ControllerTargets.Count; i++)
            {
                if (string.Equals(ControllerTargets[i].Id, targetId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static void EnsureControllerTargetVisible(
            ControllerTarget target,
            float maximumAutoScrollStep = float.PositiveInfinity)
        {
            if (!target.IsScrollable)
                return;

            float margin = Mathf.Max(8f, UiControlSpacing);
            if (target.ScrollSetter != null && target.ScrollViewHeight > 0f)
            {
                float offset = target.ScrollOffset;
                if (target.ContentTop < offset + margin)
                    offset = target.ContentTop - margin;
                else if (target.ContentBottom > offset + target.ScrollViewHeight - margin)
                    offset = target.ContentBottom - target.ScrollViewHeight + margin;

                if (!float.IsPositiveInfinity(maximumAutoScrollStep))
                {
                    offset = Mathf.Clamp(
                        offset,
                        target.ScrollOffset - maximumAutoScrollStep,
                        target.ScrollOffset + maximumAutoScrollStep);
                }

                offset = Mathf.Clamp(offset, 0f, target.ScrollMax);
                if (target.Id.StartsWith("overlay:vehicle-tuning:", StringComparison.Ordinal) &&
                    !Mathf.Approximately(offset, target.ScrollOffset))
                {
                    Log.Msg(
                        $"[VehicleTuningController] auto-scroll {target.ScrollOffset:0}->{offset:0}; " +
                        $"target={target.Id}; row={target.ContentTop:0}-{target.ContentBottom:0}; " +
                        $"max={target.ScrollMax:0}.");
                }
                target.ScrollSetter(offset);
                return;
            }

            if (_controllerOverlayActive && _controllerOverlayScrollSetter != null &&
                _controllerOverlayViewHeight > 0f)
            {
                if (target.ContentTop < _controllerOverlayScrollOffset + margin)
                    _controllerOverlayScrollOffset = Mathf.Max(0f, target.ContentTop - margin);
                else if (target.ContentBottom > _controllerOverlayScrollOffset + _controllerOverlayViewHeight - margin)
                    _controllerOverlayScrollOffset = Mathf.Max(
                        0f,
                        target.ContentBottom - _controllerOverlayViewHeight + margin);

                _controllerOverlayScrollSetter(_controllerOverlayScrollOffset);
                return;
            }

            if (viewHeight <= 0f)
                return;

            if (target.ContentTop < scrollOffset + margin)
                scrollOffset = Mathf.Max(0f, target.ContentTop - margin);
            else if (target.ContentBottom > scrollOffset + viewHeight - margin)
                scrollOffset = target.ContentBottom - viewHeight + margin;

            scrollOffset = Mathf.Clamp(scrollOffset, 0f, Mathf.Max(0f, scrollViewHeight - viewHeight));
        }

        private static string RegisterControllerTarget(string baseId, Rect rect, ControllerTargetKind kind)
        {
            string scope = !string.IsNullOrWhiteSpace(_controllerScopeOverride)
                ? $"overlay:{_controllerScopeOverride}"
                : !string.IsNullOrWhiteSpace(_menuSearch)
                    ? "search"
                    : ((int)_selectedPage).ToString();
            string stableBaseId = $"{scope}:{baseId}";
            ControllerTargetOccurrences.TryGetValue(stableBaseId, out int occurrence);
            ControllerTargetOccurrences[stableBaseId] = occurrence + 1;
            string id = $"{stableBaseId}:{occurrence}";

            float contentTop = rect.y;
            float contentBottom = rect.yMax;
            if (_controllerScrollRegionActive && _controllerScrollRegionUsesLogicalRows)
            {
                // BeginScrollView reports coordinates that can be translated by the
                // native clip region. For controller focus we need a stable order, not
                // those transient draw positions. Controls sharing a visual row keep the
                // same logical location; each following row advances by its actual height.
                if (float.IsNaN(_controllerLogicalRowLastY) ||
                    Mathf.Abs(rect.y - _controllerLogicalRowLastY) > 2f)
                {
                    _controllerLogicalRowTop = _controllerLogicalNextRowTop;
                    _controllerLogicalRowLastY = rect.y;
                    _controllerLogicalNextRowTop += Mathf.Max(30f, rect.height);
                }

                contentTop = _controllerLogicalRowTop;
                contentBottom = contentTop + Mathf.Max(30f, rect.height);
            }

            ControllerTargets.Add(new ControllerTarget
            {
                Id = id,
                Kind = kind,
                IsScrollable = _controllerScrollRegionActive || _registeringScrollableControls,
                ContentTop = contentTop,
                ContentBottom = contentBottom,
                ScrollOffset = _controllerScrollRegionOffset,
                ScrollViewHeight = _controllerScrollRegionViewHeight,
                ScrollMax = _controllerScrollRegionMax,
                ScrollSetter = _controllerScrollRegionActive ? _controllerScrollRegionSetter : null
            });

            return id;
        }

        public static bool AdjustControllerOverlayScroll(float delta)
        {
            if (!_controllerOverlayActive || _controllerOverlayScrollSetter == null)
                return false;

            _controllerOverlayScrollOffset = Mathf.Max(0f, _controllerOverlayScrollOffset + delta);
            _controllerOverlayScrollSetter(_controllerOverlayScrollOffset);
            return true;
        }

        public static bool AdjustControllerFocusedScroll(float delta)
        {
            int targetIndex = FindControllerTargetIndex(_controllerFocusedId);
            if (targetIndex < 0)
                return false;

            ControllerTarget target = ControllerTargets[targetIndex];
            if (target.ScrollSetter == null || target.ScrollViewHeight <= 0f)
                return false;

            // The right stick may browse the list, but it must never strand the
            // controller focus outside the clip area.  Previously this path could
            // move the viewport freely and focus was only repaired on the next
            // D-pad move, which is why Vehicle Tuning appeared to jump away from
            // the selected row.
            float margin = Mathf.Max(8f, UiControlSpacing);
            float minimumVisibleOffset = Mathf.Max(
                0f,
                target.ContentBottom - target.ScrollViewHeight + margin);
            float maximumVisibleOffset = Mathf.Min(
                target.ScrollMax,
                target.ContentTop - margin);

            // A control taller than the viewport cannot satisfy both edges. Keep
            // its leading edge in view rather than allowing an invalid range.
            if (minimumVisibleOffset > maximumVisibleOffset)
            {
                minimumVisibleOffset = Mathf.Clamp(target.ContentTop - margin, 0f, target.ScrollMax);
                maximumVisibleOffset = minimumVisibleOffset;
            }

            float offset = Mathf.Clamp(
                target.ScrollOffset + delta,
                minimumVisibleOffset,
                maximumVisibleOffset);
            target.ScrollSetter(offset);
            return true;
        }

        private static bool ConsumeControllerActivation(string id)
        {
            if (!_controllerNavigationActive || !_controllerActivateRequested ||
                !string.Equals(_controllerFocusedId, id, StringComparison.Ordinal) ||
                Event.current == null || Event.current.type != EventType.Layout)
            {
                return false;
            }

            _controllerActivateRequested = false;
            _controllerRequestFrame = -1;
            return true;
        }

        private static int ConsumeControllerHorizontal(string id)
        {
            if (!_controllerNavigationActive || _controllerHorizontalRequested == 0 ||
                !string.Equals(_controllerFocusedId, id, StringComparison.Ordinal) ||
                Event.current == null || Event.current.type != EventType.Layout)
            {
                return 0;
            }

            int direction = _controllerHorizontalRequested;
            _controllerHorizontalRequested = 0;
            _controllerRequestFrame = -1;
            return direction;
        }

        private static void DrawControllerFocusRing(string id, Rect rect)
        {
            if (!_controllerNavigationActive || !string.Equals(_controllerFocusedId, id, StringComparison.Ordinal) ||
                Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Rect ringRect = new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f);
            if (controllerFocusStyle != null)
            {
                GUI.Box(ringRect, GUIContent.none, controllerFocusStyle);
                return;
            }

            Color focusColor = new Color(0.40f, 0.72f, 1f, 1f);
            DrawSolidColorRect(new Rect(ringRect.x, ringRect.y, ringRect.width, 2f), focusColor);
            DrawSolidColorRect(new Rect(ringRect.x, ringRect.yMax - 2f, ringRect.width, 2f), focusColor);
            DrawSolidColorRect(new Rect(ringRect.x, ringRect.y, 2f, ringRect.height), focusColor);
            DrawSolidColorRect(new Rect(ringRect.xMax - 2f, ringRect.y, 2f, ringRect.height), focusColor);
        }

        private static void InitializeControllerNavigationStyles()
        {
            controllerFocusStyle = new GUIStyle(GUI.skin.box);
            controllerFocusStyle.normal.background = MakeStyleRoundedTex(
                48,
                36,
                Color.clear,
                10,
                2,
                new Color(0.40f, 0.72f, 1f, 1f));
            controllerFocusStyle.border = new RectOffset(10, 10, 10, 10);
            controllerFocusStyle.padding = new RectOffset(0, 0, 0, 0);
            controllerFocusStyle.margin = new RectOffset(0, 0, 0, 0);
        }

        public static bool ControllerButton(string label, GUIStyle style, params GUILayoutOption[] options)
        {
            return ControllerButton(label, label, style, options);
        }

        public static bool ControllerButton(string controlId, string label, GUIStyle style,
            params GUILayoutOption[] options)
        {
            GUIStyle resolvedStyle = style ?? GUI.skin.button;
            GUIContent content = new GUIContent(label);
            Rect rect = GUILayoutUtility.GetRect(content, resolvedStyle, options);
            return ControllerButton(rect, controlId, content, resolvedStyle);
        }

        public static bool ControllerButton(Rect rect, string controlId, string label, GUIStyle style)
        {
            return ControllerButton(rect, controlId, new GUIContent(label), style ?? GUI.skin.button);
        }

        public static float ControllerSlider(string controlId, float value, float minimum, float maximum,
            params GUILayoutOption[] options)
        {
            return ControllerSlider(controlId, value, minimum, maximum, out _, options);
        }

        public static float ControllerSlider(string controlId, float value, float minimum, float maximum,
            out bool activated, params GUILayoutOption[] options)
        {
            GUIStyle sliderStyle = horizontalSliderStyle ?? GUI.skin.horizontalSlider;
            GUIStyle thumbStyle = horizontalSliderThumbStyle ?? GUI.skin.horizontalSliderThumb;
            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                sliderStyle,
                options);
            string id = RegisterControllerSlider(controlId, rect);
            float next = GUI.HorizontalSlider(rect, value, minimum, maximum, sliderStyle, thumbStyle);
            int direction = ConsumeControllerHorizontal(id);
            if (direction != 0)
            {
                float step = GetControllerSliderStep(minimum, maximum);
                next = Mathf.Clamp(next + (direction * step), minimum, maximum);
            }

            DrawControllerFocusRing(id, rect);
            activated = ConsumeControllerActivation(id);
            return next;
        }

        public static bool FocusControllerTarget(string controlId)
        {
            if (string.IsNullOrWhiteSpace(controlId))
                return false;

            string suffix = $":{controlId}:";
            for (int i = 0; i < ControllerTargets.Count; i++)
            {
                ControllerTarget target = ControllerTargets[i];
                if (!target.Id.Contains(suffix, StringComparison.Ordinal))
                    continue;

                _controllerNavigationActive = true;
                _controllerFocusedId = target.Id;
                _controllerActivateRequested = false;
                _controllerHorizontalRequested = 0;
                EnsureControllerTargetVisible(target);
                return true;
            }

            return false;
        }

        public static int ControllerStepControl(string controlId, Rect rect)
        {
            string id = RegisterControllerSlider(controlId, rect);
            int direction = ConsumeControllerHorizontal(id);
            DrawControllerFocusRing(id, rect);
            return direction;
        }

        private static bool ControllerButton(Rect rect, string controlId, GUIContent content, GUIStyle style)
        {
            string id = GUI.enabled
                ? RegisterControllerTarget(controlId, rect, ControllerTargetKind.Button)
                : null;
            bool clicked = GUI.Button(rect, content, style);
            bool activated = GUI.enabled && ConsumeControllerActivation(id);
            if (GUI.enabled)
                DrawControllerFocusRing(id, rect);

            if (clicked)
                _controllerNavigationActive = false;

            return clicked || activated;
        }

        private static string RegisterControllerToggle(string controlId, Rect rect)
        {
            return RegisterControllerTarget(controlId, rect, ControllerTargetKind.Toggle);
        }

        private static string RegisterControllerSlider(string controlId, Rect rect)
        {
            return RegisterControllerTarget(controlId, rect, ControllerTargetKind.Slider);
        }

        private static string RegisterControllerFoldout(string controlId, Rect rect)
        {
            return RegisterControllerTarget(controlId, rect, ControllerTargetKind.Foldout);
        }

        private static float GetControllerSliderStep(float min, float max)
        {
            float range = Mathf.Abs(max - min);
            if (range <= 1f)
                return 0.01f;
            if (range <= 10f)
                return 0.05f;
            if (range <= 100f)
                return 1f;
            if (range <= 500f)
                return 5f;

            return Mathf.Max(0.01f, range / 100f);
        }
    }
}
