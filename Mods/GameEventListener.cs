using Il2CppMashBox.Character.Scripts;
using Il2CppMashBox.Core.Runtime.Events;
using MelonLoader;
using rowemod.Utils;
using System.Collections;
using Il2CppFusion;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.Character;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Log = rowemod.Utils.Log;

namespace rowemod.Mods
{
    public class GameEventListener : MelonMod
    {
        private const string OpenMainMenuEventName =
            "GameEvent_TitleLoop_TransitionTrigger_OpenMainMenu";
        private const int IntroSkipMaxAttempts = 24;
        private const float IntroSkipInitialDelay = 0.5f;
        private const float IntroSkipRetryDelay = 0.25f;

        private static bool _introSkipRoutineRunning;
        private static bool _introSkipCompleted;

        private GameEvent _localGameplayHumanSpawnEvent;
        private GameEvent _localMenuHumanSpawnEvent;
        private GameEvent _playerResetAtMarker;
        private GameEvent _playerCloseReplay;
        private GameEvent _titleLoopGameplayEnter;
        private GameEvent _mainMenuOpen;

        public void Initialize()
        {
            // Find the existing GameEvent instance
            _localGameplayHumanSpawnEvent = null;
            _localMenuHumanSpawnEvent = null;
            _playerResetAtMarker = null;
            _playerCloseReplay = null;
            _titleLoopGameplayEnter = null;
            _mainMenuOpen = null;
            GameEvent[] allEvents = Resources.FindObjectsOfTypeAll<GameEvent>();
            foreach (var ev in allEvents)
            {
                if (ev.name.Contains("GameEvent_OnResetAtMarker"))
                {
                    _playerResetAtMarker = ev;
                }

                if (ev.name.Contains("LocalGameplayHumanSpawned"))
                {
                    _localGameplayHumanSpawnEvent = ev;
                }

                if (ev.name.Contains("LocalMenuHumanSpawned"))
                {
                    _localMenuHumanSpawnEvent = ev;
                }

                if (ev.name.Contains("GameEvent_TitleLoop_TransitionTrigger_CloseReplay"))
                {
                    _playerCloseReplay = ev;
                }

                if (ev.name.Contains("GameEvent_TitleLoop_Gameplay_OnEnter"))
                {
                    _titleLoopGameplayEnter = ev;
                }
                if (ev.name.Contains("GameEvent_UI_OnMenuOpen"))
                {
                    _mainMenuOpen = ev;
                }

            }

            ScheduleAutoIntroSkip("event listener initialization");

            //PLAYER SPAWNN
            if (_localGameplayHumanSpawnEvent == null)
            {
                Log.Error("PlayerSpawnEvent is null!");
                return;
            }

            Log.Msg("GameEvent_MainPlayerHumanSpawned found! Subscribing to event...");
            UnityAction action = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerSpawned);
            _localGameplayHumanSpawnEvent.OnRaise.AddListener(action);

            //MENU PLAYER SPAWN
            if (_localMenuHumanSpawnEvent == null)
            {
                Log.Error("MenuPlayerSpawnEvent is null!");
                return;
            }

            Log.Msg("GameEvent_LocalMenuHumanSpawned found! Subscribing to event...");
            UnityAction menuAction =
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnMenuPlayerSpawned);
            _localMenuHumanSpawnEvent.OnRaise.AddListener(menuAction);

            //RESET AT MARKER
            if (_playerResetAtMarker == null)
            {
                Log.Error("PlayerResetAtMarker is null!");
                return;
            }

            Log.Msg("GameEvent_OnResetAtMarker found! Subscribing to event...");
            UnityAction resetAction =
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerResetAtMarker);
            _playerResetAtMarker.OnRaise.AddListener(resetAction);

            //PLAYER CLOSES replay
            if (_playerCloseReplay == null)
            {
                Log.Error("playerCloseMenu is null!");
                return;
            }

            Log.Msg("GameEvent_TitleLoop_TransitionTrigger_CloseReplay found! Subscribing to event...");
            UnityAction closeReplayAction =
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerCloseReplay);
            _playerCloseReplay.OnRaise.AddListener(closeReplayAction);

            //GAMEPLAY ENTER
            if (_titleLoopGameplayEnter == null)
            {
                Log.Error("titleLoopGameplayEnter is null!");
                return;
            }

            Log.Msg("GameEvent_TitleLoop_Gameplay_OnEnter found! Subscribing to event...");
            UnityAction gameplayOnEnterAction =
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnTitleLoopGameplayOnEnter);
            _titleLoopGameplayEnter.OnRaise.AddListener(gameplayOnEnterAction);

            //main menu open
            if (_mainMenuOpen == null)
            {
                Log.Error("mainMenuOpen is null!");
                return;
            }

            Log.Msg("GameEvent_UI_OnMenuOpen found! Subscribing to event...");
            UnityAction mainMenuOpen =
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnMainMenuOpen);
            _mainMenuOpen.OnRaise.AddListener(mainMenuOpen);


        }

        public static void OnSceneInitialized(string sceneName)
        {
            if (!IsTitleScene(sceneName))
                return;

            ScheduleAutoIntroSkip($"scene '{sceneName}'");
        }

        private static void ScheduleAutoIntroSkip(string source)
        {
            if (!Config.autoSkipIntro || _introSkipCompleted || _introSkipRoutineRunning)
                return;

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (!IsTitleScene(activeSceneName))
            {
                Log.Msg(
                    $"[IntroSkip] Waiting for a title scene. " +
                    $"source={source}, activeScene='{activeSceneName}'.");
                return;
            }

            _introSkipRoutineRunning = true;
            MelonCoroutines.Start(AutoSkipIntroRoutine(source));
        }

        private static IEnumerator AutoSkipIntroRoutine(string source)
        {
            yield return new WaitForSecondsRealtime(IntroSkipInitialDelay);

            for (int attempt = 1; attempt <= IntroSkipMaxAttempts; attempt++)
            {
                if (!Config.autoSkipIntro || _introSkipCompleted)
                {
                    _introSkipRoutineRunning = false;
                    yield break;
                }

                string activeSceneName = SceneManager.GetActiveScene().name;
                if (!IsTitleScene(activeSceneName))
                {
                    _introSkipRoutineRunning = false;
                    yield break;
                }

                GameEvent openMainMenuEvent = FindGameEvent(OpenMainMenuEventName);
                if (openMainMenuEvent != null)
                {
                    try
                    {
                        openMainMenuEvent.Raise();
                        _introSkipCompleted = true;
                        _introSkipRoutineRunning = false;
                        Log.Msg(
                            $"[IntroSkip] Raised '{OpenMainMenuEventName}' " +
                            $"from {source} on attempt {attempt}.");
                        yield break;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning(
                            $"[IntroSkip] Failed to raise '{OpenMainMenuEventName}' " +
                            $"on attempt {attempt}: {ex.Message}");
                    }
                }

                yield return new WaitForSecondsRealtime(IntroSkipRetryDelay);
            }

            _introSkipRoutineRunning = false;
            Log.Warning(
                $"[IntroSkip] Could not find '{OpenMainMenuEventName}' " +
                $"after {IntroSkipMaxAttempts} attempts.");
        }

        private static GameEvent FindGameEvent(string eventName)
        {
            GameEvent[] events = Resources.FindObjectsOfTypeAll<GameEvent>();
            if (events == null)
                return null;

            foreach (GameEvent gameEvent in events)
            {
                if (gameEvent != null &&
                    string.Equals(gameEvent.name, eventName, System.StringComparison.Ordinal))
                {
                    return gameEvent;
                }
            }

            return null;
        }

        private static bool IsTitleScene(string sceneName)
        {
            return string.Equals(sceneName, "MashBox_Main", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(sceneName, "TitleScreen", System.StringComparison.OrdinalIgnoreCase);
        }

        private void OnMainMenuOpen()
        {
            if (!RemoteKillSwitched.isModEnabled)
                return;
            
            Log.Msg("GameEvent_UI_OnMenuOpen triggered!");
            //MelonCoroutines.Start(DelayedLoadPreset());
        }
        private void OnPlayerCloseReplay()
        {
            if (!RemoteKillSwitched.isModEnabled)
                return;
            
            Log.Msg("GameEvent_TitleLoop_TransitionTrigger_CloseReplay!");

            Memory.FindObjects(Memory.physicsDrivenCharacter);
            MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
            MelonCoroutines.Start(DelayedLoadPreset());
        }

        private void OnPlayerResetAtMarker()
        {
            if (!RemoteKillSwitched.isModEnabled)
                return;
            
            Misc.Update();
            Misc.ApplyPlayerUserNameTargetsVisibility(true);
            Physics.Update();
            MotorVehicleUtils.FindMxVehicleSettings();
        }


        /*
        private void OnTitleLoopOnExitMainMenu()
        {

        }

        private void OnTitleLoopTitleScreenExit()
        {

        }
        
        
        private void OnTitleLoopGameplayOnExit()
        {

        }
        */
        
        private void OnPlayerSpawned()
        {
            if (!RemoteKillSwitched.isModEnabled)
                return;
            
            Log.Msg("GameEvent_MainPlayerHumanSpawned triggered!");

            var unityObj = _localGameplayHumanSpawnEvent._extraEventDataUnityObject;
            if (unityObj == null)
            {
                Log.Error("Player object is null in event data!");
                return;
            }
            
            // Log the type to see what IL2CPP actually thinks it is.
            Log.Msg($"_extraEventDataUnityObject Type: {unityObj.GetType().FullName}");
            
            GameObject go;
            
            var allNetworkPlayers = UnityEngine.Object.FindObjectsOfType<NetworkPlayer>();
            var localPlayer = allNetworkPlayers.FirstOrDefault(player => player._isLocal);
            if (localPlayer != null)
            {
                Log.Msg($"Found local network player: {localPlayer.gameObject.name}");
                go = localPlayer.gameObject.GetComponentInChildren<LocalHumanPlayerSpawnEvent>().gameObject;
            }
            else
            {
                // If you have TryCast in your environment:
                go = unityObj.TryCast<GameObject>();
            }

            if (go != null)
            {
                if (!RemoteKillSwitched.isModEnabled) return;
                Log.Msg($"Player Spawned: {go.name}");
                Memory.physicsDrivenCharacter = go;
                Memory.rMbCharacter = go.transform.parent?.gameObject;
                Memory.gamePlayer = go;
            
                

                if (Memory.gamePlayer)
                {
                    Log.Msg($"gamePlayer set to : {Memory.gamePlayer.name}");
                }
                else
                {
                    Log.Error("gamePlayer is null!");
                }
                Main.playableSceneLoaded = true;

                Main.DisableMeshCombiners();
                
                
                Memory.FindObjects(go);
                rowemod.Challenges.MultiplayerChallengeManager.OnLocalPlayerSpawned(go);
                Memory.LoadAllAssetBundles();
                Misc.ApplyPlayerUserNameTargetsVisibility(true);
                PartTweaker.FindParts();
                GrindPoseEditor.ApplyConfigToRuntime(true);
                MotorVehicleUtils.FindMxVehicleSettings();
                MelonCoroutines.Start(DelayedLoadPreset());
                MelonCoroutines.Start(Memory.DelayedLoadEquippedParts());
                MelonCoroutines.Start(PartTweaker.DelayedUpdatePartTransforms());
                MelonCoroutines.Start(GrindPoseEditor.DelayedApplyConfigRoutine());
                BikeMaterialsLoader.Initialize();
                TrickMods.LoadTricksFromConfig();
                //Memory.UpdateCharacters();

                // Load a saved session marker if it exists
                if (!string.IsNullOrEmpty(Config.misc.customSessionMarker))
                {
                    if (Memory.sessionMarkers != null)
                    {
                        GameObject savedMarker = Memory.sessionMarkers
                            .FirstOrDefault(marker =>
                                marker != null && marker.name == Config.misc.customSessionMarker);

                        if (savedMarker != null)
                        {
                            Memory.ReplaceSessionMarkerWithPrefab(savedMarker);
                            Log.Msg($"Loaded saved session marker: {Config.misc.customSessionMarker}");
                        }
                        else
                        {
                            Log.Warning(
                                $"Saved session marker '{Config.misc.customSessionMarker}' not found in loaded assets.");
                        }
                    }
                    else
                    {
                        Log.Warning("sessionMarkers list is null.");
                    }
                }
            }
        }

        private void OnMenuPlayerSpawned()
        {
            if (!RemoteKillSwitched.isModEnabled)
                return;
            
            Log.Msg("GameEvent_LocalMenuHumanSpawned triggered!");

            var unityObj = _localMenuHumanSpawnEvent._extraEventDataUnityObject;
            if (unityObj == null)
            {
                Log.Error("Menu player object is null in event data!");
                return;
            }

            var go = unityObj.TryCast<GameObject>();
            if (go != null)
            {
                if (RemoteKillSwitched.isModEnabled)
                {
                    Log.Msg($"Menu Player Spawned: {go.name}");
                    Memory.physicsDrivenCharacter = go;
                    Memory.rMbCharacter = go.transform.parent?.gameObject;
                    Memory.menuPlayer = go;
                    if(Memory.menuPlayer)
                        Log.Msg($"menuPlayer set to : {Memory.menuPlayer.name}");
                    else
                    {
                        Log.Error("menuPlayer is null!");
                    }
                    Main.DisableMeshCombiners();
                    
                    
                    Memory.FindObjects(go);
                    Memory.LoadAllAssetBundles();
                    Misc.ApplyPlayerUserNameTargetsVisibility(true);
                    MelonCoroutines.Start(DelayedLoadPreset());
                    MelonCoroutines.Start(Memory.DelayedLoadEquippedParts());
                    MelonCoroutines.Start(PartTweaker.DelayedUpdatePartTransforms());
                    MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
                    BikeMaterialsLoader.Initialize();
                    //Memory.UpdateCharacters();
                }
            }
            else
            {
                Log.Error("Menu player object is null!");
            }
        }

        private void OnTitleLoopGameplayOnEnter()
        {
            if (RemoteKillSwitched.isModEnabled)
            {
                Log.Msg("GameEvent_TitleLoop_Gameplay_OnEnter triggered!");
                // Delayed bike materials load to bypass shop load
               
                PartTweaker.barListInitialized = false;
                PartTweaker.frameListInitialized = false;
                //These might not be needed each gameplay enter 
                MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
                MelonCoroutines.Start(DelayedLoadPreset());
                MelonCoroutines.Start(Memory.DelayedLoadEquippedParts());
                MelonCoroutines.Start(PartTweaker.DelayedUpdatePartTransforms());
                
                //TrickMods.LoadTricksFromConfig();
            }
            
        }
        
        public IEnumerator DelayedLoadPreset()
        {
            
            yield return new WaitForSeconds(4f); // Give it time to fully load scene stuff

            if (Config.character.lastLoadedPresetCharacter!=null & RemoteKillSwitched.isModEnabled)
            { 
                
                Log.Msg("Manually invoking LoadPreset on TheShop scene...");
                MelonCoroutines.Start(Custom.LoadPreset(Config.character.lastLoadedPresetCharacter)); // Now it should work
            }
            else
            {
                Log.Warning("Cannot load preset - missing reference or preset name.");
            }
        }
    }

}

