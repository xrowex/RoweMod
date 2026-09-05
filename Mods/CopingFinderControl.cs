using rowemod.Utils;
using UnityEngine;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using CornerDetector = Il2CppMashBox.BMX_Physics_Development.CornerDetector;
using GrindMagnetZEM = Il2CppMashBox.BMX_Physics_Development.GrindMagnetZEM;

namespace rowemod.Mods
{
    /// <summary>
    /// Exposes the game's predictive tire-ride magnet without replacing its detector or jump gate.
    /// Native values are captured per bike and remain untouched until customization is enabled.
    /// </summary>
    internal static class CopingFinderControl
    {
        private sealed class NativeSnapshot
        {
            public bool MagnetEnabled;
            public bool UserAlignment;
            public bool AlignmentAssist;
            public float ForceMultiplier;
            public float SearchRangeMultiplier;
            public float Rotation;
        }

        private static GrindMagnetZEM _magnet;
        private static CornerDetector _detector;
        private static NativeSnapshot _native;
        private static int _magnetInstanceId = int.MinValue;
        private static bool _missingLogged;

        public static string StatusText
        {
            get
            {
                if (!TryResolve())
                    return "Waiting for the local BMX tire-ride magnet.";

                string state = _magnet.IsPulling
                    ? "pulling toward coping"
                    : _magnet._alignTireRide
                        ? "armed and searching"
                        : "idle";
                string detection = _magnet.LastChosenDetection;
                if (!string.IsNullOrWhiteSpace(detection))
                    return $"Native system: {state}. Last detection: {detection}.";
                return $"Native system: {state}.";
            }
        }

        public static void OnSceneInitialized()
        {
            _magnet = null;
            _detector = null;
            _native = null;
            _magnetInstanceId = int.MinValue;
            _missingLogged = false;
            grindMagnetZEM = null;
        }

        public static bool BeginCustomization()
        {
            if (!TryResolve())
                return false;

            CopySnapshotToConfig();
            physics.copingFinderTuningEnabled = true;
            Apply();
            Config.RequestSave();
            Log.Msg("[CopingFinder] Custom tuning enabled from the current native values.");
            return true;
        }

        public static bool CopyLiveValuesToConfig()
        {
            if (!TryResolve())
                return false;

            physics.copingFinderMagnetEnabled = _magnet._userEnabled;
            physics.copingFinderUserAlignment = _magnet._userAlign;
            physics.grindAlignAssist = _magnet._alignAssist;
            physics.grindAssistStrength = _magnet._forceMult;
            physics.copingFinderSearchRange = _magnet._userLengthMult;
            physics.copingFinderRotation = _magnet._rotate;
            Config.RequestSave();
            return true;
        }

        public static void RestoreGameValues()
        {
            if (TryResolve() && _native != null)
                ApplySnapshot(_native);

            physics.copingFinderTuningEnabled = false;
            Config.RequestSave();
            Log.Msg("[CopingFinder] Restored the captured game values and disabled custom tuning.");
        }

        public static void Apply()
        {
            if (!physics.copingFinderTuningEnabled || !TryResolve())
                return;

            physics.grindAssistStrength = Mathf.Clamp(physics.grindAssistStrength, 0.01f, 5f);
            physics.copingFinderSearchRange = Mathf.Clamp(physics.copingFinderSearchRange, 0.25f, 3f);
            physics.copingFinderRotation = Mathf.Clamp(physics.copingFinderRotation, -45f, 45f);

            _magnet.SetUserEnabled(physics.copingFinderMagnetEnabled);
            _magnet.SetUserAlign(physics.copingFinderUserAlignment);
            _magnet.SetUserAlignAssist(physics.grindAlignAssist);
            _magnet._forceMult = physics.grindAssistStrength;
            _magnet.SetUserLength(physics.copingFinderSearchRange);
            _magnet._rotate = physics.copingFinderRotation;
        }

        private static bool TryResolve()
        {
            if (_magnet != null && _magnetInstanceId == _magnet.GetInstanceID())
                return true;

            GrindMagnetZEM candidate = null;
            if (vehicleController != null)
                candidate = vehicleController.GetComponentInChildren<GrindMagnetZEM>(true);
            if (candidate == null && rMbCharacter != null)
                candidate = rMbCharacter.GetComponentInChildren<GrindMagnetZEM>(true);

            if (candidate == null)
            {
                if (!_missingLogged)
                {
                    _missingLogged = true;
                    Log.Warning("[CopingFinder] Native GrindMagnetZEM is not available yet.");
                }
                return false;
            }

            int instanceId = candidate.GetInstanceID();
            if (_magnet == null || _magnetInstanceId != instanceId)
            {
                _magnet = candidate;
                _detector = candidate._cornerDetector;
                _magnetInstanceId = instanceId;
                _native = CaptureSnapshot(candidate);
                _missingLogged = false;
                grindMagnetZEM = candidate;
                Log.Msg(
                    $"[CopingFinder] Captured native values from {candidate.name}: " +
                    $"enabled={_native.MagnetEnabled}, userAlign={_native.UserAlignment}, " +
                    $"alignAssist={_native.AlignmentAssist}, force={_native.ForceMultiplier:0.###}, " +
                    $"range={_native.SearchRangeMultiplier:0.###}, rotation={_native.Rotation:0.###}, " +
                    $"detector={(_detector != null ? _detector.name : "missing")}.");
            }

            return true;
        }

        private static NativeSnapshot CaptureSnapshot(GrindMagnetZEM magnet)
        {
            return new NativeSnapshot
            {
                MagnetEnabled = magnet._userEnabled,
                UserAlignment = magnet._userAlign,
                AlignmentAssist = magnet._alignAssist,
                ForceMultiplier = magnet._forceMult,
                SearchRangeMultiplier = magnet._userLengthMult,
                Rotation = magnet._rotate
            };
        }

        private static void CopySnapshotToConfig()
        {
            if (_native == null)
                return;

            physics.copingFinderMagnetEnabled = _native.MagnetEnabled;
            physics.copingFinderUserAlignment = _native.UserAlignment;
            physics.grindAlignAssist = _native.AlignmentAssist;
            physics.grindAssistStrength = _native.ForceMultiplier;
            physics.copingFinderSearchRange = _native.SearchRangeMultiplier;
            physics.copingFinderRotation = _native.Rotation;
        }

        private static void ApplySnapshot(NativeSnapshot snapshot)
        {
            _magnet.SetUserEnabled(snapshot.MagnetEnabled);
            _magnet.SetUserAlign(snapshot.UserAlignment);
            _magnet.SetUserAlignAssist(snapshot.AlignmentAssist);
            _magnet._forceMult = snapshot.ForceMultiplier;
            _magnet.SetUserLength(snapshot.SearchRangeMultiplier);
            _magnet._rotate = snapshot.Rotation;
        }
    }
}
