using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Core.Runtime.Gameplay.ActivityTracking;
using Il2CppMashBoxSDK.Maps.Rigging;
using Il2CppMashBoxSDK.Services;
using Newtonsoft.Json;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Events;
using PlayerTrickGameplay = Il2CppMashBox.Core.Runtime.Gameplay.ActivityTracking.PlayerTrickGameplay;

namespace rowemod.Challenges
{
    public static class MultiplayerChallengeManager
    {
        public const string RoweModMarkerName = "I_HAVE_ROWE_MOD";

        private const int WindowId = 0x524f5745;
        private const int ProtocolVersion = 5;
        private const string ChallengeStateEventKey = "rowemod.mp.challenge.state.v5";
        private const string ChallengeCommandEventKey = "rowemod.mp.challenge.command.v5";
        private const string GameModeBike = "bike";
        private const string BikePhaseChoosing = "choosing";
        private const string BikePhaseSetting = "setting";
        private const string BikePhaseMatching = "matching";
        private const string BikePhaseGameOver = "gameover";
        private const string BikeAttemptPending = "pending";
        private const string BikeAttemptLanded = "landed";
        private const string BikeAttemptMissed = "missed";
        private const string BikeAttemptOut = "out";
        private const string BikeAttemptSet = "set";
        private const string BikeCommandLanded = "landed";
        private const string BikeCommandMiss = "miss";
        private const string BikeCommandStartSet = "start_set";
        private const string BikeCommandSetLine = "set_line";
        private const string BikeCommandSetterFailed = "setter_failed";
        private const string BikeLetters = "BIKE";
        private const int MaxBikeLetters = 4;
        private const float CompletionPollInterval = 0.25f;
        private const float LocalPlayerCacheRefreshInterval = 30f;
        private const float PlayerListRefreshInterval = 120f;
        private const float PlayerListRefreshDebounceInterval = 0.25f;
        private const float PresenceBroadcastInterval = 5f;
        private const float PresenceExpirySeconds = 16f;
        private const float PresenceExpiryCheckInterval = 2f;
        private const float IdleUpdateInterval = 1f;
        private const float CompletionRetryInterval = 2f;
        private const float LocalLookupRetryInterval = 5f;
        private const float TrickCatalogRefreshInterval = 2f;
        private const float TrickGameplayBindRetryInterval = 5f;
        private const int MaxStateJsonLength = 16384;
        private const int MaxCommandJsonLength = 4096;
        private const int MaxPlayers = 64;
        private const int MaxTricks = 16;
        private const int MaxTextLength = 64;
        private const int MaxCapturedAttemptTricks = 64;
        private const int MaxRecordedSetPositionSamples = 128;
        private const float StaleRepeatCaptureWindow = 0.45f;
        private const float RecordedSetBoundsPadding = 2.5f;
        private const float RecordedSetMinWidth = 5f;
        private const float RecordedSetMinHeight = 5f;
        private const float RecordedSetMinDepth = 5f;
        private const float WindowLogoMaxWidth = 96f;
        private const float WindowLogoMaxHeight = 18f;
        private const float DefaultWindowWidth = 560f;
        private const float DefaultWindowHeight = 420f;
        private const float DropdownWindowHeight = 540f;
        private const float ActiveWindowHeight = 420f;
        private const float DetailsWindowHeight = 680f;
        private const string TrickCaptureHarmonyId = "rowemod.mpchallenge.trickcapture";
        private static readonly JsonSerializerSettings NetworkJsonSettings = new JsonSerializerSettings
        {
            MaxDepth = 16,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None
        };
        private static Rect _windowRect = new Rect(180f, 120f, DefaultWindowWidth, DefaultWindowHeight);
        private static bool _isOpen;
        private static bool _showChallengeDetails;
        private static Vector2 _playerScroll;
        private static Vector2 _lineScroll;
        private static string _trickSearchText = string.Empty;
        private static readonly List<RowePlayer> _rowePlayers = new List<RowePlayer>();
        private static readonly List<int> _lineTrickIndexes = new List<int> { 0 };
        private static readonly List<bool> _dropdownOpen = new List<bool> { false };
        private static readonly List<Vector2> _dropdownScroll = new List<Vector2> { Vector2.zero };
        private static readonly HashSet<string> _observedChallengeNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string[] _trickNames = Array.Empty<string>();
        private static float _nextTrickCatalogRefreshTime;
        private static readonly string[] ChallengeExtraTrickNames =
        {
            "180",
            "360",
            "540",
            "720",
            "900",
            "1080",
            "1260",
            "1440",
            "Manual",
            "Nose Manual"
        };
        private static ActiveChallenge _activeChallenge;
        private static TrickDetection _trickDetection;
        private static readonly List<string> _entryHistorySnapshot = new List<string>();
        private static readonly List<string> _capturedAttemptTricks = new List<string>();
        private static readonly List<float> _capturedAttemptTrickTimes = new List<float>();
        private static readonly List<Vector3> _recordedSetWorldPositions = new List<Vector3>();
        private static string _lastCheckedAttemptSignature;
        private static float _nextCompletionPollTime;
        private static bool _wasLocalPlayerInsideArea;
        private static bool _currentAttemptHasConfirmedLanding;
        private static int _lastSeenHistoryCount;
        private static string _lastSeenHistorySource = "none";
        private static string _lastSeenTrickText = "none";
        private static string _lastCompletionDiagnostic = "Waiting for trick history.";
        private static string _cachedLocalPlayerKey;
        private static string _cachedLocalPlayerName;
        private static int _cachedLocalPlayerObjectId;
        private static float _nextLocalPlayerCacheRefreshTime;
        private static float _nextPlayerListRefreshTime;
        private static float _nextPlayerListFallbackRefreshTime;
        private static float _nextPresenceExpiryCheckTime;
        private static bool _playerListRefreshRequested = true;
        private static string _lastPlayerListSignature;
        private static float _nextIdleUpdateTime;
        private static string _activeAreaStateSignature;
        private static readonly Dictionary<string, PresenceRecord> _presenceByKey =
            new Dictionary<string, PresenceRecord>(StringComparer.OrdinalIgnoreCase);
        private static GameObject _networkBridgeRoot;
        private static MBNetworkedEvent _stateEvent;
        private static MBNetworkedEvent _commandEvent;
        private static UnityAction<string> _stateStringListener;
        private static UnityAction<string> _commandStringListener;
        private static UnityAction _stateClearedListener;
        private static float _nextPresenceBroadcastTime;
        private static float _nextNetworkRetryTime;
        private static int _networkFailureCount;
        private static string _networkStatus = "Waiting for multiplayer networking.";
        private static bool _statePublishPending;
        private static bool _clearStatePending;
        private static string _pendingLocalCompletionChallengeId;
        private static float _nextCompletionRetryTime;
        private static bool _playerScanDisabled;
        private static bool _trickHistoryDisabled;
        private static float _nextPlayerScanRetryTime;
        private static float _nextTrickHistoryRetryTime;
        private static float _nextTrickGameplayBindRetryTime;
        private static PlayerTrickGameplay _subscribedTrickGameplay;
        private static UnityAction _onAbilityPerformedListener;
        private static UnityAction _onAbilityComboConfirmedListener;
        private static UnityAction _onConfirmLandingListener;
        private static UnityAction _onEndLineConfirmedListener;
        private static UnityAction _onComboFailedListener;
        private static Action<NetworkPlayer> _networkPlayerSpawnedListener;
        private static HarmonyLib.Harmony _networkPlayerLifecycleHarmony;
        private static bool _networkPlayerLifecycleHooksInstalled;
        private static float _nextActivePlayerLookupWarningTime;
        private static float _nextUpdateErrorLogTime;
        private static HarmonyLib.Harmony _trickCaptureHarmony;
        private static bool _trickCapturePatchInstalled;
        private static bool _autoOpenTriggeredForSession;
        private static GUIStyle _challengeTitleStyle;
        private static GUIStyle _challengeLineStyle;
        private static GUIStyle _challengeHintStyle;
        private static GUIStyle _challengeTrickButtonStyle;
        private static GUIStyle _challengeDropdownButtonStyle;
        private static GUIStyle _challengeNumberStyle;
        private static GUIStyle _challengeStatusStyle;
        private static GUIStyle _challengeCompleteStatusStyle;
        private static GUIStyle _challengeDetailsBoxStyle;
        private static GUIStyle _challengeSmallButtonStyle;
        private static GUIStyle _challengeSmallDangerButtonStyle;
        
        public static ActivityTracker ActivityTracker { get; set; }
        
        private sealed class RowePlayer
        {
            public string Key;
            public string DisplayName;
            public NetworkPlayer NetworkPlayer;
        }

        private sealed class PresenceRecord
        {
            public string DisplayName;
            public float LastSeenTime;
        }

        private sealed class ActiveChallenge
        {
            public int Version;
            public string ChallengeId;
            public int Revision;
            public string GameMode = GameModeBike;
            public string Phase = BikePhaseChoosing;
            public string CreatorKey;
            public string CreatorName;
            public List<string> Tricks = new List<string>();
            public Dictionary<string, bool> PlayerCompleted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> PlayerLetters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> PlayerAttemptStatus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> PlayerAttemptsRemaining = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public List<string> TurnOrder = new List<string>();
            public int SetterIndex;
            public string CurrentSetterKey;
            public string CurrentSetterName;
            public int CurrentMatcherIndex = -1;
            public string CurrentMatcherKey;
            public string CurrentMatcherName;
            public bool LastLetterGetsTwoTries = true;
            public string WinnerKey;
            public string WinnerName;
            public float[] Position;
            public float[] Rotation;
            public float[] Size;
        }

        private sealed class NetworkChallengeState
        {
            public int Version { get; set; }
            public string ChallengeId { get; set; }
            public int Revision { get; set; }
            public string GameMode { get; set; }
            public string Phase { get; set; }
            public string CreatorKey { get; set; }
            public string CreatorName { get; set; }
            public List<string> Tricks { get; set; }
            public Dictionary<string, bool> PlayerCompleted { get; set; }
            public Dictionary<string, int> PlayerLetters { get; set; }
            public Dictionary<string, string> PlayerAttemptStatus { get; set; }
            public Dictionary<string, int> PlayerAttemptsRemaining { get; set; }
            public List<string> TurnOrder { get; set; }
            public int SetterIndex { get; set; }
            public string CurrentSetterKey { get; set; }
            public string CurrentSetterName { get; set; }
            public int CurrentMatcherIndex { get; set; }
            public string CurrentMatcherKey { get; set; }
            public string CurrentMatcherName { get; set; }
            public bool LastLetterGetsTwoTries { get; set; }
            public string WinnerKey { get; set; }
            public string WinnerName { get; set; }
            public float[] Position { get; set; }
            public float[] Rotation { get; set; }
            public float[] Size { get; set; }
        }

        private sealed class NetworkChallengeCommand
        {
            public int Version { get; set; }
            public string Type { get; set; }
            public string ChallengeId { get; set; }
            public string PlayerKey { get; set; }
            public string PlayerName { get; set; }
            public List<string> Tricks { get; set; }
            public float[] Position { get; set; }
            public float[] Rotation { get; set; }
            public float[] Size { get; set; }
        }

        public static bool IsOpen => _isOpen;
        public static bool HasActiveChallenge => _activeChallenge != null;

        public static void ToggleWindow()
        {
            if (!IsRuntimeEnabled())
                return;

            _isOpen = !_isOpen;
            if (_isOpen)
            {
                RefreshNow();
            }
        }

        private static void OpenWindow()
        {
            if (!IsRuntimeEnabled())
                return;

            _isOpen = true;
        }

        private static bool IsRuntimeEnabled()
        {
            return Config.challengeRuntimeSettings == null || Config.challengeRuntimeSettings.enabled;
        }

        private static bool IsNetworkingEnabled()
        {
            return IsRuntimeEnabled() &&
                   Config.challengeRuntimeSettings != null &&
                   Config.challengeRuntimeSettings.networkingEnabled;
        }

        private static bool IsAutoOpenEnabled()
        {
            return IsNetworkingEnabled() &&
                   Config.challengeRuntimeSettings != null &&
                   Config.challengeRuntimeSettings.autoOpenEnabled;
        }

        private static bool AreTrickHooksEnabled()
        {
            return IsRuntimeEnabled() &&
                   Config.challengeRuntimeSettings != null &&
                   Config.challengeRuntimeSettings.trickHooksEnabled;
        }

        public static void ResetWindowState()
        {
            _windowRect = new Rect(180f, 120f, DefaultWindowWidth, DefaultWindowHeight);
            _showChallengeDetails = false;
            _playerScroll = Vector2.zero;
            _lineScroll = Vector2.zero;
            _trickSearchText = string.Empty;
            _lineTrickIndexes.Clear();
            _lineTrickIndexes.Add(0);
            _dropdownOpen.Clear();
            _dropdownOpen.Add(false);
            _dropdownScroll.Clear();
            _dropdownScroll.Add(Vector2.zero);
            _observedChallengeNames.Clear();
            _trickNames = Array.Empty<string>();
            _nextTrickCatalogRefreshTime = 0f;
        }

        public static void OnLocalPlayerSpawned(GameObject playerObject)
        {
            if (!IsRuntimeEnabled())
                return;

            if (IsNetworkingEnabled())
            {
                InstallNetworkPlayerLifecycleHooks();
                TryEnsureLocalModMarker(playerObject);
                RestartNetworkBridge();
                SafeRefreshPlayers();
                BroadcastPresence();
            }
            else
            {
                _networkStatus = "Challenge networking disabled by config.";
            }

            if (AreTrickHooksEnabled())
            {
                InstallTrickCapturePatch();
                ResolveLocalTrickDetection(playerObject);
                BindPlayerTrickGameplayEvents(playerObject);
            }
        }

        public static void InstallTrickCapturePatch()
        {
            if (!AreTrickHooksEnabled())
                return;

            if (_trickCapturePatchInstalled)
                return;

            try
            {
                System.Reflection.MethodInfo target = AccessTools.Method(
                    typeof(TrickDetection),
                    nameof(TrickDetection.RecordTrickToHistory),
                    new[] { typeof(string) });
                if (target == null)
                    throw new MissingMethodException(typeof(TrickDetection).FullName, nameof(TrickDetection.RecordTrickToHistory));

                _trickCaptureHarmony = new HarmonyLib.Harmony(TrickCaptureHarmonyId);
                _trickCaptureHarmony.Patch(
                    target,
                    postfix: new HarmonyMethod(
                        typeof(TrickDetectionHistoryPatch),
                        nameof(TrickDetectionHistoryPatch.Postfix)));
                _trickCapturePatchInstalled = true;
                Log.Msg("[MPChallenge][Tricks] Direct TrickDetection capture installed.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Tricks] Direct capture install failed; using polling fallback: {ex}");
            }
        }

        private static void InstallNetworkPlayerLifecycleHooks()
        {
            if (!IsNetworkingEnabled())
                return;

            if (_networkPlayerLifecycleHooksInstalled)
                return;

            int hookCount = 0;
            try
            {
                _networkPlayerSpawnedListener ??= HandleNetworkPlayerSpawned;
                NetworkPlayer.LocalPlayerSpawned -= _networkPlayerSpawnedListener;
                NetworkPlayer.LocalPlayerSpawned += _networkPlayerSpawnedListener;
                hookCount++;
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Players] LocalPlayerSpawned hook failed: {ex.Message}");
            }

            try
            {
                _networkPlayerLifecycleHarmony = new HarmonyLib.Harmony("rowemod.mpchallenge.networkplayerlifecycle");

                System.Reflection.MethodInfo spawned = AccessTools.Method(typeof(NetworkPlayer), nameof(NetworkPlayer.Spawned));
                if (spawned != null)
                {
                    _networkPlayerLifecycleHarmony.Patch(
                        spawned,
                        postfix: new HarmonyMethod(
                            typeof(NetworkPlayerSpawnedPatch),
                            nameof(NetworkPlayerSpawnedPatch.Postfix)));
                    hookCount++;
                }

                System.Reflection.MethodInfo despawned = AccessTools.Method(typeof(NetworkPlayer), nameof(NetworkPlayer.Despawned));
                if (despawned != null)
                {
                    _networkPlayerLifecycleHarmony.Patch(
                        despawned,
                        postfix: new HarmonyMethod(
                            typeof(NetworkPlayerDespawnedPatch),
                            nameof(NetworkPlayerDespawnedPatch.Postfix)));
                    hookCount++;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Players] NetworkPlayer lifecycle patch failed: {ex.Message}");
            }

            _networkPlayerLifecycleHooksInstalled = hookCount > 0;
            if (_networkPlayerLifecycleHooksInstalled)
                Log.Msg($"[MPChallenge][Players] Installed event-driven NetworkPlayer lifecycle hooks ({hookCount}).");
        }

        private static void UnbindNetworkPlayerLifecycleHooks()
        {
            try
            {
                if (_networkPlayerSpawnedListener != null)
                    NetworkPlayer.LocalPlayerSpawned -= _networkPlayerSpawnedListener;

                _networkPlayerLifecycleHarmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Players] NetworkPlayer lifecycle unhook failed: {ex.Message}");
            }

            _networkPlayerSpawnedListener = null;
            _networkPlayerLifecycleHarmony = null;
            _networkPlayerLifecycleHooksInstalled = false;
        }

        private static void HandleNetworkPlayerSpawned(NetworkPlayer player)
        {
            HandleNetworkPlayerLifecycleChanged(player, "LocalPlayerSpawned");
        }

        private static void HandleNetworkPlayerLifecycleChanged(NetworkPlayer player, string source)
        {
            if (!IsNetworkingEnabled())
                return;

            try
            {
                if (player != null && IsLocalNetworkPlayer(player))
                {
                    string name = ResolvePlayerDisplayName(player);
                    string key = ResolvePlayerKey(player, name);
                    CacheLocalPlayer(key, name);
                }

                RequestPlayerListRefresh();
                _nextIdleUpdateTime = 0f;
                _nextPresenceBroadcastTime = 0f;
                _nextPlayerListFallbackRefreshTime = Time.unscaledTime + PlayerListRefreshInterval;
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Players] NetworkPlayer {source} handling failed: {ex.Message}");
            }
        }

        public static void OnSceneInitialized()
        {
            ClearOwnedNetworkStateBestEffort();
            UnbindPlayerTrickGameplayEvents();
            ShutdownNetworkBridge();
            ResetLocalSessionState();
        }

        public static void Shutdown()
        {
            ClearOwnedNetworkStateBestEffort();
            UnbindPlayerTrickGameplayEvents();
            UnbindNetworkPlayerLifecycleHooks();
            ShutdownNetworkBridge();
            ResetLocalSessionState();
        }

        public static void Update()
        {
            if (!IsRuntimeEnabled())
                return;

            try
            {
                UpdateInternal();
            }
            catch (Exception ex)
            {
                _nextNetworkRetryTime = Time.unscaledTime + 20f;
                _networkStatus = "Update failed; retrying.";
                if (Time.unscaledTime >= _nextUpdateErrorLogTime)
                {
                    _nextUpdateErrorLogTime = Time.unscaledTime + 40f;
                    Log.Error($"[MPChallenge] Update failed: {ex}");
                }
            }
        }

        private static void UpdateInternal()
        {
            if (ShouldThrottleIdleUpdate())
                return;

            if (IsNetworkingEnabled())
            {
                EnsureNetworkBridge();
                RetryPendingNetworkOperations();

                if (Time.unscaledTime >= _nextPresenceBroadcastTime)
                {
                    BroadcastPresence();
                }

                if (Time.unscaledTime >= _nextPresenceExpiryCheckTime)
                {
                    _nextPresenceExpiryCheckTime = Time.unscaledTime + PresenceExpiryCheckInterval;
                    if (ExpirePresenceRecords())
                        RequestPlayerListRefresh();
                }

                if (!_playerListRefreshRequested && Time.unscaledTime >= _nextPlayerListFallbackRefreshTime)
                    RequestPlayerListRefresh();

                if (_playerListRefreshRequested && Time.unscaledTime >= _nextPlayerListRefreshTime)
                {
                    SafeRefreshPlayers();
                }
            }
            else
            {
                _networkStatus = "Challenge networking disabled by config.";
                _playerListRefreshRequested = false;
                _statePublishPending = false;
                _clearStatePending = false;
            }

            if (!string.IsNullOrEmpty(_pendingLocalCompletionChallengeId) &&
                Time.unscaledTime >= _nextCompletionRetryTime)
            {
                SendLocalCompletionCommand();
            }

            if (_activeChallenge == null)
                return;

            UpdateChallengeAreaResizeEditing();

            if (Time.unscaledTime < _nextCompletionPollTime)
                return;

            _nextCompletionPollTime = Time.unscaledTime + CompletionPollInterval;

            bool inside = ChallengeAreaManager.IsLocalPlayerInsideActiveArea();
            if (inside && !_wasLocalPlayerInsideArea)
            {
                Log.Msg("[MPChallenge] Local player entered challenge area.");
                BeginLocalAttempt();
            }
            else if (!inside && _wasLocalPlayerInsideArea)
            {
                SafeCheckLocalLineCompletion();
                Log.Msg("[MPChallenge] Local player exited challenge area.");
            }

            _wasLocalPlayerInsideArea = inside;

            if (inside && AreTrickHooksEnabled())
                BindPlayerTrickGameplayEvents(null);
        }

        private static void UpdateChallengeAreaResizeEditing()
        {
            ChallengeArea area = ChallengeAreaManager.Active;
            if (area == null || _activeChallenge == null)
                return;

            bool editable = IsLocalCreator() && Menu.isOpen && _isOpen;
            area.SetResizeEditing(editable, editable && IsPointerOverChallengeUi());
            if (!area.TryConsumeResizeUpdate(
                    out Vector3 position,
                    out Vector3 size,
                    out bool committed))
            {
                return;
            }

            _activeChallenge.Position = Vector3ToArray(position);
            _activeChallenge.Size = Vector3ToArray(size);
            _activeAreaStateSignature = BuildAreaStateSignature(_activeChallenge);

            Config.challengeSettings.challengeSizeX = size.x;
            Config.challengeSettings.challengeSizeY = size.y;
            Config.challengeSettings.challengeSizeZ = size.z;

            if (!committed)
                return;

            _activeChallenge.Revision++;
            PublishActiveChallengeState();
            Log.Msg(
                $"[MPChallenge][Area] Resize committed: pos={FormatVector3(position)}, size={FormatVector3(size)}.");
        }

        private static bool IsPointerOverChallengeUi()
        {
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null)
                return false;

            Vector2 screenPosition = mouse.position.ReadValue();
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return Menu.windowRect.Contains(guiPosition) || _windowRect.Contains(guiPosition);
        }

        private static bool ShouldThrottleIdleUpdate()
        {
            if (_isOpen ||
                _activeChallenge != null ||
                _statePublishPending ||
                _clearStatePending ||
                !string.IsNullOrEmpty(_pendingLocalCompletionChallengeId))
            {
                return false;
            }

            if (Time.unscaledTime < _nextIdleUpdateTime)
                return true;

            _nextIdleUpdateTime = Time.unscaledTime + IdleUpdateInterval;
            return false;
        }

        public static void DrawWindow()
        {
            if (!IsRuntimeEnabled())
                return;

            if (!_isOpen)
                return;

            FitWindowSizeForCurrentMode();
            _windowRect = GUI.Window(WindowId, _windowRect, (GUI.WindowFunction)DrawWindowContents, "RoweMod Challenge", Menu.windowStyle);
        }

        public static void NotifyLocalChallengeCompleted()
        {
            if (_activeChallenge == null)
                return;

            string key = GetLocalPlayerKey();
            if (string.IsNullOrEmpty(key))
                return;

            CompleteLocalChallenge(key);
        }

        private static void DrawWindowContents(int windowId)
        {
            try
            {
                EnsureChallengeStyles();
                DrawWindowLogo();
                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Challenge", _challengeTitleStyle);
                GUILayout.FlexibleSpace();
                string detailsLabel = _showChallengeDetails ? "Hide Details" : "Details";
                if (GUILayout.Button(detailsLabel, _challengeSmallButtonStyle, GUILayout.Width(92f), GUILayout.Height(24f)))
                {
                    _showChallengeDetails = !_showChallengeDetails;
                }

                if (GUILayout.Button("X", _challengeSmallDangerButtonStyle, GUILayout.Width(34f), GUILayout.Height(24f)))
                {
                    _isOpen = false;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6f);
                DrawChallengeBuilder();

                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            }
            catch (Exception ex)
            {
                Log.Error($"[MPChallenge] Draw failed: {ex.Message}");
            }
        }

        private static void DrawWindowLogo()
        {
            Texture2D logo = Menu.logoTexture;
            if (logo == null || logo.width <= 0 || logo.height <= 0)
                return;

            float scale = Mathf.Min(
                WindowLogoMaxWidth / logo.width,
                WindowLogoMaxHeight / logo.height);
            float width = logo.width * scale;
            float height = logo.height * scale;
            Rect logoRect = new Rect(8f, 3f, width, height);

            Color previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private static void FitWindowSizeForCurrentMode()
        {
            float targetWidth = DefaultWindowWidth;
            if (Screen.width > 120)
                targetWidth = Mathf.Min(targetWidth, Screen.width - 40f);

            _windowRect.width = Mathf.Max(_windowRect.width, targetWidth);
            if (Screen.width > 120)
                _windowRect.width = Mathf.Min(_windowRect.width, Screen.width - 40f);

            float targetHeight = _showChallengeDetails
                ? DetailsWindowHeight
                : _activeChallenge != null
                    ? string.Equals(_activeChallenge.Phase, BikePhaseChoosing, StringComparison.Ordinal) && IsLocalBikeSetter()
                        ? (_dropdownOpen.Any(open => open) ? DropdownWindowHeight : DefaultWindowHeight)
                        : ActiveWindowHeight
                    : _dropdownOpen.Any(open => open)
                        ? DropdownWindowHeight
                        : DefaultWindowHeight;

            if (Screen.height > 120)
                targetHeight = Mathf.Min(targetHeight, Screen.height - 40f);

            _windowRect.height = Mathf.Max(ActiveWindowHeight, targetHeight);

            if (Screen.width > 120)
                _windowRect.x = Mathf.Clamp(_windowRect.x, 20f, Mathf.Max(20f, Screen.width - _windowRect.width - 20f));
            if (Screen.height > 120)
                _windowRect.y = Mathf.Clamp(_windowRect.y, 20f, Mathf.Max(20f, Screen.height - _windowRect.height - 20f));
        }

        private static void EnsureChallengeStyles()
        {
            if (_challengeTrickButtonStyle != null)
                return;

            Color panelColor = new Color(0.07f, 0.08f, 0.11f, 0.95f);
            Color panelAltColor = new Color(0.10f, 0.115f, 0.155f, 0.96f);
            Color panelHoverColor = new Color(0.14f, 0.16f, 0.21f, 0.98f);
            Color accentColor = new Color(0.32f, 0.72f, 1f, 0.95f);
            Color accentSoftColor = new Color(0.18f, 0.36f, 0.56f, 0.75f);
            Color borderColor = new Color(1f, 1f, 1f, 0.14f);
            Font trickFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            _challengeTitleStyle = new GUIStyle(Menu.sectionHeaderStyle ?? Menu.labelStyle ?? GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _challengeTitleStyle.normal.textColor = Color.white;

            _challengeHintStyle = new GUIStyle(Menu.subtleLabelStyle ?? GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };

            _challengeLineStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(14, 14, 10, 10),
                margin = new RectOffset(0, 0, 2, 8),
                border = new RectOffset(10, 10, 10, 10)
            };
            _challengeLineStyle.normal.background =
                Menu.MakeRoundedTex(64, 42, accentSoftColor, 10, 1, new Color(accentColor.r, accentColor.g, accentColor.b, 0.72f));
            _challengeLineStyle.normal.textColor = Color.white;
            if (trickFont != null)
                _challengeLineStyle.font = trickFont;

            _challengeTrickButtonStyle = new GUIStyle(Menu.highQualityButtonStyle ?? GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 12, 5, 5),
                border = new RectOffset(8, 8, 8, 8)
            };
            _challengeTrickButtonStyle.normal.background = Menu.MakeRoundedTex(64, 32, panelAltColor, 8, 1, borderColor);
            _challengeTrickButtonStyle.hover.background = Menu.MakeRoundedTex(64, 32, panelHoverColor, 8, 1, new Color(accentColor.r, accentColor.g, accentColor.b, 0.58f));
            _challengeTrickButtonStyle.active.background = Menu.MakeRoundedTex(64, 32, panelColor, 8, 1, new Color(accentColor.r, accentColor.g, accentColor.b, 0.82f));
            _challengeTrickButtonStyle.normal.textColor = Color.white;
            _challengeTrickButtonStyle.hover.textColor = Color.white;
            _challengeTrickButtonStyle.active.textColor = Color.white;
            if (trickFont != null)
                _challengeTrickButtonStyle.font = trickFont;

            _challengeDropdownButtonStyle = new GUIStyle(_challengeTrickButtonStyle)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(12, 10, 4, 4)
            };

            _challengeNumberStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(8, 8, 8, 8)
            };
            _challengeNumberStyle.normal.background = Menu.MakeRoundedTex(32, 32, accentSoftColor, 8, 1, new Color(accentColor.r, accentColor.g, accentColor.b, 0.72f));
            _challengeNumberStyle.normal.textColor = Color.white;

            _challengeStatusStyle = new GUIStyle(Menu.labelStyle ?? GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _challengeStatusStyle.normal.textColor = new Color(0.88f, 0.92f, 1f, 1f);

            _challengeCompleteStatusStyle = new GUIStyle(_challengeStatusStyle);
            _challengeCompleteStatusStyle.normal.textColor = new Color(0.52f, 1f, 0.65f, 1f);

            _challengeDetailsBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 4),
                border = new RectOffset(9, 9, 9, 9)
            };
            _challengeDetailsBoxStyle.normal.background = Menu.MakeRoundedTex(64, 64, panelColor, 9, 1, borderColor);

            _challengeSmallButtonStyle = new GUIStyle(Menu.highQualityButtonStyle ?? GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4)
            };

            _challengeSmallDangerButtonStyle = new GUIStyle(Menu.redButtonStyle ?? _challengeSmallButtonStyle)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4)
            };
        }

        private static void DrawStatusMessages()
        {
            GUILayout.Label($"Network: {_networkStatus}", Menu.subtleLabelStyle);

            if (_playerScanDisabled)
            {
                GUILayout.Label("Player scanning is disabled after an IL2CPP lookup error.", Menu.subtleLabelStyle);
            }

            if (_trickHistoryDisabled)
            {
                GUILayout.Label("Trick history checking is disabled after an IL2CPP lookup error.", Menu.subtleLabelStyle);
            }
        }

        private static void DrawPlayerList()
        {
            if (_activeChallenge != null)
            {
                DrawBikeScoreboard();
                return;
            }

            GUILayout.Label("Players with RoweMod", Menu.labelStyle);

            _playerScroll = GUILayout.BeginScrollView(_playerScroll, GUILayout.Height(145f));
            if (_rowePlayers.Count == 0)
            {
                GUILayout.Label("No RoweMod players found yet.", Menu.subtleLabelStyle);
            }
            else
            {
                foreach (RowePlayer player in _rowePlayers)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("-", Menu.labelStyle, GUILayout.Width(22f));
                    GUILayout.Label(GetSafePlayerDisplayName(player), Menu.labelStyle);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }

        private static void DrawBikeScoreboard()
        {
            EnsureBikeStateCollections();
            int playerCount = _activeChallenge.TurnOrder.Count;
            string riderLabel = playerCount == 1 ? "1 rider" : $"{playerCount} riders";

            GUILayout.Space(6f);
            GUILayout.BeginVertical(_challengeDetailsBoxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Scoreboard", _challengeTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(riderLabel, Menu.subtleLabelStyle);
            GUILayout.EndHorizontal();

            _playerScroll = GUILayout.BeginScrollView(_playerScroll, GUILayout.Height(GetBikeScoreboardHeight(playerCount)));
            if (playerCount == 0)
            {
                GUILayout.Label("No players in this game yet.", Menu.subtleLabelStyle);
            }
            else
            {
                foreach (string playerKey in _activeChallenge.TurnOrder)
                    DrawBikeScoreboardRow(playerKey);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawBikeScoreboardRow(string playerKey)
        {
            string playerName = GetBikePlayerName(playerKey);
            if (string.Equals(playerKey, GetLocalPlayerKey(), StringComparison.OrdinalIgnoreCase))
                playerName = $"{playerName} (You)";

            string status = GetBikeScoreboardStatus(playerKey);
            if (string.IsNullOrEmpty(status))
                status = "-";

            GUILayout.BeginHorizontal(GUILayout.Height(26f));
            GUILayout.Label(FormatBikeLetters(playerKey), _challengeNumberStyle, GUILayout.Width(64f), GUILayout.Height(24f));
            GUILayout.Label(playerName, Menu.labelStyle, GUILayout.MinWidth(120f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(status, Menu.subtleLabelStyle, GUILayout.Width(90f));
            GUILayout.EndHorizontal();
        }

        private static float GetBikeScoreboardHeight(int playerCount)
        {
            return Mathf.Clamp(28f + Math.Max(1, playerCount) * 28f, 64f, 148f);
        }

        private static string GetBikeScoreboardStatus(string playerKey)
        {
            if (_activeChallenge == null)
                return string.Empty;

            if (string.Equals(playerKey, _activeChallenge.WinnerKey, StringComparison.OrdinalIgnoreCase))
                return "Winner";
            if (IsBikePlayerOut(playerKey))
                return "Out";
            if (string.Equals(playerKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
                return string.Equals(_activeChallenge.Phase, BikePhaseChoosing, StringComparison.Ordinal) ? "Ready" : "Recording";
            if (string.Equals(playerKey, _activeChallenge.CurrentMatcherKey, StringComparison.OrdinalIgnoreCase))
                return "Matching";

            string status = GetBikeAttemptStatus(playerKey);
            if (string.Equals(status, BikeAttemptLanded, StringComparison.Ordinal))
                return "Landed";
            if (string.Equals(status, BikeAttemptSet, StringComparison.Ordinal))
                return "Set";
            if (string.Equals(status, BikeAttemptMissed, StringComparison.Ordinal))
                return "Missed";
            if (string.Equals(status, BikeAttemptPending, StringComparison.Ordinal))
                return "Pending";

            return string.Empty;
        }

        private static void DrawChallengeBuilder()
        {
            if (_activeChallenge != null)
            {
                DrawActiveChallengeSummary();
                DrawPlayerList();
                DrawBikeTurnControls();

                if (IsLocalCreator() && GUILayout.Button("Clear Game", Menu.redButtonStyle))
                {
                    ClearActiveChallenge();
                    return;
                }

                if (_showChallengeDetails)
                    DrawChallengeDetails();

                return;
            }

            GUILayout.Label("Start a game, then the setter records a landed line in the challenge box.", _challengeHintStyle);

            if (GUILayout.Button("Start Game", _challengeTrickButtonStyle, GUILayout.Height(34f)))
            {
                AcceptChallenge();
            }

            if (_showChallengeDetails)
                DrawChallengeDetails();
        }

        private static void DrawLinePicker(float lineHeight)
        {
            _lineScroll = GUILayout.BeginScrollView(_lineScroll, GUILayout.Height(lineHeight));
            for (int i = 0; i < _lineTrickIndexes.Count; i++)
            {
                EnsureLineState(i);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}", _challengeNumberStyle, GUILayout.Width(28f), GUILayout.Height(30f));

                int trickIndex = Mathf.Clamp(_lineTrickIndexes[i], 0, _trickNames.Length - 1);
                if (GUILayout.Button(_trickNames[trickIndex], _challengeTrickButtonStyle, GUILayout.Height(30f)))
                {
                    bool open = !_dropdownOpen[i];
                    CloseAllDropdowns();
                    _dropdownOpen[i] = open;
                    if (open)
                        _trickSearchText = string.Empty;
                }

                if (_lineTrickIndexes.Count > 1 &&
                    GUILayout.Button("X", _challengeSmallDangerButtonStyle, GUILayout.Width(34f), GUILayout.Height(30f)))
                {
                    _lineTrickIndexes.RemoveAt(i);
                    _dropdownOpen.RemoveAt(i);
                    _dropdownScroll.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();

                if (_dropdownOpen[i])
                    DrawTrickDropdown(i);
            }
            GUILayout.EndScrollView();
        }

        private static void DrawTrickDropdown(int lineIndex)
        {
            GUILayout.BeginVertical(_challengeDetailsBoxStyle);
            _trickSearchText = GUILayout.TextField(
                _trickSearchText ?? string.Empty,
                Menu.textFieldStyle ?? GUI.skin.textField,
                GUILayout.Height(26f));

            string search = _trickSearchText;
            _dropdownScroll[lineIndex] = GUILayout.BeginScrollView(_dropdownScroll[lineIndex], GUILayout.Height(168f));
            bool found = false;
            for (int j = 0; j < _trickNames.Length; j++)
            {
                if (!string.IsNullOrWhiteSpace(search) &&
                    _trickNames[j].IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                found = true;
                if (GUILayout.Button(_trickNames[j], _challengeDropdownButtonStyle, GUILayout.Height(26f)))
                {
                    _lineTrickIndexes[lineIndex] = j;
                    _dropdownOpen[lineIndex] = false;
                    _trickSearchText = string.Empty;
                }
            }

            if (!found)
                GUILayout.Label("No matching tricks.", _challengeHintStyle);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawActiveChallengeSummary()
        {
            string line = _activeChallenge.Tricks != null && _activeChallenge.Tricks.Count > 0
                ? string.Join("  +  ", _activeChallenge.Tricks)
                : "No set recorded yet";
            bool localComplete = IsLocalPlayerComplete();

            GUILayout.Label("Current Set", _challengeTitleStyle);
            GUILayout.Label(line, _challengeLineStyle);
            GUILayout.Label(GetBikePhaseStatusText(), localComplete ? _challengeCompleteStatusStyle : _challengeStatusStyle);
        }

        private static string GetBikePhaseStatusText()
        {
            if (_activeChallenge == null)
                return string.Empty;

            if (string.Equals(_activeChallenge.Phase, BikePhaseChoosing, StringComparison.Ordinal))
                return $"{GetCurrentBikeSetterName()} is ready to record the next set.";

            if (string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal))
                return $"{GetCurrentBikeSetterName()} is recording. Land to lock in the set.";

            if (string.Equals(_activeChallenge.Phase, BikePhaseMatching, StringComparison.Ordinal))
            {
                int attempts = GetBikeAttemptsRemaining(_activeChallenge.CurrentMatcherKey);
                string tries = attempts > 1 ? $" ({attempts} tries left)" : string.Empty;
                return $"{GetCurrentBikeMatcherName()} must match the set{tries}.";
            }

            if (string.Equals(_activeChallenge.Phase, BikePhaseGameOver, StringComparison.Ordinal))
                return $"{GetBikePlayerName(_activeChallenge.WinnerKey, _activeChallenge.WinnerName)} wins.";

            return "Game active.";
        }

        private static void DrawBikeTurnControls()
        {
            if (_activeChallenge == null ||
                string.Equals(_activeChallenge.Phase, BikePhaseGameOver, StringComparison.Ordinal))
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.BeginVertical(_challengeDetailsBoxStyle);

            if (string.Equals(_activeChallenge.Phase, BikePhaseChoosing, StringComparison.Ordinal))
            {
                if (IsLocalBikeSetter())
                {
                    GUILayout.Label("Enter the box and click SET TRICK before starting your line.", _challengeHintStyle);
                    if (GUILayout.Button("SET TRICK", _challengeTrickButtonStyle, GUILayout.Height(32f)))
                        SubmitBikeStartSet();
                }
                else
                {
                    GUILayout.Label($"Waiting for {GetCurrentBikeSetterName()} to start recording.", _challengeHintStyle);
                }
            }
            else if (string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal))
            {
                if (IsLocalBikeSetter())
                {
                    GUILayout.Label("Recording now. Land the line to set it.", _challengeHintStyle);
                    if (GUILayout.Button("Pass Turn", _challengeSmallDangerButtonStyle, GUILayout.Height(28f)))
                        SubmitBikeSetterFailed();
                }
                else
                {
                    GUILayout.Label($"Waiting for {GetCurrentBikeSetterName()} to record and land a set.", _challengeHintStyle);
                }
            }
            else if (string.Equals(_activeChallenge.Phase, BikePhaseMatching, StringComparison.Ordinal))
            {
                if (IsLocalBikeMatcher())
                {
                    int attempts = GetBikeAttemptsRemaining(GetLocalPlayerKey());
                    string missLabel = attempts > 1 ? "Miss Attempt" : "Take Letter";
                    GUILayout.Label("Your turn to match the set. Landing it is detected automatically.", _challengeHintStyle);
                    if (GUILayout.Button(missLabel, _challengeSmallDangerButtonStyle, GUILayout.Height(28f)))
                        SubmitBikeMiss();
                }
                else
                {
                    GUILayout.Label($"Waiting for {GetCurrentBikeMatcherName()} to match.", _challengeHintStyle);
                }
            }

            GUILayout.EndVertical();
        }

        private static void DrawChallengeDetails()
        {
            GUILayout.Space(6f);
            GUILayout.BeginVertical(_challengeDetailsBoxStyle);
            if (_activeChallenge != null)
            {
                GUILayout.Label(
                    $"Host: {GetDisplayNameForKey(_activeChallenge.CreatorKey, _activeChallenge.CreatorName)}",
                    Menu.subtleLabelStyle);
            }

            DrawStatusMessages();
            if (_activeChallenge == null)
                DrawPlayerList();

            if (_activeChallenge != null)
            {
                if (IsLocalCreator() && ChallengeAreaManager.Active != null)
                {
                    Vector3 areaSize = ChallengeAreaManager.Active.transform.localScale;
                    GUILayout.Label(
                        $"Area size: {areaSize.x:0.00} x {areaSize.y:0.00} x {areaSize.z:0.00}",
                        Menu.subtleLabelStyle);
                    GUILayout.Label("Drag challenge face handles to resize.", Menu.subtleLabelStyle);
                }

                GUILayout.Label($"Last trick: {_lastSeenTrickText}", Menu.subtleLabelStyle);
                GUILayout.Label($"Match: {_lastCompletionDiagnostic}", Menu.subtleLabelStyle);
            }

            if (GUILayout.Button("Refresh Players", _challengeSmallButtonStyle, GUILayout.Height(26f)))
                RefreshNow();

            GUILayout.EndVertical();
        }

        private static void CloseAllDropdowns()
        {
            for (int i = 0; i < _dropdownOpen.Count; i++)
                _dropdownOpen[i] = false;
        }

        private static int GetCompletedPlayerCount()
        {
            if (_activeChallenge?.PlayerCompleted == null)
                return 0;

            return _activeChallenge.PlayerCompleted.Values.Count(value => value);
        }

        private static int GetChallengePlayerCount()
        {
            if (_activeChallenge?.PlayerCompleted == null)
                return Math.Max(1, _rowePlayers.Count);

            return Math.Max(1, Math.Max(_activeChallenge.PlayerCompleted.Count, _rowePlayers.Count));
        }

        private static List<string> BuildBikeTurnOrder(string creatorKey)
        {
            List<string> turnOrder = new List<string>();
            AddBikeTurnOrderKey(turnOrder, creatorKey);

            foreach (RowePlayer player in _rowePlayers)
            {
                if (player == null)
                    continue;

                AddBikeTurnOrderKey(turnOrder, player.Key);
            }

            return turnOrder;
        }

        private static void AddBikeTurnOrderKey(List<string> turnOrder, string playerKey)
        {
            if (!IsUsableBikePlayerKey(playerKey))
                return;

            if (turnOrder.Any(key => string.Equals(key, playerKey, StringComparison.OrdinalIgnoreCase)))
                return;

            turnOrder.Add(playerKey);
        }

        private static void EnsureBikeStateCollections()
        {
            if (_activeChallenge == null)
                return;

            _activeChallenge.GameMode = GameModeBike;
            _activeChallenge.Phase = string.IsNullOrWhiteSpace(_activeChallenge.Phase)
                ? BikePhaseChoosing
                : _activeChallenge.Phase;
            _activeChallenge.Tricks ??= new List<string>();
            _activeChallenge.PlayerCompleted ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _activeChallenge.PlayerLetters ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _activeChallenge.PlayerAttemptStatus ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _activeChallenge.PlayerAttemptsRemaining ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _activeChallenge.TurnOrder ??= new List<string>();
        }

        private static void EnsureBikePlayerState(string playerKey, string playerName, bool addToTurnOrder)
        {
            if (_activeChallenge == null || !IsUsableBikePlayerKey(playerKey))
                return;

            EnsureBikeStateCollections();

            if (!_activeChallenge.PlayerLetters.ContainsKey(playerKey))
                _activeChallenge.PlayerLetters[playerKey] = 0;
            else
                _activeChallenge.PlayerLetters[playerKey] = Mathf.Clamp(_activeChallenge.PlayerLetters[playerKey], 0, MaxBikeLetters);

            if (!_activeChallenge.PlayerCompleted.ContainsKey(playerKey))
                _activeChallenge.PlayerCompleted[playerKey] = false;

            if (!_activeChallenge.PlayerAttemptStatus.ContainsKey(playerKey))
            {
                _activeChallenge.PlayerAttemptStatus[playerKey] = IsBikePlayerOut(playerKey)
                    ? BikeAttemptOut
                    : string.Empty;
            }

            if (!_activeChallenge.PlayerAttemptsRemaining.ContainsKey(playerKey))
                _activeChallenge.PlayerAttemptsRemaining[playerKey] = 0;

            if (addToTurnOrder &&
                !_activeChallenge.TurnOrder.Any(key => string.Equals(key, playerKey, StringComparison.OrdinalIgnoreCase)))
            {
                _activeChallenge.TurnOrder.Add(playerKey);
            }
        }

        private static bool IsBikePlayerOut(string playerKey)
        {
            return GetBikeLetterCount(playerKey) >= MaxBikeLetters;
        }

        private static int GetBikeLetterCount(string playerKey)
        {
            if (_activeChallenge?.PlayerLetters == null || string.IsNullOrEmpty(playerKey))
                return 0;

            return _activeChallenge.PlayerLetters.TryGetValue(playerKey, out int letters)
                ? Mathf.Clamp(letters, 0, MaxBikeLetters)
                : 0;
        }

        private static int GetBikeAttemptsAllowed(string playerKey)
        {
            return _activeChallenge != null &&
                   _activeChallenge.LastLetterGetsTwoTries &&
                   GetBikeLetterCount(playerKey) == MaxBikeLetters - 1
                ? 2
                : 1;
        }

        private static int GetBikeAttemptsRemaining(string playerKey)
        {
            if (_activeChallenge?.PlayerAttemptsRemaining == null || string.IsNullOrEmpty(playerKey))
                return 0;

            return _activeChallenge.PlayerAttemptsRemaining.TryGetValue(playerKey, out int attempts)
                ? Mathf.Max(0, attempts)
                : 0;
        }

        private static string GetBikeAttemptStatus(string playerKey)
        {
            if (_activeChallenge?.PlayerAttemptStatus == null || string.IsNullOrEmpty(playerKey))
                return string.Empty;

            return _activeChallenge.PlayerAttemptStatus.TryGetValue(playerKey, out string status)
                ? status ?? string.Empty
                : string.Empty;
        }

        private static string FormatBikeLetters(string playerKey)
        {
            int count = GetBikeLetterCount(playerKey);
            char[] letters = new char[MaxBikeLetters];
            for (int i = 0; i < MaxBikeLetters; i++)
                letters[i] = i < count ? BikeLetters[i] : '_';

            return new string(letters);
        }

        private static string GetBikePlayerName(string playerKey, string fallback = null)
        {
            return GetDisplayNameForKey(playerKey, fallback);
        }

        private static bool IsLocalBikeSetter()
        {
            return _activeChallenge != null &&
                   string.Equals(_activeChallenge.CurrentSetterKey, GetLocalPlayerKey(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalBikeMatcher()
        {
            return _activeChallenge != null &&
                   string.Equals(_activeChallenge.CurrentMatcherKey, GetLocalPlayerKey(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalBikeAttemptAllowed(string localKey)
        {
            if (_activeChallenge == null || string.IsNullOrEmpty(localKey))
                return false;

            if (IsBikePlayerOut(localKey))
                return false;

            if (string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal))
                return string.Equals(localKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(_activeChallenge.Phase, BikePhaseMatching, StringComparison.Ordinal))
                return string.Equals(localKey, _activeChallenge.CurrentMatcherKey, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        private static int GetActiveBikePlayerCount()
        {
            if (_activeChallenge?.TurnOrder == null)
                return 0;

            return _activeChallenge.TurnOrder.Count(key => IsUsableBikePlayerKey(key) && !IsBikePlayerOut(key));
        }

        private static string GetCurrentBikeMatcherName()
        {
            if (_activeChallenge == null)
                return "Unknown Player";

            return GetBikePlayerName(_activeChallenge.CurrentMatcherKey, _activeChallenge.CurrentMatcherName);
        }

        private static string GetCurrentBikeSetterName()
        {
            if (_activeChallenge == null)
                return "Unknown Player";

            return GetBikePlayerName(_activeChallenge.CurrentSetterKey, _activeChallenge.CurrentSetterName);
        }

        private static List<string> GetSelectedLineTricks()
        {
            EnsureTrickNames();
            if (_trickNames.Length == 0)
                return new List<string>();

            return _lineTrickIndexes
                .Select(index => _trickNames[Mathf.Clamp(index, 0, _trickNames.Length - 1)])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(GetChallengeDisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }

        private static void AcceptChallenge()
        {
            RefreshNow();
            EnsureLocalPlayerCache(true);
            string creatorKey = GetLocalPlayerKey();
            if (!IsUsableBikePlayerKey(creatorKey))
            {
                Log.Warning("[MPChallenge] Cannot create a challenge until the local player is ready.");
                return;
            }

            List<string> turnOrder = BuildBikeTurnOrder(creatorKey);
            _activeChallenge = new ActiveChallenge
            {
                Version = ProtocolVersion,
                ChallengeId = Guid.NewGuid().ToString("N"),
                Revision = 1,
                GameMode = GameModeBike,
                Phase = BikePhaseChoosing,
                CreatorKey = creatorKey,
                CreatorName = GetLocalPlayerName(),
                Tricks = new List<string>(),
                TurnOrder = turnOrder,
                SetterIndex = Math.Max(0, turnOrder.FindIndex(key => string.Equals(key, creatorKey, StringComparison.OrdinalIgnoreCase))),
                CurrentSetterKey = creatorKey,
                CurrentSetterName = GetLocalPlayerName(),
                CurrentMatcherIndex = -1,
                CurrentMatcherKey = null,
                CurrentMatcherName = null,
                LastLetterGetsTwoTries = true
            };
            OpenWindow();

            EnsureBikePlayerState(creatorKey, GetLocalPlayerName(), true);
            foreach (RowePlayer player in _rowePlayers)
                EnsureBikePlayerState(player.Key, player.DisplayName, true);

            ChallengeArea area = SpawnLocalChallengeArea();
            if (area != null)
            {
                _activeChallenge.Position = Vector3ToArray(area.transform.position);
                _activeChallenge.Rotation = QuaternionToArray(area.transform.rotation);
                _activeChallenge.Size = Vector3ToArray(area.transform.localScale);
                _activeAreaStateSignature = BuildAreaStateSignature(_activeChallenge);
                Log.Msg($"[MPChallenge][Area] Local challenge area spawned: {DescribeArea(area)}");
            }
            else
            {
                Log.Warning("[MPChallenge][Area] Failed to spawn local challenge area. Network state will publish without area transform data.");
            }

            ResetCompletionDiagnostics();
            _lastCompletionDiagnostic = $"{GetLocalPlayerName()} is ready to record the first set.";
            PublishActiveChallengeState();
            Log.Msg($"[MPChallenge][BIKE] Started game: {DescribeActiveChallenge(_activeChallenge)}");
        }

        private static ChallengeArea SpawnLocalChallengeArea()
        {
            Vector3 areaSize = new Vector3(
                Config.challengeSettings.challengeSizeX,
                Config.challengeSettings.challengeSizeY,
                Config.challengeSettings.challengeSizeZ);
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            string spawnSource = "camera/default";

            if (ChallengeAreaManager.TryGetLocalPlayerGroundPlacement(
                    areaSize,
                    out Vector3 playerPosition,
                    out Quaternion playerRotation,
                    out string playerPoseSource))
            {
                spawnPos = playerPosition;
                spawnRot = playerRotation;
                spawnSource = playerPoseSource;
            }
            else if (UnityEngine.Camera.main != null)
            {
                Vector3 cameraTarget =
                    UnityEngine.Camera.main.transform.position +
                    UnityEngine.Camera.main.transform.forward * 5f;
                bool foundGround = ChallengeAreaManager.TryGetGroundAlignedPlacement(
                    cameraTarget,
                    UnityEngine.Camera.main.transform.forward,
                    areaSize,
                    out spawnPos,
                    out spawnRot,
                    out string groundSource);
                spawnSource = foundGround
                    ? $"camera, ground={groundSource}"
                    : "camera, ground=fallback";
            }

            ChallengeArea area = ChallengeAreaManager.Create(
                spawnPos,
                areaSize,
                spawnRot,
                "VehicleColliders");

            ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
            ChallengeAreaManager.SetCompleted(false);
            Log.Msg($"[MPChallenge][Area] SpawnLocalChallengeArea source={spawnSource} pos={FormatVector3(spawnPos)} rot={FormatQuaternion(spawnRot)} size={FormatVector3(area != null ? area.transform.localScale : Vector3.zero)} visible={Config.challengeSettings.challengeVisible}");
            return area;
        }

        private static void EnsureLocalModMarker(GameObject playerObject = null)
        {
            GameObject markerRoot = playerObject != null ? playerObject : Memory.physicsDrivenCharacter;
            if (markerRoot == null)
                return;

            Transform existing = markerRoot.transform.Find(RoweModMarkerName);
            if (existing != null)
            {
                Log.Msg($"[MPChallenge][Marker] Local RoweMod marker already exists under '{markerRoot.name}'.");
                return;
            }

            GameObject marker = new GameObject(RoweModMarkerName);
            marker.transform.SetParent(markerRoot.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            marker.hideFlags = HideFlags.DontSave;
            Log.Msg($"[MPChallenge][Marker] Created local RoweMod marker under '{markerRoot.name}'.");
        }

        private static void RefreshNow()
        {
            if (IsNetworkingEnabled())
            {
                InstallNetworkPlayerLifecycleHooks();
                TryEnsureLocalModMarker();
                EnsureNetworkBridge();
                SafeRefreshPlayers();
                BroadcastPresence();
            }
            else
            {
                _networkStatus = "Challenge networking disabled by config.";
                _playerListRefreshRequested = false;
            }

            EnsureTrickNames();
        }

        private static void TryEnsureLocalModMarker(GameObject playerObject = null)
        {
            try
            {
                EnsureLocalModMarker(playerObject);
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge] Failed to create local RoweMod marker: {ex.Message}");
            }
        }

        private static void SafeRefreshPlayers()
        {
            if (!IsNetworkingEnabled())
            {
                _playerListRefreshRequested = false;
                _networkStatus = "Challenge networking disabled by config.";
                return;
            }

            if (_playerScanDisabled && Time.unscaledTime < _nextPlayerScanRetryTime)
            {
                _nextPlayerListRefreshTime = _nextPlayerScanRetryTime;
                _nextPlayerListFallbackRefreshTime = _nextPlayerScanRetryTime;
                return;
            }

            if (_networkBridgeRoot == null && _activeChallenge == null && _presenceByKey.Count == 0)
            {
                _playerListRefreshRequested = false;
                _nextPlayerListFallbackRefreshTime = Time.unscaledTime + PlayerListRefreshInterval;
                return;
            }

            _playerListRefreshRequested = false;
            try
            {
                _playerScanDisabled = false;
                RefreshPlayers();
                TryAutoOpenForRemoteRowePlayer();
                _nextPlayerListFallbackRefreshTime = Time.unscaledTime + PlayerListRefreshInterval;
                string signature = BuildPlayerListSignature();
                if (!string.Equals(signature, _lastPlayerListSignature, StringComparison.Ordinal))
                {
                    _lastPlayerListSignature = signature;
                    Log.Msg($"[MPChallenge][Players] Refreshed RoweMod players: count={_rowePlayers.Count}, localKey='{_cachedLocalPlayerKey ?? "null"}', localName='{_cachedLocalPlayerName ?? "null"}'.");
                }
            }
            catch (Exception ex)
            {
                _playerScanDisabled = true;
                _nextPlayerScanRetryTime = Time.unscaledTime + LocalLookupRetryInterval;
                _nextPlayerListFallbackRefreshTime = _nextPlayerScanRetryTime;
                RequestPlayerListRefresh(LocalLookupRetryInterval);
                Log.Warning($"[MPChallenge] Player scanning failed; retrying: {ex.Message}");
            }
        }

        private static void TryAutoOpenForRemoteRowePlayer()
        {
            if (!IsAutoOpenEnabled())
                return;

            if (_autoOpenTriggeredForSession)
                return;

            string localKey = GetLocalPlayerKey();
            if (!IsStableNetworkPlayerKey(localKey))
                return;

            RowePlayer remotePlayer = _rowePlayers.FirstOrDefault(player =>
                player != null &&
                IsStableNetworkPlayerKey(player.Key) &&
                !string.Equals(player.Key, localKey, StringComparison.OrdinalIgnoreCase));
            if (remotePlayer == null)
                return;

            _autoOpenTriggeredForSession = true;
            OpenWindow();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Log.Msg(
                $"[MPChallenge][UI] Auto-opened challenge UI after detecting RoweMod player " +
                $"'{GetSafePlayerDisplayName(remotePlayer)}' ({remotePlayer.Key}).");
        }

        private static void SafeCheckLocalLineCompletion()
        {
            if (_trickHistoryDisabled && Time.unscaledTime < _nextTrickHistoryRetryTime)
                return;

            try
            {
                _trickHistoryDisabled = false;
                CheckLocalLineCompletion();
            }
            catch (Exception ex)
            {
                _trickHistoryDisabled = true;
                _nextTrickHistoryRetryTime = Time.unscaledTime + LocalLookupRetryInterval;
                Log.Warning($"[MPChallenge] Trick history lookup failed; retrying: {ex.Message}");
            }
        }

        private static void RefreshPlayers()
        {
            _rowePlayers.Clear();
            _cachedLocalPlayerKey = null;
            _cachedLocalPlayerName = null;
            _cachedLocalPlayerObjectId = 0;
            ExpirePresenceRecords();

            List<NetworkPlayer> players = GetKnownNetworkPlayers(true);
            foreach (NetworkPlayer player in players)
            {
                if (player == null || player.gameObject == null)
                    continue;

                string name = ResolvePlayerDisplayName(player);
                string key = ResolvePlayerKey(player, name);
                bool isLocal = IsLocalNetworkPlayer(player);
                if (isLocal)
                {
                    CacheLocalPlayer(key, name);
                }

                if (!isLocal && !_presenceByKey.ContainsKey(key))
                    continue;

                if (_presenceByKey.TryGetValue(key, out PresenceRecord presence) &&
                    IsUsableDisplayText(presence.DisplayName))
                {
                    name = presence.DisplayName;
                }

                _rowePlayers.Add(new RowePlayer
                {
                    Key = key,
                    DisplayName = name,
                    NetworkPlayer = player
                });
            }

            AddLocalFallbackPlayerIfNeeded();

            foreach (KeyValuePair<string, PresenceRecord> entry in _presenceByKey)
            {
                if (_rowePlayers.Any(player => string.Equals(player.Key, entry.Key, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _rowePlayers.Add(new RowePlayer
                {
                    Key = entry.Key,
                    DisplayName = entry.Value.DisplayName,
                    NetworkPlayer = null
                });
            }

            if (_activeChallenge != null && IsLocalCreator())
            {
                bool changed = false;
                foreach (RowePlayer player in _rowePlayers)
                {
                    int previousTurnCount = _activeChallenge.TurnOrder?.Count ?? 0;
                    bool hadLetters = _activeChallenge.PlayerLetters != null &&
                                      _activeChallenge.PlayerLetters.ContainsKey(player.Key);
                    EnsureBikePlayerState(player.Key, player.DisplayName, true);
                    if (!hadLetters || (_activeChallenge.TurnOrder?.Count ?? 0) != previousTurnCount)
                        changed = true;

                    if (_activeChallenge.PlayerCompleted.ContainsKey(player.Key))
                        continue;

                    _activeChallenge.PlayerCompleted[player.Key] = false;
                    changed = true;
                }

                if (changed)
                {
                    _activeChallenge.Revision++;
                    PublishActiveChallengeState();
                }
            }
        }

        private static List<NetworkPlayer> GetKnownNetworkPlayers(bool allowSceneFallback)
        {
            List<NetworkPlayer> players = new List<NetworkPlayer>();
            HashSet<int> seen = new HashSet<int>();

            try
            {
                int activePlayerCount = NetworkPlayer.ActivePlayerCount;
                for (int i = 0; i < activePlayerCount; i++)
                {
                    AddNetworkPlayerIfUnique(players, seen, NetworkPlayer.GetActivePlayer(i));
                }

                if (players.Count > 0 || !allowSceneFallback)
                    return players;
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= _nextActivePlayerLookupWarningTime)
                {
                    _nextActivePlayerLookupWarningTime = Time.unscaledTime + LocalLookupRetryInterval;
                    Log.Warning($"[MPChallenge][Players] ActivePlayers lookup failed; using fallback scan: {ex.Message}");
                }
            }

            if (!allowSceneFallback)
                return players;

            NetworkPlayer[] scenePlayers = UnityEngine.Object.FindObjectsOfType<NetworkPlayer>(true);
            foreach (NetworkPlayer player in scenePlayers)
                AddNetworkPlayerIfUnique(players, seen, player);

            return players;
        }

        private static void AddNetworkPlayerIfUnique(List<NetworkPlayer> players, HashSet<int> seen, NetworkPlayer player)
        {
            if (player == null)
                return;

            int id = player.GetInstanceID();
            if (!seen.Add(id))
                return;

            players.Add(player);
        }

        private static void EnsureTrickNames()
        {
            if (_trickNames.Length > 0 && Time.unscaledTime < _nextTrickCatalogRefreshTime)
                return;

            _nextTrickCatalogRefreshTime = Time.unscaledTime + TrickCatalogRefreshInterval;
            string[] selectedNames = _lineTrickIndexes
                .Select(index => _trickNames.Length > 0
                    ? _trickNames[Mathf.Clamp(index, 0, _trickNames.Length - 1)]
                    : null)
                .ToArray();

            string[] grindNames = GrindPoseEditor.GetAvailableGrindNames();
            string[] refreshedNames = ChallengeExtraTrickNames
                .Concat(TrickMods.GetAvailableTrickNames().OrderBy(name => name))
                .Concat(grindNames)
                .Concat(_observedChallengeNames)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(GetChallengeDisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (_trickNames.SequenceEqual(refreshedNames, StringComparer.OrdinalIgnoreCase))
                return;

            _trickNames = refreshedNames;
            Log.Msg(
                $"[MPChallenge][Picker] Loaded {_trickNames.Length} names, " +
                $"including {grindNames.Length} runtime grind entries.");
            for (int i = 0; i < _lineTrickIndexes.Count; i++)
            {
                int selectedIndex = i < selectedNames.Length && !string.IsNullOrWhiteSpace(selectedNames[i])
                    ? Array.FindIndex(
                        _trickNames,
                        name => string.Equals(name, selectedNames[i], StringComparison.OrdinalIgnoreCase))
                    : -1;
                _lineTrickIndexes[i] = selectedIndex >= 0 ? selectedIndex : 0;
            }
        }

        private static void EnsureLineState(int index)
        {
            while (_dropdownOpen.Count <= index)
            {
                _dropdownOpen.Add(false);
            }

            while (_dropdownScroll.Count <= index)
            {
                _dropdownScroll.Add(Vector2.zero);
            }

            if (_lineTrickIndexes[index] < 0 || _lineTrickIndexes[index] >= _trickNames.Length)
            {
                _lineTrickIndexes[index] = 0;
            }
        }

        private static void CheckLocalLineCompletion()
        {
            if (_activeChallenge == null)
                return;

            string localKey = GetLocalPlayerKey();
            if (string.IsNullOrEmpty(localKey))
                return;

            if (!IsLocalBikeAttemptAllowed(localKey))
            {
                _lastCompletionDiagnostic = "Waiting for your turn.";
                return;
            }

            if (_activeChallenge.PlayerCompleted.TryGetValue(localKey, out bool alreadyComplete) && alreadyComplete)
                return;

            if (!_currentAttemptHasConfirmedLanding)
            {
                _lastCompletionDiagnostic = "Waiting for landing confirmation.";
                return;
            }

            List<string> history = GetCurrentTrickHistory();
            UpdateCompletionDiagnostics(history);
            List<string> attemptHistory = _capturedAttemptTricks.Count > 0
                ? new List<string>(_capturedAttemptTricks)
                : GetHistorySinceEntry(history);
            attemptHistory = SanitizeAttemptTrickSequence(attemptHistory);

            if (attemptHistory.Count == 0)
            {
                _lastCompletionDiagnostic = "No new tricks since entering the area.";
                return;
            }

            string attemptSignature = string.Join("\u001f", attemptHistory.Select(NormalizeTrickName));
            if (string.Equals(attemptSignature, _lastCheckedAttemptSignature, StringComparison.Ordinal))
            {
                _lastCompletionDiagnostic = "Already checked this attempt history.";
                return;
            }

            _lastCheckedAttemptSignature = attemptSignature;

            if (string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal))
            {
                CaptureRecordedSetPosition("landing");
                List<string> recordedSet = BuildRecordedSetTricks(attemptHistory);
                if (recordedSet.Count == 0)
                {
                    _lastCompletionDiagnostic = "No recordable tricks in this set.";
                    return;
                }

                SubmitBikeRecordedSet(recordedSet);
                Log.Msg($"[MPChallenge][BIKE] Local recorded set: {string.Join(" + ", recordedSet)}");
                return;
            }

            if (_activeChallenge.Tricks == null || _activeChallenge.Tricks.Count == 0)
            {
                _lastCompletionDiagnostic = "No active set to match.";
                return;
            }

            if (!ContainsLineInOrder(attemptHistory, _activeChallenge.Tricks))
            {
                _lastCompletionDiagnostic = $"No match for {string.Join(" + ", _activeChallenge.Tricks)}.";
                return;
            }

            CompleteLocalChallenge(localKey);
            Log.Msg($"[MPChallenge][BIKE] Local landed line: {string.Join(" + ", _activeChallenge.Tricks)}");
        }

        private static List<string> BuildRecordedSetTricks(List<string> attemptHistory)
        {
            if (attemptHistory == null || attemptHistory.Count == 0)
                return new List<string>();

            return SanitizeAttemptTrickSequence(attemptHistory)
                .Take(MaxTricks)
                .ToList();
        }

        private static List<string> SanitizeAttemptTrickSequence(IEnumerable<string> attemptHistory)
        {
            List<string> sanitized = new List<string>();
            if (attemptHistory == null)
                return sanitized;

            foreach (string rawTrick in attemptHistory)
            {
                string displayName = GetChallengeDisplayName(rawTrick);
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                if (!TryGetSpinCaptureInfo(displayName, out int degrees, out bool standaloneSpin))
                {
                    sanitized.Add(displayName);
                    continue;
                }

                while (sanitized.Count > 0 &&
                       TryGetSpinCaptureInfo(sanitized[sanitized.Count - 1], out int previousDegrees, out bool previousStandaloneSpin) &&
                       previousStandaloneSpin &&
                       degrees >= previousDegrees)
                {
                    sanitized.RemoveAt(sanitized.Count - 1);
                }

                if (standaloneSpin &&
                    sanitized.Count > 0 &&
                    TryGetSpinCaptureInfo(sanitized[sanitized.Count - 1], out int lastDegrees, out _) &&
                    lastDegrees >= degrees)
                {
                    continue;
                }

                sanitized.Add(displayName);
            }

            return sanitized;
        }

        private static void BeginLocalSetRecording()
        {
            BeginLocalAttempt();
            _wasLocalPlayerInsideArea = true;
            CaptureRecordedSetPosition("recording start");
            _lastCompletionDiagnostic = "Recording set. Land to lock it in.";
        }

        private static void CaptureRecordedSetPosition(string source)
        {
            if (!IsLocalSetterRecording())
                return;

            if (!ChallengeAreaManager.TryGetLocalPlayerPose(
                    out Vector3 position,
                    out _,
                    out string poseSource))
            {
                return;
            }

            if (_recordedSetWorldPositions.Count >= MaxRecordedSetPositionSamples)
                _recordedSetWorldPositions.RemoveAt(0);

            _recordedSetWorldPositions.Add(position);
            Log.Msg($"[MPChallenge][Area] Captured set position from {source}: pos={FormatVector3(position)}, pose={poseSource}.");
        }

        private static bool IsLocalSetterRecording()
        {
            return _activeChallenge != null &&
                   string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal) &&
                   IsLocalBikeSetter();
        }

        private static bool TryBuildRecordedSetArea(
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 size)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            size = Vector3.zero;

            CaptureRecordedSetPosition("recording finish");
            if (_recordedSetWorldPositions.Count == 0)
                return false;

            ChallengeArea active = ChallengeAreaManager.Active;
            Vector3 origin = active != null ? active.transform.position : _recordedSetWorldPositions[0];
            rotation = active != null ? active.transform.rotation : Quaternion.identity;
            if (!IsFinite(rotation))
                rotation = Quaternion.identity;

            Quaternion inverseRotation = Quaternion.Inverse(rotation);
            Vector3 firstLocal = inverseRotation * (_recordedSetWorldPositions[0] - origin);
            Vector3 min = firstLocal;
            Vector3 max = firstLocal;

            for (int i = 1; i < _recordedSetWorldPositions.Count; i++)
            {
                Vector3 local = inverseRotation * (_recordedSetWorldPositions[i] - origin);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            Vector3 localCenter = (min + max) * 0.5f;
            Vector3 span = max - min;
            size = new Vector3(
                Mathf.Clamp(Mathf.Max(RecordedSetMinWidth, span.x + RecordedSetBoundsPadding * 2f), 0.1f, 1000f),
                Mathf.Clamp(Mathf.Max(RecordedSetMinHeight, span.y + RecordedSetBoundsPadding * 2f), 0.1f, 1000f),
                Mathf.Clamp(Mathf.Max(RecordedSetMinDepth, span.z + RecordedSetBoundsPadding * 2f), 0.1f, 1000f));
            position = origin + rotation * localCenter;

            return IsFinite(position) && IsFinite(rotation) && IsFinite(size);
        }

        private static void BeginLocalAttempt()
        {
            _capturedAttemptTricks.Clear();
            _capturedAttemptTrickTimes.Clear();
            _recordedSetWorldPositions.Clear();
            _entryHistorySnapshot.Clear();
            _entryHistorySnapshot.AddRange(GetCurrentTrickHistory());
            _lastCheckedAttemptSignature = null;
            _currentAttemptHasConfirmedLanding = false;
            _lastCompletionDiagnostic = "Tracking tricks performed inside the area.";
        }

        private static void CaptureLocalTrick(TrickDetection source, string trickName)
        {
            if (_activeChallenge == null ||
                source == null ||
                string.IsNullOrWhiteSpace(trickName) ||
                !IsLocalTrickDetection(source))
            {
                return;
            }

            CaptureAttemptTrick(trickName, "TrickDetection.RecordTrickToHistory()", false);
        }

        private static bool CaptureAttemptTrick(object trickValue, string source, bool checkCompletion)
        {
            if (_activeChallenge == null)
                return false;

            string captured = ExtractTrickText(trickValue);
            if (string.IsNullOrWhiteSpace(captured))
                return false;

            if (!ChallengeAreaManager.IsLocalPlayerInsideActiveArea())
                return false;

            if (!_wasLocalPlayerInsideArea)
            {
                Log.Msg("[MPChallenge] Local player entered challenge area.");
                BeginLocalAttempt();
                _wasLocalPlayerInsideArea = true;
            }

            captured = captured.Trim();
            RememberChallengeName(captured);
            float now = Time.unscaledTime;
            if (IsLikelyStaleRepeatedCapture(captured, now))
            {
                _lastCompletionDiagnostic = $"Ignored stale repeat: {GetChallengeDisplayName(captured)}.";
                Log.Msg($"[MPChallenge][Tricks] Ignored stale repeat trick from {source}: '{captured}'.");
                return false;
            }

            if (_capturedAttemptTricks.Count >= MaxCapturedAttemptTricks)
            {
                _capturedAttemptTricks.RemoveAt(0);
                if (_capturedAttemptTrickTimes.Count > 0)
                    _capturedAttemptTrickTimes.RemoveAt(0);
            }

            _capturedAttemptTricks.Add(captured);
            _capturedAttemptTrickTimes.Add(now);
            CaptureRecordedSetPosition(source);
            List<string> sanitizedAttempt = SanitizeAttemptTrickSequence(_capturedAttemptTricks);
            _lastSeenHistoryCount = sanitizedAttempt.Count;
            _lastSeenHistorySource = source;
            _lastSeenTrickText = string.Join(" | ", sanitizedAttempt.TakeLast(3));
            _lastCheckedAttemptSignature = null;
            Log.Msg($"[MPChallenge][Tricks] Captured local trick from {source}: '{captured}'.");
            if (checkCompletion)
                SafeCheckLocalLineCompletion();
            return true;
        }

        private static bool IsLikelyStaleRepeatedCapture(string trickName, float now)
        {
            if (_capturedAttemptTricks.Count == 0)
                return false;

            if (_capturedAttemptTrickTimes.Count != _capturedAttemptTricks.Count)
                return false;

            string normalized = NormalizeTrickName(trickName);
            if (string.IsNullOrEmpty(normalized))
                return false;

            string previousNormalized = NormalizeTrickName(_capturedAttemptTricks[_capturedAttemptTricks.Count - 1]);
            float elapsedSincePreviousCapture = now - _capturedAttemptTrickTimes[_capturedAttemptTrickTimes.Count - 1];
            if (string.Equals(previousNormalized, normalized, StringComparison.Ordinal))
            {
                return elapsedSincePreviousCapture >= 0f &&
                       elapsedSincePreviousCapture <= StaleRepeatCaptureWindow;
            }

            if (!ActiveChallengeRequiresRepeatedTrick(normalized))
                return false;

            bool alreadyCapturedThisTrick = _capturedAttemptTricks
                .Any(captured => string.Equals(NormalizeTrickName(captured), normalized, StringComparison.Ordinal));
            if (!alreadyCapturedThisTrick)
                return false;

            return elapsedSincePreviousCapture >= 0f &&
                   elapsedSincePreviousCapture <= StaleRepeatCaptureWindow;
        }

        private static bool ActiveChallengeRequiresRepeatedTrick(string normalizedTrickName)
        {
            if (_activeChallenge?.Tricks == null || string.IsNullOrEmpty(normalizedTrickName))
                return false;

            int count = 0;
            foreach (string requiredTrick in _activeChallenge.Tricks)
            {
                if (!string.Equals(NormalizeTrickName(requiredTrick), normalizedTrickName, StringComparison.Ordinal))
                    continue;

                count++;
                if (count > 1)
                    return true;
            }

            return false;
        }

        private static bool ApplyBikeStartSet(string playerKey, string playerName)
        {
            if (_activeChallenge == null ||
                !string.Equals(_activeChallenge.Phase, BikePhaseChoosing, StringComparison.Ordinal) ||
                !string.Equals(playerKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            EnsureBikePlayerState(playerKey, playerName, true);
            _activeChallenge.Tricks.Clear();
            _activeChallenge.Phase = BikePhaseSetting;
            _activeChallenge.CurrentSetterName = GetBikePlayerName(playerKey, playerName);
            _activeChallenge.CurrentMatcherIndex = -1;
            _activeChallenge.CurrentMatcherKey = null;
            _activeChallenge.CurrentMatcherName = null;
            ResetBikeRoundAttemptState();
            _activeChallenge.PlayerAttemptStatus[playerKey] = BikeAttemptPending;
            _activeChallenge.PlayerAttemptsRemaining[playerKey] = 1;
            ResetCompletionDiagnostics();
            _lastCompletionDiagnostic = $"{_activeChallenge.CurrentSetterName} is recording a set.";
            return true;
        }

        private static bool ApplyBikeSetLine(
            string playerKey,
            string playerName,
            List<string> tricks,
            float[] position = null,
            float[] rotation = null,
            float[] size = null)
        {
            if (_activeChallenge == null ||
                !string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal) ||
                !string.Equals(playerKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            List<string> normalizedTricks = (tricks ?? new List<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(GetChallengeDisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(MaxTricks)
                .ToList();
            if (normalizedTricks.Count == 0)
                return false;

            EnsureBikePlayerState(playerKey, playerName, true);
            _activeChallenge.Tricks = normalizedTricks;
            _activeChallenge.CurrentSetterName = GetBikePlayerName(playerKey, playerName);
            ApplyRecordedSetArea(position, rotation, size);
            BeginBikeMatcherPhase();
            return true;
        }

        private static bool ApplyRecordedSetArea(float[] position, float[] rotation, float[] size)
        {
            if (_activeChallenge == null ||
                position == null ||
                rotation == null ||
                size == null ||
                !IsValidVector(position, 3, -100000f, 100000f) ||
                !IsValidQuaternion(rotation) ||
                !IsValidVector(size, 3, 0.1f, 1000f))
            {
                return false;
            }

            Vector3 resolvedPosition = ArrayToVector3(position, Vector3.zero);
            Quaternion resolvedRotation = ArrayToQuaternion(rotation, Quaternion.identity);
            Vector3 resolvedSize = ArrayToVector3(size, Vector3.one);

            _activeChallenge.Position = Vector3ToArray(resolvedPosition);
            _activeChallenge.Rotation = QuaternionToArray(resolvedRotation);
            _activeChallenge.Size = Vector3ToArray(resolvedSize);
            _activeAreaStateSignature = BuildAreaStateSignature(_activeChallenge);

            if (ChallengeAreaManager.Active != null)
            {
                ChallengeAreaManager.SetPosition(resolvedPosition);
                ChallengeAreaManager.SetRotation(resolvedRotation);
                ChallengeAreaManager.SetSize(resolvedSize);
                ChallengeAreaManager.SetCompleted(false);
            }

            Log.Msg(
                $"[MPChallenge][Area] Applied recorded set boundary: pos={FormatVector3(resolvedPosition)}, rot={FormatQuaternion(resolvedRotation)}, size={FormatVector3(resolvedSize)}.");
            return true;
        }

        private static bool ApplyBikeLanded(string playerKey, string playerName)
        {
            if (_activeChallenge == null || string.IsNullOrEmpty(playerKey))
                return false;

            EnsureBikePlayerState(playerKey, playerName, true);
            if (IsBikePlayerOut(playerKey))
                return false;

            if (string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal))
            {
                if (!string.Equals(playerKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (_activeChallenge.Tricks == null || _activeChallenge.Tricks.Count == 0)
                    return false;

                _activeChallenge.CurrentSetterName = GetBikePlayerName(playerKey, playerName);
                BeginBikeMatcherPhase();
                return true;
            }

            if (!string.Equals(_activeChallenge.Phase, BikePhaseMatching, StringComparison.Ordinal) ||
                !string.Equals(playerKey, _activeChallenge.CurrentMatcherKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _activeChallenge.PlayerAttemptStatus[playerKey] = BikeAttemptLanded;
            _activeChallenge.PlayerCompleted[playerKey] = true;
            _activeChallenge.PlayerAttemptsRemaining[playerKey] = 0;
            _lastCompletionDiagnostic = $"{GetBikePlayerName(playerKey, playerName)} matched the set.";
            AdvanceToNextBikeMatcher();
            return true;
        }

        private static bool ApplyBikeMiss(string playerKey, string playerName)
        {
            if (_activeChallenge == null ||
                !string.Equals(_activeChallenge.Phase, BikePhaseMatching, StringComparison.Ordinal) ||
                !string.Equals(playerKey, _activeChallenge.CurrentMatcherKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            EnsureBikePlayerState(playerKey, playerName, true);
            int attemptsRemaining = GetBikeAttemptsRemaining(playerKey);
            if (attemptsRemaining > 1)
            {
                _activeChallenge.PlayerAttemptsRemaining[playerKey] = attemptsRemaining - 1;
                _lastCompletionDiagnostic =
                    $"{GetBikePlayerName(playerKey, playerName)} used a last-letter rebate. One try remains.";
                return true;
            }

            int letters = Mathf.Clamp(GetBikeLetterCount(playerKey) + 1, 0, MaxBikeLetters);
            _activeChallenge.PlayerLetters[playerKey] = letters;
            _activeChallenge.PlayerCompleted[playerKey] = false;
            _activeChallenge.PlayerAttemptsRemaining[playerKey] = 0;
            _activeChallenge.PlayerAttemptStatus[playerKey] =
                letters >= MaxBikeLetters ? BikeAttemptOut : BikeAttemptMissed;
            _lastCompletionDiagnostic =
                $"{GetBikePlayerName(playerKey, playerName)} takes {FormatBikeLetters(playerKey)}.";

            AdvanceToNextBikeMatcher();
            return true;
        }

        private static bool ApplyBikeSetterFailed(string playerKey, string playerName)
        {
            if (_activeChallenge == null ||
                !string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal) ||
                !string.Equals(playerKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            EnsureBikePlayerState(playerKey, playerName, true);
            _activeChallenge.PlayerAttemptStatus[playerKey] = BikeAttemptMissed;
            _activeChallenge.PlayerCompleted[playerKey] = false;
            _activeChallenge.PlayerAttemptsRemaining[playerKey] = 0;
            _lastCompletionDiagnostic =
                $"{GetBikePlayerName(playerKey, playerName)} did not set the line. No letters awarded.";
            AdvanceToNextBikeSetter();
            return true;
        }

        private static void ResetBikeRoundAttemptState()
        {
            EnsureBikeStateCollections();
            foreach (string key in _activeChallenge.TurnOrder.ToArray())
            {
                _activeChallenge.PlayerCompleted[key] = false;
                _activeChallenge.PlayerAttemptsRemaining[key] = 0;
                _activeChallenge.PlayerAttemptStatus[key] = IsBikePlayerOut(key)
                    ? BikeAttemptOut
                    : string.Empty;
            }
        }

        private static void BeginBikeMatcherPhase()
        {
            EnsureBikeStateCollections();
            ResetBikeRoundAttemptState();

            string setterKey = _activeChallenge.CurrentSetterKey;
            _activeChallenge.PlayerAttemptStatus[setterKey] = BikeAttemptSet;
            _activeChallenge.PlayerCompleted[setterKey] = true;
            _activeChallenge.PlayerAttemptsRemaining[setterKey] = 0;

            foreach (string key in _activeChallenge.TurnOrder)
            {
                if (!IsStableNetworkPlayerKey(key) ||
                    IsBikePlayerOut(key) ||
                    string.Equals(key, setterKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _activeChallenge.PlayerAttemptStatus[key] = BikeAttemptPending;
                _activeChallenge.PlayerCompleted[key] = false;
                _activeChallenge.PlayerAttemptsRemaining[key] = GetBikeAttemptsAllowed(key);
            }

            _activeChallenge.Phase = BikePhaseMatching;
            _activeChallenge.CurrentMatcherIndex = _activeChallenge.SetterIndex;
            _lastCompletionDiagnostic = $"{GetCurrentBikeSetterName()} set the line.";
            AdvanceToNextBikeMatcher();
        }

        private static void AdvanceToNextBikeMatcher()
        {
            EnsureBikeStateCollections();
            if (_activeChallenge.TurnOrder.Count == 0)
            {
                EndBikeGame();
                return;
            }

            int startIndex = Mathf.Clamp(_activeChallenge.CurrentMatcherIndex, -1, Math.Max(0, _activeChallenge.TurnOrder.Count - 1));
            for (int offset = 1; offset <= _activeChallenge.TurnOrder.Count; offset++)
            {
                int index = (startIndex + offset) % _activeChallenge.TurnOrder.Count;
                string key = _activeChallenge.TurnOrder[index];
                if (!string.Equals(GetBikeAttemptStatus(key), BikeAttemptPending, StringComparison.Ordinal))
                    continue;

                _activeChallenge.CurrentMatcherIndex = index;
                _activeChallenge.CurrentMatcherKey = key;
                _activeChallenge.CurrentMatcherName = GetBikePlayerName(key);
                return;
            }

            AdvanceToNextBikeSetter();
        }

        private static void AdvanceToNextBikeSetter()
        {
            EnsureBikeStateCollections();

            if (GetActiveBikePlayerCount() <= 1)
            {
                EndBikeGame();
                return;
            }

            int startIndex = _activeChallenge.SetterIndex;
            if (startIndex < 0 || startIndex >= _activeChallenge.TurnOrder.Count)
            {
                startIndex = _activeChallenge.TurnOrder.FindIndex(key =>
                    string.Equals(key, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase));
            }

            if (startIndex < 0)
                startIndex = 0;

            for (int offset = 1; offset <= _activeChallenge.TurnOrder.Count; offset++)
            {
                int index = (startIndex + offset) % _activeChallenge.TurnOrder.Count;
                string key = _activeChallenge.TurnOrder[index];
                if (!IsStableNetworkPlayerKey(key) || IsBikePlayerOut(key))
                    continue;

                _activeChallenge.SetterIndex = index;
                _activeChallenge.CurrentSetterKey = key;
                _activeChallenge.CurrentSetterName = GetBikePlayerName(key);
                _activeChallenge.CurrentMatcherIndex = -1;
                _activeChallenge.CurrentMatcherKey = null;
                _activeChallenge.CurrentMatcherName = null;
                _activeChallenge.Phase = BikePhaseChoosing;
                _activeChallenge.Tricks.Clear();
                ResetBikeRoundAttemptState();
                ResetCompletionDiagnostics();
                _lastCompletionDiagnostic = $"{_activeChallenge.CurrentSetterName} is choosing the next set.";
                return;
            }

            EndBikeGame();
        }

        private static void EndBikeGame()
        {
            EnsureBikeStateCollections();
            string winnerKey = _activeChallenge.TurnOrder
                .FirstOrDefault(key => IsStableNetworkPlayerKey(key) && !IsBikePlayerOut(key));

            _activeChallenge.Phase = BikePhaseGameOver;
            _activeChallenge.WinnerKey = winnerKey;
            _activeChallenge.WinnerName = !string.IsNullOrEmpty(winnerKey) ? GetBikePlayerName(winnerKey) : "No winner";
            _activeChallenge.CurrentMatcherIndex = -1;
            _activeChallenge.CurrentMatcherKey = null;
            _activeChallenge.CurrentMatcherName = null;
            _lastCompletionDiagnostic = !string.IsNullOrEmpty(winnerKey)
                ? $"{_activeChallenge.WinnerName} wins."
                : "Game ended.";
        }

        private static List<string> GetHistorySinceEntry(List<string> history)
        {
            if (history == null || history.Count == 0)
                return new List<string>();

            int commonPrefix = 0;
            int maxPrefix = Math.Min(history.Count, _entryHistorySnapshot.Count);
            while (commonPrefix < maxPrefix &&
                   string.Equals(
                       NormalizeTrickName(history[commonPrefix]),
                       NormalizeTrickName(_entryHistorySnapshot[commonPrefix]),
                       StringComparison.Ordinal))
            {
                commonPrefix++;
            }

            if (commonPrefix == history.Count && commonPrefix == _entryHistorySnapshot.Count)
                return new List<string>();

            return history.Skip(commonPrefix).ToList();
        }

        private static void CompleteLocalChallenge(string localKey)
        {
            if (_activeChallenge == null || string.IsNullOrEmpty(localKey))
                return;

            if (IsLocalCreator())
            {
                if (ApplyBikeLanded(localKey, GetLocalPlayerName()))
                {
                    _activeChallenge.Revision++;
                    PublishActiveChallengeState();
                    ChallengeAreaManager.SetCompleted(IsLocalPlayerComplete());
                }
                return;
            }

            _pendingLocalCompletionChallengeId = _activeChallenge.ChallengeId;
            _activeChallenge.PlayerCompleted[localKey] = true;
            _activeChallenge.PlayerAttemptStatus[localKey] = BikeAttemptLanded;
            ChallengeAreaManager.SetCompleted(true);
            SendLocalCompletionCommand();
        }

        private static void SubmitBikeStartSet()
        {
            if (_activeChallenge == null)
                return;

            string localKey = GetLocalPlayerKey();
            if (!string.Equals(localKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
                return;

            if (!ChallengeAreaManager.IsLocalPlayerInsideActiveArea())
            {
                _lastCompletionDiagnostic = "Enter the challenge box before recording.";
                return;
            }

            if (IsLocalCreator())
            {
                if (!ApplyBikeStartSet(localKey, GetLocalPlayerName()))
                    return;

                BeginLocalSetRecording();
                _activeChallenge.Revision++;
                PublishActiveChallengeState();
                ChallengeAreaManager.SetCompleted(false);
                return;
            }

            NetworkChallengeCommand command = new NetworkChallengeCommand
            {
                Version = ProtocolVersion,
                Type = BikeCommandStartSet,
                ChallengeId = _activeChallenge.ChallengeId,
                PlayerKey = localKey,
                PlayerName = GetLocalPlayerName()
            };

            if (!TryRaiseCommand(command))
            {
                _lastCompletionDiagnostic = "Could not notify the host to start recording.";
                return;
            }

            if (!ApplyBikeStartSet(localKey, GetLocalPlayerName()))
                return;

            BeginLocalSetRecording();
        }

        private static void SubmitBikeRecordedSet(List<string> tricks)
        {
            if (_activeChallenge == null || tricks == null || tricks.Count == 0)
                return;

            string localKey = GetLocalPlayerKey();
            if (!string.Equals(localKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
                return;

            float[] recordedPosition = null;
            float[] recordedRotation = null;
            float[] recordedSize = null;
            if (TryBuildRecordedSetArea(out Vector3 areaPosition, out Quaternion areaRotation, out Vector3 areaSize))
            {
                recordedPosition = Vector3ToArray(areaPosition);
                recordedRotation = QuaternionToArray(areaRotation);
                recordedSize = Vector3ToArray(areaSize);
            }

            if (IsLocalCreator())
            {
                if (ApplyBikeSetLine(localKey, GetLocalPlayerName(), tricks, recordedPosition, recordedRotation, recordedSize))
                {
                    _activeChallenge.Revision++;
                    PublishActiveChallengeState();
                    ChallengeAreaManager.SetCompleted(IsLocalPlayerComplete());
                }
                return;
            }

            TryRaiseCommand(new NetworkChallengeCommand
            {
                Version = ProtocolVersion,
                Type = BikeCommandSetLine,
                ChallengeId = _activeChallenge.ChallengeId,
                PlayerKey = localKey,
                PlayerName = GetLocalPlayerName(),
                Tricks = tricks,
                Position = recordedPosition,
                Rotation = recordedRotation,
                Size = recordedSize
            });
        }

        private static void SubmitBikeMiss()
        {
            if (_activeChallenge == null)
                return;

            string localKey = GetLocalPlayerKey();
            if (!string.Equals(localKey, _activeChallenge.CurrentMatcherKey, StringComparison.OrdinalIgnoreCase))
                return;

            if (IsLocalCreator())
            {
                if (ApplyBikeMiss(localKey, GetLocalPlayerName()))
                {
                    _activeChallenge.Revision++;
                    PublishActiveChallengeState();
                    ChallengeAreaManager.SetCompleted(IsLocalPlayerComplete());
                }
                return;
            }

            TryRaiseCommand(new NetworkChallengeCommand
            {
                Version = ProtocolVersion,
                Type = BikeCommandMiss,
                ChallengeId = _activeChallenge.ChallengeId,
                PlayerKey = localKey,
                PlayerName = GetLocalPlayerName()
            });
        }

        private static void SubmitBikeSetterFailed()
        {
            if (_activeChallenge == null)
                return;

            string localKey = GetLocalPlayerKey();
            if (!string.Equals(localKey, _activeChallenge.CurrentSetterKey, StringComparison.OrdinalIgnoreCase))
                return;

            if (IsLocalCreator())
            {
                if (ApplyBikeSetterFailed(localKey, GetLocalPlayerName()))
                {
                    _activeChallenge.Revision++;
                    PublishActiveChallengeState();
                    ChallengeAreaManager.SetCompleted(IsLocalPlayerComplete());
                }
                return;
            }

            TryRaiseCommand(new NetworkChallengeCommand
            {
                Version = ProtocolVersion,
                Type = BikeCommandSetterFailed,
                ChallengeId = _activeChallenge.ChallengeId,
                PlayerKey = localKey,
                PlayerName = GetLocalPlayerName()
            });
        }

        private static List<string> GetCurrentTrickHistory()
        {
            if (_trickDetection == null)
                ResolveLocalTrickDetection(null);

            List<string> history = new List<string>();
            if (_trickDetection == null)
            {
                AddPlayerTrickGameplayFallback(history);
                return history;
            }

            AddTrickDetectionHistory(history);
            AddPlayerTrickGameplayFallback(history);

            if (history.Count > 0)
                return history;

            object rawHistory = TryGetMemberValue(_trickDetection, "_trickHistory") ??
                                TryGetMemberValue(_trickDetection, "trickHistory") ??
                                TryGetMemberValue(_trickDetection, "TrickHistory");

            if (rawHistory == null)
                return history;

            AddHistoryItems(history, rawHistory, "_trickHistory");
            return history;
        }

        private static void ResolveLocalTrickDetection(GameObject playerObject)
        {
            TrickDetection local = null;

            if (Memory.rMbCharacter != null)
                local = Memory.rMbCharacter.GetComponentInChildren<TrickDetection>(true);
            if (local == null && Memory.physicsDrivenCharacter != null)
                local = Memory.physicsDrivenCharacter.GetComponentInChildren<TrickDetection>(true);
            if (local == null && playerObject != null)
                local = playerObject.GetComponentInChildren<TrickDetection>(true);

            if (local == null || local == _trickDetection)
                return;

            _trickDetection = local;
            Log.Msg($"[MPChallenge][Tricks] Bound local TrickDetection on '{local.gameObject.name}'.");
        }

        private static void BindPlayerTrickGameplayEvents(GameObject playerObject)
        {
            if (!AreTrickHooksEnabled())
                return;

            PlayerTrickGameplay gameplay = ResolvePlayerTrickGameplay(playerObject);
            if (gameplay == null)
                return;

            if (_subscribedTrickGameplay != null &&
                _subscribedTrickGameplay.GetInstanceID() == gameplay.GetInstanceID())
            {
                return;
            }

            if (Time.unscaledTime < _nextTrickGameplayBindRetryTime)
                return;

            UnbindPlayerTrickGameplayEvents();
            EnsurePlayerTrickGameplayDelegates();

            bool addedAny = false;
            addedAny |= AddTrickGameplayListener(
                GetPlayerTrickUnityEvent(gameplay, "OnAbilityPerformed"),
                _onAbilityPerformedListener,
                "OnAbilityPerformed");
            addedAny |= AddTrickGameplayListener(
                GetPlayerTrickUnityEvent(gameplay, "OnAbilityComboConfirmed"),
                _onAbilityComboConfirmedListener,
                "OnAbilityComboConfirmed");
            addedAny |= AddTrickGameplayListener(
                GetPlayerTrickUnityEvent(gameplay, "OnConfirmLanding"),
                _onConfirmLandingListener,
                "OnConfirmLanding");
            addedAny |= AddTrickGameplayListener(
                GetPlayerTrickUnityEvent(gameplay, "OnEndLineConfirmed"),
                _onEndLineConfirmedListener,
                "OnEndLineConfirmed");
            addedAny |= AddTrickGameplayListener(
                GetPlayerTrickUnityEvent(gameplay, "OnComboFailed"),
                _onComboFailedListener,
                "OnComboFailed");

            if (!addedAny)
            {
                _nextTrickGameplayBindRetryTime = Time.unscaledTime + TrickGameplayBindRetryInterval;
                Log.Warning("[MPChallenge][Tricks] PlayerTrickGameplay events were not available; will retry.");
                return;
            }

            _subscribedTrickGameplay = gameplay;
            _nextTrickGameplayBindRetryTime = 0f;
            Log.Msg($"[MPChallenge][Tricks] Bound PlayerTrickGameplay events on '{gameplay.gameObject.name}'.");
        }

        private static PlayerTrickGameplay ResolvePlayerTrickGameplay(GameObject playerObject)
        {
            PlayerTrickGameplay gameplay = null;

            if (Memory.playerTrickGameplay != null)
                gameplay = Memory.playerTrickGameplay;
            if (gameplay == null && Memory.rMbCharacter != null)
                gameplay = Memory.rMbCharacter.GetComponentInChildren<PlayerTrickGameplay>(true);
            if (gameplay == null && Memory.physicsDrivenCharacter != null)
                gameplay = Memory.physicsDrivenCharacter.GetComponentInChildren<PlayerTrickGameplay>(true);
            if (gameplay == null && playerObject != null)
                gameplay = playerObject.GetComponentInChildren<PlayerTrickGameplay>(true);

            if (gameplay != null)
                Memory.playerTrickGameplay = gameplay;

            return gameplay;
        }

        private static void UnbindPlayerTrickGameplayEvents()
        {
            if (_subscribedTrickGameplay == null)
                return;

            try
            {
                RemoveTrickGameplayListener(
                    GetPlayerTrickUnityEvent(_subscribedTrickGameplay, "OnAbilityPerformed"),
                    _onAbilityPerformedListener);
                RemoveTrickGameplayListener(
                    GetPlayerTrickUnityEvent(_subscribedTrickGameplay, "OnAbilityComboConfirmed"),
                    _onAbilityComboConfirmedListener);
                RemoveTrickGameplayListener(
                    GetPlayerTrickUnityEvent(_subscribedTrickGameplay, "OnConfirmLanding"),
                    _onConfirmLandingListener);
                RemoveTrickGameplayListener(
                    GetPlayerTrickUnityEvent(_subscribedTrickGameplay, "OnEndLineConfirmed"),
                    _onEndLineConfirmedListener);
                RemoveTrickGameplayListener(
                    GetPlayerTrickUnityEvent(_subscribedTrickGameplay, "OnComboFailed"),
                    _onComboFailedListener);
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Tricks] Failed to unbind PlayerTrickGameplay events: {ex.Message}");
            }
            finally
            {
                _subscribedTrickGameplay = null;
            }
        }

        private static void EnsurePlayerTrickGameplayDelegates()
        {
            if (_onAbilityPerformedListener == null)
                _onAbilityPerformedListener =
                    DelegateSupport.ConvertDelegate<UnityAction>(HandleAbilityPerformed);
            if (_onAbilityComboConfirmedListener == null)
                _onAbilityComboConfirmedListener =
                    DelegateSupport.ConvertDelegate<UnityAction>(HandleAbilityComboConfirmed);
            if (_onConfirmLandingListener == null)
                _onConfirmLandingListener =
                    DelegateSupport.ConvertDelegate<UnityAction>(HandleConfirmLanding);
            if (_onEndLineConfirmedListener == null)
                _onEndLineConfirmedListener =
                    DelegateSupport.ConvertDelegate<UnityAction>(HandleEndLineConfirmed);
            if (_onComboFailedListener == null)
                _onComboFailedListener =
                    DelegateSupport.ConvertDelegate<UnityAction>(HandleComboFailed);
        }

        private static UnityEvent GetPlayerTrickUnityEvent(PlayerTrickGameplay gameplay, string eventName)
        {
            return TryGetMemberValue(gameplay, eventName) as UnityEvent;
        }

        private static bool AddTrickGameplayListener(UnityEvent unityEvent, UnityAction listener, string eventName)
        {
            if (unityEvent == null || listener == null)
                return false;

            unityEvent.RemoveListener(listener);
            unityEvent.AddListener(listener);
            Log.Msg($"[MPChallenge][Tricks] Subscribed to PlayerTrickGameplay.{eventName}.");
            return true;
        }

        private static void RemoveTrickGameplayListener(UnityEvent unityEvent, UnityAction listener)
        {
            if (unityEvent == null || listener == null)
                return;

            unityEvent.RemoveListener(listener);
        }

        private static void HandleAbilityPerformed()
        {
            HandlePlayerTrickGameplayEvent("OnAbilityPerformed", false, false);
        }

        private static void HandleAbilityComboConfirmed()
        {
            HandlePlayerTrickGameplayEvent("OnAbilityComboConfirmed", false, false);
        }

        private static void HandleConfirmLanding()
        {
            HandlePlayerTrickGameplayEvent("OnConfirmLanding", true, true);
        }

        private static void HandleEndLineConfirmed()
        {
            HandlePlayerTrickGameplayEvent("OnEndLineConfirmed", true, true);
        }

        private static void HandleComboFailed()
        {
            try
            {
                if (_activeChallenge == null || !ChallengeAreaManager.IsLocalPlayerInsideActiveArea())
                    return;

                if (string.Equals(_activeChallenge.Phase, BikePhaseSetting, StringComparison.Ordinal) &&
                    IsLocalBikeSetter())
                {
                    SubmitBikeSetterFailed();
                    _lastCompletionDiagnostic = "Set failed; passing turn.";
                    return;
                }

                BeginLocalAttempt();
                _lastCompletionDiagnostic = "Combo failed; restarted local attempt.";
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Tricks] OnComboFailed handling failed: {ex.Message}");
            }
        }

        private static void HandlePlayerTrickGameplayEvent(
            string eventName,
            bool confirmsLanding,
            bool checkCompletion)
        {
            if (!AreTrickHooksEnabled())
                return;

            try
            {
                if (_activeChallenge == null)
                    return;

                if (!ChallengeAreaManager.IsLocalPlayerInsideActiveArea())
                    return;

                CapturePlayerTrickGameplaySnapshot(eventName);
                if (confirmsLanding)
                    _currentAttemptHasConfirmedLanding = true;

                if (checkCompletion)
                    SafeCheckLocalLineCompletion();
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Tricks] {eventName} handling failed: {ex.Message}");
            }
        }

        private static void CapturePlayerTrickGameplaySnapshot(string eventName)
        {
            PlayerTrickGameplay gameplay = ResolvePlayerTrickGameplay(null);
            if (gameplay == null)
                return;

            HashSet<string> capturedThisEvent = new HashSet<string>(StringComparer.Ordinal);
            CaptureUniqueGameplayTrick(
                gameplay.GetCurrentAbilityTrick(),
                $"PlayerTrickGameplay.{eventName}.GetCurrentAbilityTrick()",
                capturedThisEvent);
            CaptureUniqueGameplayTrick(
                gameplay.GetLastAbilityTrick(),
                $"PlayerTrickGameplay.{eventName}.GetLastAbilityTrick()",
                capturedThisEvent);
        }

        private static void CaptureUniqueGameplayTrick(
            object trickValue,
            string source,
            HashSet<string> capturedThisEvent)
        {
            string text = ExtractTrickText(trickValue);
            string normalized = NormalizeTrickName(text);
            if (string.IsNullOrEmpty(normalized) || !capturedThisEvent.Add(normalized))
                return;

            CaptureAttemptTrick(text, source, false);
        }

        private static bool IsLocalTrickDetection(TrickDetection candidate)
        {
            if (candidate == null)
                return false;

            if (_trickDetection == null)
                ResolveLocalTrickDetection(null);

            return _trickDetection != null &&
                   candidate.GetInstanceID() == _trickDetection.GetInstanceID();
        }

        private static void AddTrickDetectionHistory(List<string> history)
        {
            if (_trickDetection == null)
                return;

            try
            {
                Il2CppSystem.Collections.Generic.List<string> trickHistory = _trickDetection.GetTrickHistory();
                if (trickHistory == null)
                    return;

                int originalCount = history.Count;
                for (int i = 0; i < trickHistory.Count; i++)
                {
                    AddHistoryText(history, trickHistory[(Index)i], "TrickDetection.GetTrickHistory()");
                }

                if (history.Count == originalCount)
                    AddHistoryItems(history, trickHistory, "TrickDetection.GetTrickHistory()");
            }
            catch (Exception ex)
            {
                _lastCompletionDiagnostic = $"GetTrickHistory failed: {ex.Message}";
            }
        }

        private static void AddPlayerTrickGameplayFallback(List<string> history)
        {
            if (Memory.playerTrickGameplay == null)
                return;

            try
            {
                AddHistoryText(history, Memory.playerTrickGameplay.GetCurrentAbilityTrick(), "PlayerTrickGameplay.GetCurrentAbilityTrick()");
                AddHistoryText(history, Memory.playerTrickGameplay.GetLastAbilityTrick(), "PlayerTrickGameplay.GetLastAbilityTrick()");
            }
            catch (Exception ex)
            {
                _lastCompletionDiagnostic = $"PlayerTrickGameplay read failed: {ex.Message}";
            }
        }

        private static void AddHistoryItems(List<string> history, object rawHistory, string source)
        {
            if (rawHistory == null)
                return;

            if (rawHistory is IEnumerable<string> managedStrings)
            {
                foreach (string value in managedStrings)
                    AddHistoryText(history, value, source);
                return;
            }

            System.Collections.IEnumerable enumerable = rawHistory as System.Collections.IEnumerable;
            if (enumerable == null)
            {
                AddHistoryText(history, rawHistory, source);
                return;
            }

            foreach (object item in enumerable)
            {
                AddHistoryText(history, item, source);
            }
        }

        private static void AddHistoryText(List<string> history, object value, string source)
        {
            string text = ExtractTrickText(value);
            if (string.IsNullOrWhiteSpace(text))
                return;

            history.Add(text);
            RememberChallengeName(text);
            _lastSeenHistorySource = source;
        }

        private static void RememberChallengeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !_observedChallengeNames.Add(name.Trim()))
                return;

            _nextTrickCatalogRefreshTime = 0f;
        }

        private static string ExtractTrickText(object value)
        {
            if (value == null)
                return null;

            if (value is string text)
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

            if (value is UnityEngine.Object unityObject && !string.IsNullOrWhiteSpace(unityObject.name))
                return unityObject.name.Trim();

            string readable = ExtractReadableString(value);
            if (!string.IsNullOrWhiteSpace(readable))
                return readable.Trim();

            string fallback = value.ToString();
            return string.IsNullOrWhiteSpace(fallback) || IsTypeLikeDisplayText(fallback) ? null : fallback.Trim();
        }

        private static void UpdateCompletionDiagnostics(List<string> history)
        {
            List<string> sanitizedHistory = SanitizeAttemptTrickSequence(history);
            _lastSeenHistoryCount = sanitizedHistory.Count;
            _lastSeenTrickText = sanitizedHistory.Count > 0 ? string.Join(" | ", sanitizedHistory.TakeLast(3)) : "none";
            if (sanitizedHistory.Count == 0)
                _lastSeenHistorySource = "none";
        }

        private static void ResetCompletionDiagnostics()
        {
            _capturedAttemptTricks.Clear();
            _capturedAttemptTrickTimes.Clear();
            _recordedSetWorldPositions.Clear();
            _entryHistorySnapshot.Clear();
            _lastCheckedAttemptSignature = null;
            _currentAttemptHasConfirmedLanding = false;
            _lastSeenHistoryCount = 0;
            _lastSeenHistorySource = "none";
            _lastSeenTrickText = "none";
            _lastCompletionDiagnostic = "Waiting for trick history.";
        }

        private static bool ContainsLineInOrder(List<string> history, List<string> requiredTricks)
        {
            int requiredIndex = 0;
            for (int i = 0; i < history.Count && requiredIndex < requiredTricks.Count; i++)
            {
                if (TrickNamesMatch(history[i], requiredTricks[requiredIndex]))
                {
                    requiredIndex++;
                }
            }

            return requiredIndex >= requiredTricks.Count;
        }

        private static bool TrickNamesMatch(string actual, string required)
        {
            string normalizedActual = NormalizeTrickName(actual);
            string normalizedRequired = NormalizeTrickName(required);

            if (string.IsNullOrEmpty(normalizedActual) || string.IsNullOrEmpty(normalizedRequired))
                return false;

            if (IsSpinRequirement(normalizedRequired, out int requiredDegrees))
                return ContainsDegreeToken(normalizedActual, requiredDegrees);

            if (normalizedRequired == "nose manual")
                return normalizedActual == "nose manual";

            if (normalizedRequired == "manual")
                return normalizedActual == "manual";

            return normalizedActual.Equals(normalizedRequired, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTrickName(string trickName)
        {
            if (string.IsNullOrWhiteSpace(trickName))
                return string.Empty;

            string normalized = GetChallengeDisplayName(trickName).ToLowerInvariant();
            normalized = normalized.Replace("nosemanual", "nose manual");
            normalized = normalized.Replace("nosey", "nose manual");
            normalized = normalized.Replace("nosemanny", "nose manual");
            normalized = normalized.Replace("nose manny", "nose manual");
            normalized = normalized.Replace("nose manual", "nose manual");
            normalized = normalized.Replace("mannie", "manual");
            normalized = normalized.Replace("manny", "manual");
            normalized = normalized.Replace("degrees", string.Empty);
            normalized = normalized.Replace("degree", string.Empty);
            normalized = normalized.Replace("rotation", string.Empty);
            normalized = normalized.Replace("rot", string.Empty);
            normalized = normalized.Replace("spin", string.Empty);
            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");
            return normalized.Trim();
        }

        private static string GetChallengeDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string separated = InsertWordBoundaries(name.Trim())
                .Replace("_", " ")
                .Replace("-", " ");
            string[] tokens = separated.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            List<string> displayTokens = tokens
                .Where(token =>
                    !string.Equals(token, "BMX", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(token, "Left", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(token, "Right", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (displayTokens.Count >= 2 &&
                string.Equals(displayTokens[0], "Grind", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(displayTokens[1], "Pose", StringComparison.OrdinalIgnoreCase))
            {
                displayTokens.RemoveRange(0, 2);
            }

            return string.Join(" ", displayTokens);
        }

        private static bool TryGetSpinCaptureInfo(string trickName, out int degrees, out bool standaloneSpin)
        {
            degrees = 0;
            standaloneSpin = false;
            string displayName = GetChallengeDisplayName(trickName);
            if (string.IsNullOrWhiteSpace(displayName))
                return false;

            string[] tokens = displayName.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            int semanticTokenCount = 0;
            foreach (string token in tokens)
            {
                string cleanedToken = CleanTrickToken(token);
                if (string.IsNullOrWhiteSpace(cleanedToken))
                    continue;

                if (int.TryParse(cleanedToken, out int parsedDegrees) &&
                    IsKnownSpinDegree(parsedDegrees))
                {
                    degrees = Mathf.Max(degrees, parsedDegrees);
                    continue;
                }

                if (IsRotationDescriptorToken(cleanedToken))
                    continue;

                semanticTokenCount++;
            }

            bool hasSpin = degrees > 0;
            standaloneSpin = hasSpin && semanticTokenCount == 0;
            return hasSpin;
        }

        private static string CleanTrickToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return string.Empty;

            char[] chars = token
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray();
            return new string(chars);
        }

        private static bool IsRotationDescriptorToken(string token)
        {
            return string.Equals(token, "degree", StringComparison.Ordinal) ||
                   string.Equals(token, "degrees", StringComparison.Ordinal) ||
                   string.Equals(token, "rotation", StringComparison.Ordinal) ||
                   string.Equals(token, "rot", StringComparison.Ordinal) ||
                   string.Equals(token, "spin", StringComparison.Ordinal);
        }

        private static bool IsKnownSpinDegree(int degrees)
        {
            string degreeText = degrees.ToString();
            for (int i = 0; i < ChallengeExtraTrickNames.Length; i++)
            {
                if (ChallengeExtraTrickNames[i] == degreeText)
                    return true;
            }

            return false;
        }

        private static string InsertWordBoundaries(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            List<char> chars = new List<char>(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 &&
                    char.IsUpper(current) &&
                    (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])))
                {
                    chars.Add(' ');
                }

                chars.Add(current);
            }

            return new string(chars.ToArray());
        }

        private static bool IsSpinRequirement(string normalizedRequired, out int degrees)
        {
            degrees = 0;
            if (!int.TryParse(normalizedRequired, out degrees))
                return false;

            return IsKnownSpinDegree(degrees);
        }

        private static bool ContainsDegreeToken(string text, int degrees)
        {
            string token = degrees.ToString();
            int index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                bool leftBoundary = index == 0 || !char.IsDigit(text[index - 1]);
                int afterIndex = index + token.Length;
                bool rightBoundary = afterIndex >= text.Length || !char.IsDigit(text[afterIndex]);
                if (leftBoundary && rightBoundary)
                    return true;

                index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
            }

            return false;
        }

        private static void EnsureNetworkBridge()
        {
            if (!IsNetworkingEnabled())
            {
                _networkStatus = "Challenge networking disabled by config.";
                return;
            }

            if (_networkBridgeRoot != null)
                return;
            if (Time.unscaledTime < _nextNetworkRetryTime)
                return;
            if (!CanUseNetwork())
                return;

            string setupStage = "create bridge root";
            try
            {
                GameObject root = new GameObject("RoweMod_MPChallenge_NetworkBridge");
                root.hideFlags = HideFlags.HideAndDontSave;
                root.SetActive(false);
                _networkBridgeRoot = root;

                setupStage = "create state event object";
                GameObject stateEventObject = new GameObject("ChallengeStateEvent");
                stateEventObject.transform.SetParent(root.transform, false);

                setupStage = "add state event component";
                _stateEvent = stateEventObject
                    .AddComponent(Il2CppType.Of<MBNetworkedEvent>())
                    .Cast<MBNetworkedEvent>();

                setupStage = "configure state event";
                _stateEvent.eventKey = ChallengeStateEventKey;
                _stateEvent.applyStoredStateOnEnable = true;
                EnsureNetworkEventCallbacks(_stateEvent);

                setupStage = "create state listeners";
                _stateStringListener =
                    DelegateSupport.ConvertDelegate<UnityAction<string>>(HandleNetworkStateJson);
                _stateClearedListener =
                    DelegateSupport.ConvertDelegate<UnityAction>(HandleNetworkStateCleared);
                if (_stateStringListener == null || _stateClearedListener == null)
                    throw new InvalidOperationException("State listener delegate conversion returned null.");

                setupStage = "register state listeners";
                _stateEvent.onString.AddListener(_stateStringListener);
                _stateEvent.onStateCleared.AddListener(_stateClearedListener);

                setupStage = "create command event object";
                GameObject commandEventObject = new GameObject("ChallengeCommandEvent");
                commandEventObject.transform.SetParent(root.transform, false);

                setupStage = "add command event component";
                _commandEvent = commandEventObject
                    .AddComponent(Il2CppType.Of<MBNetworkedEvent>())
                    .Cast<MBNetworkedEvent>();

                setupStage = "configure command event";
                _commandEvent.eventKey = ChallengeCommandEventKey;
                _commandEvent.applyStoredStateOnEnable = false;
                EnsureNetworkEventCallbacks(_commandEvent);

                setupStage = "create command listener";
                _commandStringListener =
                    DelegateSupport.ConvertDelegate<UnityAction<string>>(HandleNetworkCommandJson);
                if (_commandStringListener == null)
                    throw new InvalidOperationException("Command listener delegate conversion returned null.");

                setupStage = "register command listener";
                _commandEvent.onString.AddListener(_commandStringListener);

                setupStage = "activate bridge";
                root.SetActive(true);
                _nextPresenceBroadcastTime = 0f;
                _networkStatus = "Connected; listeners ready.";
                Log.Msg("[MPChallenge][Net] Registered pushed state and command listeners.");
            }
            catch (Exception ex)
            {
                RecordNetworkFailure(
                    $"listener setup failed during '{setupStage}' ({ex.GetType().Name}): {ex.Message}");
                ShutdownNetworkBridge();
            }
        }

        private static void EnsureNetworkEventCallbacks(MBNetworkedEvent networkEvent)
        {
            if (networkEvent == null)
                throw new InvalidOperationException("MBNetworkedEvent component creation returned null.");

            if (networkEvent.onReceived == null)
                networkEvent.onReceived = new UnityEvent();
            if (networkEvent.onRaised == null)
                networkEvent.onRaised = new UnityEvent();
            if (networkEvent.onStateApplied == null)
                networkEvent.onStateApplied = new UnityEvent();
            if (networkEvent.onStateCleared == null)
                networkEvent.onStateCleared = new UnityEvent();
            if (networkEvent.onBool == null)
                networkEvent.onBool = new MBBoolEvent();
            if (networkEvent.onInt == null)
                networkEvent.onInt = new MBIntEvent();
            if (networkEvent.onFloat == null)
                networkEvent.onFloat = new MBFloatEvent();
            if (networkEvent.onString == null)
                networkEvent.onString = new MBStringEvent();
        }

        private static void RestartNetworkBridge()
        {
            if (!IsNetworkingEnabled())
                return;

            ShutdownNetworkBridge();
            EnsureNetworkBridge();
        }

        private static void ShutdownNetworkBridge()
        {
            GameObject root = _networkBridgeRoot;
            try
            {
                if (_stateEvent != null)
                {
                    if (_stateStringListener != null && _stateEvent.onString != null)
                        _stateEvent.onString.RemoveListener(_stateStringListener);
                    if (_stateClearedListener != null && _stateEvent.onStateCleared != null)
                        _stateEvent.onStateCleared.RemoveListener(_stateClearedListener);
                }

                if (_commandEvent != null &&
                    _commandStringListener != null &&
                    _commandEvent.onString != null)
                {
                    _commandEvent.onString.RemoveListener(_commandStringListener);
                }

            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Net] Listener shutdown failed: {ex.Message}");
            }

            try
            {
                if (root != null)
                {
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MPChallenge][Net] Bridge destruction failed: {ex.Message}");
            }

            _networkBridgeRoot = null;
            _stateEvent = null;
            _commandEvent = null;
            _stateStringListener = null;
            _commandStringListener = null;
            _stateClearedListener = null;
        }

        private static void HandleNetworkStateJson(string json)
        {
            if (!IsNetworkingEnabled())
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > MaxStateJsonLength)
                {
                    Log.Warning("[MPChallenge][Net] Ignored empty or oversized challenge state.");
                    return;
                }

                NetworkChallengeState state =
                    JsonConvert.DeserializeObject<NetworkChallengeState>(json, NetworkJsonSettings);
                if (!ValidateNetworkState(state, out string reason))
                {
                    Log.Warning($"[MPChallenge][Net] Ignored invalid challenge state: {reason}");
                    return;
                }

                ApplyNetworkState(state, "listener");
                RecordNetworkSuccess("Challenge state synchronized.");
            }
            catch (Exception ex)
            {
                RecordNetworkFailure($"state handling failed: {ex.Message}");
            }
        }

        private static void HandleNetworkStateCleared()
        {
            if (!IsNetworkingEnabled())
                return;

            Log.Msg("[MPChallenge][Net] Stored challenge state was cleared.");
            ResetActiveChallenge();
            RecordNetworkSuccess("No active network challenge.");
        }

        private static void HandleNetworkCommandJson(string json)
        {
            if (!IsNetworkingEnabled())
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > MaxCommandJsonLength)
                    return;

                NetworkChallengeCommand command =
                    JsonConvert.DeserializeObject<NetworkChallengeCommand>(json, NetworkJsonSettings);
                if (!ValidateNetworkCommand(command))
                    return;

                if (string.Equals(command.Type, "presence", StringComparison.Ordinal))
                {
                    RegisterPresence(command.PlayerKey, command.PlayerName);
                    return;
                }

                if (_activeChallenge == null ||
                    !IsLocalCreator() ||
                    !string.Equals(command.ChallengeId, _activeChallenge.ChallengeId, StringComparison.Ordinal))
                {
                    return;
                }

                RegisterPresence(command.PlayerKey, command.PlayerName);
                bool changed = false;
                if (string.Equals(command.Type, BikeCommandLanded, StringComparison.Ordinal) ||
                    string.Equals(command.Type, "complete", StringComparison.Ordinal))
                {
                    changed = ApplyBikeLanded(command.PlayerKey, command.PlayerName);
                }
                else if (string.Equals(command.Type, BikeCommandMiss, StringComparison.Ordinal))
                {
                    changed = ApplyBikeMiss(command.PlayerKey, command.PlayerName);
                }
                else if (string.Equals(command.Type, BikeCommandStartSet, StringComparison.Ordinal))
                {
                    changed = ApplyBikeStartSet(command.PlayerKey, command.PlayerName);
                }
                else if (string.Equals(command.Type, BikeCommandSetLine, StringComparison.Ordinal))
                {
                    changed = ApplyBikeSetLine(
                        command.PlayerKey,
                        command.PlayerName,
                        command.Tricks,
                        command.Position,
                        command.Rotation,
                        command.Size);
                }
                else if (string.Equals(command.Type, BikeCommandSetterFailed, StringComparison.Ordinal))
                {
                    changed = ApplyBikeSetterFailed(command.PlayerKey, command.PlayerName);
                }

                if (!changed)
                    return;

                _activeChallenge.Revision++;
                PublishActiveChallengeState();
                Log.Msg($"[MPChallenge][BIKE] Accepted '{command.Type}' from {command.PlayerName} ({command.PlayerKey}).");
            }
            catch (Exception ex)
            {
                RecordNetworkFailure($"command handling failed: {ex.Message}");
            }
        }

        private static void ApplyNetworkState(NetworkChallengeState state, string source)
        {
            bool isNewChallenge =
                _activeChallenge == null ||
                !string.Equals(_activeChallenge.ChallengeId, state.ChallengeId, StringComparison.Ordinal);

            if (_activeChallenge != null &&
                string.Equals(_activeChallenge.ChallengeId, state.ChallengeId, StringComparison.Ordinal) &&
                state.Revision <= _activeChallenge.Revision)
            {
                return;
            }

            _activeChallenge = new ActiveChallenge
            {
                Version = state.Version,
                ChallengeId = state.ChallengeId,
                Revision = state.Revision,
                GameMode = string.IsNullOrWhiteSpace(state.GameMode) ? GameModeBike : state.GameMode,
                Phase = string.IsNullOrWhiteSpace(state.Phase) ? BikePhaseChoosing : state.Phase,
                CreatorKey = state.CreatorKey,
                CreatorName = GetDisplayNameForKey(state.CreatorKey, state.CreatorName),
                Tricks = state.Tricks.ToList(),
                PlayerCompleted = state.PlayerCompleted != null
                    ? new Dictionary<string, bool>(state.PlayerCompleted, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
                PlayerLetters = state.PlayerLetters != null
                    ? new Dictionary<string, int>(state.PlayerLetters, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                PlayerAttemptStatus = state.PlayerAttemptStatus != null
                    ? new Dictionary<string, string>(state.PlayerAttemptStatus, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PlayerAttemptsRemaining = state.PlayerAttemptsRemaining != null
                    ? new Dictionary<string, int>(state.PlayerAttemptsRemaining, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                TurnOrder = state.TurnOrder != null ? state.TurnOrder.ToList() : new List<string>(),
                SetterIndex = state.SetterIndex,
                CurrentSetterKey = state.CurrentSetterKey,
                CurrentSetterName = state.CurrentSetterName,
                CurrentMatcherIndex = state.CurrentMatcherIndex,
                CurrentMatcherKey = state.CurrentMatcherKey,
                CurrentMatcherName = state.CurrentMatcherName,
                LastLetterGetsTwoTries = state.LastLetterGetsTwoTries,
                WinnerKey = state.WinnerKey,
                WinnerName = state.WinnerName,
                Position = state.Position,
                Rotation = state.Rotation,
                Size = state.Size
            };
            EnsureBikeStateCollections();

            if (isNewChallenge)
                OpenWindow();

            string localKey = GetLocalPlayerKey();
            if (!string.IsNullOrEmpty(localKey) &&
                (string.Equals(GetBikeAttemptStatus(localKey), BikeAttemptLanded, StringComparison.Ordinal) ||
                 string.Equals(GetBikeAttemptStatus(localKey), BikeAttemptSet, StringComparison.Ordinal)))
            {
                _pendingLocalCompletionChallengeId = null;
            }

            Log.Msg($"[MPChallenge][Net] Applying active challenge state from {source}: {DescribeActiveChallenge(_activeChallenge)}");
            RequestPlayerListRefresh(PlayerListRefreshDebounceInterval);
            SyncChallengeAreaFromActiveState();
        }

        private static void PublishActiveChallengeState()
        {
            if (_activeChallenge == null || !IsLocalCreator())
                return;

            if (!IsNetworkingEnabled())
            {
                _statePublishPending = false;
                _networkStatus = "Challenge networking disabled by config.";
                return;
            }

            EnsureBikeStateCollections();
            NetworkChallengeState state = new NetworkChallengeState
            {
                Version = ProtocolVersion,
                ChallengeId = _activeChallenge.ChallengeId,
                Revision = _activeChallenge.Revision,
                GameMode = _activeChallenge.GameMode,
                Phase = _activeChallenge.Phase,
                CreatorKey = _activeChallenge.CreatorKey,
                CreatorName = _activeChallenge.CreatorName,
                Tricks = _activeChallenge.Tricks.ToList(),
                PlayerCompleted = new Dictionary<string, bool>(_activeChallenge.PlayerCompleted, StringComparer.OrdinalIgnoreCase),
                PlayerLetters = new Dictionary<string, int>(_activeChallenge.PlayerLetters, StringComparer.OrdinalIgnoreCase),
                PlayerAttemptStatus = new Dictionary<string, string>(_activeChallenge.PlayerAttemptStatus, StringComparer.OrdinalIgnoreCase),
                PlayerAttemptsRemaining = new Dictionary<string, int>(_activeChallenge.PlayerAttemptsRemaining, StringComparer.OrdinalIgnoreCase),
                TurnOrder = _activeChallenge.TurnOrder.ToList(),
                SetterIndex = _activeChallenge.SetterIndex,
                CurrentSetterKey = _activeChallenge.CurrentSetterKey,
                CurrentSetterName = _activeChallenge.CurrentSetterName,
                CurrentMatcherIndex = _activeChallenge.CurrentMatcherIndex,
                CurrentMatcherKey = _activeChallenge.CurrentMatcherKey,
                CurrentMatcherName = _activeChallenge.CurrentMatcherName,
                LastLetterGetsTwoTries = _activeChallenge.LastLetterGetsTwoTries,
                WinnerKey = _activeChallenge.WinnerKey,
                WinnerName = _activeChallenge.WinnerName,
                Position = _activeChallenge.Position,
                Rotation = _activeChallenge.Rotation,
                Size = _activeChallenge.Size
            };

            string json = JsonConvert.SerializeObject(state, NetworkJsonSettings);
            if (json.Length > MaxStateJsonLength)
            {
                _networkStatus = "Challenge state is too large to publish.";
                Log.Warning($"[MPChallenge][Net] Refused oversized challenge state ({json.Length} bytes).");
                return;
            }

            Log.Msg($"[MPChallenge][Net] Publishing active challenge state: len={json.Length}, hash={json.GetHashCode()}, {DescribeActiveChallenge(_activeChallenge)}");
            _statePublishPending = !TryPublishNetworkState(json);
        }

        private static bool TryPublishNetworkState(string json)
        {
            if (!IsNetworkingEnabled())
                return false;

            if (_stateEvent == null || !CanUseNetwork())
                return false;

            try
            {
                _stateEvent.SetStringState(json);
                RecordNetworkSuccess("Challenge state published.");
                return true;
            }
            catch (Exception ex)
            {
                RecordNetworkFailure($"publish failed: {ex.Message}");
                return false;
            }
        }

        private static void SendLocalCompletionCommand()
        {
            if (_activeChallenge == null ||
                string.IsNullOrEmpty(_pendingLocalCompletionChallengeId) ||
                !string.Equals(_pendingLocalCompletionChallengeId, _activeChallenge.ChallengeId, StringComparison.Ordinal))
            {
                _pendingLocalCompletionChallengeId = null;
                return;
            }

            NetworkChallengeCommand command = new NetworkChallengeCommand
            {
                Version = ProtocolVersion,
                Type = BikeCommandLanded,
                ChallengeId = _activeChallenge.ChallengeId,
                PlayerKey = GetLocalPlayerKey(),
                PlayerName = GetLocalPlayerName()
            };

            TryRaiseCommand(command);
            _nextCompletionRetryTime = Time.unscaledTime + CompletionRetryInterval;
        }

        private static void BroadcastPresence()
        {
            _nextPresenceBroadcastTime = Time.unscaledTime + PresenceBroadcastInterval;
            if (!IsNetworkingEnabled())
            {
                _networkStatus = "Challenge networking disabled by config.";
                return;
            }

            if (_commandEvent == null || !CanUseNetwork())
                return;

            string localKey = GetLocalPlayerKey();
            if (!IsStableNetworkPlayerKey(localKey))
            {
                EnsureLocalPlayerCache(true);
                localKey = _cachedLocalPlayerKey;
                if (!IsStableNetworkPlayerKey(localKey))
                    return;
            }

            RegisterPresence(localKey, GetLocalPlayerName());
            TryRaiseCommand(new NetworkChallengeCommand
            {
                Version = ProtocolVersion,
                Type = "presence",
                PlayerKey = localKey,
                PlayerName = GetLocalPlayerName()
            });
        }

        private static bool TryRaiseCommand(NetworkChallengeCommand command)
        {
            if (!IsNetworkingEnabled())
                return false;

            if (_commandEvent == null || !CanUseNetwork())
                return false;

            try
            {
                string json = JsonConvert.SerializeObject(command, NetworkJsonSettings);
                if (json.Length > MaxCommandJsonLength)
                    return false;

                _commandEvent.RaiseString(json);
                RecordNetworkSuccess("Connected.");
                return true;
            }
            catch (Exception ex)
            {
                RecordNetworkFailure($"command send failed: {ex.Message}");
                return false;
            }
        }

        private static bool CanUseNetwork()
        {
            if (!IsNetworkingEnabled())
            {
                _networkStatus = "Challenge networking disabled by config.";
                return false;
            }

            if (Time.unscaledTime < _nextNetworkRetryTime)
                return false;

            try
            {
                INetworkedEventService service = NetworkedEventService.Service;
                bool available = service != null && service.IsAvailable;
                if (!available)
                {
                    _networkStatus = "Waiting for an active multiplayer session.";
                    _nextNetworkRetryTime = Time.unscaledTime + 1f;
                }
                return available;
            }
            catch (Exception ex)
            {
                RecordNetworkFailure($"availability check failed: {ex.Message}");
                return false;
            }
        }

        private static void RetryPendingNetworkOperations()
        {
            if (!IsNetworkingEnabled())
                return;

            if (Time.unscaledTime < _nextNetworkRetryTime)
                return;

            if (_clearStatePending)
            {
                TryClearNetworkState();
                return;
            }

            if (_statePublishPending && _activeChallenge != null && IsLocalCreator())
                PublishActiveChallengeState();
        }

        private static bool TryClearNetworkState()
        {
            if (!IsNetworkingEnabled())
                return false;

            if (_stateEvent == null || !CanUseNetwork())
                return false;

            try
            {
                _stateEvent.ClearState();
                _clearStatePending = false;
                RecordNetworkSuccess("Challenge cleared.");
                return true;
            }
            catch (Exception ex)
            {
                RecordNetworkFailure($"clear failed: {ex.Message}");
                return false;
            }
        }

        private static void ClearOwnedNetworkStateBestEffort()
        {
            if (!IsNetworkingEnabled())
                return;

            if (_activeChallenge == null || !IsLocalCreator())
                return;

            TryClearNetworkState();
        }

        private static void RecordNetworkSuccess(string status)
        {
            _networkFailureCount = 0;
            _nextNetworkRetryTime = 0f;
            _networkStatus = status;
        }

        private static void RecordNetworkFailure(string reason)
        {
            _networkFailureCount++;
            float delay = Mathf.Min(10f, Mathf.Pow(2f, Mathf.Min(_networkFailureCount - 1, 3)));
            _nextNetworkRetryTime = Time.unscaledTime + delay;
            _networkStatus = $"Retrying in {delay:0}s.";
            Log.Warning($"[MPChallenge][Net] {reason}");
        }

        private static void SyncChallengeAreaFromActiveState()
        {
            if (_activeChallenge == null)
                return;

            Vector3 position = ArrayToVector3(_activeChallenge.Position, Vector3.zero);
            Quaternion rotation = ArrayToQuaternion(_activeChallenge.Rotation, Quaternion.identity);
            Vector3 size = ArrayToVector3(_activeChallenge.Size,
                new Vector3(Config.challengeSettings.challengeSizeX, Config.challengeSettings.challengeSizeY, Config.challengeSettings.challengeSizeZ));
            string areaStateSignature = BuildAreaStateSignature(_activeChallenge);

            ChallengeArea active = ChallengeAreaManager.Active;
            if (active == null || !string.Equals(_activeAreaStateSignature, areaStateSignature, StringComparison.Ordinal))
            {
                ChallengeAreaManager.Create(position, size, rotation, "VehicleColliders");
                _activeAreaStateSignature = areaStateSignature;
                _wasLocalPlayerInsideArea = false;
                ResetCompletionDiagnostics();
                Log.Msg($"[MPChallenge][Area] Recreated synced challenge area: pos={FormatVector3(position)}, rot={FormatQuaternion(rotation)}, size={FormatVector3(size)}, signatureHash={areaStateSignature?.GetHashCode() ?? 0}.");
            }
            else
            {
                ChallengeAreaManager.SetPosition(position);
                ChallengeAreaManager.SetRotation(rotation);
                ChallengeAreaManager.SetSize(size);
                Log.Msg($"[MPChallenge][Area] Updated existing synced challenge area: pos={FormatVector3(position)}, rot={FormatQuaternion(rotation)}, size={FormatVector3(size)}.");
            }

            ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
            ChallengeAreaManager.SetCompleted(IsLocalPlayerComplete());
            Log.Msg($"[MPChallenge][Area] Synced challenge area flags: visible={Config.challengeSettings.challengeVisible}, completed={IsLocalPlayerComplete()}, activeExists={ChallengeAreaManager.Active != null}.");
        }

        private static bool ValidateNetworkState(NetworkChallengeState state, out string reason)
        {
            reason = null;
            if (state == null)
                return FailValidation("state is null", out reason);
            if (state.Version != ProtocolVersion)
                return FailValidation($"unsupported version {state.Version}", out reason);
            if (!IsBoundedText(state.ChallengeId) || !Guid.TryParseExact(state.ChallengeId, "N", out _))
                return FailValidation("invalid challenge id", out reason);
            if (state.Revision < 1)
                return FailValidation("invalid revision", out reason);
            if (!string.Equals(state.GameMode, GameModeBike, StringComparison.Ordinal))
                return FailValidation("invalid game mode", out reason);
            if (!IsValidBikePhase(state.Phase))
                return FailValidation("invalid game phase", out reason);
            if (!IsBoundedText(state.CreatorKey) || !IsBoundedText(state.CreatorName))
                return FailValidation("invalid creator identity", out reason);
            if (state.Tricks == null || state.Tricks.Count > MaxTricks)
                return FailValidation("invalid trick count", out reason);
            if (string.Equals(state.Phase, BikePhaseMatching, StringComparison.Ordinal) &&
                state.Tricks.Count == 0)
                return FailValidation("missing active line", out reason);
            if (state.Tricks.Any(trick => !IsBoundedText(trick)))
                return FailValidation("invalid trick name", out reason);
            if (state.PlayerCompleted == null || state.PlayerCompleted.Count > MaxPlayers)
                return FailValidation("invalid player completion map", out reason);
            if (state.PlayerCompleted.Keys.Any(key => !IsBoundedText(key)))
                return FailValidation("invalid player key", out reason);
            if (!IsValidBikeLettersMap(state.PlayerLetters))
                return FailValidation("invalid letter map", out reason);
            if (!IsValidBikeAttemptStatusMap(state.PlayerAttemptStatus))
                return FailValidation("invalid attempt status map", out reason);
            if (!IsValidBikeAttemptsMap(state.PlayerAttemptsRemaining))
                return FailValidation("invalid attempt count map", out reason);
            if (state.TurnOrder == null || state.TurnOrder.Count == 0 || state.TurnOrder.Count > MaxPlayers)
                return FailValidation("invalid turn order", out reason);
            if (state.TurnOrder.Any(key => !IsBoundedText(key)))
                return FailValidation("invalid turn order key", out reason);
            if (state.SetterIndex < 0 || state.SetterIndex >= state.TurnOrder.Count)
                return FailValidation("invalid setter index", out reason);
            if (!IsBoundedText(state.CurrentSetterKey) || !IsBoundedText(state.CurrentSetterName))
                return FailValidation("invalid setter identity", out reason);
            if (string.Equals(state.Phase, BikePhaseMatching, StringComparison.Ordinal) &&
                (!IsBoundedText(state.CurrentMatcherKey) || !IsBoundedText(state.CurrentMatcherName)))
                return FailValidation("invalid matcher identity", out reason);
            if (string.Equals(state.Phase, BikePhaseGameOver, StringComparison.Ordinal) &&
                (!string.IsNullOrEmpty(state.WinnerKey) && !IsBoundedText(state.WinnerKey)))
                return FailValidation("invalid winner key", out reason);
            if (!IsValidVector(state.Position, 3, -100000f, 100000f))
                return FailValidation("invalid position", out reason);
            if (!IsValidQuaternion(state.Rotation))
                return FailValidation("invalid rotation", out reason);
            if (!IsValidVector(state.Size, 3, 0.1f, 1000f))
                return FailValidation("invalid size", out reason);

            return true;
        }

        private static bool ValidateNetworkCommand(NetworkChallengeCommand command)
        {
            if (command == null ||
                command.Version != ProtocolVersion ||
                !IsBoundedText(command.PlayerKey) ||
                !IsBoundedText(command.PlayerName))
            {
                return false;
            }

            if (string.Equals(command.Type, "presence", StringComparison.Ordinal))
                return string.IsNullOrEmpty(command.ChallengeId);

            if (!Guid.TryParseExact(command.ChallengeId, "N", out _))
                return false;

            if (string.Equals(command.Type, BikeCommandLanded, StringComparison.Ordinal) ||
                string.Equals(command.Type, "complete", StringComparison.Ordinal) ||
                string.Equals(command.Type, BikeCommandMiss, StringComparison.Ordinal) ||
                string.Equals(command.Type, BikeCommandStartSet, StringComparison.Ordinal) ||
                string.Equals(command.Type, BikeCommandSetterFailed, StringComparison.Ordinal))
            {
                return command.Tricks == null || command.Tricks.Count == 0;
            }

            if (!string.Equals(command.Type, BikeCommandSetLine, StringComparison.Ordinal) ||
                command.Tricks == null ||
                command.Tricks.Count == 0 ||
                command.Tricks.Count > MaxTricks ||
                !command.Tricks.All(IsBoundedText))
            {
                return false;
            }

            bool includesArea =
                command.Position != null ||
                command.Rotation != null ||
                command.Size != null;
            return !includesArea ||
                   (IsValidVector(command.Position, 3, -100000f, 100000f) &&
                    IsValidQuaternion(command.Rotation) &&
                    IsValidVector(command.Size, 3, 0.1f, 1000f));
        }

        private static bool IsValidBikePhase(string phase)
        {
            return string.Equals(phase, BikePhaseChoosing, StringComparison.Ordinal) ||
                   string.Equals(phase, BikePhaseSetting, StringComparison.Ordinal) ||
                   string.Equals(phase, BikePhaseMatching, StringComparison.Ordinal) ||
                   string.Equals(phase, BikePhaseGameOver, StringComparison.Ordinal);
        }

        private static bool IsValidBikeLettersMap(Dictionary<string, int> values)
        {
            return values != null &&
                   values.Count <= MaxPlayers &&
                   values.All(entry => IsBoundedText(entry.Key) && entry.Value >= 0 && entry.Value <= MaxBikeLetters);
        }

        private static bool IsValidBikeAttemptsMap(Dictionary<string, int> values)
        {
            return values != null &&
                   values.Count <= MaxPlayers &&
                   values.All(entry => IsBoundedText(entry.Key) && entry.Value >= 0 && entry.Value <= 2);
        }

        private static bool IsValidBikeAttemptStatusMap(Dictionary<string, string> values)
        {
            return values != null &&
                   values.Count <= MaxPlayers &&
                   values.All(entry =>
                       IsBoundedText(entry.Key) &&
                       (string.IsNullOrEmpty(entry.Value) ||
                        string.Equals(entry.Value, BikeAttemptPending, StringComparison.Ordinal) ||
                        string.Equals(entry.Value, BikeAttemptLanded, StringComparison.Ordinal) ||
                        string.Equals(entry.Value, BikeAttemptMissed, StringComparison.Ordinal) ||
                        string.Equals(entry.Value, BikeAttemptOut, StringComparison.Ordinal) ||
                        string.Equals(entry.Value, BikeAttemptSet, StringComparison.Ordinal)));
        }

        private static bool FailValidation(string message, out string reason)
        {
            reason = message;
            return false;
        }

        private static bool IsBoundedText(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= MaxTextLength;
        }

        private static bool IsStableNetworkPlayerKey(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   (value.StartsWith("fusion:", StringComparison.Ordinal) ||
                    value.StartsWith("token:", StringComparison.Ordinal));
        }

        private static bool IsUsableBikePlayerKey(string value)
        {
            return IsStableNetworkPlayerKey(value) ||
                   (!string.IsNullOrEmpty(value) && value.StartsWith("local:", StringComparison.Ordinal));
        }

        private static bool IsValidVector(float[] values, int requiredLength, float minimum, float maximum)
        {
            if (values == null || values.Length != requiredLength)
                return false;

            return values.All(value => IsFinite(value) && value >= minimum && value <= maximum);
        }

        private static bool IsValidQuaternion(float[] values)
        {
            if (values == null || values.Length != 4 || values.Any(value => !IsFinite(value)))
                return false;

            float magnitudeSquared = values.Sum(value => value * value);
            return magnitudeSquared > 0.5f && magnitudeSquared < 1.5f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static void RegisterPresence(string playerKey, string playerName)
        {
            if (!IsNetworkingEnabled())
                return;

            if (!IsBoundedText(playerKey) || !IsBoundedText(playerName))
                return;

            bool isNew = !_presenceByKey.ContainsKey(playerKey);
            bool nameChanged = !isNew &&
                               _presenceByKey.TryGetValue(playerKey, out PresenceRecord previous) &&
                               !string.Equals(previous.DisplayName, playerName, StringComparison.Ordinal);
            _presenceByKey[playerKey] = new PresenceRecord
            {
                DisplayName = playerName,
                LastSeenTime = Time.unscaledTime
            };

            if (isNew || nameChanged)
            {
                RequestPlayerListRefresh(PlayerListRefreshDebounceInterval);
                TryAutoOpenForRemotePresence(playerKey, playerName);
                Log.Msg($"[MPChallenge][Players] Presence discovered: {playerName} ({playerKey}).");
            }
        }

        private static void TryAutoOpenForRemotePresence(string playerKey, string playerName)
        {
            if (!IsAutoOpenEnabled())
                return;

            if (_autoOpenTriggeredForSession || !IsStableNetworkPlayerKey(playerKey))
                return;

            string localKey = GetLocalPlayerKey();
            if (!IsStableNetworkPlayerKey(localKey))
            {
                EnsureLocalPlayerCache(true);
                localKey = _cachedLocalPlayerKey;
            }

            if (!string.IsNullOrEmpty(localKey) &&
                string.Equals(playerKey, localKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _autoOpenTriggeredForSession = true;
            OpenWindow();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Log.Msg(
                $"[MPChallenge][UI] Auto-opened challenge UI after remote RoweMod presence from " +
                $"'{playerName}' ({playerKey}).");
        }

        private static bool ExpirePresenceRecords()
        {
            float cutoff = Time.unscaledTime - PresenceExpirySeconds;
            string localKey = GetLocalPlayerKey();
            string[] expiredKeys = _presenceByKey
                .Where(entry =>
                    entry.Value.LastSeenTime < cutoff &&
                    !string.Equals(entry.Key, localKey, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key)
                .ToArray();

            foreach (string key in expiredKeys)
                _presenceByKey.Remove(key);

            return expiredKeys.Length > 0;
        }

        private static void ResetActiveChallenge()
        {
            _activeChallenge = null;
            _activeAreaStateSignature = null;
            _pendingLocalCompletionChallengeId = null;
            _statePublishPending = false;
            _wasLocalPlayerInsideArea = false;
            ResetCompletionDiagnostics();
            ChallengeAreaManager.DestroyActive();
        }

        private static void ResetLocalSessionState()
        {
            ResetActiveChallenge();
            _presenceByKey.Clear();
            _rowePlayers.Clear();
            _cachedLocalPlayerKey = null;
            _cachedLocalPlayerName = null;
            _cachedLocalPlayerObjectId = 0;
            _nextLocalPlayerCacheRefreshTime = 0f;
            _nextPlayerListRefreshTime = 0f;
            _nextPlayerListFallbackRefreshTime = 0f;
            _nextPresenceExpiryCheckTime = 0f;
            _playerListRefreshRequested = true;
            _lastPlayerListSignature = null;
            _nextIdleUpdateTime = 0f;
            _nextPresenceBroadcastTime = 0f;
            _clearStatePending = false;
            _networkFailureCount = 0;
            _nextNetworkRetryTime = 0f;
            _networkStatus = "Waiting for multiplayer networking.";
            _trickDetection = null;
            _playerScanDisabled = false;
            _trickHistoryDisabled = false;
            _nextPlayerScanRetryTime = 0f;
            _nextTrickHistoryRetryTime = 0f;
            _nextUpdateErrorLogTime = 0f;
            _nextActivePlayerLookupWarningTime = 0f;
            _autoOpenTriggeredForSession = false;
        }

        private static class TrickDetectionHistoryPatch
        {
            public static void Postfix(TrickDetection __instance, string __0)
            {
                if (!AreTrickHooksEnabled())
                    return;

                try
                {
                    CaptureLocalTrick(__instance, __0);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MPChallenge][Tricks] Direct capture failed: {ex.Message}");
                }
            }
        }

        private static class NetworkPlayerSpawnedPatch
        {
            public static void Postfix(NetworkPlayer __instance)
            {
                HandleNetworkPlayerLifecycleChanged(__instance, "Spawned");
            }
        }

        private static class NetworkPlayerDespawnedPatch
        {
            public static void Postfix(NetworkPlayer __instance)
            {
                HandleNetworkPlayerLifecycleChanged(__instance, "Despawned");
            }
        }

        private static string BuildAreaStateSignature(ActiveChallenge challenge)
        {
            if (challenge == null)
                return null;

            return string.Join("|",
                challenge.ChallengeId ?? string.Empty,
                FloatArraySignature(challenge.Position),
                FloatArraySignature(challenge.Rotation),
                FloatArraySignature(challenge.Size));
        }

        private static string FloatArraySignature(float[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;

            return string.Join(",", values.Select(value => value.ToString("R")));
        }

        private static string DescribeActiveChallenge(ActiveChallenge challenge)
        {
            if (challenge == null)
                return "null challenge";

            string tricks = challenge.Tricks != null ? string.Join(" + ", challenge.Tricks) : "none";
            int playerCount = challenge.TurnOrder != null ? challenge.TurnOrder.Count : 0;
            return $"id='{challenge.ChallengeId ?? "null"}', revision={challenge.Revision}, phase='{challenge.Phase ?? "null"}', setter='{challenge.CurrentSetterKey ?? "null"}', matcher='{challenge.CurrentMatcherKey ?? "null"}', tricks='{tricks}', players={playerCount}, pos={FormatArray3(challenge.Position)}, rot={FormatArray4(challenge.Rotation)}, size={FormatArray3(challenge.Size)}";
        }

        private static string DescribeArea(ChallengeArea area)
        {
            if (area == null)
                return "null area";

            return $"id={area.GetInstanceID()}, go='{area.gameObject.name}', pos={FormatVector3(area.transform.position)}, rot={FormatQuaternion(area.transform.rotation)}, size={FormatVector3(area.transform.localScale)}, active={area.gameObject.activeSelf}";
        }

        private static string FormatArray3(float[] values)
        {
            if (values == null || values.Length < 3)
                return "(null)";

            return $"({values[0]:0.###}, {values[1]:0.###}, {values[2]:0.###})";
        }

        private static string FormatArray4(float[] values)
        {
            if (values == null || values.Length < 4)
                return "(null)";

            return $"({values[0]:0.###}, {values[1]:0.###}, {values[2]:0.###}, {values[3]:0.###})";
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string FormatQuaternion(Quaternion value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###}, {value.w:0.###})";
        }

        private static string TrimForLog(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Length <= 500 ? text : text.Substring(0, 500) + "...";
        }

        private static bool IsLocalPlayerComplete()
        {
            if (_activeChallenge == null)
                return false;

            string localKey = GetLocalPlayerKey();
            if (string.IsNullOrEmpty(localKey))
                return false;

            string status = GetBikeAttemptStatus(localKey);
            return string.Equals(status, BikeAttemptLanded, StringComparison.Ordinal) ||
                   string.Equals(status, BikeAttemptSet, StringComparison.Ordinal) ||
                   string.Equals(localKey, _activeChallenge.WinnerKey, StringComparison.OrdinalIgnoreCase);
        }

        private static float[] Vector3ToArray(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private static float[] QuaternionToArray(Quaternion value)
        {
            return new[] { value.x, value.y, value.z, value.w };
        }

        private static Vector3 ArrayToVector3(float[] values, Vector3 fallback)
        {
            if (values == null || values.Length < 3)
                return fallback;

            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion ArrayToQuaternion(float[] values, Quaternion fallback)
        {
            if (values == null || values.Length < 4)
                return fallback;

            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

        private static void ClearActiveChallenge()
        {
            if (!IsLocalCreator())
                return;

            Log.Msg("[MPChallenge] ClearActiveChallenge requested by local creator.");
            _clearStatePending = !TryClearNetworkState();
            ResetActiveChallenge();
        }

        private static void AddLocalFallbackPlayerIfNeeded()
        {
            if (Memory.physicsDrivenCharacter == null)
                return;

            Transform marker = Memory.physicsDrivenCharacter.transform.Find(RoweModMarkerName);
            if (marker == null)
                return;

            string key = GetLocalPlayerKey();
            if (string.IsNullOrEmpty(key) || _rowePlayers.Any(player => string.Equals(player.Key, key, StringComparison.OrdinalIgnoreCase)))
                return;

            _rowePlayers.Add(new RowePlayer
            {
                Key = key,
                DisplayName = GetLocalPlayerName(),
                NetworkPlayer = null
            });
        }

        private static bool IsLocalCreator()
        {
            return _activeChallenge != null &&
                   string.Equals(_activeChallenge.CreatorKey, GetLocalPlayerKey(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLocalPlayerKey()
        {
            EnsureLocalPlayerCache(false);
            return _cachedLocalPlayerKey;
        }

        private static string GetLocalPlayerName()
        {
            EnsureLocalPlayerCache(false);
            return !string.IsNullOrWhiteSpace(_cachedLocalPlayerName) ? _cachedLocalPlayerName : "Local Player";
        }

        private static void RequestPlayerListRefresh(float delaySeconds = 0f)
        {
            if (!IsNetworkingEnabled())
            {
                _playerListRefreshRequested = false;
                return;
            }

            bool wasRequested = _playerListRefreshRequested;
            float requestedTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
            _playerListRefreshRequested = true;

            if (!wasRequested || _nextPlayerListRefreshTime <= 0f || requestedTime < _nextPlayerListRefreshTime)
                _nextPlayerListRefreshTime = requestedTime;
        }

        private static string BuildPlayerListSignature()
        {
            return string.Join("|", _rowePlayers
                .Select(player => $"{player.Key}:{player.DisplayName}")
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static void EnsureLocalPlayerCache(bool forceRefresh)
        {
            int currentObjectId = Memory.physicsDrivenCharacter != null ? Memory.physicsDrivenCharacter.GetInstanceID() : 0;
            bool cacheMatchesCharacter = currentObjectId == 0 ||
                                         _cachedLocalPlayerObjectId == 0 ||
                                         _cachedLocalPlayerObjectId == currentObjectId;

            if (!forceRefresh &&
                cacheMatchesCharacter &&
                !string.IsNullOrEmpty(_cachedLocalPlayerKey) &&
                Time.unscaledTime < _nextLocalPlayerCacheRefreshTime)
            {
                return;
            }

            _nextLocalPlayerCacheRefreshTime = Time.unscaledTime + LocalPlayerCacheRefreshInterval;

            if (!IsNetworkingEnabled())
            {
                if (Memory.physicsDrivenCharacter != null)
                {
                    CacheLocalPlayer($"local:{Memory.physicsDrivenCharacter.GetInstanceID()}", "Local Player");
                    return;
                }

                _cachedLocalPlayerKey = null;
                _cachedLocalPlayerName = null;
                _cachedLocalPlayerObjectId = 0;
                return;
            }

            List<NetworkPlayer> players = GetKnownNetworkPlayers(true);
            foreach (NetworkPlayer player in players)
            {
                if (player == null)
                    continue;

                if (!TryGetBoolMember(player, "_isLocal", out bool isLocal) || !isLocal)
                    continue;

                string name = ResolvePlayerDisplayName(player);
                string key = ResolvePlayerKey(player, name);
                CacheLocalPlayer(key, name);
                return;
            }

            if (Memory.physicsDrivenCharacter != null)
            {
                CacheLocalPlayer($"local:{Memory.physicsDrivenCharacter.GetInstanceID()}", "Local Player");
                return;
            }

            _cachedLocalPlayerKey = null;
            _cachedLocalPlayerName = null;
            _cachedLocalPlayerObjectId = 0;
        }

        private static void CacheLocalPlayer(string key, string name)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _cachedLocalPlayerKey = key;
            _cachedLocalPlayerName = !string.IsNullOrWhiteSpace(name) ? name : "Local Player";
            _cachedLocalPlayerObjectId = Memory.physicsDrivenCharacter != null ? Memory.physicsDrivenCharacter.GetInstanceID() : 0;
        }

        private static string ResolvePlayerKey(NetworkPlayer player, string displayName)
        {
            try
            {
                if (player.PlayerReference.IsRealPlayer)
                    return $"fusion:{player.PlayerReference.RawEncoded}";
            }
            catch
            {
                // Fall back to older game builds that do not expose PlayerReference cleanly.
            }

            try
            {
                if (player.Token != 0)
                    return $"token:{player.Token}";
            }
            catch
            {
                // Continue through compatibility fallbacks.
            }

            string playerId = TryGetStringMember(player, "PlayerId") ??
                              TryGetStringMember(player, "playerId") ??
                              TryGetStringMember(player, "Network_playerId") ??
                              TryGetStringMember(player, "_playerId");

            if (!string.IsNullOrEmpty(playerId))
                return playerId;

            return !string.IsNullOrEmpty(displayName)
                ? displayName
                : $"player:{player.GetInstanceID()}";
        }

        private static string ResolvePlayerDisplayName(NetworkPlayer player)
        {
            string name = null;
            try
            {
                name = player.GetUsername();
            }
            catch
            {
                // Continue through compatibility fallbacks.
            }

            name = name ??
                          TryGetStringMember(player, "DisplayName") ??
                          TryGetStringMember(player, "PlayerName") ??
                          TryGetStringMember(player, "UserName") ??
                          TryGetStringMember(player, "Username") ??
                          TryGetStringMember(player, "NickName") ??
                          TryGetStringMember(player, "Nickname") ??
                          TryGetStringMember(player, "Name") ??
                          TryGetStringMember(player, "_displayName") ??
                          TryGetStringMember(player, "_playerName") ??
                          TryGetStringMember(player, "_userName") ??
                          TryGetStringMember(player, "_username") ??
                          TryGetStringMember(player, "_nickName") ??
                          TryGetStringMember(player, "_nickname");

            if (!string.IsNullOrWhiteSpace(name))
                return SanitizePlayerDisplayName(name, player);

            return SanitizePlayerDisplayName(player.gameObject != null ? player.gameObject.name : null, player);
        }

        private static bool TryGetBoolMember(object target, string memberName, out bool value)
        {
            value = false;
            object raw = TryGetMemberValue(target, memberName);
            if (raw is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            return raw != null && bool.TryParse(raw.ToString(), out value);
        }

        private static string TryGetStringMember(object target, string memberName)
        {
            object value = TryGetMemberValue(target, memberName);
            return ExtractReadableString(value);
        }

        private static string ExtractReadableString(object value)
        {
            if (value == null)
                return null;

            if (value is string directString)
                return IsUsableDisplayText(directString) && !IsTypeLikeDisplayText(directString) ? directString.Trim() : null;

            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            string[] memberNames =
            {
                "Value",
                "String",
                "Text",
                "Name",
                "_value",
                "_string",
                "_text",
                "_name",
                "m_Value",
                "m_String",
                "m_Text"
            };

            Type type = value.GetType();
            foreach (string memberName in memberNames)
            {
                System.Reflection.PropertyInfo property = type.GetProperty(memberName, Flags);
                if (property != null && property.CanRead)
                {
                    try
                    {
                        string nested = ExtractReadableString(property.GetValue(value, null));
                        if (!string.IsNullOrWhiteSpace(nested))
                            return nested;
                    }
                    catch
                    {
                        // Ignore unavailable IL2CPP members.
                    }
                }

                System.Reflection.FieldInfo field = type.GetField(memberName, Flags);
                if (field != null)
                {
                    try
                    {
                        string nested = ExtractReadableString(field.GetValue(value));
                        if (!string.IsNullOrWhiteSpace(nested))
                            return nested;
                    }
                    catch
                    {
                        // Ignore unavailable IL2CPP members.
                    }
                }
            }

            string text = value.ToString();
            return IsUsableDisplayText(text) && !IsTypeLikeDisplayText(text) ? text.Trim() : null;
        }

        private static string GetSafePlayerDisplayName(RowePlayer player)
        {
            if (player == null)
                return "Unknown Player";

            return SanitizePlayerDisplayName(player.DisplayName, player.NetworkPlayer);
        }

        private static string GetDisplayNameForKey(string playerKey, string displayName)
        {
            if (!IsUsableDisplayText(displayName) || IsTypeLikeDisplayText(displayName))
            {
                if (!string.IsNullOrEmpty(playerKey) &&
                    string.Equals(playerKey, GetLocalPlayerKey(), StringComparison.OrdinalIgnoreCase))
                {
                    return GetSteamPersonaName() ?? "Local Player";
                }

                RowePlayer knownPlayer = _rowePlayers.FirstOrDefault(player =>
                    string.Equals(player.Key, playerKey, StringComparison.OrdinalIgnoreCase));
                if (knownPlayer != null)
                    return GetSafePlayerDisplayName(knownPlayer);

                if (_rowePlayers.Count == 1)
                    return GetSafePlayerDisplayName(_rowePlayers[0]);
            }

            return SanitizePlayerDisplayName(displayName, null);
        }

        private static string SanitizePlayerDisplayName(string displayName, NetworkPlayer player)
        {
            if (IsUsableDisplayText(displayName) && !IsTypeLikeDisplayText(displayName))
                return displayName.Trim();

            if (player != null && IsLocalNetworkPlayer(player))
            {
                string steamName = GetSteamPersonaName();
                if (!string.IsNullOrWhiteSpace(steamName))
                    return steamName;
            }

            string gameObjectName = player != null && player.gameObject != null ? player.gameObject.name : null;
            if (IsUsableDisplayText(gameObjectName) && !IsTypeLikeDisplayText(gameObjectName))
                return gameObjectName.Trim();

            return player != null ? $"Player {Math.Abs(player.GetInstanceID())}" : "Unknown Player";
        }

        private static bool IsLocalNetworkPlayer(NetworkPlayer player)
        {
            return player != null && TryGetBoolMember(player, "_isLocal", out bool isLocal) && isLocal;
        }

        private static string GetSteamPersonaName()
        {
            try
            {
                string personaName = Il2CppSteamworks.SteamFriends.GetPersonaName();
                return IsUsableDisplayText(personaName) && !IsTypeLikeDisplayText(personaName)
                    ? personaName.Trim()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsUsableDisplayText(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   !string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(text, "(null)", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTypeLikeDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();
            return trimmed.StartsWith("Il2Cpp", StringComparison.Ordinal) ||
                   trimmed.StartsWith("System.", StringComparison.Ordinal) ||
                   trimmed.Contains("NetworkString`") ||
                   trimmed.Contains("`1[") ||
                   trimmed.Contains("[Il2Cpp");
        }

        private static object TryGetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            Type type = target.GetType();
            System.Reflection.PropertyInfo property = type.GetProperty(memberName, Flags);
            if (property != null && property.CanRead)
            {
                try
                {
                    return property.GetValue(target, null);
                }
                catch
                {
                    // Ignore and try field.
                }
            }

            System.Reflection.FieldInfo field = type.GetField(memberName, Flags);
            if (field != null)
            {
                try
                {
                    return field.GetValue(target);
                }
                catch
                {
                    // Ignore unavailable IL2CPP members.
                }
            }

            return null;
        }
    }
}
