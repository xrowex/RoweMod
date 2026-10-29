using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace rowemod.Mods
{
    // Runtime authoring data, independent of UnityEditor and native game objects.
    public sealed class StudioVariation
    {
        public int Version = 1;
        public string Id = Guid.NewGuid().ToString("N"), Name, TrickKey;
        public string[] RiderClips = new string[4], BikeClips = new string[4];
        public bool MirrorEdits = true;
        public List<StudioPoseKey> Keys = new();
        public List<StudioContactKey> Contacts = new();

        public string Validate()
        {
            if ((Version != 1 && Version != 2) || !Guid.TryParseExact(Id, "N", out _)) return "Unsupported variation identity.";
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100 || string.IsNullOrWhiteSpace(TrickKey) || TrickKey.Length > 256)
                return "Give the variation a name and a base trick.";
            if (RiderClips?.Length != 4 || BikeClips?.Length != 4 || Keys == null || Contacts == null || Keys.Count + Contacts.Count > 8192)
                return "Invalid animation tracks.";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in Keys)
            {
                if (key == null || key.Phase < 0 || key.Phase > 3 || !Finite(key.Time) || key.Time < 0 || key.Time > 1 ||
                    !StudioTargets.Contains(key.Target) || !key.Rotation.Valid(1440) || !key.Position.Valid(2)) return "Invalid pose key.";
                if (!seen.Add(key.Phase + "|" + key.Target + "|" + key.Time.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))
                    return "Two pose keys occupy the same frame.";
            }
            foreach (var key in Contacts)
            {
                if (key == null || key.Phase < 0 || key.Phase > 3 || key.Limb < 0 || key.Limb > 3 ||
                    !Finite(key.Time) || key.Time < 0 || key.Time > 1 || !Enum.IsDefined(typeof(StudioContactMode), key.Mode) ||
                    (key.HasAnchor && (key.TargetId < 0 || key.TargetId > 3 || !key.AnchorPosition.Valid(4) || !key.AnchorRotation.Valid(1440)))) return "Invalid contact key.";
                if (!seen.Add("contact|" + key.Phase + "|" + key.Limb + "|" + key.Time.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) return "Two contact keys occupy the same frame.";
            }
            return null;
        }
        public StudioContactMode Contact(int phase, int limb, float time)
            => ContactKeyAt(phase, limb, time)?.Mode ?? StudioContactMode.Auto;
        public StudioContactKey ContactKeyAt(int phase, int limb, float time)
        {
            if (!Finite(time)) return null;
            StudioContactKey found = null;
            foreach (var key in Contacts)
                if (key.Phase == phase && key.Limb == limb && key.Time <= time + .00001f && (found == null || key.Time > found.Time)) found = key;
            return found;
        }
        public void SetContact(int phase, int limb, float time, StudioContactMode mode)
        {
            Version = 2;
            Contacts.RemoveAll(k => k.Phase == phase && k.Limb == limb && Math.Abs(k.Time - time) < .00001f);
            Contacts.Add(new StudioContactKey { Phase = phase, Limb = limb, Time = time, Mode = mode });
        }
        public float? AdjacentKey(int phase, float time, int direction, string target = null)
        {
            var times = Keys.Where(k => k.Phase == phase && (target == null || k.Target == target)).Select(k => k.Time)
                .Concat(Contacts.Where(k => k.Phase == phase && (target == null || StudioTargets.Effectors[k.Limb] == target)).Select(k => k.Time));
            var candidates = times.Where(t => direction > 0 ? t > time + .00001f : t < time - .00001f).Distinct().OrderBy(t => t).ToArray();
            return candidates.Length == 0 ? null : direction > 0 ? candidates[0] : candidates[^1];
        }
        internal static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public StudioVariation Copy() => JsonConvert.DeserializeObject<StudioVariation>(JsonConvert.SerializeObject(this));
        public StudioPose Evaluate(int phase, string target, float time)
        {
            if (!Finite(time)) return default;
            time = Math.Clamp(time, 0, 1);
            StudioPoseKey before = null, after = null;
            foreach (var key in Keys)
            {
                if (key.Phase != phase || key.Target != target) continue;
                if (key.Time <= time && (before == null || key.Time > before.Time)) before = key;
                if (key.Time >= time && (after == null || key.Time < after.Time)) after = key;
            }
            if (before == null && after == null) return default;
            if (before == after && before != null) return new StudioPose(before.Rotation, before.Position);
            float start = before?.Time ?? 0, end = after?.Time ?? 1;
            float blend = end <= start ? 0 : (time - start) / (end - start);
            blend = blend * blend * (3 - 2 * blend);
            return new StudioPose(StudioVector.Lerp(before?.Rotation ?? default, after?.Rotation ?? default, blend),
                StudioVector.Lerp(before?.Position ?? default, after?.Position ?? default, blend));
        }
        public void SetKey(int phase, string target, float time, StudioPose pose)
        {
            Keys.RemoveAll(k => k.Phase == phase && k.Target == target && Math.Abs(k.Time - time) < .00001f);
            Keys.Add(new StudioPoseKey { Phase = phase, Target = target, Time = time, Rotation = pose.Rotation, Position = pose.Position });
            Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
        public bool MoveKey(int phase, string target, float from, float to)
        {
            if (!Finite(from) || !Finite(to)) return false;
            var key = Keys.FirstOrDefault(k => k.Phase == phase && k.Target == target && Math.Abs(k.Time - from) < .00001f);
            if (key == null || Keys.Any(k => k != key && k.Phase == phase && k.Target == target && Math.Abs(k.Time - to) < .00001f)) return false;
            key.Time = Math.Clamp(to, 0, 1); return true;
        }
    }
    public sealed class StudioPoseKey
    {
        public int Phase;
        public string Target;
        public float Time;
        public StudioVector Rotation, Position;
    }
    public enum StudioContactMode { Auto, Locked, Free }
    public sealed class StudioContactKey
    {
        public int Phase, Limb;
        public float Time;
        public StudioContactMode Mode;
        public bool HasAnchor;
        public int TargetId;
        public StudioVector AnchorPosition, AnchorRotation;
    }
    public struct StudioVector
    {
        public float X, Y, Z;
        public StudioVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool Valid(float max) => StudioVariation.Finite(X) && StudioVariation.Finite(Y) && StudioVariation.Finite(Z) &&
            Math.Abs(X) <= max && Math.Abs(Y) <= max && Math.Abs(Z) <= max;
        public static StudioVector Lerp(StudioVector a, StudioVector b, float t) =>
            new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }
    public struct StudioPose
    {
        public StudioVector Rotation, Position;
        public StudioPose(StudioVector rotation, StudioVector position) { Rotation = rotation; Position = position; }
    }
    internal static class StudioTargets
    {
        internal static readonly string[] Effectors = { "LeftHand", "RightHand", "LeftFoot", "RightFoot" };
        internal static readonly string[] ContactPaths = {
            "Character_Reference/LeftHandIK/LeftHand_IKWeight", "Character_Reference/RightHandIK/RightHand_IKWeight",
            "Character_Reference/LeftFootIK/LeftFoot_IKWeight", "Character_Reference/RightFootIK/RightFoot_IKWeight" };
        internal static readonly string[] ContactIdPaths = {
            "Character_Reference/LeftHandIK/LeftHand_IKTarget_ID", "Character_Reference/RightHandIK/RightHand_IKTarget_ID",
            "Character_Reference/LeftFootIK/LeftFootIK_Target_ID", "Character_Reference/RightFootIK/RightFootIK_Target_ID" };
        internal static readonly string[] Rider = { "Hips", "Spine", "Chest", "Neck", "Head", "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes", "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes" };
        internal static readonly string[] Bike = { "Bike:Frame", "Bike:Bars", "Bike:Cranks", "Bike:LeftPedal", "Bike:RightPedal", "Bike:FrontWheel", "Bike:RearWheel" };
        internal static readonly string[] BikePaths = { "Joints/Frame_Joint", "Joints/Bars_Joint", "Joints/Frame_Joint/DriveTrain_Joint",
            "Joints/Frame_Joint/DriveTrain_Joint/LeftPedal_Joint", "Joints/Frame_Joint/DriveTrain_Joint/RightPedal_Joint",
            "Joints/Bars_Joint/FrontWheel_Joint", "Joints/Frame_Joint/BackWheel_Joint" };
        internal static bool Contains(string target) => Array.IndexOf(Rider, target) >= 0 || Array.IndexOf(Bike, target) >= 0;
        internal static string Mirror(string target) => target.StartsWith("Left", StringComparison.Ordinal) ? "Right" + target.Substring(4) :
            target.StartsWith("Right", StringComparison.Ordinal) ? "Left" + target.Substring(5) :
            target == "Bike:LeftPedal" ? "Bike:RightPedal" : target == "Bike:RightPedal" ? "Bike:LeftPedal" : target;
        internal static StudioPose Reflect(StudioPose pose) => new(new StudioVector(pose.Rotation.X, -pose.Rotation.Y, -pose.Rotation.Z),
            new StudioVector(-pose.Position.X, pose.Position.Y, pose.Position.Z));
    }
    internal sealed class StudioLibrary
    {
        private readonly string root;
        internal readonly List<StudioVariation> Variations = new();
        internal Dictionary<string, string> Bindings = new(StringComparer.Ordinal);
        private Dictionary<string, StudioVariation> applied = new(StringComparer.Ordinal);
        private bool assignmentsNeedUpgrade;
        private sealed class Assignments
        {
            public int Version = 1;
            public Dictionary<string, StudioVariation> Applied = new(StringComparer.Ordinal);
        }
        private const int MaxAssignmentBytes = 16 * 1024 * 1024;
        internal StudioLibrary(string folder) { root = Path.GetFullPath(folder); }
        private static readonly JsonSerializerSettings JsonSettings = new() { TypeNameHandling = TypeNameHandling.None, MaxDepth = 16 };
        internal void Load()
        {
            Variations.Clear(); Bindings.Clear(); applied.Clear(); assignmentsNeedUpgrade = false;
            if (!Directory.Exists(root)) return;
            foreach (string path in Directory.GetFiles(root, "*.variation.json").Take(1024))
            {
                try
                {
                    if (new FileInfo(path).Length > 4 * 1024 * 1024) continue;
                    var value = JsonConvert.DeserializeObject<StudioVariation>(File.ReadAllText(path), JsonSettings);
                    if (value?.Validate() == null && value != null && Path.GetFileName(path) == value.Id + ".variation.json" && !Variations.Any(v => v.Id == value.Id))
                        Variations.Add(value);
                }
                catch (JsonException) { }
            }
            string bindingsPath = Path.Combine(root, "assignments.json");
            if (File.Exists(bindingsPath) && new FileInfo(bindingsPath).Length <= MaxAssignmentBytes)
            {
                try
                {
                    var document = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(bindingsPath));
                    if (document["Version"] != null)
                    {
                        var entries = document.ToObject<Assignments>(JsonSerializer.Create(JsonSettings));
                        if (entries?.Version == 1 && entries.Applied != null)
                            foreach (var pair in entries.Applied)
                                if (pair.Value != null && pair.Value.Validate() == null && pair.Key == pair.Value.TrickKey &&
                                    Variations.Any(v => v.Id == pair.Value.Id && v.TrickKey == pair.Key))
                                { applied[pair.Key] = pair.Value.Copy(); Bindings[pair.Key] = pair.Value.Id; }
                    }
                    else
                    {
                        // Read the first prototype's ID-only assignments without changing its files.
                        var entries = document.ToObject<Dictionary<string, string>>(JsonSerializer.Create(JsonSettings));
                        if (entries != null) foreach (var pair in entries)
                        {
                            var value = Variations.FirstOrDefault(v => v.Id == pair.Value && v.TrickKey == pair.Key);
                            if (value != null) { applied[pair.Key] = value.Copy(); Bindings[pair.Key] = value.Id; }
                        }
                        assignmentsNeedUpgrade = true;
                    }
                }
                catch (JsonException) { }
            }
        }
        internal void Save(StudioVariation variation)
        {
            string error = variation.Validate(); if (error != null) throw new InvalidDataException(error);
            if (assignmentsNeedUpgrade) WriteAssignments(new Dictionary<string, StudioVariation>(applied, StringComparer.Ordinal));
            Write(Path.Combine(root, variation.Id + ".variation.json"), JsonConvert.SerializeObject(variation, Formatting.Indented));
            Variations.RemoveAll(v => v.Id == variation.Id); Variations.Add(variation.Copy());
        }
        internal void Assign(StudioVariation variation)
        {
            if (!Variations.Any(v => v.Id == variation.Id && v.TrickKey == variation.TrickKey)) throw new InvalidOperationException("Save the variation first.");
            var next = new Dictionary<string, StudioVariation>(applied, StringComparer.Ordinal) { [variation.TrickKey] = variation.Copy() };
            WriteAssignments(next);
        }
        internal void Reset(string key)
        {
            var next = new Dictionary<string, StudioVariation>(applied, StringComparer.Ordinal); next.Remove(key);
            WriteAssignments(next);
        }
        private void WriteAssignments(Dictionary<string, StudioVariation> next)
        {
            string document = JsonConvert.SerializeObject(new Assignments { Applied = next }, Formatting.Indented);
            if (System.Text.Encoding.UTF8.GetByteCount(document) > MaxAssignmentBytes) throw new InvalidDataException("Applied animation tracks exceed the library size limit.");
            Write(Path.Combine(root, "assignments.json"), document);
            applied = next; Bindings = next.ToDictionary(p => p.Key, p => p.Value.Id, StringComparer.Ordinal); assignmentsNeedUpgrade = false;
        }
        internal StudioVariation Active(string key) => applied.TryGetValue(key, out var value) ? value : null;
        private void Write(string path, string text)
        {
            Directory.CreateDirectory(root); string pending = path + ".tmp"; File.WriteAllText(pending, text);
            if (File.Exists(path)) File.Replace(pending, path, path + ".bak"); else File.Move(pending, path);
        }
    }
}
