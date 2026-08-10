using System;
using rowemod.Utils;
using UnityEngine;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using BMXBalanaceBehaviour = Il2CppMashBox.BMX_Physics_Development.BMXBalanaceBehaviour;
using VehicleController = Il2CppMashBox.BMX_Physics_Development.VehicleController;

namespace rowemod.Mods
{
    public static class Physics
    {
        private static VehicleController _noseTuningController;
        private static BMXBalanaceBehaviour _noseTuningBalance;
        private static MotorVehicleSettings _noseTuningVehicleSettings;
        private static bool _noseTuningBaselineCaptured;
        private static bool _noseTuningApplied;
        private static Vector3 _originalChassisComMannyInput;
        private static Vector3 _originalDriverComMannyInput;
        private static Vector3 _originalDriverInertiaNoseyInput;
        private static Vector3 _originalDesiredChassisCom;
        private static Vector3 _originalDesiredDriverCom;
        private static VehicleController _spinCompletionController;
        private static bool _spinCompletionNativeStateCaptured;
        private static bool _spinCompletionOriginalHoldAssistOff;
        private static float _spinCompletionInput;
        private static float _spinCompletionDirection;
        private static bool _spinCompletionFailureLogged;

        private const float SpinCompletionMinimumFlightTime = 0.08f;
        private const float SpinCompletionMaximumFlightTime = 2.5f;
        private const float SpinCompletionLandingWindow = 1.5f;
        private const float SpinCompletionFullInputRate = 270f;
        private const float SpinCompletionInputResponse = 5f;

        public static void Update()
        {
            UnityEngine.Physics.gravity = new Vector3(0f, -physics.gravity, 0f);

            if (!physics.spinCompletionAssist)
                ReleaseSpinCompletionAssist();
            
            if (rMbCharacter != null)
            {
                if (spinSystem != null)
                {
                    spinSystem._torqueMult = physics.spinMultiplier;
                    if (airControlSettings != null)
                    {
                        //airControlSettings._airAngularDrag = physics.airAngularDrag;
                    }
                    else
                    {
                        Log.Warning("air control settings component not found!");
                    }
                }
                else
                {
                    Log.Warning("spinSystem component is not initialized.");
                }
                
                if (driftAbility != null)
                {
                    driftAbility.enabled = physics.driftAbility;
                }
                else
                {
                    Log.Warning("DriftAbility component is not initialized.");
                }

                if (vehicleController != null)
                {
                    vehicleController._airSpinAssist = physics.spinAssist;
                    vehicleController._steerDampRate = physics.steerDamp;
                    vehicleController._mannyAngle = physics.manualAngle;
                    vehicleController._noseyAngle = physics.noseManualAngle;
                }
                else
                {
                    Log.Warning("VehicleController component is not initialized.");
                }

                if (pumpSystem != null)
                {
                    pumpSystem._pumpForce = physics.pumpForce;
                }
                else
                {
                    Log.Warning("PumpSystem component is not initialized.");
                }

                if (grindMagnetZEM != null)
                {
                    grindMagnetZEM._alignAssist = physics.grindAlignAssist;
                    grindMagnetZEM._forceMult = physics.grindAssistStrength;
                }

            }

            if (vehicleSettingsInstances == null)
                return;

            foreach (MotorVehicleSettings vehicleInstance in vehicleSettingsInstances)
            {
                if (vehicleInstance == null)
                    continue;

                try
                {
                    if (vehicleInstance.JumpSettings != null)
                    {
                        vehicleInstance.JumpSettings.JumpVelocity[0] = physics.smallHopForce;
                        vehicleInstance.JumpSettings.JumpVelocity[1] = physics.smallHopForce;
                    }

                    if (vehicleInstance.EngineSettings == null)
                        continue;

                    string tuneKey = string.IsNullOrEmpty(vehicleInstance.name) ? "MotorVehicleSettings" : vehicleInstance.name;
                    if (motorTuning != null && motorTuning.TryGetValue(tuneKey, out MotorTuningConfigEntry tune) && tune != null)
                    {
                        MigrateMotorTuningEnabledFlag(
                            tune,
                            vehicleInstance.EngineSettings._forceFactor,
                            vehicleInstance.EngineSettings._maxForce,
                            vehicleInstance.EngineSettings._maxSpeed);
                        if (tune.enabled)
                        {
                            vehicleInstance.EngineSettings._forceFactor = tune.forceFactor;
                            vehicleInstance.EngineSettings._maxForce = tune.maxForce;
                            vehicleInstance.EngineSettings._maxSpeed = tune.maxSpeed;
                        }
                        else
                        {
                            vehicleInstance.EngineSettings._forceFactor = physics.bmxForceFactor;
                            vehicleInstance.EngineSettings._maxSpeed = physics.bmxMaxSpeed;
                        }
                    }
                    else
                    {
                        vehicleInstance.EngineSettings._forceFactor = physics.bmxForceFactor;
                        vehicleInstance.EngineSettings._maxSpeed = physics.bmxMaxSpeed;
                    }
                    //vehicleInstance.JumpSettings.JumpVelocity[2] = hopForce;
                }
                catch (Exception ex)
                {
                    Log.Error($"[Physics.Update] Error updating vehicle settings: {ex.Message}\n{ex.StackTrace}");
                }
            }

        }

        public static void UpdateSpinCompletionAssist()
        {
            if (!physics.spinCompletionAssist)
            {
                ReleaseSpinCompletionAssist();
                return;
            }

            VehicleController controller = vehicleController;
            var mashSpinSystem = spinSystem;
            if (controller == null || mashSpinSystem == null)
            {
                ReleaseSpinCompletionAssist();
                return;
            }

            if (_spinCompletionController != controller)
            {
                ReleaseSpinCompletionAssist();
                _spinCompletionController = controller;
            }

            try
            {
                if (controller.Grounded())
                {
                    RestoreSpinCompletionNativeState();
                    _spinCompletionInput = 0f;
                    _spinCompletionDirection = 0f;
                    return;
                }

                float timeLeft = controller.TimeLeftInAir();
                if (!IsFinite(timeLeft) || timeLeft <= 0f || timeLeft > SpinCompletionMaximumFlightTime)
                {
                    RestoreSpinCompletionNativeState();
                    _spinCompletionInput = 0f;
                    return;
                }

                Vector3 landingUp = controller.BestLandingUp;
                if (landingUp.sqrMagnitude < 0.0001f)
                    landingUp = Vector3.up;
                else
                    landingUp.Normalize();

                float predictedAngleError = controller.PredictedSignedAngleToLandingForward();
                if (!IsFinite(predictedAngleError))
                {
                    RestoreSpinCompletionNativeState();
                    _spinCompletionInput = 0f;
                    _spinCompletionDirection = 0f;
                    return;
                }

                // The game's signed landing error describes the shortest route to forward.
                // Latch the rider's actual world-space yaw direction and unwrap that error so
                // the assist can only continue the current spin, never reverse for a shortcut.
                float signedYawRate = Vector3.Dot(controller.HumanAngularVelocity, landingUp);
                if (Mathf.Abs(signedYawRate) > 0.12f)
                    _spinCompletionDirection = Mathf.Sign(signedYawRate);

                float manualInput = Mathf.Clamp(controller._spinInput, -1f, 1f);
                if (Mathf.Approximately(_spinCompletionDirection, 0f) && Mathf.Abs(manualInput) > 0.12f)
                    _spinCompletionDirection = Mathf.Sign(manualInput);

                if (_spinCompletionDirection > 0f && predictedAngleError < 0f)
                    predictedAngleError += 360f;
                else if (_spinCompletionDirection < 0f && predictedAngleError > 0f)
                    predictedAngleError -= 360f;

                CaptureSpinCompletionNativeState(controller);
                controller._holdSpinAssistOff = true;

                float safeTimeLeft = Mathf.Max(SpinCompletionMinimumFlightTime, timeLeft);
                float requiredRateCorrection = predictedAngleError / safeTimeLeft;
                float requestedInput = Mathf.Clamp(
                    requiredRateCorrection / SpinCompletionFullInputRate,
                    -1f,
                    1f);

                float landingProgress = 1f - Mathf.Clamp01(
                    (timeLeft - SpinCompletionMinimumFlightTime) /
                    (SpinCompletionLandingWindow - SpinCompletionMinimumFlightTime));
                landingProgress = Mathf.SmoothStep(0f, 1f, landingProgress);

                bool descending = Vector3.Dot(controller.Velocity, landingUp) <= 0f;
                if (!descending)
                    landingProgress *= 0.25f;

                if (Mathf.Abs(manualInput) > 0.12f && timeLeft > 0.45f)
                    landingProgress *= 0.45f;

                float responseStep = SpinCompletionInputResponse * Time.fixedDeltaTime;
                _spinCompletionInput = Mathf.MoveTowards(
                    _spinCompletionInput,
                    requestedInput,
                    responseStep);

                float completedInput = Mathf.Lerp(
                    manualInput,
                    _spinCompletionInput,
                    landingProgress);
                completedInput = Mathf.Clamp(completedInput, -1f, 1f);

                if (_spinCompletionDirection > 0f)
                    completedInput = Mathf.Max(0f, completedInput);
                else if (_spinCompletionDirection < 0f)
                    completedInput = Mathf.Min(0f, completedInput);

                controller._spinInput = completedInput;
                mashSpinSystem.SetInput(completedInput, controller._flipInput);
                _spinCompletionFailureLogged = false;
            }
            catch (Exception ex)
            {
                RestoreSpinCompletionNativeState();
                _spinCompletionInput = 0f;
                if (!_spinCompletionFailureLogged)
                {
                    _spinCompletionFailureLogged = true;
                    Log.Warning($"[Physics][SpinCompletion] Disabled for the current state: {ex.Message}");
                }
            }
        }

        public static void ReleaseSpinCompletionAssist()
        {
            RestoreSpinCompletionNativeState();
            OnePointOhLandingAssist.Reset();
            _spinCompletionController = null;
            _spinCompletionInput = 0f;
            _spinCompletionDirection = 0f;
            _spinCompletionFailureLogged = false;
        }

        private static void CaptureSpinCompletionNativeState(VehicleController controller)
        {
            if (_spinCompletionNativeStateCaptured || controller == null)
                return;

            _spinCompletionController = controller;
            _spinCompletionOriginalHoldAssistOff = controller._holdSpinAssistOff;
            _spinCompletionNativeStateCaptured = true;
        }

        private static void RestoreSpinCompletionNativeState()
        {
            try
            {
                if (_spinCompletionNativeStateCaptured && _spinCompletionController != null)
                    _spinCompletionController._holdSpinAssistOff = _spinCompletionOriginalHoldAssistOff;
            }
            catch
            {
                // The cached IL2CPP controller may already have been destroyed during a respawn.
            }

            _spinCompletionNativeStateCaptured = false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static void UpdateNoseManualTuning()
        {
            try
            {
                if (!physics.noseManualTurnTuning)
                {
                    ReleaseNoseManualTuning();
                    return;
                }

                var controller = vehicleController;
                BMXBalanaceBehaviour balance = controller?._balanaceBehaviour;
                MotorVehicleSettings vehicleSettings = controller?._motorVehicleSettings;

                if (controller == null || balance == null || vehicleSettings == null)
                {
                    ReleaseNoseManualTuning();
                    return;
                }

                if (_noseTuningController != controller ||
                    _noseTuningBalance != balance ||
                    _noseTuningVehicleSettings != vehicleSettings)
                {
                    ReleaseNoseManualTuning();
                    _noseTuningController = controller;
                    _noseTuningBalance = balance;
                    _noseTuningVehicleSettings = vehicleSettings;
                }

                bool collisionNosey =
                    controller._bmxCollisionHandler != null &&
                    controller._bmxCollisionHandler.Nosey();
                bool thresholdNosePivot =
                    balance._noseyPivotThresholdMet &&
                    Mathf.Abs(controller._noseyInput) > 0.01f;
                bool physicalNosePivot =
                    controller.IsNosey ||
                    collisionNosey ||
                    balance._noseyPivoting ||
                    thresholdNosePivot;
                if (!physicalNosePivot)
                {
                    RestoreNoseManualTuningValues();
                    return;
                }

                if (!_noseTuningBaselineCaptured)
                    CaptureNoseManualTuningBaseline();

                bool wasApplied = _noseTuningApplied;
                _noseTuningApplied = true;

                float turnLean = Mathf.Clamp(physics.noseManualComTurnLean, -0.5f, 0.5f);
                float steerInput = Mathf.Clamp(controller._steerInput, -1f, 1f);
                Vector3 chassisCom = _originalChassisComMannyInput + new Vector3(
                    0f,
                    Mathf.Clamp(physics.noseManualChassisComVerticalOffset, -1f, 1f),
                    Mathf.Clamp(physics.noseManualChassisComForwardOffset, -1f, 1f));
                Vector3 driverCom = _originalDriverComMannyInput + new Vector3(
                    steerInput * turnLean,
                    Mathf.Clamp(physics.noseManualDriverComVerticalOffset, -1f, 1f),
                    Mathf.Clamp(physics.noseManualDriverComForwardOffset, -1f, 1f));
                float inertiaMultiplier = Mathf.Clamp(physics.noseManualDriverInertiaMultiplier, 0.25f, 3f);

                vehicleSettings.ChassisCenterOfMass_MannyInput = chassisCom;
                vehicleSettings.DriverCenterOfMass_MannyInput = driverCom;
                vehicleSettings.DriverBodyInetiaTensor_NoseyInput = _originalDriverInertiaNoseyInput * inertiaMultiplier;
                controller._desiredChassisCOM = chassisCom;
                controller._desiredDriverCOM = driverCom;

                if (!wasApplied && physics.noseManualDebugLogging)
                {
                    Log.Msg(
                        "[Physics][Nosey] COM tuning active: " +
                        $"chassisCOM={FormatVector(chassisCom)}, driverCOM={FormatVector(driverCom)}, " +
                        $"turnLean={turnLean:0.000}, inertia={inertiaMultiplier:0.00}x.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Physics][Nosey] Failed to apply turn tuning: {ex.Message}");
                ReleaseNoseManualTuning();
            }
        }

        public static void ReleaseNoseManualTuning()
        {
            RestoreNoseManualTuningValues();
            _noseTuningController = null;
            _noseTuningBalance = null;
            _noseTuningVehicleSettings = null;
            _noseTuningBaselineCaptured = false;
        }

        private static void CaptureNoseManualTuningBaseline()
        {
            if (_noseTuningBalance == null || _noseTuningVehicleSettings == null)
                return;

            _originalChassisComMannyInput = _noseTuningVehicleSettings.ChassisCenterOfMass_MannyInput;
            _originalDriverComMannyInput = _noseTuningVehicleSettings.DriverCenterOfMass_MannyInput;
            _originalDriverInertiaNoseyInput = _noseTuningVehicleSettings.DriverBodyInetiaTensor_NoseyInput;
            _originalDesiredChassisCom = _noseTuningController?._desiredChassisCOM ?? _originalChassisComMannyInput;
            _originalDesiredDriverCom = _noseTuningController?._desiredDriverCOM ?? _originalDriverComMannyInput;
            _noseTuningBaselineCaptured = true;

            if (physics.noseManualDebugLogging)
            {
                Log.Msg(
                    "[Physics][Nosey] Baseline: " +
                    $"chassisCOM={FormatVector(_originalChassisComMannyInput)}, " +
                    $"driverCOM={FormatVector(_originalDriverComMannyInput)}, " +
                    $"driverInertia={FormatVector(_originalDriverInertiaNoseyInput)}.");
            }
        }

        private static void RestoreNoseManualTuningValues()
        {
            if (!_noseTuningBaselineCaptured)
                return;

            try
            {
                if (_noseTuningVehicleSettings != null)
                {
                    _noseTuningVehicleSettings.ChassisCenterOfMass_MannyInput = _originalChassisComMannyInput;
                    _noseTuningVehicleSettings.DriverCenterOfMass_MannyInput = _originalDriverComMannyInput;
                    _noseTuningVehicleSettings.DriverBodyInetiaTensor_NoseyInput = _originalDriverInertiaNoseyInput;
                }

                if (_noseTuningController != null)
                {
                    _noseTuningController._desiredChassisCOM = _originalDesiredChassisCom;
                    _noseTuningController._desiredDriverCOM = _originalDesiredDriverCom;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[Physics][Nosey] Failed to restore original tuning: {ex.Message}");
            }

            if (_noseTuningApplied && physics.noseManualDebugLogging)
                Log.Msg("[Physics][Nosey] Turn tuning restored.");

            _noseTuningApplied = false;
            _noseTuningBaselineCaptured = false;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.000},{value.y:0.000},{value.z:0.000})";
        }

        private static void MigrateMotorTuningEnabledFlag(
            MotorTuningConfigEntry tune,
            float defaultForceFactor,
            float defaultMaxForce,
            float defaultMaxSpeed)
        {
            if (tune == null || tune.enabledMigrated)
                return;

            tune.enabled =
                !Mathf.Approximately(tune.forceFactor, defaultForceFactor) ||
                !Mathf.Approximately(tune.maxForce, defaultMaxForce) ||
                !Mathf.Approximately(tune.maxSpeed, defaultMaxSpeed);
            tune.enabledMigrated = true;
        }
    }
}
