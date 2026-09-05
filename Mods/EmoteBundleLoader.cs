using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using rowemod.Utils;

namespace rowemod.Mods
{
    internal static class EmoteBundleLoader
    {
        private const string FileName = "rowemod_custom_emotes";
        private const string DownloadUrl =
            "https://raw.githubusercontent.com/xrowex/RoweMod/master/Bundles/rowemod_custom_emotes";
        private const string ExpectedSha256 =
            "EEE804C961C334B72B24C08813F74EFC540F605F0D6205264257B836E1936C34";
        private const int MaximumBytes = 4 * 1024 * 1024;
        private static AssetBundle _bundle;
        private static bool _busy;
        private static float _retryAfter;
        public static int Revision { get; private set; }
        public static string Status { get; private set; } = "Custom emotes download automatically when you open this page.";

        public static void EnsureAvailable(bool retry = false)
        {
            if (_bundle != null || _busy || (!retry && Time.unscaledTime < _retryAfter))
                return;
            _busy = true;
            MelonCoroutines.Start(EnsureBundle());
        }

        // The general bundle catalog can be rebuilt without unloading this owned bundle.
        public static void RegisterLoadedBundle()
        {
            if (_bundle == null || Memory.loadedBundles.Contains(_bundle))
                return;
            Memory.loadedBundles.Add(_bundle);
            TrickAnimationEditor.NotifyAssetBundlesReloaded();
            Revision++;
        }

        private static bool ValidFile(string path)
        {
            if (!File.Exists(path)) return false;
            long length = new FileInfo(path).Length;
            if (length <= 0 || length > MaximumBytes) return false;
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return string.Equals(Convert.ToHexString(sha.ComputeHash(stream)), ExpectedSha256,
                StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerator EnsureBundle()
        {
            string path = Path.Combine(Memory.bundlesFolderPath, FileName);
            string staging = path + ".download";
            UnityWebRequest request = null;
            try
            {
                bool cached = false;
                try { cached = ValidFile(path); }
                catch (Exception ex) { Log.Warning("[EmotePack] Cache check failed: " + ex.Message); }

                if (!cached)
                {
                    Status = "Downloading custom emotes...";
                    request = UnityWebRequest.Get(DownloadUrl);
                    request.timeout = 30;
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Fail("Download failed: " + request.error);
                        yield break;
                    }

                    bool installed = false;
                    try
                    {
                        byte[] bytes = request.downloadHandler.data;
                        if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes)
                            throw new InvalidDataException("The emote download is empty or too large.");
                        using SHA256 sha = SHA256.Create();
                        if (!string.Equals(Convert.ToHexString(sha.ComputeHash(bytes)), ExpectedSha256,
                                StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("The emote download failed verification.");
                        Directory.CreateDirectory(Memory.bundlesFolderPath);
                        File.WriteAllBytes(staging, bytes);
                        File.Move(staging, path, true);
                        installed = true;
                    }
                    catch (Exception ex) { Fail(ex.Message); }
                    if (!installed) yield break;
                }

                try
                {
                    _bundle = AssetBundle.LoadFromFile(path);
                    if (_bundle == null)
                        throw new InvalidDataException("Unity could not load the verified emote pack.");
                    RegisterLoadedBundle();
                    Status = "Custom emotes ready.";
                    Log.Msg("[EmotePack] Verified emote pack loaded and registered.");
                }
                catch (Exception ex) { Fail(ex.Message); }
            }
            finally
            {
                request?.Dispose();
                try { if (File.Exists(staging)) File.Delete(staging); }
                catch (Exception ex) { Log.Warning("[EmotePack] Staging cleanup failed: " + ex.Message); }
                _busy = false;
                _retryAfter = Time.unscaledTime + 30f;
            }
        }

        private static void Fail(string reason)
        {
            Status = "Custom emotes unavailable. Use Refresh to retry. " + reason;
            Log.Warning("[EmotePack] " + Status);
        }
    }
}
