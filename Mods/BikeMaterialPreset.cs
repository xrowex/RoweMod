using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using Newtonsoft.Json;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    [Serializable]
    public class BikeMaterialPreset
    {
        public string name;
        public Dictionary<string, string> materials = new Dictionary<string, string>();

        private static string PresetDirectory => Path.Combine(MelonEnvironment.ModsDirectory, "rowemod", "BikePresets");

        public static void SavePreset(BikeMaterialPreset preset)
        {
            if (!Directory.Exists(PresetDirectory))
                Directory.CreateDirectory(PresetDirectory);

            string filePath = Path.Combine(PresetDirectory, $"{preset.name}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(preset, Formatting.Indented));

            Log.Msg($"Bike material preset '{preset.name}' saved.");
        }

        public static BikeMaterialPreset? LoadPreset(string presetName)
        {
            string filePath = Path.Combine(PresetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Error($"Bike material preset '{presetName}' not found.");
                return null;
            }

            return JsonConvert.DeserializeObject<BikeMaterialPreset>(File.ReadAllText(filePath));
        }

        public static List<string> GetAvailablePresets()
        {
            if (!Directory.Exists(PresetDirectory))
                return new List<string>();

            List<string> presets = new List<string>();
            foreach (var file in Directory.GetFiles(PresetDirectory, "*.json"))
            {
                presets.Add(Path.GetFileNameWithoutExtension(file));
            }
            return presets;
        }
    }
}
