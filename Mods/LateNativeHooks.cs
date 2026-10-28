using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppFusion;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.Addons.ProtoDrone;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Core.Runtime.InverseKinematics;
using FusionBootstrap = Il2CppMashBox.Netorking.FusionBootstrap;
using Il2CppMashBoxBridge.Common.Interfaces;
using Log = rowemod.Utils.Log;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Explicit allowlist replacing both early PatchAll and MelonLoader auto-patching.
    /// Addresses are audited across ALL Cpp2IL methods, including nested/generic types.
    /// </summary>
    internal static class LateNativeHooks
    {
        private static bool _attempted;
        private static readonly HashSet<string> ReadyGroups = new HashSet<string>();
        internal static bool ManualIkReady => ReadyGroups.Contains("Manual IK");

        internal static void Install(global::HarmonyLib.Harmony harmony)
        {
            if (_attempted)
                return;
            _attempted = true;
            InstallGroup(harmony, "Host settings",
                new Hook(typeof(FusionBootstrap), "DrawServerBrowserHostMapControls", 0xA61F70,
                    typeof(HostPlayerLimitHostSetupPatch), typeof(float)),
                new Hook(typeof(NetworkRunner), "StartGame", 0xD53AA0,
                    typeof(HostPlayerLimitStartGamePatch), typeof(StartGameArgs)));
            InstallGroup(harmony, "Player labels",
                new Hook(typeof(NetworkPlayer), "Spawned", 0x9F47C0, typeof(PlayerUserNameTargetSpawnPatch)));
            InstallGroup(harmony, "Custom hair visibility",
                new Hook(typeof(Il2CppMashBox.Character.Scripts.EquipSlot), "ApplyRenderPolicy", 0x7DE910,
                    typeof(CustomHairRenderPolicyPatch)),
                new Hook(typeof(Il2CppMashBox.Character.Scripts.EquipSlot), "_EnumEquip_d__80", "MoveNext", 0x7F15C0,
                    typeof(CustomEquipCompletionPatch), "_EnumEquip_d__111", "_EnumEquip_d__112"));
            InstallGroup(harmony, "Manual IK",
                new Hook(typeof(HumanIK), "OnAnimatorIK", 0x2983750, typeof(ManualIkHumanPatch), typeof(int)),
                new Hook(typeof(UnityIKLimb), "UpdateIK", 0x298C9E0, typeof(ManualIkNativeLimbPatch), typeof(bool)),
                new Hook(typeof(VehicleFootPedalAnimationRig), "LateUpdate", 0x89A960, typeof(ManualIkPedalRigPatch)));
            InstallGroup(harmony, "Grind remapping",
                new Hook(typeof(BikeGrindPoser), "SetInputData", 0x8077B0,
                    typeof(BikeGrindPoserInputRemapPatch), typeof(int), typeof(HookGrind)));
            InstallGroup(harmony, "Landing assistance",
                new Hook(typeof(QuaternionPDDrive), "Tick", 0x837760,
                    typeof(OnePointOhQuaternionDrivePatch), typeof(Rigidbody), typeof(Quaternion)));
            InstallGroup(harmony, "Drone placement input",
                new Hook(typeof(DroneController), "LocalSpawnBullet", 0x90DA80,
                    typeof(DroneControllerLocalSpawnBulletPatch),
                    new[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3) },
                    new[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(float) }),
                new Hook(typeof(DroneController), "RPC_FireBullet", 0x90E100,
                    typeof(DroneControllerRpcFireBulletPatch),
                    new[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3) },
                    new[] { typeof(int), typeof(double), typeof(Vector3), typeof(Quaternion), typeof(Vector3) }));
            Log.Msg($"[NativeHooks] Late registration complete: {ReadyGroups.Count}/7 feature groups ready. No blanket PatchAll.");
        }

        private static void InstallGroup(global::HarmonyLib.Harmony harmony, string name, params Hook[] hooks)
        {
            try
            {
                // Validate the ENTIRE group before installing any part of it.
                foreach (Hook hook in hooks)
                    hook.ResolveAndValidate();
                foreach (Hook hook in hooks)
                {
                    hook.PatchAttempted = true;
                    harmony.Patch(hook.Target,
                        prefix: hook.Prefix == null ? null : new HarmonyMethod(hook.Prefix),
                        postfix: hook.Postfix == null ? null : new HarmonyMethod(hook.Postfix));
                }
                ReadyGroups.Add(name);
                Log.Msg($"[NativeHooks] {name}: installed {hooks.Length} verified native target(s).");
            }
            catch (Exception ex)
            {
                // Never remove another feature/mod's patches when a group fails.
                foreach (Hook hook in hooks)
                {
                    // Unpatch itself can resolve/create a patcher. Never call it for
                    // a target rejected before patching (especially an unknown build).
                    if (!hook.PatchAttempted)
                        continue;
                    TryRemove(harmony, hook.Target, hook.Prefix);
                    TryRemove(harmony, hook.Target, hook.Postfix);
                }
                Log.Warning($"[NativeHooks] {name} hooks unavailable; group skipped. {ex}");
            }
        }

        private static void TryRemove(global::HarmonyLib.Harmony harmony, MethodInfo target, MethodInfo patch)
        {
            if (target == null || patch == null)
                return;
            try { harmony.Unpatch(target, patch); }
            catch (Exception ex) { Log.Warning($"[NativeHooks] Rollback failed for {target.Name}: {ex.Message}. Restart the game."); }
        }

        private sealed class Hook
        {
            private readonly Type _type;
            private readonly string _nestedType;
            private readonly string[] _alternateNestedTypes;
            private readonly string _method;
            private readonly long _rva;
            private readonly Type _patch;
            private readonly Type[] _arguments;
            private readonly Type[] _alternateArguments;
            internal MethodInfo Target;
            internal MethodInfo Prefix;
            internal MethodInfo Postfix;
            internal bool PatchAttempted;

            internal Hook(Type type, string method, long rva, Type patch, params Type[] arguments)
            {
                _type = type; _method = method; _rva = rva; _patch = patch; _arguments = arguments;
            }

            internal Hook(Type type, string method, long rva, Type patch, Type[] arguments, Type[] alternateArguments)
                : this(type, method, rva, patch, arguments)
            {
                _alternateArguments = alternateArguments;
            }

            internal Hook(Type owner, string nestedType, string method, long rva, Type patch, params string[] alternateNestedTypes)
                : this(owner, method, rva, patch, Type.EmptyTypes)
            {
                _nestedType = nestedType;
                _alternateNestedTypes = alternateNestedTypes;
            }

            internal void ResolveAndValidate()
            {
                // Generated coroutine names vary between interop assemblies. Resolve
                // inside the group's try/catch; a typeof token can fail during JIT
                // compilation of Install, before any of its error handling runs.
                Type type = _nestedType == null ? _type :
                    _type.GetNestedType(_nestedType, BindingFlags.Public | BindingFlags.NonPublic);
                if (type == null && _alternateNestedTypes != null)
                    foreach (string alternate in _alternateNestedTypes)
                    {
                        type = _type.GetNestedType(alternate, BindingFlags.Public | BindingFlags.NonPublic);
                        if (type != null) break;
                    }
                if (type == null)
                    throw new TypeLoadException($"Missing generated wrapper {_type.FullName}+{_nestedType}. Check the BMX Streets build and MelonLoader interop assemblies.");
                Target = AccessTools.DeclaredMethod(type, _method, _arguments);
                if (Target == null && _alternateArguments != null)
                    Target = AccessTools.DeclaredMethod(type, _method, _alternateArguments);
                Prefix = AccessTools.DeclaredMethod(_patch, "Prefix");
                Postfix = AccessTools.DeclaredMethod(_patch, "Postfix");
                if (Prefix == null && Postfix == null)
                    throw new InvalidOperationException($"No patch methods for {_patch.Name}.");
                NativeHookSafety.Validate(Target, _rva);
                if (_patch == typeof(CustomEquipCompletionPatch))
                    CustomEquipCompletionPatch.Prepare(type);
            }
        }
    }
}
