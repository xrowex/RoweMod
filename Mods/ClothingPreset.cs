using rowemod.Utils;
using Newtonsoft.Json;
using static rowemod.Mods.Custom;
using MelonLoader.Utils;

namespace rowemod.Mods
{

    public class ClothingPreset
    {
        public string Name;
        public Dictionary<Slot, string> ModelPaths = new Dictionary<Slot, string>();
        public Dictionary<Slot, string> MaterialPaths = new Dictionary<Slot, string>();

        public static string presetDirectory => Path.Combine(MelonEnvironment.ModsDirectory, "rowemod", "Presets");

        public static void SavePreset(ClothingPreset preset)
        {
            string filePath = Path.Combine(presetDirectory, $"{preset.Name}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(preset, Formatting.Indented));

            Log.Msg($"Preset '{preset.Name}' saved.");
        }

        public static ClothingPreset? LoadPreset(string presetName)
        {
            string filePath = Path.Combine(presetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Msg($"Preset '{presetName}' not found.");
                return null;
            }

            return JsonConvert.DeserializeObject<ClothingPreset>(File.ReadAllText(filePath));
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
