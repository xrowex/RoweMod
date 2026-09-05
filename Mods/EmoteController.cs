using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMashBox.Addons.CharacterController;
using UnityEngine;
using rowemod.Utils;

namespace rowemod.Mods
{
    internal static class EmoteController
    {
        internal sealed class EmoteOption
        {
            public string Label;
            public string ClipName;
            public int NativeIndex;
            public bool Stock;
            public bool Imported;
            public AnimationClip Clip;
        }

        private sealed class CuratedPose
        {
            public readonly string Label;
            public readonly string ClipName;

            public CuratedPose(string label, string clipName)
            {
                Label = label;
                ClipName = clipName;
            }
        }

        private static readonly CuratedPose[] CuratedPoses =
        {
            new CuratedPose("Sitting", "Player@Bench_Idle"),
            new CuratedPose("Party Kid", "Emote_PartyKid"),
            new CuratedPose("Gun Fingers", "Player@Emote_GunFingers"),
            new CuratedPose("Locking Dance", "Locking Hip Hop Dance"),
            new CuratedPose("Bike Dance", "Player@BMX_Stationary_Dance")
        };

        private static readonly string[] PreferredDanceClips =
        {
            "RoweMod_Custom_Emote_Sturdy_GangGang",
            "RoweMod_Custom_Emote_Sturdy_Freestyle",
            "Locking Hip Hop Dance",
            "Player@BMX_Stationary_Dance",
            "Emote_PartyKid"
        };

        private static readonly List<EmoteOption> OptionsInternal = new List<EmoteOption>();
        private static readonly List<EmoteOption> DanceOptionsInternal = new List<EmoteOption>();
        private static EmoteSystem _system;
        private static Il2CppReferenceArray<AnimationClip> _originalClips;
        private static int _systemInstanceId = int.MinValue;
        private static int _packRevision = -1;
        private static bool _lastPlayWasAddedClip;
        private static EmoteOption _loopOption;
        private static bool _loopSawActive;
        private static float _loopStartedAt;
        private static float _loopExpectedEndAt;
        private static string _status = "Open Emotes in a gameplay map to discover the native emote system.";

        public static IReadOnlyList<EmoteOption> Options => OptionsInternal;
        public static IReadOnlyList<EmoteOption> DanceOptions => DanceOptionsInternal;
        public static string Status => _status;
        public static bool IsActive => _system != null && SafeRead(() => _system._isEmoteActive, false);
        public static bool IsLooping => _loopOption != null;

        public static void OnSceneInitialized()
        {
            Release();
            RadioDanceController.OnSceneInitialized();
            VideoTvController.OnSceneInitialized();
            _status = "Waiting for the local rider's native EmoteSystem.";
        }

        public static bool Refresh(bool force = false)
        {
            EmoteBundleLoader.EnsureAvailable(force);
            EmoteSystem resolved = ResolveLocalSystem();
            if (resolved == null)
            {
                Release();
                _status = "Native EmoteSystem not found on the local rider yet.";
                return false;
            }

            int instanceId = resolved.GetInstanceID();
            bool sameSystem = _system != null && _systemInstanceId == instanceId;
            if (!force && sameSystem && OptionsInternal.Count > 0 &&
                (_packRevision == EmoteBundleLoader.Revision || IsActive))
                return true;

            ClearLoopState();
            EmotePropController.StopEmoteProp();

            if (sameSystem)
            {
                // Rebuild from the game's untouched array. Otherwise repeated Refresh clicks would
                // treat RoweMod's previous replacement as stock and permanently duplicate entries.
                RestoreOriginalArray(true);
                _system = resolved;
            }
            else
            {
                if (_system != null)
                    RestoreOriginalArray(true);

                _system = resolved;
                _systemInstanceId = instanceId;
                _originalClips = resolved._emote;
            }

            if (_originalClips == null)
                _originalClips = resolved._emote;
            _lastPlayWasAddedClip = false;
            OptionsInternal.Clear();

            var combined = new List<AnimationClip>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int stockCount = _originalClips?.Length ?? 0;
            for (int i = 0; i < stockCount; i++)
            {
                AnimationClip clip = _originalClips[i];
                if (clip == null)
                    continue;

                string clipName = SafeRead(() => clip.name, $"Emote {i + 1}");
                combined.Add(clip);
                names.Add(clipName);
                OptionsInternal.Add(new EmoteOption
                {
                    Label = FriendlyName(clipName),
                    ClipName = clipName,
                    NativeIndex = combined.Count - 1,
                    Stock = true,
                    Clip = clip
                });
            }

            AnimationClip[] loadedClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
            var loadedByName = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            if (loadedClips != null)
            {
                for (int i = 0; i < loadedClips.Length; i++)
                {
                    AnimationClip clip = loadedClips[i];
                    if (clip == null)
                        continue;
                    string name = SafeRead(() => clip.name, string.Empty);
                    if (!string.IsNullOrWhiteSpace(name) && !loadedByName.ContainsKey(name))
                        loadedByName.Add(name, clip);
                }
            }

            int curatedCount = 0;
            for (int i = 0; i < CuratedPoses.Length; i++)
            {
                CuratedPose pose = CuratedPoses[i];
                if (names.Contains(pose.ClipName) ||
                    !loadedByName.TryGetValue(pose.ClipName, out AnimationClip clip) ||
                    clip == null)
                    continue;

                combined.Add(clip);
                names.Add(pose.ClipName);
                curatedCount++;
                OptionsInternal.Add(new EmoteOption
                {
                    Label = pose.Label,
                    ClipName = pose.ClipName,
                    NativeIndex = combined.Count - 1,
                    Stock = false,
                    Clip = clip
                });
            }

            int importedCount = 0;
            List<AnimationClip> imported = TrickAnimationEditor.GetCustomRiderClips(false);
            for (int i = 0; i < imported.Count; i++)
            {
                AnimationClip clip = imported[i];
                string clipName = SafeRead(() => clip?.name, string.Empty);
                if (clip == null || string.IsNullOrWhiteSpace(clipName) || !names.Add(clipName))
                    continue;

                combined.Add(clip);
                importedCount++;
                OptionsInternal.Add(new EmoteOption
                {
                    Label = FriendlyName(clipName),
                    ClipName = clipName,
                    NativeIndex = combined.Count - 1,
                    Stock = false,
                    Imported = true,
                    Clip = clip
                });
            }

            RebuildDanceOptions();

            var replacement = new Il2CppReferenceArray<AnimationClip>(combined.Count);
            for (int i = 0; i < combined.Count; i++)
                replacement[i] = combined[i];
            resolved._emote = replacement;
            _packRevision = EmoteBundleLoader.Revision;

            _status =
                $"Native emotes ready: {stockCount} stock, {curatedCount} added poses, " +
                $"{importedCount} imported RoweMod clips. Added clips are local only.";
            Main.NotifyRuntimeContributionApplied();
            Log.Msg($"[Emotes] {_status} component={instanceId}.");
            return true;
        }

        public static bool Play(EmoteOption option, bool loop = false)
        {
            if (option == null || !Refresh(false) || _system == null)
                return false;

            try
            {
                ClearLoopState();
                if (_system._isEmoteActive)
                {
                    if (_lastPlayWasAddedClip)
                        _system.CancelEmote();
                    else
                        _system.Cancel();
                }

                if (option.Stock)
                    _system.Play(option.NativeIndex);
                else
                    _system.FireEmote(option.NativeIndex);

                _lastPlayWasAddedClip = !option.Stock;
                if (loop && !option.Stock)
                    ArmLoop(option);

                EmotePropController.Begin(option.ClipName, option.Clip, loop);
                _status = option.Stock
                    ? $"Playing stock emote: {option.Label}. Native networking remains available."
                    : loop
                        ? $"Looping local pose: {option.Label}. Move or press Stop Emote to cancel."
                        : $"Playing local pose: {option.Label}.";
                Log.Msg($"[Emotes] {_status} clip={option.ClipName}, index={option.NativeIndex}.");
                return true;
            }
            catch (Exception ex)
            {
                _status = $"Could not play {option.Label}: {ex.Message}";
                Log.Error($"[Emotes] {_status}");
                return false;
            }
        }

        public static void Update()
        {
            RadioDanceController.Update();
            VideoTvController.Update();
            EmotePropController.Update(IsActive);

            if (_loopOption == null || _system == null)
                return;

            bool active = SafeRead(() => _system._isEmoteActive, false);
            if (active)
            {
                _loopSawActive = true;
                return;
            }

            float now = Time.unscaledTime;
            if (!_loopSawActive)
            {
                if (now - _loopStartedAt > 1f)
                {
                    string label = _loopOption.Label;
                    ClearLoopState();
                    _status = $"{label} did not enter the native emote layer.";
                }
                return;
            }

            // A cancellation before the clip's expected end came from movement, a native
            // gameplay transition, or another system. Respect it instead of restarting the
            // rider later and making them feel trapped in an emote loop.
            if (now + 0.2f < _loopExpectedEndAt)
            {
                string label = _loopOption.Label;
                bool keepRadioPlaying = EmotePropController.IsRadioPlaying;
                ClearLoopState();
                if (keepRadioPlaying)
                {
                    _status = $"Stopped looping {label} because riding cancelled the dance; the placed radio is still playing.";
                }
                else
                {
                    EmotePropController.Stop();
                    _status = $"Stopped looping {label} because the game cancelled the emote.";
                }
                return;
            }

            RestartLoop();
        }

        public static void Stop()
        {
            if (_system == null)
            {
                EmotePropController.StopEmoteProp();
                return;
            }

            try
            {
                ClearLoopState();
                EmotePropController.StopEmoteProp();
                if (_lastPlayWasAddedClip)
                    _system.CancelEmote();
                else
                    _system.Cancel();
                _lastPlayWasAddedClip = false;
                _status = "Emote stopped through the native emote system.";
            }
            catch (Exception ex)
            {
                _status = $"Could not stop emote: {ex.Message}";
                Log.Error($"[Emotes] {_status}");
            }
        }

        public static void RestoreOriginalList()
        {
            ClearLoopState();
            EmotePropController.StopEmoteProp();
            RestoreOriginalArray(true);
            OptionsInternal.Clear();
            DanceOptionsInternal.Clear();
            _system = null;
            _systemInstanceId = int.MinValue;
            _status = "Restored the native emote list. Refresh to add RoweMod poses again.";
        }

        public static void Release()
        {
            ClearLoopState();
            EmotePropController.Release();
            RestoreOriginalArray(true);
            OptionsInternal.Clear();
            DanceOptionsInternal.Clear();
            _system = null;
            _originalClips = null;
            _systemInstanceId = int.MinValue;
            _lastPlayWasAddedClip = false;
        }

        private static void ArmLoop(EmoteOption option)
        {
            _loopOption = option;
            _loopSawActive = false;
            _loopStartedAt = Time.unscaledTime;
            _loopExpectedEndAt = _loopStartedAt + Mathf.Max(0.1f, SafeRead(() => option.Clip.length, 1f));
        }

        private static void RestartLoop()
        {
            EmoteOption option = _loopOption;
            if (option == null || _system == null)
                return;

            try
            {
                _system.FireEmote(option.NativeIndex);
                ArmLoop(option);
                EmotePropController.Begin(option.ClipName, option.Clip, true);
                Log.Msg($"[Emotes] Repeated local loop: {option.Label}.");
            }
            catch (Exception ex)
            {
                ClearLoopState();
                EmotePropController.StopEmoteProp();
                _status = $"Could not repeat {option.Label}: {ex.Message}";
                Log.Error($"[Emotes] {_status}");
            }
        }

        private static void ClearLoopState()
        {
            _loopOption = null;
            _loopSawActive = false;
            _loopStartedAt = 0f;
            _loopExpectedEndAt = 0f;
        }

        private static void RebuildDanceOptions()
        {
            DanceOptionsInternal.Clear();
            for (int i = 0; i < PreferredDanceClips.Length; i++)
            {
                for (int j = 0; j < OptionsInternal.Count; j++)
                {
                    EmoteOption option = OptionsInternal[j];
                    if (option != null && !option.Stock &&
                        string.Equals(option.ClipName, PreferredDanceClips[i], StringComparison.OrdinalIgnoreCase))
                    {
                        DanceOptionsInternal.Add(option);
                        break;
                    }
                }
            }

            for (int i = 0; i < OptionsInternal.Count; i++)
            {
                EmoteOption option = OptionsInternal[i];
                if (option == null || option.Stock || DanceOptionsInternal.Contains(option))
                    continue;

                string searchable = (option.Label ?? string.Empty) + " " + (option.ClipName ?? string.Empty);
                if (searchable.IndexOf("dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    searchable.IndexOf("sturdy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    searchable.IndexOf("freestyle", StringComparison.OrdinalIgnoreCase) >= 0)
                    DanceOptionsInternal.Add(option);
            }
        }

        private static EmoteSystem ResolveLocalSystem()
        {
            if (Memory.rMbCharacter == null)
                return null;

            EmoteSystem system = Memory.rMbCharacter.GetComponentInChildren<EmoteSystem>(true) ??
                                 Memory.rMbCharacter.GetComponentInParent<EmoteSystem>();
            if (system != null)
                return system;

            Transform localRoot = Memory.rMbCharacter.transform?.root;
            if (localRoot == null)
                return null;

            EmoteSystem[] systems = UnityEngine.Object.FindObjectsOfType<EmoteSystem>();
            if (systems == null)
                return null;
            for (int i = 0; i < systems.Length; i++)
            {
                EmoteSystem candidate = systems[i];
                if (candidate != null && candidate.transform?.root == localRoot)
                    return candidate;
            }
            return null;
        }

        private static void RestoreOriginalArray(bool stopAddedClip)
        {
            if (_system == null)
                return;

            try
            {
                if (stopAddedClip && _lastPlayWasAddedClip && _system._isEmoteActive)
                    _system.CancelEmote();
                if (_originalClips != null)
                    _system._emote = _originalClips;
            }
            catch (Exception ex)
            {
                Log.Warning($"[Emotes] Could not restore native emote list: {ex.Message}");
            }
        }

        private static string FriendlyName(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
                return "Unnamed Emote";

            string result = clipName;
            if (result.StartsWith("Player@", StringComparison.OrdinalIgnoreCase))
                result = result.Substring("Player@".Length);
            if (result.StartsWith("RoweMod_Custom_", StringComparison.OrdinalIgnoreCase))
                result = result.Substring("RoweMod_Custom_".Length);
            if (result.StartsWith("Emote_", StringComparison.OrdinalIgnoreCase))
                result = result.Substring("Emote_".Length);
            if (string.Equals(result, "Sturdy_GangGang", StringComparison.OrdinalIgnoreCase))
                return "Sturdy - Gang Gang";
            if (string.Equals(result, "Sturdy_Freestyle", StringComparison.OrdinalIgnoreCase))
                return "Sturdy - Freestyle";
            return result.Replace('_', ' ').Trim();
        }

        private static T SafeRead<T>(Func<T> read, T fallback)
        {
            try { return read(); }
            catch { return fallback; }
        }
    }
}
