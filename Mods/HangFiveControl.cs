using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Il2CppMashBox.BMX_Physics_Development;
using rowemod.Utils;

namespace rowemod.Mods
{
    /// <summary>
    /// Routes the game's existing Nosey Variant write BEFORE it reaches either animator.
    /// No frame loop, IK override, shared getter hook, or global shoulder-input blocking.
    /// </summary>
    internal static class HangFiveControl
    {
        private const string SupportedGameHash = "3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1";
        private const long SetIntegerRva = 0x85BE70;
        private const string NoseyVariantParameter = "Nosey Variant";
        private const int DiagnosticLogLimit = 6;
        private static bool _installAttempted;
        private static bool _hooksReady;
        private static bool _gameplayScene;
        private static string _hookStatus = "Nose-manual guard is waiting for native patching support.";
        private static VehicleAnimationInputHandler _localHandler;
        private static IntPtr _localHandlerPointer;
        private static int _diagnosticLogs;
        private static int _lastMode = int.MinValue;
        private static int _lastNativeVariant = int.MinValue;

        public static string HookStatus => _hooksReady && _localHandlerPointer == IntPtr.Zero
            ? "Nose-manual guard ready; waiting for the local bike."
            : _hookStatus;

        // Deliberately no HarmonyPatch attributes: early PatchAll would cache the wrong
        // (managed-wrapper-only) patcher before Il2CppInterop's native backend is ready.
        public static void InstallNativeHooks(global::HarmonyLib.Harmony harmony)
        {
            if (_installAttempted)
                return;
            _installAttempted = true;
            MethodInfo target = null;
            MethodInfo prefix = AccessTools.DeclaredMethod(typeof(HangFiveControl), nameof(SetIntegerPrefix));
            try
            {
                using Process process = Process.GetCurrentProcess();
                ProcessModule gameModule = null;
                foreach (ProcessModule module in process.Modules)
                {
                    if (string.Equals(module.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        gameModule = module;
                        break;
                    }
                }
                if (gameModule == null)
                    throw new InvalidOperationException("GameAssembly module not found.");
                using (FileStream binary = File.OpenRead(gameModule.FileName))
                using (SHA256 sha = SHA256.Create())
                {
                    if (!string.Equals(Convert.ToHexString(sha.ComputeHash(binary)), SupportedGameHash, StringComparison.Ordinal))
                        throw new InvalidOperationException("This game build has not been audited for native nose-manual hooks.");
                }

                target = ValidateNativeTarget(gameModule);
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                _hooksReady = true;
                _hookStatus = "Local nose-manual variant guard ready (verified native hook).";
                Log.Msg($"[HangFive] {_hookStatus} No animator-wide, input, or per-frame override hooks installed.");
            }
            catch (Exception ex)
            {
                _hooksReady = false;
                if (target != null)
                {
                    try { harmony.Unpatch(target, prefix); }
                    catch (Exception rollback) { Log.Warning($"[HangFive] Could not remove our prefix: {rollback.Message}. Restart the game."); }
                }
                _hookStatus = "Nose-manual guard unavailable on this build. Game behavior is unchanged; check the log.";
                Log.Warning($"[HangFive] {_hookStatus} {ex}");
            }
        }

        private static unsafe MethodInfo ValidateNativeTarget(ProcessModule gameModule)
        {
            MethodInfo target = AccessTools.DeclaredMethod(typeof(VehicleAnimationInputHandler),
                nameof(VehicleAnimationInputHandler.SetInteger), new[] { typeof(string), typeof(int) });
            if (target == null || target.IsStatic || target.IsGenericMethod || target.ReturnType != typeof(void))
                throw new InvalidOperationException("Unexpected VehicleAnimationInputHandler.SetInteger signature.");
            FieldInfo methodField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target);
            if (methodField == null)
                throw new InvalidOperationException("No native SetInteger method metadata.");
            IntPtr methodInfo = (IntPtr)methodField.GetValue(null);
            if (methodInfo == IntPtr.Zero)
                throw new InvalidOperationException("Null native SetInteger method metadata.");
            IntPtr nativeAddress = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfo).MethodPointer;
            long actualRva = nativeAddress.ToInt64() - gameModule.BaseAddress.ToInt64();
            if (actualRva != SetIntegerRva || actualRva < 0 || actualRva >= gameModule.ModuleMemorySize)
                throw new InvalidOperationException($"Refusing SetInteger: RVA 0x{actualRva:X} does not match audited 0x{SetIntegerRva:X}.");
            var patcher = PatchManager.GetMethodPatcher(target);
            if (patcher?.GetType().FullName != "Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher")
                throw new InvalidOperationException($"Native patcher not ready: {patcher?.GetType().FullName ?? "none"}.");
            Log.Msg($"[HangFive] Verified unique VehicleAnimationInputHandler.SetInteger at RVA 0x{actualRva:X}.");
            return target;
        }

        // Indexed arguments are intentional: generated IL2CPP parameter names may be blank.
        // Let the original method forward the routed value to BOTH rider and bike animators.
        private static void SetIntegerPrefix(VehicleAnimationInputHandler __instance, string __0, ref int __1)
        {
            if (!_hooksReady || !_gameplayScene || !RemoteKillSwitched.isModEnabled ||
                _localHandlerPointer == IntPtr.Zero || __instance is null ||
                __instance.Pointer != _localHandlerPointer ||
                !string.Equals(__0, NoseyVariantParameter, StringComparison.Ordinal))
                return;
            int mode = Config.physics.hangFiveMode;
            if (mode != 1 && mode != 2)
                return;

            int nativeVariant = __1;
            __1 = mode == 1 ? 0 : nativeVariant == 1 ? 2 : nativeVariant == 2 ? 1 : nativeVariant;
            if (_lastMode == mode && _lastNativeVariant == nativeVariant)
                return;
            _lastMode = mode;
            _lastNativeVariant = nativeVariant;
            // Check the cap BEFORE formatting; held bumpers do not generate log spam.
            if (_diagnosticLogs >= DiagnosticLogLimit)
                return;
            _diagnosticLogs++;
            Log.Msg($"[HangFive] Local Nosey Variant {nativeVariant} -> {__1}; mode={mode}." +
                (_diagnosticLogs == DiagnosticLogLimit ? " Further variant messages suppressed." : string.Empty));
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            ClearLocalHandler();
            _gameplayScene = gameplayScene;
        }

        public static void OnLocalPlayerSpawned()
        {
            ClearLocalHandler();
            BindLocalHandler();
        }

        public static void ApplySettingChange()
        {
            ResetDiagnostics();
            if (_localHandler == null)
                BindLocalHandler();
            // The next normal game animation-input write uses this mode. Don't add a
            // second animator write, mutate the source variant, or invert swaps twice.
            if (_hooksReady && _gameplayScene && RemoteKillSwitched.isModEnabled)
                Log.Msg($"[HangFive] Mode={Config.physics.hangFiveMode}; applies to the next local Nosey Variant write.");
        }

        private static void BindLocalHandler()
        {
            if (!_hooksReady || !_gameplayScene || !RemoteKillSwitched.isModEnabled)
                return;
            try
            {
                // Existing local-spawn event supplies this root. No scene-wide search
                // or LocalDriverVehicle property lookup in the native prefix.
                var vehicle = Memory.rMbCharacter == null ? null :
                    Memory.rMbCharacter.GetComponentInChildren<VehicleController>(true);
                if (vehicle != null)
                    _localHandler = vehicle.GetComponent<VehicleAnimationInputHandler>() ??
                        vehicle.GetComponentInChildren<VehicleAnimationInputHandler>(true) ??
                        vehicle.GetComponentInParent<VehicleAnimationInputHandler>();
                _localHandlerPointer = _localHandler == null ? IntPtr.Zero : _localHandler.Pointer;
                if (_localHandlerPointer == IntPtr.Zero)
                    Log.Warning("[HangFive] No local bike animation handler found. No global fallback will be used.");
                else
                    Log.Msg($"[HangFive] Bound local bike animation handler {_localHandler.GetInstanceID()}; mode={Config.physics.hangFiveMode}. Remote riders are untouched.");
            }
            catch (Exception ex)
            {
                ClearLocalHandler();
                Log.Warning($"[HangFive] Local bike binding failed: {ex.Message}");
            }
        }

        private static void ResetDiagnostics()
        {
            _diagnosticLogs = 0;
            _lastMode = int.MinValue;
            _lastNativeVariant = int.MinValue;
        }

        private static void ClearLocalHandler()
        {
            _localHandler = null;
            _localHandlerPointer = IntPtr.Zero;
            ResetDiagnostics();
        }

        public static void Cleanup()
        {
            _gameplayScene = false;
            ClearLocalHandler();
        }
    }
}
