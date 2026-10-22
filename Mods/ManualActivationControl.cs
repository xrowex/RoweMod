using System;
using Il2CppMashBox.BMX_Physics_Development;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    internal static class ManualActivationControl
    {
        private sealed class PointState
        {
            internal InputHoldPoint Point;
            internal float Delay;
            internal float Minimum;
            internal float CancelMinimum;
            internal PointState(InputHoldPoint point)
            {
                Point = point;
                Delay = point._minHoldTime;
                Minimum = point._minHoldValue;
                CancelMinimum = point._minCancelValue;
            }
            internal void Apply(float delay, float minimum)
            {
                if (Point == null) return;
                Point._minHoldTime = float.IsFinite(delay) ? Mathf.Clamp(delay, 0f, 1f) : Delay;
                float max = Mathf.Max(0.01f, Point._maxHoldValue - 0.01f);
                minimum = float.IsFinite(minimum) ? Mathf.Clamp(minimum, 0.01f, max) : Minimum;
                Point._minHoldValue = minimum;
                // Keep the native cancel gate consistent with the activation gate.
                // Otherwise a low-threshold manual would cancel on its next tick.
                Point._minCancelValue = Minimum > 0f
                    ? Mathf.Min(minimum, CancelMinimum * minimum / Minimum) : CancelMinimum;
            }
            internal void Restore()
            {
                if (Point == null) return;
                Point._minHoldTime = Delay;
                Point._minHoldValue = Minimum;
                Point._minCancelValue = CancelMinimum;
            }
        }

        private static PointState _manual, _nose;
        private static float _nextRefresh;
        private static bool _loggedFailure;
        internal static bool RequiresUpdate => Config.physics.manualActivationEnabled;

        private static bool Resolve()
        {
            var input = Memory.playerInputBehaviour;
            if (input == null || Memory.rMbCharacter == null || !input._hasInputAuthority) return false;
            var manual = input._mannyInputHoldPoint;
            var nose = input._noseyInputHoldPoint;
            if (manual == null || nose == null) return false;
            // The game's current local actions provide positive manual magnitudes.
            // Leave a different/older layout untouched rather than invert its gates.
            if (manual._minHoldValue < 0f || nose._minHoldValue < 0f) return false;
            if (_manual?.Point == manual && _nose?.Point == nose) return true;
            Release();
            _manual = new PointState(manual);
            _nose = new PointState(nose);
            return true;
        }

        internal static bool BeginCustomization()
        {
            if (!Resolve()) return false;
            Config.physics.manualActivationDelay = _manual.Delay;
            Config.physics.noseManualActivationDelay = _nose.Delay;
            Config.physics.manualActivationThreshold = _manual.Minimum;
            Config.physics.noseManualActivationThreshold = _nose.Minimum;
            Config.physics.manualActivationEnabled = true;
            Apply();
            Config.RequestSave();
            return true;
        }

        internal static void Update()
        {
            if (Time.realtimeSinceStartup < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartup + 0.5f;
            Apply();
        }

        internal static void Apply()
        {
            try
            {
                if (!Config.physics.manualActivationEnabled || !Resolve()) { Release(); return; }
                _manual.Apply(Config.physics.manualActivationDelay, Config.physics.manualActivationThreshold);
                _nose.Apply(Config.physics.noseManualActivationDelay, Config.physics.noseManualActivationThreshold);
                _loggedFailure = false;
            }
            catch (Exception ex)
            {
                if (_loggedFailure) return;
                _loggedFailure = true;
                Log.Warning("[Manual Activation] Local tuning unavailable: " + ex.Message);
            }
        }

        internal static void Release()
        {
            _manual?.Restore();
            _nose?.Restore();
            _manual = null;
            _nose = null;
        }

        internal static void DrawControls()
        {
            Menu.BeginAltPane("Manual Activation", "Lower delay activates sooner; a lower stick threshold needs less input.");
            if (!Config.physics.manualActivationEnabled)
            {
                if (Menu.SecondaryButton("Customize Manual Activation", GUILayout.Height(27f))) BeginCustomization();
                GUILayout.Label("Using the game's manual and nose-manual activation. Mount the local bike to customize.", Menu.UiMutedWrappedStyle);
            }
            else if (!Resolve()) GUILayout.Label("Waiting for the local bike's manual inputs.", Menu.UiMutedWrappedStyle);
            else
            {
                float delay = Config.physics.manualActivationDelay;
                float noseDelay = Config.physics.noseManualActivationDelay;
                float threshold = Config.physics.manualActivationThreshold;
                float noseThreshold = Config.physics.noseManualActivationThreshold;
                DelaySlider("Manual Delay (ms)", ref Config.physics.manualActivationDelay, _manual.Delay);
                DelaySlider("Nose Manual Delay (ms)", ref Config.physics.noseManualActivationDelay, _nose.Delay);
                Menu.Slider("Manual Stick Threshold", ref Config.physics.manualActivationThreshold, _manual.Minimum, 0.01f, Mathf.Max(0.01f, _manual.Point._maxHoldValue - 0.01f));
                Menu.Slider("Nose Manual Stick Threshold", ref Config.physics.noseManualActivationThreshold, _nose.Minimum, 0.01f, Mathf.Max(0.01f, _nose.Point._maxHoldValue - 0.01f));
                if (Menu.SecondaryButton("Use Game Activation", GUILayout.Height(27f)))
                {
                    Config.physics.manualActivationEnabled = false;
                    Release();
                    Config.RequestSave();
                }
                if (delay != Config.physics.manualActivationDelay || noseDelay != Config.physics.noseManualActivationDelay ||
                    threshold != Config.physics.manualActivationThreshold || noseThreshold != Config.physics.noseManualActivationThreshold)
                {
                    Apply();
                    Config.RequestSave();
                }
            }
            Menu.EndPane();
        }

        private static void DelaySlider(string label, ref float seconds, float nativeSeconds)
        {
            float ms = seconds * 1000f;
            Menu.Slider(label, ref ms, nativeSeconds * 1000f, 0f, 1000f);
            seconds = ms / 1000f;
        }
    }
}
