using System;
using System.Collections.Generic;
using UnityEngine;

namespace rowemod.Mods
{
    // Clothing can bring a second copy of the rider armature. Cloning that
    // hierarchy alone leaves it static: only the avatar's canonical rig plays.
    internal sealed class StudioSkinBinding
    {
        private readonly Dictionary<int, Transform> copies;
        private readonly Dictionary<int, Transform> resolved = new();
        private readonly Dictionary<string, Transform> names = new(StringComparer.Ordinal);
        private readonly HashSet<string> ambiguousNames = new(StringComparer.Ordinal);
        private readonly Dictionary<(int parent, string name), Transform> children = new();
        private readonly HashSet<(int parent, string name)> ambiguousChildren = new();
        private readonly HashSet<int> canonical = new();
        private readonly HashSet<int> anchors = new();
        internal int Rebound { get; private set; }
        internal int Reparented { get; private set; }
        internal int AddedHelpers { get; private set; }

        internal StudioSkinBinding(Animator rider, Dictionary<int, Transform> copies)
        {
            this.copies = copies;
            var rig = rider.GetBoneTransform(HumanBodyBones.Hips);
            if (rig != null)
                while (rig.parent != null && rig.parent != rider.transform) rig = rig.parent;
            else rig = rider.transform.Find("Character_Reference") ?? rider.transform;

            for (var node = rig; node != null; node = node.parent)
            {
                anchors.Add(node.GetInstanceID());
                if (node == rider.transform) break;
            }
            foreach (Transform bone in rig.GetComponentsInChildren<Transform>(true))
            {
                int id = bone.GetInstanceID();
                if (!copies.TryGetValue(id, out var copy)) continue;
                canonical.Add(id);
                if (!names.TryAdd(bone.name, copy)) ambiguousNames.Add(bone.name);
                if (bone.parent == null || !copies.TryGetValue(bone.parent.GetInstanceID(), out var parent)) continue;
                var key = (parent.GetInstanceID(), bone.name);
                if (!children.TryAdd(key, copy)) ambiguousChildren.Add(key);
            }
        }

        internal Transform Resolve(Transform bone)
        {
            if (bone == null) return null;
            int id = bone.GetInstanceID();
            if (resolved.TryGetValue(id, out var known)) return known;
            copies.TryGetValue(id, out var direct);
            Transform target;
            if (canonical.Contains(id) || anchors.Contains(id)) target = direct;
            else if (!ambiguousNames.Contains(bone.name) && names.TryGetValue(bone.name, out target)) { }
            else
            {
                var parent = Resolve(bone.parent);
                if (parent == null) target = null;
                else if (ambiguousNames.Contains(bone.name))
                {
                    var key = (parent.GetInstanceID(), bone.name);
                    // A repeated name must match its canonical parent. Guessing
                    // globally can bind a left garment bone to the right limb.
                    target = !ambiguousChildren.Contains(key) && children.TryGetValue(key, out var match) ? match : null;
                }
                else
                {
                    target = direct;
                    if (target == null)
                    {
                        target = new GameObject(bone.name).transform;
                        target.SetParent(parent, false);
                        target.localPosition = bone.localPosition;
                        target.localRotation = bone.localRotation;
                        target.localScale = bone.localScale;
                        target.gameObject.layer = 30;
                        target.gameObject.SetActive(bone.gameObject.activeSelf);
                        copies[id] = target; AddedHelpers++;
                    }
                    else if (target.parent != parent)
                    {
                        // Keep a garment's own helper/rest offset when moving it
                        // off its inactive duplicate armature onto the driven rig.
                        target.SetParent(parent, true); Reparented++;
                    }
                }
            }
            resolved[id] = target;
            if (target != null && target != direct) Rebound++;
            return target;
        }
    }
}
