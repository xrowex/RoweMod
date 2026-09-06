using System;
using Il2CppMashBox.Character.Scripts;
using UnityEngine;
using rowemod.Utils;

namespace rowemod.Mods
{
    // ApplyRenderPolicy is a unique audited native method, installed by LateNativeHooks.
    internal static class CustomHairRenderPolicyPatch
    {
        internal static void Prefix(EquipSlot __instance)
        {
            try
            {
                if (__instance == null || !__instance._renderDisabled ||
                    !Custom.ShouldKeepCustomHairVisible(__instance))
                    return;

                // Recalculate writes this flag before calling ApplyRenderPolicy. Mask it
                // BEFORE native SetActive(false), so cloth/occlusion never sees a disable.
                // The original hat/bust source flags remain in CharacterFeatureController.
                __instance._renderDisabled = false;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CustomHair] Could not apply visibility override: {ex.Message}");
            }
        }
    }

    internal static class CustomEquipCompletionPatch
    {
        internal static void Postfix(EquipSlot._EnumEquip_d__80 __instance, bool __result)
        {
            if (__result || __instance == null) return;
            try
            {
                Custom.OnNativeEquipCompleted(__instance.__4__this, __instance.go);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CustomEquip] Completion notification failed: {ex.Message}");
            }
        }
    }
}
