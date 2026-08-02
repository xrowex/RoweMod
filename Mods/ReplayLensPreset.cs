using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using Newtonsoft.Json;
using rowemod.Utils;

namespace rowemod.Mods
{
    [Serializable]
    public sealed class ReplayLensPreset
    {
        public int version = 1;
        public string name = string.Empty;
        public float fov = 60f;
        public float tilt;
        public float fisheye;
        public float vignette = 0.05f;
        public int shakeMode;
        public bool dofEnabled;
        public bool dofPhysicallyBased;
        public float nearFocusStart;
        public float nearFocusEnd = 2f;
        public float farFocusStart = 8f;
        public float farFocusEnd = 20f;
        public int framingMode;
        public float matteOpacity = 1f;

        public static string PresetDirectory =>
            Path.Combine(MelonEnvironment.ModsDirectory, "RoweMod", "ReplayLensPresets");

        public static ReplayLensPreset FromSettings(string presetName, ReplaySettings settings)
        {
            Config.NormalizeReplaySettings(settings);
            return new ReplayLensPreset
            {
                name = NormalizeName(presetName),
                fov = settings.replayFov,
                tilt = settings.replayTilt,
                fisheye = settings.replayFisheye,
                vignette = settings.replayVignette,
                shakeMode = settings.replayShakeMode,
                dofEnabled = settings.replayDofEnabled,
                dofPhysicallyBased = settings.replayDofPhysicallyBased,
                nearFocusStart = settings.replayNearFocusStart,
                nearFocusEnd = settings.replayNearFocusEnd,
                farFocusStart = settings.replayFarFocusStart,
                farFocusEnd = settings.replayFarFocusEnd,
                framingMode = settings.replayFramingMode,
                matteOpacity = settings.replayMatteOpacity
            };
        }

        public void ApplyTo(ReplaySettings settings)
        {
            settings.replayFov = fov;
            settings.replayTilt = tilt;
            settings.replayFisheye = fisheye;
            settings.replayVignette = vignette;
            settings.replayShakeMode = shakeMode;
            settings.replayDofEnabled = dofEnabled;
            settings.replayDofPhysicallyBased = dofPhysicallyBased;
            settings.replayNearFocusStart = nearFocusStart;
            settings.replayNearFocusEnd = nearFocusEnd;
            settings.replayFarFocusStart = farFocusStart;
            settings.replayFarFocusEnd = farFocusEnd;
            settings.replayFramingMode = framingMode;
            settings.replayMatteOpacity = matteOpacity;
            settings.activeReplayLensPreset = name ?? string.Empty;
            Config.NormalizeReplaySettings(settings);
        }

        public static bool Save(ReplayLensPreset preset)
        {
            if (preset == null)
                return false;

            preset.name = NormalizeName(preset.name);
            if (string.IsNullOrWhiteSpace(preset.name))
                return false;

            try
            {
                Directory.CreateDirectory(PresetDirectory);
                File.WriteAllText(PathFor(preset.name), JsonConvert.SerializeObject(preset, Formatting.Indented));
                Log.Msg($"[ReplayCameraLab] Saved lens preset '{preset.name}'.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[ReplayCameraLab] Could not save lens preset '{preset.name}': {ex.Message}");
                return false;
            }
        }

        public static ReplayLensPreset Load(string presetName)
        {
            string normalized = NormalizeName(presetName);
            string path = PathFor(normalized);
            if (string.IsNullOrEmpty(normalized) || !File.Exists(path))
                return null;

            try
            {
                ReplayLensPreset preset = JsonConvert.DeserializeObject<ReplayLensPreset>(File.ReadAllText(path));
                if (preset == null)
                    return null;

                preset.name = normalized;
                ReplaySettings validation = new ReplaySettings();
                preset.ApplyTo(validation);
                return FromSettings(normalized, validation);
            }
            catch (Exception ex)
            {
                Log.Warning($"[ReplayCameraLab] Could not load lens preset '{normalized}': {ex.Message}");
                return null;
            }
        }

        public static List<string> GetAvailablePresets()
        {
            List<string> names = new List<string>();
            if (!Directory.Exists(PresetDirectory))
                return names;

            try
            {
                foreach (string path in Directory.GetFiles(PresetDirectory, "*.json"))
                    names.Add(Path.GetFileNameWithoutExtension(path));
                names.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Warning($"[ReplayCameraLab] Could not enumerate lens presets: {ex.Message}");
            }

            return names;
        }

        public static bool Delete(string presetName)
        {
            string normalized = NormalizeName(presetName);
            string path = PathFor(normalized);
            if (string.IsNullOrEmpty(normalized) || !File.Exists(path))
                return false;

            try
            {
                File.Delete(path);
                Log.Msg($"[ReplayCameraLab] Deleted lens preset '{normalized}'.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[ReplayCameraLab] Could not delete lens preset '{normalized}': {ex.Message}");
                return false;
            }
        }

        public static bool Rename(string oldName, string newName)
        {
            ReplayLensPreset preset = Load(oldName);
            string normalizedNewName = NormalizeName(newName);
            if (preset == null || string.IsNullOrEmpty(normalizedNewName))
                return false;

            string oldPath = PathFor(oldName);
            string newPath = PathFor(normalizedNewName);
            if (File.Exists(newPath))
                return false;

            preset.name = normalizedNewName;
            if (!Save(preset))
                return false;

            try
            {
                if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
                    File.Delete(oldPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[ReplayCameraLab] Renamed preset but could not remove the old file: {ex.Message}");
                return true;
            }
        }

        private static string PathFor(string presetName)
        {
            string normalized = NormalizeName(presetName);
            return Path.Combine(PresetDirectory, normalized + ".json");
        }

        private static string NormalizeName(string value)
        {
            string name = (value ?? string.Empty).Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name.Length > 48 ? name.Substring(0, 48) : name;
        }
    }
}
