using Il2CppMashBox.Core.Runtime.Input;
using MelonLoader;
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
        private static bool lastDisableDroneCollider = Config.misc.disableDroneCollider;
        
        public static void Update()
        {
            // Detect change in bDisableDroneCollider
            if (lastDisableDroneCollider != misc.disableDroneCollider)
            {
                Log.Msg(
                    $"Detected bDisableDroneCollider change from {lastDisableDroneCollider} to {misc.disableDroneCollider}. Refreshing drone components.");
                Memory.RefreshDroneComponents();
                lastDisableDroneCollider = misc.disableDroneCollider;
            }
            
            
            // Update Ragdoll Behaviour
            if(activeRagdollBehaviour !=null)
                activeRagdollBehaviour._invinsible = misc.neverBail;
            
            // Update Drone Rigidbody Mass
            if (droneRb != null)
            {
                droneRb.mass = misc.droneMass;
            }
            
            // Update Drone Mesh Renderers (Hide Body)
            if (allDroneMeshRenderers != null && allDroneMeshRenderers.Count > 0)
            {
                foreach (var renderer in allDroneMeshRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = misc.droneBodyToggle;
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
                        emitter.enabled = misc.droneEmitterToggle;
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
                        collider.enabled = misc.disableDroneCollider;
                    }
                }
            }

            // Update FreeCam Collider
            if (freeCamCollider != null)
            {
                freeCamCollider.enabled = !misc.disableFreeCamCollider;
            }

        }
    }
}