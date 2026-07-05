using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using Newtonsoft.Json;
using rowemod.Utils;

namespace rowemod.Mods
{
    [Serializable]
    public class TrickPreset
    {
        public string name;
        public string createdAtUtc;
        public CustomTricks customTricks = new CustomTricks { trickSets = new Dictionary<string, List<TrickEntry>>() };
        public TrickAnimationDebugSettings animationSettings = new TrickAnimationDebugSettings();

        public static string presetDirectory => Path.Combine(MelonEnvironment.ModsDirectory, "RoweMod", "TrickPresets");

        public static TrickPreset CaptureCurrent(string presetName)
        {
            TrickMods.SaveTricksToConfig();

            return new TrickPreset
            {
                name = NormalizePresetName(presetName),
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                customTricks = Clone(Config.tricks),
                animationSettings = Clone(Config.trickAnimationDebugSettings) ?? new TrickAnimationDebugSettings()
            };
        }

        public static void SaveCurrent(string presetName)
        {
            TrickPreset preset = CaptureCurrent(presetName);
            Directory.CreateDirectory(presetDirectory);

            string filePath = Path.Combine(presetDirectory, $"{SanitizeFileName(preset.name)}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(preset, Formatting.Indented));
            Log.Msg($"Trick preset '{preset.name}' saved.");
        }

        public static TrickPreset Load(string presetName)
        {
            string filePath = Path.Combine(presetDirectory, $"{SanitizeFileName(presetName)}.json");
            if (!File.Exists(filePath))
            {
                Log.Warning($"Trick preset '{presetName}' not found.");
                return null;
            }

            TrickPreset preset = JsonConvert.DeserializeObject<TrickPreset>(File.ReadAllText(filePath));
            if (preset == null)
            {
                Log.Warning($"Failed to parse trick preset '{presetName}'.");
                return null;
            }

            Normalize(preset);
            return preset;
        }

        public static bool Apply(string presetName)
        {
            TrickPreset preset = Load(presetName);
            if (preset == null)
                return false;

            Config.tricks = Clone(preset.customTricks);
            if (Config.tricks.trickSets == null)
                Config.tricks.trickSets = new Dictionary<string, List<TrickEntry>>();

            Config.trickAnimationDebugSettings = Clone(preset.animationSettings) ?? new TrickAnimationDebugSettings();
            EnsureAnimationSettings(Config.trickAnimationDebugSettings);

            Config.Save();
            TrickMods.LoadTricksFromConfig();
            TrickAnimationEditor.ApplySavedOverridesNow();

            Log.Msg($"Trick preset '{preset.name}' loaded.");
            return true;
        }

        public static List<string> GetAvailablePresets()
        {
            if (!Directory.Exists(presetDirectory))
                return new List<string>();

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
                Log.Warning("Cannot delete trick preset: missing preset name.");
                return false;
            }

            string filePath = Path.Combine(presetDirectory, $"{SanitizeFileName(presetName)}.json");
            if (!File.Exists(filePath))
            {
                Log.Warning($"Cannot delete trick preset '{presetName}': file not found.");
                return false;
            }

            try
            {
                File.Delete(filePath);
                Log.Msg($"Trick preset '{presetName}' deleted.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to delete trick preset '{presetName}': {ex.Message}");
                return false;
            }
        }

        private static void Normalize(TrickPreset preset)
        {
            preset.name = NormalizePresetName(preset.name);
            if (preset.customTricks.trickSets == null)
                preset.customTricks.trickSets = new Dictionary<string, List<TrickEntry>>();
            preset.animationSettings ??= new TrickAnimationDebugSettings();
            EnsureAnimationSettings(preset.animationSettings);
        }

        private static void EnsureAnimationSettings(TrickAnimationDebugSettings settings)
        {
            settings.overrides ??= new Dictionary<string, TrickAnimationOverride>();
            foreach (TrickAnimationOverride trickOverride in settings.overrides.Values)
            {
                if (trickOverride == null)
                    continue;

                trickOverride.poseOverrides ??= new List<TrickPoseOverride>();
                trickOverride.ikTargetOverrides ??= new List<TrickIkTargetOverride>();
            }
        }

        private static string NormalizePresetName(string presetName)
        {
            string normalized = (presetName ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalized) ? "TrickPreset" : normalized;
        }

        private static string SanitizeFileName(string presetName)
        {
            string sanitized = NormalizePresetName(presetName);
            foreach (char invalid in Path.GetInvalidFileNameChars())
                sanitized = sanitized.Replace(invalid, '_');
            return sanitized;
        }

        private static T Clone<T>(T value)
        {
            if (value == null)
                return default;

            string json = JsonConvert.SerializeObject(value);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
