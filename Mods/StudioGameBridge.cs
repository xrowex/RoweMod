using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAnimancer;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation;
using Il2CppMashBox.Core.Runtime.Physics.Jointing;
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
        internal AnimancerState RiderState, BikeState;
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
            bool refreshedStockClips = false;
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
                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    if (string.IsNullOrEmpty(name) || TryFindClip(name, out clips[i])) continue;
                    if (!refreshedStockClips)
                    {
                        // The shared name cache may predate the current scene's loaded assets.
                        foreach (var loaded in Resources.FindObjectsOfTypeAll<AnimationClip>())
                            if (loaded != null && (!clipLookup.TryGetValue(loaded.name, out var known) || known == null))
                                clipLookup[loaded.name] = loaded;
                        refreshedStockClips = true;
                    }
                    clipLookup.TryGetValue(name, out clips[i]);
                }
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
            out bool mirror, out Animator rider, out Animator bike, out float phaseProgress)
        {
            source = null; phase = 0; time = weight = phaseProgress = 0; mirror = false; rider = bike = null;
            if (!Main.playableSceneLoaded || Memory.rMbCharacter == null) return false;
            // Resolve strictly below the local character; never use the global first brain.
            var brain = Memory.rMbCharacter.GetComponentInChildren<TrickSystemBrainV2>(true);
            if (brain == null) return false;
            var data = ResolveV2TrickData(brain, brain._lastFiredSetId, brain._lastFiredSlotId);
            var animator = brain._trickAnimator;
            var layer = animator?.PlayerTrick;
            var state = layer?.CurrentState;
            if (data == null || state?.Clip == null ||
                (layer.Weight < .001f && (brain._trickController == null || brain._trickController._phase == TrickControllerV2.Phase.Idle)))
                return false;
            source = new StudioSource { Key = GetDataKey(data), Name = TrickName(data), Data = data,
                RiderState = state, BikeState = animator.VehicleTrick?.CurrentState };
            // Some tricks share clips in both directions. Prefer the animator's active mirror route.
            bool matched = false;
            bool activeMirror = animator.Mirror;
            for (int direction = 0; direction < 2 && !matched; direction++)
                for (int p = 0; p < 4; p++)
                {
                    bool clipMirror = direction == 0 ? activeMirror : !activeMirror;
                    var clip = source.Clip(p, false, clipMirror);
                    if (clip != null && clip.GetInstanceID() == state.Clip.GetInstanceID())
                    { phase = p; mirror = activeMirror; matched = true; break; }
                }
            if (!matched) return false;
            phaseProgress = state.NormalizedTime;
            time = phase == 1 ? Mathf.Repeat(phaseProgress, 1) : Mathf.Clamp01(phaseProgress);
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
        private static string lastContactFailure;
        private static Animator lastBlendRider;
        private static StudioVariation lastBlendVariation;
        private static bool lastBlendMirror;
        private static readonly AnimancerState[] riderStates = new AnimancerState[4];
        private static readonly AnimancerState[] bikeStates = new AnimancerState[4];
        private static int lastPhase = -1, previousPhase = -1;
        private static bool lastPhaseAtEndpoint;
        private static AnimatedRagdoll contactRagdoll;
        private static Transform contactBikeRoot;
        private static FemaleJoint[] contactSockets;
        private static HumanIK animatorContactOwner;
        private static Animator animatorContactRider, animatorContactBike;
        private static bool animatorContactMirror;
        private static float animatorContactWeight;
        private static readonly StudioContactKey[] animatorKeys = new StudioContactKey[4];
        private static readonly UnityIKLimb[] animatorLimbs = new UnityIKLimb[4];
        private static readonly (Transform node, Vector3 position)[] originalSelectors = new (Transform, Vector3)[4];
        private static readonly (float position, float rotation)[] originalLimbWeights = new (float, float)[4];
        private static readonly MaleJoint[] overriddenJoints = new MaleJoint[4];
        private static readonly bool[] previousConnectionPermission = new bool[4];
        private static readonly AvatarIKHint[] ContactHints =
            { AvatarIKHint.LeftElbow, AvatarIKHint.RightElbow, AvatarIKHint.LeftKnee, AvatarIKHint.RightKnee };
        private static UnityIKLimb[] Limbs(HumanIK human) => new[] { StudioContacts.Limb(human?._leftHandIK?.UnityObject),
            StudioContacts.Limb(human?._rightHandIK?.UnityObject), StudioContacts.Limb(human?._leftFootIK?.UnityObject), StudioContacts.Limb(human?._rightFootIK?.UnityObject) };
        private static bool TryActiveVariation(out StudioVariation variation, out int phase, out float time, out float weight,
            out bool mirror, out Animator rider, out Animator bike, out float phaseProgress, out StudioSource source)
        {
            variation = null; source = null; phase = 0; time = weight = phaseProgress = 0; mirror = false; rider = bike = null;
            var library = RuntimeAnimationStudio.Library;
            if (RuntimeAnimationStudio.IsOpen || library == null || library.Bindings.Count == 0 || !Main.playableSceneLoaded ||
                !RemoteKillSwitched.isModEnabled || ReplayInputPatch.IsNativeReplayActive()) return false;
            if (!TrickAnimationEditor.StudioLiveContext(out source, out phase, out time, out weight, out mirror, out rider, out bike, out phaseProgress))
                return false;
            variation = library.Active(source.Key);
            return variation != null && (!mirror || variation.MirrorEdits) &&
                string.Equals(source.Clip(phase, false)?.name, variation.RiderClips[phase], StringComparison.Ordinal);
        }

        internal static void BeginAnimatorContacts(HumanIK human)
        {
            try
            {
                if (!TryActiveVariation(out var variation, out int phase, out float time, out float weight, out bool mirror, out var rider, out var bike, out _, out _) ||
                    variation.Contacts.Count == 0) return;

                var limbs = Limbs(human);
                animatorContactOwner = human;
                animatorContactRider = rider;
                animatorContactBike = bike;
                animatorContactWeight = weight;
                animatorContactMirror = mirror;
                for (int i = 0; i < limbs.Length; i++)
                {
                    var limb = limbs[i];
                    if (limb == null || limb._animator != rider) continue;
                    var contact = variation.ContactKeyAt(phase, mirror ? i ^ 1 : i, time);
                    if (contact == null || contact.Mode == StudioContactMode.Auto) continue;
                    animatorKeys[i] = contact;
                    animatorLimbs[i] = limb;
                    if (contact.Mode == StudioContactMode.Free)
                    {
                        originalLimbWeights[i] = (limb._OverridePositionWeight, limb._OverrideRotationWeight);
                        limb._OverridePositionWeight = 0f;
                        limb._OverrideRotationWeight = 0f;
                    }
                    else if (contact.UseBikeTarget && limb._targetIDTrans is { } selector && selector != null &&
                        StudioContacts.NativeTarget(limb, contact.TargetId) is { } target)
                    {
                        // Native UpdateIK selects by list index and recalculates the wrist
                        // roller for that target. Restore the sampled curve after the pass.
                        for (int index = 0; index < limb.Targets.Count; index++)
                        {
                            if (limb.Targets[index] != target) continue;
                            Vector3 position = selector.localPosition;
                            originalSelectors[i] = (selector, position);
                            position.y = StudioContacts.TargetControlFromIndex(index);
                            selector.localPosition = position;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (lastContactFailure != ex.Message) Log.Warning("[AnimationStudio] Could not override native contact: " + ex.Message);
                lastContactFailure = ex.Message;
                RestoreAnimatorContacts();
            }
        }

        internal static void ApplyAnimatorContacts(HumanIK human)
        {
            if (animatorContactOwner != human) return;
            try
            {
                var rider = animatorContactRider;
                for (int i = 0; i < animatorKeys.Length; i++)
                {
                    var key = animatorKeys[i];
                    var limb = animatorLimbs[i];
                    if (key == null || limb == null || rider == null) continue;
                    if (key.Mode == StudioContactMode.Free)
                    {
                        rider.SetIKPositionWeight(limb._Type, 0f);
                        rider.SetIKRotationWeight(limb._Type, 0f);
                    }
                    else
                    {
                        Vector3 position = limb._goalPosition;
                        Quaternion rotation = limb._goalRotation;
                        if (key.HasAnchor && StudioContacts.BikeJoint(animatorContactBike, i, key.TargetId) is { } joint && joint != null)
                        {
                            var pose = new StudioPose(key.AnchorRotation, key.AnchorPosition);
                            if (animatorContactMirror) pose = StudioTargets.Reflect(pose);
                            position = joint.TransformPoint(new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z));
                            rotation = StudioContacts.GoalFromBoneRotation(i, joint.rotation *
                                Quaternion.Euler(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z));
                        }
                        else if (key.UseBikeTarget && StudioContacts.NativeTarget(limb, key.TargetId) is { } target)
                        {
                            position = target.transform.TransformPoint(limb._positionOffset);
                            if (limb.Target != target) rotation = target.transform.rotation * Quaternion.Euler(limb._rotationOffset);
                        }
                        rider.SetIKPosition(limb._Type, position);
                        rider.SetIKRotation(limb._Type, rotation);
                        rider.SetIKPositionWeight(limb._Type, animatorContactWeight);
                        rider.SetIKRotationWeight(limb._Type, animatorContactWeight);
                    }
                    rider.SetIKHintPositionWeight(ContactHints[i], 0f);
                }
                lastContactFailure = null;
            }
            catch (Exception ex)
            {
                if (lastContactFailure != ex.Message) Log.Warning("[AnimationStudio] Could not override native contact: " + ex.Message);
                lastContactFailure = ex.Message;
            }
            finally { RestoreAnimatorContacts(); }
        }

        private static void RestoreAnimatorContacts()
        {
            if (ReferenceEquals(animatorContactOwner, null)) return;
            for (int i = 0; i < animatorKeys.Length; i++)
            {
                if (originalSelectors[i].node != null)
                    originalSelectors[i].node.localPosition = originalSelectors[i].position;
                var limb = animatorLimbs[i];
                if (animatorKeys[i]?.Mode == StudioContactMode.Free && limb != null)
                {
                    limb._OverridePositionWeight = originalLimbWeights[i].position;
                    limb._OverrideRotationWeight = originalLimbWeights[i].rotation;
                }
                animatorKeys[i] = null;
                animatorLimbs[i] = null;
                originalSelectors[i] = default;
            }
            animatorContactOwner = null;
            animatorContactRider = animatorContactBike = null;
        }

        internal static void Restore()
        {
            RestorePose();
            RestorePhysicalContacts();
            RestoreAnimatorContacts();
            ResetBlendTransition();
            contactRagdoll = null;
            contactBikeRoot = null;
            contactSockets = null;
        }

        private static void ResetBlendTransition()
        {
            if (lastBlendVariation != null)
            {
                Array.Clear(riderStates, 0, riderStates.Length);
                Array.Clear(bikeStates, 0, bikeStates.Length);
            }
            lastBlendVariation = null;
            lastBlendRider = null;
            lastPhase = previousPhase = -1;
            lastPhaseAtEndpoint = false;
        }

        private static int ObservePhase(StudioVariation variation, Animator rider, bool mirror, int phase, float time, float phaseProgress)
        {
            if (lastBlendVariation != variation || lastBlendRider != rider || lastBlendMirror != mirror)
            {
                ResetBlendTransition();
                lastBlendVariation = variation;
                lastBlendRider = rider;
                lastBlendMirror = mirror;
            }
            if (phase != lastPhase)
            {
                previousPhase = lastPhaseAtEndpoint ? lastPhase : -1;
                lastPhase = phase;
            }
            // A loop may cross its seam in the frame immediately before exit.
            lastPhaseAtEndpoint = time >= .9f || (phase == 1 && phaseProgress >= 1f && time <= .025f);
            return previousPhase;
        }

        private static void RestorePose()
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

        private static void ObserveState(AnimancerState[] states, int phase, AnimancerState state)
        {
            if (state == null) return;
            for (int i = 0; i < states.Length; i++)
                if (i != phase && states[i]?.Pointer == state.Pointer) states[i] = null;
            states[phase] = state;
        }

        private static float PoseWeight(AnimancerState state) =>
            state == null || !state.IsValid ? 0f : Mathf.Clamp01(state.EffectiveWeight);

        private static void RestorePhysicalContacts()
        {
            for (int i = 0; i < overriddenJoints.Length; i++) SetPhysicalContact(i, null, null);
        }

        internal static void FixedUpdate()
        {
            try
            {
                if (!TryActiveVariation(out var variation, out int phase, out float time, out _, out bool mirror,
                        out var rider, out var bike, out _, out _) || variation.Contacts.Count == 0)
                {
                    RestorePhysicalContacts();
                    return;
                }

                var human = rider.GetComponentInChildren<HumanIK>(true) ?? rider.GetComponentInParent<HumanIK>();
                ApplyPhysicalContacts(variation, phase, time, mirror, rider, bike, Limbs(human));
            }
            catch (Exception ex)
            {
                if (lastContactFailure != ex.Message) Log.Warning("[AnimationStudio] Could not update physical contact: " + ex.Message);
                lastContactFailure = ex.Message;
                RestorePhysicalContacts();
            }
        }

        private static void ApplyPhysicalContacts(StudioVariation variation, int phase, float time, bool mirror,
            Animator rider, Animator bike, UnityIKLimb[] limbs)
        {
            for (int i = 0; i < StudioTargets.Effectors.Length; i++)
            {
                var key = variation.ContactKeyAt(phase, mirror ? i ^ 1 : i, time);
                var limb = limbs?[i];
                var target = key?.UseBikeTarget == true && limb != null && limb._animator == rider
                    ? StudioContacts.NativeTarget(limb, key.TargetId) : null;
                var socket = target != null ? PhysicalSocket(bike, target) : null;
                SetPhysicalContact(i, key?.Mode == StudioContactMode.Free || socket != null ? PhysicalJoint(rider, i) : null, socket);
            }
        }

        private static void SetPhysicalContact(int index, MaleJoint joint, FemaleJoint socket)
        {
            if (overriddenJoints[index] != joint)
            {
                var previous = overriddenJoints[index];
                if (previous != null && !previous.CanConnect)
                    previous.CanConnect = previousConnectionPermission[index];

                overriddenJoints[index] = joint;
                previousConnectionPermission[index] = joint != null && joint.CanConnect;
            }

            if (joint == null) return;
            bool connected = joint.IsConnected();
            if (connected && socket != null && joint.IsConnected(socket))
            {
                joint.CanConnect = false;
                return;
            }

            // Keep the native socket joint, but restrict it to the authored target
            // while this contact key is active.
            if (connected) joint.Disconnect();
            joint.CanConnect = false;
            if (socket != null && socket.isActiveAndEnabled && socket.CanConnect() && socket.CanConnectTo(joint.name))
            {
                var collider = socket.GetComponent<Collider>();
                Vector3 anchor = joint.transform.TransformPoint(joint._anchor);
                if (collider != null && collider.enabled && (collider.ClosestPoint(anchor) - anchor).sqrMagnitude <= .025f * .025f)
                {
                    joint.CanConnect = previousConnectionPermission[index];
                    try
                    {
                        if (joint.CanAttemptConnection()) joint.Connect(socket);
                    }
                    finally
                    {
                        joint.CanConnect = false;
                    }
                }
            }
        }

        private static MaleJoint PhysicalJoint(Animator rider, int limb)
        {
            var leftHand = rider.GetBoneTransform(HumanBodyBones.LeftHand);
            if (contactRagdoll == null || contactRagdoll._leftHandAnimated != leftHand)
                contactRagdoll = rider.GetComponentInParent<AnimatedRagdoll>();
            if (contactRagdoll == null || contactRagdoll._leftHandAnimated != leftHand) return null;
            if (limb == 0) return contactRagdoll._leftHandMaleJoint;
            if (limb == 1) return contactRagdoll._rightHandMaleJoint;
            var foot = rider.GetBoneTransform(StudioContacts.End[limb]);
            if (foot != null && contactRagdoll._animatedPhysicsJoints != null)
                foreach (var bone in contactRagdoll._animatedPhysicsJoints)
                    if (bone != null && bone.AnimatedBone == foot) return bone.MaleJoint;
            return null;
        }

        private static FemaleJoint PhysicalSocket(Animator bike, IKTarget target)
        {
            var root = bike?.transform.parent;
            if (target == null || root == null || !target.transform.IsChildOf(root)) return null;
            if (contactBikeRoot != root)
            {
                contactBikeRoot = root;
                contactSockets = root.GetComponentsInChildren<FemaleJoint>(true);
            }
            foreach (var socket in contactSockets)
            {
                if (socket == null || !socket.gameObject.activeInHierarchy || socket._target == null) continue;
                // Grip colliders point at the wrist roller, whose child is the IK target.
                if (target.transform == socket._target || target.transform.parent == socket._target) return socket;
            }
            return null;
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
            var follower = geometry?.Follower(node);
            var followerState = follower == null ? null : new PoseState
                { Node = follower, Before = follower.localRotation, BeforePosition = follower.localPosition };
            ApplyOffset(node, pose, weight, geometry);
            state.After = node.localRotation; state.AfterPosition = node.localPosition; applied.Add(state);
            if (followerState != null)
            {
                followerState.After = follower.localRotation;
                followerState.AfterPosition = follower.localPosition;
                applied.Add(followerState);
            }
        }
        internal static void ApplyOffset(Transform node, StudioPose pose, float weight, StudioBikeGeometry geometry)
        {
            if (node == null) return;
            if (geometry != null) geometry.Apply(node, pose, weight);
            else
            {
                node.localRotation *= Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z), weight);
                node.localPosition += new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z) * weight;
            }
        }
        internal static void LateUpdate()
        {
            RestorePose();
            try
            {
                if (!TryActiveVariation(out var variation, out int phase, out float time, out _,
                    out bool mirror, out var rider, out var bike, out float phaseProgress, out var source))
                {
                    RestorePhysicalContacts();
                    ResetBlendTransition();
                    return;
                }
                int fromPhase = ObservePhase(variation, rider, mirror, phase, time, phaseProgress);
                ObserveState(riderStates, phase, source.RiderState);
                var bikeClip = source.Clip(phase, true, mirror);
                if (bikeClip != null && source.BikeState?.Clip == bikeClip) ObserveState(bikeStates, phase, source.BikeState);
                var blend = variation.BlendInto(phase);
                float blendWeight = blend?.Weight(fromPhase, phaseProgress) ?? 0f;
                AppliedFrames++;
                LastApplied = variation.Name;
                bool hasPoseOffsets = variation.Keys.Count > 0 || blendWeight > 0f;
                if (!hasPoseOffsets && variation.Contacts.Count == 0) return;

                var human = rider.GetComponentInChildren<HumanIK>(true) ?? rider.GetComponentInParent<HumanIK>();
                var limbs = human == null ? null : Limbs(human);
                // Native target activation can restore joint permission after the
                // fixed-step pass. Reassert the authored contact after animation too.
                if (variation.Contacts.Count > 0) ApplyPhysicalContacts(variation, phase, time, mirror, rider, bike, limbs);
                if (!hasPoseOffsets) return;
                var goals = new (Transform joint, Transform end, Vector3 local, Quaternion rotation, float weight)[4];
                if (limbs != null)
                    for (int i = 0; i < 4; i++)
                    {
                        var limb = limbs[i];
                        if (limb == null || limb._animator != rider) continue;
                        int editedLimb = mirror ? i ^ 1 : i;
                        var key = variation.ContactKeyAt(phase, editedLimb, time);
                        float contact = StudioContacts.Weight(variation, phase, editedLimb, time, limb._finalPositionWeight);
                        if (contact < .0001f) continue;
                        int targetID = key?.HasAnchor == true || key?.UseBikeTarget == true ? key.TargetId : limb.Target?.TargetID ?? limb._currentTargetID;
                        var joint = StudioContacts.BikeJoint(bike, i, targetID);
                        var end = rider.GetBoneTransform(StudioContacts.End[i]);
                        if (joint == null || end == null) continue;
                        // Transport the pose Unity actually solved, including target
                        // blending, wrist roll and reach limits, through our authored offset.
                        goals[i] = (joint, end, joint.InverseTransformPoint(end.position), Quaternion.Inverse(joint.rotation) * end.rotation, contact);
                    }
                StudioBikeGeometry geometry = null;
                void ApplyTrack(string target, StudioPose pose, float poseWeight, bool isBike)
                {
                    if (poseWeight < .001f) return;
                    if (isBike) geometry ??= StudioBikeGeometry.Get(bike, bike?.transform.parent, live: true);
                    Apply(Target(rider, bike, mirror ? StudioTargets.Mirror(target) : target), mirror ? StudioTargets.Reflect(pose) : pose,
                        poseWeight, isBike ? geometry : null);
                }
                // The incoming state can be near zero even on a fully weighted trick layer.
                // Keep evaluating outgoing keys while their native clip is still fading out.
                for (int p = 0; p < riderStates.Length; p++)
                {
                    var riderState = riderStates[p];
                    var bikeState = bikeStates[p];
                    float riderWeight = PoseWeight(riderState);
                    float bikeWeight = PoseWeight(bikeState);
                    if (riderWeight < .001f && bikeWeight < .001f) continue;
                    foreach (string target in StudioTargets.Rider.Concat(StudioTargets.Bike))
                    {
                        bool isBike = target.StartsWith("Bike:", StringComparison.Ordinal);
                        var state = isBike ? bikeState : riderState;
                        float poseWeight = isBike ? bikeWeight : riderWeight;
                        if (poseWeight < .001f || !variation.Keys.Any(k => k.Phase == p && k.Target == target)) continue;
                        float progress = state.NormalizedTime;
                        float poseTime = p == 1 ? Mathf.Repeat(progress, 1) : Mathf.Clamp01(progress);
                        ApplyTrack(target, variation.Evaluate(p, target, poseTime), poseWeight, isBike);
                    }
                }
                if (blendWeight > 0f)
                    foreach (var offset in blend.Offsets)
                    {
                        bool isBike = offset.Target.StartsWith("Bike:", StringComparison.Ordinal);
                        ApplyTrack(offset.Target, new StudioPose(offset.Rotation, offset.Position),
                            blendWeight * PoseWeight(isBike ? bikeStates[phase] : riderStates[phase]), isBike);
                    }
                if (limbs != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var goal = goals[i]; if (goal.joint == null) continue;
                        Vector3 position = goal.joint.TransformPoint(goal.local);
                        Quaternion rotation = goal.joint.rotation * goal.rotation;
                        if ((goal.end.position - position).sqrMagnitude < .000004f && Quaternion.Angle(goal.end.rotation, rotation) < .1f) continue;
                        int start = applied.Count;
                        foreach (var bone in new[] { StudioContacts.Upper[i], StudioContacts.Middle[i], StudioContacts.End[i] }) Apply(rider.GetBoneTransform(bone), default, 0);
                        StudioContacts.Solve(rider, i, position, rotation, goal.weight, 1, false);
                        for (int j = start; j < applied.Count; j++) { applied[j].After = applied[j].Node.localRotation; applied[j].AfterPosition = applied[j].Node.localPosition; }
                    }
                }
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
