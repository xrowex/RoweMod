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
        public Dictionary<string, RuntimeVehicleTunePresetValue> values =
            new Dictionary<string, RuntimeVehicleTunePresetValue>(StringComparer.Ordinal);

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
