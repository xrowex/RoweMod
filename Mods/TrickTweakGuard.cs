using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using rowemod.Utils;

namespace rowemod.Mods
{
    /// <summary>
    /// Event-bound, local-rider-only guard. Never patch generic/constant-return getters:
    /// IL2CPP can fold unrelated methods (including Fusion loading checks) into one body.
    /// </summary>
    internal static class TrickTweakGuard
    {
        // Audited across every method in matching Cpp2IL metadata, not just this class.
        // Unknown binaries leave the game untouched until their addresses are re-audited.
        private const string SupportedGameHash = "3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1";
        private const long RequestTweakRva = 0x8B0A50;
        private const long TweakRva = 0x8B0E30;
        private const int BlockLogLimit = 3;
        private static bool _installAttempted;
        private static bool _hooksReady;
        private static bool _gameplayScene;
        private static string _hookStatus = "Tweak guard is waiting for native patching support.";
        private static TrickControllerV2 _localController;
        private static IntPtr _localControllerPointer;
        private static bool _capturedAutoTweak;
        private static bool _originalAutoTweak;
        private static int _blockedRequestLogs;
        private static int _blockedDirectLogs;

        public static string HookStatus => _hookStatus;

        // Explicit late registration prevents early PatchAll caching managed-only patchers.
        // Keep these methods free of HarmonyPatch attributes.
        public static void InstallNativeHooks(global::HarmonyLib.Harmony harmony)
        {
            if (_installAttempted)
                return;
            _installAttempted = true;
            MethodInfo request = null;
            MethodInfo transition = null;
            MethodInfo requestPrefix = AccessTools.DeclaredMethod(typeof(TrickTweakGuard), nameof(RequestTweakPrefix));
            MethodInfo transitionPrefix = AccessTools.DeclaredMethod(typeof(TrickTweakGuard), nameof(DirectTweakPrefix));
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
                        throw new InvalidOperationException("This game build has not been audited for native tweak hooks.");
                }

                // Validate BOTH before installing either. Names alone don't establish
                // native identity or protect against shared code addresses.
                request = ValidateNativeTarget(nameof(TrickControllerV2.RequestTweak), RequestTweakRva, gameModule);
                transition = ValidateNativeTarget(nameof(TrickControllerV2.Tweak), TweakRva, gameModule);
                harmony.Patch(request, prefix: new HarmonyMethod(requestPrefix));
                harmony.Patch(transition, prefix: new HarmonyMethod(transitionPrefix));
                _hooksReady = true;
                _hookStatus = "Local-rider tweak guard ready (2 verified native hooks).";
                Log.Msg($"[TrickTweakGuard] {_hookStatus} No property, legacy-state, or Awake hooks installed.");
            }
            catch (Exception ex)
            {
                // Remove only our exact prefixes on partial failure. Never fall back to a
                // managed wrapper, a shared getter, or a per-frame override.
                _hooksReady = false;
                TryUnpatch(harmony, request, requestPrefix);
                TryUnpatch(harmony, transition, transitionPrefix);
                _hookStatus = "Tweak guard unavailable on this build. Game behavior is unchanged; check the log.";
                Log.Warning($"[TrickTweakGuard] {_hookStatus} {ex}");
            }
        }

        private static unsafe MethodInfo ValidateNativeTarget(string methodName, long expectedRva, ProcessModule gameModule)
        {
            MethodInfo target = AccessTools.DeclaredMethod(typeof(TrickControllerV2), methodName);
            if (target == null || target.IsStatic || target.IsGenericMethod ||
                target.ReturnType != typeof(void) || target.GetParameters().Length != 0)
                throw new InvalidOperationException($"Unexpected signature for {methodName}.");
            FieldInfo methodField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(target);
            if (methodField == null)
                throw new InvalidOperationException($"No native method metadata for {methodName}.");
            IntPtr methodInfo = (IntPtr)methodField.GetValue(null);
            if (methodInfo == IntPtr.Zero)
                throw new InvalidOperationException($"Null native method metadata for {methodName}.");
            IntPtr nativeAddress = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfo).MethodPointer;
            long actualRva = nativeAddress.ToInt64() - gameModule.BaseAddress.ToInt64();
            if (actualRva != expectedRva || actualRva < 0 || actualRva >= gameModule.ModuleMemorySize)
                throw new InvalidOperationException($"Refusing {methodName}: RVA 0x{actualRva:X} does not match audited 0x{expectedRva:X}.");
            var patcher = PatchManager.GetMethodPatcher(target);
            if (patcher?.GetType().FullName != "Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher")
                throw new InvalidOperationException($"Native patcher not ready for {methodName}: {patcher?.GetType().FullName ?? "none"}.");
            Log.Msg($"[TrickTweakGuard] Verified unique native target TrickControllerV2.{methodName} at RVA 0x{actualRva:X}.");
            return target;
        }

        private static void TryUnpatch(global::HarmonyLib.Harmony harmony, MethodInfo target, MethodInfo prefix)
        {
            if (target == null || prefix == null)
                return;
            try { harmony.Unpatch(target, prefix); }
            catch (Exception ex) { Log.Warning($"[TrickTweakGuard] Could not remove {target.Name} prefix: {ex.Message}. Restart the game."); }
        }

        private static bool Enabled => _hooksReady && _gameplayScene &&
            RemoteKillSwitched.isModEnabled && Config.trickAnimationDebugSettings?.disableTweaking == true;

        private static bool IsBlockedController(TrickControllerV2 controller) =>
            Enabled && _localControllerPointer != IntPtr.Zero &&
            controller is not null && controller.Pointer == _localControllerPointer;

        private static bool RequestTweakPrefix(TrickControllerV2 __instance)
        {
            if (!IsBlockedController(__instance))
                return true;
            __instance._requestedTweak = false;
            LogBlocked(ref _blockedRequestLogs, "Blocked local RequestTweak input.");
            return false;
        }

        private static bool DirectTweakPrefix(TrickControllerV2 __instance)
        {
            if (!IsBlockedController(__instance))
                return true;
            __instance._requestedTweak = false;
            LogBlocked(ref _blockedDirectLogs, "Blocked local Tweak transition (manual/auto safety gate).");
            return false;
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            RestoreAndClear("scene changed");
            _gameplayScene = gameplayScene;
            // Wait for the existing local-player-spawn event. No whole-scene scans.
        }

        public static void OnLocalPlayerSpawned()
        {
            RestoreAndClear("local player spawned");
            BindLocalController();
            ApplyToLocalController("local player spawned");
        }

        public static void ApplySettingChange()
        {
            _blockedRequestLogs = 0;
            _blockedDirectLogs = 0;
            if (!Enabled)
            {
                RestoreAutoTweak("setting disabled or unavailable");
                return;
            }
            if (_localController == null)
                BindLocalController();
            ApplyToLocalController("setting enabled");
        }

        private static void BindLocalController()
        {
            if (!_hooksReady || !_gameplayScene)
                return;
            try
            {
                if (Memory.rMbCharacter != null)
                    _localController = Memory.rMbCharacter.GetComponentInChildren<TrickControllerV2>(true);
                _localControllerPointer = _localController == null ? IntPtr.Zero : _localController.Pointer;
                if (_localControllerPointer == IntPtr.Zero)
                    Log.Warning("[TrickTweakGuard] No local V2 trick controller found. No fallback/global hooks will be used.");
                else
                    Log.Msg($"[TrickTweakGuard] Bound local V2 controller {_localController.GetInstanceID()}; remote riders are untouched.");
            }
            catch (Exception ex)
            {
                _localController = null;
                _localControllerPointer = IntPtr.Zero;
                Log.Warning($"[TrickTweakGuard] Local controller binding failed: {ex.Message}");
            }
        }

        private static void ApplyToLocalController(string reason)
        {
            if (!Enabled || _localController == null || _capturedAutoTweak)
                return;
            try
            {
                _originalAutoTweak = _localController.autoTweak;
                _capturedAutoTweak = true;
                _localController.autoTweak = false;
                _localController._requestedTweak = false;
                Log.Msg($"[TrickTweakGuard] Disabled local auto-tweak once ({reason}); captured autoTweak={_originalAutoTweak}.");
            }
            catch (Exception ex) { Log.Warning($"[TrickTweakGuard] Could not apply local auto-tweak setting: {ex.Message}"); }
        }

        private static void RestoreAutoTweak(string reason)
        {
            if (!_capturedAutoTweak)
                return;
            try
            {
                if (_localController != null)
                {
                    _localController.autoTweak = _originalAutoTweak;
                    Log.Msg($"[TrickTweakGuard] Restored local autoTweak={_originalAutoTweak} ({reason}).");
                }
            }
            catch (Exception ex) { Log.Warning($"[TrickTweakGuard] Could not restore local auto-tweak: {ex.Message}"); }
            _capturedAutoTweak = false;
        }

        private static void RestoreAndClear(string reason)
        {
            RestoreAutoTweak(reason);
            _localController = null;
            _localControllerPointer = IntPtr.Zero;
            _blockedRequestLogs = 0;
            _blockedDirectLogs = 0;
        }

        public static void Cleanup()
        {
            _gameplayScene = false;
            RestoreAndClear("mod cleanup");
        }

        private static void LogBlocked(ref int count, string message)
        {
            if (count >= BlockLogLimit)
                return;
            count++;
            Log.Msg($"[TrickTweakGuard] {message}{(count == BlockLogLimit ? " Further messages suppressed." : string.Empty)}");
        }
    }
}
