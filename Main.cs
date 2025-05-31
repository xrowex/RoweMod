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
using Il2CppSteamworks;

[assembly: MelonInfo(typeof(rowemod.Main), "rowemod", "1.9.8", "rowe & nolew & holo", null)]
[assembly: MelonGame("Mash Games", "BMX Streets")]

namespace rowemod
{
    public class Main : MelonMod
    {
        public const string ModVersion = "1.9.8";
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



            Log.Msg("Starting Bundle loading...");
            Memory.LoadAllAssetBundles();

            // Set up event listener
            Log.Msg("Starting game event listener...");
            GameEventListener listener = new GameEventListener();
            listener.Initialize();

            // Initializing ObjectDropper
            ObjectDropper.Initialize();
        }
        
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            Log.Msg($"Scene Loaded: {sceneName} (Index: {buildIndex})");

            cachedHDRCameras = UnityEngine.Camera.allCameras
                .Select(cam => cam.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>())
                .Where(hdrp => hdrp != null)
                .ToList();

            cachedVolumes = UnityEngine.Object.FindObjectsOfType<UnityEngine.Rendering.Volume>().ToList();
            
            //We set styles to false to reload each time scene is initialized
            stylesInitialized = false;
            
            //load rowe logo
            MelonCoroutines.Start(LoadRoweLogo());
            
            //disable test mod in game
            /*foreach (var obj in GameObject.FindObjectsOfType<GameObject>())
            {
                if (obj.name == "TestRoweMod" || obj.name == "TestRoweMod(Clone)")
                {
                    obj.SetActive(false);
                    Log.Msg($"Disabled: {obj.name}");
                }
            }*/
            
            if (sceneName.Contains("TheShop"))
            {
                RoweCustomCharacter.GetDefaultCharacter();

                Memory.rMbCharacter = GameObject.Find("Custom Character");
                Memory.physicsDrivenCharacter = Memory.rMbCharacter.GetComponentsInChildren<CustomCharacterManager>().FirstOrDefault()?.gameObject;
                if (Memory.physicsDrivenCharacter == null)
                    return;
                
                Custom.UpdateAllPresets();
                
                MelonCoroutines.Start(DelayedLoadPreset());
                MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());
            }

            // Reload assets from cached bundles
            Memory.ReloadAssetsFromCachedBundles();

            // Re-initialize ObjectDropper on scene load
            ObjectDropper.Initialize();
        }
        
        private IEnumerator DelayedLoadPreset()
        {
            yield return new WaitForSeconds(3f); // Give it time to fully load scene stuff

            if (!string.IsNullOrEmpty(lastLoadedPresetCharacter) && Memory.rMbCharacter != null)
            {
                Log.Msg("Manually invoking LoadPreset on TheShop scene...");
                Memory.FindObjects(Memory.rMbCharacter); // Refresh all references
                Custom.LoadPreset(lastLoadedPresetCharacter); // Now it should work
            }
            else
            {
                Log.Warning("Cannot load preset - missing reference or preset name.");
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
                Menu.windowRect = GUI.Window(0, Menu.windowRect, (GUI.WindowFunction)Menu.DrawMenu, $"RoweMod v. {ModVersion}", Menu.windowStyle);
            }
        }
        
        private void HandleMenuToggle()
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
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