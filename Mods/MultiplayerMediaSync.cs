using System;
using Il2CppInterop.Runtime;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBoxSDK.Maps.Rigging;
using Il2CppMashBoxSDK.Services;
using Newtonsoft.Json;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace rowemod.Mods
{
    /// <summary>
    /// Shares a small station description through the game's Fusion-backed event service.
    /// Media bytes are never sent over gameplay networking; each opted-in RoweMod client
    /// validates and prepares the same YouTube URL in its own bounded cache.
    /// </summary>
    internal static class MultiplayerMediaSync
    {
        private sealed class StationState
        {
            public int Version;
            public string PlaybackId;
            public string SenderSessionId;
            public bool Playing;
            public bool IncludeTv;
            public string Title;
            public string Url;
            public string VideoId;
            public float Duration;
            public long StartedUtcMs;
            public long UpdatedUtcMs;
            public float RadioX;
            public float RadioY;
            public float RadioZ;
            public float RadioQx;
            public float RadioQy;
            public float RadioQz;
            public float RadioQw;
            public float TvX;
            public float TvY;
            public float TvZ;
            public float TvQx;
            public float TvQy;
            public float TvQz;
            public float TvQw;
        }

        private const int ProtocolVersion = 1;
        private const string StateEventKey = "rowemod.media.station.state.v1";
        private const int MaxStateJsonLength = 8192;
        private const float RetrySeconds = 2f;
        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            MaxDepth = 8,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None
        };

        private static GameObject _bridgeRoot;
        private static MBNetworkedEvent _stateEvent;
        private static UnityAction<string> _stateListener;
        private static UnityAction _stateClearedListener;
        private static string _pendingPublishJson;
        private static float _nextRetryTime;
        private static string _activeLocalPlaybackId;
        private static string _activeRemotePlaybackId;
        private static string _status = "Multiplayer media sync is off.";

        public static bool Enabled => Config.replaySettings.multiplayerMediaEnabled;
        public static string Status => _status;

        public static void Update()
        {
            if (!Enabled)
                return;
            if (Time.unscaledTime < _nextRetryTime)
                return;

            EnsureBridge();
            if (_stateEvent != null && !string.IsNullOrEmpty(_pendingPublishJson) && CanUseNetwork())
            {
                string json = _pendingPublishJson;
                try
                {
                    _stateEvent.SetStringState(json);
                    _pendingPublishJson = null;
                    _status = "Shared station state published.";
                }
                catch (Exception ex)
                {
                    RecordFailure("publish retry", ex);
                }
            }
        }

        public static void OnSettingChanged()
        {
            if (Enabled)
            {
                _status = "Waiting for an active multiplayer session.";
                _nextRetryTime = 0f;
                EnsureBridge();
            }
            else
            {
                PublishStoppedInternal(true);
                ShutdownBridge();
                _pendingPublishJson = null;
                _activeLocalPlaybackId = null;
                if (!string.IsNullOrEmpty(_activeRemotePlaybackId))
                    RadioDanceController.StopSynchronizedFromNetwork(_activeRemotePlaybackId);
                _activeRemotePlaybackId = null;
                _status = "Multiplayer media sync is off.";
            }
        }

        public static void OnSceneInitialized()
        {
            ShutdownBridge();
            _pendingPublishJson = null;
            _activeLocalPlaybackId = null;
            _activeRemotePlaybackId = null;
            _nextRetryTime = 0f;
            _status = Enabled
                ? "Waiting for an active multiplayer session."
                : "Multiplayer media sync is off.";
        }

        public static void Shutdown()
        {
            PublishStoppedInternal(true);
            ShutdownBridge();
            _pendingPublishJson = null;
            _activeLocalPlaybackId = null;
            _activeRemotePlaybackId = null;
        }

        public static void PublishStarted(
            string title,
            string url,
            string videoId,
            float duration,
            bool includeTv,
            Vector3 radioPosition,
            Quaternion radioRotation,
            Vector3 tvPosition,
            Quaternion tvRotation)
        {
            if (!Enabled)
                return;

            string playbackId = Guid.NewGuid().ToString("N");
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var state = new StationState
            {
                Version = ProtocolVersion,
                PlaybackId = playbackId,
                SenderSessionId = SessionId,
                Playing = true,
                IncludeTv = includeTv,
                Title = title ?? string.Empty,
                Url = url ?? string.Empty,
                VideoId = videoId ?? string.Empty,
                Duration = duration,
                StartedUtcMs = now,
                UpdatedUtcMs = now,
                RadioX = radioPosition.x,
                RadioY = radioPosition.y,
                RadioZ = radioPosition.z,
                RadioQx = radioRotation.x,
                RadioQy = radioRotation.y,
                RadioQz = radioRotation.z,
                RadioQw = radioRotation.w,
                TvX = tvPosition.x,
                TvY = tvPosition.y,
                TvZ = tvPosition.z,
                TvQx = tvRotation.x,
                TvQy = tvRotation.y,
                TvQz = tvRotation.z,
                TvQw = tvRotation.w
            };
            _activeLocalPlaybackId = playbackId;
            _activeRemotePlaybackId = null;
            Publish(state);
        }

        public static void PublishStopped()
        {
            PublishStoppedInternal(false);
        }

        private static void PublishStoppedInternal(bool bestEffortWhenDisabled)
        {
            if (string.IsNullOrEmpty(_activeLocalPlaybackId) || (!Enabled && !bestEffortWhenDisabled))
                return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var state = new StationState
            {
                Version = ProtocolVersion,
                PlaybackId = _activeLocalPlaybackId,
                SenderSessionId = SessionId,
                Playing = false,
                UpdatedUtcMs = now
            };
            if (bestEffortWhenDisabled)
            {
                try
                {
                    if (_stateEvent != null)
                    {
                        string json = JsonConvert.SerializeObject(state, JsonSettings);
                        if (json.Length <= MaxStateJsonLength)
                            _stateEvent.SetStringState(json);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MediaSync] Best-effort stop publish failed: {ex.Message}");
                }
            }
            else
            {
                Publish(state);
            }
            _activeLocalPlaybackId = null;
        }

        private static void Publish(StationState state)
        {
            string json = JsonConvert.SerializeObject(state, JsonSettings);
            if (json.Length > MaxStateJsonLength)
            {
                _status = "Station state was too large to share.";
                Log.Warning($"[MediaSync] Refused oversized state ({json.Length} bytes).");
                return;
            }

            _pendingPublishJson = json;
            _nextRetryTime = 0f;
            Update();
        }

        private static void EnsureBridge()
        {
            if (_bridgeRoot != null || !CanUseNetwork())
                return;

            string stage = "create bridge";
            try
            {
                _bridgeRoot = new GameObject("RoweMod_Media_NetworkBridge");
                _bridgeRoot.hideFlags = HideFlags.HideAndDontSave;
                _bridgeRoot.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_bridgeRoot);

                stage = "add state event";
                _stateEvent = _bridgeRoot
                    .AddComponent(Il2CppType.Of<MBNetworkedEvent>())
                    .Cast<MBNetworkedEvent>();
                _stateEvent.eventKey = StateEventKey;
                _stateEvent.applyStoredStateOnEnable = true;
                EnsureCallbacks(_stateEvent);

                stage = "register listeners";
                _stateListener = DelegateSupport.ConvertDelegate<UnityAction<string>>(HandleStateJson);
                _stateClearedListener = DelegateSupport.ConvertDelegate<UnityAction>(HandleStateCleared);
                if (_stateListener == null || _stateClearedListener == null)
                    throw new InvalidOperationException("network listener conversion returned null");
                _stateEvent.onString.AddListener(_stateListener);
                _stateEvent.onStateCleared.AddListener(_stateClearedListener);

                stage = "activate bridge";
                _bridgeRoot.SetActive(true);
                _status = "Connected. Shared station sync is ready.";
                Log.Msg("[MediaSync] Registered Fusion stored-state listener.");
            }
            catch (Exception ex)
            {
                RecordFailure(stage, ex);
                ShutdownBridge();
            }
        }

        private static void EnsureCallbacks(MBNetworkedEvent networkEvent)
        {
            if (networkEvent.onReceived == null) networkEvent.onReceived = new UnityEvent();
            if (networkEvent.onRaised == null) networkEvent.onRaised = new UnityEvent();
            if (networkEvent.onStateApplied == null) networkEvent.onStateApplied = new UnityEvent();
            if (networkEvent.onStateCleared == null) networkEvent.onStateCleared = new UnityEvent();
            if (networkEvent.onBool == null) networkEvent.onBool = new MBBoolEvent();
            if (networkEvent.onInt == null) networkEvent.onInt = new MBIntEvent();
            if (networkEvent.onFloat == null) networkEvent.onFloat = new MBFloatEvent();
            if (networkEvent.onString == null) networkEvent.onString = new MBStringEvent();
        }

        private static void HandleStateJson(string json)
        {
            if (!Enabled)
                return;
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > MaxStateJsonLength)
                    throw new InvalidOperationException("empty or oversized state");
                StationState state = JsonConvert.DeserializeObject<StationState>(json, JsonSettings);
                if (!Validate(state, out string reason))
                    throw new InvalidOperationException(reason);
                if (string.Equals(state.SenderSessionId, SessionId, StringComparison.Ordinal))
                    return;

                if (!state.Playing)
                {
                    RadioDanceController.StopSynchronizedFromNetwork(state.PlaybackId);
                    if (string.Equals(_activeRemotePlaybackId, state.PlaybackId, StringComparison.Ordinal))
                        _activeRemotePlaybackId = null;
                    _status = "The shared station was stopped.";
                    return;
                }

                double elapsed = Math.Max(0d,
                    (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - state.StartedUtcMs) / 1000d);
                if (state.Duration > 0f && elapsed >= state.Duration)
                {
                    _status = "Ignored an expired shared station track.";
                    return;
                }

                _activeRemotePlaybackId = state.PlaybackId;
                _activeLocalPlaybackId = null;
                RadioDanceController.StartSynchronizedFromNetwork(
                    state.Url,
                    state.Title,
                    state.VideoId,
                    state.Duration,
                    state.IncludeTv,
                    state.StartedUtcMs,
                    new Vector3(state.RadioX, state.RadioY, state.RadioZ),
                    NormalizeRotation(state.RadioQx, state.RadioQy, state.RadioQz, state.RadioQw),
                    new Vector3(state.TvX, state.TvY, state.TvZ),
                    NormalizeRotation(state.TvQx, state.TvQy, state.TvQz, state.TvQw),
                    state.PlaybackId);
                _status = $"Preparing shared station: {CleanTitle(state.Title)}";
            }
            catch (Exception ex)
            {
                _status = "Ignored invalid multiplayer station data.";
                Log.Warning($"[MediaSync] Ignored state: {ex.Message}");
            }
        }

        private static void HandleStateCleared()
        {
            if (!string.IsNullOrEmpty(_activeRemotePlaybackId))
                RadioDanceController.StopSynchronizedFromNetwork(_activeRemotePlaybackId);
            _activeRemotePlaybackId = null;
            _status = "No shared station is active.";
        }

        private static bool Validate(StationState state, out string reason)
        {
            reason = null;
            if (state == null || state.Version != ProtocolVersion)
            {
                reason = "unsupported protocol";
                return false;
            }
            if (!IsToken(state.PlaybackId, 64) || !IsToken(state.SenderSessionId, 64))
            {
                reason = "invalid playback identity";
                return false;
            }
            if (!state.Playing)
                return true;
            if (!RadioDanceController.IsAllowedYouTubeUrl(state.Url))
            {
                reason = "URL is not an allowed YouTube link";
                return false;
            }
            if (!IsToken(state.VideoId, 64) || state.Duration <= 0f || state.Duration > 3600f ||
                string.IsNullOrWhiteSpace(state.Title) || state.Title.Length > 300)
            {
                reason = "invalid media metadata";
                return false;
            }
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (state.StartedUtcMs > now + 30000L || state.StartedUtcMs < now - 3900000L)
            {
                reason = "invalid playback timestamp";
                return false;
            }
            if (!IsFinitePosition(state.RadioX, state.RadioY, state.RadioZ) ||
                !IsFiniteRotation(state.RadioQx, state.RadioQy, state.RadioQz, state.RadioQw) ||
                (state.IncludeTv && (!IsFinitePosition(state.TvX, state.TvY, state.TvZ) ||
                                     !IsFiniteRotation(state.TvQx, state.TvQy, state.TvQz, state.TvQw))))
            {
                reason = "invalid world transform";
                return false;
            }
            return true;
        }

        private static bool IsToken(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }
            return true;
        }

        private static bool IsFinitePosition(float x, float y, float z)
        {
            return IsFiniteBounded(x, 100000f) && IsFiniteBounded(y, 100000f) && IsFiniteBounded(z, 100000f);
        }

        private static bool IsFiniteRotation(float x, float y, float z, float w)
        {
            if (!IsFiniteBounded(x, 2f) || !IsFiniteBounded(y, 2f) ||
                !IsFiniteBounded(z, 2f) || !IsFiniteBounded(w, 2f))
                return false;
            float magnitude = x * x + y * y + z * z + w * w;
            return magnitude > 0.25f && magnitude < 4f;
        }

        private static bool IsFiniteBounded(float value, float bound)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= bound;
        }

        private static Quaternion NormalizeRotation(float x, float y, float z, float w)
        {
            Quaternion rotation = new Quaternion(x, y, z, w);
            float magnitude = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            return magnitude > 0.0001f
                ? new Quaternion(x / magnitude, y / magnitude, z / magnitude, w / magnitude)
                : Quaternion.identity;
        }

        private static bool CanUseNetwork()
        {
            try
            {
                INetworkedEventService service = NetworkedEventService.Service;
                bool available = service != null && service.IsAvailable;
                if (!available)
                {
                    _status = "Waiting for an active multiplayer session.";
                    _nextRetryTime = Time.unscaledTime + RetrySeconds;
                }
                return available;
            }
            catch (Exception ex)
            {
                RecordFailure("availability check", ex);
                return false;
            }
        }

        private static void ShutdownBridge()
        {
            GameObject root = _bridgeRoot;
            try
            {
                if (_stateEvent != null)
                {
                    if (_stateListener != null && _stateEvent.onString != null)
                        _stateEvent.onString.RemoveListener(_stateListener);
                    if (_stateClearedListener != null && _stateEvent.onStateCleared != null)
                        _stateEvent.onStateCleared.RemoveListener(_stateClearedListener);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MediaSync] Listener cleanup failed: {ex.Message}");
            }

            if (root != null)
            {
                try
                {
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
                catch { }
            }
            _bridgeRoot = null;
            _stateEvent = null;
            _stateListener = null;
            _stateClearedListener = null;
        }

        private static void RecordFailure(string stage, Exception ex)
        {
            _status = "Multiplayer media sync is retrying.";
            _nextRetryTime = Time.unscaledTime + RetrySeconds;
            Log.Warning($"[MediaSync] {stage} failed: {ex.GetType().Name}: {ex.Message}");
        }

        private static string CleanTitle(string title)
        {
            string clean = (title ?? "shared track").Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= 80 ? clean : clean.Substring(0, 77) + "...";
        }
    }
}
