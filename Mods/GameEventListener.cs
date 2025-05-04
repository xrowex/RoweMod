using Il2CppMashBox.Core.Runtime.Events;
using UnityEngine;
using rowemod.Utils;
using UnityEngine.Events;
using rowemod.Mods;
using Physics = UnityEngine.Physics;
using System;

namespace rowemod
{
    public class GameEventListener : IDisposable
    {
        private GameEvent _playerSpawnEvent;
        private GameEvent _playerResetAtMarker;
        private GameEvent _playerCloseMenu;
        
        // Store delegate references so we can remove them later
        private UnityAction _spawnAction;
        private UnityAction _resetAction;
        private UnityAction _closeMenuAction;

        public void Initialize()
        {
            // Find the existing GameEvent instance
            _playerSpawnEvent = null;
            _playerResetAtMarker = null;
            GameEvent[] allEvents = Resources.FindObjectsOfTypeAll<GameEvent>();
            foreach (var ev in allEvents)
            {
                Log.Msg(ev.name);

                if(ev.name.Contains("GameEvent_OnResetAtMarker"))
                {
                    _playerResetAtMarker = ev;
                }

                if (ev.name.Contains("MainPlayerHumanSpawned"))
                {
                    _playerSpawnEvent = ev;
                    break;
                }

                if (ev.name.Contains("GameEvent_SimpleGameLoop_Paused_OnExit"))
                {
                    _playerCloseMenu = ev;
                }
                
            }

            //PLAYER SPAWNN
            if (_playerSpawnEvent == null)
            {
                Log.Error("PlayerSpawnEvent is null!");
                return;
            }

            Log.Msg("GameEvent_MainPlayerHumanSpawned found! Subscribing to event...");
            _spawnAction = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerSpawned);
            _playerSpawnEvent.OnRaise.AddListener(_spawnAction);
            
            


            //RESET AT MARKER
            if (_playerResetAtMarker == null)
            {
                Log.Error("PlayerResetAtMarker is null!");
                return;
            }   
            
            Log.Msg("GameEvent_OnResetAtMarker found! Subscribing to event...");
            _resetAction = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerResetAtMarker);
            _playerResetAtMarker.OnRaise.AddListener(_resetAction);



            //PLAYER CLOSES MENU
            if (_playerCloseMenu == null)
            {
                Log.Error("playerCloseMenu is null!");
                return;
            }

            Log.Msg("GameEvent_SimpleGameLoop_Paused_OnExit found! Subscribing to event...");
            _closeMenuAction = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerCloseMenu);
            _playerCloseMenu.OnRaise.AddListener(_closeMenuAction);

        }

        public void Dispose()
        {
            // Remove listeners
            if (_playerSpawnEvent != null && _spawnAction != null)
                _playerSpawnEvent.OnRaise.RemoveListener(_spawnAction);
                
            if (_playerResetAtMarker != null && _resetAction != null)
                _playerResetAtMarker.OnRaise.RemoveListener(_resetAction);
                
            if (_playerCloseMenu != null && _closeMenuAction != null)
                _playerCloseMenu.OnRaise.RemoveListener(_closeMenuAction);

            // Clear references
            _playerSpawnEvent = null;
            _playerResetAtMarker = null;
            _playerCloseMenu = null;
            _spawnAction = null;
            _resetAction = null;
            _closeMenuAction = null;
        }

        private void OnPlayerCloseMenu()
        {
            Log.Msg("GameEvent_SimpleGameLoop_Paused_OnExit!");
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

            var unityObj = _playerSpawnEvent._extraEventDataUnityObject;
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
                //PartTweaker.FindParts();
                
                //Memory.ToggleBmxFrames();
                
                // Load saved session marker if it exists
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
