using HarmonyLib;
using Il2CppMashBox.BMX_Physics_Development;
using UnityEngine.InputSystem;
using static rowemod.Utils.Memory;

namespace rowemod.Mods.Patches
{
    public static class TestDeathPatches
    {
        [HarmonyPatch(typeof(TestDeath), "KillDriver")]
        public static class KillDriverPatch
        {
            public static bool Prefix(TestDeath __instance)
            {
                if (Gamepad.current.buttonWest.wasPressedThisFrame) return true;
                return !Config.bNeverBail;
            }
        }

        [HarmonyPatch(typeof(TestDeath), "Kill")]
        public static class KillPatch
        {
            public static bool Prefix(TestDeath __instance)
            {
                if (Gamepad.current.buttonWest.wasPressedThisFrame) return true;
                return !Config.bNeverBail;
            }
        }
    }
}