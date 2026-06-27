using System;
using UnityEngine;
using MelonLoader;
using static rowemod.Menu;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using rowemod.Utils;
using rowemod.Mods;
using Log = rowemod.Utils.Log;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Il2CppMashBox.Character.Scripts;
using Il2CppMashBox.Core.Runtime.Events;
using Il2CppMashBox.Development.RandD.PlayFabTesting;
using Il2CppSteamworks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[assembly: MelonInfo(typeof(rowemod.Main), "rowemod", "3.0.7", "rowe & nolew & holo & 8bitt", null)]
[assembly: MelonGame("Mash Games", "BMX Streets")]
namespace rowemod
{
    public class Main : MelonMod
    {
        public const string ModVersion = "3.0.7";
        private static readonly bool EnablePieMenu = true;
        public static bool playableSceneLoaded = false;
        private Coroutine _currentVehicleCheckCoroutine;
        private bool _isProcessingVehicleChange;
        private bool _replayInputPatchApplied;
        private static bool _showDisabledMessage = false;
        private static float _disabledMessageEndTime = 0f;
        private static bool _startupAccessGranted = false;
        private static bool _startupAccessCheckStarted = false;
        private static bool _showStartupBlockedWarning = false;
        private static bool _showStartupRetryWarning = false;
        private static string _startupRetryMessage = string.Empty;
        private static bool _showPrivacyDisclaimer = false;
        private static bool _showPrivacyDisclaimerConfirmation = false;


        public override void OnEarlyInitializeMelon()
        {
            CreateModDirectories();
            HarmonyInstance.PatchAll();
        }
        
        
        public override void OnLateInitializeMelon()
        {
            LoadStartupConfig();
            if (!Config.disclaimerAccepted)
            {
                _showPrivacyDisclaimer = true;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                Log.Msg("Startup paused until the Steam ID logging disclaimer is accepted.");
                return;
            }

            StartStartupAccessCheck();
        }

        private static void LoadStartupConfig()
        {
            try
            {
                Config.Load();
            }
            catch (Exception ex)
            {
                Log.Msg($"Failed to load startup configuration: {ex.Message}");
            }
        }

        private static void StartStartupAccessCheck()
        {
            if (_startupAccessCheckStarted || _startupAccessGranted)
                return;

            if (!SteamAPI.IsSteamRunning())
            {
                ShowStartupRetryWarning("Steam is not running. Start Steam, then retry.");
                return;
            }

            /*SteamAPI.Shutdown(); // Ensure SteamAPI resets*/
            if (!SteamAPI.Init())
            {
                ShowStartupRetryWarning("Failed to initialize Steamworks. Make sure Steam is running, then retry.");
                return;
            }

            Log.Msg("Steamworks initialized successfully.");
            _startupAccessCheckStarted = true;
            _showStartupRetryWarning = false;
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
                _startupAccessCheckStarted = false;
                isOpen = false;
                Log.Error("Mod initialization stopped because the startup access check did not pass.");
                if (SteamUserManager.LastAccessDeniedByBan)
                {
                    _showStartupBlockedWarning = true;
                    _showStartupRetryWarning = false;
                }
                else
                {
                    ShowStartupRetryWarning(SteamUserManager.LastAccessFailureReason);
                }

                yield break;
            }

            _startupAccessGranted = true;
            _showStartupBlockedWarning = false;
            _showStartupRetryWarning = false;
            InitializeModFeatures();
        }

        private static void ShowStartupRetryWarning(string message)
        {
            _startupAccessCheckStarted = false;
            _showStartupRetryWarning = true;
            _startupRetryMessage = string.IsNullOrWhiteSpace(message)
                ? "Could not verify RoweMod access. Check your connection, then retry."
                : message;
            Log.Msg($"Startup access check can be retried: {_startupRetryMessage}");
        }

        private static void InitializeModFeatures()
        {
            RemoteKillSwitched.CheckStatus();
            if (!RemoteKillSwitched.isModEnabled)
                return;

            previousWindowPosition = windowRect.position;

            try
            {
                Config.Load();
            }
            catch (Exception ex)
            {
                Log.Msg($"Failed to load configuration: {ex.Message}");
            }

            try
            {
                Config.Save();
            }
            catch (Exception ex)
            {
                Log.Msg($"Failed to save configuration: {ex.Message}");
            }

            AutoUpdater.Initialize();
            LogRuntimeDiagnosticsSettings();

            // Set up event listener
            Log.Msg("Starting game event listener...");
            GameEventListener listener = new GameEventListener();
            listener.Initialize();
            if (Config.challengeRuntimeSettings.trickHooksEnabled)
                rowemod.Challenges.MultiplayerChallengeManager.InstallTrickCapturePatch();
            else
                Log.Msg("[MPChallenge][Diagnostics] Trick hooks disabled by config.");

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
            if (!_startupAccessGranted)
                return;

            RemoteKillSwitched.CheckStatus();
            
            if (!RemoteKillSwitched.isModEnabled)
                return;

            if (Config.challengeRuntimeSettings.enabled)
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

                PieMenu.PreloadAssets();
            
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
            if (!_startupAccessGranted)
                return;

            // Let the user press Ctrl+N to get the disabled message even when disabled
            HandleMenuToggle();

            if (!RemoteKillSwitched.isModEnabled)
                return;

            if (playableSceneLoaded && rMbCharacter)
            {
                if (!_replayInputPatchApplied)
                {
                    ReplayInputPatch.ApplyLatePatch(HarmonyInstance);
                    _replayInputPatchApplied = true;
                }

                if (EnablePieMenu)
                {
                    PieMenu.Update();
                }

                ReplayInputPatch.Update();

                ControllerMenuInput.Update();

                if (Config.challengeRuntimeSettings.enabled)
                {
                    rowemod.Challenges.MultiplayerChallengeManager.Update();
                }

                BikePoseEditor.Update();

                if (!EnablePieMenu || (!PieMenu.IsOpen && !PieMenu.ConsumedInputThisFrame))
                {
                    ObjectDropper.Update();
                }

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
            if (_showStartupBlockedWarning)
            {
                DrawStartupBlockedWarning();
                if (!_startupAccessGranted)
                    return;
            }

            if (_showPrivacyDisclaimer)
            {
                DrawPrivacyDisclaimer();
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
                if (Config.challengeRuntimeSettings.enabled)
                    rowemod.Challenges.MultiplayerChallengeManager.DrawWindow();
                if (EnablePieMenu)
                    PieMenu.Draw();

                AutoUpdater.DrawUpdatePrompt(isOpen);
            }
        }

        private static void DrawPrivacyDisclaimer()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Color previousColor = GUI.color;
            Color previousBackgroundColor = GUI.backgroundColor;

            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (_showPrivacyDisclaimerConfirmation)
                DrawPrivacyDisclaimerConfirmation(previousBackgroundColor);
            else
                DrawPrivacyDisclaimerPrompt(previousBackgroundColor);

            GUI.backgroundColor = previousBackgroundColor;
            GUI.color = previousColor;
        }

        private static void DrawPrivacyDisclaimerPrompt(Color previousBackgroundColor)
        {
            float width = Mathf.Min(620f, Screen.width - 40f);
            float height = 300f;
            Rect rect = new Rect(
                (Screen.width - width) / 2f,
                (Screen.height - height) / 2f,
                width,
                height);

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(24, 24, 24, 24)
            };
            GUI.Box(rect, GUIContent.none, boxStyle);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            bodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            GUI.Label(new Rect(rect.x + 24f, rect.y + 24f, rect.width - 48f, 38f), "Official Disclaimer", titleStyle);
            GUI.Label(
                new Rect(rect.x + 42f, rect.y + 82f, rect.width - 84f, 110f),
                "I log your Steam ID and Steam username.\n\nClick AGREE to use RoweMod and you will not see this disclaimer again. Click DISAGREE if you do not agree.",
                bodyStyle);

            GUIStyle buttonStyle = CreateDisclaimerButtonStyle();
            float buttonWidth = 180f;
            float buttonHeight = 46f;
            float spacing = 24f;
            float buttonsY = rect.yMax - 72f;
            float agreeX = rect.center.x - buttonWidth - (spacing / 2f);
            float disagreeX = rect.center.x + (spacing / 2f);

            GUI.backgroundColor = new Color(0.1f, 0.55f, 0.22f, 1f);
            if (GUI.Button(new Rect(agreeX, buttonsY, buttonWidth, buttonHeight), "AGREE", buttonStyle))
            {
                AcceptPrivacyDisclaimer();
            }

            GUI.backgroundColor = new Color(0.72f, 0.12f, 0.12f, 1f);
            if (GUI.Button(new Rect(disagreeX, buttonsY, buttonWidth, buttonHeight), "DISAGREE", buttonStyle))
            {
                _showPrivacyDisclaimerConfirmation = true;
            }

            GUI.backgroundColor = previousBackgroundColor;
        }

        private static void DrawPrivacyDisclaimerConfirmation(Color previousBackgroundColor)
        {
            float width = Mathf.Min(500f, Screen.width - 40f);
            float height = 220f;
            Rect rect = new Rect(
                (Screen.width - width) / 2f,
                (Screen.height - height) / 2f,
                width,
                height);

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(24, 24, 24, 24)
            };
            GUI.Box(rect, GUIContent.none, boxStyle);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            bodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            GUI.Label(new Rect(rect.x + 24f, rect.y + 24f, rect.width - 48f, 34f), "Are you sure?", titleStyle);
            GUI.Label(new Rect(rect.x + 42f, rect.y + 70f, rect.width - 84f, 52f), "The mod will close.", bodyStyle);

            GUIStyle buttonStyle = CreateDisclaimerButtonStyle();
            float buttonWidth = 150f;
            float buttonHeight = 42f;
            float spacing = 22f;
            float buttonsY = rect.yMax - 66f;
            float cancelX = rect.center.x - buttonWidth - (spacing / 2f);
            float disagreeX = rect.center.x + (spacing / 2f);

            GUI.backgroundColor = new Color(0.34f, 0.36f, 0.4f, 1f);
            if (GUI.Button(new Rect(cancelX, buttonsY, buttonWidth, buttonHeight), "CANCEL", buttonStyle))
            {
                _showPrivacyDisclaimerConfirmation = false;
            }

            GUI.backgroundColor = new Color(0.72f, 0.12f, 0.12f, 1f);
            if (GUI.Button(new Rect(disagreeX, buttonsY, buttonWidth, buttonHeight), "DISAGREE", buttonStyle))
            {
                Log.Msg("Steam ID logging disclaimer declined. Closing game.");
                Application.Quit();
            }

            GUI.backgroundColor = previousBackgroundColor;
        }

        private static GUIStyle CreateDisclaimerButtonStyle()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            return buttonStyle;
        }

        private static void AcceptPrivacyDisclaimer()
        {
            Config.disclaimerAccepted = true;
            Config.Save();
            _showPrivacyDisclaimer = false;
            _showPrivacyDisclaimerConfirmation = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
            Log.Msg("Steam ID logging disclaimer accepted.");
            StartStartupAccessCheck();
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

            GUI.Box(rect, "ACCESS DENIED", style);
        }

        public override void OnDeinitializeMelon()
        {
            rowemod.Challenges.MultiplayerChallengeManager.Shutdown();
            if (EnablePieMenu)
                PieMenu.Cleanup();
        }

        private static void LogRuntimeDiagnosticsSettings()
        {
            Log.Msg(
                "[Diagnostics] " +
                $"challengeEnabled={Config.challengeRuntimeSettings.enabled}, " +
                $"challengeNetworking={Config.challengeRuntimeSettings.networkingEnabled}, " +
                $"challengeAutoOpen={Config.challengeRuntimeSettings.autoOpenEnabled}, " +
                $"challengeTrickHooks={Config.challengeRuntimeSettings.trickHooksEnabled}.");
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
                ToggleRoweModMenu();
            }
        }

        public static void OpenRoweModMenu()
        {
            SetRoweModMenuOpen(true);
        }

        public static void CloseRoweModMenu()
        {
            SetRoweModMenuOpen(false);
        }

        public static void ToggleRoweModMenu()
        {
            SetRoweModMenuOpen(!isOpen);
        }

        private static void SetRoweModMenuOpen(bool open)
        {
            if (isOpen == open)
                return;

            isOpen = open;

            try
            {
                if (isOpen)
                {
                    Log.Msg("Menu toggled on.");
                    Mods.Physics.Update();
                    Mods.Misc.Update();
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    ControllerMenuInput.SetGameplayInputBlocked(true);
                }
                else
                {
                    ControllerMenuInput.SetGameplayInputBlocked(false);
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
