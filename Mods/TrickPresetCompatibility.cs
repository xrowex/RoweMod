using System;
using System.Collections.Generic;
using System.Text;

namespace rowemod.Mods
{
    internal static class TrickPresetCompatibility
    {
        internal static string SetIdentity(string name)
        {
            string raw = (name ?? string.Empty).Replace("(Clone)", "", StringComparison.OrdinalIgnoreCase)
                .Replace("_TrickSetData_BMX", "", StringComparison.OrdinalIgnoreCase).Trim();
            var text = new StringBuilder();
            foreach (char c in raw) if (char.IsLetterOrDigit(c)) text.Append(char.ToLowerInvariant(c));
            string key = text.ToString();
            return key switch
            {
                "leftshoulderrighttrigger" or "lsrt" or "lbrt" => "LB+RT",
                "rightshoulderlefttrigger" or "rslt" or "rblt" => "RB+LT",
                "leftrightshoulder" or "leftshoulderrightshoulder" or "lsrs" or "lbrb" => "LB+RB",
                "leftshoulder" or "ls" or "lb" => "LB",
                "rightshoulder" or "rs" or "rb" => "RB",
                "leftrighttrigger" or "lefttriggerrighttrigger" or "ltrt" => "LT+RT",
                "lefttrigger" or "lt" => "LT",
                "righttrigger" or "rt" => "RT",
                _ => key
            };
        }

        internal static bool TryFindSet(Dictionary<string, List<TrickEntry>> saved, string name,
            out string savedKey, out List<TrickEntry> entries)
        {
            savedKey = name;
            entries = null;
            if (saved == null) return false;
            if (saved.TryGetValue(name, out entries)) return true;
            string identity = SetIdentity(name);
            bool found = false;
            foreach (var pair in saved)
            {
                if (SetIdentity(pair.Key) != identity) continue;
                if (found) { savedKey = null; entries = null; return false; } // ambiguous aliases must not pick a random mapping
                savedKey = pair.Key;
                entries = pair.Value;
                found = true;
            }
            return found;
        }

        internal static Dictionary<string, List<TrickEntry>> CopySets(Dictionary<string, List<TrickEntry>> saved)
        {
            var result = new Dictionary<string, List<TrickEntry>>(StringComparer.Ordinal);
            if (saved == null) return result;
            foreach (var pair in saved)
            {
                var entries = new List<TrickEntry>();
                if (pair.Value != null)
                    foreach (var entry in pair.Value) entries.Add(entry == null ? null : new TrickEntry { Name = entry.Name, Enabled = entry.Enabled });
                result[pair.Key] = entries;
            }
            return result;
        }
    }
}
