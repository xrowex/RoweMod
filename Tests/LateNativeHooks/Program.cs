using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using rowemod.Mods;
using rowemod.Utils;

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
        Reset();
        var harmony = new HarmonyLib.Harmony();
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 10, "all ten old early hooks registered");
        Check(NativeHookSafety.Validated.Count == 10, "every target validated first");
        Check(LateNativeHooks.ManualIkReady, "IK ready only after all three targets install");
        Check(harmony.Calls.Select(x => x.DeclaringType.FullName + x.Name).Distinct().Count() == 10, "no duplicate targets");
        Check(harmony.Calls.Single(x => x.Name == "Tick").GetParameters().Length == 2, "exact landing drive overload selected");
        Check(harmony.Calls.Single(x => x.Name == "SetInputData").GetParameters().Length == 2, "exact grind overload selected");
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 10 && NativeHookSafety.Validated.Count == 10, "registration attempted only once");

        Reset();
        harmony = new HarmonyLib.Harmony();
        NativeHookSafety.FailMethod = "UpdateIK";
        LateNativeHooks.Install(harmony);
        Check(harmony.Calls.Count == 7 && !harmony.Calls.Any(IsIk), "entire IK group validated before any patch");
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
        Check(Log.Lines.Count(x => x.Contains("group skipped")) == 6, "failure is isolated and reported per group");
        LateNativeHooks.Install(harmony);
        Check(Log.Lines.Count(x => x.Contains("group skipped")) == 6, "failed bootstrap is not retried on frame/update calls");
        Console.WriteLine($"PASS: {checks} native-registration assertions. No native/game runtime simulated.");
    }
}

namespace rowemod.Mods
{
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
namespace UnityEngine { internal struct Vector3 { } internal struct Quaternion { } internal class Rigidbody { } }
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
        public void LocalSpawnBullet(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.Vector3 velocity) { }
        public void RPC_FireBullet(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.Vector3 velocity) { }
    }
}
