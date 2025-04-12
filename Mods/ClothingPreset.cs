using rowemod.Utils;
using Newtonsoft.Json;
using static rowemod.Mods.Custom;
using MelonLoader.Utils;

namespace rowemod.Mods
{

    public class ClothingPreset
    {
        public string name;
        public Dictionary<Slot, string> modelPaths = new Dictionary<Slot, string>();
        public Dictionary<Slot, string> materialPaths = new Dictionary<Slot, string>();

        public static string PresetDirectory => Path.Combine(MelonEnvironment.ModsDirectory, "rowemod", "Presets");

        public static void SavePreset(ClothingPreset preset)
        {
            string filePath = Path.Combine(PresetDirectory, $"{preset.name}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(preset, Formatting.Indented));

            Log.Msg($"Preset '{preset.name}' saved.");
        }

        public static ClothingPreset? LoadPreset(string presetName)
        {
            string filePath = Path.Combine(PresetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Msg($"Preset '{presetName}' not found.");
                return null;
            }

            return JsonConvert.DeserializeObject<ClothingPreset>(File.ReadAllText(filePath));
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
