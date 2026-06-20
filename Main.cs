using UnityEngine;
using MelonLoader;
using static rowemod.Menu;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using rowemod.Utils;
using rowemod.Mods;
using Log = rowemod.Utils.Log;
using System.Collections;
using Il2CppMashBox.Character.Scripts;
using Il2CppMashBox.Core.Runtime.Events;
using Il2CppMashBox.Development.RandD.PlayFabTesting;
using Il2CppSteamworks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[assembly: MelonInfo(typeof(rowemod.Main), "rowemod", "3.1", "rowe & nolew & holo & 8bitt", null)]
[assembly: MelonGame("Mash Games", "BMX Streets")]

namespace rowemod
{
    public class Main : MelonMod
    {
        public const string ModVersion = "3.1";
        private static readonly bool EnablePieMenu = false;
        public static bool playableSceneLoaded = false;
        private Coroutine _currentVehicleCheckCoroutine;
        private bool _isProcessingVehicleChange;
        private static bool _showDisabledMessage = false;
        private static float _disabledMessageEndTime = 0f;
        

        public override void OnEarlyInitializeMelon()
        {
            CreateModDirectories();
            HarmonyInstance.PatchAll();
        }
        
        
        public override void OnLateInitializeMelon()
        {
            RemoteKillSwitched.CheckStatus();
            
            if (!SteamAPI.IsSteamRunning())
            {
                Log.Msg("Steam is not running. Cannot retrieve Steam ID.");
                return;
            }

            /*SteamAPI.Shutdown(); // Ensure SteamAPI resets*/
            if (!SteamAPI.Init())
            {
                Log.Msg("Failed to initialize Steamworks!");
                return;
            }

            Log.Msg("Steamworks initialized successfully.");
            SteamUserManager.LogAndCheckUser();

            if (!RemoteKillSwitched.isModEnabled)
                return;

            previousWindowPosition = windowRect.position;

            bool configExists = File.Exists(cfgFile);
            if (configExists)
            {

                try
                {
                    Config.Load();
                }
                catch (Exception ex)
                {
                    Log.Msg($"Failed to load configuration: {ex.Message}");
                }
            }

            if (!configExists)
            {
                try
                {
                    Config.Save(); // creates a file if it doesn't exist
                }
                catch (Exception ex)
                {
                    Log.Msg($"Failed to save configuration: {ex.Message}");
                }
            }

            AutoUpdater.Initialize();

            // Set up event listener
            Log.Msg("Starting game event listener...");
            GameEventListener listener = new GameEventListener();
            listener.Initialize();
            rowemod.Challenges.MultiplayerChallengeManager.InstallTrickCapturePatch();

            Log.Msg("Bundle loading deferred until player context is available.");
            
        }
        
        public static void DisableMeshCombiners()
        {
            var smCombiner = UnityEngine.Object.FindObjectsOfType<SkinnedMeshCombiner>();
            if(smCombiner!=null)
                foreach (var smc in smCombiner)
                    smc.enabled = false;
            
            var combinedMesh = GameObject.Find("CombinedMesh");

            if(combinedMesh!=null)
                combinedMesh.SetActive(false);
        }
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            RemoteKillSwitched.CheckStatus();
            
            if (!RemoteKillSwitched.isModEnabled)
                return;

            rowemod.Challenges.MultiplayerChallengeManager.OnSceneInitialized();
            GameEventListener.OnSceneInitialized(sceneName);
            
            DisableMeshCombiners();
            Log.Msg($"Scene Loaded: {sceneName} (Index: {buildIndex})");

            cachedHDRCameras = UnityEngine.Camera.allCameras
                .Select(cam => cam.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>())
                .Where(hdrp => hdrp != null)
                .ToList();

            
            //We set styles to false to reload each time scene is initialized
            stylesInitialized = false;



            if (sceneName != "MashBox_Main" && sceneName != "TitleScreen")
            {
                
                //load rowe logo if not loaded
                if(!isLogoLoaded)
                    MelonCoroutines.Start(LoadRoweLogo());
            
                // Reload assets from cached bundles
                if (Memory.loadedBundles.Count > 0)
                    Memory.ReloadAssetsFromCachedBundles();
            
                //Initialize bike materials
                //BikeMaterialsLoader.Initialize();
            
                // Re-initialize ObjectDropper on scene load
                //ObjectDropper.Initialize();
            }
            
        }
        
        
        public override void OnUpdate()
        {
            // Let the user press Ctrl+N to get the disabled message even when disabled
            HandleMenuToggle();

            if (!RemoteKillSwitched.isModEnabled)
                return;

            if (playableSceneLoaded && rMbCharacter)
            {
                if (EnablePieMenu)
                    PieMenu.Update();

                rowemod.Challenges.MultiplayerChallengeManager.Update();
                BikePoseEditor.Update();

                if (!EnablePieMenu || (!PieMenu.IsOpen && !PieMenu.ConsumedInputThisFrame))
                    ObjectDropper.Update();

                if (!misc.showPlayerUserNameTargets)
                {
                    Mods.Misc.ApplyPlayerUserNameTargetsVisibility();
                }

                if (isOpen)
                {
                    Mods.Physics.Update();
                    //Mods.Misc.Update();
                }
            }
        }
        
        public override void OnGUI()
        {
            if (_showDisabledMessage)
            {
                if (Time.unscaledTime > _disabledMessageEndTime)
                {
                    _showDisabledMessage = false;
                }
                else
                {
                    GUIStyle style = new GUIStyle(GUI.skin.box);
                    style.fontSize = 18;
                    style.alignment = TextAnchor.MiddleCenter;
                    style.normal.textColor = Color.red;

                    Rect rect = new Rect(
                        Screen.width / 2f - 150,
                        40,
                        300,
                        40
                    );

                    GUI.Box(rect, "Mod is disabled.", style);
                }
            }

            
            if (!stylesInitialized)
            {
                InitializeStyles();
                stylesInitialized = true;
            }
            
            if (isOpen)
            {
                if (RemoteKillSwitched.isModEnabled)
                {
                    Menu.windowRect = GUI.Window(0, Menu.windowRect, (GUI.WindowFunction)Menu.DrawMenu, $"RoweMod v. {ModVersion}", Menu.windowStyle);
                    TrickMods.DrawTrickPickerPopup();
                    ObjectDropper.DrawNotPlaceableWarning();
                }
            }

            if (RemoteKillSwitched.isModEnabled)
            {
                rowemod.Challenges.MultiplayerChallengeManager.DrawWindow();
                if (EnablePieMenu)
                    PieMenu.Draw();

                AutoUpdater.DrawUpdatePrompt(isOpen);
            }
        }

        public override void OnDeinitializeMelon()
        {
            rowemod.Challenges.MultiplayerChallengeManager.Shutdown();
            PieMenu.Cleanup();
        }
        
        // 1-second cooldown shared across all instances (prevents double-toggle issues)
        private static float _nextToggleTime = 0f;

        private void HandleMenuToggle()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Cooldown guard (use unscaled time so pause/timeScale doesn't affect it)
            if (Time.unscaledTime < _nextToggleTime)
                return;

            if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) &&
                kb.nKey.isPressed)
            {
                if (!RemoteKillSwitched.isModEnabled)
                {
                    // Still apply cooldown so holding the keys doesn't keep extending the timer every frame
                    _nextToggleTime = Time.unscaledTime + 1f; // 1 second cooldown

                    if (!_showDisabledMessage || Time.unscaledTime > _disabledMessageEndTime)
                    {
                        _showDisabledMessage = true;
                        _disabledMessageEndTime = Time.unscaledTime + 3f; // show for 3 seconds
                    }

                    return;
                }


                    
                _nextToggleTime = Time.unscaledTime + 1f; // 1 second cooldown

                isOpen = !isOpen;

                try
                {
                    if (isOpen)
                    {
                        Log.Msg("Menu toggled on.");
                        Mods.Physics.Update();
                        Mods.Misc.Update();
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                    }
                    else
                    {
                        GrindPoseEditor.OnGrindsTabExited();
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Confined;
                        Config.Save();
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }
        void CreateModDirectories()
        {
            // Main mod folder
            if (!Directory.Exists(modFolder))
                Directory.CreateDirectory(modFolder);

            // Bundles folder
            if (!Directory.Exists(bundlesFolderPath))
                Directory.CreateDirectory(bundlesFolderPath);

            // character root path
            if (!Directory.Exists(Custom.characterRootPath))
                Directory.CreateDirectory(Custom.characterRootPath);

            // Bike material folders
            foreach (var category in BikeMaterialsLoader.categories.Values)
            {
                string categoryPath = Path.Combine(BikeMaterialsLoader.BikeRootPath, category.displayName);

                if (!Directory.Exists(categoryPath))
                    Directory.CreateDirectory(categoryPath);
            }

            // Clothing slot folders
            foreach (var slot in Enum.GetValues(typeof(Custom.Slot)))
            {
                var slotPath = Path.Combine(Custom.characterRootPath, slot.ToString());

                if (!Directory.Exists(slotPath))
                    Directory.CreateDirectory(slotPath);
            }

            // Bike presets directory
            if (!Directory.Exists(BikeMaterialPreset.presetDirectory))
                Directory.CreateDirectory(BikeMaterialPreset.presetDirectory);

            // Clothing presets directory
            if (!Directory.Exists(ClothingPreset.presetDirectory))
                Directory.CreateDirectory(ClothingPreset.presetDirectory);
        }
    }
}
