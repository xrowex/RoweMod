using System;
using rowemod.Utils;
using UnityEngine;
using NativeRecordableBody = Il2CppMashBox.Addons.ReplaySystem.RecordableBody;
using NativeReplayManagerData = Il2CppMashBox.Addons.ReplaySystem.Replay_System.ReplayManagerData;
using NativeReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;

namespace rowemod.Mods
{
    /// <summary>
    /// Keeps the native replay's time span stable when RoweMod raises Unity's
    /// fixed-update rate. BMX Streets sizes its replay buffers from record FPS,
    /// while RecordableBody samples are produced by the physics loop.
    /// </summary>
    public static class ReplayCapacityController
    {
        private static IntPtr _managerPointer;
        private static int _nativeRecordFps;
        private static float _nativeBufferSeconds;
        private static int _appliedRecordFps;
        private static bool _baselineCaptured;
        private static bool _failureLogged;

        public static bool TryPreserveDuration(float gamePhysicsRate, float selectedPhysicsRate)
        {
            if (!float.IsFinite(gamePhysicsRate) || gamePhysicsRate <= 0f ||
                !float.IsFinite(selectedPhysicsRate) || selectedPhysicsRate <= gamePhysicsRate)
            {
                RestoreNativeRate();
                return true;
            }

            try
            {
                NativeReplaySystem replaySystem = NativeReplaySystem.Instance;
                var managerData = replaySystem?._replayManagerData;
                if (managerData == null)
                    return false;

                CaptureBaseline(managerData);
                if (!_baselineCaptured)
                    return false;

                float multiplier = selectedPhysicsRate / gamePhysicsRate;
                int requestedRecordFps = Mathf.Max(
                    _nativeRecordFps,
                    Mathf.CeilToInt(_nativeRecordFps * multiplier));

                managerData._recordFPS = requestedRecordFps;
                string bodyCapacitySummary = EnsureNativeBodiesSeeUpdatedCapacity();

                if (_appliedRecordFps != requestedRecordFps)
                {
                    _appliedRecordFps = requestedRecordFps;
                    _failureLogged = false;
                    Log.Msg(
                        $"[ReplayCapacity] Extended native replay sampling from {_nativeRecordFps} to " +
                        $"{requestedRecordFps} FPS for {selectedPhysicsRate:0.##} Hz physics " +
                        $"({multiplier:0.##}x samples, {_nativeBufferSeconds:0.##}s target duration, " +
                        $"{bodyCapacitySummary}).");
                }

                return managerData._recordFPS == requestedRecordFps;
            }
            catch (Exception ex)
            {
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    Log.Warning($"[ReplayCapacity] Could not extend the native replay buffer: {ex.Message}");
                }

                return false;
            }
        }

        public static void RestoreNativeRate()
        {
            if (!_baselineCaptured)
                return;

            try
            {
                NativeReplaySystem replaySystem = NativeReplaySystem.Instance;
                var managerData = replaySystem?._replayManagerData;
                if (managerData == null || managerData.Pointer != _managerPointer)
                {
                    ClearBaseline();
                    return;
                }

                if (managerData._recordFPS != _nativeRecordFps)
                {
                    managerData._recordFPS = _nativeRecordFps;
                    Log.Msg($"[ReplayCapacity] Restored native replay sampling to {_nativeRecordFps} FPS.");
                }
            }
            catch
            {
                // Scene-owned replay data can already be gone during shutdown.
            }

            _appliedRecordFps = 0;
        }

        public static void ResetSceneState()
        {
            RestoreNativeRate();
            ClearBaseline();
        }

        private static void CaptureBaseline(NativeReplayManagerData managerData)
        {
            if (_baselineCaptured && managerData.Pointer == _managerPointer)
                return;

            _managerPointer = managerData.Pointer;
            _nativeRecordFps = Mathf.Max(1, managerData._recordFPS);
            _nativeBufferSeconds = Mathf.Max(0f, managerData._maxRecordTimeBuffer);
            _appliedRecordFps = 0;
            _baselineCaptured = true;
        }

        private static string EnsureNativeBodiesSeeUpdatedCapacity()
        {
            NativeRecordableBody[] bodies =
                UnityEngine.Object.FindObjectsOfType<NativeRecordableBody>(true);
            if (bodies == null || bodies.Length == 0)
                return "recordableBodies=0";

            int initializedBodies = 0;
            int minimumCapacity = int.MaxValue;
            int maximumCapacity = 0;

            foreach (NativeRecordableBody body in bodies)
            {
                if (body == null)
                    continue;

                body.EnsureBufferAllocated();
                if (body._capacity <= 0)
                    continue;

                initializedBodies++;
                minimumCapacity = Mathf.Min(minimumCapacity, body._capacity);
                maximumCapacity = Mathf.Max(maximumCapacity, body._capacity);
            }

            return initializedBodies == 0
                ? $"recordableBodies={bodies.Length}, initialized=0"
                : $"recordableBodies={bodies.Length}, capacity={minimumCapacity}-{maximumCapacity}";
        }

        private static void ClearBaseline()
        {
            _managerPointer = IntPtr.Zero;
            _nativeRecordFps = 0;
            _nativeBufferSeconds = 0f;
            _appliedRecordFps = 0;
            _baselineCaptured = false;
            _failureLogged = false;
        }
    }
}
