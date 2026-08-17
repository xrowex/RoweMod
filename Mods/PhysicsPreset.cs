using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace rowemod.Mods
{
    [Serializable]
    internal sealed class PhysicsPreset
    {
        public string name;
        public global::rowemod.Physics settings;

        private static Vector2 _scroll;
        private static string _newPresetName = string.Empty;
        private static int _selectedPresetIndex;

        private static string PresetDirectory =>
            Path.Combine(MelonEnvironment.ModsDirectory, "RoweMod", "PhysicsPresets");

        internal static void DrawControls()
        {
            Menu.BeginAltPane("Physics Presets", "Save your full handling setup, including Small Hop, gravity, pump, spin, manuals, and speed settings.");

            GUILayout.BeginHorizontal();
            _newPresetName = GUILayout.TextField(_newPresetName, 32, Menu.UiSearchFieldStyle,
                GUILayout.MinWidth(180f), GUILayout.ExpandWidth(true));
            if (Menu.PrimaryButton("Save Current", GUILayout.Width(130f), GUILayout.Height(26f)))
            {
                string name = (_newPresetName ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Save(name);
                    _newPresetName = string.Empty;
                    GUI.FocusControl(null);
                }
            }
            GUILayout.EndHorizontal();

            List<string> presets = GetNames();
            if (presets.Count == 0)
            {
                GUILayout.Label("No Physics presets saved yet.", Menu.UiMutedWrappedStyle);
                Menu.EndPane();
                return;
            }

            _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, 0, presets.Count - 1);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(112f));
            bool deleted = false;
            for (int i = 0; i < presets.Count; i++)
            {
                string name = presets[i];
                GUIStyle style = i == _selectedPresetIndex ? Menu.UiRowButtonSelectedStyle : Menu.UiRowButtonStyle;
                GUILayout.BeginHorizontal();
                if (Menu.ControllerButton($"physics_preset_{name}", name, style,
                        GUILayout.Height(32f * Menu.EffectiveUiScale), GUILayout.ExpandWidth(true)))
                {
                    _selectedPresetIndex = i;
                    Load(name);
                }

                if (Menu.ControllerButton($"delete_physics_preset_{name}", "X", Menu.redButtonStyle,
                        GUILayout.Width(36f * Menu.EffectiveUiScale), GUILayout.Height(32f * Menu.EffectiveUiScale)))
                {
                    Delete(name);
                    deleted = true;
                    _selectedPresetIndex = Mathf.Max(0, _selectedPresetIndex - 1);
                }
                GUILayout.EndHorizontal();

                if (deleted)
                    break;
            }
            GUILayout.EndScrollView();
            Menu.EndPane();
        }

        private static void Save(string name)
        {
            try
            {
                Directory.CreateDirectory(PresetDirectory);
                var preset = new PhysicsPreset
                {
                    name = name,
                    settings = Config.physics
                };
                File.WriteAllText(PathFor(name), JsonConvert.SerializeObject(preset, Formatting.Indented));
                rowemod.Utils.Log.Msg($"[Physics Presets] Saved '{name}'.");
            }
            catch (Exception ex)
            {
                rowemod.Utils.Log.Warning($"[Physics Presets] Could not save '{name}': {ex.Message}");
            }
        }

        private static void Load(string name)
        {
            try
            {
                string path = PathFor(name);
                if (!File.Exists(path))
                    return;

                PhysicsPreset preset = JsonConvert.DeserializeObject<PhysicsPreset>(File.ReadAllText(path));
                if (preset == null)
                    return;

                // Vehicle selection is session state, not part of a handling preset.
                int activeVehicle = Config.physics.lastVehicle;
                Config.physics = preset.settings;
                Config.physics.lastVehicle = activeVehicle;
                Config.physics.smallHopForce = Mathf.Clamp(Config.physics.smallHopForce, 0f, 25f);
                Config.physics.gravity = Mathf.Clamp(Config.physics.gravity, 0f, 30f);
                Config.physics.grindPoseLerpSpeed = Mathf.Clamp(Config.physics.grindPoseLerpSpeed, 0.05f, 10f);

                Physics.Update();
                Config.RequestSave();
                rowemod.Utils.Log.Msg($"[Physics Presets] Loaded '{name}'.");
            }
            catch (Exception ex)
            {
                rowemod.Utils.Log.Warning($"[Physics Presets] Could not load '{name}': {ex.Message}");
            }
        }

        private static List<string> GetNames()
        {
            if (!Directory.Exists(PresetDirectory))
                return new List<string>();

            var names = new List<string>();
            foreach (string path in Directory.GetFiles(PresetDirectory, "*.json"))
                names.Add(Path.GetFileNameWithoutExtension(path));

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static void Delete(string name)
        {
            try
            {
                string path = PathFor(name);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                rowemod.Utils.Log.Warning($"[Physics Presets] Could not delete '{name}': {ex.Message}");
            }
        }

        private static string PathFor(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return Path.Combine(PresetDirectory, $"{name}.json");
        }
    }
}
