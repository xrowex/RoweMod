using System.Collections;
using System.Reflection;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Networking;

internal static class Program
{
    static int checks;
    static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
    }

    static void Main(string[] args)
    {
        byte[] official = File.ReadAllBytes(args[0]);
        string root = Path.Combine(Path.GetTempPath(), "rowemod-emote-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Memory.bundlesFolderPath = root;
            string target = Path.Combine(root, "rowemod_custom_emotes");
            UnityWebRequest.Bytes = official;
            EmoteBundleLoader.EnsureAvailable();
            EmoteBundleLoader.EnsureAvailable();
            Check(MelonLoader.MelonCoroutines.Pending.Count == 1, "Only one concurrent request may start");
            Drain();
            Check(File.ReadAllBytes(target).SequenceEqual(official), "Fresh download installs verified bytes");
            Check(!File.Exists(target + ".download"), "Success removes staging file");
            Check(Memory.loadedBundles.Count == 1 && EmoteBundleLoader.Revision == 1, "Pack registered once");
            EmoteBundleLoader.EnsureAvailable(true);
            Check(MelonLoader.MelonCoroutines.Pending.Count == 0, "Loaded pack does not redownload");
            Memory.loadedBundles.Clear();
            EmoteBundleLoader.RegisterLoadedBundle();
            EmoteBundleLoader.RegisterLoadedBundle();
            Check(Memory.loadedBundles.Count == 1, "Catalog rebuild re-registers without duplicates");

            Reset();
            EmoteBundleLoader.EnsureAvailable();
            Drain();
            Check(UnityWebRequest.Requests == 0, "Valid cache needs no network request");
            Check(Memory.loadedBundles.Count == 1, "Cached pack registers");

            Reset();
            byte[] old = { 1, 2, 3 };
            File.WriteAllBytes(target, old);
            UnityWebRequest.Bytes = new byte[] { 4, 5, 6 };
            EmoteBundleLoader.EnsureAvailable();
            Drain();
            Check(File.ReadAllBytes(target).SequenceEqual(old), "Hash mismatch preserves previous cache");
            Check(Memory.loadedBundles.Count == 0, "Unverified data never loads");
            EmoteBundleLoader.EnsureAvailable();
            Check(MelonLoader.MelonCoroutines.Pending.Count == 0, "Failure cooldown prevents request spam");
            UnityWebRequest.Bytes = official;
            EmoteBundleLoader.EnsureAvailable(true);
            Drain();
            Check(File.ReadAllBytes(target).SequenceEqual(official), "Explicit retry replaces stale cache");

            Reset();
            File.WriteAllBytes(target, old);
            UnityWebRequest.NextResult = UnityWebRequest.Result.ConnectionError;
            EmoteBundleLoader.EnsureAvailable();
            Drain();
            Check(File.ReadAllBytes(target).SequenceEqual(old), "Network failure preserves previous cache");
            Check(UnityWebRequest.Disposals == 1, "Failed request is disposed");

            Reset();
            Directory.CreateDirectory(target + ".download");
            EmoteBundleLoader.EnsureAvailable();
            Drain();
            Check(File.ReadAllBytes(target).SequenceEqual(old), "Staging write failure preserves previous cache");
            Check(Memory.loadedBundles.Count == 0, "Install failure never loads stale pack");
            Console.WriteLine($"PASS: {checks} download/cache checks using the official bundle and stubbed Unity/network services; not an in-game test.");
        }
        finally { Directory.Delete(root, true); }
    }

    static void Drain()
    {
        foreach (IEnumerator routine in MelonLoader.MelonCoroutines.Pending.ToArray())
        {
            try { while (routine.MoveNext()) { } }
            finally { (routine as IDisposable)?.Dispose(); }
        }
        MelonLoader.MelonCoroutines.Pending.Clear();
    }

    static void Reset()
    {
        Type type = typeof(EmoteBundleLoader);
        foreach (string name in new[] { "_bundle", "_busy", "_retryAfter", "<Revision>k__BackingField" })
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
        }
        Memory.loadedBundles.Clear();
        UnityWebRequest.Requests = UnityWebRequest.Disposals = 0;
        UnityWebRequest.NextResult = UnityWebRequest.Result.Success;
    }
}

namespace UnityEngine
{
    public static class Time { public static float unscaledTime; }
    public class AssetBundle
    {
        public static AssetBundle LoadFromFile(string path) => File.Exists(path) ? new AssetBundle() : null;
    }
}
namespace UnityEngine.Networking
{
    public sealed class DownloadHandler { public byte[] data; }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { Success, ConnectionError }
        public static byte[] Bytes;
        public static Result NextResult;
        public static int Requests, Disposals;
        public int timeout;
        public Result result;
        public string error = "Simulated connection failure";
        public DownloadHandler downloadHandler;
        public static UnityWebRequest Get(string url)
        {
            Requests++;
            return new UnityWebRequest { result = NextResult, downloadHandler = new DownloadHandler { data = Bytes } };
        }
        public object SendWebRequest() => null;
        public void Dispose() => Disposals++;
    }
}
namespace MelonLoader
{
    public static class MelonCoroutines
    {
        public static readonly List<IEnumerator> Pending = new();
        public static void Start(IEnumerator routine) => Pending.Add(routine);
    }
}
namespace rowemod.Utils
{
    public static class Memory
    {
        public static string bundlesFolderPath;
        public static readonly List<AssetBundle> loadedBundles = new();
    }
    public static class Log
    {
        public static void Msg(string message) { }
        public static void Warning(string message) { }
    }
}
namespace rowemod.Mods
{
    public static class TrickAnimationEditor { public static void NotifyAssetBundlesReloaded() { } }
}
