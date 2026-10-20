using System;
using System.Reflection;
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
        private static PropertyInfo _slot;
        private static PropertyInfo _source;

        internal static void Prepare(Type coroutineType)
        {
            PropertyInfo slot = coroutineType.GetProperty("__4__this", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo source = coroutineType.GetProperty("go", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (slot?.PropertyType != typeof(EquipSlot) || slot.GetGetMethod(true) == null ||
                source?.PropertyType != typeof(GameObject) || source.GetGetMethod(true) == null)
                throw new InvalidOperationException("Unsupported native equip completion wrapper properties.");
            _slot = slot;
            _source = source;
        }

        // Keep the generated coroutine out of every compiled signature as well as
        // Install's IL. Harmony supplies the actual wrapper through __instance.
        internal static void Postfix(object __instance, bool __result)
        {
            if (__result || __instance == null) return;
            try
            {
                Custom.OnNativeEquipCompleted((EquipSlot)_slot.GetValue(__instance), (GameObject)_source.GetValue(__instance));
            }
            catch (Exception ex)
            {
                Log.Warning($"[CustomEquip] Completion notification failed: {ex.Message}");
            }
        }
    }
}
