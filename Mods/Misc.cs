using Il2CppMashBox.Core.Runtime.Input;
using MelonLoader;
using rowemod.Utils;
using System;
using System.Collections.Generic;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation.BeyondMeat;
using Il2CppMashBox.Addons.SessionMarker;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using Il2CppMashBoxSDK;

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
        private const string DriftCarSpawnerObjectName = "CAR SPAWNER";
        private const string DriftCarSpawnKey = "Proto_Vehicle (Drift Car)";
        private const string DriftCarRequestKey = "0b389bb09dc44c21ae2c228e75e419bf";
        private const string DriftTrikeSpawnerObjectName = "DRIFT TRIKE SPAWNER";
        private const string DriftTrikeSpawnKey = "Drift Trike";
        private const string DriftTrikeRequestKey = DriftCarRequestKey;
        private static GameObject driftCarSpawnerHost;
        private static Il2CppMashBoxSDK.Maps.Rigging.MBNetworkedObjectSpawner driftCarSpawnerType;
        private static GameObject driftTrikeSpawnerHost;
        private static Il2CppMashBoxSDK.Maps.Rigging.MBNetworkedObjectSpawner driftTrikeSpawnerType;
        private static bool temporaryNeverBailOverrideActive;
        private static bool temporaryNeverBailOverrideValue;
        private const float BoneHitboxRefreshInterval = 1f;
        private static readonly Dictionary<int, BoneHitboxRuntimeState> boneHitboxOriginalStates =
            new Dictionary<int, BoneHitboxRuntimeState>();
        private static bool? lastDisableBoneBreaking;
        private static float lastBoneBreakingStrength = float.NaN;
        private static float nextBoneHitboxRefreshTime;
        private static int lastBoneHitboxCount = -1;

        private sealed class BoneHitboxRuntimeState
        {
            public BoneHitbox Hitbox;
            public float MinNormalDeltaV;
            public float DamageMultiplier;
        }
        

        public static void Update()
        {
            if (driftCarSpawnerType != null && Memory.activeMarkerTransform != null &&
                driftCarSpawnerType.spawnPoint != Memory.activeMarkerTransform)
                driftCarSpawnerType.spawnPoint = Memory.activeMarkerTransform;
            if (driftTrikeSpawnerType != null && Memory.activeMarkerTransform != null &&
                driftTrikeSpawnerType.spawnPoint != Memory.activeMarkerTransform)
                driftTrikeSpawnerType.spawnPoint = Memory.activeMarkerTransform;
            Memory.RefreshDroneComponents();
            // Update Ragdoll Behaviour
            ApplyNeverBailState();
            ApplyBoneBreakingState();

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

        public static void SetTemporaryNeverBailOverride(bool active, bool value = false)
        {
            temporaryNeverBailOverrideActive = active;
            temporaryNeverBailOverrideValue = value;
            ApplyNeverBailState();
        }

        private static void ApplyNeverBailState()
        {
            if (activeRagdollBehaviour != null)
                activeRagdollBehaviour._invinsible = temporaryNeverBailOverrideActive
                    ? temporaryNeverBailOverrideValue
                    : misc.neverBail;
        }

        public static void ApplyBoneBreakingState(bool force = false)
        {
            bool disableBoneBreaking = misc.disableBoneBreaking;
            float boneBreakingStrength = Mathf.Clamp(misc.boneBreakingStrength, 0.25f, 5f);
            misc.boneBreakingStrength = boneBreakingStrength;
            bool stateChanged = lastDisableBoneBreaking != disableBoneBreaking;
            bool strengthChanged = float.IsNaN(lastBoneBreakingStrength) ||
                                   !Mathf.Approximately(lastBoneBreakingStrength, boneBreakingStrength);
            bool hasOverride = disableBoneBreaking || !Mathf.Approximately(boneBreakingStrength, 1f);

            if (!force && !stateChanged && !strengthChanged)
            {
                if (!hasOverride || Time.unscaledTime < nextBoneHitboxRefreshTime)
                    return;
            }

            nextBoneHitboxRefreshTime = Time.unscaledTime + BoneHitboxRefreshInterval;

            try
            {
                BoneHitbox[] hitboxes = FindLocalBoneHitboxes();
                int changedCount = 0;

                foreach (BoneHitbox hitbox in hitboxes)
                {
                    if (hitbox == null)
                        continue;

                    int instanceId = hitbox.GetInstanceID();
                    if (!boneHitboxOriginalStates.ContainsKey(instanceId))
                    {
                        boneHitboxOriginalStates[instanceId] = new BoneHitboxRuntimeState
                        {
                            Hitbox = hitbox,
                            MinNormalDeltaV = hitbox.minNormalDeltaV,
                            DamageMultiplier = hitbox.damageMultiplier
                        };
                    }
                }

                foreach (BoneHitboxRuntimeState state in boneHitboxOriginalStates.Values)
                {
                    if (state.Hitbox == null)
                        continue;

                    float targetMinNormalDeltaV = disableBoneBreaking
                        ? float.MaxValue
                        : state.MinNormalDeltaV;
                    float targetDamageMultiplier = disableBoneBreaking
                        ? 0f
                        : state.DamageMultiplier / boneBreakingStrength;

                    bool changed = false;
                    if (state.Hitbox.minNormalDeltaV != targetMinNormalDeltaV)
                    {
                        state.Hitbox.minNormalDeltaV = targetMinNormalDeltaV;
                        changed = true;
                    }

                    if (!Mathf.Approximately(state.Hitbox.damageMultiplier, targetDamageMultiplier))
                    {
                        state.Hitbox.damageMultiplier = targetDamageMultiplier;
                        changed = true;
                    }

                    if (changed)
                        changedCount++;
                }

                if (!hasOverride)
                    boneHitboxOriginalStates.Clear();

                if (force || stateChanged || strengthChanged || hitboxes.Length != lastBoneHitboxCount || changedCount > 0)
                {
                    string mode = disableBoneBreaking
                        ? "blocked"
                        : Mathf.Approximately(boneBreakingStrength, 1f) ? "stock" : "scaled";
                    Log.Msg(
                        $"[BrokenBones] BeyondMeat collision damage {mode} | " +
                        $"strength={boneBreakingStrength:0.00}x, hitboxes={hitboxes.Length}, changed={changedCount}.");
                }

                lastDisableBoneBreaking = disableBoneBreaking;
                lastBoneBreakingStrength = boneBreakingStrength;
                lastBoneHitboxCount = hitboxes.Length;
            }
            catch (Exception ex)
            {
                Log.Error($"[BrokenBones] Failed to update BoneHitbox state: {ex.Message}");
            }
        }

        private static BoneHitbox[] FindLocalBoneHitboxes()
        {
            if (physicsDrivenCharacter != null)
            {
                BoneHitbox[] hitboxes = physicsDrivenCharacter.GetComponentsInChildren<BoneHitbox>(true);
                if (hitboxes != null && hitboxes.Length > 0)
                    return hitboxes;
            }

            if (beyondMeatSystem != null)
            {
                BoneHitbox[] hitboxes = beyondMeatSystem.GetComponentsInChildren<BoneHitbox>(true);
                if (hitboxes != null && hitboxes.Length > 0)
                    return hitboxes;
            }

            return Array.Empty<BoneHitbox>();
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

        public static void SpawnDriftCarInFrontOfPlayer()
        {
            try
            {
                if (!TryGetSessionMarkerSpawnTransform(out Vector3 spawnPosition, out Quaternion spawnRotation))
                {
                    Log.Warning("No active session marker transform available for drift car spawn.");
                    return;
                }

                if (!TryEnsureDriftCarSpawner())
                {
                    Log.Error("Drift car spawner component could not be created.");
                    return;
                }

                driftCarSpawnerHost.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                ConfigureDriftCarSpawner();

                if (TryInvokeSpawner(driftCarSpawnerType))
                {
                    Log.Msg($"Drift car spawn requested at {spawnPosition}.");
                }
                else
                {
                    Log.Error("Failed to invoke drift car spawn on MBNetworkedObjectSpawner.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SpawnDriftCarInFrontOfPlayer failed: {ex.Message}");
            }
        }

        public static void SpawnDriftTrikeInFrontOfPlayer()
        {
            try
            {
                if (!TryGetSessionMarkerSpawnTransform(out Vector3 spawnPosition, out Quaternion spawnRotation))
                {
                    Log.Warning("No active session marker transform available for drift trike spawn.");
                    return;
                }

                if (!TryEnsureDriftTrikeSpawner())
                {
                    Log.Error("Drift trike spawner component could not be created.");
                    return;
                }

                driftTrikeSpawnerHost.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                ConfigureDriftTrikeSpawner();

                if (TryInvokeSpawner(driftTrikeSpawnerType))
                {
                    Log.Msg($"Drift trike spawn requested at {spawnPosition}.");
                }
                else
                {
                    Log.Error("Failed to invoke drift trike spawn on MBNetworkedObjectSpawner.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SpawnDriftTrikeInFrontOfPlayer failed: {ex.Message}");
            }
        }

        private static bool TryGetSessionMarkerSpawnTransform(out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            spawnPosition = Vector3.zero;
            spawnRotation = Quaternion.identity;

            if (Memory.activeMarkerTransform == null)
            {
                return false;
            }

            spawnPosition = Memory.activeMarkerTransform.position;
            spawnRotation = Memory.activeMarkerTransform.rotation;

            return true;
        }

        private static bool TryEnsureDriftCarSpawner()
        {
            if (driftCarSpawnerType != null && driftCarSpawnerHost != null)
            {
                return true;
            }

            driftCarSpawnerHost = GameObject.Find(DriftCarSpawnerObjectName);
            if (driftCarSpawnerHost == null)
            {
                driftCarSpawnerHost = new GameObject(DriftCarSpawnerObjectName);
            }

            driftCarSpawnerType = driftCarSpawnerHost.GetComponent<Il2CppMashBoxSDK.Maps.Rigging.MBNetworkedObjectSpawner>();
            driftCarSpawnerType ??= driftCarSpawnerHost.AddComponent<Il2CppMashBoxSDK.Maps.Rigging.MBNetworkedObjectSpawner>();

            if (driftCarSpawnerType == null)
            {
                return false;
            }

            ConfigureDriftCarSpawner();
            return true;
        }

        private static void ConfigureDriftCarSpawner()
        {
            if (driftCarSpawnerType == null || driftCarSpawnerHost == null)
            {
                return;
            }

            SetMemberValue(driftCarSpawnerType, "requestKey", DriftCarRequestKey);
            SetMemberValue(driftCarSpawnerType, "RequestKey", DriftCarRequestKey);
            SetMemberValue(driftCarSpawnerType, "spawnKey", DriftCarSpawnKey);
            SetMemberValue(driftCarSpawnerType, "SpawnKey", DriftCarSpawnKey);
            Transform spawnPoint = Memory.activeMarkerTransform != null ? Memory.activeMarkerTransform : driftCarSpawnerHost.transform;
            SetMemberValue(driftCarSpawnerType, "spawnPoint", spawnPoint);
            SetMemberValue(driftCarSpawnerType, "SpawnPoint", spawnPoint);
            SetMemberValue(driftCarSpawnerType, "snapToGround", true);
            SetMemberValue(driftCarSpawnerType, "warnWhenUnavailable", true);
            SetMemberValue(driftCarSpawnerType, "localOffset", Vector3.zero);

            TryInvokeSpawnerMethod(driftCarSpawnerType, "SetSpawnKey", DriftCarSpawnKey);
            TryInvokeSpawnerMethod(driftCarSpawnerType, "EnsureRequestKey");
        }

        private static bool TryEnsureDriftTrikeSpawner()
        {
            if (driftTrikeSpawnerType != null && driftTrikeSpawnerHost != null)
            {
                return true;
            }

            driftTrikeSpawnerHost = GameObject.Find(DriftTrikeSpawnerObjectName);
            if (driftTrikeSpawnerHost == null)
            {
                driftTrikeSpawnerHost = new GameObject(DriftTrikeSpawnerObjectName);
            }

            driftTrikeSpawnerType = driftTrikeSpawnerHost.GetComponent<Il2CppMashBoxSDK.Maps.Rigging.MBNetworkedObjectSpawner>();
            driftTrikeSpawnerType ??= driftTrikeSpawnerHost.AddComponent<Il2CppMashBoxSDK.Maps.Rigging.MBNetworkedObjectSpawner>();

            if (driftTrikeSpawnerType == null)
            {
                return false;
            }

            ConfigureDriftTrikeSpawner();
            return true;
        }

        private static void ConfigureDriftTrikeSpawner()
        {
            if (driftTrikeSpawnerType == null || driftTrikeSpawnerHost == null)
            {
                return;
            }

            SetMemberValue(driftTrikeSpawnerType, "requestKey", DriftTrikeRequestKey);
            SetMemberValue(driftTrikeSpawnerType, "RequestKey", DriftTrikeRequestKey);
            SetMemberValue(driftTrikeSpawnerType, "spawnKey", DriftTrikeSpawnKey);
            SetMemberValue(driftTrikeSpawnerType, "SpawnKey", DriftTrikeSpawnKey);
            Transform spawnPoint = Memory.activeMarkerTransform != null ? Memory.activeMarkerTransform : driftTrikeSpawnerHost.transform;
            SetMemberValue(driftTrikeSpawnerType, "spawnPoint", spawnPoint);
            SetMemberValue(driftTrikeSpawnerType, "SpawnPoint", spawnPoint);
            SetMemberValue(driftTrikeSpawnerType, "snapToGround", true);
            SetMemberValue(driftTrikeSpawnerType, "warnWhenUnavailable", true);
            SetMemberValue(driftTrikeSpawnerType, "localOffset", Vector3.zero);

            TryInvokeSpawnerMethod(driftTrikeSpawnerType, "SetSpawnKey", DriftTrikeSpawnKey);
            TryInvokeSpawnerMethod(driftTrikeSpawnerType, "EnsureRequestKey");
        }

        private static bool TryInvokeSpawner(Il2CppMashBoxSDK.Maps.Rigging.MBNetworkedObjectSpawner spawner)
        {
            if (spawner == null)
            {
                return false;
            }

            try
            {
                spawner.Spawn();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvokeSpawnerMethod(Component component, string methodName, params object[] args)
        {
            if (component == null)
            {
                return false;
            }

            MethodInfo[] methods = component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                try
                {
                    object result = method.Invoke(component, args);
                    if (method.ReturnType == typeof(bool))
                    {
                        return result is bool b && b;
                    }

                    return true;
                }
                catch
                {
                    // Ignore and try another overload.
                }
            }

            return false;
        }

        private static bool SetMemberValue(Component component, string memberName, object value)
        {
            if (component == null)
            {
                return false;
            }

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = component.GetType();

            FieldInfo field = type.GetField(memberName, Flags);
            if (field != null)
            {
                try
                {
                    field.SetValue(component, value);
                    return true;
                }
                catch
                {
                    // Continue and try property.
                }
            }

            PropertyInfo property = type.GetProperty(memberName, Flags);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(component, value, null);
                    return true;
                }
                catch
                {
                    // Ignore assignment failures.
                }
            }

            return false;
        }

    }
}
