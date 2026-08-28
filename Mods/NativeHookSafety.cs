using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib.Public.Patching;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;

namespace rowemod.Mods
{
    /// <summary>Startup-only validation, never called by gameplay prefixes.</summary>
    internal static class NativeHookSafety
    {
        private const string SupportedGameHash = "3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1";
        // Cache both success and failure: optional late hooks must not re-hash each spawn.
        private static readonly Lazy<AuditedModule> Module = new Lazy<AuditedModule>(ReadModule);

        internal static unsafe void Validate(MethodInfo target, long expectedRva)
        {
            if (target == null || target.IsStatic || target.ContainsGenericParameters || target.IsGenericMethod)
                throw new InvalidOperationException("Missing or unsupported native instance method.");
            AuditedModule module = Module.Value;
            FieldInfo field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target);
            if (field == null)
                throw new InvalidOperationException($"No native metadata for {target.Name}.");
            IntPtr methodInfo = (IntPtr)field.GetValue(null);
            if (methodInfo == IntPtr.Zero)
                throw new InvalidOperationException($"Null native metadata for {target.Name}.");
            IntPtr address = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfo).MethodPointer;
            long rva = address.ToInt64() - module.BaseAddress;
            if (rva != expectedRva || rva < 0 || rva >= module.Size)
                throw new InvalidOperationException($"Refusing {target.DeclaringType?.Name}.{target.Name}: native RVA 0x{rva:X} does not match audited 0x{expectedRva:X}.");
            var patcher = PatchManager.GetMethodPatcher(target);
            if (patcher?.GetType().FullName != "Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher")
                throw new InvalidOperationException($"Native patcher unavailable for {target.Name}: {patcher?.GetType().FullName ?? "none"}.");
        }

        private static AuditedModule ReadModule()
        {
            using Process process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
            {
                if (!string.Equals(module.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                using FileStream binary = File.OpenRead(module.FileName);
                using SHA256 sha = SHA256.Create();
                if (!string.Equals(Convert.ToHexString(sha.ComputeHash(binary)), SupportedGameHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("This game build has not been audited for native RoweMod hooks.");
                return new AuditedModule(module.BaseAddress.ToInt64(), module.ModuleMemorySize);
            }
            throw new InvalidOperationException("GameAssembly module not found.");
        }

        private sealed class AuditedModule
        {
            internal readonly long BaseAddress;
            internal readonly int Size;
            internal AuditedModule(long baseAddress, int size) { BaseAddress = baseAddress; Size = size; }
        }
    }
}
