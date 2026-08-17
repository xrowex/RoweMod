using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using Newtonsoft.Json;
using rowemod.Utils;

namespace rowemod.Mods
{
    [Serializable]
    internal sealed class RuntimeVehicleTunePreset
    {
        public string name;
        public string sourceSettings;
        // Optional human-readable explanation for shipped and player-created presets.
        // Older preset files omit this safely.
        public string description;
        public Dictionary<string, RuntimeVehicleTunePresetValue> values =
            new Dictionary<string, RuntimeVehicleTunePresetValue>(StringComparer.Ordinal);

        private static bool _builtInsEnsured;

        public static string PresetDirectory => Path.Combine(
            MelonEnvironment.ModsDirectory,
            "RoweMod",
            "VehicleTunePresets");

        public static bool Save(RuntimeVehicleTunePreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.name))
                return false;

            try
            {
                Directory.CreateDirectory(PresetDirectory);
                string safeName = MakeSafeFileName(preset.name.Trim());
                if (string.IsNullOrWhiteSpace(safeName))
                    return false;

                preset.name = safeName;
                string path = Path.Combine(PresetDirectory, safeName + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(preset, Formatting.Indented));
                Log.Msg($"[RuntimeVehiclePreset] Saved '{safeName}' with {preset.values?.Count ?? 0} values.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RuntimeVehiclePreset] Save failed: {ex.Message}");
                return false;
            }
        }

        public static RuntimeVehicleTunePreset Load(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                return null;

            try
            {
                string path = Path.Combine(PresetDirectory, MakeSafeFileName(presetName) + ".json");
                if (!File.Exists(path))
                    return null;

                RuntimeVehicleTunePreset preset =
                    JsonConvert.DeserializeObject<RuntimeVehicleTunePreset>(File.ReadAllText(path));
                if (preset == null)
                    return null;

                preset.values ??= new Dictionary<string, RuntimeVehicleTunePresetValue>(StringComparer.Ordinal);
                return preset;
            }
            catch (Exception ex)
            {
                Log.Error($"[RuntimeVehiclePreset] Load failed for '{presetName}': {ex.Message}");
                return null;
            }
        }

        public static List<string> GetAvailablePresets()
        {
            var result = new List<string>();
            try
            {
                EnsureBuiltInPresets();
                if (!Directory.Exists(PresetDirectory))
                    return result;

                foreach (string path in Directory.GetFiles(PresetDirectory, "*.json"))
                    result.Add(Path.GetFileNameWithoutExtension(path));
                result.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Error($"[RuntimeVehiclePreset] Could not list presets: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Ships a few safe starting points without ever replacing a player's own preset file.
        /// The values use the same stable inspector paths saved by the vehicle tuning menu.
        /// </summary>
        private static void EnsureBuiltInPresets()
        {
            if (_builtInsEnsured)
                return;

            _builtInsEnsured = true;
            try
            {
                Directory.CreateDirectory(PresetDirectory);
                int created = 0;
                foreach (RuntimeVehicleTunePreset preset in CreateBuiltInPresets())
                {
                    string path = Path.Combine(PresetDirectory, MakeSafeFileName(preset.name) + ".json");
                    if (File.Exists(path))
                        continue;

                    File.WriteAllText(path, JsonConvert.SerializeObject(preset, Formatting.Indented));
                    created++;
                }

                if (created > 0)
                    Log.Msg($"[RuntimeVehiclePreset] Added {created} built-in vehicle preset(s).");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RuntimeVehiclePreset] Could not add built-in presets: {ex.Message}");
            }
        }

        private static IEnumerable<RuntimeVehicleTunePreset> CreateBuiltInPresets()
        {
            yield return CreatePreset(
                "Vert",
                "High speed and hop with stronger in-air rotation and responsive landing assist. " +
                "The grind-pose values only affect how quickly the pose visually settles.",
                new Dictionary<string, RuntimeVehicleTunePresetValue>(StringComparer.Ordinal)
            {
                ["Top Speed"] = Float(12f),
                ["Terminal Velocity"] = Float(80f),
                ["Acceleration Force"] = Float(1f),
                ["Spin Land Assist Scale"] = Float(0.991f),
                ["Spin Land Assist Prediction Time Scale"] = Float(0.6847599f),
                ["Spin Land Assist Input Angle"] = Float(90f),
                ["Spin Land Assist Max Apply Angle"] = Float(180f),
                ["Engine Settings/Force Factor"] = Float(0.12f),
                ["Engine Settings/Max Speed"] = Float(7.5f),
                ["Engine Settings/Max Force"] = Float(10f),
                ["Air Control Settings/Spin Torque"] = Float(0.28f),
                ["Air Control Settings/Flip Torque"] = Float(0.28f),
                ["Air Control Settings/Max Angular"] = Float(12f),
                ["Air Control Settings/Air Angular Drag"] = Float(1.5240083f),
                ["Jump Settings/Vel"] = Float(5.2f),
                ["Grinding Settings/Pose Lerp Speed Air"] = Float(12f),
                ["Grinding Settings/Pose Lerp Speed Connected"] = Float(3f)
            });

            yield return CreatePreset(
                "Test - Street Balanced",
                "A neutral street baseline: moderate speed, pop, and air rotation without the loose feel of the Flow profile.",
                new Dictionary<string, RuntimeVehicleTunePresetValue>(StringComparer.Ordinal)
            {
                ["Top Speed"] = Float(9.5f),
                ["Terminal Velocity"] = Float(55f),
                ["Acceleration Force"] = Float(0.82f),
                ["Engine Settings/Force Factor"] = Float(0.085f),
                ["Engine Settings/Max Speed"] = Float(6.5f),
                ["Engine Settings/Max Force"] = Float(8f),
                ["Air Control Settings/Spin Torque"] = Float(0.23f),
                ["Air Control Settings/Flip Torque"] = Float(0.23f),
                ["Air Control Settings/Air Angular Drag"] = Float(2.2f),
                ["Jump Settings/Vel"] = Float(4.5f)
            });

            yield return CreatePreset(
                "Test - Flow & Speed",
                "More pace, pop, and freer rotation for lines and larger transitions. Lower air drag lets rotation carry longer.",
                new Dictionary<string, RuntimeVehicleTunePresetValue>(StringComparer.Ordinal)
            {
                ["Top Speed"] = Float(14f),
                ["Terminal Velocity"] = Float(95f),
                ["Acceleration Force"] = Float(1.18f),
                ["Engine Settings/Force Factor"] = Float(0.15f),
                ["Engine Settings/Max Speed"] = Float(9f),
                ["Engine Settings/Max Force"] = Float(13f),
                ["Air Control Settings/Spin Torque"] = Float(0.32f),
                ["Air Control Settings/Flip Torque"] = Float(0.32f),
                ["Air Control Settings/Air Angular Drag"] = Float(1.2f),
                ["Jump Settings/Vel"] = Float(5.8f)
            });

            yield return CreatePreset(
                "Test - Tech Control",
                "Lower speed and pop with more air damping for tighter technical riding and easier rotation stops.",
                new Dictionary<string, RuntimeVehicleTunePresetValue>(StringComparer.Ordinal)
            {
                ["Top Speed"] = Float(8f),
                ["Terminal Velocity"] = Float(45f),
                ["Acceleration Force"] = Float(0.68f),
                ["Engine Settings/Force Factor"] = Float(0.07f),
                ["Engine Settings/Max Speed"] = Float(5.5f),
                ["Engine Settings/Max Force"] = Float(6.5f),
                ["Air Control Settings/Spin Torque"] = Float(0.19f),
                ["Air Control Settings/Flip Torque"] = Float(0.19f),
                ["Air Control Settings/Air Angular Drag"] = Float(2.8f),
                ["Jump Settings/Vel"] = Float(4f)
            });
        }

        private static RuntimeVehicleTunePreset CreatePreset(
            string name,
            string description,
            Dictionary<string, RuntimeVehicleTunePresetValue> values)
        {
            return new RuntimeVehicleTunePreset
            {
                name = name,
                sourceSettings = "MotorVehicleSettings_ProtoBMX",
                description = description,
                values = values
            };
        }

        /// <summary>
        /// Shipped presets used to have no description field. Keep an in-code fallback so
        /// existing files remain understandable and are never overwritten on update.
        /// </summary>
        public static string GetDescription(RuntimeVehicleTunePreset preset)
        {
            if (!string.IsNullOrWhiteSpace(preset?.description))
                return preset.description;

            switch (preset?.name)
            {
                case "Vert":
                    return "High speed and hop with stronger in-air rotation and responsive landing assist.";
                case "Test - Street Balanced":
                    return "A neutral street baseline: moderate speed, pop, and air rotation.";
                case "Test - Flow & Speed":
                    return "More pace, pop, and freer rotation for lines and larger transitions.";
                case "Test - Tech Control":
                    return "Lower speed and pop with more air damping for tighter technical riding.";
                default:
                    return "Player-saved vehicle tuning preset.";
            }
        }

        private static RuntimeVehicleTunePresetValue Float(float value)
        {
            return new RuntimeVehicleTunePresetValue
            {
                type = "System.Single",
                value = value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        public static bool Delete(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                return false;

            try
            {
                string path = Path.Combine(PresetDirectory, MakeSafeFileName(presetName) + ".json");
                if (!File.Exists(path))
                    return false;
                File.Delete(path);
                Log.Msg($"[RuntimeVehiclePreset] Deleted '{presetName}'.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[RuntimeVehiclePreset] Delete failed for '{presetName}': {ex.Message}");
                return false;
            }
        }

        private static string MakeSafeFileName(string value)
        {
            string safe = (value ?? string.Empty).Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safe = safe.Replace(invalid, '_');
            return safe;
        }
    }

    [Serializable]
    internal sealed class RuntimeVehicleTunePresetValue
    {
        public string type;
        public string value;
    }
}
