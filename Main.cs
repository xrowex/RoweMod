using UnityEngine;
using MelonLoader;
using static rowemod.Menu;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using rowemod.Utils;
using rowemod.Mods;
using Log = rowemod.Utils.Log;
using System.Collections;
using Il2CppSteamworks;

[assembly: MelonInfo(typeof(rowemod.Main), "rowemod","1.4.0" , "rowe & nolew", null)]
[assembly: MelonGame("Mash Games", "BMX Streets")]

namespace rowemod
{
    public class Main : MelonMod
    {
        public static bool playableSceneLoaded = false;
        private Coroutine _currentVehicleCheckCoroutine;
        private bool _isProcessingVehicleChange;

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
                string categoryPath = Path.Combine(BikeMaterialsLoader.bikeRootPath, category.displayName);

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
            if (!Directory.Exists(BikeMaterialPreset.PresetDirectory))
                Directory.CreateDirectory(BikeMaterialPreset.PresetDirectory);

            // Clothing presets directory
            if (!Directory.Exists(ClothingPreset.PresetDirectory))
                Directory.CreateDirectory(ClothingPreset.PresetDirectory);
        }

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

        }
        
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            Log.Msg($"Scene Loaded: {sceneName} (Index: {buildIndex})");

            cachedHDRCameras = UnityEngine.Camera.allCameras
                .Select(cam => cam.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>())
                .Where(hdrp => hdrp != null)
                .ToList();

            cachedVolumes = UnityEngine.Object.FindObjectsOfType<UnityEngine.Rendering.Volume>().ToList();
            
            /*if(sceneName.Contains("TheShop"))
            {
                rMBCharacter = GameObject.Find("Custom Character");
                Memory.physicsDrivenCharacter = rMBCharacter;
                //Memory.rMBCharacter = go.transform.parent?.gameObject;
                //Main.playableSceneLoaded = true;
                Custom.UpdateAllPresets();
                Memory.FindObjects(rMBCharacter);
                Memory.SetupCameraSeatRelay();
                PartTweaker.FindParts();
            }*/
        }

        public override void OnFixedUpdate()
        {
            if (playableSceneLoaded && rMBCharacter)
            {
                Mods.Physics.FixedUpdate();
            }
        }
        public override void OnUpdate()
        {
            if (playableSceneLoaded && rMBCharacter)
            {
                HandleMenuToggle();
                if (isOpen)
                {
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                }
            }
        }
        private static System.Collections.IEnumerator DelayedUpdateCharacter()
        {
            yield return new WaitForSeconds(3f); // Wait 3 seconds for the scene to load fully
            Log.Msg("Running delayed UpdateCharacter()");
            Custom.LoadPreset(lastLoadedPresetCharacter);
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
                Menu.windowRect = GUI.Window(0, Menu.windowRect, (GUI.WindowFunction)Menu.DrawMenu, $"rowemod v. 1.4.0", Menu.windowStyle);
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
                        Mods.Camera.Update();
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

        private void HandleVehicleChanges()
        {
            if (vehicleChanger == null)
            {
                //Log.Error("vehicleChanger is not set.");
                return;
            }

            if (vehicleChanger._currentVehicle != lastVehicle)
            {
                Log.Msg($"Vehicle changed from '{lastVehicle}' to '{vehicleChanger._currentVehicle}'");

                // Cancel previous coroutine if still running
                if (_currentVehicleCheckCoroutine != null)
                {
                    MelonCoroutines.Stop(_currentVehicleCheckCoroutine);
                }

                _currentVehicleCheckCoroutine = (Coroutine?)MelonCoroutines.Start(ProcessVehicleChange());
                lastVehicle = vehicleChanger._currentVehicle;
            }
        }

        private IEnumerator ProcessVehicleChange()
        {
            _isProcessingVehicleChange = true;
            Log.Msg("Processing vehicle change...");
            // Wait for vehicle to fully spawn
            yield return new WaitForSeconds(2f);

            FindObjects(Memory.physicsDrivenCharacter);
            ValidateCurrentVehicleComponents();
            _isProcessingVehicleChange = false;
        }

        private static void ValidateCurrentVehicleComponents()
        {
            var currentVehicle = vehicleChanger?._spawnedVehicle;
            if (currentVehicle == null) return;

            switch (vehicleChanger._currentVehicle)
            {
                case 0://Proto_Dually
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                    break;

                case 1://Proto_Bike
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                    break;

                case 2://MTB
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                    Log.Msg("Starting DelayedUpdateCharacter(3) coroutine...");
                    MelonCoroutines.Start(DelayedUpdateCharacter());
                    break;

                case 3://Proto_Scoot
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                    Log.Msg("Starting DelayedUpdateCharacter(3) coroutine...");
                    MelonCoroutines.Start(DelayedUpdateCharacter());
                    break;

                case 4://BMX
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                    
                    Log.Msg("Starting DelayedUpdateCharacter(3) coroutine...");
                    MelonCoroutines.Start(DelayedUpdateCharacter());

                    BikeMaterialsLoader.Initialize();
                    Log.Msg("Starting DelayedApplySavedMaterials(5) coroutine...");
                    MelonCoroutines.Start(BikeMaterialsLoader.DelayedApplySavedMaterials());

                    break;

                case 5://Proto_Uni
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                    break;

                case 6://Proto_BMX_LONG Variant
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Mods.Camera.Update();
                    Log.Msg("Starting DelayedUpdateCharacter(3) coroutine...");
                    MelonCoroutines.Start(DelayedUpdateCharacter());
                    break;
            }
        }
    }
}