using rowemod.Utils;
using UnityEngine;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;

namespace rowemod.Mods
{
    public static class Physics
    {
        public static void Update()
        {
            UnityEngine.Physics.gravity = new Vector3(0f, -physics.gravity, 0f);
            
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

                foreach (var peg in pegColliders)
                {
                    if (peg != null && peg.sharedMaterial != null)
                    {
                        peg.sharedMaterial.staticFriction = physics.grindFriction;
                        peg.sharedMaterial.dynamicFriction = physics.grindFriction;
                    }
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
                        vehicleInstance.EngineSettings._forceFactor = tune.forceFactor;
                        vehicleInstance.EngineSettings._maxForce = tune.maxForce;
                        vehicleInstance.EngineSettings._maxSpeed = tune.maxSpeed;
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
    }
}
