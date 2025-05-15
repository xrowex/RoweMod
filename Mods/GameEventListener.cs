using Il2CppMashBox.Core.Runtime.Events;
using UnityEngine;
using rowemod.Utils;
using UnityEngine.Events;
using rowemod.Mods;
using Physics = UnityEngine.Physics;

namespace rowemod
{
    public class GameEventListener
    {
        private GameEvent _localGameplayHumanSpawnEvent;
        private GameEvent _localMenuHumanSpawnEvent;
        private GameEvent _playerResetAtMarker;
        private GameEvent _playerCloseReplay;
        public void Initialize()
        {
            // Find the existing GameEvent instance
            _localGameplayHumanSpawnEvent = null;
            _localMenuHumanSpawnEvent = null;
            _playerResetAtMarker = null;
            GameEvent[] allEvents = Resources.FindObjectsOfTypeAll<GameEvent>();
            foreach (var ev in allEvents)
            {
                Log.Msg(ev.name);

                if(ev.name.Contains("GameEvent_OnResetAtMarker"))
                {
                    _playerResetAtMarker = ev;
                }

                if (ev.name.Contains("LocalGameplayHumanSpawned"))
                {
                    _localGameplayHumanSpawnEvent = ev;
                    break;
                }

                if (ev.name.Contains("GameEvent_TitleLoop_TransitionTrigger_OpenReplay"))
                {
                    _playerCloseReplay = ev;
                }
                if (ev.name.Contains("GameEvent_LocalMenuHumanSpawned"))
                {
                    _localMenuHumanSpawnEvent = ev;
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
            _playerResetAtMarker.OnRaise.AddListener(closeReplayAction);

        }
        private void OnPlayerCloseReplay()
        {
            Log.Msg("GameEvent_TitleLoop_TransitionTrigger_OpenReplay!");
            Memory.FindObjects(Memory.physicsDrivenCharacter);
        }
        private void OnPlayerResetAtMarker()
        {
            Misc.Update();
            Mods.Physics.Update();
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
                
                //Memory.ToggleBmxFrames();
                
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


    }
}
