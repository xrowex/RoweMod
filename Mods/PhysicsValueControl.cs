using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using Il2CppMashBox.Core.Runtime.SmartData;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    // Uses the local player's native settings, including their defaults and range.
    // Optional properties are reflected so older supported interop can still load.
    internal static class PhysicsValueControl
    {
        private sealed class Binding
        {
            public object Owner;
            public PropertyInfo Value;
            public SmartDataFloat Definition;
            public float Read() => (float)Value.GetValue(Owner);
            public void Write(float value)
            {
                Type type = Owner.GetType();
                if (type.GetProperty("IsBound")?.GetValue(Owner) is not true ||
                    type.GetProperty("_isProxy")?.GetValue(Owner) is true)
                    throw new InvalidOperationException("The local player setting is no longer bound.");
                Value.SetValue(Owner, Mathf.Clamp(float.IsFinite(value) ? value : Definition.DefaultValue,
                    Definition.MinValue, Definition.MaxValue));
            }
        }

        private sealed class JumpBaseline
        {
            public JumpSettings Settings;
            public float First;
            public float Second;
        }

        private static readonly Dictionary<IntPtr, JumpBaseline> LegacyJumps = new();
        private static Vector3 _gameGravity;
        private static bool _gravityCaptured;
        private static bool _legacyGravityApplied;
        private static IntPtr _controller;
        private static Binding _gravity;
        private static Binding _hop;
        private static bool _savedValuesApplied;
        private static bool _failureLogged;
        private static float _nextRefresh;

        internal static bool UsesStreetsValues => Config.physics.physicsValueScaleVersion >= 1;

        internal static void Update()
        {
            if (!UsesStreetsValues || Time.realtimeSinceStartup < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartup + 0.5f;
            Apply();
        }

        internal static void Apply()
        {
            if (!_gravityCaptured)
            {
                _gameGravity = UnityEngine.Physics.gravity;
                _gravityCaptured = true;
            }
            if (!UsesStreetsValues)
            {
                UnityEngine.Physics.gravity = new Vector3(0f, -Config.physics.gravity, 0f);
                _legacyGravityApplied = true;
                return;
            }
            RestoreLegacyValues();
            if (!Resolve()) return;
            try
            {
                if (!_savedValuesApplied)
                {
                    if (Config.physics.nativeGravityOverride) _gravity.Write(Config.physics.nativeGravity);
                    if (Config.physics.nativeSmallHopOverride) _hop.Write(Config.physics.nativeSmallHop);
                    _savedValuesApplied = true;
                }
                // Native menus can change the same settings. Preserve those edits
                // in saved overrides instead of fighting them on the next restart.
                bool changed = false;
                if (Config.physics.nativeGravityOverride && Config.physics.nativeGravity != _gravity.Read())
                {
                    Config.physics.nativeGravity = _gravity.Read();
                    changed = true;
                }
                if (Config.physics.nativeSmallHopOverride && Config.physics.nativeSmallHop != _hop.Read())
                {
                    Config.physics.nativeSmallHop = _hop.Read();
                    changed = true;
                }
                if (changed) Config.RequestSave();
            }
            catch (Exception ex) { Report(ex); }
        }

        internal static void ApplyLegacyHop(JumpSettings settings)
        {
            if (UsesStreetsValues || settings == null) return;
            var values = settings.JumpVelocity;
            if (values == null || values.Length < 2) return;
            if (!LegacyJumps.ContainsKey(settings.Pointer))
                LegacyJumps.Add(settings.Pointer, new JumpBaseline { Settings = settings, First = values[0], Second = values[1] });
            values[0] = Config.physics.smallHopForce;
            values[1] = Config.physics.smallHopForce;
        }

        internal static bool UseStreetsValues()
        {
            if (!Resolve()) return false;
            RestoreLegacyValues();
            Config.physics.physicsValueScaleVersion = 1;
            Config.physics.nativeGravityOverride = false;
            Config.physics.nativeSmallHopOverride = false;
            _savedValuesApplied = true;
            Config.RequestSave();
            return true;
        }

        internal static void OnPresetLoaded()
        {
            _savedValuesApplied = false;
            _nextRefresh = 0f;
            Apply();
        }

        internal static void OnConfigurationLoaded() => _savedValuesApplied = false;

        internal static bool SyncPresetValues()
        {
            if (!UsesStreetsValues || !Resolve()) return false;
            // A saved handling preset includes both native values, even when the
            // current session was using untouched Streets preferences.
            Config.physics.nativeGravity = _gravity.Read();
            Config.physics.nativeSmallHop = _hop.Read();
            return true;
        }

        internal static void ResetToGameDefaults()
        {
            RestoreLegacyValues();
            if (Resolve())
            {
                try
                {
                    _gravity.Write(_gravity.Definition.DefaultValue);
                    _hop.Write(_hop.Definition.DefaultValue);
                }
                catch (Exception ex) { Report(ex); }
            }
            _savedValuesApplied = false;
        }

        internal static void Release()
        {
            RestoreLegacyValues();
            _controller = IntPtr.Zero;
            _gravity = null;
            _hop = null;
            _savedValuesApplied = false;
        }

        private static void RestoreLegacyValues()
        {
            if (_legacyGravityApplied)
            {
                UnityEngine.Physics.gravity = _gameGravity;
                _legacyGravityApplied = false;
            }
            foreach (var baseline in LegacyJumps.Values)
            {
                try
                {
                    var values = baseline.Settings.JumpVelocity;
                    if (values == null || values.Length < 2) continue;
                    values[0] = baseline.First;
                    values[1] = baseline.Second;
                }
                catch { /* A vehicle from the previous scene may have been destroyed. */ }
            }
            LegacyJumps.Clear();
        }

        private static bool Resolve()
        {
            var vehicle = Memory.vehicleController;
            if (vehicle == null || Memory.rMbCharacter == null) return false;
            if (_controller == vehicle.Pointer && _gravity != null && _hop != null) return true;
            try
            {
                var gravity = ReadBinding(vehicle, "_userGravitySmartData");
                var hop = ReadBinding(vehicle, "_userQuickHopHeightSmartData");
                if (gravity == null || hop == null) return false;
                _controller = vehicle.Pointer;
                _gravity = gravity;
                _hop = hop;
                _savedValuesApplied = false;
                _failureLogged = false;
                Log.Msg($"[Physics Values] Local Streets settings ready: gravity={gravity.Read():0.###}, small hop={hop.Read():0.###}x; defaults={gravity.Definition.DefaultValue:0.###}/{hop.Definition.DefaultValue:0.###}x.");
                return true;
            }
            catch (Exception ex) { Report(ex); return false; }
        }

        private static Binding ReadBinding(object vehicle, string property)
        {
            object owner = vehicle.GetType().GetProperty(property)?.GetValue(vehicle);
            if (owner == null) return null;
            Type type = owner.GetType();
            // Never change an unbound prefab or a remote player's proxy.
            if (type.GetProperty("IsBound")?.GetValue(owner) is not true) return null;
            if (type.GetProperty("_isProxy")?.GetValue(owner) is true) return null;
            var definition = type.GetProperty("Definition")?.GetValue(owner) as SmartDataFloat;
            var value = type.GetProperty("Value");
            if (definition == null || value?.CanRead != true || !value.CanWrite) return null;
            return new Binding { Owner = owner, Value = value, Definition = definition };
        }

        internal static void DrawControls()
        {
            if (!UsesStreetsValues)
            {
                GUILayout.Label("Legacy raw tune (saved values preserved).", Menu.UiMutedWrappedStyle);
                Menu.Slider("Legacy Gravity (m/s²)", ref Config.physics.gravity, 12.5f, 0f, 30f);
                Menu.Slider("Legacy Small Hop Velocity", ref Config.physics.smallHopForce, 4.2f, 0f, 25f);
                if (Menu.SecondaryButton("Use Streets Physics Values", GUILayout.Height(27f)))
                    UseStreetsValues();
                GUILayout.Label("Switching restores the bike's original hop velocity and uses your current Streets settings.", Menu.UiMutedWrappedStyle);
                return;
            }
            if (!Resolve())
            {
                GUILayout.Label("Gravity and Small Hop use Streets values. Mount the local bike to edit them.", Menu.UiMutedWrappedStyle);
                return;
            }
            DrawValue("Gravity", _gravity, true);
            DrawValue("Small Hop (x)", _hop, false);
            GUILayout.Label("Uses Streets values. 1x Small Hop is the game default.", Menu.UiMutedWrappedStyle);
        }

        private static void DrawValue(string label, Binding binding, bool gravity)
        {
            try
            {
                float previous = binding.Read();
                float value = previous;
                Menu.Slider(label, ref value, binding.Definition.DefaultValue,
                    binding.Definition.MinValue, binding.Definition.MaxValue);
                if (value == previous) return;
                binding.Write(value);
                if (gravity)
                {
                    Config.physics.nativeGravity = binding.Read();
                    Config.physics.nativeGravityOverride = true;
                }
                else
                {
                    Config.physics.nativeSmallHop = binding.Read();
                    Config.physics.nativeSmallHopOverride = true;
                }
                Config.RequestSave();
            }
            catch (Exception ex) { Report(ex); }
        }

        private static void Report(Exception ex)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            Log.Warning("[Physics Values] Native settings unavailable: " + ex.Message);
        }
    }
}
