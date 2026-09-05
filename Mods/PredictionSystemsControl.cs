using rowemod.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using ActiveRagdollBehaviour = Il2CppMashBox.Addons.PhysicsDrivenAnimation.ActiveRagdollBehaviour;
using AnimatedRagdoll = Il2CppMashBox.Addons.PhysicsDrivenAnimation.AnimatedRagdoll;
using CornerDetector = Il2CppMashBox.BMX_Physics_Development.CornerDetector;
using FlightAugmentSettings = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightAugmentSettings;
using FlightAugmentTest = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightAugmentTest;
using FlightPathInfo = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightPathInfo;
using FlightPrediction = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightPrediction;
using FlightPredictionSettings = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightPredictionSettings;
using GrindMagnetZEM = Il2CppMashBox.BMX_Physics_Development.GrindMagnetZEM;
using LipMagnet = Il2CppMashBox.BMX_Physics_Development.LipMagnet;

namespace rowemod.Mods
{
    /// <summary>
    /// Captures and tunes the game's predictive riding systems. Each group is opt-in and can
    /// restore the exact live values captured for the current bike. Visualization consumes
    /// native FlightPathInfo only; it never creates physics bodies or applies forces.
    /// </summary>
    internal static class PredictionSystemsControl
    {
        private sealed class FlightSnapshot
        {
            public float CorrectionStrength, ForceCap, NormalPushOff, MaximumVelocityAdjust;
            public float MinimumAirTime, MaximumTickTime, LandingPitch;
            public bool ClosestToBody, FurthestPoint, SteepestAngle, DoNotTickIfFalling;
        }

        private sealed class PredictionSnapshot
        {
            public int Steps, SurfaceNormalRays;
            public float PathTimeStep, StartYOffset, TickInterval, CastRadius, CastRadiusMin;
            public float SurfaceNormalMaxDistance, SurfaceNormalStartOffset, SurfaceNormalSpread;
            public bool PreferPlayerUp, PreferTransitionAngle;
            public Vector3 WantedTransitionNormal;
            public float LandingNormalPitch;
        }

        private sealed class CornerSnapshot
        {
            public float DistanceMultiplier, CheckDistance, FacingDot, YNormalThreshold, StartUpOffset;
            public float EdgeCheckMaximum, EdgeStep, EdgeProbeOutset, EdgeAngleTolerance;
            public float DownTestDistance, UpWalkTestDistance, InwardTestDistance;
            public int NumberOfRays, MissesAllowed;
        }

        private sealed class LipSnapshot
        {
            public float PowerMultiplier, LengthMultiplier, NormalPushOff, MinimumYFromFlightApex;
            public float MinimumDistance, DistanceBoost, MaximumMagnetVelocity, Rotation;
            public float MoveDeltaMultiplier, MaximumMoveDelta;
            public bool AlignmentAssist, OverrideVertical, RunFlightPrediction;
            public bool PreferVerticalAssist, AllowNegativePush;
        }

        private static FlightAugmentSettings _flightSettings;
        private static FlightAugmentTest _flightAugment;
        private static FlightPrediction _prediction;
        private static FlightPredictionSettings _predictionSettings;
        private static CornerDetector _corner;
        private static LipMagnet _lip;
        private static FlightSnapshot _nativeFlight;
        private static PredictionSnapshot _nativePrediction;
        private static CornerSnapshot _nativeCorner;
        private static LipSnapshot _nativeLip;
        private static int _flightId = int.MinValue;
        private static int _predictionSettingsId = int.MinValue;
        private static int _cornerId = int.MinValue;
        private static int _lipId = int.MinValue;

        private static GameObject _visualRoot;
        private static LineRenderer _arcLine;
        private static LineRenderer _markerForward;
        private static LineRenderer _markerRight;
        private static LineRenderer _normalLine;
        private static Material _lineMaterial;

        public static string FlightStatus
        {
            get
            {
                if (!TryResolveFlight()) return "Waiting for native Flight Augment settings.";
                return _flightAugment != null && _flightAugment.IsAugmenting
                    ? "Native Flight Augment is correcting toward a landing."
                    : "Native Flight Augment found and currently idle.";
            }
        }

        public static string PredictionStatus
        {
            get
            {
                if (!TryResolvePrediction()) return "Waiting for native Flight Prediction.";
                FlightPathInfo path = _prediction._flightPathInfo;
                if (path.numStepOnHit <= 0)
                    return "Native Flight Prediction found; no landing is currently resolved.";
                return $"Landing in {_prediction.TimeLeftInAir:0.00}s at " +
                       $"({path.LandingPos.x:0.0}, {path.LandingPos.y:0.0}, {path.LandingPos.z:0.0}).";
            }
        }

        public static string CornerStatus
        {
            get
            {
                if (!TryResolveCorner()) return "Waiting for the native coping CornerDetector.";
                int left = _corner._leftCorners?.Count ?? 0;
                int right = _corner._rightCorners?.Count ?? 0;
                return _corner.HasCorner
                    ? $"Corner detected. Candidate rays: left {left}, right {right}."
                    : $"Corner detector idle. Candidate rays: left {left}, right {right}.";
            }
        }

        public static string LipStatus
        {
            get
            {
                if (!TryResolveLip()) return "Waiting for the native LipMagnet.";
                if (_lip.IsPulling) return $"Lip Magnet is pulling; target time {_lip.TimeToReachTarget:0.00}s.";
                if (_lip._predictiveEngadged) return "Lip Magnet prediction is engaged and searching.";
                return "Native Lip Magnet found and currently idle.";
            }
        }

        public static string RagdollStatus
        {
            get
            {
                if (rMbCharacter == null) return "Waiting for the local rider body.";
                ActiveRagdollBehaviour behaviour = rMbCharacter.GetComponentInChildren<ActiveRagdollBehaviour>(true);
                AnimatedRagdoll ragdoll = behaviour?._animatedRagdoll;
                if (ragdoll == null) return "No local AnimatedRagdoll was found.";
                int joints = ragdoll._animatedPhysicsJoints?.Length ?? 0;
                int bodies = rMbCharacter.GetComponentsInChildren<Rigidbody>(true)?.Length ?? 0;
                return $"Active ragdoll: {bodies} rigidbodies, {joints} driven joints, " +
                       $"strength {ragdoll._strength:0.##}, positional {ragdoll._positionalStrength:0.##}, " +
                       $"relaxation {ragdoll.RelaxationPercent:0.##}.";
            }
        }

        public static void OnSceneInitialized()
        {
            DestroyVisualization();
            _flightSettings = null;
            _flightAugment = null;
            _prediction = null;
            _predictionSettings = null;
            _corner = null;
            _lip = null;
            _nativeFlight = null;
            _nativePrediction = null;
            _nativeCorner = null;
            _nativeLip = null;
            _flightId = _predictionSettingsId = _cornerId = _lipId = int.MinValue;
        }

        public static void Update()
        {
            if (!predictionLabSettings.visualizationEnabled)
            {
                SetVisualizationVisible(false);
                return;
            }

            UpdateVisualization();
        }

        public static void ApplyAll()
        {
            ApplyFlight();
            ApplyPrediction();
            ApplyCorner();
            ApplyLip();
        }

        public static void ReleaseRuntimeValues()
        {
            if (predictionLabSettings.flightAugmentTuningEnabled && _flightSettings != null && _nativeFlight != null)
                ApplyFlightSnapshot(_nativeFlight);
            if (predictionLabSettings.predictionTuningEnabled && _predictionSettings != null && _nativePrediction != null)
                ApplyPredictionSnapshot(_nativePrediction);
            if (predictionLabSettings.cornerDetectorTuningEnabled && _corner != null && _nativeCorner != null)
                ApplyCornerSnapshot(_nativeCorner);
            if (predictionLabSettings.lipMagnetTuningEnabled && _lip != null && _nativeLip != null)
                ApplyLipSnapshot(_nativeLip);
            DestroyVisualization();
        }

        public static void RestoreAllGameValues()
        {
            RestoreFlight();
            RestorePrediction();
            RestoreCorner();
            RestoreLip();
            predictionLabSettings.visualizationEnabled = false;
            DestroyVisualization();
            Config.RequestSave();
        }

        public static bool BeginFlightCustomization()
        {
            if (!TryResolveFlight()) return false;
            _nativeFlight = CaptureFlight(_flightSettings);
            CopyFlightSnapshotToConfig(_nativeFlight);
            predictionLabSettings.flightAugmentTuningEnabled = true;
            ApplyFlight();
            Config.RequestSave();
            return true;
        }

        public static void RestoreFlight()
        {
            if (TryResolveFlight() && _nativeFlight != null) ApplyFlightSnapshot(_nativeFlight);
            predictionLabSettings.flightAugmentTuningEnabled = false;
            Config.RequestSave();
        }

        public static bool BeginPredictionCustomization()
        {
            if (!TryResolvePrediction()) return false;
            _nativePrediction = CapturePrediction(_predictionSettings);
            CopyPredictionSnapshotToConfig(_nativePrediction);
            predictionLabSettings.predictionTuningEnabled = true;
            ApplyPrediction();
            Config.RequestSave();
            return true;
        }

        public static void RestorePrediction()
        {
            if (TryResolvePrediction() && _nativePrediction != null) ApplyPredictionSnapshot(_nativePrediction);
            predictionLabSettings.predictionTuningEnabled = false;
            Config.RequestSave();
        }

        public static bool BeginCornerCustomization()
        {
            if (!TryResolveCorner()) return false;
            _nativeCorner = CaptureCorner(_corner);
            CopyCornerSnapshotToConfig(_nativeCorner);
            predictionLabSettings.cornerDetectorTuningEnabled = true;
            ApplyCorner();
            Config.RequestSave();
            return true;
        }

        public static void RestoreCorner()
        {
            if (TryResolveCorner() && _nativeCorner != null) ApplyCornerSnapshot(_nativeCorner);
            predictionLabSettings.cornerDetectorTuningEnabled = false;
            Config.RequestSave();
        }

        public static bool BeginLipCustomization()
        {
            if (!TryResolveLip()) return false;
            _nativeLip = CaptureLip(_lip);
            CopyLipSnapshotToConfig(_nativeLip);
            predictionLabSettings.lipMagnetTuningEnabled = true;
            ApplyLip();
            Config.RequestSave();
            return true;
        }

        public static void RestoreLip()
        {
            if (TryResolveLip() && _nativeLip != null) ApplyLipSnapshot(_nativeLip);
            predictionLabSettings.lipMagnetTuningEnabled = false;
            Config.RequestSave();
        }

        private static void ApplyFlight()
        {
            if (!predictionLabSettings.flightAugmentTuningEnabled || !TryResolveFlight()) return;
            PredictionLabSettings s = predictionLabSettings;
            _flightSettings._correctionStrength = Mathf.Clamp(s.flightCorrectionStrength, 0f, 100f);
            _flightSettings.forceCap = Mathf.Clamp(s.flightForceCap, 0f, 1000f);
            _flightSettings._normalPushOff = Mathf.Clamp(s.flightNormalPushOff, -100f, 100f);
            _flightSettings.maxVelAdjust = Mathf.Clamp(s.flightMaximumVelocityAdjust, 0f, 100f);
            _flightSettings.minTimeInAir = Mathf.Clamp(s.flightMinimumAirTime, 0f, 5f);
            _flightSettings.maxTickTime = Mathf.Clamp(s.flightMaximumTickTime, 0f, 5f);
            _flightSettings._landingPitch = Mathf.Clamp(s.flightLandingPitch, -90f, 90f);
            _flightSettings.closestToBody = s.flightClosestToBody;
            _flightSettings.furthestPoint = s.flightFurthestPoint;
            _flightSettings.steepestAngle = s.flightSteepestAngle;
            _flightSettings.DontTickIfFalling = s.flightDoNotTickIfFalling;
        }

        private static void ApplyPrediction()
        {
            if (!predictionLabSettings.predictionTuningEnabled || !TryResolvePrediction()) return;
            PredictionLabSettings s = predictionLabSettings;
            _predictionSettings.steps = Mathf.Clamp(s.predictionSteps, 1, 1000);
            _predictionSettings.pathTimeStep = Mathf.Clamp(s.predictionPathTimeStep, 0.001f, 0.2f);
            _predictionSettings.startYOffset = Mathf.Clamp(s.predictionStartYOffset, -1f, 1f);
            _predictionSettings.tickHRZ = Mathf.Clamp(s.predictionTickInterval, 0f, 0.2f);
            _predictionSettings.castRadius = Mathf.Clamp(s.predictionCastRadius, 0f, 1f);
            _predictionSettings.castRadiusMin = Mathf.Clamp(s.predictionCastRadiusMin, 0f, 1f);
            _predictionSettings.averageSurfaceNormal_numberOfRays = Mathf.Clamp(s.predictionSurfaceNormalRays, 1, 128);
            _predictionSettings.averageSurfaceNormal_maxCastDist = Mathf.Clamp(s.predictionSurfaceNormalMaxDistance, 0f, 20f);
            _predictionSettings.averageSurfaceNormal_startOffset = Mathf.Clamp(s.predictionSurfaceNormalStartOffset, 0f, 10f);
            _predictionSettings.averageSurfaceNormal_raySpread = Mathf.Clamp(s.predictionSurfaceNormalSpread, 0f, 10f);
            _predictionSettings.preferPlayerUp = s.predictionPreferPlayerUp;
            _predictionSettings.preferTranisitionAngle = s.predictionPreferTransitionAngle;
            _predictionSettings.wantedTrasitionNormal = SafeNormal(new Vector3(
                s.predictionWantedNormalX, s.predictionWantedNormalY, s.predictionWantedNormalZ));
            _predictionSettings.landingNormalPitch = Mathf.Clamp(s.predictionLandingNormalPitch, -90f, 90f);
        }

        private static void ApplyCorner()
        {
            if (!predictionLabSettings.cornerDetectorTuningEnabled || !TryResolveCorner()) return;
            PredictionLabSettings s = predictionLabSettings;
            _corner.SetDistMult(Mathf.Clamp(s.cornerDistanceMultiplier, 0.1f, 10f));
            _corner._cornerCheckDistance = Mathf.Clamp(s.cornerCheckDistance, 0.1f, 10f);
            _corner.NumberOfRays = Mathf.Clamp(s.cornerNumberOfRays, 4, 360);
            _corner._dotFacing = Mathf.Clamp01(s.cornerFacingDot);
            _corner._yNormalThreshold = Mathf.Clamp01(s.cornerYNormalThreshold);
            _corner._startUpOffset = Mathf.Clamp01(s.cornerStartUpOffset);
            _corner._edgeCheckMax = Mathf.Clamp(s.cornerEdgeCheckMaximum, 0.25f, 20f);
            _corner._edgeStep = Mathf.Clamp(s.cornerEdgeStep, 0.01f, 0.5f);
            _corner._edgeProbeOutset = Mathf.Clamp(s.cornerEdgeProbeOutset, 0f, 0.2f);
            _corner._edgeAngleToleranceDeg = Mathf.Clamp(s.cornerEdgeAngleTolerance, 0f, 30f);
            _corner._missesAllowed = Mathf.Clamp(s.cornerMissesAllowed, 0, 2);
            _corner._downTestDist = Mathf.Clamp(s.cornerDownTestDistance, 0f, 0.5f);
            _corner._upWalkTestDist = Mathf.Clamp(s.cornerUpWalkTestDistance, 0f, 0.5f);
            _corner._inwardTestDist = Mathf.Clamp(s.cornerInwardTestDistance, 0f, 0.5f);
        }

        private static void ApplyLip()
        {
            if (!predictionLabSettings.lipMagnetTuningEnabled || !TryResolveLip()) return;
            PredictionLabSettings s = predictionLabSettings;
            _lip._userPowerMult = Mathf.Clamp01(s.lipPowerMultiplier);
            _lip._userLengthMult = Mathf.Clamp(s.lipLengthMultiplier, 0f, 3f);
            _lip._normalPushOff = Mathf.Clamp(s.lipNormalPushOff, -0.3f, 0.3f);
            _lip._minYFromMaxFlight = Mathf.Clamp(s.lipMinimumYFromFlightApex, -1f, 1f);
            _lip._minDistanceTo = Mathf.Clamp(s.lipMinimumDistance, -0.5f, 0.5f);
            _lip._distanceBoost = Mathf.Clamp(s.lipDistanceBoost, -0.5f, 0.5f);
            _lip._alignAssist = s.lipAlignmentAssist;
            _lip._maxMagnetVelThres = Mathf.Clamp(s.lipMaximumMagnetVelocity, 0f, 100f);
            _lip._rotate = Mathf.Clamp(s.lipRotation, -90f, 90f);
            _lip._overrideVert = s.lipOverrideVertical;
            _lip._runFlightPrediction = s.lipRunFlightPrediction;
            _lip._preferVertAssist = s.lipPreferVerticalAssist;
            _lip._allowNetativePush = s.lipAllowNegativePush;
            _lip._moveDeltaMult = Mathf.Clamp(s.lipMoveDeltaMultiplier, 0f, 10f);
            _lip._maxMoveDelta = Mathf.Clamp(s.lipMaximumMoveDelta, 0f, 10f);
        }

        private static bool TryResolveFlight()
        {
            FlightAugmentSettings candidate = vehicleController?.FlightAugmentSettings;
            if (candidate == null) return false;
            int id = candidate.GetInstanceID();
            if (_flightSettings == null || _flightId != id)
            {
                _flightSettings = candidate;
                _flightId = id;
                _nativeFlight = CaptureFlight(candidate);
                _flightAugment = vehicleController.GetComponentInChildren<FlightAugmentTest>(true);
                Log.Msg($"[PredictionLab] Captured native Flight Augment settings from {candidate.name}.");
            }
            else if (_flightAugment == null)
            {
                _flightAugment = vehicleController.GetComponentInChildren<FlightAugmentTest>(true);
            }
            return true;
        }

        private static bool TryResolvePrediction()
        {
            FlightPrediction candidate = vehicleController?._flightPrediction;
            if (candidate == null) return false;
            FlightPredictionSettings settings = candidate.settings;
            if (settings == null) return false;
            int id = settings.GetInstanceID();
            if (_prediction == null || _predictionSettingsId != id)
            {
                _prediction = candidate;
                _predictionSettings = settings;
                _predictionSettingsId = id;
                _nativePrediction = CapturePrediction(settings);
                Log.Msg($"[PredictionLab] Captured native Flight Prediction settings from {settings.name}.");
            }
            return true;
        }

        private static bool TryResolveCorner()
        {
            CornerDetector candidate = vehicleController?._mainGrindMagnet?._cornerDetector;
            if (candidate == null)
            {
                GrindMagnetZEM magnet = vehicleController?.GetComponentInChildren<GrindMagnetZEM>(true);
                candidate = magnet?._cornerDetector;
            }
            if (candidate == null) return false;
            int id = candidate.GetInstanceID();
            if (_corner == null || _cornerId != id)
            {
                _corner = candidate;
                _cornerId = id;
                _nativeCorner = CaptureCorner(candidate);
                Log.Msg($"[PredictionLab] Captured native CornerDetector values from {candidate.name}.");
            }
            return true;
        }

        private static bool TryResolveLip()
        {
            LipMagnet candidate = vehicleController?.GetComponentInChildren<LipMagnet>(true);
            if (candidate == null) return false;
            int id = candidate.GetInstanceID();
            if (_lip == null || _lipId != id)
            {
                _lip = candidate;
                _lipId = id;
                _nativeLip = CaptureLip(candidate);
                Log.Msg($"[PredictionLab] Captured native LipMagnet values from {candidate.name}.");
            }
            return true;
        }

        private static FlightSnapshot CaptureFlight(FlightAugmentSettings s) => new FlightSnapshot
        {
            CorrectionStrength = s._correctionStrength, ForceCap = s.forceCap,
            NormalPushOff = s._normalPushOff, MaximumVelocityAdjust = s.maxVelAdjust,
            MinimumAirTime = s.minTimeInAir, MaximumTickTime = s.maxTickTime,
            LandingPitch = s._landingPitch, ClosestToBody = s.closestToBody,
            FurthestPoint = s.furthestPoint, SteepestAngle = s.steepestAngle,
            DoNotTickIfFalling = s.DontTickIfFalling
        };

        private static PredictionSnapshot CapturePrediction(FlightPredictionSettings s) => new PredictionSnapshot
        {
            Steps = s.steps, PathTimeStep = s.pathTimeStep, StartYOffset = s.startYOffset,
            TickInterval = s.tickHRZ, CastRadius = s.castRadius, CastRadiusMin = s.castRadiusMin,
            SurfaceNormalRays = s.averageSurfaceNormal_numberOfRays,
            SurfaceNormalMaxDistance = s.averageSurfaceNormal_maxCastDist,
            SurfaceNormalStartOffset = s.averageSurfaceNormal_startOffset,
            SurfaceNormalSpread = s.averageSurfaceNormal_raySpread,
            PreferPlayerUp = s.preferPlayerUp, PreferTransitionAngle = s.preferTranisitionAngle,
            WantedTransitionNormal = s.wantedTrasitionNormal, LandingNormalPitch = s.landingNormalPitch
        };

        private static CornerSnapshot CaptureCorner(CornerDetector c) => new CornerSnapshot
        {
            DistanceMultiplier = c.DistanceMult, CheckDistance = c.CornerCheckDistance,
            NumberOfRays = c.NumberOfRays, FacingDot = c._dotFacing,
            YNormalThreshold = c._yNormalThreshold, StartUpOffset = c._startUpOffset,
            EdgeCheckMaximum = c._edgeCheckMax, EdgeStep = c._edgeStep,
            EdgeProbeOutset = c._edgeProbeOutset, EdgeAngleTolerance = c._edgeAngleToleranceDeg,
            MissesAllowed = c._missesAllowed, DownTestDistance = c._downTestDist,
            UpWalkTestDistance = c._upWalkTestDist, InwardTestDistance = c._inwardTestDist
        };

        private static LipSnapshot CaptureLip(LipMagnet l) => new LipSnapshot
        {
            PowerMultiplier = l._userPowerMult, LengthMultiplier = l._userLengthMult,
            NormalPushOff = l._normalPushOff, MinimumYFromFlightApex = l._minYFromMaxFlight,
            MinimumDistance = l._minDistanceTo, DistanceBoost = l._distanceBoost,
            AlignmentAssist = l._alignAssist, MaximumMagnetVelocity = l._maxMagnetVelThres,
            Rotation = l._rotate, OverrideVertical = l._overrideVert,
            RunFlightPrediction = l._runFlightPrediction, PreferVerticalAssist = l._preferVertAssist,
            AllowNegativePush = l._allowNetativePush, MoveDeltaMultiplier = l._moveDeltaMult,
            MaximumMoveDelta = l._maxMoveDelta
        };

        private static void CopyFlightSnapshotToConfig(FlightSnapshot n)
        {
            if (n == null) return;
            PredictionLabSettings s = predictionLabSettings;
            s.flightCorrectionStrength = n.CorrectionStrength; s.flightForceCap = n.ForceCap;
            s.flightNormalPushOff = n.NormalPushOff; s.flightMaximumVelocityAdjust = n.MaximumVelocityAdjust;
            s.flightMinimumAirTime = n.MinimumAirTime; s.flightMaximumTickTime = n.MaximumTickTime;
            s.flightLandingPitch = n.LandingPitch; s.flightClosestToBody = n.ClosestToBody;
            s.flightFurthestPoint = n.FurthestPoint; s.flightSteepestAngle = n.SteepestAngle;
            s.flightDoNotTickIfFalling = n.DoNotTickIfFalling;
        }

        private static void CopyPredictionSnapshotToConfig(PredictionSnapshot n)
        {
            if (n == null) return;
            PredictionLabSettings s = predictionLabSettings;
            s.predictionSteps = n.Steps; s.predictionPathTimeStep = n.PathTimeStep;
            s.predictionStartYOffset = n.StartYOffset; s.predictionTickInterval = n.TickInterval;
            s.predictionCastRadius = n.CastRadius; s.predictionCastRadiusMin = n.CastRadiusMin;
            s.predictionSurfaceNormalRays = n.SurfaceNormalRays;
            s.predictionSurfaceNormalMaxDistance = n.SurfaceNormalMaxDistance;
            s.predictionSurfaceNormalStartOffset = n.SurfaceNormalStartOffset;
            s.predictionSurfaceNormalSpread = n.SurfaceNormalSpread;
            s.predictionPreferPlayerUp = n.PreferPlayerUp;
            s.predictionPreferTransitionAngle = n.PreferTransitionAngle;
            s.predictionWantedNormalX = n.WantedTransitionNormal.x;
            s.predictionWantedNormalY = n.WantedTransitionNormal.y;
            s.predictionWantedNormalZ = n.WantedTransitionNormal.z;
            s.predictionLandingNormalPitch = n.LandingNormalPitch;
        }

        private static void CopyCornerSnapshotToConfig(CornerSnapshot n)
        {
            if (n == null) return;
            PredictionLabSettings s = predictionLabSettings;
            s.cornerDistanceMultiplier = n.DistanceMultiplier; s.cornerCheckDistance = n.CheckDistance;
            s.cornerNumberOfRays = n.NumberOfRays; s.cornerFacingDot = n.FacingDot;
            s.cornerYNormalThreshold = n.YNormalThreshold; s.cornerStartUpOffset = n.StartUpOffset;
            s.cornerEdgeCheckMaximum = n.EdgeCheckMaximum; s.cornerEdgeStep = n.EdgeStep;
            s.cornerEdgeProbeOutset = n.EdgeProbeOutset; s.cornerEdgeAngleTolerance = n.EdgeAngleTolerance;
            s.cornerMissesAllowed = n.MissesAllowed; s.cornerDownTestDistance = n.DownTestDistance;
            s.cornerUpWalkTestDistance = n.UpWalkTestDistance; s.cornerInwardTestDistance = n.InwardTestDistance;
        }

        private static void CopyLipSnapshotToConfig(LipSnapshot n)
        {
            if (n == null) return;
            PredictionLabSettings s = predictionLabSettings;
            s.lipPowerMultiplier = n.PowerMultiplier; s.lipLengthMultiplier = n.LengthMultiplier;
            s.lipNormalPushOff = n.NormalPushOff; s.lipMinimumYFromFlightApex = n.MinimumYFromFlightApex;
            s.lipMinimumDistance = n.MinimumDistance; s.lipDistanceBoost = n.DistanceBoost;
            s.lipAlignmentAssist = n.AlignmentAssist; s.lipMaximumMagnetVelocity = n.MaximumMagnetVelocity;
            s.lipRotation = n.Rotation; s.lipOverrideVertical = n.OverrideVertical;
            s.lipRunFlightPrediction = n.RunFlightPrediction; s.lipPreferVerticalAssist = n.PreferVerticalAssist;
            s.lipAllowNegativePush = n.AllowNegativePush; s.lipMoveDeltaMultiplier = n.MoveDeltaMultiplier;
            s.lipMaximumMoveDelta = n.MaximumMoveDelta;
        }

        private static void ApplyFlightSnapshot(FlightSnapshot n)
        {
            _flightSettings._correctionStrength = n.CorrectionStrength; _flightSettings.forceCap = n.ForceCap;
            _flightSettings._normalPushOff = n.NormalPushOff; _flightSettings.maxVelAdjust = n.MaximumVelocityAdjust;
            _flightSettings.minTimeInAir = n.MinimumAirTime; _flightSettings.maxTickTime = n.MaximumTickTime;
            _flightSettings._landingPitch = n.LandingPitch; _flightSettings.closestToBody = n.ClosestToBody;
            _flightSettings.furthestPoint = n.FurthestPoint; _flightSettings.steepestAngle = n.SteepestAngle;
            _flightSettings.DontTickIfFalling = n.DoNotTickIfFalling;
        }

        private static void ApplyPredictionSnapshot(PredictionSnapshot n)
        {
            _predictionSettings.steps = n.Steps; _predictionSettings.pathTimeStep = n.PathTimeStep;
            _predictionSettings.startYOffset = n.StartYOffset; _predictionSettings.tickHRZ = n.TickInterval;
            _predictionSettings.castRadius = n.CastRadius; _predictionSettings.castRadiusMin = n.CastRadiusMin;
            _predictionSettings.averageSurfaceNormal_numberOfRays = n.SurfaceNormalRays;
            _predictionSettings.averageSurfaceNormal_maxCastDist = n.SurfaceNormalMaxDistance;
            _predictionSettings.averageSurfaceNormal_startOffset = n.SurfaceNormalStartOffset;
            _predictionSettings.averageSurfaceNormal_raySpread = n.SurfaceNormalSpread;
            _predictionSettings.preferPlayerUp = n.PreferPlayerUp;
            _predictionSettings.preferTranisitionAngle = n.PreferTransitionAngle;
            _predictionSettings.wantedTrasitionNormal = n.WantedTransitionNormal;
            _predictionSettings.landingNormalPitch = n.LandingNormalPitch;
        }

        private static void ApplyCornerSnapshot(CornerSnapshot n)
        {
            _corner.SetDistMult(n.DistanceMultiplier); _corner._cornerCheckDistance = n.CheckDistance;
            _corner.NumberOfRays = n.NumberOfRays; _corner._dotFacing = n.FacingDot;
            _corner._yNormalThreshold = n.YNormalThreshold; _corner._startUpOffset = n.StartUpOffset;
            _corner._edgeCheckMax = n.EdgeCheckMaximum; _corner._edgeStep = n.EdgeStep;
            _corner._edgeProbeOutset = n.EdgeProbeOutset; _corner._edgeAngleToleranceDeg = n.EdgeAngleTolerance;
            _corner._missesAllowed = n.MissesAllowed; _corner._downTestDist = n.DownTestDistance;
            _corner._upWalkTestDist = n.UpWalkTestDistance; _corner._inwardTestDist = n.InwardTestDistance;
        }

        private static void ApplyLipSnapshot(LipSnapshot n)
        {
            _lip._userPowerMult = n.PowerMultiplier; _lip._userLengthMult = n.LengthMultiplier;
            _lip._normalPushOff = n.NormalPushOff; _lip._minYFromMaxFlight = n.MinimumYFromFlightApex;
            _lip._minDistanceTo = n.MinimumDistance; _lip._distanceBoost = n.DistanceBoost;
            _lip._alignAssist = n.AlignmentAssist; _lip._maxMagnetVelThres = n.MaximumMagnetVelocity;
            _lip._rotate = n.Rotation; _lip._overrideVert = n.OverrideVertical;
            _lip._runFlightPrediction = n.RunFlightPrediction; _lip._preferVertAssist = n.PreferVerticalAssist;
            _lip._allowNetativePush = n.AllowNegativePush; _lip._moveDeltaMult = n.MoveDeltaMultiplier;
            _lip._maxMoveDelta = n.MaximumMoveDelta;
        }

        private static void UpdateVisualization()
        {
            if (!TryResolvePrediction() || vehicleController == null || vehicleController.TimeInAir <= 0.02f)
            {
                SetVisualizationVisible(false);
                return;
            }

            FlightPathInfo path = _prediction._flightPathInfo;
            if (path.numStepOnHit <= 0 || !IsFinite(path.LaunchPoint) ||
                !IsFinite(path.maxPoint) || !IsFinite(path.LandingPos))
            {
                SetVisualizationVisible(false);
                return;
            }

            EnsureVisualization();
            SetVisualizationVisible(true);
            PredictionLabSettings s = predictionLabSettings;
            int segments = Mathf.Clamp(s.visualizationArcSegments, 6, 96);
            float width = Mathf.Clamp(s.visualizationLineWidth, 0.005f, 0.15f);
            float markerSize = Mathf.Clamp(s.visualizationMarkerSize, 0.05f, 2f);
            Color color = new Color(Mathf.Clamp01(s.visualizationColorR), Mathf.Clamp01(s.visualizationColorG),
                Mathf.Clamp01(s.visualizationColorB), 1f);
            ConfigureLine(_arcLine, color, width);
            ConfigureLine(_markerForward, color, width);
            ConfigureLine(_markerRight, color, width);
            ConfigureLine(_normalLine, Color.Lerp(color, Color.white, 0.35f), width * 0.75f);

            _arcLine.enabled = s.visualizationShowArc;
            if (_arcLine.enabled)
            {
                _arcLine.positionCount = segments + 1;
                Vector3 start = path.LaunchPoint;
                Vector3 end = path.LandingPos;
                Vector3 control = (path.maxPoint * 2f) - ((start + end) * 0.5f);
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float omt = 1f - t;
                    _arcLine.SetPosition(i, (omt * omt * start) + (2f * omt * t * control) + (t * t * end));
                }
            }

            Vector3 normal = SafeNormal(path.surfaceHitInfo.normal);
            Vector3 landing = path.LandingPos + (normal * 0.01f);
            Vector3 forward = SafePlaneDirection(_prediction.BestLandingForward, normal, Vector3.forward);
            Vector3 right = SafePlaneDirection(_prediction.BestLandingRight, normal, Vector3.right);
            bool showMarker = s.visualizationShowLandingMarker;
            SetTwoPointLine(_markerForward, showMarker, landing - forward * markerSize, landing + forward * markerSize);
            SetTwoPointLine(_markerRight, showMarker, landing - right * markerSize, landing + right * markerSize);
            SetTwoPointLine(_normalLine, s.visualizationShowSurfaceNormal, landing, landing + normal * markerSize * 1.5f);
        }

        private static void EnsureVisualization()
        {
            if (_visualRoot != null) return;
            _visualRoot = new GameObject("RoweMod Native Prediction Display");
            _visualRoot.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial = CreateLineMaterial();
            _arcLine = CreateLine("Prediction Arc");
            _markerForward = CreateLine("Landing Forward");
            _markerRight = CreateLine("Landing Right");
            _normalLine = CreateLine("Landing Normal");
        }

        private static LineRenderer CreateLine(string name)
        {
            GameObject child = new GameObject(name);
            child.hideFlags = HideFlags.HideAndDontSave;
            child.transform.SetParent(_visualRoot.transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = _lineMaterial;
            return line;
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader) { name = "RoweMod Prediction Depth Material" };
            material.hideFlags = HideFlags.HideAndDontSave;
            material.renderQueue = (int)RenderQueue.GeometryLast;
            if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            return material;
        }

        private static void ConfigureLine(LineRenderer line, Color color, float width)
        {
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }

        private static void SetTwoPointLine(LineRenderer line, bool visible, Vector3 a, Vector3 b)
        {
            line.enabled = visible;
            if (!visible) return;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        private static void SetVisualizationVisible(bool visible)
        {
            if (_visualRoot != null && _visualRoot.activeSelf != visible)
                _visualRoot.SetActive(visible);
        }

        private static void DestroyVisualization()
        {
            if (_visualRoot != null) UnityEngine.Object.Destroy(_visualRoot);
            if (_lineMaterial != null) UnityEngine.Object.Destroy(_lineMaterial);
            _visualRoot = null;
            _arcLine = _markerForward = _markerRight = _normalLine = null;
            _lineMaterial = null;
        }

        private static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        private static Vector3 SafeNormal(Vector3 value)
        {
            if (!IsFinite(value) || value.sqrMagnitude < 0.0001f) return Vector3.up;
            return value.normalized;
        }

        private static Vector3 SafePlaneDirection(Vector3 value, Vector3 normal, Vector3 fallback)
        {
            Vector3 projected = Vector3.ProjectOnPlane(value, normal);
            if (!IsFinite(projected) || projected.sqrMagnitude < 0.0001f)
                projected = Vector3.ProjectOnPlane(fallback, normal);
            if (projected.sqrMagnitude < 0.0001f)
                projected = Vector3.Cross(normal, Vector3.right);
            return projected.normalized;
        }
    }
}
