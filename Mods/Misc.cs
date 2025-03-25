using rowemod.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using static rowemod.Utils.Memory;
using static rowemod.Config;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.LookDev;
using Harmony;
using Il2CppInterop.Runtime.InteropTypes.Fields;
using Object = UnityEngine.Object;

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

                if (helmet != null)
                {
                    if (bHideHelmet) helmet.active = false;
                    else helmet.active = true;
                }

                roweTimeInterpolator.maxStanimaTime = sloMoTimer;

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
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            

        }
    }
}
