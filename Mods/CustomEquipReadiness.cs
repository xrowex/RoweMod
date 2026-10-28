using System;
using System.Collections.Generic;
using System.Reflection;

namespace rowemod.Mods
{
    internal static class CustomEquipReadiness
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<Type, Reader> Readers = new();

        internal static bool CanSubmit(object slot)
        {
            if (slot == null) return false;
            Type type = slot.GetType();
            if (!Readers.TryGetValue(type, out Reader reader)) Readers[type] = reader = new Reader(type);
            return reader.CanSubmit(slot);
        }

        private sealed class Reader
        {
            private readonly PropertyInfo complete, handle;
            private readonly Dictionary<string, PropertyInfo> fields = new();
            internal Reader(Type type)
            {
                complete = type.GetProperty("IsEquipRequestComplete", Flags);
                handle = type.GetProperty("_lastLoadHandle", Flags);
                foreach (string name in new[] { "_isAlive", "_isBusyEquipping", "_go", "_pendingInstance", "_equippingCo",
                    "_currentEquipUid", "_EquippedUid", "_equipNextLoad", "_localReloadPending", "_currentSelectedData" })
                    fields[name] = type.GetProperty(name, Flags);
            }

            private bool Is(object slot, string name, object expected) =>
                fields[name] != null && Equals(fields[name].GetValue(slot), expected);

            internal bool CanSubmit(object slot)
            {
                // Keep the completion API authoritative on ordinary slots, and retain
                // support for the original wrappers which did not expose that API.
                if (complete == null || (bool)complete.GetValue(slot)) return true;
                try
                {
                    // Clearing an empty native slot can advance its request nonce without
                    // completing it. That is not an in-flight load. Never rewrite native
                    // nonces or cancel a valid load to make this fallback pass.
                    if (!Is(slot, "_isAlive", true) || !Is(slot, "_isBusyEquipping", false) ||
                        !Is(slot, "_go", null) || !Is(slot, "_pendingInstance", null) || !Is(slot, "_equippingCo", null) ||
                        !Is(slot, "_currentEquipUid", 0UL) || !Is(slot, "_EquippedUid", 0UL) ||
                        !Is(slot, "_equipNextLoad", false) || !Is(slot, "_localReloadPending", false) ||
                        !Is(slot, "_currentSelectedData", null)) return false;
                    object operation = handle?.GetValue(slot);
                    MethodInfo valid = operation?.GetType().GetMethod("IsValid", Flags, null, Type.EmptyTypes, null);
                    return valid?.ReturnType == typeof(bool) && !(bool)valid.Invoke(operation, null);
                }
                catch { return false; } // Unavailable fields/handles are not proof of idle.
            }
        }
    }
}
