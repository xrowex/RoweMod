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
    /// Discovers and restores the current RoweMod outfit on the non-networked character
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
        private static GameObject _eventCharacter;
        private static int _generation;

        public static void OnSceneInitialized(string sceneName)
        {
            if (string.Equals(sceneName, MainMenuSceneName, StringComparison.OrdinalIgnoreCase))
            {
                Reset();
                RequestRestore("scene initialized");
            }
            else if (!SceneManager.GetSceneByName(MainMenuSceneName).isLoaded)
                Reset();
        }

        public static void NotifyMenuCharacterSpawned(GameObject character)
        {
            if (character == null)
                return;

            GameObject root = FindCharacterRoot(character);
            if (root != _eventCharacter)
            {
                _generation++;
                _applyRoutineRunning = false;
            }
            _eventCharacter = root;
            if (IsReadyMenuCharacter(root)) Memory.menuPlayer = root;
            RequestRestore("menu character spawned");
        }

        public static void Update()
        {
            if (!_restoreRequested || _applyRoutineRunning || Time.unscaledTime < _nextResolveTime)
                return;

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
                Memory.menuPlayer = menuCharacter;
                if (_appliedCharacterId == characterId)
                {
                    _restoreRequested = false;
                    return;
                }

                Memory.menuPlayer = menuCharacter;
                _restoreRequested = false;
                _applyRoutineRunning = true;
                MelonCoroutines.Start(ApplyPresetWhenReady(menuCharacter, characterId, _generation, Custom.SelectionRevision));
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

        private static IEnumerator ApplyPresetWhenReady(GameObject menuCharacter, int characterId, int generation, long selectionRevision)
        {
            try
            {
                yield return new WaitForSecondsRealtime(InitialApplyDelaySeconds);

                if (generation != _generation || !IsReadyMenuCharacter(menuCharacter) ||
                    !SceneManager.GetSceneByName(MainMenuSceneName).isLoaded)
                {
                    yield break;
                }

                Memory.menuPlayer = menuCharacter;
                // A manual selection or Restore Game Outfit during the delay wins.
                if (Custom.SelectionRevision == selectionRevision)
                    Custom.RestoreMenuSelection(menuCharacter);
                _appliedCharacterId = characterId;
                Log.Msg($"[MainMenuCharacter] Menu character ready: {menuCharacter.name}. Outfit requests target this preview only.");
            }
            finally
            {
                if (generation == _generation) _applyRoutineRunning = false;
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
                   string.Equals(character.scene.name, MainMenuSceneName, StringComparison.OrdinalIgnoreCase) &&
                   HasEquipSlots(character.transform);
        }

        private static bool HasEquipSlots(Transform root)
        {
            return root != null &&
                   (root.Find("Physics Skeleton/EquipSlot_Body") != null ||
                    root.Find("Skeleton/EquipSlot_Body") != null);
        }

        private static void RequestRestore(string source)
        {
            _restoreRequested = true;
            _resolveAttempts = 0;
            _nextResolveTime = Time.unscaledTime + RetryIntervalSeconds;
            Log.Msg($"[MainMenuCharacter] Queued menu character discovery from {source}.");
        }

        private static void Reset()
        {
            _generation++;
            _restoreRequested = false;
            _applyRoutineRunning = false;
            _nextResolveTime = 0f;
            _resolveAttempts = 0;
            _appliedCharacterId = 0;
            _eventCharacter = null;
        }
    }
}
