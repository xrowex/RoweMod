using System;
using System.Collections.Generic;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    internal static class GrindFrictionControl
    {
        private sealed class PegMaterial
        {
            public Collider Collider;
            public PhysicMaterial Original;
            public PhysicMaterial Local;
        }

        private static readonly Dictionary<int, PegMaterial> Materials = new();
        private static GameObject _root;
        private static float _nextRefresh;
        private static bool _failureLogged;

        internal static bool RequiresUpdate => Config.physics.grindFrictionEnabled;

        internal static void Update()
        {
            if (Time.realtimeSinceStartup < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartup + 1f;
            Apply();
        }

        internal static void Apply()
        {
            if (!Config.physics.grindFrictionEnabled)
            {
                Restore();
                return;
            }
            GameObject root = Memory.vehicleController?.gameObject;
            if (root == null || Memory.rMbCharacter == null) { Restore(); return; }
            ApplyToLocalRoot(root);
        }

        private static void ApplyToLocalRoot(GameObject root)
        {
            if (_root != root) { Restore(); _root = root; }
            try
            {
                foreach (int id in new List<int>(Materials.Keys))
                {
                    var entry = Materials[id];
                    if (entry.Collider != null && IsPeg(entry.Collider.transform, root.transform)) continue;
                    if (entry.Collider != null && entry.Collider.sharedMaterial == entry.Local)
                        entry.Collider.sharedMaterial = entry.Original;
                    if (entry.Local != null) UnityEngine.Object.Destroy(entry.Local);
                    Materials.Remove(id);
                }
                // A peg can finish equipping after the rider-spawn event. Refresh only
                // this local bike, at most once a second while the override is active.
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (collider == null || collider.isTrigger || !IsPeg(collider.transform, root.transform)) continue;
                    int id = collider.GetInstanceID();
                    if (Materials.TryGetValue(id, out var existing))
                    {
                        // Respect a native part/material replacement: retire the old
                        // clone and capture the new part's own material on this pass.
                        if (collider.sharedMaterial == existing.Local) continue;
                        UnityEngine.Object.Destroy(existing.Local);
                        Materials.Remove(id);
                    }
                    PhysicMaterial original = collider.sharedMaterial;
                    if (original == null) continue;
                    var local = UnityEngine.Object.Instantiate(original);
                    local.name = original.name + "_RoweModLocalPeg";
                    local.hideFlags = HideFlags.HideAndDontSave;
                    collider.sharedMaterial = local;
                    Materials.Add(id, new PegMaterial { Collider = collider, Original = original, Local = local });
                }
                float friction = float.IsFinite(Config.physics.grindFriction)
                    ? Mathf.Clamp(Config.physics.grindFriction, 0f, 5f) : 0.03f;
                Config.physics.grindFriction = friction;
                foreach (var material in Materials.Values)
                {
                    if (material.Collider == null || material.Local == null) continue;
                    material.Local.staticFriction = friction;
                    material.Local.dynamicFriction = friction;
                }
                _failureLogged = false;
            }
            catch (Exception ex)
            {
                if (_failureLogged) return;
                _failureLogged = true;
                Log.Warning("[Grind Friction] Could not apply local peg friction: " + ex.Message);
            }
        }

        private static bool IsPeg(Transform current, Transform root)
        {
            bool peg = false;
            while (current != null && current != root)
            {
                if ((current.name ?? string.Empty).IndexOf("peg", StringComparison.OrdinalIgnoreCase) >= 0) peg = true;
                current = current.parent;
            }
            return current == root && peg;
        }

        internal static void Restore()
        {
            foreach (var material in Materials.Values)
            {
                if (material.Collider != null && material.Collider.sharedMaterial == material.Local)
                    material.Collider.sharedMaterial = material.Original;
                if (material.Local != null) UnityEngine.Object.Destroy(material.Local);
            }
            Materials.Clear();
            _root = null;
        }

        internal static void DrawControls()
        {
            Menu.BeginAltPane("Grinding", "Friction for the local bike's pegs.");
            bool enabled = Config.physics.grindFrictionEnabled;
            Menu.ModernToggle("Custom Grind Friction", ref enabled, "physics_grind_friction");
            if (enabled != Config.physics.grindFrictionEnabled)
            {
                Config.physics.grindFrictionEnabled = enabled;
                Apply();
                Config.RequestSave();
            }
            if (enabled)
            {
                float previous = Config.physics.grindFriction;
                Menu.Slider("Grind Friction", ref Config.physics.grindFriction, 0.03f, 0f, 5f);
                if (previous != Config.physics.grindFriction)
                {
                    Apply();
                    Config.RequestSave();
                }
                GUILayout.Label(Materials.Count > 0 ? $"Applied to {Materials.Count} local peg collider(s)." : "Waiting for the local bike's pegs.", Menu.UiMutedWrappedStyle);
            }
            else GUILayout.Label("Using the game's peg materials.", Menu.UiMutedWrappedStyle);
            Menu.EndPane();
        }
    }
}
