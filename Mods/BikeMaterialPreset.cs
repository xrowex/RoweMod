using System.Collections.Generic;
using MelonLoader.Utils;
using Newtonsoft.Json;
using rowemod.Utils;

namespace rowemod.Mods
{
    [Serializable]
    public class BikeMaterialPreset
    {
        public string name;
        public Dictionary<string, string> Materials = new Dictionary<string, string>();

        public static string presetDirectory => Path.Combine(MelonEnvironment.ModsDirectory, "RoweMod", "BikePresets");

        public static void SavePreset(BikeMaterialPreset preset)
        {
            string filePath = Path.Combine(presetDirectory, $"{preset.name}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(preset, Formatting.Indented));

            Log.Msg($"Bike material preset '{preset.name}' saved.");
        }

        public static BikeMaterialPreset? LoadPreset(string presetName)
        {
            string filePath = Path.Combine(presetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Error($"Bike material preset '{presetName}' not found.");
                return null;
            }

            return JsonConvert.DeserializeObject<BikeMaterialPreset>(File.ReadAllText(filePath));
        }

        public static List<string> GetAvailablePresets()
        {
            if (!Directory.Exists(presetDirectory))
                return new List<string>();

            List<string> presets = new List<string>();
            foreach (var file in Directory.GetFiles(presetDirectory, "*.json"))
            {
                presets.Add(Path.GetFileNameWithoutExtension(file));
            }
            return presets;
        }
    }
}
