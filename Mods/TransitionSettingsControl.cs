using rowemod.Utils;
using UnityEngine;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using TransitionSettings = Il2CppMashBox.Core.Runtime.Physics.Raycasting.SurfaceScanning.TransitionSettings;

namespace rowemod.Mods
{
    /// <summary>
    /// Tunes the live native transition scanner without replacing its surface detection.
    /// Values are captured per TransitionSettings asset and remain untouched until opted in.
    /// </summary>
    internal static class TransitionSettingsControl
    {
        private sealed class NativeSnapshot
        {
            public float AcceptableSlope;
            public float ScanRadius;
            public float ScanRadiusPerVelocityMin;
            public float ScanRadiusPerVelocityMax;
            public int HorizontalRays;
            public int VerticalRays;
            public float MinimumYDot;
            public bool RunUnderScanner;
            public float SecondarySideScannerLength;
            public float SecondaryUnderScannerLength;
        }

        private static TransitionSettings _settings;
        private static NativeSnapshot _native;
        private static int _settingsInstanceId = int.MinValue;
        private static bool _missingLogged;

        public static string StatusText
        {
            get
            {
                if (!TryResolve())
                    return "Waiting for the local bike's native transition scanner.";

                return $"Native scanner found: {_settings.name}. " +
                       $"Radius {_settings.scanRadius:0.###}, " +
                       $"rays {_settings.numRaysHorizontal} x {_settings.numRaysVertical}.";
            }
        }

        public static float NativeAcceptableSlope => _native?.AcceptableSlope ?? 45f;
        public static float NativeScanRadius => _native?.ScanRadius ?? 1f;
        public static float NativeScanRadiusPerVelocityMin => _native?.ScanRadiusPerVelocityMin ?? 0f;
        public static float NativeScanRadiusPerVelocityMax => _native?.ScanRadiusPerVelocityMax ?? 0f;
        public static int NativeHorizontalRays => _native?.HorizontalRays ?? 8;
        public static int NativeVerticalRays => _native?.VerticalRays ?? 8;
        public static float NativeMinimumYDot => _native?.MinimumYDot ?? 0f;
        public static float NativeSecondarySideScannerLength => _native?.SecondarySideScannerLength ?? 0f;
        public static float NativeSecondaryUnderScannerLength => _native?.SecondaryUnderScannerLength ?? 0f;

        public static void OnSceneInitialized()
        {
            _settings = null;
            _native = null;
            _settingsInstanceId = int.MinValue;
            _missingLogged = false;
        }

        public static bool BeginCustomization()
        {
            if (!TryResolve())
                return false;

            CopySnapshotToConfig();
            physics.transitionTuningEnabled = true;
            Apply();
            Config.RequestSave();
            Log.Msg("[TransitionSettings] Custom tuning enabled from the current native values.");
            return true;
        }

        public static bool CopyLiveValuesToConfig()
        {
            if (!TryResolve())
                return false;

            physics.transitionAcceptableSlope = _settings.acceptableSlope;
            physics.transitionScanRadius = _settings.scanRadius;
            physics.transitionScanRadiusPerVelocityMin = _settings.scanRadiusPerVelMin;
            physics.transitionScanRadiusPerVelocityMax = _settings.scanRadiusPerVelMax;
            physics.transitionHorizontalRays = _settings.numRaysHorizontal;
            physics.transitionVerticalRays = _settings.numRaysVertical;
            physics.transitionMinimumYDot = _settings.MinYDotAngleTo;
            physics.transitionRunUnderScanner = _settings.runUnderScanner;
            physics.transitionSecondarySideScannerLength = _settings.secondarySideScannerMaxLength;
            physics.transitionSecondaryUnderScannerLength = _settings.secondaryUnderScannerMaxLength;
            Config.RequestSave();
            return true;
        }

        public static void RestoreGameValues()
        {
            if (TryResolve() && _native != null)
                ApplySnapshot(_native);

            physics.transitionTuningEnabled = false;
            Config.RequestSave();
            Log.Msg("[TransitionSettings] Restored the captured game values and disabled custom tuning.");
        }

        public static void Apply()
        {
            if (!physics.transitionTuningEnabled || !TryResolve())
                return;

            physics.transitionAcceptableSlope = Mathf.Clamp(physics.transitionAcceptableSlope, 0f, 90f);
            physics.transitionScanRadius = Mathf.Clamp(physics.transitionScanRadius, 0.01f, 10f);
            physics.transitionScanRadiusPerVelocityMin = Mathf.Clamp(physics.transitionScanRadiusPerVelocityMin, 0f, 10f);
            physics.transitionScanRadiusPerVelocityMax = Mathf.Clamp(physics.transitionScanRadiusPerVelocityMax, 0f, 10f);
            physics.transitionHorizontalRays = Mathf.Clamp(physics.transitionHorizontalRays, 1, 32);
            physics.transitionVerticalRays = Mathf.Clamp(physics.transitionVerticalRays, 1, 32);
            physics.transitionMinimumYDot = Mathf.Clamp01(physics.transitionMinimumYDot);
            physics.transitionSecondarySideScannerLength = Mathf.Clamp(physics.transitionSecondarySideScannerLength, 0f, 20f);
            physics.transitionSecondaryUnderScannerLength = Mathf.Clamp(physics.transitionSecondaryUnderScannerLength, 0f, 20f);

            _settings.acceptableSlope = physics.transitionAcceptableSlope;
            _settings.scanRadius = physics.transitionScanRadius;
            _settings.scanRadiusPerVelMin = physics.transitionScanRadiusPerVelocityMin;
            _settings.scanRadiusPerVelMax = physics.transitionScanRadiusPerVelocityMax;
            _settings.numRaysHorizontal = physics.transitionHorizontalRays;
            _settings.numRaysVertical = physics.transitionVerticalRays;
            _settings.MinYDotAngleTo = physics.transitionMinimumYDot;
            _settings.runUnderScanner = physics.transitionRunUnderScanner;
            _settings.secondarySideScannerMaxLength = physics.transitionSecondarySideScannerLength;
            _settings.secondaryUnderScannerMaxLength = physics.transitionSecondaryUnderScannerLength;
        }

        private static bool TryResolve()
        {
            if (_settings != null && _settingsInstanceId == _settings.GetInstanceID())
                return true;

            TransitionSettings candidate = vehicleController?.TransitionSettings;
            if (candidate == null)
            {
                if (!_missingLogged)
                {
                    _missingLogged = true;
                    Log.Warning("[TransitionSettings] Native TransitionSettings are not available yet.");
                }
                return false;
            }

            int instanceId = candidate.GetInstanceID();
            if (_settings == null || _settingsInstanceId != instanceId)
            {
                _settings = candidate;
                _settingsInstanceId = instanceId;
                _native = CaptureSnapshot(candidate);
                _missingLogged = false;
                Log.Msg(
                    $"[TransitionSettings] Captured native values from {candidate.name}: " +
                    $"slope={_native.AcceptableSlope:0.###}, radius={_native.ScanRadius:0.###}, " +
                    $"velocityRadius={_native.ScanRadiusPerVelocityMin:0.###}-{_native.ScanRadiusPerVelocityMax:0.###}, " +
                    $"rays={_native.HorizontalRays}x{_native.VerticalRays}, minYDot={_native.MinimumYDot:0.###}, " +
                    $"under={_native.RunUnderScanner}, sideLength={_native.SecondarySideScannerLength:0.###}, " +
                    $"underLength={_native.SecondaryUnderScannerLength:0.###}.");
            }

            return true;
        }

        private static NativeSnapshot CaptureSnapshot(TransitionSettings settings)
        {
            return new NativeSnapshot
            {
                AcceptableSlope = settings.acceptableSlope,
                ScanRadius = settings.scanRadius,
                ScanRadiusPerVelocityMin = settings.scanRadiusPerVelMin,
                ScanRadiusPerVelocityMax = settings.scanRadiusPerVelMax,
                HorizontalRays = settings.numRaysHorizontal,
                VerticalRays = settings.numRaysVertical,
                MinimumYDot = settings.MinYDotAngleTo,
                RunUnderScanner = settings.runUnderScanner,
                SecondarySideScannerLength = settings.secondarySideScannerMaxLength,
                SecondaryUnderScannerLength = settings.secondaryUnderScannerMaxLength
            };
        }

        private static void CopySnapshotToConfig()
        {
            if (_native == null)
                return;

            physics.transitionAcceptableSlope = _native.AcceptableSlope;
            physics.transitionScanRadius = _native.ScanRadius;
            physics.transitionScanRadiusPerVelocityMin = _native.ScanRadiusPerVelocityMin;
            physics.transitionScanRadiusPerVelocityMax = _native.ScanRadiusPerVelocityMax;
            physics.transitionHorizontalRays = _native.HorizontalRays;
            physics.transitionVerticalRays = _native.VerticalRays;
            physics.transitionMinimumYDot = _native.MinimumYDot;
            physics.transitionRunUnderScanner = _native.RunUnderScanner;
            physics.transitionSecondarySideScannerLength = _native.SecondarySideScannerLength;
            physics.transitionSecondaryUnderScannerLength = _native.SecondaryUnderScannerLength;
        }

        private static void ApplySnapshot(NativeSnapshot snapshot)
        {
            _settings.acceptableSlope = snapshot.AcceptableSlope;
            _settings.scanRadius = snapshot.ScanRadius;
            _settings.scanRadiusPerVelMin = snapshot.ScanRadiusPerVelocityMin;
            _settings.scanRadiusPerVelMax = snapshot.ScanRadiusPerVelocityMax;
            _settings.numRaysHorizontal = snapshot.HorizontalRays;
            _settings.numRaysVertical = snapshot.VerticalRays;
            _settings.MinYDotAngleTo = snapshot.MinimumYDot;
            _settings.runUnderScanner = snapshot.RunUnderScanner;
            _settings.secondarySideScannerMaxLength = snapshot.SecondarySideScannerLength;
            _settings.secondaryUnderScannerMaxLength = snapshot.SecondaryUnderScannerLength;
        }
    }
}
