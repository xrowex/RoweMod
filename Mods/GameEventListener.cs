using Il2CppMashBox.Core.Runtime.Events;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Physics = UnityEngine.Physics;
using static rowemod.Mods.Misc;

namespace rowemod.Mods
{
    public class GameEventListener
    {
        private GameEvent _localGameplayHumanSpawnEvent;
        private GameEvent _localMenuHumanSpawnEvent;
        private GameEvent _playerResetAtMarker;
        private GameEvent _playerCloseReplay;
        private GameEvent _titleLoopGameplayEnter;
        public void Initialize()
        {
            // Find the existing GameEvent instance
            _localGameplayHumanSpawnEvent = null;
            _localMenuHumanSpawnEvent = null;
            _playerResetAtMarker = null;
            GameEvent[] allEvents = Resources.FindObjectsOfTypeAll<GameEvent>();
            foreach (var ev in allEvents)
            {

                UnityAction genericListener = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(() => OnAnyGameEvent(ev.name));
                ev.OnRaise.AddListener(genericListener);

                Log.Msg(ev.name);
                if (ev.name.Contains("GameEvent_OnResetAtMarker"))
                {
                    _playerResetAtMarker = ev;
                }

                if (ev.name.Contains("LocalGameplayHumanSpawned"))
                {
                    _localGameplayHumanSpawnEvent = ev;
                    break;
                }

                if (ev.name.Contains("LocalMenuHumanSpawned"))
                {
                    _localMenuHumanSpawnEvent = ev;
                }

                if (ev.name.Contains("GameEvent_TitleLoop_TransitionTrigger_OpenReplay"))
                {
                    _playerCloseReplay = ev;
                }

                if (ev.name.Contains("GameEvent_TitleLoop_Gameplay_OnEnter"))
                {
                    _titleLoopGameplayEnter = ev;
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
            UnityAction menuAction = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnMenuPlayerSpawned);
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

            Log.Msg("GameEvent_TitleLoop_TransitionTrigger_OpenReplay found! Subscribing to event...");
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
            UnityAction gameplayEnterAction =
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnTitleLoopGameplayEnter);
            _titleLoopGameplayEnter.OnRaise.AddListener(gameplayEnterAction);



        }
        private void OnAnyGameEvent(string eventName)
        {
            Log.Msg($"[GameEventListener] Event raised: {eventName}");
        }

        private void OnPlayerCloseReplay()
        {
            Log.Msg("GameEvent_TitleLoop_TransitionTrigger_CloseReplay!");
            /*
            Memory.FindObjects(Memory.physicsDrivenCharacter);
            MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
            */
        }
        private void OnPlayerResetAtMarker()
        {
            Misc.Update();
            Physics.Update();
            MotorVehicleUtils.FindMxVehicleSettings();
        }

        

        private void OnPlayerSpawned()
        {
            Log.Msg("GameEvent_MainPlayerHumanSpawned triggered!");

            var unityObj = _localGameplayHumanSpawnEvent._extraEventDataUnityObject;
            if (unityObj == null)
            {
                Log.Error("Player object is null in event data!");
                return;
            }

            // Log the type to see what IL2CPP actually thinks it is.
            Log.Msg($"_extraEventDataUnityObject Type: {unityObj.GetType().FullName}");

            

            // If you have TryCast in your environment:
            var go = unityObj.TryCast<GameObject>();
            if (go != null)
            {
                Log.Msg($"Player Spawned: {go.name}");
                Memory.physicsDrivenCharacter = go;
                Memory.rMbCharacter = go.transform.parent?.gameObject;
                Main.playableSceneLoaded = true;
                Custom.UpdateAllPresets();
                Memory.FindObjects(go);
                PartTweaker.FindParts();
                MotorVehicleUtils.FindMxVehicleSettings();
                ToggleEmoteInput();

                // Load a saved session marker if it exists
                if (!string.IsNullOrEmpty(Config.customSessionMarker))
                {
                    if (Memory.sessionMarkers != null)
                    {
                        GameObject savedMarker = Memory.sessionMarkers
                            .FirstOrDefault(marker => marker != null && marker.name == Config.customSessionMarker);

                        if (savedMarker != null)
                        {
                            Memory.ReplaceSessionMarkerWithPrefab(savedMarker);
                            Log.Msg($"Loaded saved session marker: {Config.customSessionMarker}");
                        }
                        else
                        {
                            Log.Warning($"Saved session marker '{Config.customSessionMarker}' not found in loaded assets.");
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
                Log.Msg($"Menu Player Spawned: {go.name}");
                Memory.physicsDrivenCharacter = go;
                Memory.rMbCharacter = go.transform.parent?.gameObject;
                Memory.FindObjects(go);
                PartTweaker.FindParts();
                MotorVehicleUtils.FindMxVehicleSettings();
            }
        }

        private void OnTitleLoopGameplayEnter()
        {
            Log.Msg("GameEvent_TitleLoop_Gameplay_OnEnter triggered!");
            // Delayed bike materials load to bypass shop load
            MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
            //Memory.FindObjects(Memory.physicsDrivenCharacter);
        }

    }
}
