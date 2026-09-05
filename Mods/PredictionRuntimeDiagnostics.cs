using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using ActiveRagdollBehaviour = Il2CppMashBox.Addons.PhysicsDrivenAnimation.ActiveRagdollBehaviour;
using AnimatedRagdoll = Il2CppMashBox.Addons.PhysicsDrivenAnimation.AnimatedRagdoll;
using CornerDetector = Il2CppMashBox.BMX_Physics_Development.CornerDetector;
using FlightAugmentTest = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightAugmentTest;
using FlightPathInfo = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightPathInfo;
using FlightPrediction = Il2CppMashBox.Core.Runtime.Physics.ProjectileMotion.FlightPrediction;
using GrindMagnetZEM = Il2CppMashBox.BMX_Physics_Development.GrindMagnetZEM;
using LipMagnet = Il2CppMashBox.BMX_Physics_Development.LipMagnet;
using TransitionScanner = Il2CppMashBox.Core.Runtime.Physics.Raycasting.SurfaceScanning.TransitionScanner;
using VehicleBalancePID = Il2CppMashBox.BMX_Physics_Development.VehicleBalancePID;

namespace rowemod.Mods
{
    /// <summary>
    /// Read-only runtime inventory and one-jump recorder for the native landing pipeline.
    /// It never writes native settings, transforms, rigidbodies, joints, IK targets, or forces.
    /// </summary>
    internal static class PredictionRuntimeDiagnostics
    {
        private enum CaptureState
        {
            Idle,
            Armed,
            Recording,
            Complete,
            Failed
        }

        private sealed class RuntimeTopology
        {
            public Il2CppMashBox.BMX_Physics_Development.VehicleController Vehicle;
            public FlightPrediction ControllerPrediction;
            public FlightAugmentTest FlightAugment;
            public FlightPrediction AugmentPrediction;
            public TransitionScanner AugmentScanner;
            public GrindMagnetZEM GrindMagnet;
            public FlightPrediction GrindPrediction;
            public FlightPrediction GrindAlignmentPrediction;
            public CornerDetector GrindCorner;
            public LipMagnet LipMagnet;
            public FlightPrediction LipAlignmentPrediction;
            public CornerDetector LipCorner;
            public VehicleBalancePID Balance;
            public ActiveRagdollBehaviour ActiveRagdoll;
            public AnimatedRagdoll AnimatedRagdoll;
        }

        private const float AirborneThreshold = 0.02f;
        private const float SampleInterval = 0.02f;
        private const float CaptureTimeout = 15f;
        private const int GroundedFramesToFinish = 3;

        private static CaptureState _state;
        private static string _status = "Topology has not been inspected yet.";
        private static string _topologySummary = "Press Refresh Topology after entering gameplay.";
        private static string _lastReportPath;
        private static string _lastCapturePath;
        private static StringBuilder _captureCsv;
        private static float _captureStartedAt;
        private static float _nextSampleAt;
        private static int _groundedFinishFrames;
        private static int _samples;
        private static int _pathResolvedSamples;
        private static int _augmentSamples;
        private static int _grindSamples;
        private static int _lipSamples;
        private static float _maxAugmentForce;
        private static float _maxGrindDistance;
        private static float _maxLipForce;
        private static float _maxRagdollPositionError;
        private static float _maxRagdollAngleError;

        public static string Status => _status;
        public static string TopologySummary => _topologySummary;
        public static string LastReportPath => _lastReportPath;
        public static string LastCapturePath => _lastCapturePath;
        public static bool IsArmed => _state == CaptureState.Armed;
        public static bool IsRecording => _state == CaptureState.Recording;
        public static bool CanCancel => IsArmed || IsRecording;

        public static void OnSceneInitialized()
        {
            if (_state == CaptureState.Recording)
                FinishCapture("scene changed");
            else if (_state == CaptureState.Armed)
                CancelCapture("Capture cancelled because the scene changed.");

            _topologySummary = "Press Refresh Topology after the local bike and rider load.";
        }

        public static void Release()
        {
            if (_state == CaptureState.Recording)
                FinishCapture("RoweMod disabled");
            else if (_state == CaptureState.Armed)
                CancelCapture("Capture cancelled because RoweMod was disabled.");
        }

        public static bool RefreshTopology()
        {
            try
            {
                RuntimeTopology topology = ResolveTopology();
                if (topology.Vehicle == null)
                {
                    _status = "Waiting for the local VehicleController.";
                    _topologySummary = "No local bike topology is available yet.";
                    return false;
                }

                string report = BuildTopologyReport(topology);
                string directory = EnsureDiagnosticsDirectory();
                _lastReportPath = Path.Combine(directory,
                    $"prediction-topology-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(_lastReportPath, report);
                _topologySummary = BuildTopologySummary(topology);
                _status = "Runtime topology refreshed and written to a report.";
                rowemod.Utils.Log.Msg("[PredictionTopology] " + _topologySummary);
                LogTopologyIdentities(topology);
                return true;
            }
            catch (Exception ex)
            {
                _state = CaptureState.Failed;
                _status = "Topology refresh failed: " + ex.Message;
                rowemod.Utils.Log.Warning("[PredictionTopology] " + _status);
                return false;
            }
        }

        public static bool ArmOneJumpCapture()
        {
            if (_state == CaptureState.Recording)
                return false;

            RuntimeTopology topology;
            try
            {
                topology = ResolveTopology();
            }
            catch (Exception ex)
            {
                _state = CaptureState.Failed;
                _status = "Could not arm capture: " + ex.Message;
                return false;
            }

            if (topology.Vehicle == null)
            {
                _status = "Could not arm capture: local VehicleController is not ready.";
                return false;
            }

            ResetCaptureStatistics();
            _state = CaptureState.Armed;
            _status = "One-jump capture armed. Close the menu, then jump and land.";
            _topologySummary = BuildTopologySummary(topology);
            rowemod.Utils.Log.Msg("[PredictionCapture] Armed; waiting for takeoff.");
            return true;
        }

        public static void CancelCapture()
        {
            CancelCapture("One-jump capture cancelled.");
        }

        public static void Update()
        {
            if (_state != CaptureState.Armed && _state != CaptureState.Recording)
                return;

            try
            {
                RuntimeTopology topology = ResolveTopology();
                if (topology.Vehicle == null)
                {
                    if (_state == CaptureState.Recording)
                        FinishCapture("vehicle disappeared");
                    else
                        _status = "Capture armed; waiting for the local bike.";
                    return;
                }

                bool grounded = SafeGrounded(topology.Vehicle);
                bool airborne = !grounded && topology.Vehicle.TimeInAir > AirborneThreshold;
                if (_state == CaptureState.Armed)
                {
                    if (!airborne)
                        return;

                    BeginCapture(topology);
                }

                float now = Time.unscaledTime;
                if (now >= _nextSampleAt)
                {
                    RecordSample(topology, grounded);
                    _nextSampleAt = now + SampleInterval;
                }

                if (grounded && topology.Vehicle.TimeInAir <= AirborneThreshold)
                    _groundedFinishFrames++;
                else
                    _groundedFinishFrames = 0;

                if (_groundedFinishFrames >= GroundedFramesToFinish)
                    FinishCapture("landed");
                else if (now - _captureStartedAt >= CaptureTimeout)
                    FinishCapture("timeout");
            }
            catch (Exception ex)
            {
                _state = CaptureState.Failed;
                _status = "One-jump capture failed: " + ex.Message;
                rowemod.Utils.Log.Warning("[PredictionCapture] " + _status);
            }
        }

        private static RuntimeTopology ResolveTopology()
        {
            var topology = new RuntimeTopology { Vehicle = vehicleController };
            if (topology.Vehicle == null)
                return topology;

            topology.ControllerPrediction = topology.Vehicle._flightPrediction;
            topology.FlightAugment = topology.Vehicle.GetComponentInChildren<FlightAugmentTest>(true);
            topology.AugmentPrediction = topology.FlightAugment?._flightPrediction;
            topology.AugmentScanner = topology.FlightAugment?._transitionScanner;
            topology.GrindMagnet = topology.Vehicle._mainGrindMagnet ??
                                    topology.Vehicle.GetComponentInChildren<GrindMagnetZEM>(true);
            topology.GrindPrediction = topology.GrindMagnet?._flightPrediction;
            topology.GrindAlignmentPrediction = topology.GrindMagnet?._flightPredictionAlign;
            topology.GrindCorner = topology.GrindMagnet?._cornerDetector;
            topology.LipMagnet = topology.GrindMagnet?._opposingMagnet ??
                                 topology.Vehicle.GetComponentInChildren<LipMagnet>(true);
            topology.LipAlignmentPrediction = topology.LipMagnet?._flightPredictionAlign;
            topology.LipCorner = topology.LipMagnet?._cornerDetector;
            topology.Balance = topology.Vehicle._vehicleBalancePid;

            if (rMbCharacter != null)
            {
                topology.ActiveRagdoll = rMbCharacter.GetComponentInChildren<ActiveRagdollBehaviour>(true);
                topology.AnimatedRagdoll = topology.ActiveRagdoll?._animatedRagdoll;
            }

            return topology;
        }

        private static string BuildTopologySummary(RuntimeTopology t)
        {
            int predictionComponents = CountUniquePointers(t.ControllerPrediction, t.AugmentPrediction,
                t.GrindPrediction, t.GrindAlignmentPrediction, t.LipAlignmentPrediction);
            int predictionSettings = CountUniquePointers(t.ControllerPrediction?.settings,
                t.AugmentPrediction?.settings, t.GrindPrediction?.settings,
                t.GrindAlignmentPrediction?.settings, t.LipAlignmentPrediction?.settings);
            bool transitionShared = Same(t.Vehicle?.TransitionSettings, t.AugmentScanner?._transitionSettings);
            bool flightSettingsShared = Same(t.Vehicle?.FlightAugmentSettings, t.FlightAugment?._augmentSettings);
            bool cornerShared = Same(t.GrindCorner, t.LipCorner);
            int bodies = rMbCharacter == null ? 0 :
                (rMbCharacter.GetComponentsInChildren<Rigidbody>(true)?.Length ?? 0);
            int joints = t.AnimatedRagdoll?._animatedPhysicsJoints?.Length ?? 0;

            return $"Predictions: {predictionComponents} component(s), {predictionSettings} settings asset(s). " +
                   $"Shared transition settings: {YesNoUnknown(t.Vehicle?.TransitionSettings, t.AugmentScanner?._transitionSettings, transitionShared)}. " +
                   $"Shared augment settings: {YesNoUnknown(t.Vehicle?.FlightAugmentSettings, t.FlightAugment?._augmentSettings, flightSettingsShared)}. " +
                   $"Shared grind/lip corner detector: {YesNoUnknown(t.GrindCorner, t.LipCorner, cornerShared)}. " +
                   $"Rider body: {bodies} rigidbodies, {joints} driven joints.";
        }

        private static string BuildTopologyReport(RuntimeTopology t)
        {
            var b = new StringBuilder(4096);
            b.AppendLine("RoweMod Native Prediction Topology");
            b.AppendLine("Captured: " + DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            b.AppendLine();
            b.AppendLine(BuildTopologySummary(t));
            b.AppendLine();
            AppendIdentity(b, "VehicleController", t.Vehicle);
            AppendIdentity(b, "ChassisBody", t.Vehicle?._chassisBody);
            AppendIdentity(b, "DriverSeatBody", t.Vehicle?._driverSeatBody);
            AppendIdentity(b, "DriveConnectionJoint", t.Vehicle?._driveConnectionJoint);
            b.AppendLine();
            AppendPredictionIdentity(b, "Controller prediction", t.ControllerPrediction);
            AppendPredictionIdentity(b, "Flight Augment prediction", t.AugmentPrediction);
            AppendPredictionIdentity(b, "Grind prediction", t.GrindPrediction);
            AppendPredictionIdentity(b, "Grind alignment prediction", t.GrindAlignmentPrediction);
            AppendPredictionIdentity(b, "Lip alignment prediction", t.LipAlignmentPrediction);
            b.AppendLine();
            AppendIdentity(b, "FlightAugmentTest", t.FlightAugment);
            AppendIdentity(b, "Vehicle FlightAugmentSettings", t.Vehicle?.FlightAugmentSettings);
            AppendIdentity(b, "Augment FlightAugmentSettings", t.FlightAugment?._augmentSettings);
            b.AppendLine("Vehicle/Augment settings shared: " +
                         YesNoUnknown(t.Vehicle?.FlightAugmentSettings, t.FlightAugment?._augmentSettings,
                             Same(t.Vehicle?.FlightAugmentSettings, t.FlightAugment?._augmentSettings)));
            b.AppendLine();
            AppendIdentity(b, "TransitionScanner", t.AugmentScanner);
            AppendIdentity(b, "Vehicle TransitionSettings", t.Vehicle?.TransitionSettings);
            AppendIdentity(b, "Scanner TransitionSettings", t.AugmentScanner?._transitionSettings);
            b.AppendLine("Vehicle/Scanner settings shared: " +
                         YesNoUnknown(t.Vehicle?.TransitionSettings, t.AugmentScanner?._transitionSettings,
                             Same(t.Vehicle?.TransitionSettings, t.AugmentScanner?._transitionSettings)));
            b.AppendLine();
            AppendIdentity(b, "GrindMagnetZEM", t.GrindMagnet);
            AppendIdentity(b, "Grind CornerDetector", t.GrindCorner);
            AppendIdentity(b, "LipMagnet", t.LipMagnet);
            AppendIdentity(b, "Lip CornerDetector", t.LipCorner);
            b.AppendLine("Grind/Lip corner shared: " +
                         YesNoUnknown(t.GrindCorner, t.LipCorner, Same(t.GrindCorner, t.LipCorner)));
            b.AppendLine("Lip predictive magnet points back to GrindMagnetZEM: " +
                         YesNoUnknown(t.LipMagnet?._predictiveMagnet, t.GrindMagnet,
                             Same(t.LipMagnet?._predictiveMagnet, t.GrindMagnet)));
            b.AppendLine();
            AppendIdentity(b, "VehicleBalancePID", t.Balance);
            AppendIdentity(b, "ActiveRagdollBehaviour", t.ActiveRagdoll);
            AppendIdentity(b, "AnimatedRagdoll", t.AnimatedRagdoll);
            if (t.AnimatedRagdoll != null)
            {
                b.AppendLine($"Animated ragdoll values: joints={t.AnimatedRagdoll._animatedPhysicsJoints?.Length ?? 0}, " +
                             $"strength={F(t.AnimatedRagdoll._strength)}, " +
                             $"positionalStrength={F(t.AnimatedRagdoll._positionalStrength)}, " +
                             $"relaxation={F(t.AnimatedRagdoll.RelaxationPercent)}, " +
                             $"positionError={F(t.AnimatedRagdoll.TotalPositionError)}, " +
                             $"angleError={F(t.AnimatedRagdoll.TotalAngleError)}");
            }

            b.AppendLine();
            b.AppendLine("This report is read-only. No native values were changed.");
            return b.ToString();
        }

        private static void LogTopologyIdentities(RuntimeTopology t)
        {
            rowemod.Utils.Log.Msg("[PredictionTopology] ControllerFP=" + ShortIdentity(t.ControllerPrediction) +
                                  " AugmentFP=" + ShortIdentity(t.AugmentPrediction) +
                                  " GrindFP=" + ShortIdentity(t.GrindPrediction) +
                                  " GrindAlignFP=" + ShortIdentity(t.GrindAlignmentPrediction) +
                                  " LipAlignFP=" + ShortIdentity(t.LipAlignmentPrediction));
            rowemod.Utils.Log.Msg("[PredictionTopology] ControllerSettings=" +
                                  ShortIdentity(t.ControllerPrediction?.settings) + " AugmentSettings=" +
                                  ShortIdentity(t.AugmentPrediction?.settings) + " GrindSettings=" +
                                  ShortIdentity(t.GrindPrediction?.settings) + " GrindAlignSettings=" +
                                  ShortIdentity(t.GrindAlignmentPrediction?.settings) + " LipAlignSettings=" +
                                  ShortIdentity(t.LipAlignmentPrediction?.settings));
        }

        private static void BeginCapture(RuntimeTopology topology)
        {
            _captureCsv = new StringBuilder(64 * 1024);
            _captureCsv.AppendLine(string.Join(",", CaptureColumns));
            _captureStartedAt = Time.unscaledTime;
            _nextSampleAt = _captureStartedAt;
            _groundedFinishFrames = 0;
            _state = CaptureState.Recording;
            _status = "Recording airborne prediction and physics state. Land to finish.";
            rowemod.Utils.Log.Msg("[PredictionCapture] Takeoff detected; recording started. " +
                                  BuildTopologySummary(topology));
        }

        private static void RecordSample(RuntimeTopology t, bool grounded)
        {
            FlightPrediction prediction = t.ControllerPrediction;
            FlightPathInfo path = prediction == null ? default : prediction._flightPathInfo;
            bool pathResolved = prediction != null && path.numStepOnHit > 0;
            bool augmenting = t.FlightAugment?.IsAugmenting ?? false;
            bool grinding = t.GrindMagnet?.IsPulling ?? false;
            bool lipPulling = t.LipMagnet?.IsPulling ?? false;
            Vector3 velocity = t.Vehicle?.Velocity ?? Vector3.zero;
            Vector3 landing = pathResolved ? path.LandingPos : Vector3.zero;
            Vector3 landingNormal = pathResolved ? path.surfaceHitInfo.normal : Vector3.zero;
            Vector3 augmentForce = t.FlightAugment?._adjustmentForce ?? Vector3.zero;
            Vector3 lipForce = t.LipMagnet?._magnetForce ?? Vector3.zero;
            AnimatedRagdoll ragdoll = t.AnimatedRagdoll;

            var values = new List<string>(CaptureColumns.Length)
            {
                F(Time.unscaledTime - _captureStartedAt),
                Time.frameCount.ToString(CultureInfo.InvariantCulture),
                B(grounded),
                F(t.Vehicle?.TimeInAir ?? 0f),
                F(t.Vehicle?.PercentThroughAir ?? 0f),
                F(velocity.x), F(velocity.y), F(velocity.z),
                B(pathResolved),
                path.numStepOnHit.ToString(CultureInfo.InvariantCulture),
                F(path.timeTillImpact),
                F(path.LaunchPoint.x), F(path.LaunchPoint.y), F(path.LaunchPoint.z),
                F(path.maxPoint.x), F(path.maxPoint.y), F(path.maxPoint.z),
                F(landing.x), F(landing.y), F(landing.z),
                F(path.landingVel.x), F(path.landingVel.y), F(path.landingVel.z),
                F(landingNormal.x), F(landingNormal.y), F(landingNormal.z),
                B(t.FlightAugment?._enteredFlight ?? false),
                B(t.FlightAugment?._enteredFlightLandingInTransition ?? false),
                B(augmenting),
                F(augmentForce.x), F(augmentForce.y), F(augmentForce.z),
                F(t.FlightAugment?._distanceToTarget ?? 0f),
                (t.AugmentScanner?._potentialLandingSpots?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                B(t.GrindCorner?.HasCorner ?? false),
                (t.GrindCorner?._leftCorners?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                (t.GrindCorner?._rightCorners?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                B(t.GrindMagnet?._alignTireRide ?? false),
                B(t.GrindMagnet?._startedPullThisFlight ?? false),
                B(t.GrindMagnet?._cancelledTillNextJump ?? false),
                B(grinding),
                F(t.GrindMagnet?._targetPos.x ?? 0f), F(t.GrindMagnet?._targetPos.y ?? 0f),
                F(t.GrindMagnet?._targetPos.z ?? 0f),
                F(t.GrindMagnet?._latDist ?? 0f),
                Csv(t.GrindMagnet?.LastChosenDetection),
                B(t.LipMagnet?._predictiveEngadged ?? false),
                B(lipPulling),
                F(t.LipMagnet?._targetPos.x ?? 0f), F(t.LipMagnet?._targetPos.y ?? 0f),
                F(t.LipMagnet?._targetPos.z ?? 0f),
                F(lipForce.x), F(lipForce.y), F(lipForce.z),
                F(t.LipMagnet?._distanceTo ?? 0f),
                B(t.Balance?._runPID ?? false),
                F(t.Balance?.currentAngleToX ?? 0f),
                F(t.Balance?.currentAngleToZ ?? 0f),
                F(t.Balance?.Normal.x ?? 0f), F(t.Balance?.Normal.y ?? 0f), F(t.Balance?.Normal.z ?? 0f),
                F(ragdoll?._strength ?? 0f),
                F(ragdoll?._positionalStrength ?? 0f),
                F(ragdoll?.RelaxationPercent ?? 0f),
                F(ragdoll?.TotalPositionError ?? 0f),
                F(ragdoll?.TotalAngleError ?? 0f)
            };

            _captureCsv.AppendLine(string.Join(",", values));
            _samples++;
            if (pathResolved) _pathResolvedSamples++;
            if (augmenting) _augmentSamples++;
            if (grinding) _grindSamples++;
            if (lipPulling) _lipSamples++;
            _maxAugmentForce = Mathf.Max(_maxAugmentForce, augmentForce.magnitude);
            _maxGrindDistance = Mathf.Max(_maxGrindDistance, Mathf.Abs(t.GrindMagnet?._latDist ?? 0f));
            _maxLipForce = Mathf.Max(_maxLipForce, lipForce.magnitude);
            _maxRagdollPositionError = Mathf.Max(_maxRagdollPositionError,
                Mathf.Abs(ragdoll?.TotalPositionError ?? 0f));
            _maxRagdollAngleError = Mathf.Max(_maxRagdollAngleError,
                Mathf.Abs(ragdoll?.TotalAngleError ?? 0f));
        }

        private static readonly string[] CaptureColumns =
        {
            "capture_time", "frame", "grounded", "time_in_air", "air_percent",
            "velocity_x", "velocity_y", "velocity_z", "path_resolved", "path_hit_step", "impact_time",
            "launch_x", "launch_y", "launch_z", "apex_x", "apex_y", "apex_z",
            "landing_x", "landing_y", "landing_z", "landing_velocity_x", "landing_velocity_y",
            "landing_velocity_z", "landing_normal_x", "landing_normal_y", "landing_normal_z",
            "augment_entered_flight", "augment_transition_landing", "augment_active",
            "augment_force_x", "augment_force_y", "augment_force_z", "augment_target_distance",
            "transition_candidates", "corner_found", "corner_left_candidates", "corner_right_candidates",
            "grind_tire_ride_armed", "grind_started_this_flight", "grind_cancelled", "grind_pulling",
            "grind_target_x", "grind_target_y", "grind_target_z", "grind_lateral_distance", "grind_detection",
            "lip_prediction_engaged", "lip_pulling", "lip_target_x", "lip_target_y", "lip_target_z",
            "lip_force_x", "lip_force_y", "lip_force_z", "lip_target_distance",
            "balance_pid", "balance_angle_x", "balance_angle_z", "balance_normal_x", "balance_normal_y",
            "balance_normal_z", "ragdoll_strength", "ragdoll_positional_strength", "ragdoll_relaxation",
            "ragdoll_position_error", "ragdoll_angle_error"
        };

        private static void FinishCapture(string reason)
        {
            try
            {
                if (_captureCsv == null || _samples == 0)
                {
                    _state = CaptureState.Failed;
                    _status = "Capture ended without samples: " + reason + ".";
                    return;
                }

                string directory = EnsureDiagnosticsDirectory();
                _lastCapturePath = Path.Combine(directory,
                    $"prediction-jump-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                File.WriteAllText(_lastCapturePath, _captureCsv.ToString());
                _state = CaptureState.Complete;
                _status = $"Jump captured: {_samples} samples; path {_pathResolvedSamples}, " +
                          $"augment {_augmentSamples}, grind {_grindSamples}, lip {_lipSamples}. " +
                          $"Ended: {reason}.";
                rowemod.Utils.Log.Msg("[PredictionCapture] " + _status +
                                      $" Max augment force={_maxAugmentForce:0.###}, " +
                                      $"max grind distance={_maxGrindDistance:0.###}, " +
                                      $"max lip force={_maxLipForce:0.###}, " +
                                      $"max ragdoll errors={_maxRagdollPositionError:0.###}/{_maxRagdollAngleError:0.###}. " +
                                      "File=" + _lastCapturePath);
            }
            catch (Exception ex)
            {
                _state = CaptureState.Failed;
                _status = "Could not save capture: " + ex.Message;
                rowemod.Utils.Log.Warning("[PredictionCapture] " + _status);
            }
            finally
            {
                _captureCsv = null;
            }
        }

        private static void CancelCapture(string status)
        {
            _state = CaptureState.Idle;
            _captureCsv = null;
            _status = status;
            _groundedFinishFrames = 0;
            rowemod.Utils.Log.Msg("[PredictionCapture] " + status);
        }

        private static void ResetCaptureStatistics()
        {
            _captureCsv = null;
            _samples = _pathResolvedSamples = _augmentSamples = _grindSamples = _lipSamples = 0;
            _maxAugmentForce = _maxGrindDistance = _maxLipForce = 0f;
            _maxRagdollPositionError = _maxRagdollAngleError = 0f;
            _groundedFinishFrames = 0;
        }

        private static string EnsureDiagnosticsDirectory()
        {
            string directory = Path.Combine(modFolder, "Diagnostics");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void AppendPredictionIdentity(StringBuilder b, string label, FlightPrediction prediction)
        {
            AppendIdentity(b, label, prediction);
            AppendIdentity(b, label + " settings", prediction?.settings);
        }

        private static void AppendIdentity(StringBuilder b, string label, UnityEngine.Object value)
        {
            b.Append(label).Append(": ").AppendLine(Identity(value));
        }

        private static string Identity(UnityEngine.Object value)
        {
            if (value == null) return "missing";
            return $"{value.name} | type={value.GetType().FullName} | instance={value.GetInstanceID()} | ptr=0x{value.Pointer.ToInt64():X}";
        }

        private static string ShortIdentity(UnityEngine.Object value)
        {
            if (value == null) return "missing";
            return $"{value.GetInstanceID()}/0x{value.Pointer.ToInt64():X}";
        }

        private static bool Same(UnityEngine.Object a, UnityEngine.Object b) =>
            a != null && b != null && a.Pointer == b.Pointer;

        private static string YesNoUnknown(UnityEngine.Object a, UnityEngine.Object b, bool same)
        {
            if (a == null || b == null) return "unknown";
            return same ? "yes" : "no";
        }

        private static int CountUniquePointers(params UnityEngine.Object[] values)
        {
            var pointers = new HashSet<long>();
            foreach (UnityEngine.Object value in values)
            {
                if (value != null)
                    pointers.Add(value.Pointer.ToInt64());
            }
            return pointers.Count;
        }

        private static bool SafeGrounded(Il2CppMashBox.BMX_Physics_Development.VehicleController vehicle)
        {
            try { return vehicle.Grounded(); }
            catch { return vehicle.TimeInAir <= AirborneThreshold; }
        }

        private static string F(float value) =>
            float.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;

        private static string B(bool value) => value ? "1" : "0";

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return '"' + value.Replace("\"", "\"\"") + '"';
        }
    }
}
