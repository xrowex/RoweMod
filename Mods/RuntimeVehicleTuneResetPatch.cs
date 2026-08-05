using System;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using rowemod.Utils;
using UnityEngine;
using Il2CppFieldInfo = Il2CppSystem.Reflection.FieldInfo;
using Il2CppObject = Il2CppSystem.Object;
using Il2CppType = Il2CppSystem.Type;

namespace rowemod.Mods
{
    /// <summary>
    /// Draws per-value Reset buttons over the spare right-hand column in the
    /// game's Ctrl+Shift+U vehicle inspector. The game's inspector is native
    /// IL2CPP code, so its internal Draw* calls do not pass through managed
    /// Harmony wrappers. Rendering from RoweMod's OnGUI callback is reliable.
    /// </summary>
    internal static class RuntimeVehicleTuneResetSupport
    {
        private const float ReferenceHeight = 1080f;
        private const float MinimumScale = 0.72f;
        private const float PanelInsetX = 16f;
        private const float PanelInsetY = 14f;
        private const float InspectorTop = 110f;
        private const float RowPitch = 38f;
        private const float ResetButtonWidth = 82f;
        private const float ResetButtonHeight = 30f;
        private const float ResetButtonRightMargin = 34f;
        private const int MaxObjectDepth = 4;

        private static RuntimeVehicleTuneMenu _menu;
        private static MotorVehicleSettings _defaultSettings;
        private static int _settingsInstanceId = int.MinValue;
        private static bool _loggedOverlay;

        public static void ResetCapturedDefaults()
        {
            if (_defaultSettings != null)
                UnityEngine.Object.Destroy(_defaultSettings);

            _defaultSettings = null;
            _settingsInstanceId = int.MinValue;
            _loggedOverlay = false;
        }

        public static void DrawOverlay()
        {
            RuntimeVehicleTuneMenu menu = ResolveMenu();
            if (menu == null || !menu._isOpen || menu._currentSettings == null)
                return;

            if (!EnsureDefaultSnapshot(menu))
                return;

            float scale = Mathf.Max(MinimumScale, Screen.height / ReferenceHeight);
            Rect panel = menu._panelRect;
            if (panel.width <= 0f || panel.height <= 0f)
                return;

            Rect clip = new Rect(
                panel.x + PanelInsetX,
                panel.y + PanelInsetY + InspectorTop,
                Mathf.Max(1f, panel.width - (PanelInsetX * 2f)),
                Mathf.Max(1f, panel.height - (PanelInsetY * 2f) - InspectorTop));

            Matrix4x4 previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth;
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            Color previousBackgroundColor = GUI.backgroundColor;

            try
            {
                GUI.depth = -1000;
                GUI.matrix = Matrix4x4.TRS(
                    Vector3.zero,
                    Quaternion.identity,
                    new Vector3(scale, scale, 1f));

                GUI.BeginGroup(clip);
                float y = -menu._scroll.y;
                Il2CppObject currentRoot = menu._currentSettings;
                Il2CppObject defaultRoot = _defaultSettings;
                Il2CppType rootType = Il2CppInterop.Runtime.Il2CppType.From(
                    typeof(MotorVehicleSettings));
                bool changed = DrawObjectRows(
                    menu,
                    string.Empty,
                    currentRoot,
                    defaultRoot,
                    rootType,
                    0,
                    clip.width,
                    ref y);
                GUI.EndGroup();

                if (changed)
                {
                    menu._textEdits?.Clear();
                    menu._currentVehicle?.ApplyRuntimeTuningSettings();
                }

                if (!_loggedOverlay && Event.current?.type == EventType.Repaint)
                {
                    _loggedOverlay = true;
                    Log.Msg(
                        $"[RuntimeVehicleReset] Overlay active; panel={panel}; " +
                        $"scroll={menu._scroll}; scale={scale:0.###}; rowsHeight={y + menu._scroll.y:0.#}");
                }
            }
            catch (Exception ex)
            {
                if (!_loggedOverlay)
                {
                    _loggedOverlay = true;
                    Log.Error($"[RuntimeVehicleReset] Overlay failed: {ex}");
                }
            }
            finally
            {
                GUI.enabled = previousEnabled;
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackgroundColor;
                GUI.depth = previousDepth;
                GUI.matrix = previousMatrix;
            }
        }

        private static RuntimeVehicleTuneMenu ResolveMenu()
        {
            if (_menu != null)
                return _menu;

            _menu = UnityEngine.Object.FindObjectOfType<RuntimeVehicleTuneMenu>();
            return _menu;
        }

        private static bool EnsureDefaultSnapshot(RuntimeVehicleTuneMenu menu)
        {
            int instanceId = menu._currentSettings.GetInstanceID();
            if (_defaultSettings != null && _settingsInstanceId == instanceId)
                return true;

            ResetCapturedDefaults();
            _settingsInstanceId = instanceId;

            try
            {
                _defaultSettings = UnityEngine.Object.Instantiate(menu._currentSettings);
                if (_defaultSettings == null)
                    return false;

                _defaultSettings.name = $"{menu._currentSettings.name}_RoweModDefaults";
                _defaultSettings.hideFlags = HideFlags.HideAndDontSave;
                Log.Msg(
                    $"[RuntimeVehicleReset] Captured defaults for " +
                    $"{menu._currentSettings.name} ({instanceId}).");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RuntimeVehicleReset] Could not capture defaults: {ex}");
                return false;
            }
        }

        private static bool DrawObjectRows(
            RuntimeVehicleTuneMenu menu,
            string parentPath,
            Il2CppObject currentTarget,
            Il2CppObject defaultTarget,
            Il2CppType type,
            int depth,
            float clipWidth,
            ref float y)
        {
            if (currentTarget == null || defaultTarget == null || type == null || depth > MaxObjectDepth)
                return false;

            bool changed = false;
            var fields = menu.GetEditableFields(type);
            if (fields == null)
                return false;

            for (int i = 0; i < fields.Length; i++)
            {
                Il2CppFieldInfo field = fields[i];
                if (field == null)
                    continue;

                string label = RuntimeVehicleTuneMenu.Nicify(field.Name);
                string path = string.IsNullOrEmpty(parentPath)
                    ? label
                    : $"{parentPath}/{label}";

                Il2CppObject currentValue = field.GetValue(currentTarget);
                Il2CppObject defaultValue = field.GetValue(defaultTarget);
                Il2CppType fieldType = field.FieldType;

                if (IsEditableLeaf(fieldType))
                {
                    if (DrawResetRow(menu, path, currentValue, defaultValue, clipWidth, y))
                    {
                        field.SetValue(currentTarget, defaultValue);
                        currentValue = defaultValue;
                        changed = true;
                    }

                    y += RowPitch;
                    continue;
                }

                // Native Unity object references, strings, curves, and null values
                // are label-only rows in this inspector.
                if (currentValue == null || IsLabelOnlyType(fieldType))
                {
                    y += RowPitch;
                    continue;
                }

                // Complex settings objects have a foldout header row. Reset their
                // editable descendants when expanded, matching the native row order.
                y += RowPitch;
                if (!menu.GetFoldout(path) || depth >= MaxObjectDepth || defaultValue == null)
                    continue;

                bool childChanged = DrawObjectRows(
                    menu,
                    path,
                    currentValue,
                    defaultValue,
                    fieldType,
                    depth + 1,
                    clipWidth,
                    ref y);

                if (!childChanged)
                    continue;

                // Nested structs are returned boxed; writing the updated box back
                // propagates descendant resets to the parent object.
                if (fieldType.IsValueType)
                    field.SetValue(currentTarget, currentValue);

                changed = true;
            }

            return changed;
        }

        private static bool DrawResetRow(
            RuntimeVehicleTuneMenu menu,
            string path,
            Il2CppObject currentValue,
            Il2CppObject defaultValue,
            float clipWidth,
            float y)
        {
            if (defaultValue == null)
                return false;

            float x = Mathf.Max(0f, clipWidth - ResetButtonRightMargin - ResetButtonWidth);
            Rect buttonRect = new Rect(
                x,
                y + ((RowPitch - ResetButtonHeight) * 0.5f),
                ResetButtonWidth,
                ResetButtonHeight);

            bool alreadyDefault = currentValue != null &&
                RuntimeVehicleTuneMenu.ValuesEqual(currentValue, defaultValue);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !alreadyDefault;
            bool pressed = GUI.Button(
                buttonRect,
                "Reset",
                menu._buttonStyle ?? GUI.skin.button);
            GUI.enabled = previousEnabled;

            if (pressed)
                Log.Msg($"[RuntimeVehicleReset] Restored {path}.");

            return pressed;
        }

        private static bool IsEditableLeaf(Il2CppType type)
        {
            if (type == null)
                return false;

            string name = type.FullName ?? string.Empty;
            return type.IsEnum ||
                name == "System.Single" ||
                name == "System.Int32" ||
                name == "System.Boolean" ||
                name == "UnityEngine.Vector2" ||
                name == "UnityEngine.Vector3" ||
                name == "UnityEngine.Vector4";
        }

        private static bool IsLabelOnlyType(Il2CppType type)
        {
            if (type == null)
                return true;

            string name = type.FullName ?? string.Empty;
            if (name == "System.String" || name == "UnityEngine.AnimationCurve")
                return true;

            for (Il2CppType cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                if ((cursor.FullName ?? string.Empty) == "UnityEngine.Object")
                    return true;
            }

            return false;
        }
    }
}
