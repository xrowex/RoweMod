using System;
using UnityEngine;
using Il2CppMashBox.Core.Runtime.InverseKinematics;

namespace rowemod.Mods
{
    internal static class StudioContacts
    {
        internal static UnityIKLimb Limb(UnityEngine.Object reference)
        {
            if (reference == null) return null;
            var limb = reference.TryCast<UnityIKLimb>(); if (limb != null) return limb;
            // Streets serializes InterfaceObject references to either a chain
            // component or its GameObject. Resolve the latter explicitly.
            var owner = reference.TryCast<GameObject>() ?? reference.TryCast<Component>()?.gameObject;
            return owner?.GetComponent<UnityIKLimb>() ?? owner?.GetComponentInChildren<UnityIKLimb>(true);
        }
        internal static readonly HumanBodyBones[] Upper = { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg };
        internal static readonly HumanBodyBones[] Middle = { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
        internal static readonly HumanBodyBones[] End = { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        internal static readonly string[] BikeTargets = {
            "Headset/Upper Steer Components/Left Grip/Wrist Roller/Joint Target", "Headset/Upper Steer Components/Right Grip/Wrist Roller/Joint Target",
            "Left Pedal Body/Joint Target", "Right Pedal Body/Joint Target" };
        internal static Transform BikeJoint(Animator bike, int limb, int targetId)
        {
            int index;
            if (limb < 2) index = targetId == 0 ? 1 : 0; // Grips / seat.
            else if (targetId == 0) index = limb == 2 ? 3 : 4; // Pedals.
            else index = targetId >= 2 ? 1 : 0; // Front targets steer; rear targets follow the frame.
            return bike?.transform.Find(StudioTargets.BikePaths[index]);
        }
        internal static float Weight(StudioVariation variation, int phase, int limb, float time, float native) =>
            variation?.Contact(phase, limb, time) switch { StudioContactMode.Locked => 1, StudioContactMode.Free => 0, _ => Mathf.Clamp01(native) };
        internal static float Gap(Animator rider, int limb, Vector3 goal, Quaternion rotation, bool soleGoal = true)
        {
            if (soleGoal && limb >= 2) goal += rotation * Vector3.up * (limb == 2 ? rider.leftFeetBottomHeight : rider.rightFeetBottomHeight);
            var end = rider.GetBoneTransform(End[limb]);
            return end == null ? float.PositiveInfinity : Vector3.Distance(end.position, goal);
        }
        // Two-bone solve uses the sampled elbow/knee plane. No physics, animator
        // controller or private Unity-editor IK dependency is required.
        internal static void Solve(Animator rider, int limb, Vector3 goal, Quaternion rotation, float weight, float rotationWeight = 1, bool soleGoal = true)
        {
            if (rider == null || !rider.isHuman || weight <= .0001f) return;
            var a = rider.GetBoneTransform(Upper[limb]); var b = rider.GetBoneTransform(Middle[limb]); var c = rider.GetBoneTransform(End[limb]);
            if (a == null || b == null || c == null) return;
            weight = Mathf.Clamp01(weight);
            if (soleGoal && limb >= 2) goal += rotation * Vector3.up * (limb == 2 ? rider.leftFeetBottomHeight : rider.rightFeetBottomHeight);
            if (Vector3.Distance(c.position, goal) < .002f && Quaternion.Angle(c.rotation, rotation) < .1f) return;
            goal = Vector3.Lerp(c.position, goal, weight);
            var oldEndRotation = c.rotation;
            Vector3 ab = b.position - a.position, ac = c.position - a.position, direction = goal - a.position;
            float first = ab.magnitude, second = (c.position - b.position).magnitude;
            if (first < .0001f || second < .0001f || direction.sqrMagnitude < .000001f) return;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(first - second) + .0001f, first + second - .0001f);
            Vector3 forward = direction.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(ab, forward).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.ProjectOnPlane(rider.transform.forward * (limb < 2 ? -1 : 1), forward).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.Cross(forward, Vector3.right).normalized;
            float along = (first * first + distance * distance - second * second) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            Vector3 middle = a.position + forward * along + bend * height;
            a.rotation = Quaternion.FromToRotation(ab, middle - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, goal - b.position) * b.rotation;
            c.rotation = Quaternion.Slerp(oldEndRotation, rotation, Mathf.Clamp01(rotationWeight * weight));
        }
    }
    internal sealed class StudioBikeGeometry
    {
        private static readonly System.Collections.Generic.Dictionary<int, StudioBikeGeometry> cache = new();
        private Transform frame;
        private Vector3 pivot;
        private Quaternion basis;
        internal static StudioBikeGeometry Get(Animator bike, Transform physicalRoot)
        {
            if (bike == null) return null;
            int id = bike.GetInstanceID();
            if (cache.TryGetValue(id, out var found) && found.frame != null) return found;
            var result = new StudioBikeGeometry();
            result.frame = bike.transform.Find(StudioTargets.BikePaths[0]);
            if (result.frame == null || physicalRoot == null) return null;
            var nodes = physicalRoot.GetComponentsInChildren<Transform>(false);
            foreach (var lower in nodes)
                if (lower.name == "Forks_Anchor")
                    foreach (var upper in nodes)
                        if (upper.name == "Headset_Anchor" && upper.parent == lower.parent && (upper.position - lower.position).sqrMagnitude > .000001f)
                        {
                            var axis = (upper.position - lower.position).normalized;
                            var forward = Vector3.ProjectOnPlane(lower.parent.forward, axis).normalized;
                            result.pivot = result.frame.InverseTransformPoint((lower.position + upper.position) * .5f);
                            result.basis = Quaternion.Inverse(result.frame.rotation) * Quaternion.LookRotation(forward, axis);
                            cache[id] = result; return result;
                        }
            return null;
        }
        internal void Apply(Transform node, StudioPose pose, float weight)
        {
            var center = frame.TransformPoint(pivot);
            var axes = frame.rotation * basis;
            var delta = axes * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z), weight) * Quaternion.Inverse(axes);
            node.position = center + delta * (node.position - center);
            node.rotation = delta * node.rotation;
            node.localPosition += new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z) * weight;
        }
    }
}
