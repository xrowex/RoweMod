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
using Log = rowemod.Utils.Log;

namespace rowemod.Mods
{
    public class GameEventListener : MelonMod
    {
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
                UnityAction genericListener =
                    Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(() => OnAnyGameEvent(ev.name));
                ev.OnRaise.AddListener(genericListener);

                Log.Msg(ev.name);
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

        private void OnAnyGameEvent(string eventName)
        {
            Log.Msg($"[GameEventListener] Event raised: {eventName}");
        }
        private void OnMainMenuOpen()
        {
            if (!RemoteKillSwitch.isModEnabled)
                return;
            
            Log.Msg("GameEvent_UI_OnMenuOpen triggered!");
            //MelonCoroutines.Start(DelayedLoadPreset());
        }
        private void OnPlayerCloseReplay()
        {
            if (!RemoteKillSwitch.isModEnabled)
                return;
            
            Log.Msg("GameEvent_TitleLoop_TransitionTrigger_CloseReplay!");

            Memory.FindObjects(Memory.physicsDrivenCharacter);
            MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
            MelonCoroutines.Start(DelayedLoadPreset());
        }

        private void OnPlayerResetAtMarker()
        {
            if (!RemoteKillSwitch.isModEnabled)
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
            if (!RemoteKillSwitch.isModEnabled)
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
                if (!RemoteKillSwitch.isModEnabled) return;
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
            if (!RemoteKillSwitch.isModEnabled)
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
                if (RemoteKillSwitch.isModEnabled)
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
            if (RemoteKillSwitch.isModEnabled)
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

            if (Config.character.lastLoadedPresetCharacter!=null & RemoteKillSwitch.isModEnabled)
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

