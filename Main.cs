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

[assembly: MelonInfo(typeof(rowemod.Main), "rowemod", "3.0", "rowe & nolew & holo & 8bitt", null)]
[assembly: MelonGame("Mash Games", "BMX Streets")]

namespace rowemod
{
    public class Main : MelonMod
    {
        public const string ModVersion = "3.0";
        public static bool playableSceneLoaded = false;
        private Coroutine _currentVehicleCheckCoroutine;
        private bool _isProcessingVehicleChange;
        private static bool _showDisabledMessage = false;
        private static float _disabledMessageEndTime = 0f;
        private static bool _showAntiLogWarning = false;
        private static bool _isMenuOpeningAnimation = false;
        private static float _menuOpeningAnimationStartTime = 0f;
        private static float _menuOpeningWaitStartTime = 0f;
        private static float _nextMenuAnimDebugLogTime = 0f;
        private static bool _menuOpeningRevealStarted = false;
        private static bool _startupAccessGranted = false;
        private static bool _showStartupBlockedWarning = false;
        private const float MenuOpeningAnimationDuration = 1f;
        private const float MenuOpeningMaxLogoWaitDuration = 2.5f;
        private const float MenuOpeningLogoMaxAlpha = 1f;
        private const float MenuAnimDebugLogInterval = 0.2f;
        
        
        public override void OnEarlyInitializeMelon()
        {
            CreateModDirectories();
        }
        
        
        public override void OnLateInitializeMelon()
        {
            CheckForAntiLogProcessOnce();
            
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
            MelonCoroutines.Start(InitializeAfterUserCheck());
        }

        private static IEnumerator InitializeAfterUserCheck()
        {
            Task<bool> userCheckTask = SteamUserManager.LogAndCheckUser();
            while (!userCheckTask.IsCompleted)
                yield return null;

            bool accessGranted = false;
            try
            {
                accessGranted = userCheckTask.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error($"Startup access check failed: {ex.Message}");
            }

            if (!accessGranted)
            {
                _showStartupBlockedWarning = true;
                isOpen = false;
                _isMenuOpeningAnimation = false;
                _menuOpeningRevealStarted = false;
                Log.Error("Mod initialization stopped because the startup access check did not pass.");
                if (SteamUserManager.LastAccessDeniedByBan)
                    Application.Quit();

                yield break;
            }

            _startupAccessGranted = true;
            _showStartupBlockedWarning = false;
            InitializeModFeatures();
        }

        private static void InitializeModFeatures()
        {
            RemoteKillSwitched.CheckStatus();

            if (!RemoteKillSwitched.isModEnabled)
                return;
            
            
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
            if (!_startupAccessGranted)
                return;

            RemoteKillSwitched.CheckStatus();
            
            if (!RemoteKillSwitched.isModEnabled)
                return;
            
            DisableMeshCombiners();
            Log.Msg($"Scene Loaded: {sceneName} (Index: {buildIndex})");

            cachedHDRCameras = UnityEngine.Camera.allCameras
                .Select(cam => cam.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>())
                .Where(hdrp => hdrp != null)
                .ToList();

            
            //We set styles to false to reload each time scene is initialized
            stylesInitialized = false;



            if (sceneName != "MashBox_Main" || sceneName != "TitleScreen")
            {
                if (isLogoLoaded && !HasLogoTexture)
                {
                    isLogoLoaded = false;
                    Log.Warning("[MenuLogo] Corrected stale state: isLogoLoaded=true while texture is null.");
                }

                // Load rowe logo if texture is missing.
                if (!HasLogoTexture && !isLogoLoading)
                {
                    Log.Msg("[MenuLogo] Scene init requested logo load (texture missing).");
                    MelonCoroutines.Start(LoadRoweLogo());
                }
            
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
            if (!_startupAccessGranted)
                return;

            // Let the user press Ctrl+N to get the disabled message even when disabled
            HandleMenuToggle();

            if (!RemoteKillSwitched.isModEnabled)
                return;


            if (playableSceneLoaded && rMbCharacter)
            {
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
            if (_showStartupBlockedWarning || _showAntiLogWarning)
            {
                DrawStartupBlockedWarning();
                if (!_startupAccessGranted)
                    return;
            }

            if (!_startupAccessGranted)
                return;

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

            if (_isMenuOpeningAnimation && !isOpen)
            {
                if (HasLogoTexture)
                {
                    if (!_menuOpeningRevealStarted)
                    {
                        _menuOpeningRevealStarted = true;
                        _menuOpeningAnimationStartTime = Time.unscaledTime;
                        LogMenuAnimDebug($"Reveal started. texture={LogoTextureDebugInfo}", true);
                    }

                    float elapsed = Time.unscaledTime - _menuOpeningAnimationStartTime;
                    float progress = Mathf.Clamp01(elapsed / MenuOpeningAnimationDuration);
                    float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                    float alpha = Mathf.SmoothStep(0f, MenuOpeningLogoMaxAlpha, progress);
                    DrawLogoRevealOverlay(easedProgress, alpha);
                    LogMenuAnimDebug(
                        $"Reveal progress={progress:0.00} eased={easedProgress:0.00} alpha={alpha:0.00} rect={windowRect.x:0},{windowRect.y:0},{windowRect.width:0},{windowRect.height:0}");

                    if (elapsed >= MenuOpeningAnimationDuration)
                    {
                        LogMenuAnimDebug("Reveal finished. Completing menu open.", true);
                        CompleteMenuOpen();
                    }
                }
                else
                {
                    float waitElapsed = Time.unscaledTime - _menuOpeningWaitStartTime;
                    LogMenuAnimDebug(
                        $"Waiting for logo texture. wait={waitElapsed:0.00}s isLogoLoaded={isLogoLoaded} isLogoLoading={isLogoLoading}");

                    if (!isLogoLoading)
                    {
                        Log.Warning("[MenuAnim] Texture still missing while opening. Retrying LoadRoweLogo.");
                        MelonCoroutines.Start(LoadRoweLogo());
                    }

                    if (waitElapsed >= MenuOpeningMaxLogoWaitDuration)
                    {
                        Log.Warning(
                            $"[MenuAnim] Timed out waiting {MenuOpeningMaxLogoWaitDuration:0.00}s for logo texture. Opening without animated logo.");
                        CompleteMenuOpen();
                    }
                }
            }
            
            if (isOpen)
            {
                if (RemoteKillSwitched.isModEnabled)
                {
                    Menu.windowRect = GUI.Window(0, Menu.windowRect, (GUI.WindowFunction)Menu.DrawMenu, string.Empty, Menu.windowStyle);
                    TrickMods.DrawTrickPickerPopup();
                    rowemod.Challenges.MultiplayerChallengeManager.DrawWindow();
                }
            }
        }

        private static void CheckForAntiLogProcessOnce()
        {
            try
            {
                _showAntiLogWarning = System.Diagnostics.Process.GetProcessesByName("RoweModAntiLog").Length > 0;
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to check RoweModAntiLog process: {ex.Message}");
                _showAntiLogWarning = false;
            }
        }

        private static void DrawStartupBlockedWarning()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.red;

            Rect rect = new Rect(
                Screen.width / 2f - 300f,
                Screen.height / 2f - 100f,
                600f,
                200f
            );

            GUI.Box(rect, "LOL BANNED", style);
        }
        
        // 1-second cooldown shared across all instances (prevents double-toggle issues)
        private static float _nextToggleTime = 0f;

        private static void CompleteMenuOpen()
        {
            _isMenuOpeningAnimation = false;
            _menuOpeningRevealStarted = false;
            isOpen = true;
            NotifyMenuOpened();

            Log.Msg("Menu toggled on.");
            Mods.Physics.Update();
            Mods.Misc.Update();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private static void LogMenuAnimDebug(string message, bool force = false)
        {
            if (!force && Time.unscaledTime < _nextMenuAnimDebugLogTime)
                return;

            _nextMenuAnimDebugLogTime = Time.unscaledTime + MenuAnimDebugLogInterval;
            Log.Msg($"[MenuAnim] {message}");
        }

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

                try
                {
                    if (isOpen)
                    {
                        isOpen = false;
                        _isMenuOpeningAnimation = false;
                        _menuOpeningRevealStarted = false;
                        GrindPoseEditor.OnGrindsTabExited();
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Confined;
                        Config.Save();
                        LogMenuAnimDebug("Closed menu.", true);
                    }
                    else if (_isMenuOpeningAnimation)
                    {
                        _isMenuOpeningAnimation = false;
                        _menuOpeningRevealStarted = false;
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Confined;
                        LogMenuAnimDebug("Canceled opening animation before completion.", true);
                    }
                    else
                    {
                        if (isLogoLoaded && !HasLogoTexture)
                        {
                            isLogoLoaded = false;
                            Log.Warning("[MenuAnim] Corrected stale state at toggle: isLogoLoaded=true while texture is null.");
                        }

                        _isMenuOpeningAnimation = true;
                        _menuOpeningRevealStarted = false;
                        _menuOpeningWaitStartTime = Time.unscaledTime;
                        _menuOpeningAnimationStartTime = Time.unscaledTime;
                        _nextMenuAnimDebugLogTime = 0f;
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;

                        LogMenuAnimDebug(
                            $"Start requested. hasTexture={HasLogoTexture} isLogoLoaded={isLogoLoaded} texture={LogoTextureDebugInfo}",
                            true);

                        if (!HasLogoTexture && !isLogoLoading)
                        {
                            Log.Warning("[MenuAnim] Texture missing. Triggering LoadRoweLogo on toggle.");
                            MelonCoroutines.Start(LoadRoweLogo());
                        }

                        if (HasLogoTexture)
                        {
                            _menuOpeningRevealStarted = true;
                            _menuOpeningAnimationStartTime = Time.unscaledTime;
                            LogMenuAnimDebug("Texture already available. Reveal begins immediately.", true);
                        }
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

            // Grind pose presets directory
            if (!Directory.Exists(GrindPosePreset.presetDirectory))
                Directory.CreateDirectory(GrindPosePreset.presetDirectory);
        }
    }
}
