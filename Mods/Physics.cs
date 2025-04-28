using rowemod.Utils;
using UnityEngine;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using Il2CppMashBox.Core.Runtime.Physics.Vehicle;

namespace rowemod.Mods
{
    public static class Physics
    {
        public static void FixedUpdate()
        {
            try
            {
                if (rMbCharacter != null)
                {

                    float originalValue = spinSystem.quickSpinEffectivness;
                    float multiplier = quickSpinMultiplier;
                    float adjustedValue = originalValue + multiplier;

                    //set quickSpinEffectiveness to adjustedValue
                    spinSystem.quickSpinEffectivness = Mathf.Clamp(adjustedValue, 1, 1 * multiplier);

                    /*
                    if (bSpinFlipFix)
                    {
                        //
                        //  SQUARE SCALING INPUTS
                        //
                        // Read your raw inputs
                        float rawX = vehicleController._steerInputRaw.x;  // For spin
                        float rawY = vehicleController._flipInputRaw.y;   // For flip

                        // Compute the axis with the larger absolute value
                        float maxVal = Mathf.Max(Mathf.Abs(rawX), Mathf.Abs(rawY));

                        // If maxVal is 0, the stick is centered; otherwise, rescale so the bigger axis goes to ±1
                        if (maxVal > 0f)
                        {
                            rawX /= maxVal;
                            rawY = Mathf.Lerp(rawY, rawY / maxVal, Time.deltaTime * 10f); // Lerp rawY after normalization
                        }

                        // Now rawX, rawY lie on a square boundary, so diagonal pushes will be (±1, ±1)
                        vehicleController._spinInput = rawX;
                        vehicleController._flipInput = rawY;
                    }


                    //set lateral hop power
                    vehicleController._lateralInputLeftRaw = Mathf.Clamp(vehicleController._lateralInputLeftRaw, 0, sideHopPower);
                    vehicleController._lateralInputRightRaw = Mathf.Clamp(vehicleController._lateralInputRightRaw, 0, sideHopPower);
                    */
                    
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Physics.FixedUpdate] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
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
                    vehicleInstance.JumpSettings.JumpVelocity[2] = hopForce;
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
