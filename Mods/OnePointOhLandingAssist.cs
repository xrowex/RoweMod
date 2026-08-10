using HarmonyLib;
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
        private const float DirectionCaptureRate = 0.12f;
        private const float MinimumAirProgress = 0.30f;
        private const float MaximumWaypointAngle = 110f;
        private const float MinimumCorrectionAngle = 0.5f;
        private const float ScheduledSpinBoostStart = 0.55f;
        private const float ScheduledSpinBoostRemainingScale = 0.58f;
        private const float MinimumScheduledSpinRemaining = 0.08f;
        private const float MinimumHeadingErrorForBoost = 20f;

        private static System.IntPtr _systemPointer;
        private static float _spinDirection;
        private static bool _interventionLogged;
        private static bool _failureLogged;
        private static float _observedScheduledSpinStartTime = float.NaN;
        private static bool _scheduledSpinBoostApplied;

        internal static void Reset()
        {
            _systemPointer = System.IntPtr.Zero;
            _spinDirection = 0f;
            _interventionLogged = false;
            _failureLogged = false;
            _observedScheduledSpinStartTime = float.NaN;
            _scheduledSpinBoostApplied = false;
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
                return;
            }

            VehicleController controller = vehicleController;
            if (controller == null || !VehicleController.UseOnePointOhSpinSystem)
                return;

            OnePointOhFlipSystem system = controller._onePointOhFlipSystem;
            if (system == null || system._drive == null || system._driverBody == null)
                return;

            if (drive.Pointer != system._drive.Pointer || body.Pointer != system._driverBody.Pointer)
                return;

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

                // A planned One Point Oh 360 is a native time-based transform sequence.
                // Retiming its remaining segment once gives the existing PD drive a real
                // chance to finish an under-rotated spin without adding another torque loop.
                TryAccelerateScheduledSpinFinish(system, controller, landingUp);

                if (Mathf.Approximately(_spinDirection, 0f) ||
                    controller.PercentThroughAir < MinimumAirProgress)
                {
                    return;
                }

                Vector3 currentForward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, landingUp);
                Vector3 landingForward = Vector3.ProjectOnPlane(controller.BestLandingForward, landingUp);
                Vector3 nativeForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, landingUp);
                if (currentForward.sqrMagnitude < 0.0001f ||
                    landingForward.sqrMagnitude < 0.0001f ||
                    nativeForward.sqrMagnitude < 0.0001f)
                {
                    return;
                }

                currentForward.Normalize();
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

                if (Mathf.Abs(directionalLandingError) < MinimumCorrectionAngle)
                    return;

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

                bool nativeWouldReverse = Mathf.Abs(shortestLandingError) > MinimumCorrectionAngle &&
                                          Mathf.Sign(shortestLandingError) != _spinDirection;
                if (nativeWouldReverse && !_interventionLogged)
                {
                    _interventionLogged = true;
                    string direction = _spinDirection > 0f ? "positive" : "negative";
                    Log.Msg($"[OnePointOhLanding] Preserving {direction} spin direction; blocked the nearest-path reversal.");
                }

                _failureLogged = false;
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

        private static void TryAccelerateScheduledSpinFinish(
            OnePointOhFlipSystem system,
            VehicleController controller,
            Vector3 landingUp)
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

            float direction = _spinDirection;
            if (Mathf.Approximately(direction, 0f) && Mathf.Abs(spin.dir) > 0.01f)
                direction = Mathf.Sign(spin.dir);
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

    [HarmonyPatch(typeof(QuaternionPDDrive), nameof(QuaternionPDDrive.Tick),
        new[] { typeof(Rigidbody), typeof(Quaternion) })]
    internal static class OnePointOhQuaternionDrivePatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            QuaternionPDDrive __instance,
            Rigidbody rb,
            ref Quaternion targetRotation)
        {
            OnePointOhLandingAssist.Apply(__instance, rb, ref targetRotation);
        }
    }
}
