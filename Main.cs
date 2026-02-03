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

[assembly: MelonInfo(typeof(rowemod.Main), "rowemod", "2.3.0", "rowe & nolew & holo & 8bitt", null)]
[assembly: MelonGame("Mash Games", "BMX Streets")]

namespace rowemod
{
    public class Main : MelonMod
    {
        public const string ModVersion = "2.3.0";
        public static bool playableSceneLoaded = false;
        private Coroutine _currentVehicleCheckCoroutine;
        private bool _isProcessingVehicleChange;
        
        

        public override void OnEarlyInitializeMelon()
        {
            CreateModDirectories();
            
        }
        
        
        public override void OnLateInitializeMelon()
        {
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


            previousWindowPosition = windowRect.position;

            if (File.Exists(cfgFile))
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

            try
            {
                Config.Save(); // creates a file if it doesn't exist
            }
            catch (Exception ex)
            {
                Log.Msg($"Failed to save configuration: {ex.Message}");
            }

            // Set up event listener
            Log.Msg("Starting game event listener...");
            GameEventListener listener = new GameEventListener();
            listener.Initialize();

            Log.Msg("Starting Bundle loading...");
            Memory.LoadAllAssetBundles();
            
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
            RemoteKillSwitch.CheckStatus();
            DisableMeshCombiners();
            Log.Msg($"Scene Loaded: {sceneName} (Index: {buildIndex})");

            cachedHDRCameras = UnityEngine.Camera.allCameras
                .Select(cam => cam.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>())
                .Where(hdrp => hdrp != null)
                .ToList();

            cachedVolumes = UnityEngine.Object.FindObjectsOfType<UnityEngine.Rendering.Volume>().ToList();
            
            //We set styles to false to reload each time scene is initialized
            stylesInitialized = false;



            if (sceneName != "MashBox_Main" || sceneName != "TitleScreen")
            {
                //load rowe logo if not loaded
                if(!isLogoLoaded)
                    MelonCoroutines.Start(LoadRoweLogo());
            
                // Reload assets from cached bundles
                Memory.ReloadAssetsFromCachedBundles();
            
                //Initialize bike materials
                //BikeMaterialsLoader.Initialize();
            
                // Re-initialize ObjectDropper on scene load
                //ObjectDropper.Initialize();
            }
            
        }
        
        
        
        public override void OnUpdate()
        {
            if (playableSceneLoaded && rMbCharacter)
            {
                // Updating ObjectDropper regardless of menu state
                ObjectDropper.Update();

                HandleMenuToggle();
                if (isOpen)
                {
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                }
            }
        } 
        public override void OnGUI()
        {
            if (!stylesInitialized)
            {
                InitializeStyles();
                stylesInitialized = true;
            }
                
            if (isOpen)
            {
                if (RemoteKillSwitch.isModEnabled)
                {
                    Menu.windowRect = GUI.Window(0, Menu.windowRect, (GUI.WindowFunction)Menu.DrawMenu, $"RoweMod v. {ModVersion}", Menu.windowStyle);
                    TrickMods.DrawTrickPickerPopup();
                }
                else
                {
                    Menu.windowRect = GUI.Window(0, Menu.windowRect, (GUI.WindowFunction)RemoteKillSwitch.DrawDisabledWindow, $"RoweMod v. {ModVersion}", Menu.windowStyle);
                }
            }
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
                kb.nKey.wasPressedThisFrame)
            {
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