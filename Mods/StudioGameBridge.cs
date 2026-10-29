using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppMashBox.Core.Runtime.TrickSystem;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using rowemod.Utils;
using UnityEngine;
using Il2CppMashBox.Core.Runtime.InverseKinematics;

namespace rowemod.Mods
{
    internal sealed class StudioSource
    {
        internal string Key, Name;
        internal SyncTrickAnimationData Data;
        internal AnimationClip[] Stock;
        internal AnimationClip Clip(int phase, bool bike, bool mirror = false)
        {
            if (Stock != null) return Stock[phase + (bike ? 4 : 0) + (mirror ? 8 : 0)];
            return phase switch {
                0 => bike ? Data.GetVehicleEnterClip(mirror) : Data.GetPlayerEnterClip(mirror),
                1 => bike ? Data.GetVehicleLoopClip(mirror) : Data.GetPlayerLoopClip(mirror),
                2 => bike ? Data.GetVehicleTweakClip(mirror) : Data.GetPlayerTweakClip(mirror),
                _ => bike ? Data.GetVehicleExitClip(mirror) : Data.GetPlayerExitClip(mirror)
            };
        }
    }
    internal static partial class TrickAnimationEditor
    {
        // Scene-local caches are rebuilt after remaps may already have changed
        // shared ScriptableObjects. Keep the first stock snapshot by asset key
        // for this process; a fresh launch captures the installed game's data.
        private static readonly Dictionary<string, TrickAnimationOverride> studioStock = new(StringComparer.Ordinal);
        private static void RememberStudioStock(SyncTrickAnimationData data, TrickAnimationOverride original)
        {
            string key = GetDataKey(data);
            if (key == null || !key.EndsWith("_PlayerBMX", StringComparison.Ordinal) || studioStock.ContainsKey(key)) return;
            if (original.playerEnterClip?.StartsWith("Player@BMX_", StringComparison.Ordinal) == true &&
                original.vehicleEnterClip?.StartsWith("BMX@BMX_", StringComparison.Ordinal) == true) studioStock[key] = original;
        }
        internal static List<StudioSource> StudioSources()
        {
            RefreshRuntimeDataCache(true, false, "Animation Studio"); EnsureTrickCatalog(false);
            var sources = new List<StudioSource>();
            foreach (var data in trickCatalog.Where(d => d != null && !GetDataKey(d).EndsWith("_PlayerScooter", StringComparison.Ordinal)))
            {
                if (!studioStock.TryGetValue(GetDataKey(data), out var original)) continue;
                string[] names = { original.playerEnterClip, original.playerLoopClip, original.playerTweakClip, original.playerExitClip,
                    original.vehicleEnterClip, original.vehicleLoopClip, original.vehicleTweakClip, original.vehicleExitClip,
                    original.playerMirrorEnterClip, original.playerMirrorLoopClip, original.playerMirrorTweakClip, original.playerMirrorExitClip,
                    original.vehicleMirrorEnterClip, original.vehicleMirrorLoopClip, original.vehicleMirrorTweakClip, original.vehicleMirrorExitClip };
                if (names.Any(n => n != null && n.Contains("@Scooter_", StringComparison.Ordinal)))
                { Log.Warning("[AnimationStudio] Rejected contaminated stock snapshot: " + data.name); continue; }
                var clips = new AnimationClip[16];
                for (int i = 0; i < names.Length; i++) TryFindClip(names[i], out clips[i]);
                if (clips[0] != null) sources.Add(new StudioSource { Key = GetDataKey(data), Name = TrickName(data), Data = data, Stock = clips });
            }
            return sources;
        }
        internal static void StudioUseStock(StudioSource source)
        {
            if (source?.Stock == null) return;
            foreach (var data in runtimeDataCache.Values.Where(d => d != null && GetDataKey(d) == source.Key))
                if (studioStock.TryGetValue(source.Key, out var original)) { SetStudioMirrorRouting(data, false); ApplyClipOverride(data, original); }
            if (Config.trickAnimationDebugSettings.overrides.TryGetValue(source.Key, out var saved))
            { saved.hasClipOverride = false; saved.studioMirrorRouting = false; saved.isolateAnimationTracks = false; Config.Save(); }
        }
        internal static bool StudioLiveContext(out StudioSource source, out int phase, out float time, out float weight,
            out bool mirror, out Animator rider, out Animator bike)
        {
            source = null; phase = 0; time = weight = 0; mirror = false; rider = bike = null;
            if (!Main.playableSceneLoaded || Memory.rMbCharacter == null) return false;
            // Resolve strictly below the local character; never use the global first brain.
            var brain = Memory.rMbCharacter.GetComponentInChildren<TrickSystemBrainV2>(true);
            if (brain == null) return false;
            var data = ResolveV2TrickData(brain, brain._lastFiredSetId, brain._lastFiredSlotId);
            var animator = brain._trickAnimator;
            var layer = animator?.PlayerTrick;
            var state = layer?.CurrentState;
            if (data == null || state?.Clip == null || layer.Weight < .001f) return false;
            source = new StudioSource { Key = GetDataKey(data), Name = TrickName(data), Data = data };
            bool matched = false;
            for (int direction = 0; direction < 2 && !matched; direction++)
                for (int p = 0; p < 4; p++)
                {
                    var clip = source.Clip(p, false, direction == 1);
                    if (clip != null && clip.GetInstanceID() == state.Clip.GetInstanceID())
                    { phase = p; mirror = direction == 1; matched = true; break; }
                }
            if (!matched) return false;
            time = phase == 1 ? Mathf.Repeat(state.NormalizedTime, 1) : Mathf.Clamp01(state.NormalizedTime);
            weight = Mathf.Clamp01(layer.Weight);
            rider = animator.Player.Animator; bike = animator.Vehicle?.Animator;
            return rider != null;
        }
    }
    internal static class StudioRuntimePlayer
    {
        private sealed class PoseState
        {
            internal Transform Node;
            internal Quaternion Before, After;
            internal Vector3 BeforePosition, AfterPosition;
        }
        private static readonly List<PoseState> applied = new();
        internal static string LastApplied;
        internal static int AppliedFrames;
        private static string lastFailure;
        private static UnityIKLimb[] Limbs(HumanIK human) => new[] { StudioContacts.Limb(human?._leftHandIK?.UnityObject),
            StudioContacts.Limb(human?._rightHandIK?.UnityObject), StudioContacts.Limb(human?._leftFootIK?.UnityObject), StudioContacts.Limb(human?._rightFootIK?.UnityObject) };
        internal static void Restore()
        {
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                var state = applied[i];
                if (state.Node == null) continue;
                if (Quaternion.Angle(state.Node.localRotation, state.After) < .01f) state.Node.localRotation = state.Before;
                if (Vector3.Distance(state.Node.localPosition, state.AfterPosition) < .00001f) state.Node.localPosition = state.BeforePosition;
            }
            applied.Clear();
        }
        internal static Transform Target(Animator rider, Animator bike, string target)
        {
            int index = Array.IndexOf(StudioTargets.Bike, target);
            if (index >= 0) return bike?.transform.Find(StudioTargets.BikePaths[index]);
            return rider != null && rider.isHuman && Enum.TryParse<HumanBodyBones>(target, out var bone) ? rider.GetBoneTransform(bone) : null;
        }
        internal static void Apply(Transform node, StudioPose pose, float weight, StudioBikeGeometry geometry = null)
        {
            if (node == null) return;
            var state = new PoseState { Node = node, Before = node.localRotation, BeforePosition = node.localPosition };
            if (geometry != null) geometry.Apply(node, pose, weight);
            else { node.localRotation *= Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z), weight);
                node.localPosition += new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z) * weight; }
            state.After = node.localRotation; state.AfterPosition = node.localPosition; applied.Add(state);
        }
        internal static void LateUpdate()
        {
            Restore();
            var library = RuntimeAnimationStudio.Library;
            if (RuntimeAnimationStudio.IsOpen || library == null || library.Bindings.Count == 0 || !Main.playableSceneLoaded) return;
            try
            {
                if (!TrickAnimationEditor.StudioLiveContext(out var source, out int phase, out float time, out float weight,
                    out bool mirror, out var rider, out var bike)) return;
                var variation = library.Active(source.Key);
                if (variation == null || (mirror && !variation.MirrorEdits)) return;
                var expected = variation.RiderClips[phase];
                if (!string.Equals(source.Clip(phase, false)?.name, expected, StringComparison.Ordinal)) return;
                var human = rider.GetComponentInChildren<HumanIK>(true) ?? rider.GetComponentInParent<HumanIK>();
                var limbs = human == null ? null : Limbs(human);
                var goals = new (Transform joint, Vector3 local, Quaternion rotation)[4];
                if (limbs != null)
                    for (int i = 0; i < 4; i++)
                    {
                        var limb = limbs[i]; if (limb == null || limb._animator != rider) continue;
                        var key = variation.ContactKeyAt(phase, mirror ? i ^ 1 : i, time);
                        bool anchored = key?.Mode == StudioContactMode.Locked && key.HasAnchor;
                        var joint = StudioContacts.BikeJoint(bike, i, anchored ? key.TargetId : limb._currentTargetID);
                        var end = rider.GetBoneTransform(StudioContacts.End[i]);
                        if (joint != null && end != null) goals[i] = (joint, joint.InverseTransformPoint(end.position), Quaternion.Inverse(joint.rotation) * end.rotation);
                        if (joint != null && anchored)
                        {
                            var anchor = new StudioPose(key.AnchorRotation, key.AnchorPosition); if (mirror) anchor = StudioTargets.Reflect(anchor);
                            goals[i] = (joint, new Vector3(anchor.Position.X, anchor.Position.Y, anchor.Position.Z), Quaternion.Euler(anchor.Rotation.X, anchor.Rotation.Y, anchor.Rotation.Z));
                        }
                    }
                foreach (string target in variation.Keys.Where(k => k.Phase == phase).Select(k => k.Target).Distinct())
                {
                    StudioPose pose = variation.Evaluate(phase, target, time);
                    Apply(Target(rider, bike, mirror ? StudioTargets.Mirror(target) : target), mirror ? StudioTargets.Reflect(pose) : pose, weight,
                        target == "Bike:Frame" || target == "Bike:Bars" ? StudioBikeGeometry.Get(bike, bike?.transform.parent) : null);
                }
                if (limbs != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var limb = limbs[i]; if (limb == null || limb._animator != rider || limb.Target == null) continue;
                        float contact = StudioContacts.Weight(variation, phase, mirror ? i ^ 1 : i, time, limb.PositionWeight) * weight;
                        if (contact < .0001f) continue;
                        var goal = goals[i]; if (goal.joint == null) continue;
                        int start = applied.Count;
                        foreach (var bone in new[] { StudioContacts.Upper[i], StudioContacts.Middle[i], StudioContacts.End[i] }) Apply(rider.GetBoneTransform(bone), default, 0);
                        // Carry the native goal through our extra bike-joint
                        // motion, then solve the rider against that moved goal.
                        StudioContacts.Solve(rider, i, goal.joint.TransformPoint(goal.local), goal.joint.rotation * goal.rotation, contact, 1, false);
                        for (int j = start; j < applied.Count; j++) { applied[j].After = applied[j].Node.localRotation; applied[j].AfterPosition = applied[j].Node.localPosition; }
                    }
                }
                AppliedFrames++; LastApplied = variation.Name;
                lastFailure = null;
            }
            catch (Exception ex)
            {
                if (lastFailure != ex.Message) rowemod.Utils.Log.Warning("[AnimationStudio] Playback stopped: " + ex.Message);
                lastFailure = ex.Message; Restore();
            }
        }
    }
}
