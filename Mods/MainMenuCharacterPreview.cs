using System;
using System.Collections;
using Il2CppMashBox.Character.Scripts;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace rowemod.Mods
{
    /// <summary>
    /// Restores the saved RoweMod clothing preset on the non-networked character
    /// displayed by the MainMenu scene. The menu-spawn event is not guaranteed to
    /// arrive after RoweMod has subscribed, so this uses a short, bounded retry
    /// window after MainMenu loads instead of relying on that event alone.
    /// </summary>
    internal static class MainMenuCharacterPreview
    {
        private const string MainMenuSceneName = "MainMenu";
        private const float InitialApplyDelaySeconds = 0.5f;
        private const float RetryIntervalSeconds = 0.25f;
        private const int MaxResolveAttempts = 24;

        private static bool _restoreRequested;
        private static bool _applyRoutineRunning;
        private static float _nextResolveTime;
        private static int _resolveAttempts;
        private static int _appliedCharacterId;
        private static string _appliedPresetName = string.Empty;
        private static GameObject _eventCharacter;

        public static void OnSceneInitialized(string sceneName)
        {
            Reset();

            if (string.Equals(sceneName, MainMenuSceneName, StringComparison.OrdinalIgnoreCase))
                RequestRestore("scene initialized");
        }

        public static void NotifyMenuCharacterSpawned(GameObject character)
        {
            if (character == null)
                return;

            _eventCharacter = character;
            RequestRestore("menu character spawned");
        }

        public static void Update()
        {
            if (!_restoreRequested || _applyRoutineRunning || Time.unscaledTime < _nextResolveTime)
                return;

            if (!HasSavedPreset())
            {
                _restoreRequested = false;
                return;
            }

            Scene mainMenu = SceneManager.GetSceneByName(MainMenuSceneName);
            if (!mainMenu.IsValid() || !mainMenu.isLoaded)
            {
                _restoreRequested = false;
                return;
            }

            GameObject menuCharacter = FindReadyMenuCharacter(mainMenu);
            if (menuCharacter != null)
            {
                int characterId = menuCharacter.GetInstanceID();
                string presetName = Config.character.lastLoadedPresetCharacter;
                if (_appliedCharacterId == characterId &&
                    string.Equals(_appliedPresetName, presetName, StringComparison.Ordinal))
                {
                    _restoreRequested = false;
                    return;
                }

                Memory.menuPlayer = menuCharacter;
                _restoreRequested = false;
                _applyRoutineRunning = true;
                MelonCoroutines.Start(ApplyPresetWhenReady(menuCharacter, characterId, presetName));
                return;
            }

            _resolveAttempts++;
            if (_resolveAttempts >= MaxResolveAttempts)
            {
                _restoreRequested = false;
                Log.Warning("[MainMenuCharacter] Timed out waiting for the menu character equip slots.");
                return;
            }

            _nextResolveTime = Time.unscaledTime + RetryIntervalSeconds;
        }

        private static IEnumerator ApplyPresetWhenReady(GameObject menuCharacter, int characterId, string presetName)
        {
            try
            {
                yield return new WaitForSeconds(InitialApplyDelaySeconds);

                if (menuCharacter == null ||
                    !HasSavedPreset() ||
                    !string.Equals(Config.character.lastLoadedPresetCharacter, presetName, StringComparison.Ordinal))
                {
                    yield break;
                }

                Memory.menuPlayer = menuCharacter;
                yield return Custom.LoadPreset(presetName);
                _appliedCharacterId = characterId;
                _appliedPresetName = presetName;
                Log.Msg($"[MainMenuCharacter] Restored preset '{presetName}' on {menuCharacter.name}.");
            }
            finally
            {
                _applyRoutineRunning = false;
            }
        }

        private static GameObject FindReadyMenuCharacter(Scene mainMenu)
        {
            GameObject eventRoot = FindCharacterRoot(_eventCharacter);
            if (IsReadyMenuCharacter(eventRoot))
                return eventRoot;

            GameObject[] roots = mainMenu.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                if (root == null)
                    continue;

                CustomCharacterManager manager = root.GetComponentInChildren<CustomCharacterManager>(true);
                GameObject candidate = FindCharacterRoot(manager != null ? manager.gameObject : null);
                if (IsReadyMenuCharacter(candidate))
                    return candidate;
            }

            // The menu rider can be instantiated before RoweMod finishes subscribing
            // to game events, or can live under an additive bootstrap scene. Search
            // loaded runtime managers as a bounded fallback so neither case relies on
            // receiving LocalMenuHumanSpawned.
            CustomCharacterManager[] managers = Resources.FindObjectsOfTypeAll<CustomCharacterManager>();
            foreach (CustomCharacterManager manager in managers)
            {
                GameObject candidate = FindCharacterRoot(manager != null ? manager.gameObject : null);
                if (IsReadyMenuCharacter(candidate))
                    return candidate;
            }

            return null;
        }

        private static GameObject FindCharacterRoot(GameObject character)
        {
            if (character == null)
                return null;

            Transform current = character.transform;
            while (current != null)
            {
                if (HasEquipSlots(current))
                    return current.gameObject;

                current = current.parent;
            }

            return character.transform.parent != null
                ? character.transform.parent.gameObject
                : character;
        }

        private static bool IsReadyMenuCharacter(GameObject character)
        {
            return character != null &&
                   character.scene.IsValid() &&
                   character.scene.isLoaded &&
                   HasEquipSlots(character.transform);
        }

        private static bool HasEquipSlots(Transform root)
        {
            return root != null &&
                   (root.Find("Physics Skeleton/EquipSlot_Body") != null ||
                    root.Find("Skeleton/EquipSlot_Body") != null);
        }

        private static bool HasSavedPreset()
        {
            return !string.IsNullOrWhiteSpace(Config.character.lastLoadedPresetCharacter) &&
                   !string.Equals(Config.character.lastLoadedPresetCharacter, "None", StringComparison.OrdinalIgnoreCase);
        }

        private static void RequestRestore(string source)
        {
            if (!HasSavedPreset())
                return;

            _restoreRequested = true;
            _resolveAttempts = 0;
            _nextResolveTime = Time.unscaledTime + RetryIntervalSeconds;
            Log.Msg($"[MainMenuCharacter] Queued preset restore from {source}.");
        }

        private static void Reset()
        {
            _restoreRequested = false;
            _applyRoutineRunning = false;
            _nextResolveTime = 0f;
            _resolveAttempts = 0;
            _appliedCharacterId = 0;
            _appliedPresetName = string.Empty;
            _eventCharacter = null;
        }
    }
}
