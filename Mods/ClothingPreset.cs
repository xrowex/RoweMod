using System;
using System.Collections.Generic;
using MelonLoader.Utils;
using Newtonsoft.Json;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    public class ClothingPreset
    {
        public string Name;
        public Dictionary<Custom.Slot, string> ModelPaths = new Dictionary<Custom.Slot, string>();
        public Dictionary<Custom.Slot, string> MaterialPaths = new Dictionary<Custom.Slot, string>();
        public Dictionary<Custom.Slot, bool> SlotVisibility = new Dictionary<Custom.Slot, bool>();

        public static string presetDirectory => Path.Combine(MelonEnvironment.ModsDirectory, "rowemod", "Presets");

        public static void Save(ClothingPreset preset)
        {
            preset.PopulateSlotVisibility();
            
            string filePath = Path.Combine(presetDirectory, $"{preset.Name}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(preset, Formatting.Indented));
            Log.Msg($"Preset '{preset.Name}' saved.");
        }

        public static ClothingPreset Load(string presetName)
        {
            string filePath = Path.Combine(presetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Msg($"Preset '{presetName}' not found.");
                return null;
            }

            var preset = JsonConvert.DeserializeObject<ClothingPreset>(File.ReadAllText(filePath));

            // Backward compatibility
            if (preset.SlotVisibility == null)
                preset.SlotVisibility = new Dictionary<Custom.Slot, bool>();

            return preset;
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

        public static bool Delete(string presetName)
        {
            string filePath = Path.Combine(presetDirectory, $"{presetName}.json");
            if (!File.Exists(filePath))
            {
                Log.Msg($"Cannot delete preset '{presetName}' - file not found.");
                return false;
            }

            try
            {
                File.Delete(filePath);
                Log.Msg($"Preset '{presetName}' deleted successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to delete preset '{presetName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fills SlotVisibility based on current in-game equipped objects.
        /// </summary>
        public void PopulateSlotVisibility()
        {
            foreach (Custom.Slot slot in Enum.GetValues(typeof(Custom.Slot)))
            {
                GameObject go = Custom.GetSlotObject(slot); 
                SlotVisibility[slot] = go?.activeSelf ?? true;
            }
        }
        
    }
}