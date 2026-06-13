using HarmonyLib;
using Il2CppMashBox.Addons.ProtoDrone;
using System;
using UnityEngine;
using Log = rowemod.Utils.Log;

namespace rowemod.Mods
{
    public static class DroneEvents
    {
        public static event Action<DroneController, Vector3, Quaternion, Vector3> BallShot;

        internal static void OnBallShot(DroneController drone, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            // Using a local copy for thread safety (good practice, though Unity is mostly single-threaded)
            BallShot?.Invoke(drone, position, rotation, velocity);
            Log.Msg($"Drone ball shot at {position} with velocity {velocity}.");
        }
    }

    [HarmonyPatch(typeof(DroneController), nameof(DroneController.LocalSpawnBullet))]
    internal static class DroneControllerLocalSpawnBulletPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !ObjectDropper.ShouldBlockDroneShot();
        }

        [HarmonyPostfix]
        private static void Postfix(bool __runOriginal, DroneController __instance, Vector3 pos, Quaternion rot, Vector3 velocity)
        {
            // CRITICAL: If Prefix returned false, __runOriginal will be false. 
            // We exit early because the drone didn't actually shoot.
            if (!__runOriginal)
                return;

            // Check if the mod features are enabled
            if (!RemoteKillSwitch.isModEnabled)
                return;

            DroneEvents.OnBallShot(__instance, pos, rot, velocity);
        }
    }

    [HarmonyPatch(typeof(DroneController), "RPC_FireBullet")]
    internal static class DroneControllerRpcFireBulletPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(DroneController), "RPC_FireBullet") != null;
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !ObjectDropper.ShouldBlockDroneShot();
        }
    }

    [HarmonyPatch(typeof(DroneManager), nameof(DroneManager.TestDeployCommand))]
    internal static class DisableDroneDpadShortcutPatch
    {
        private static bool verified;
        private static bool loggedInvocation;

        internal static void EnsureApplied(global::HarmonyLib.Harmony harmony)
        {
            if (verified)
                return;

            verified = true;

            try
            {
                System.Reflection.MethodInfo target =
                    AccessTools.Method(typeof(DroneManager), nameof(DroneManager.TestDeployCommand));
                System.Reflection.MethodInfo prefix =
                    AccessTools.Method(typeof(DisableDroneDpadShortcutPatch), nameof(Prefix));

                if (target == null || prefix == null)
                {
                    Log.Error("[PieMenu] Could not resolve DroneManager.TestDeployCommand patch methods.");
                    return;
                }

                Patches patchInfo = global::HarmonyLib.Harmony.GetPatchInfo(target);
                bool alreadyPatched = patchInfo?.Owners.Contains(harmony.Id) == true;

                if (!alreadyPatched)
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                    patchInfo = global::HarmonyLib.Harmony.GetPatchInfo(target);
                    alreadyPatched = patchInfo?.Owners.Contains(harmony.Id) == true;
                }

                Log.Msg(
                    $"[PieMenuDiag] Drone shortcut patch target='{target.DeclaringType?.FullName}.{target.Name}', " +
                    $"ownedByRoweMod={alreadyPatched}, prefixCount={patchInfo?.Prefixes.Count ?? 0}.");
            }
            catch (Exception ex)
            {
                Log.Error($"[PieMenu] Failed to verify drone shortcut patch: {ex}");
            }
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!loggedInvocation)
            {
                loggedInvocation = true;
                Log.Msg("[PieMenuDiag] Blocked DroneManager.TestDeployCommand.");
            }

            // Reserve the game's drone shortcut input for the pie menu.
            return false;
        }
    }
}
