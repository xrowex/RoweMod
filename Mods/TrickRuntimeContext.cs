using System;
using System.Reflection;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using Il2CppMashBox.Core.Runtime.TrickSystem;
using UnityEngine.InputSystem;
using rowemod.Utils;

namespace rowemod.Mods
{
    internal static class TrickRuntimeContext
    {
        internal static InputAction LocalInputAction(int index)
        {
            var input = Memory.playerInputBehaviour;
            if (input == null || index < 1 || index > 8) return null;
            return input.GetType().GetProperty($"_trickSet{index}InputAction")?.GetValue(input) as InputAction;
        }
        internal static TrickDataSets LocalDataSets()
        {
            var vehicle = Memory.vehicleController;
            if (vehicle != null && Memory.rMbCharacter != null)
            {
                // This is the same provider used by native ResolveVehicleTrick.
                var property = vehicle.GetType().GetProperty("VehicleTrickDataSets");
                if (property?.GetValue(vehicle) is TrickDataSets data && data.TrickSets != null) return data;
            }
            return LocalBrain()?._trickDataSets;
        }

        private static TrickSystemBrainV2 LocalBrain()
        {
            var root = Memory.rMbCharacter;
            if (root == null) return null;
            foreach (var brain in root.GetComponentsInChildren<TrickSystemBrainV2>(true))
                if (brain != null && brain.enabled && brain.gameObject.activeInHierarchy) return brain;
            return null;
        }

        internal static void Refresh()
        {
            var brain = LocalBrain();
            if (brain == null) return;
            // Refresh the native set cache only. Never enable disabled components,
            // restart other riders' brains, or unregister/register input actions.
            MethodInfo refresh = brain.GetType().GetMethod("RefreshTrickSet", Type.EmptyTypes);
            refresh?.Invoke(brain, null);
        }
    }
}
