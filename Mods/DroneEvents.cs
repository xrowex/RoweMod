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

    internal static class DroneControllerLocalSpawnBulletPatch
    {
        private static bool Prefix()
        {
            return !ObjectDropper.ShouldBlockDroneShot();
        }

        private static void Postfix(bool __runOriginal, DroneController __instance, Vector3 __0, Quaternion __1, Vector3 __2)
        {
            // CRITICAL: If Prefix returned false, __runOriginal will be false. 
            // We exit early because the drone didn't actually shoot.
            if (!__runOriginal)
                return;

            // Check if the mod features are enabled
            if (!RemoteKillSwitched.isModEnabled)
                return;

            DroneEvents.OnBallShot(__instance, __0, __1, __2);
        }
    }

    internal static class DroneControllerRpcFireBulletPatch
    {
        private static bool Prefix()
        {
            return !ObjectDropper.ShouldBlockDroneShot();
        }
    }

}
