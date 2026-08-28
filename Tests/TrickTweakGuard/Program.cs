using System;
using System.Collections.Generic;
using System.Reflection;
using rowemod.Mods;
using rowemod.Utils;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;

// Executes the real guard source against minimal doubles. This verifies guard logic,
// NOT the native trampoline, Unity object lifetimes, or in-game animation behavior.
internal static class Program
{
    private static int assertions;
    private static readonly Type Guard = typeof(TrickTweakGuard);
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        assertions++;
    }
    private static void SetField(string name, object value) =>
        Guard.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    private static bool Prefix(string name, TrickControllerV2 controller) =>
        (bool)Guard.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { controller });
    private static bool Request(TrickControllerV2 c) => Prefix("RequestTweakPrefix", c);
    private static bool Tweak(TrickControllerV2 c) => Prefix("DirectTweakPrefix", c);

    private static void Main()
    {
        var local = new TrickControllerV2(1, true);
        var remote = new TrickControllerV2(2, true);
        Memory.rMbCharacter = new FakeCharacter(local);
        rowemod.Config.trickAnimationDebugSettings.disableTweaking = true;
        TrickTweakGuard.OnSceneInitialized(true);
        TrickTweakGuard.OnLocalPlayerSpawned();
        Check(Request(local) && Tweak(local), "unknown/unverified build leaves calls unchanged");
        Check(local.Writes == 0, "unknown build never changes autoTweak");

        SetField("_hooksReady", true);
        Check(Request(local) && Tweak(local), "no bound local rider means no blocking");
        TrickTweakGuard.OnLocalPlayerSpawned();
        Check(!local.autoTweak && local.Writes == 1, "local spawn captures and disables autoTweak once");
        Check(remote.autoTweak && remote.Writes == 0, "remote autoTweak untouched");
        Check(Request(remote) && Tweak(remote), "remote prefixes pass through");
        Check(Request(null) && Tweak(null), "null controller passes through");
        local._requestedTweak = true;
        Check(!Request(local) && !local._requestedTweak, "local request is blocked and pending flag cleared");
        local._requestedTweak = true;
        Check(!Tweak(local) && !local._requestedTweak, "local direct/auto transition blocked");
        for (int i = 0; i < 100; i++) { Request(local); Tweak(local); }
        Check(local.Writes == 1, "prefixes never repeatedly force autoTweak");
        Check(Log.Entries.FindAll(x => x.Contains("Blocked local RequestTweak")).Count == 3, "request logs capped");
        Check(Log.Entries.FindAll(x => x.Contains("Blocked local Tweak transition")).Count == 3, "transition logs capped");
        TrickTweakGuard.ApplySettingChange();
        Check(local.Writes == 1, "repeated enabled setting does not overwrite capture");

        rowemod.Config.trickAnimationDebugSettings.disableTweaking = false;
        TrickTweakGuard.ApplySettingChange();
        Check(local.autoTweak && local.Writes == 2, "off restores original true");
        Check(Request(local) && Tweak(local), "off restores both original calls");
        TrickTweakGuard.ApplySettingChange();
        Check(local.Writes == 2, "repeated off does not write again");

        rowemod.Config.trickAnimationDebugSettings.disableTweaking = true;
        TrickTweakGuard.ApplySettingChange();
        Check(!local.autoTweak, "re-enable captures again");
        TrickTweakGuard.OnSceneInitialized(false);
        Check(local.autoTweak && Request(local) && Tweak(local), "scene exit restores and stops blocking");

        var originalFalse = new TrickControllerV2(3, false);
        Memory.rMbCharacter = new FakeCharacter(originalFalse);
        TrickTweakGuard.OnSceneInitialized(true);
        TrickTweakGuard.OnLocalPlayerSpawned();
        Check(Request(local), "old rider excluded after rebinding");
        Check(!Tweak(originalFalse), "replacement rider is guarded");
        RemoteKillSwitched.isModEnabled = false;
        Check(Request(originalFalse) && Tweak(originalFalse), "mod disabled passes through");
        RemoteKillSwitched.isModEnabled = true;
        TrickTweakGuard.Cleanup();
        Check(!originalFalse.autoTweak, "cleanup restores original false, not an invented default");
        Check(Request(originalFalse) && Tweak(originalFalse), "cleanup clears local identity");

        Memory.rMbCharacter = null;
        TrickTweakGuard.OnSceneInitialized(true);
        TrickTweakGuard.OnLocalPlayerSpawned();
        Check(Request(local) && Tweak(remote), "missing player has no global fallback");
        TestNoseManualGuard();
        Console.WriteLine($"PASS: {assertions} tweak/nose-manual guard assertions. Native/in-game behavior still requires runtime testing.");
    }

    private static int Variant(VehicleAnimationInputHandler handler, int value, string parameter = "Nosey Variant")
    {
        var args = new object[] { handler, parameter, value };
        typeof(HangFiveControl).GetMethod("SetIntegerPrefix", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        return (int)args[2];
    }

    private static void TestNoseManualGuard()
    {
        var local = new VehicleAnimationInputHandler(101);
        var remote = new VehicleAnimationInputHandler(102);
        Memory.rMbCharacter = new FakeCharacter(null, new VehicleController(local));
        rowemod.Config.physics.hangFiveMode = 1;
        HangFiveControl.OnSceneInitialized(true);
        HangFiveControl.OnLocalPlayerSpawned();
        Check(Variant(local, 1) == 1, "unaudited nose-manual hook leaves native value unchanged");
        typeof(HangFiveControl).GetField("_hooksReady", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, true);
        Check(Variant(local, 1) == 1, "unbound nose-manual handler passes through");
        HangFiveControl.OnLocalPlayerSpawned();
        int searches = VehicleController.Searches;
        Check(Variant(local, 1) == 0 && Variant(local, 2) == 0, "disabled suppresses both bumper variants");
        Check(Variant(local, 0) == 0 && Variant(local, 3) == 0, "disabled keeps base pose and suppresses other variants");
        Check(Variant(remote, 2) == 2, "remote bike untouched");
        Check(Variant(null, 1) == 1, "null animation handler untouched");
        Check(Variant(local, 2, "Hop") == 2, "other integer animation parameters unchanged");
        Check(Variant(local, 2, null) == 2 && Variant(local, 2, "nosey variant") == 2, "exact parameter name required");
        Check(Variant(new VehicleAnimationInputHandler(101), 2) == 0, "local identity uses native pointer, not wrapper identity");
        for (int i = 0; i < 100; i++) Variant(local, 1 + (i % 2));
        Check(Log.Entries.FindAll(x => x.Contains("Local Nosey Variant")).Count == 6, "nose-manual diagnostics capped");
        Check(VehicleController.Searches == searches, "native prefix never searches for components");
        Check(local.Writes == 0, "no extra SetInteger writes or forced frame updates");

        rowemod.Config.physics.hangFiveMode = 0;
        HangFiveControl.ApplySettingChange();
        Check(Variant(local, 1) == 1 && Variant(local, 2) == 2, "Game mode restores original incoming values");
        rowemod.Config.physics.hangFiveMode = 2;
        HangFiveControl.ApplySettingChange();
        Check(Variant(local, 1) == 2 && Variant(local, 2) == 1, "Swap Sides reverses both variants");
        Check(Variant(local, 0) == 0 && Variant(local, 3) == 3, "Swap Sides leaves base and unknown variants alone");
        Check(Variant(local, 1) == 2 && Variant(local, 1) == 2, "held input is not repeatedly inverted");
        Check(Variant(remote, 1) == 1, "Swap Sides never swaps remote input");
        rowemod.Config.physics.hangFiveMode = 99;
        Check(Variant(local, 1) == 1, "invalid mode safely falls back to Game behavior");
        rowemod.Config.physics.hangFiveMode = 1;
        Check(Variant(local, 2) == 0, "config/preset change works without a frame enforcement loop");
        RemoteKillSwitched.isModEnabled = false;
        Check(Variant(local, 2) == 2, "disabled mod leaves variants alone");
        RemoteKillSwitched.isModEnabled = true;
        HangFiveControl.OnSceneInitialized(false);
        HangFiveControl.OnLocalPlayerSpawned();
        Check(Variant(local, 1) == 1, "main menu cannot bind or suppress a bike");

        var replacement = new VehicleAnimationInputHandler(103);
        Memory.rMbCharacter = new FakeCharacter(null, new VehicleController(replacement));
        HangFiveControl.OnSceneInitialized(true);
        Check(Variant(local, 1) == 1, "map change clears stale local identity");
        HangFiveControl.OnLocalPlayerSpawned();
        Check(Variant(replacement, 1) == 0 && Variant(local, 1) == 1, "respawn binds new handler only");
        HangFiveControl.Cleanup();
        Check(Variant(replacement, 1) == 1, "cleanup stops routing");
        Check(local.Writes == 0 && replacement.Writes == 0, "toggle/respawn/cleanup never write animator state directly");
        Memory.rMbCharacter = null;
        HangFiveControl.OnSceneInitialized(true);
        HangFiveControl.OnLocalPlayerSpawned();
        Check(Variant(replacement, 2) == 2, "missing rider has no global fallback");
        Memory.rMbCharacter = new FakeCharacter(null, new VehicleController(replacement));
        HangFiveControl.ApplySettingChange();
        Check(Variant(replacement, 2) == 0, "explicit setting event can retry a missing local binding");
        Memory.rMbCharacter = new FakeCharacter(null, new VehicleController(null));
        HangFiveControl.OnLocalPlayerSpawned();
        Check(Variant(replacement, 2) == 2, "missing handler on replacement clears old identity");
        var prefix = typeof(HangFiveControl).GetMethod("SetIntegerPrefix", BindingFlags.NonPublic | BindingFlags.Static);
        Check(prefix.GetParameters()[1].Name == "__0" && prefix.GetParameters()[2].Name == "__1" &&
            prefix.GetParameters()[2].ParameterType == typeof(int).MakeByRefType(), "indexed by-ref argument avoids blank IL2CPP parameter-name failure");
        HangFiveControl.Cleanup();
    }
}

namespace rowemod
{
    internal static class Config
    {
        public static Settings trickAnimationDebugSettings = new Settings();
        public static PhysicsSettings physics = new PhysicsSettings();
    }
    internal sealed class PhysicsSettings { public int hangFiveMode; }
    internal sealed class Settings { public bool disableTweaking; }
}
namespace rowemod.Mods
{
    internal static class RemoteKillSwitched { public static bool isModEnabled = true; }
}
namespace rowemod.Utils
{
    internal static class Memory { public static FakeCharacter rMbCharacter; }
    internal sealed class FakeCharacter
    {
        private readonly TrickControllerV2 controller;
        private readonly VehicleController vehicle;
        public FakeCharacter(TrickControllerV2 value, VehicleController bike = null) { controller = value; vehicle = bike; }
        public T GetComponentInChildren<T>(bool includeInactive) where T : class => controller as T ?? vehicle as T;
    }
    internal static class Log
    {
        public static readonly List<string> Entries = new List<string>();
        public static void Msg(string message) => Entries.Add(message);
        public static void Warning(string message) => Entries.Add(message);
    }
}
namespace Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2
{
    internal sealed class TrickControllerV2
    {
        private bool auto;
        public int Writes;
        public IntPtr Pointer { get; }
        public bool autoTweak { get => auto; set { auto = value; Writes++; } }
        public bool _requestedTweak;
        public TrickControllerV2(int id, bool original) { Pointer = new IntPtr(id); auto = original; }
        public int GetInstanceID() => Pointer.ToInt32();
        public void RequestTweak() { }
        public void Tweak() { }
    }
}
namespace HarmonyLib
{
    internal sealed class Harmony { public void Patch(MethodInfo method, HarmonyMethod prefix) { } public void Unpatch(MethodInfo method, MethodInfo prefix) { } }
    internal sealed class HarmonyMethod { public HarmonyMethod(MethodInfo method) { } }
    internal static class AccessTools
    {
        public static MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null) => parameters == null
            ? type.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            : type.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, parameters, null);
    }
}
namespace Il2CppMashBox.BMX_Physics_Development
{
    internal sealed class VehicleAnimationInputHandler
    {
        public IntPtr Pointer { get; }
        public int Writes;
        public VehicleAnimationInputHandler(int id) { Pointer = new IntPtr(id); }
        public int GetInstanceID() => Pointer.ToInt32();
        public void SetInteger(string name, int value) { Writes++; }
    }
    internal sealed class VehicleController
    {
        private readonly VehicleAnimationInputHandler handler;
        public static int Searches;
        public VehicleController(VehicleAnimationInputHandler animation) { handler = animation; }
        public T GetComponent<T>() where T : class { Searches++; return handler as T; }
        public T GetComponentInChildren<T>(bool includeInactive) where T : class { Searches++; return handler as T; }
        public T GetComponentInParent<T>() where T : class { Searches++; return handler as T; }
    }
}
namespace HarmonyLib.Public.Patching
{
    internal static class PatchManager { public static object GetMethodPatcher(MethodInfo method) => null; }
}
namespace Il2CppInterop.Common
{
    internal static class Il2CppInteropUtils { public static FieldInfo GetIl2CppMethodInfoPointerFieldForGeneratedMethod(MethodInfo method) => null; }
}
namespace Il2CppInterop.Runtime.Runtime
{
    internal struct Il2CppMethodInfo { }
    internal sealed class NativeMethodInfo { public IntPtr MethodPointer => IntPtr.Zero; }
    internal static class UnityVersionHandler { public static unsafe NativeMethodInfo Wrap(Il2CppMethodInfo* pointer) => throw new NotSupportedException("Native calls are not simulated."); }
}
