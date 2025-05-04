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
            UnityEngine.Physics.gravity = new Vector3(0f, -gravity, 0f);
        
            if (rMbCharacter != null)
            {
                if (spinSystem != null)
                {
                    spinSystem._torqueMult = spinTorque;
                }
                else
                {
                    Log.Warning("spinSystem component is not initialized.");
                }
                
                if (driftAbility != null)
                {
                    driftAbility.enabled = bDriftAbility;
                }
                else
                {
                    Log.Warning("DriftAbility component is not initialized.");
                }

                if (vehicleController != null)
                {
                    vehicleController._airSpinAssist = bSpinAssist;
                    vehicleController._steerDampRate = steerDamp;
                    vehicleController._mannyAngle = manualAngle;
                    vehicleController._noseyAngle = noseManualAngle;
                }
                else
                {
                    Log.Warning("VehicleController component is not initialized.");
                }

                if (pumpSystem != null)
                {
                    pumpSystem._pumpForce = pumpForce;
                }
                else
                {
                    Log.Warning("PumpSystem component is not initialized.");
                }

                
            }

            foreach (MotorVehicleSettings vehicleInstance in vehicleSettingsInstances)
            {
                try
                {
                    vehicleInstance.JumpSettings.JumpVelocity[0] = smallHopForce;
                    vehicleInstance.JumpSettings.JumpVelocity[1] = smallHopForce;
                    //vehicleInstance.JumpSettings.JumpVelocity[2] = hopForce;
                }
                catch (Exception ex)
                {
                    Log.Error($"[Physics.Update] Error updating vehicle settings: {ex.Message}\n{ex.StackTrace}");
                }
            }

            /*if (theCharacter != null)
            {
                try
                {
                    theCharacter._allowTrickLanders = allowTrickLanders;
                    theCharacter._alwaysAllowFireTricks = alwaysAllowFireTricks;
                }
                catch (Exception ex)
                {
                    Log.Error($"[Physics.Update] Error updating character settings: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                Log.Error("theCharacter is null.");
            }*/
        }
    }
}
