using Il2CppMashBox.Core.Runtime.Events;
using UnityEngine;
using System;
using rowemod.Utils;
using UnityEngine.Events;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMashBox.Addons.ContentManagment;
using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;
using rowemod.Mods;
using rowemod.Utils;
using MelonLoader;
using Il2CppModIOBrowser;
using rowemod.Mods;
using UnityEngine.Animations;
using Camera = UnityEngine.Camera;

namespace rowemod
{
    public class GameEventListener
    {
        private GameEvent playerSpawnEvent;
        private GameEvent playerResetAtMarker;
        public void Initialize()
        {
            // Find the existing GameEvent instance
            playerSpawnEvent = null;
            playerResetAtMarker = null;
            GameEvent[] allEvents = Resources.FindObjectsOfTypeAll<GameEvent>();
            foreach (var ev in allEvents)
            {
                Log.Msg(ev.name);
                if(ev.name.Contains("GameEvent_OnResetAtMarker"))
                {
                    playerResetAtMarker = ev;
                }
                if (ev.name.Contains("MainPlayerHumanSpawned"))
                {
                    playerSpawnEvent = ev;
                    break;
                }
                
            }

            if (playerSpawnEvent == null)
            {
                Log.Error("PlayerSpawnEvent is null!");
                return;
            }

            Log.Msg("GameEvent_MainPlayerHumanSpawned found! Subscribing to event...");


            
            // IL2CPP-Safe Delegate Registration
            UnityAction action = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerSpawned);
            playerSpawnEvent.OnRaise.AddListener(action);
            
            
            if (playerResetAtMarker == null)
            {
                Log.Error("PlayerResetAtMarker is null!");
                return;
            }
            
            Log.Msg("GameEvent_OnResetAtMarker found! Subscribing to event...");
            
            UnityAction resetAction =
                Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityAction>(OnPlayerResetAtMarker);
            playerResetAtMarker.OnRaise.AddListener(resetAction);
        }

        private void OnPlayerResetAtMarker()
        {
            Misc.Update();
            Mods.Physics.Update();
        }

        private void OnPlayerSpawned()
        {
            Log.Msg("GameEvent_MainPlayerHumanSpawned triggered!");

            var unityObj = playerSpawnEvent._extraEventDataUnityObject;
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
                Memory.rMBCharacter = go.transform.parent?.gameObject;
                Main.playableSceneLoaded = true;
                Memory.ToggleBMXFrames();
                Custom.UpdateAllPresets();
                Memory.FindObjects(go);
                Memory.SetupCameraSeatRelay();
                PartTweaker.FindParts();
                if (!string.IsNullOrEmpty(Config.customSessionMarker))
                {
                    GameObject savedMarker = Memory.sessionMarkers.FirstOrDefault(marker => marker.name == Config.customSessionMarker);
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
                    Log.Msg("No saved session marker found in config.");
                }  
                
                
                /*var chain = Memory.rMBCharacter.transform.FindDeepChild("Chain");
                if (chain != null)
                {
                    chain.gameObject.SetActive(true);
                    var anchor = chain.GetComponent<Anchor>();
                    var aimConstraint = chain.GetComponent<AimConstraint>();

                    if (anchor != null) anchor.enabled = true;
                    if (aimConstraint != null) aimConstraint.enabled = true;

                    chain.localEulerAngles = new Vector3(353.9754f, 359.6659f, 0.2001f);
                }*/

            }
        }


    }
}
