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

            
            if (!Directory.Exists(modFolder))
            {
                Directory.CreateDirectory(modFolder);
            }
        
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
                
                Vector2 currentWindowPosition = Menu.windowRect.position;

                // Render the main menu
                Menu.windowRect = GUI.Window(0, Menu.windowRect, (GUI.WindowFunction)Menu.DrawMenu, $"<b>rowemod v. 1.4.0</b>", Menu.windowStyle);


                // Handle dragging logic
                if (Menu.windowRect.position != currentWindowPosition)
                {
                    if (!Menu.isDraggingWindow)
                    {
                        Menu.isDraggingWindow = true;
                    }
                }
                else
                {
                    if (Menu.isDraggingWindow)
                    {
                        Menu.isDraggingWindow = false;
                    }
                }

                Menu.previousWindowPosition = Menu.windowRect.position;

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