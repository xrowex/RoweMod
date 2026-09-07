using System;
using System.Collections;
using System.Collections.Generic;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using MelonLoader;

internal static class Program
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static GameObject Setup(bool rootAvailable = true)
    {
        SceneManager.MenuLoaded = true;
        var root = new GameObject("Menu rider", "MainMenu", true);
        SceneManager.Roots = rootAvailable ? new[] { root } : Array.Empty<GameObject>();
        Memory.menuPlayer = null;
        Custom.SelectionRevision = 0;
        Custom.Restored.Clear();
        MelonCoroutines.Pending.Clear();
        Time.unscaledTime = 0;
        MainMenuCharacterPreview.OnSceneInitialized("MainMenu");
        return root;
    }
    static void Tick(float time = 1) { Time.unscaledTime = time; MainMenuCharacterPreview.Update(); }
    static void Complete(IEnumerator routine)
    {
        Check(routine.MoveNext() && routine.Current is WaitForSecondsRealtime, "delay uses realtime, independent of menu timescale");
        Check(!routine.MoveNext(), "restore finishes after delay");
    }
    static void Main()
    {
        var root = Setup(); Tick();
        Check(Memory.menuPlayer == root, "discovery does not require a saved preset");
        Complete(MelonCoroutines.Pending[0]);
        Check(Custom.Restored.Count == 1 && Custom.Restored[0] == root, "restore explicitly targets menu character");
        MainMenuCharacterPreview.NotifyMenuCharacterSpawned(root); Tick(2);
        Check(MelonCoroutines.Pending.Count == 1, "duplicate spawn does not reapply outfit");

        root = Setup(); Tick();
        Custom.SelectionRevision++;
        Complete(MelonCoroutines.Pending[0]);
        Check(Custom.Restored.Count == 0, "manual selection or outfit reset during delay wins");

        root = Setup(); Tick();
        SceneManager.MenuLoaded = false;
        MainMenuCharacterPreview.OnSceneInitialized("Gameplay");
        Complete(MelonCoroutines.Pending[0]);
        Check(Custom.Restored.Count == 0, "leaving menu cancels pending restore");

        root = Setup(); Tick();
        MainMenuCharacterPreview.OnSceneInitialized("AdditiveLighting");
        Complete(MelonCoroutines.Pending[0]);
        Check(Custom.Restored.Count == 1, "additive scene does not cancel loaded menu restore");

        root = Setup(false);
        var child = new GameObject("Physics child", "MainMenu", false);
        child.transform.parent = root.transform;
        MainMenuCharacterPreview.NotifyMenuCharacterSpawned(child); Tick();
        Check(Memory.menuPlayer == root, "spawn child normalized to equip-slot owner");
        Complete(MelonCoroutines.Pending[0]);

        Setup(false);
        var foreign = new GameObject("Remote gameplay rider", "Gameplay", true);
        MainMenuCharacterPreview.NotifyMenuCharacterSpawned(foreign); Tick();
        Check(Memory.menuPlayer == null && MelonCoroutines.Pending.Count == 0, "gameplay character cannot become menu target");

        root = Setup(false); Tick();
        SceneManager.Roots = new[] { root }; Tick(2);
        Check(Memory.menuPlayer == root, "bounded retry finds late menu spawn without event");

        root = Setup(); Tick();
        var old = MelonCoroutines.Pending[0];
        var replacement = new GameObject("New preview", "MainMenu", true);
        MainMenuCharacterPreview.NotifyMenuCharacterSpawned(replacement); Tick(2);
        Complete(old);
        Check(Custom.Restored.Count == 0, "replaced character cancels old routine");
        Complete(MelonCoroutines.Pending[1]);
        Check(Custom.Restored.Count == 1 && Custom.Restored[0] == replacement, "new character gets its own restore");

        root = Setup(false);
        for (int i = 1; i <= 25; i++) Tick(i);
        SceneManager.Roots = new[] { root }; Tick(30);
        Check(MelonCoroutines.Pending.Count == 0, "discovery stops after bounded timeout");
        Console.WriteLine($"PASS: {checks} main-menu lifecycle checks with stubbed Unity; in-game visual acceptance still required.");
    }
}
namespace rowemod.Mods
{
    internal static class Custom
    {
        internal static long SelectionRevision;
        internal static readonly List<GameObject> Restored = new();
        internal static void RestoreMenuSelection(GameObject target) => Restored.Add(target);
    }
}
namespace rowemod.Utils
{
    internal static class Memory { internal static GameObject menuPlayer; }
    internal static class Log { internal static void Msg(string message) { } internal static void Warning(string message) { } }
}
namespace MelonLoader
{
    internal static class MelonCoroutines
    {
        internal static readonly List<IEnumerator> Pending = new();
        internal static void Start(IEnumerator routine) => Pending.Add(routine);
    }
}
namespace UnityEngine
{
    internal static class Time { internal static float unscaledTime; }
    internal class WaitForSecondsRealtime { internal WaitForSecondsRealtime(float delay) { } }
    internal class GameObject
    {
        static int nextId;
        readonly int id = ++nextId;
        internal string name, SceneName;
        internal bool HasSlots;
        internal Transform transform;
        internal Scene scene => new Scene(SceneName);
        internal GameObject(string name, string sceneName, bool hasSlots)
        { this.name = name; SceneName = sceneName; HasSlots = hasSlots; transform = new Transform(this); }
        internal int GetInstanceID() => id;
        internal T GetComponentInChildren<T>(bool inactive) where T : class =>
            new Il2CppMashBox.Character.Scripts.CustomCharacterManager { gameObject = this } as T;
    }
    internal class Transform
    {
        internal GameObject gameObject;
        internal Transform parent;
        internal Transform(GameObject go) { gameObject = go; }
        internal Transform Find(string path) => gameObject.HasSlots && path == "Physics Skeleton/EquipSlot_Body" ? this : null;
    }
}
namespace UnityEngine.SceneManagement
{
    internal struct Scene
    {
        internal string name;
        internal Scene(string name) { this.name = name; }
        internal bool isLoaded => name != "MainMenu" || SceneManager.MenuLoaded;
        internal bool IsValid() => name != null;
        internal GameObject[] GetRootGameObjects() => SceneManager.Roots;
    }
    internal static class SceneManager
    {
        internal static bool MenuLoaded;
        internal static GameObject[] Roots = Array.Empty<GameObject>();
        internal static Scene GetSceneByName(string name) => new Scene(name);
    }
}
namespace Il2CppMashBox.Character.Scripts
{
    internal class CustomCharacterManager { internal GameObject gameObject; }
}
