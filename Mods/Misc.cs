using Il2CppMashBox.Core.Runtime.Input;
using MelonLoader;
using rowemod.Utils;
using System;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.Addons.SessionMarker;
using UnityEngine;
using UnityEngine.InputSystem;
using static rowemod.Config;
using static rowemod.Utils.Memory;

namespace rowemod.Mods
{
    public static class Misc
    {
        private static bool lastDisableDroneCollider = Config.misc.disableDroneCollider;
        private static bool lastShowPlayerUserNameTargets = Config.misc.showPlayerUserNameTargets;
        private static float nextPlayerUserNameTargetCheckTime = 0f;
        private const float PlayerUserNameTargetCheckInterval = 1f;
        private static int lastPlayerUserNameTargetCount = -1;
        private const string PlayerUserNameTargetComponentName = "PlayerUserNameTarget";

        public static void Update()
        {
            Memory.RefreshDroneComponents();
            // Update Ragdoll Behaviour
            if (activeRagdollBehaviour != null)
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

            ApplyPlayerUserNameTargetsVisibility();
        }

        public static void ApplyPlayerUserNameTargetsVisibility(bool force = false)
        {
            try
            {
                bool shouldEnableTargets = misc.showPlayerUserNameTargets;
                bool targetStateChanged = shouldEnableTargets != lastShowPlayerUserNameTargets;

                if (!force && !targetStateChanged)
                {
                    if (shouldEnableTargets)
                    {
                        return;
                    }

                    if (Time.unscaledTime < nextPlayerUserNameTargetCheckTime)
                    {
                        return;
                    }
                }
                
                int foundCount = 0;
                int changedCount = 0;

                PlayerUserNameTarget[] allComponents = Resources.FindObjectsOfTypeAll<PlayerUserNameTarget>();
                foreach (PlayerUserNameTarget targetComponent in allComponents)
                {
                    if (targetComponent == null || targetComponent.gameObject == null)
                        continue;

                    GameObject go = targetComponent.gameObject;
                    if (!go.scene.IsValid())
                        continue;

                    foundCount++;
                    if (go.activeSelf != shouldEnableTargets)
                    {
                        go.SetActive(shouldEnableTargets);
                        changedCount++;
                    }
                }

                if (force || targetStateChanged || changedCount > 0 || foundCount != lastPlayerUserNameTargetCount)
                {
                    Log.Msg(
                        $"PlayerUserNameTarget {(shouldEnableTargets ? "enabled" : "disabled")} | found: {foundCount}, changed: {changedCount}");
                }

                lastShowPlayerUserNameTargets = shouldEnableTargets;
                lastPlayerUserNameTargetCount = foundCount;
                nextPlayerUserNameTargetCheckTime = Time.unscaledTime + PlayerUserNameTargetCheckInterval;
            }
            catch (Exception ex)
            {
                Log.Error($"Error while applying PlayerUserNameTarget visibility: {ex.Message}");
            }
        }
    }
}
