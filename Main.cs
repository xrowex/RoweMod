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
using Il2CppMashBox.Addons.GameLoop;
using Il2CppMashBox.Core.Runtime.Events;
using Il2CppMashBox.Development.RandD.PlayFabTesting;
using Il2CppMashBoxBridge.Common.Sys;
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
        private static bool _startupAccessCheckStarted = false;
        private static bool _showStartupBlockedWarning = false;
        private static bool _showPrivacyDisclaimer = false;
        private static bool _showPrivacyDisclaimerConfirmation = false;
        private static bool _autoSkipIntroStarted = false;
        private static bool _autoSkipIntroCompleted = false;
        private const float MenuOpeningAnimationDuration = 1f;
        private const float MenuOpeningMaxLogoWaitDuration = 2.5f;
        private const float MenuOpeningLogoMaxAlpha = 1f;
        private const float MenuAnimDebugLogInterval = 0.2f;
        private const float AutoSkipIntroTimeoutSeconds = 10f;
        private const float AutoSkipIntroBundleLoadMaxDelaySeconds = 4f;
        private const string TitleScreenSceneName = "TitleScreen";
        private const string OpenMainMenuEventName = "GameEvent_TitleLoop_TransitionTrigger_OpenMainMenu";
        private const string TitleScreenPlayEventName = "GameEvent_TitleScreen_Play";
        
        
        public override void OnEarlyInitializeMelon()
        {
            CreateModDirectories();
        }
        
        
        public override void OnLateInitializeMelon()
        {
            CheckForAntiLogProcessOnce();

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
            _startupAccessCheckStarted = true;
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

            StartAutoSkipIntroIfEnabled();
            MelonCoroutines.Start(LoadAssetBundlesAfterStartup());
        }

        private static void StartAutoSkipIntroIfEnabled()
        {
            if (_autoSkipIntroStarted || !Config.autoSkipIntro)
                return;

            _autoSkipIntroStarted = true;
            MelonCoroutines.Start(AutoSkipIntroRoutine());
        }

        private static IEnumerator LoadAssetBundlesAfterStartup()
        {
            if (Config.autoSkipIntro)
            {
                float deadline = Time.unscaledTime + AutoSkipIntroBundleLoadMaxDelaySeconds;
                while (!_autoSkipIntroCompleted && Time.unscaledTime < deadline)
                    yield return null;
            }

            Log.Msg("Starting Bundle loading...");
            Memory.LoadAllAssetBundles();
        }

        private static IEnumerator AutoSkipIntroRoutine()
        {
            float deadline = Time.unscaledTime + AutoSkipIntroTimeoutSeconds;
            float nextAttemptTime = 0f;
            bool raisedOpenMainMenu = false;
            bool raisedTitleScreenPlay = false;
            bool requestedMainMenuTransition = false;
            bool forcedMainMenuState = false;
            bool calledSkipTo = false;

            Log.Msg("[IntroSkip] Waiting for title screen skip target.");

            while (Time.unscaledTime < deadline)
            {
                if (!RemoteKillSwitched.isModEnabled || !Config.autoSkipIntro)
                {
                    _autoSkipIntroCompleted = true;
                    yield break;
                }

                if (IsGameLoopInState(GameState.MainMenu))
                {
                    Log.Msg("[IntroSkip] Main menu is active.");
                    _autoSkipIntroCompleted = true;
                    yield break;
                }

                if (Time.unscaledTime < nextAttemptTime)
                {
                    yield return null;
                    continue;
                }

                bool titleScreenReady = IsSceneLoaded(TitleScreenSceneName) ||
                                        IsGameLoopInState(GameState.TitleScreen) ||
                                        TryFindGameEvent(OpenMainMenuEventName, out _);
                if (!titleScreenReady)
                {
                    yield return null;
                    continue;
                }

                if (!raisedOpenMainMenu && TryRaiseGameEvent(OpenMainMenuEventName))
                {
                    raisedOpenMainMenu = true;
                    nextAttemptTime = Time.unscaledTime + 0.5f;
                    yield return null;
                    continue;
                }

                if (!raisedTitleScreenPlay && TryRaiseGameEvent(TitleScreenPlayEventName))
                {
                    raisedTitleScreenPlay = true;
                    nextAttemptTime = Time.unscaledTime + 0.25f;
                    yield return null;
                    continue;
                }

                if (!requestedMainMenuTransition && TryRequestMainMenuTransition())
                {
                    requestedMainMenuTransition = true;
                    nextAttemptTime = Time.unscaledTime + 0.5f;
                    yield return null;
                    continue;
                }

                if (!forcedMainMenuState && TryForceMainMenuState())
                {
                    forcedMainMenuState = true;
                    nextAttemptTime = Time.unscaledTime + 0.5f;
                    yield return null;
                    continue;
                }

                if (!calledSkipTo && TryCallSkipIntro())
                {
                    calledSkipTo = true;
                    nextAttemptTime = Time.unscaledTime + 0.5f;
                    yield return null;
                    continue;
                }

                nextAttemptTime = Time.unscaledTime + 0.25f;
                yield return null;
            }

            Log.Warning("[IntroSkip] Could not skip intro before timeout.");
            _autoSkipIntroCompleted = true;
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            try
            {
                int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
                for (int i = 0; i < sceneCount; i++)
                {
                    UnityEngine.SceneManagement.Scene scene =
                        UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[IntroSkip] Scene check failed: {ex.Message}");
            }

            return false;
        }

        private static bool IsGameLoopInState(GameState state)
        {
            try
            {
                GameLoopManager manager = FindGameLoopManager();
                return manager != null && manager.State == state;
            }
            catch
            {
                return false;
            }
        }

        private static GameLoopManager FindGameLoopManager()
        {
            try
            {
                if (GameLoopManager.Instance != null)
                    return GameLoopManager.Instance;
            }
            catch
            {
                // Instance can be unavailable while the title loop is still bootstrapping.
            }

            try
            {
                GameLoopManager[] managers = Resources.FindObjectsOfTypeAll<GameLoopManager>();
                if (managers != null)
                {
                    foreach (GameLoopManager manager in managers)
                    {
                        if (manager != null)
                            return manager;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[IntroSkip] Failed to find GameLoopManager: {ex.Message}");
            }

            return null;
        }

        private static bool TryRequestMainMenuTransition()
        {
            try
            {
                GameLoopManager manager = FindGameLoopManager();
                if (manager == null)
                    return false;

                if (manager.State == GameState.MainMenu)
                    return true;

                GameLoopTransitionRequest request =
                    GameLoopTransitionRequest.State(GameState.MainMenu, "rowemod auto skip intro");
                manager.RequestTransition(request);
                Log.Msg("[IntroSkip] Requested MainMenu transition through GameLoopManager.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[IntroSkip] MainMenu transition request failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryForceMainMenuState()
        {
            try
            {
                GameLoopManager manager = FindGameLoopManager();
                if (manager == null)
                    return false;

                manager.SetGameStateToMainMenu();
                Log.Msg("[IntroSkip] Forced GameLoopManager state to MainMenu.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[IntroSkip] Forcing MainMenu state failed: {ex.Message}");
                return false;
            }
        }

        private static bool TryRaiseGameEvent(string eventName)
        {
            try
            {
                if (!TryFindGameEvent(eventName, out GameEvent gameEvent))
                    return false;

                gameEvent.Raise();
                Log.Msg($"[IntroSkip] Raised {eventName}.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[IntroSkip] Raising {eventName} failed: {ex.Message}");
            }

            return false;
        }

        private static bool TryFindGameEvent(string eventName, out GameEvent foundEvent)
        {
            foundEvent = null;

            try
            {
                GameEvent[] allEvents = Resources.FindObjectsOfTypeAll<GameEvent>();
                if (allEvents == null)
                    return false;

                foreach (GameEvent gameEvent in allEvents)
                {
                    if (gameEvent == null || !string.Equals(gameEvent.name, eventName, StringComparison.Ordinal))
                        continue;

                    foundEvent = gameEvent;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[IntroSkip] Finding {eventName} failed: {ex.Message}");
            }

            return false;
        }

        private static bool TryCallSkipIntro()
        {
            try
            {
                Il2CppBMXS.TitleScreen.SkipIntro[] skippers =
                    Resources.FindObjectsOfTypeAll<Il2CppBMXS.TitleScreen.SkipIntro>();
                if (skippers == null)
                    return false;

                foreach (Il2CppBMXS.TitleScreen.SkipIntro skipper in skippers)
                {
                    if (skipper == null)
                        continue;

                    skipper.SkipTo();
                    Log.Msg("[IntroSkip] Called SkipIntro.SkipTo() fallback.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[IntroSkip] SkipTo fallback failed: {ex.Message}");
            }

            return false;
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

            rowemod.Challenges.MultiplayerChallengeManager.OnSceneInitialized();
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
                BikePoseEditor.Update();
                if (!misc.showPlayerUserNameTargets)
                {
                    Mods.Misc.ApplyPlayerUserNameTargetsVisibility();
                }

                rowemod.Challenges.MultiplayerChallengeManager.Update();

                if (isOpen)
                {
                    Mods.Physics.Update();
                    //Mods.Misc.Update();
                }
            }
        }

        public override void OnDeinitializeMelon()
        {
            rowemod.Challenges.MultiplayerChallengeManager.Shutdown();
        }
        
        public override void OnGUI()
        {
            if (_showStartupBlockedWarning || _showAntiLogWarning)
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
            {
                DrawPrivacyDisclaimerConfirmation(previousBackgroundColor);
            }
            else
            {
                DrawPrivacyDisclaimerPrompt(previousBackgroundColor);
            }

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
                        BikePoseEditor.OnTabExited();
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
