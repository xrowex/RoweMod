using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using Newtonsoft.Json;
using rowemod.Utils;

namespace rowemod.Mods
{
    [Serializable]
    public class GrindPosePreset
    {
        public string name;
        public float grindPoseLerpSpeed = 2f;
        public GrindPoseSettings grindPoseData = new GrindPoseSettings();

        public static string presetDirectory => Path.Combine(MelonEnvironment.ModsDirectory, "RoweMod", "GrindPosePresets");

        public static void Save(GrindPosePreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.name))
            {
                Log.Warning("Cannot save grind pose preset: missing preset name.");
                return;
            }

            Directory.CreateDirectory(presetDirectory);

            string filePath = Path.Combine(presetDirectory, $"{preset.name}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(preset, Formatting.Indented));
            Log.Msg($"Grind pose preset '{preset.name}' saved.");
        }

        public static GrindPosePreset Load(string presetName)
        {
            string filePath = Path.Combine(presetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Warning($"Grind pose preset '{presetName}' not found.");
                return null;
            }

            GrindPosePreset preset = JsonConvert.DeserializeObject<GrindPosePreset>(File.ReadAllText(filePath));
            if (preset == null)
            {
                Log.Warning($"Failed to parse grind pose preset '{presetName}'.");
                return null;
            }

            preset.grindPoseData ??= new GrindPoseSettings();
            preset.grindPoseData.poses ??= new Dictionary<string, GrindPoseConfigEntry>();
            return preset;
        }

        public static List<string> GetAvailablePresets()
        {
            if (!Directory.Exists(presetDirectory))
            {
                return new List<string>();
            }

            List<string> presets = new List<string>();
            foreach (string file in Directory.GetFiles(presetDirectory, "*.json"))
            {
                presets.Add(Path.GetFileNameWithoutExtension(file));
            }

            presets.Sort(StringComparer.OrdinalIgnoreCase);
            return presets;
        }

        public static bool Delete(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
            {
                Log.Warning("Cannot delete grind pose preset: missing preset name.");
                return false;
            }

            string filePath = Path.Combine(presetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Warning($"Cannot delete grind pose preset '{presetName}': file not found.");
                return false;
            }

            try
            {
                File.Delete(filePath);
                Log.Msg($"Grind pose preset '{presetName}' deleted.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to delete grind pose preset '{presetName}': {ex.Message}");
                return false;
            }
        }
    }
}
