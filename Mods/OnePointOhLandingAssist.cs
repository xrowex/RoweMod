using Il2CppMashBox.BMX_Physics_Development;
using UnityEngine;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using Log = rowemod.Utils.Log;

namespace rowemod.Mods
{
    /// <summary>
    /// Keeps One Point Oh's native landing drive from taking the shorter route
    /// backwards around the rider's yaw axis. The game's drive still supplies all
    /// torque; this patch only changes its target to a direction-safe waypoint.
    /// </summary>
    internal static class OnePointOhLandingAssist
    {
        private const float DirectionCaptureRate = 0.04f;
        private const float MinimumAirProgress = 0.18f;
        private const float MaximumWaypointAngle = 110f;
        private const float MinimumCorrectionAngle = 0.5f;
        private const float ScheduledSpinBoostStart = 0.55f;
        private const float ScheduledSpinBoostRemainingScale = 0.58f;
        private const float MinimumScheduledSpinRemaining = 0.08f;
        private const float MinimumHeadingErrorForBoost = 20f;
        private const float SkipLogCooldownSeconds = 1.0f;
        private const float MinDirectionHint = 0.01f;
        private const float SpinAssistDebugInterval = 1f;
        private const float MinimumDirectionDeltaDegrees = 1.2f;

        private static System.IntPtr _systemPointer;
        private static float _spinDirection;
        private static bool _interventionLogged;
        private static bool _failureLogged;
        private static float _observedScheduledSpinStartTime = float.NaN;
        private static bool _scheduledSpinBoostApplied;
        private static string _lastSkipReason;
        private static float _nextSkipLogAt = 0f;
        private static bool _wasGrounded = true;
        private static float _nextAssistDebugLogAt;
        private static Vector3 _lastAirForward;
        private static bool _hasLastAirForward;

        internal static void Reset()
        {
            _systemPointer = System.IntPtr.Zero;
            _spinDirection = 0f;
            _interventionLogged = false;
            _failureLogged = false;
            _observedScheduledSpinStartTime = float.NaN;
            _scheduledSpinBoostApplied = false;
            _lastSkipReason = null;
            _nextSkipLogAt = 0f;
            _wasGrounded = true;
            _nextAssistDebugLogAt = 0f;
            _hasLastAirForward = false;
            _lastAirForward = default;
        }

        internal static void Apply(
            QuaternionPDDrive drive,
            Rigidbody body,
            ref Quaternion targetRotation)
        {
            if (!physics.spinCompletionAssist ||
                !RemoteKillSwitched.isModEnabled ||
                !Main.playableSceneLoaded ||
                drive == null ||
                body == null)
            {
                // Native Tick also runs for unrelated drives. Disabled assistance must
                // remain a cheap no-op, not format a diagnostic on every physics call.
                return;
            }

            VehicleController controller = vehicleController;
            if (controller == null || !VehicleController.UseOnePointOhSpinSystem)
            {
                LogSkip("[OnePointOhLanding] Skipped: vehicle controller missing or One Point Oh unavailable.");
                return;
            }

            OnePointOhFlipSystem system = controller._onePointOhFlipSystem;
            if (system == null || system._drive == null || system._driverBody == null)
            {
                LogSkip("[OnePointOhLanding] Skipped: One Point Oh runtime not fully initialized.");
                return;
            }

            if (drive.Pointer != system._drive.Pointer || body.Pointer != system._driverBody.Pointer)
            {
                return;
            }

            if (_systemPointer != system.Pointer)
            {
                Reset();
                _systemPointer = system.Pointer;
            }

            try
            {
                if (controller.Grounded())
                {
                    _spinDirection = 0f;
                    _interventionLogged = false;
                    _wasGrounded = true;
                    return;
                }

                if (_wasGrounded && !controller.Grounded())
                {
                    _wasGrounded = false;
                    Log.Msg("[OnePointOhLanding] Airborne and tracking landing correction.");
                    _lastSkipReason = null;
                    _hasLastAirForward = false;
                }

                if (Time.timeScale <= 0f)
                {
                    LogSkip("[OnePointOhLanding] Skipped: game is paused (timeScale <= 0).");
                    return;
                }

                Vector3 landingUp = controller.BestLandingUp;
                if (landingUp.sqrMagnitude < 0.0001f)
                    landingUp = Vector3.up;
                else
                    landingUp.Normalize();

                float signedYawRate = Vector3.Dot(controller.HumanAngularVelocity, landingUp);
                if (Mathf.Abs(signedYawRate) > DirectionCaptureRate)
                    _spinDirection = Mathf.Sign(signedYawRate);

                OnePointOhFlipSystem.TrickState spin = system._spinTrick;

                Vector3 currentForward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, landingUp);
                if (currentForward.sqrMagnitude < 0.0001f)
                {
                    LogSkip("[OnePointOhLanding] Skipped this frame: invalid projected body forward.");
                    return;
                }

                currentForward.Normalize();
                if (_hasLastAirForward && _lastAirForward.sqrMagnitude > 0.0001f)
                {
                    float forwardDelta = Vector3.SignedAngle(_lastAirForward, currentForward, landingUp);
                    if (Mathf.Abs(forwardDelta) > MinimumDirectionDeltaDegrees && _spinDirection == 0f)
                        _spinDirection = Mathf.Sign(forwardDelta);
                }
                _lastAirForward = currentForward;
                _hasLastAirForward = true;

                _spinDirection = ResolveSpinDirection(_spinDirection, spin);
                if (Mathf.Approximately(_spinDirection, 0f))
                {
                    LogSkip("[OnePointOhLanding] Skipped this frame: no stable spin direction captured yet.");
                    return;
                }

                // A planned One Point Oh 360 is a native time-based transform sequence.
                // Retiming its remaining segment once gives the existing PD drive a real
                // chance to finish an under-rotated spin without adding another torque loop.
                TryAccelerateScheduledSpinFinish(system, controller, landingUp, _spinDirection);

                if (controller.PercentThroughAir < MinimumAirProgress)
                {
                    LogSkip("[OnePointOhLanding] Skipped this frame: not enough air time yet.");
                    return;
                }

                Vector3 landingForward = Vector3.ProjectOnPlane(controller.BestLandingForward, landingUp);
                Vector3 nativeForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, landingUp);
                if (currentForward.sqrMagnitude < 0.0001f ||
                    landingForward.sqrMagnitude < 0.0001f ||
                    nativeForward.sqrMagnitude < 0.0001f)
                {
                    LogSkip("[OnePointOhLanding] Skipped this frame: invalid forward vectors from body/landing vectors.");
                    return;
                }

                landingForward.Normalize();
                nativeForward.Normalize();

                float shortestLandingError = Vector3.SignedAngle(
                    currentForward,
                    landingForward,
                    landingUp);

                // Unwrap the shortest signed error into the rider's current direction.
                // A -40 degree shortcut during a positive spin becomes +320 degrees.
                float directionalLandingError = shortestLandingError;
                if (_spinDirection > 0f && directionalLandingError < 0f)
                    directionalLandingError += 360f;
                else if (_spinDirection < 0f && directionalLandingError > 0f)
                    directionalLandingError -= 360f;
                if (Mathf.Approximately(directionalLandingError, 0f))
                    directionalLandingError = shortestLandingError;

                if (Mathf.Abs(directionalLandingError) < MinimumCorrectionAngle)
                {
                    LogSkip("[OnePointOhLanding] Skipped this frame: correction is within minimum threshold.");
                    return;
                }

                float waypointAngle = Mathf.Sign(directionalLandingError) *
                                      Mathf.Min(Mathf.Abs(directionalLandingError), MaximumWaypointAngle);
                Vector3 waypointForward = Quaternion.AngleAxis(waypointAngle, landingUp) * currentForward;

                // Rotate the native target only around the landing-up axis. Its flip,
                // lean, gains, damping, and torque limits remain owned by One Point Oh.
                float nativeYawAdjustment = Vector3.SignedAngle(
                    nativeForward,
                    waypointForward,
                    landingUp);
                targetRotation = Quaternion.AngleAxis(nativeYawAdjustment, landingUp) * targetRotation;

                if (Time.unscaledTime >= _nextAssistDebugLogAt)
                    LogAssistStatus(
                    $"[OnePointOhLanding] Applied correction: " +
                    $"dir={(_spinDirection > 0f ? '+' : '-')}, " +
                    $"air={controller.PercentThroughAir:0.00}, " +
                    $"shortest={shortestLandingError:0.00}, " +
                    $"directional={directionalLandingError:0.00}, " +
                    $"yawAdj={nativeYawAdjustment:0.00}, " +
                    $"spinActive={spin.active}, " +
                    $"spinDuration={(spin.duration <= 0f ? 0f : spin.duration):0.00}, " +
                    $"spinDirHint={spin.dir:0.00}");

                bool nativeWouldReverse = Mathf.Abs(shortestLandingError) > MinimumCorrectionAngle &&
                                          Mathf.Sign(shortestLandingError) != _spinDirection;
                if (nativeWouldReverse && !_interventionLogged)
                {
                    _interventionLogged = true;
                    string direction = _spinDirection > 0f ? "positive" : "negative";
                    Log.Msg($"[OnePointOhLanding] Preserving {direction} spin direction; blocked the nearest-path reversal.");
                }

                _failureLogged = false;
                _lastSkipReason = null;
            }
            catch (System.Exception ex)
            {
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    Log.Warning($"[OnePointOhLanding] Target guidance skipped: {ex.Message}");
                }
            }
        }

        private static void LogSkip(string message)
        {
            if (Time.unscaledTime < _nextSkipLogAt)
                return;

            if (_lastSkipReason == message)
                return;

            _lastSkipReason = message;
            _nextSkipLogAt = Time.unscaledTime + SkipLogCooldownSeconds;
            Log.Msg(message);
        }

        private static void LogAssistStatus(string message)
        {
            if (Time.unscaledTime < _nextAssistDebugLogAt)
                return;

            _nextAssistDebugLogAt = Time.unscaledTime + SpinAssistDebugInterval;
            Log.Msg(message);
        }

        private static float ResolveSpinDirection(float detectedDirection, OnePointOhFlipSystem.TrickState spin)
        {
            if (!Mathf.Approximately(detectedDirection, 0f))
                return detectedDirection;

            if (Mathf.Abs(spin.dir) > MinDirectionHint)
                return Mathf.Sign(spin.dir);

            return 0f;
        }

        private static void TryAccelerateScheduledSpinFinish(
            OnePointOhFlipSystem system,
            VehicleController controller,
            Vector3 landingUp,
            float spinDirection)
        {
            OnePointOhFlipSystem.TrickState spin = system._spinTrick;
            if (!spin.active)
            {
                _observedScheduledSpinStartTime = float.NaN;
                _scheduledSpinBoostApplied = false;
                return;
            }

            if (float.IsNaN(_observedScheduledSpinStartTime) ||
                !Mathf.Approximately(_observedScheduledSpinStartTime, spin.startTime))
            {
                _observedScheduledSpinStartTime = spin.startTime;
                _scheduledSpinBoostApplied = false;
            }

            if (_scheduledSpinBoostApplied ||
                spin.duration <= MinimumScheduledSpinRemaining ||
                controller.PercentThroughAir < MinimumAirProgress)
            {
                return;
            }

            float direction = ResolveSpinDirection(spinDirection, spin);
            if (Mathf.Approximately(direction, 0f))
                return;

            float now = Time.time;
            float progress = Mathf.Clamp01((now - spin.startTime) / spin.duration);
            if (progress < ScheduledSpinBoostStart)
                return;

            float timeLeft = controller.TimeLeftInAir();
            if (!IsFinite(timeLeft) || timeLeft <= MinimumScheduledSpinRemaining)
                return;

            Vector3 currentForward = Vector3.ProjectOnPlane(
                system._driverBody.rotation * Vector3.forward,
                landingUp);
            Vector3 landingForward = Vector3.ProjectOnPlane(controller.BestLandingForward, landingUp);
            if (currentForward.sqrMagnitude < 0.0001f || landingForward.sqrMagnitude < 0.0001f)
                return;

            currentForward.Normalize();
            landingForward.Normalize();
            float remainingHeading = Vector3.SignedAngle(currentForward, landingForward, landingUp);
            if (direction > 0f && remainingHeading < 0f)
                remainingHeading += 360f;
            else if (direction < 0f && remainingHeading > 0f)
                remainingHeading -= 360f;

            if (Mathf.Abs(remainingHeading) < MinimumHeadingErrorForBoost)
                return;

            float remainingDuration = Mathf.Max(0f, (1f - progress) * spin.duration);
            if (remainingDuration <= MinimumScheduledSpinRemaining)
                return;

            // Preserve the current native target position, then shorten only its remaining
            // segment. One Point Oh continues to own the easing curve and the final drive.
            float newRemainingDuration = Mathf.Max(
                MinimumScheduledSpinRemaining,
                remainingDuration * ScheduledSpinBoostRemainingScale);
            float newDuration = (progress * spin.duration) + newRemainingDuration;
            spin.duration = newDuration;
            spin.startTime = now - (progress * newDuration);
            system._spinTrick = spin;

            _observedScheduledSpinStartTime = spin.startTime;
            _scheduledSpinBoostApplied = true;
            Log.Msg($"[OnePointOhLanding] Accelerated the remaining native 360 finish ({remainingHeading:0} degrees left).");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal static class OnePointOhQuaternionDrivePatch
    {
        private static void Prefix(
            QuaternionPDDrive __instance,
            Rigidbody __0,
            ref Quaternion __1)
        {
            OnePointOhLandingAssist.Apply(__instance, __0, ref __1);
        }
    }
}
