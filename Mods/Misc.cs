using UnityEngine;
using static rowemod.Utils.Memory;
using static rowemod.Config;

namespace rowemod.Mods
{
    public static class Misc
    {
        public static void Update()
        {
            try
            {
                // Update Drone Rigidbody Mass
                if (droneRb != null)
                {
                    droneRb.mass = droneMass;
                }
                if (droneEmitters != null)
                {
                    foreach (var droneEmitter in droneEmitters)
                    {
                        if (droneEmitter != null)
                        {
                            droneEmitter.enabled = droneEmitterToggle;
                        }
                    }
                }
                if (helmet != null)
                {
                    if (bHideHelmet) helmet.active = false;
                    else helmet.active = true;
                }

                //roweTimeInterpolator.maxStanimaTime = sloMoTimer;

                if (allDroneMeshRenderers != null)
                {
                    foreach (var drnMesh in allDroneMeshRenderers)
                    {
                        if (drnMesh != null)
                        {
                            drnMesh.enabled = droneBodyToggle;
                        }
                        else
                        {
                            Debug.LogWarning("Null Renderer detected in allDroneMeshRenderers.");
                        }
                    }
                }

                // Added for FreeCam collider toggle feature
                if (freeCamCollider != null)
                {
                    freeCamCollider.enabled = !bDisableFreeCamCollider;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}