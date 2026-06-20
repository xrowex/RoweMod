using UnityEngine;
using MelonLoader;
using static rowemod.Menu;
using static rowemod.Config;
using static rowemod.Utils.Memory;
using rowemod.Utils;
using rowemod.Mods;
using Log = rowemod.Utils.Log;
using System.Collections;
using System.Diagnostics;
using Il2CppMashBox.Character.Scripts;
using Il2CppMashBox.Core.Runtime.Events;
using Il2CppMashBox.Development.RandD.PlayFabTesting;
using Il2CppSteamworks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[assembly: MelonInfo(typeof(rowemod.Main), "rowemod", "3.0.1", "rowe & nolew & holo & 8bitt", null)]
[assembly: MelonGame("Mash Games", "BMX Streets")]
namespace rowemod
{
    public class Main : MelonMod
    {
        public const string ModVersion = "3.0.1";
        private static readonly bool EnablePieMenu = false;
        public static bool playableSceneLoaded = false;
        private Coroutine _currentVehicleCheckCoroutine;
        private bool _isProcessingVehicleChange;
        private static bool _showDisabledMessage = false;
        private static float _disabledMessageEndTime = 0f;
        private static readonly PerfCounter _perfRoweUpdate = new PerfCounter("rowemod.update");
        private static readonly PerfCounter _perfPieMenu = new PerfCounter("pieMenu");
        private static readonly PerfCounter _perfChallenge = new PerfCounter("challenge");
        private static readonly PerfCounter _perfBikePose = new PerfCounter("bikePose");
        private static readonly PerfCounter _perfObjectDropper = new PerfCounter("objectDropper");
        private static readonly PerfCounter _perfNameTargets = new PerfCounter("nameTargets");
        private static readonly PerfCounter _perfPhysics = new PerfCounter("physics");
        private static float _nextPerformanceLogTime = 0f;
        

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
            long updateStart = BeginPerfSample();
            try
            {
                // Let the user press Ctrl+N to get the disabled message even when disabled
                HandleMenuToggle();

                if (!RemoteKillSwitched.isModEnabled)
                    return;

                if (playableSceneLoaded && rMbCharacter)
                {
                    if (EnablePieMenu)
                    {
                        long pieStart = BeginPerfSample();
                        PieMenu.Update();
                        EndPerfSample(_perfPieMenu, pieStart);
                    }

                    if (Config.challengeRuntimeSettings.enabled)
                    {
                        long challengeStart = BeginPerfSample();
                        rowemod.Challenges.MultiplayerChallengeManager.Update();
                        EndPerfSample(_perfChallenge, challengeStart);
                    }

                    long bikePoseStart = BeginPerfSample();
                    BikePoseEditor.Update();
                    EndPerfSample(_perfBikePose, bikePoseStart);

                    if (!EnablePieMenu || (!PieMenu.IsOpen && !PieMenu.ConsumedInputThisFrame))
                    {
                        long objectDropperStart = BeginPerfSample();
                        ObjectDropper.Update();
                        EndPerfSample(_perfObjectDropper, objectDropperStart);
                    }

                    if (!misc.showPlayerUserNameTargets)
                    {
                        long nameTargetStart = BeginPerfSample();
                        Mods.Misc.ApplyPlayerUserNameTargetsVisibility();
                        EndPerfSample(_perfNameTargets, nameTargetStart);
                    }

                    if (isOpen)
                    {
                        long physicsStart = BeginPerfSample();
                        Mods.Physics.Update();
                        EndPerfSample(_perfPhysics, physicsStart);
                        //Mods.Misc.Update();
                    }
                }
            }
            finally
            {
                EndPerfSample(_perfRoweUpdate, updateStart);
                MaybeLogPerformanceSummary();
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
                if (Config.challengeRuntimeSettings.enabled)
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

        private static void LogRuntimeDiagnosticsSettings()
        {
            Log.Msg(
                "[Diagnostics] " +
                $"perfEnabled={Config.performanceSettings.enabled}, " +
                $"perfInterval={Config.performanceSettings.logIntervalSeconds:0.#}s, " +
                $"perfSpike={Config.performanceSettings.spikeThresholdMs:0.###}ms, " +
                $"challengeEnabled={Config.challengeRuntimeSettings.enabled}, " +
                $"challengeNetworking={Config.challengeRuntimeSettings.networkingEnabled}, " +
                $"challengeAutoOpen={Config.challengeRuntimeSettings.autoOpenEnabled}, " +
                $"challengeTrickHooks={Config.challengeRuntimeSettings.trickHooksEnabled}.");
        }

        private static long BeginPerfSample()
        {
            return Config.performanceSettings.enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        private static void EndPerfSample(PerfCounter counter, long startTicks)
        {
            if (startTicks == 0L)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            counter.Add(elapsedTicks * 1000.0 / Stopwatch.Frequency);
        }

        private static void MaybeLogPerformanceSummary()
        {
            if (!Config.performanceSettings.enabled)
                return;

            float interval = Mathf.Max(1f, Config.performanceSettings.logIntervalSeconds);
            if (Time.unscaledTime < _nextPerformanceLogTime)
                return;

            _nextPerformanceLogTime = Time.unscaledTime + interval;
            if (!_perfRoweUpdate.HasSamples)
                return;

            float threshold = Mathf.Max(0f, Config.performanceSettings.spikeThresholdMs);
            string prefix = _perfRoweUpdate.MaxMs >= threshold && threshold > 0f
                ? "[Perf][Spike]"
                : "[Perf]";
            Log.Msg(
                $"{prefix} " +
                _perfRoweUpdate.FormatAndReset() + " | " +
                _perfPieMenu.FormatAndReset() + " | " +
                _perfChallenge.FormatAndReset() + " | " +
                _perfBikePose.FormatAndReset() + " | " +
                _perfObjectDropper.FormatAndReset() + " | " +
                _perfNameTargets.FormatAndReset() + " | " +
                _perfPhysics.FormatAndReset());
        }

        private sealed class PerfCounter
        {
            private readonly string _name;
            private int _samples;
            private double _totalMs;
            private double _maxMs;

            public PerfCounter(string name)
            {
                _name = name;
            }

            public bool HasSamples => _samples > 0;
            public double MaxMs => _maxMs;

            public void Add(double elapsedMs)
            {
                _samples++;
                _totalMs += elapsedMs;
                if (elapsedMs > _maxMs)
                    _maxMs = elapsedMs;
            }

            public string FormatAndReset()
            {
                if (_samples == 0)
                    return $"{_name}:samples=0";

                double averageMs = _totalMs / _samples;
                string value =
                    $"{_name}:samples={_samples},avg={averageMs:0.###}ms,max={_maxMs:0.###}ms";
                _samples = 0;
                _totalMs = 0.0;
                _maxMs = 0.0;
                return value;
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
