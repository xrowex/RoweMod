using System;
using System.Collections.Generic;

namespace RoweMod.AnimationAuthoring
{
    // Shared by Unity's authoring tool and the IL2CPP mod. No Unity/game types.
    [Serializable]
    public sealed class RoweAnimationPackage
    {
        public const string AssetFileName = "rowemod.animation.json";
        public int version = 1;
        public string id, title;
        public RoweAnimationTrack rider = new RoweAnimationTrack();
        public RoweAnimationTrack bike = new RoweAnimationTrack();
        public RoweAnimationTrack riderMirror = new RoweAnimationTrack();
        public RoweAnimationTrack bikeMirror = new RoweAnimationTrack();
        public bool applyTiming;
        public float overallSpeed = 1, enterSpeed = 1, loopSpeed = 1, tweakSpeed = 1, exitSpeed = 1;
        public float tweakAt = .7f;
        public bool applyRules, onlyInAir, allowLandingHold;

        public IEnumerable<RoweAnimationTrack> Tracks()
        {
            yield return rider; yield return bike; yield return riderMirror; yield return bikeMirror;
        }

        public string Validate(Func<string, bool, bool> clipExists)
        {
            if (version != 1) return "Unsupported Animation Studio package version.";
            if (string.IsNullOrEmpty(id) || !id.StartsWith("RoweMod_Custom_", StringComparison.Ordinal) || id.Length > 160)
                return "Invalid package identity.";
            foreach (char c in id) if (!char.IsLetterOrDigit(c) && c != '_') return "Invalid package identity.";
            if (string.IsNullOrWhiteSpace(title) || title.Length > 200) return "Invalid trick title.";
            if (rider == null || !rider.replace) return "A package must supply rider clips.";
            int index = 0;
            foreach (var track in Tracks())
            {
                if (track == null) return "Missing track information.";
                bool human = index++ % 2 == 0;
                if (!track.replace) continue;
                if (track.clips == null || track.clips.Length != 4) return "Each replaced track needs four phase slots.";
                if (string.IsNullOrEmpty(track.clips[0]) || string.IsNullOrEmpty(track.clips[3])) return "Enter and Exit are required for replaced tracks.";
                foreach (string name in track.clips)
                {
                    if (string.IsNullOrEmpty(name)) continue; // Explicitly remove Loop/Tweak.
                    if (!name.StartsWith(id + "_", StringComparison.Ordinal) || !clipExists(name, human))
                        return "Missing, ambiguous or incompatible clip: " + name;
                }
            }
            if (applyTiming && (!Speed(overallSpeed) || !Speed(enterSpeed) || !Speed(loopSpeed) || !Speed(tweakSpeed) || !Speed(exitSpeed) || !Range(tweakAt, 0, 1)))
                return "Speed must be 0.05–10; Tweak start must be 0–1.";
            return null;
        }

        static bool Speed(float f) => Range(f, .05f, 10);
        static bool Range(float f, float min, float max) => !float.IsNaN(f) && !float.IsInfinity(f) && f >= min && f <= max;
    }

    [Serializable]
    public sealed class RoweAnimationTrack
    {
        public bool replace;
        // Enter / Loop / Tweak / Exit. Null in a replaced track clears that phase.
        public string[] clips = new string[4];
    }
}
