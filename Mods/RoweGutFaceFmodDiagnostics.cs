using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Il2CppFMODUnity;
using Il2CppMashBoxSDK.Maps.Audio;
using UnityEngine;
using Log = rowemod.Utils.Log;

namespace rowemod.Mods
{
    internal static class RoweGutFaceFmodDiagnostics
    {
        private const string Prefix = "[RoweGutFaceFMOD]";
        private const string EventPath = "event:/fire";
        private static readonly HashSet<string> ExpectedBanks = new HashSet<string>(StringComparer.Ordinal)
        {
            "rowe.strings",
            "rowe",
            "rowe2.strings",
            "rowe2"
        };

        private static readonly HashSet<string> LoadedBanks = new HashSet<string>(StringComparer.Ordinal);
        private static bool _scannedSceneObjects;
        private static bool _loggedFiles;
        private static bool _probedAfterAllBanks;

        public static void OnSceneInitialized(string sceneName)
        {
            if (!string.Equals(sceneName, "RoweGutFace", StringComparison.OrdinalIgnoreCase))
                return;

            _scannedSceneObjects = false;
            _loggedFiles = false;
            _probedAfterAllBanks = false;
            LoadedBanks.Clear();

            LogSceneObjectsOnce("RoweGutFace scene initialized");
            LogBankFilesOnce();
            ProbeEventResolve("on RoweGutFace scene initialized before SDK sequence");
        }

        private static void LogSceneObjectsOnce(string reason)
        {
            if (_scannedSceneObjects)
                return;

            _scannedSceneObjects = true;
            Log.Msg($"{Prefix} SDK object scan: {reason}; activeScene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'.");

            try
            {
                var loaders = UnityEngine.Object.FindObjectsOfType<MBSimpleFmodBankLoader>(true);
                Log.Msg($"{Prefix} SDK bank loaders found={loaders.Length}.");
                foreach (var loader in loaders)
                {
                    var bankName = ReadStringMember(loader, "bankName");
                    var go = loader.gameObject;
                    Log.Msg($"{Prefix} SDK bank loader object='{go.name}' bankName='{bankName}' loadOnStart={ReadBoolMember(loader, "loadOnStart")} loadSamples={ReadBoolMember(loader, "loadSamples")} unloadOnDisable={ReadBoolMember(loader, "unloadOnDisable")} enabled={loader.enabled} active={go.activeInHierarchy} scene='{go.scene.name}'.");
                }

                var events = UnityEngine.Object.FindObjectsOfType<MBSimpleFmodEventAudio>(true);
                Log.Msg($"{Prefix} SDK FMOD event audio components found={events.Length}.");
                foreach (var audio in events)
                {
                    var eventPath = ReadStringMember(audio, "eventPath");
                    var go = audio.gameObject;
                    Log.Msg($"{Prefix} SDK event audio object='{go.name}' eventPath='{eventPath}' startOnEnable={ReadBoolMember(audio, "startOnEnable")} stopOnDisable={ReadBoolMember(audio, "stopOnDisable")} enabled={audio.enabled} active={go.activeInHierarchy} scene='{go.scene.name}'.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"{Prefix} SDK object scan failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogBankFilesOnce()
        {
            if (_loggedFiles)
                return;

            _loggedFiles = true;
            LogBankFileSet("StreamingAssets", Application.streamingAssetsPath);
            LogBankFileSet("RoweGutFace map folder", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BMX Streets",
                "Maps",
                "rowegutface_map"));
        }

        private static void LogBankFileSet(string label, string folder)
        {
            Log.Msg($"{Prefix} Bank folder {label}: '{folder}'.");
            foreach (var fileName in new[]
                     {
                         "rowe.strings.bank",
                         "rowe.bank",
                         "rowe2.strings.bank",
                         "rowe2.bank",
                         "fire.strings.bank",
                         "fire.bank"
                     })
            {
                var path = Path.Combine(folder, fileName);
                if (!File.Exists(path))
                {
                    Log.Msg($"{Prefix} Bank file {label}/{fileName}: missing path='{path}'.");
                    continue;
                }

                var file = new FileInfo(path);
                Log.Msg($"{Prefix} Bank file {label}/{fileName}: exists size={file.Length} modified={file.LastWriteTime:O} path='{path}'.");
            }
        }

        private static void MarkLoaded(string bankName)
        {
            if (!ExpectedBanks.Contains(bankName))
                return;

            LoadedBanks.Add(bankName);
            if (_probedAfterAllBanks || LoadedBanks.Count < ExpectedBanks.Count)
                return;

            _probedAfterAllBanks = true;
            ProbeEventResolve("after SDK loaded rowe.strings,rowe,rowe2.strings,rowe2");
        }

        private static void ProbeEventResolve(string reason)
        {
            try
            {
                var description = RuntimeManager.GetEventDescription(EventPath);
                Log.Msg($"{Prefix} FMOD event resolve {reason}: GetEventDescription('{EventPath}') returned {description}.");
            }
            catch (Exception ex)
            {
                Log.Error($"{Prefix} FMOD event resolve {reason}: GetEventDescription('{EventPath}') failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string ReadStringMember(object target, string memberName)
        {
            if (target == null)
                return string.Empty;

            var property = target.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(target) as string ?? string.Empty;

            var field = target.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target) as string ?? string.Empty;
        }

        private static bool ReadBoolMember(object target, string memberName)
        {
            if (target == null)
                return false;

            var property = target.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetValue(target) is bool propertyValue)
                return propertyValue;

            var field = target.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.GetValue(target) is bool fieldValue && fieldValue;
        }

        [HarmonyPatch]
        private static class BankLoaderStartPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MBSimpleFmodBankLoader), "Start", Type.EmptyTypes);
            }

            private static void Prefix(MBSimpleFmodBankLoader __instance)
            {
                LogSceneObjectsOnce("first SDK bank loader Start call");
                LogBankFilesOnce();

                var bankName = ReadStringMember(__instance, "bankName");
                Log.Msg($"{Prefix} SDK BankLoader.Start begin object='{__instance.gameObject.name}' bankName='{bankName}' loadOnStart={ReadBoolMember(__instance, "loadOnStart")}.");
            }

            private static void Postfix(MBSimpleFmodBankLoader __instance)
            {
                var bankName = ReadStringMember(__instance, "bankName");
                Log.Msg($"{Prefix} SDK BankLoader.Start success object='{__instance.gameObject.name}' bankName='{bankName}'.");
            }
        }

        [HarmonyPatch]
        private static class BankLoaderNoArgsPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MBSimpleFmodBankLoader), nameof(MBSimpleFmodBankLoader.LoadBank), Type.EmptyTypes);
            }

            private static void Prefix(MBSimpleFmodBankLoader __instance)
            {
                LogSceneObjectsOnce("first SDK LoadBank call");
                LogBankFilesOnce();

                var bankName = ReadStringMember(__instance, "bankName");
                Log.Msg($"{Prefix} SDK LoadBank() begin object='{__instance.gameObject.name}' bankName='{bankName}'.");
            }

            private static void Postfix(MBSimpleFmodBankLoader __instance)
            {
                var bankName = ReadStringMember(__instance, "bankName");
                Log.Msg($"{Prefix} SDK LoadBank() success object='{__instance.gameObject.name}' bankName='{bankName}'.");
                MarkLoaded(bankName);
            }

            private static Exception Finalizer(MBSimpleFmodBankLoader __instance, Exception __exception)
            {
                if (__exception == null)
                    return null;

                var bankName = ReadStringMember(__instance, "bankName");
                Log.Error($"{Prefix} SDK LoadBank() failed object='{__instance.gameObject.name}' bankName='{bankName}': {__exception.GetType().Name}: {__exception.Message}");
                return __exception;
            }
        }

        [HarmonyPatch]
        private static class BankLoaderStringPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MBSimpleFmodBankLoader), nameof(MBSimpleFmodBankLoader.LoadBank), new[] { typeof(string) });
            }

            private static void Prefix(MBSimpleFmodBankLoader __instance, string bankToLoad)
            {
                Log.Msg($"{Prefix} SDK LoadBank(string) begin object='{__instance.gameObject.name}' bankToLoad='{bankToLoad}'.");
            }

            private static void Postfix(MBSimpleFmodBankLoader __instance, string bankToLoad)
            {
                Log.Msg($"{Prefix} SDK LoadBank(string) success object='{__instance.gameObject.name}' bankToLoad='{bankToLoad}'.");
                MarkLoaded(bankToLoad);
            }

            private static Exception Finalizer(MBSimpleFmodBankLoader __instance, string bankToLoad, Exception __exception)
            {
                if (__exception == null)
                    return null;

                Log.Error($"{Prefix} SDK LoadBank(string) failed object='{__instance.gameObject.name}' bankToLoad='{bankToLoad}': {__exception.GetType().Name}: {__exception.Message}");
                return __exception;
            }
        }

        [HarmonyPatch]
        private static class EventAudioPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(MBSimpleFmodEventAudio), nameof(MBSimpleFmodEventAudio.RestartAudioEvent), Type.EmptyTypes);
            }

            private static void Prefix(MBSimpleFmodEventAudio __instance)
            {
                var eventPath = ReadStringMember(__instance, "eventPath");
                Log.Msg($"{Prefix} SDK RestartAudioEvent begin object='{__instance.gameObject.name}' eventPath='{eventPath}'.");
            }

            private static void Postfix(MBSimpleFmodEventAudio __instance)
            {
                var eventPath = ReadStringMember(__instance, "eventPath");
                Log.Msg($"{Prefix} SDK RestartAudioEvent success object='{__instance.gameObject.name}' eventPath='{eventPath}'.");
                if (string.Equals(eventPath, EventPath, StringComparison.Ordinal))
                    ProbeEventResolve("after SDK RestartAudioEvent");
            }

            private static Exception Finalizer(MBSimpleFmodEventAudio __instance, Exception __exception)
            {
                if (__exception == null)
                    return null;

                var eventPath = ReadStringMember(__instance, "eventPath");
                Log.Error($"{Prefix} SDK RestartAudioEvent failed object='{__instance.gameObject.name}' eventPath='{eventPath}': {__exception.GetType().Name}: {__exception.Message}");
                return __exception;
            }
        }
    }
}
