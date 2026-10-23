using System;
using System.Collections;
using System.Collections.Generic;
using rowemod;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;

internal static class Program
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static GameObject Setup()
    {
        var root = new GameObject("Local rider");
        Memory.gamePlayer = new GameObject("Spawn child") { Root = root };
        rowemod.Main.playableSceneLoaded = true;
        rowemod.Main.IsGameMainMenuActive = false;
        RemoteKillSwitched.isModEnabled = true;
        Custom.SelectionRevision = 0;
        Custom.NativeRestoreRunning = false;
        Custom.Restored.Clear();
        return root;
    }
    static IEnumerator Start()
    {
        var routine = GameplayCharacterOutfit.RestoreWhenReady();
        Check(routine.MoveNext() && routine.Current is WaitForSecondsRealtime delay && delay.Seconds == 4f,
            "initial delay uses realtime during paused gameplay");
        return routine;
    }
    static void Main()
    {
        var root = Setup(); var routine = Start();
        Check(!routine.MoveNext(), "ready rider completes");
        Check(Custom.Restored.Count == 1 && Custom.Restored[0] == root, "spawn child resolves to local equip root");

        Setup(); var old = Start(); var newer = Start();
        Check(!old.MoveNext() && Custom.Restored.Count == 0, "overlapping spawn/gameplay events coalesce");
        Check(!newer.MoveNext() && Custom.Restored.Count == 1, "latest event restores once");

        Setup(); routine = Start(); Custom.SelectionRevision++;
        Check(!routine.MoveNext() && Custom.Restored.Count == 0, "new material selection during delay wins");
        Setup(); routine = Start(); Custom.NativeRestoreRunning = true;
        Check(!routine.MoveNext() && Custom.Restored.Count == 0, "restore-game-outfit operation wins");
        Setup(); routine = Start(); Memory.gamePlayer = new GameObject("Replacement rider");
        Check(!routine.MoveNext() && Custom.Restored.Count == 0, "stale spawn cannot edit replacement rider");
        Setup(); routine = Start(); rowemod.Main.playableSceneLoaded = false;
        Check(!routine.MoveNext() && Custom.Restored.Count == 0, "leaving gameplay cancels restore");
        Setup(); routine = Start(); rowemod.Main.IsGameMainMenuActive = true;
        Check(!routine.MoveNext() && Custom.Restored.Count == 0, "menu character is not restored by gameplay request");
        Setup(); routine = Start(); RemoteKillSwitched.isModEnabled = false;
        Check(!routine.MoveNext() && Custom.Restored.Count == 0, "disabled mod cancels restore");
        Setup(); Memory.gamePlayer = null; routine = Start();
        Check(!routine.MoveNext() && Custom.Restored.Count == 0, "missing local player never searches remote characters");

        root = Setup(); Memory.gamePlayer.Root = null; routine = Start();
        Check(routine.MoveNext() && routine.Current is WaitForSecondsRealtime retry && retry.Seconds == .25f,
            "late equip slots receive bounded retry");
        Memory.gamePlayer.Root = root;
        Check(!routine.MoveNext() && Custom.Restored[0] == root, "late local root restores current selection");
        Setup(); Memory.gamePlayer.Root = null; routine = Start();
        int retries = 0; while (routine.MoveNext()) retries++;
        Check(retries == 40 && Custom.Restored.Count == 0, "missing equip slots stop after ten seconds");
        Console.WriteLine($"PASS: {checks} gameplay outfit lifecycle checks. Stubbed Unity; native appearance requires runtime verification.");
    }
}
namespace UnityEngine
{
    internal class GameObject
    {
        internal string name;
        internal GameObject Root;
        internal GameObject(string value) { name = value; }
    }
    internal class WaitForSecondsRealtime
    {
        internal float Seconds;
        internal WaitForSecondsRealtime(float value) { Seconds = value; }
    }
}
namespace rowemod
{
    internal static class Main { internal static bool playableSceneLoaded, IsGameMainMenuActive; }
}
namespace rowemod.Mods
{
    internal static class RemoteKillSwitched { internal static bool isModEnabled; }
    internal static class Custom
    {
        internal static long SelectionRevision;
        internal static bool NativeRestoreRunning;
        internal static List<GameObject> Restored = new();
        internal static GameObject FindCharacterRoot(GameObject character) => character?.Root;
        internal static void RestoreCurrentSelection(GameObject root) => Restored.Add(root);
    }
}
namespace rowemod.Utils
{
    internal static class Memory { internal static GameObject gamePlayer; }
    internal static class Log
    {
        internal static void Msg(string value) { }
        internal static void Warning(string value) { }
    }
}
