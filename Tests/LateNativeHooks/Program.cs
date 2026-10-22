using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using rowemod.Mods;
using rowemod.Utils;
#if UPDATED_INTEROP && !MISSING_EQUIP_COROUTINE
using TestEquipCoroutine = Il2CppMashBox.Character.Scripts.EquipSlot._EnumEquip_d__111;
#elif !MISSING_EQUIP_COROUTINE
using TestEquipCoroutine = Il2CppMashBox.Character.Scripts.EquipSlot._EnumEquip_d__80;
#endif

// Actual registrar with fake patching/validation. Tests transactions and coverage,
// not a native detour or Unity gameplay. Compiled real signatures are tested separately.
internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        checks++;
    }
    private static void Reset()
    {
        typeof(LateNativeHooks).GetField("_attempted", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
        ((HashSet<string>)typeof(LateNativeHooks).GetField("ReadyGroups", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Clear();
        NativeHookSafety.FailMethod = null;
        NativeHookSafety.RejectAll = false;
        NativeHookSafety.Validated.Clear();
        Log.Lines.Clear();
    }
    private static bool IsIk(MethodInfo method) => method.DeclaringType.Name is "HumanIK" or "UnityIKLimb" or "VehicleFootPedalAnimationRig";
    private static void Main()
    {
        CheckBuildProfiles();
#if MISSING_EQUIP_COROUTINE
        Reset();
        var harmony = new HarmonyLib.Harmony();
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 10, "missing coroutine skips only the two hair hooks");
        Check(!harmony.Calls.Any(x => x.Name == "ApplyRenderPolicy"), "missing coroutine leaves hair group untouched");
        Check(LateNativeHooks.ManualIkReady, "IK still initializes after missing coroutine");
        Check(harmony.Calls.Any(x => x.Name == "RPC_FireBullet"), "last group still initializes after missing coroutine");
        Check(Log.Lines.Count(x => x.Contains("group skipped")) == 1, "only unavailable group reported");
        Check(Log.Lines.Any(x => x.Contains("Missing generated wrapper")), "actionable missing wrapper diagnostic");
        Check(harmony.Removed.Count == 0, "missing wrapper never triggers unpatch");
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 10, "missing wrapper does not cause repeated registration");
#else
        Reset();
        var harmony = new HarmonyLib.Harmony();
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 12, "all late hooks registered including hair policy and native completion");
        Check(NativeHookSafety.Validated.Count == 12, "every target validated first");
        Check(LateNativeHooks.ManualIkReady, "IK ready only after all three targets install");
        Check(harmony.Calls.Select(x => x.DeclaringType.FullName + x.Name).Distinct().Count() == 12, "no duplicate targets");
        Check(harmony.Calls.Single(x => x.Name == "Tick").GetParameters().Length == 2, "exact landing drive overload selected");
        Check(harmony.Calls.Single(x => x.Name == "SetInputData").GetParameters().Length == 2, "exact grind overload selected");
#if UPDATED_INTEROP
        Check(harmony.Calls.Single(x => x.Name == "LocalSpawnBullet").GetParameters().Length == 4, "updated local drone signature selected");
        Check(harmony.Calls.Single(x => x.Name == "RPC_FireBullet").GetParameters().Length == 5, "updated drone RPC signature selected");
        Check(harmony.Calls.Single(x => x.Name == "MoveNext").DeclaringType.Name == "_EnumEquip_d__111", "updated equip coroutine resolved");
#else
        Check(harmony.Calls.Single(x => x.Name == "LocalSpawnBullet").GetParameters().Length == 3, "original local drone signature preserved");
        Check(harmony.Calls.Single(x => x.Name == "RPC_FireBullet").GetParameters().Length == 3, "original drone RPC signature preserved");
#endif
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 12 && NativeHookSafety.Validated.Count == 12, "registration attempted only once");

        Reset();
        harmony = new HarmonyLib.Harmony();
        NativeHookSafety.FailMethod = "UpdateIK";
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 9 && !harmony.Calls.Any(IsIk), "entire IK group validated before any patch");
        Check(!LateNativeHooks.ManualIkReady, "failed validation leaves IK inactive");
        Check(harmony.Removed.Count == 0, "validation rejection never invokes Unpatch or creates a patcher");
        Check(harmony.Calls.Any(x => x.Name == "RPC_FireBullet"), "later unrelated groups still install");

        Reset();
        harmony = new HarmonyLib.Harmony { FailMethod = "UpdateIK" };
        LateNativeHooks.Install(harmony);
        Check(!LateNativeHooks.ManualIkReady, "partial patch failure does not mark IK ready");
        Check(!harmony.Active.Any(x => IsIk(x.Target)), "partial group rolled back completely");
        Check(harmony.Removed.All(x => x.Patch.DeclaringType.Namespace == "rowemod.Mods"), "rollback identifies exact owned patch methods");
        Check(harmony.Active.Any(x => x.Target.Name == "StartGame") && harmony.Active.Any(x => x.Target.Name == "RPC_FireBullet"), "rollback preserves earlier and later feature groups");
        Check(harmony.ForeignPatchPresent, "rollback never unpatches foreign owners");

        Reset();
        harmony = new HarmonyLib.Harmony();
        NativeHookSafety.RejectAll = true;
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 0 && !LateNativeHooks.ManualIkReady, "unsupported build or native backend installs nothing");
        Check(harmony.Removed.Count == 0, "unsupported build never calls patch or unpatch");
        Check(Log.Lines.Count(x => x.Contains("group skipped")) == 7, "failure is isolated and reported per group");
        LateNativeHooks.Install(harmony);
        Check(Log.Lines.Count(x => x.Contains("group skipped")) == 7, "failed bootstrap is not retried on frame/update calls");
        CheckHairPolicy();
        Reset();
        harmony = new HarmonyLib.Harmony();
        NativeHookSafety.FailMethod = "MoveNext";
        LateNativeHooks.Install(harmony);
        Check(!harmony.Calls.Any(x => x.Name == "ApplyRenderPolicy"), "hair visibility does not install without reliable completion hook");
#endif
        Console.WriteLine($"PASS: {checks} native-registration assertions. No native/game runtime simulated.");
    }

    private static void CheckBuildProfiles()
    {
        Check(NativeHookBuilds.IsSupported(NativeHookBuilds.OriginalHash), "original game hash retained");
        Check(NativeHookBuilds.IsSupported(NativeHookBuilds.UpdatedHash), "updated game hash supported");
        Check(NativeHookBuilds.IsSupported(NativeHookBuilds.October21Hash), "October 21 game hash supported");
        Check(!NativeHookBuilds.IsSupported(null) && !NativeHookBuilds.IsSupported("unknown"), "unknown game hashes rejected");
        Check(NativeHookBuilds.ResolveRva(NativeHookBuilds.OriginalHash, 0x7F15C0) == 0x7F15C0, "original completion address unchanged");
        Check(NativeHookBuilds.ResolveRva(NativeHookBuilds.UpdatedHash, 0x7F15C0) == 0x87B630, "updated completion address selected by game hash");
        Check(NativeHookBuilds.ResolveRva(NativeHookBuilds.October21Hash, 0x7F15C0) == 0x890E00, "October 21 completion address selected by game hash");
        Check(NativeHookBuilds.ResolveRva(NativeHookBuilds.October21Hash, 0xD53AA0) == 0xF36250, "October 21 networking address selected by game hash");
        ExpectRejected("unknown", 0x7F15C0, "known target cannot enable an unknown game build");
        ExpectRejected(NativeHookBuilds.UpdatedHash, 0x66E640, "shared original getter cannot become an approved target");
        ExpectRejected(NativeHookBuilds.UpdatedHash, 0x6AE680, "shared updated getter cannot become an approved target");
        ExpectRejected(NativeHookBuilds.October21Hash, 0x6BE680, "shared October 21 getter cannot become an approved target");
        ExpectRejected(NativeHookBuilds.October21Hash, 0x87B630, "previous completion address cannot be used as target identity on latest build");
        ExpectRejected(NativeHookBuilds.OriginalHash, 0x87B630, "updated address cannot be passed as an original target identity");
    }

    private static void ExpectRejected(string hash, long rva, string label)
    {
        bool rejected = false;
        try { NativeHookBuilds.ResolveRva(hash, rva); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, label);
    }

#if !MISSING_EQUIP_COROUTINE
    private static void CheckHairPolicy()
    {
        var meshA = new UnityEngine.Renderer();
        var meshB = new UnityEngine.Renderer();
        var item = new UnityEngine.GameObject { Renderers = new[] { meshA, meshB } };
        var slot = new Il2CppMashBox.Character.Scripts.EquipSlot { _renderDisabled = true, Item = item };
        Custom.Eligible = true;
        CustomHairRenderPolicyPatch.Prefix(slot);
        Check(!slot._renderDisabled, "hide flag masked before native renderer deactivation");
        Check(!meshA.gameObject.Active && !meshB.gameObject.Active, "prefix leaves renderer writes to native policy");
        slot._renderDisabled = true; // later hat/bust recalculation writes its source flags again
        CustomHairRenderPolicyPatch.Prefix(slot);
        Check(!slot._renderDisabled, "later clothing recalculation masked again");
        slot._renderDisabled = true;
        Custom.Eligible = false;
        CustomHairRenderPolicyPatch.Prefix(slot);
        Check(slot._renderDisabled, "ineligible slot retains native hide flag (remote, native or hidden hair)");
        Custom.Eligible = true;
        slot._renderDisabled = false;
        CustomHairRenderPolicyPatch.Prefix(slot);
        Check(!slot._renderDisabled, "unsuppressed item stays unsuppressed");
        CustomHairRenderPolicyPatch.Prefix(null);
        var native = new TestEquipCoroutine { __4__this = slot, go = item, Pointer = new IntPtr(123) };
        Custom.CompletionCount = 0;
        Custom.StartCount = 0;
        CustomEquipCompletionPatch.Prepare(native.GetType());
        CustomEquipCompletionPatch.Prefix(native);
        Check(Custom.StartCount == 1 && Custom.StartedSource == item && Custom.StartedCoroutine == native.Pointer,
            "initial MoveNext captures original source and native coroutine identity before mutation");
        native.__1__state = 1;
        native.go = new UnityEngine.GameObject(); // updated game replaces the source with a clone
        CustomEquipCompletionPatch.Prefix(native);
        Check(Custom.StartCount == 1 && Custom.StartedSource == item, "resumed coroutine cannot rebind a request to the clone");
        CustomEquipCompletionPatch.Postfix(native, true);
        Check(Custom.CompletionCount == 0, "yielding native coroutine is not completed");
        CustomEquipCompletionPatch.Postfix(native, false);
        Check(Custom.CompletionCount == 1 && Custom.CompletedSlot == slot && Custom.CompletedCoroutine == Custom.StartedCoroutine,
            "completion follows the captured coroutine even after native source cloning");
        CustomEquipCompletionPatch.Prefix(null);
        CustomEquipCompletionPatch.Postfix(null, false);
    }
#endif
}

namespace rowemod.Mods
{
    internal static class Custom
    {
        internal static bool Eligible;
        internal static int CompletionCount;
        internal static int StartCount;
        internal static Il2CppMashBox.Character.Scripts.EquipSlot CompletedSlot;
        internal static UnityEngine.GameObject StartedSource;
        internal static IntPtr StartedCoroutine, CompletedCoroutine;
        internal static bool ShouldKeepCustomHairVisible(Il2CppMashBox.Character.Scripts.EquipSlot slot) => Eligible;
        internal static void OnNativeEquipStarted(Il2CppMashBox.Character.Scripts.EquipSlot slot, UnityEngine.GameObject source, IntPtr coroutine)
        {
            StartCount++;
            StartedSource = source;
            StartedCoroutine = coroutine;
        }
        internal static void OnNativeEquipCompleted(Il2CppMashBox.Character.Scripts.EquipSlot slot, IntPtr coroutine)
        {
            CompletionCount++;
            CompletedSlot = slot;
            CompletedCoroutine = coroutine;
        }
    }
    internal static class NativeHookSafety
    {
        internal static bool RejectAll;
        internal static string FailMethod;
        internal static readonly List<MethodInfo> Validated = new();
        internal static void Validate(MethodInfo target, long rva)
        {
            if (target == null || rva <= 0 || RejectAll || target.Name == FailMethod) throw new InvalidOperationException("Refused native target");
            Validated.Add(target);
        }
    }
    internal static class HostPlayerLimitHostSetupPatch { private static void Prefix() { } }
    internal static class HostPlayerLimitStartGamePatch { private static void Prefix() { } }
    internal static class PlayerUserNameTargetSpawnPatch { private static void Postfix() { } }
    internal static class ManualIkHumanPatch { private static void Prefix() { } private static void Postfix() { } }
    internal static class ManualIkNativeLimbPatch { private static void Prefix() { } }
    internal static class ManualIkPedalRigPatch { private static void Prefix() { } }
    internal static class BikeGrindPoserInputRemapPatch { private static void Prefix() { } private static void Postfix() { } }
    internal static class OnePointOhQuaternionDrivePatch { private static void Prefix() { } }
    internal static class DroneControllerLocalSpawnBulletPatch { private static void Prefix() { } private static void Postfix() { } }
    internal static class DroneControllerRpcFireBulletPatch { private static void Prefix() { } }
}
namespace rowemod.Utils
{
    internal static class Log
    {
        internal static readonly List<string> Lines = new();
        internal static void Msg(string message) => Lines.Add(message);
        internal static void Warning(string message) => Lines.Add(message);
    }
}
namespace HarmonyLib
{
    internal sealed class HarmonyMethod { internal readonly MethodInfo Method; public HarmonyMethod(MethodInfo method) { Method = method; } }
    internal sealed class Harmony
    {
        internal string FailMethod;
        internal bool ForeignPatchPresent = true;
        internal readonly List<MethodInfo> Calls = new();
        internal readonly List<(MethodInfo Target, MethodInfo Patch)> Active = new();
        internal readonly List<(MethodInfo Target, MethodInfo Patch)> Removed = new();
        public void Patch(MethodInfo target, HarmonyMethod prefix, HarmonyMethod postfix)
        {
            if (!NativeHookSafety.Validated.Contains(target)) throw new Exception("Patch ran before validation");
            Calls.Add(target);
            if (prefix != null) Active.Add((target, prefix.Method));
            if (postfix != null) Active.Add((target, postfix.Method));
            if (target.Name == FailMethod) throw new Exception("Simulated partial detour install failure");
        }
        public void Unpatch(MethodInfo target, MethodInfo patch)
        {
            Removed.Add((target, patch));
            Active.RemoveAll(x => x.Target == target && x.Patch == patch);
        }
    }
    internal static class AccessTools
    {
        public static MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null) => parameters == null
            ? type.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            : type.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, parameters, null);
    }
}
namespace UnityEngine
{
    internal struct Vector3 { }
    internal struct Quaternion { }
    internal class Rigidbody { }
    internal class Renderer { internal GameObject gameObject = new(); }
    internal class GameObject
    {
        internal bool Active, IncludedInactive;
        internal Renderer[] Renderers = Array.Empty<Renderer>();
        internal void SetActive(bool value) => Active = value;
        internal T[] GetComponentsInChildren<T>(bool includeInactive)
        {
            IncludedInactive = includeInactive;
            return Renderers.Cast<T>().ToArray();
        }
    }
}
namespace Il2CppMashBox.Character.Scripts
{
    internal class EquipSlot
    {
        internal bool _renderDisabled;
        internal UnityEngine.GameObject Item;
        internal UnityEngine.GameObject GetEquipItem() => Item;
        public void ApplyRenderPolicy() { }
#if !MISSING_EQUIP_COROUTINE
#if UPDATED_INTEROP
        internal class _EnumEquip_d__111 : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
#else
        internal class _EnumEquip_d__80 : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
#endif
        {
            public EquipSlot __4__this { get; set; }
            public UnityEngine.GameObject go { get; set; }
            public int __1__state { get; set; }
            public bool MoveNext() => false;
        }
#endif
    }
}
namespace Il2CppInterop.Runtime.InteropTypes
{
    internal class Il2CppObjectBase { public IntPtr Pointer { get; set; } }
}
namespace Il2CppMashBoxBridge.Common.Interfaces { internal enum HookGrind { Null } }
namespace Il2CppMashBox.Netorking { internal class FusionBootstrap { public void DrawServerBrowserHostMapControls(float value) { } } }
namespace Il2CppFusion
{
    internal class StartGameArgs { }
    internal class NetworkRunner { public object StartGame(StartGameArgs args) => null; }
}
namespace Il2CppMashBox.Addons.NetworkingFusion { internal class NetworkPlayer { public void Spawned() { } } }
namespace Il2CppMashBox.Core.Runtime.InverseKinematics
{
    internal class HumanIK { public void OnAnimatorIK(int layer) { } }
    internal class UnityIKLimb { public void UpdateIK(bool mirror) { } }
}
namespace Il2CppMashBox.BMX_Physics_Development
{
    internal class VehicleFootPedalAnimationRig { public void LateUpdate() { } }
    internal class BikeGrindPoser { public void SetInputData(int slice, Il2CppMashBoxBridge.Common.Interfaces.HookGrind hook) { } }
    internal class QuaternionPDDrive
    {
        public void Tick(UnityEngine.Rigidbody body) { }
        public void Tick(UnityEngine.Rigidbody body, UnityEngine.Quaternion target) { }
    }
}
namespace Il2CppMashBox.Addons.ProtoDrone
{
    internal class DroneController
    {
#if UPDATED_INTEROP
        public void LocalSpawnBullet(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.Vector3 velocity, float lifetime) { }
        public void RPC_FireBullet(int sequence, double time, UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.Vector3 velocity) { }
#else
        public void LocalSpawnBullet(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.Vector3 velocity) { }
        public void RPC_FireBullet(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.Vector3 velocity) { }
#endif
    }
}
