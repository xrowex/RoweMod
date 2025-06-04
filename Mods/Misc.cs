using Il2CppMashBox.Core.Runtime.Input;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using static rowemod.Config;
using static rowemod.Utils.Memory;

namespace rowemod.Mods
{
    public static class Misc
    {
        private static bool lastDisableDroneCollider = bDisableDroneCollider;

        public static void ToggleEmoteInput()
        {
            InputActionAsset[] ias = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            foreach(InputActionAsset ia in ias)
            {
                if(ia.name == "MashBox_Freerun_InputActions")
                {
                    ia.actionMaps[0].m_Actions[5] = null;
                    break;
                }
            }
        }


        public static void Update()
        {
            try
            {
                // Detect change in bDisableDroneCollider
                if (lastDisableDroneCollider != bDisableDroneCollider)
                {
                    Log.Msg($"Detected bDisableDroneCollider change from {lastDisableDroneCollider} to {bDisableDroneCollider}. Refreshing drone components.");
                    Memory.RefreshDroneComponents();
                    lastDisableDroneCollider = bDisableDroneCollider;
                }

                // Update Drone Rigidbody Mass
                if (droneRb != null)
                {
                    droneRb.mass = droneMass;
                }

                // Update Drone Mesh Renderers (Hide Body)
                if (allDroneMeshRenderers != null && allDroneMeshRenderers.Count > 0)
                {
                    foreach (var renderer in allDroneMeshRenderers)
                    {
                        if (renderer != null)
                        {
                            renderer.enabled = droneBodyToggle;
                        }
                    }
                }

                // Update Drone Sound Emitters
                if (droneEmitters != null && droneEmitters.Length > 0)
                {
                    foreach (var emitter in droneEmitters)
                    {
                        if (emitter != null)
                        {
                            emitter.enabled = droneEmitterToggle;
                        }
                    }
                }

                // Update Drone Colliders
                if (droneColliders != null && droneColliders.Count > 0)
                {
                    foreach (var collider in droneColliders)
                    {
                        if (collider != null)
                        {
                            collider.enabled = bDisableDroneCollider;
                        }
                    }
                }

                // Update FreeCam Collider
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