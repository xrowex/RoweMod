/*using HarmonyLib;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation;
using UnityEngine.InputSystem;

namespace rowemod.Mods.Patches
{
    public static class ActiveRagdollBehaviourPatches
    {
        [HarmonyPatch(typeof(ActiveRagdollBehaviour), "KillDriver")]
        public static class KillDriverPatch
        {
            public static bool Prefix(ActiveRagdollBehaviour __instance)
            {
                if (Gamepad.current.buttonWest.wasPressedThisFrame) return true;
                return !Config.bNeverBail;
            }
        }

        [HarmonyPatch(typeof(ActiveRagdollBehaviour), "Kill")]
        public static class KillPatch
        {
            public static bool Prefix(ActiveRagdollBehaviour __instance)
            {
                if (Gamepad.current.buttonWest.wasPressedThisFrame) return true;
                return !Config.bNeverBail;
            }
        }


    }
}*/