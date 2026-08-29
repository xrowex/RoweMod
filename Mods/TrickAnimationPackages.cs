using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppMashBox.Core.Runtime.TrickSystem;
using Newtonsoft.Json;
using RoweMod.AnimationAuthoring;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    internal static partial class TrickAnimationEditor
    {
        private sealed class StudioPackage
        {
            public RoweAnimationPackage Data;
            public Dictionary<string, AnimationClip> Clips;
        }
        private static readonly Dictionary<string, StudioPackage> studioPackages = new(StringComparer.Ordinal);
        private static readonly HashSet<string> studioDuplicatePackages = new(StringComparer.Ordinal);
        private static readonly HashSet<int> studioIsolatedData = new();
        private sealed class StudioTrackCopy
        {
            public SyncTrickAnimationData Target;
            public TrickAnimationData OriginalRider, OriginalBike, Rider, Bike;
            public SyncTrickAnimationData OriginalMirror, Mirror;
            public TrickAnimationData MirrorRider, MirrorBike;
        }
        private static readonly List<StudioTrackCopy> studioTrackCopies = new();

        private static void ReleaseStudioTrackCopies()
        {
            foreach (var copy in studioTrackCopies)
            {
                if (copy.Target != null)
                {
                    if (copy.Target._playerAnimationData == copy.Rider) copy.Target._playerAnimationData = copy.OriginalRider;
                    if (copy.Target._vehicleAnimationData == copy.Bike) copy.Target._vehicleAnimationData = copy.OriginalBike;
                    if (copy.Target._mirrorSyncData == copy.Mirror) copy.Target._mirrorSyncData = copy.OriginalMirror;
                }
                if (copy.Rider != null) UnityEngine.Object.Destroy(copy.Rider);
                if (copy.Bike != null) UnityEngine.Object.Destroy(copy.Bike);
                if (copy.MirrorRider != null) UnityEngine.Object.Destroy(copy.MirrorRider);
                if (copy.MirrorBike != null) UnityEngine.Object.Destroy(copy.MirrorBike);
                if (copy.Mirror != null) UnityEngine.Object.Destroy(copy.Mirror);
            }
            studioTrackCopies.Clear(); studioIsolatedData.Clear();
        }

        private static void ReadStudioPackage(AssetBundle bundle, string[] assets, Dictionary<string, AnimationClip> clips, HashSet<string> duplicateClips)
        {
            foreach (string path in assets)
            {
                if (!path.EndsWith("/" + RoweAnimationPackage.AssetFileName, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var text = bundle.LoadAsset(path, Il2CppType.Of<TextAsset>())?.TryCast<TextAsset>();
                    if (text == null || text.text.Length > 32768) throw new InvalidOperationException("Missing or oversized settings asset.");
                    var data = JsonConvert.DeserializeObject<RoweAnimationPackage>(text.text, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 12 });
                    // Unity's IL2CPP AnimationClip.humanMotion flag reports these
                    // exported Generic bike clips as human motion at runtime even
                    // though their bindings are Generic Transform curves. The
                    // Studio exporter already validates the real editor-side clip
                    // type before building. At runtime, validate the package's
                    // strict role-based names instead of the misleading flag.
                    string error = data?.Validate((name, human) =>
                        !duplicateClips.Contains(name) &&
                        clips.TryGetValue(name, out var c) &&
                        c != null &&
                        !c.legacy &&
                        StudioClipNameMatchesRole(name, human));
                    if (data == null || error != null) throw new InvalidOperationException(error ?? "Empty settings.");
                    if (studioPackages.ContainsKey(data.id)) { studioPackages.Remove(data.id); studioDuplicatePackages.Add(data.id); }
                    if (studioDuplicatePackages.Contains(data.id)) throw new InvalidOperationException("Two bundles use the same package identity. Keep only one version.");
                    studioPackages.Add(data.id, new StudioPackage { Data = data, Clips = clips });
                    Log.Msg($"[TrickAnimEditor] Studio package ready: {data.title}. Timing={data.applyTiming}, rules={data.applyRules}.");
                }
                catch (Exception ex) { Log.Warning("[TrickAnimEditor] Studio package rejected: " + ex.Message); }
            }
        }

        private static int DrawStudioPackageBrowser(SyncTrickAnimationData target)
        {
            if (studioPackages.Count == 0)
                return 0;

            GUILayout.Label("Ready to apply", mutedStyle);
            GUILayout.Label(
                "Choose a complete package below. RoweMod assigns every included rider and bike phase, then saves the trick automatically.",
                mutedStyle);

            foreach (StudioPackage package in studioPackages.Values
                         .OrderBy(value => value.Data.title, StringComparer.OrdinalIgnoreCase))
            {
                RoweAnimationPackage data = package.Data;
                bool applied = StudioPackageMatchesTarget(target, package);

                GUILayout.BeginVertical(panelAltStyle);
                GUILayout.Label(data.title, headerStyle);
                GUILayout.Label(StudioPackageSummary(data), mutedStyle);

                if (applied)
                {
                    GUILayout.Label($"Applied to {TrickName(target)}", activePillStyle);
                    if (Menu.SecondaryButton(
                            "Reapply Package",
                            GUILayout.ExpandWidth(true),
                            GUILayout.Height(28f)))
                    {
                        ApplyStudioPackage(target, package);
                    }
                }
                else if (Menu.PrimaryButton(
                             data.bike.replace
                                 ? $"Apply Rider + Bike to {TrickName(target)}"
                                 : $"Apply Rider to {TrickName(target)}",
                             GUILayout.ExpandWidth(true),
                             GUILayout.Height(32f)))
                {
                    ApplyStudioPackage(target, package);
                }

                GUILayout.EndVertical();
                GUILayout.Space(5f);
            }

            GUILayout.Label(
                "Only the selected trick changes. Reset Trick Animation restores its captured game defaults.",
                mutedStyle);
            GUILayout.Space(5f);
            if (Menu.SecondaryButton(
                    "Capture Live Feet (12 Seconds)",
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(28f)))
            {
                LiveFootDiagnostics.Arm();
            }
            GUILayout.Label(LiveFootDiagnostics.Status, mutedStyle);
            return studioPackages.Count;
        }

        private static string StudioPackageSummary(RoweAnimationPackage package)
        {
            var phases = new List<string> { "Enter" };
            if (!string.IsNullOrEmpty(package.rider.clips[1])) phases.Add("Loop");
            if (!string.IsNullOrEmpty(package.rider.clips[2])) phases.Add("Tweak");
            phases.Add("Exit");

            string bike = package.bike.replace ? "custom bike" : "game bike";
            string opposite = package.riderMirror.replace || package.bikeMirror.replace
                ? "custom opposite direction"
                : "game opposite direction";
            string timing = package.applyTiming ? "package timing" : "game timing";
            return $"Rider: {string.Join(" + ", phases)}  |  {bike}  |  {opposite}  |  {timing}";
        }

        private static bool StudioPackageMatchesTarget(SyncTrickAnimationData target, StudioPackage package)
        {
            if (target == null || package?.Data == null)
                return false;

            RoweAnimationPackage data = package.Data;
            if (!StudioTrackMatches(target._playerAnimationData, data.rider) ||
                !StudioTrackMatches(target._vehicleAnimationData, data.bike))
            {
                return false;
            }

            if (data.riderMirror.replace || data.bikeMirror.replace)
            {
                SyncTrickAnimationData mirror = target._mirrorSyncData;
                if (mirror == null ||
                    !StudioTrackMatches(mirror._playerAnimationData, data.riderMirror) ||
                    !StudioTrackMatches(mirror._vehicleAnimationData, data.bikeMirror))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StudioTrackMatches(TrickAnimationData target, RoweAnimationTrack track)
        {
            if (track == null || !track.replace)
                return true;
            if (target == null || track.clips == null || track.clips.Length != 4)
                return false;

            return string.Equals(ClipName(target._enterAnimationClip) ?? string.Empty, track.clips[0] ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(ClipName(target._loopAnimationClip) ?? string.Empty, track.clips[1] ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(ClipName(target._tweakAnimationClip) ?? string.Empty, track.clips[2] ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(ClipName(target._exitAnimationClip) ?? string.Empty, track.clips[3] ?? string.Empty, StringComparison.Ordinal);
        }

        private static void EnsureIsolatedStudioTracks(SyncTrickAnimationData target)
        {
            int id = target.GetInstanceID();
            if (studioIsolatedData.Contains(id)) return;
            // Track ScriptableObjects may be shared by other tricks. Copy once per
            // target, never per frame; keep original clips and track-level settings.
            var rider = ScriptableObject.CreateInstance<TrickAnimationData>();
            var bike = ScriptableObject.CreateInstance<TrickAnimationData>();
            var copy = new StudioTrackCopy { Target = target, OriginalRider = target._playerAnimationData, OriginalBike = target._vehicleAnimationData, Rider = rider, Bike = bike, OriginalMirror = target._mirrorSyncData };
            studioTrackCopies.Add(copy);
            if (target._playerAnimationData != null) CopyAnimationData(target._playerAnimationData, rider);
            if (target._vehicleAnimationData != null) CopyAnimationData(target._vehicleAnimationData, bike);
            target._playerAnimationData = rider;
            target._vehicleAnimationData = bike;
            studioIsolatedData.Add(id);
        }

        private static void SetStudioMirrorRouting(SyncTrickAnimationData target, bool custom)
        {
            var copy = studioTrackCopies.FirstOrDefault(c => c.Target == target);
            if (copy == null) return;
            if (!custom) { target._mirrorSyncData = copy.OriginalMirror; return; }
            if (copy.Mirror == null)
            {
                copy.Mirror = ScriptableObject.CreateInstance<SyncTrickAnimationData>();
                copy.MirrorRider = ScriptableObject.CreateInstance<TrickAnimationData>();
                copy.MirrorBike = ScriptableObject.CreateInstance<TrickAnimationData>();
                var source = copy.OriginalMirror;
                CopyAnimationData(source != null ? source._playerAnimationData : copy.OriginalRider, copy.MirrorRider);
                CopyAnimationData(source != null ? source._vehicleAnimationData : copy.OriginalBike, copy.MirrorBike);
                copy.Mirror._playerAnimationData = copy.MirrorRider;
                copy.Mirror._vehicleAnimationData = copy.MirrorBike;
                // Get*Clip(true) delegates to linked data's Get*Clip(false).
                // Do not link back or modify the stock partner trick.
                copy.Mirror._mirrorSyncData = null;
            }
            target._mirrorSyncData = copy.Mirror;
        }

        private static void CaptureStudioMirror(SyncTrickAnimationData target, TrickAnimationOverride values)
        {
            var copy = studioTrackCopies.FirstOrDefault(c => c.Target == target);
            if (copy?.Mirror == null || target._mirrorSyncData != copy.Mirror) return;
            values.studioMirrorRouting = true;
            var rider = copy.MirrorRider; var bike = copy.MirrorBike;
            values.playerMirrorEnterClip = ClipName(rider._enterAnimationClip); values.playerMirrorLoopClip = ClipName(rider._loopAnimationClip);
            values.playerMirrorTweakClip = ClipName(rider._tweakAnimationClip); values.playerMirrorExitClip = ClipName(rider._exitAnimationClip);
            values.vehicleMirrorEnterClip = ClipName(bike._enterAnimationClip); values.vehicleMirrorLoopClip = ClipName(bike._loopAnimationClip);
            values.vehicleMirrorTweakClip = ClipName(bike._tweakAnimationClip); values.vehicleMirrorExitClip = ClipName(bike._exitAnimationClip);
        }

        private static void ApplySavedStudioMirror(SyncTrickAnimationData target, TrickAnimationOverride values)
        {
            var mirror = target._mirrorSyncData;
            if (mirror == null) return;
            ApplyClipNames(mirror._playerAnimationData, values.playerMirrorEnterClip, values.playerMirrorLoopClip, values.playerMirrorTweakClip, values.playerMirrorExitClip, null, null, null, null, true);
            ApplyClipNames(mirror._vehicleAnimationData, values.vehicleMirrorEnterClip, values.vehicleMirrorLoopClip, values.vehicleMirrorTweakClip, values.vehicleMirrorExitClip, null, null, null, null, true);
        }

        private static void ApplyStudioPackage(SyncTrickAnimationData target, StudioPackage package)
        {
            if (target == null) return;
            var p = package.Data;
            string error = p.Validate((name, human) =>
                package.Clips.TryGetValue(name, out var c) &&
                c != null &&
                !c.legacy &&
                StudioClipNameMatchesRole(name, human));
            if (error != null) { status = error; return; }
            RememberRuntimeDefault(target);
            var before = CaptureOverride(target, GetDataKey(target));
            try
            {
                EnsureIsolatedStudioTracks(target);
                if (p.riderMirror.replace || p.bikeMirror.replace) SetStudioMirrorRouting(target, true);
                ApplyStudioTrack(target._playerAnimationData, p.rider, false, package.Clips);
                ApplyStudioTrack(target._vehicleAnimationData, p.bike, false, package.Clips);
                if (p.riderMirror.replace) ApplyStudioTrack(target._mirrorSyncData._playerAnimationData, p.riderMirror, false, package.Clips);
                if (p.bikeMirror.replace) ApplyStudioTrack(target._mirrorSyncData._vehicleAnimationData, p.bikeMirror, false, package.Clips);
                if (p.applyTiming)
                {
                    target._overallSpeedMult = p.overallSpeed; target._enterSpeedMult = p.enterSpeed;
                    target._loopMult = p.loopSpeed; target._tweakSpeedMult = p.tweakSpeed; target._exitSpeedMult = p.exitSpeed;
                    target._tweakBeginBlendNormalizedTime = p.tweakAt;
                }
                if (p.applyRules) { target._onlyFireIfInAir = p.onlyInAir; target._allowLandingHolding = p.allowLandingHold; }
                suppressAutoApplyUntil = Time.unscaledTime + 5;
                SaveAnimationOverride(target, GetDataKey(target), "Applied and saved " + p.title + ". Test both directions and release in game.");
            }
            catch (Exception ex)
            {
                ApplyOverride(target, before);
                status = "Package apply failed; restored prior values: " + ex.Message;
                Log.Warning("[TrickAnimEditor] " + status);
            }
        }

        private static bool StudioClipNameMatchesRole(string name, bool rider)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string role = rider ? "Rider" : "Bike";
            string mirrorRole = role + "Mirror";
            foreach (string phase in new[] { "Enter", "Loop", "Tweak", "Exit" })
            {
                if (name.EndsWith("_" + role + "_" + phase, StringComparison.Ordinal) ||
                    name.EndsWith("_" + mirrorRole + "_" + phase, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ApplyStudioTrack(TrickAnimationData target, RoweAnimationTrack track, bool mirror, Dictionary<string, AnimationClip> clips)
        {
            if (!track.replace) return;
            AnimationClip Clip(int i) => string.IsNullOrEmpty(track.clips[i]) ? null : clips[track.clips[i]];
            if (mirror)
            {
                target._enterAnimationClipMirror = Clip(0); target._loopAnimationClipMirror = Clip(1);
                target._tweakAnimationClipMirror = Clip(2); target._exitAnimationClipMirror = Clip(3);
            }
            else
            {
                target._enterAnimationClip = Clip(0); target._loopAnimationClip = Clip(1);
                target._tweakAnimationClip = Clip(2); target._exitAnimationClip = Clip(3);
            }
        }
    }
}
