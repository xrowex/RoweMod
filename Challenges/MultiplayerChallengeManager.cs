using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppMashBox.Addons.NetworkingFusion;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.Core.Runtime.Gameplay.ActivityTracking;
using Il2CppMashBoxSDK.Services;
using Newtonsoft.Json;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Challenges
{
    public static class MultiplayerChallengeManager
    {
        public const string RoweModMarkerName = "I_HAVE_ROWE_MOD";

        private const int WindowId = 0x524f5745;
        private const string ChallengeEventKey = "rowemod.mp.challenge.v1";
        private static Rect _windowRect = new Rect(180f, 120f, 460f, 560f);
        private static bool _isOpen;
        private static Vector2 _playerScroll;
        private static Vector2 _lineScroll;
        private static readonly List<RowePlayer> _rowePlayers = new List<RowePlayer>();
        private static readonly List<int> _lineTrickIndexes = new List<int> { 0 };
        private static readonly List<bool> _dropdownOpen = new List<bool> { false };
        private static readonly List<Vector2> _dropdownScroll = new List<Vector2> { Vector2.zero };
        private static string[] _trickNames = Array.Empty<string>();
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
        private static int _lastMatchedHistoryCount = -1;
        private static bool _networkSyncDisabled;
        private static bool _playerScanDisabled;
        private static bool _trickHistoryDisabled;
        
        public static ActivityTracker ActivityTracker { get; set; }
        
        private sealed class RowePlayer
        {
            public string Key;
            public string DisplayName;
            public NetworkPlayer NetworkPlayer;
        }

        private sealed class ActiveChallenge
        {
            public string CreatorKey;
            public string CreatorName;
            public List<string> Tricks = new List<string>();
            public Dictionary<string, bool> PlayerCompleted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public float[] Position;
            public float[] Rotation;
            public float[] Size;
        }

        private sealed class NetworkChallengeState
        {
            public string CreatorKey { get; set; }
            public string CreatorName { get; set; }
            public List<string> Tricks { get; set; }
            public Dictionary<string, bool> PlayerCompleted { get; set; }
            public float[] Position { get; set; }
            public float[] Rotation { get; set; }
            public float[] Size { get; set; }
        }

        public static bool IsOpen => _isOpen;
        public static bool HasActiveChallenge => _activeChallenge != null;

        public static void ToggleWindow()
        {
            _isOpen = !_isOpen;
            if (_isOpen)
            {
                RefreshNow();
            }
        }

        public static void OnLocalPlayerSpawned(GameObject playerObject)
        {
            TryEnsureLocalModMarker(playerObject);
            SafeRefreshPlayers();
        }

        public static void NotifyChallengeAreaExited()
        {
            SafeCheckLocalLineCompletion();
        }

        public static void DrawWindow()
        {
            if (!_isOpen)
                return;

            _windowRect = GUI.Window(WindowId, _windowRect, (GUI.WindowFunction)DrawWindowContents, "RoweMod MP Challenge", Menu.windowStyle);
        }

        public static void NotifyLocalChallengeCompleted()
        {
            if (_activeChallenge == null)
                return;

            string key = GetLocalPlayerKey();
            if (string.IsNullOrEmpty(key))
                return;

            _activeChallenge.PlayerCompleted[key] = true;
            ChallengeAreaManager.SetCompleted(true);
            PublishActiveChallengeState();
        }

        private static void DrawWindowContents(int windowId)
        {
            try
            {
                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh", Menu.highQualityButtonStyle, GUILayout.Width(100f)))
                {
                    RefreshNow();
                }

                if (GUILayout.Button("Close", Menu.redButtonStyle, GUILayout.Width(100f)))
                {
                    _isOpen = false;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(8f);
                DrawStatusMessages();
                DrawPlayerList();
                GUILayout.Space(10f);
                DrawChallengeBuilder();

                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            }
            catch (Exception ex)
            {
                Log.Error($"[MPChallenge] Draw failed: {ex.Message}");
            }
        }

        private static void DrawStatusMessages()
        {
            if (_networkSyncDisabled)
            {
                GUILayout.Label("Network challenge sync is disabled after an SDK error.", Menu.subtleLabelStyle);
            }

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
                    bool complete = _activeChallenge != null &&
                                    _activeChallenge.PlayerCompleted.TryGetValue(player.Key, out bool value) &&
                                    value;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(complete ? "\u2713" : "X", Menu.labelStyle, GUILayout.Width(22f));
                    GUILayout.Label(player.DisplayName, Menu.labelStyle);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }

        private static void DrawChallengeBuilder()
        {
            if (_activeChallenge != null)
            {
                GUILayout.Label("Active Line", Menu.labelStyle);
                GUILayout.Label(string.Join(" + ", _activeChallenge.Tricks), Menu.subtleLabelStyle);
                GUILayout.Label($"Created by: {_activeChallenge.CreatorName}", Menu.subtleLabelStyle);

                if (IsLocalCreator() && GUILayout.Button("Clear Active Challenge", Menu.redButtonStyle))
                {
                    ClearActiveChallenge();
                    return;
                }

                return;
            }

            GUILayout.Label("Build Line", Menu.labelStyle);
            EnsureTrickNames();

            if (_trickNames.Length == 0)
            {
                GUILayout.Label("No tricks loaded yet. Open the Tricks tab or respawn to load trick data.", Menu.subtleLabelStyle);
                return;
            }

            _lineScroll = GUILayout.BeginScrollView(_lineScroll, GUILayout.Height(210f));
            for (int i = 0; i < _lineTrickIndexes.Count; i++)
            {
                EnsureLineState(i);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}.", Menu.labelStyle, GUILayout.Width(22f));

                int trickIndex = Mathf.Clamp(_lineTrickIndexes[i], 0, _trickNames.Length - 1);
                if (GUILayout.Button(_trickNames[trickIndex], Menu.highQualityButtonStyle))
                {
                    _dropdownOpen[i] = !_dropdownOpen[i];
                }

                if (_lineTrickIndexes.Count > 1 && GUILayout.Button("X", Menu.redButtonStyle, GUILayout.Width(34f)))
                {
                    _lineTrickIndexes.RemoveAt(i);
                    _dropdownOpen.RemoveAt(i);
                    _dropdownScroll.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();

                if (_dropdownOpen[i])
                {
                    DrawTrickDropdown(i);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+", Menu.highQualityButtonStyle, GUILayout.Width(44f)))
            {
                _lineTrickIndexes.Add(0);
                _dropdownOpen.Add(false);
                _dropdownScroll.Add(Vector2.zero);
            }

            if (GUILayout.Button("Accept", Menu.highQualityButtonStyle))
            {
                AcceptChallenge();
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawTrickDropdown(int lineIndex)
        {
            _dropdownScroll[lineIndex] = GUILayout.BeginScrollView(_dropdownScroll[lineIndex], GUILayout.Height(160f));
            for (int j = 0; j < _trickNames.Length; j++)
            {
                if (GUILayout.Button(_trickNames[j], Menu.highQualityButtonStyle))
                {
                    _lineTrickIndexes[lineIndex] = j;
                    _dropdownOpen[lineIndex] = false;
                }
            }
            GUILayout.EndScrollView();
        }

        private static void AcceptChallenge()
        {
            RefreshNow();
            string creatorKey = GetLocalPlayerKey();
            if (string.IsNullOrEmpty(creatorKey))
            {
                Log.Warning("[MPChallenge] Cannot accept challenge without a local RoweMod player marker.");
                return;
            }

            List<string> tricks = _lineTrickIndexes
                .Select(index => _trickNames[Mathf.Clamp(index, 0, _trickNames.Length - 1)])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (tricks.Count == 0)
                return;

            if (TryReadNetworkChallengeState(out NetworkChallengeState existingState) &&
                existingState != null &&
                existingState.Tricks != null &&
                existingState.Tricks.Count > 0)
            {
                ApplyNetworkState(existingState);
                Log.Warning("[MPChallenge] A challenge is already active on the network.");
                return;
            }

            _activeChallenge = new ActiveChallenge
            {
                CreatorKey = creatorKey,
                CreatorName = GetLocalPlayerName(),
                Tricks = tricks
            };

            foreach (RowePlayer player in _rowePlayers)
            {
                _activeChallenge.PlayerCompleted[player.Key] = false;
            }

            ChallengeArea area = SpawnLocalChallengeArea();
            if (area != null)
            {
                _activeChallenge.Position = Vector3ToArray(area.transform.position);
                _activeChallenge.Rotation = QuaternionToArray(area.transform.rotation);
                _activeChallenge.Size = Vector3ToArray(area.transform.localScale);
            }

            PublishActiveChallengeState();
            Log.Msg($"[MPChallenge] Accepted local line: {string.Join(" + ", tricks)}");
        }

        private static ChallengeArea SpawnLocalChallengeArea()
        {
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (Memory.physicsDrivenCharacter != null)
            {
                spawnPos = Memory.physicsDrivenCharacter.transform.position;
                spawnRot = Memory.physicsDrivenCharacter.transform.rotation;
            }
            else if (UnityEngine.Camera.main != null)
            {
                spawnPos = UnityEngine.Camera.main.transform.position + UnityEngine.Camera.main.transform.forward * 5f;
                spawnRot = UnityEngine.Camera.main.transform.rotation;
            }

            ChallengeArea area = ChallengeAreaManager.Create(
                spawnPos,
                new Vector3(Config.challengeSettings.challengeSizeX, Config.challengeSettings.challengeSizeY, Config.challengeSettings.challengeSizeZ),
                spawnRot,
                "VehicleColliders");

            ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
            ChallengeAreaManager.SetCompleted(false);
            return area;
        }

        private static void EnsureLocalModMarker(GameObject playerObject = null)
        {
            GameObject markerRoot = playerObject != null ? playerObject : Memory.physicsDrivenCharacter;
            if (markerRoot == null)
                return;

            Transform existing = markerRoot.transform.Find(RoweModMarkerName);
            if (existing != null)
                return;

            GameObject marker = new GameObject(RoweModMarkerName);
            marker.transform.SetParent(markerRoot.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            marker.hideFlags = HideFlags.DontSave;
        }

        private static void RefreshNow()
        {
            TryEnsureLocalModMarker();
            SafePollNetworkState();
            SafeRefreshPlayers();
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

        private static void SafePollNetworkState()
        {
            if (_networkSyncDisabled)
                return;

            try
            {
                PollNetworkState();
            }
            catch (Exception ex)
            {
                DisableNetworkSync($"poll failed: {ex.Message}");
            }
        }

        private static void SafeRefreshPlayers()
        {
            if (_playerScanDisabled)
                return;

            try
            {
                RefreshPlayers();
            }
            catch (Exception ex)
            {
                _playerScanDisabled = true;
                Log.Warning($"[MPChallenge] Disabled player scanning after lookup failed: {ex.Message}");
            }
        }

        private static void SafeCheckLocalLineCompletion()
        {
            if (_trickHistoryDisabled)
                return;

            try
            {
                CheckLocalLineCompletion();
            }
            catch (Exception ex)
            {
                _trickHistoryDisabled = true;
                Log.Warning($"[MPChallenge] Disabled trick history checking after lookup failed: {ex.Message}");
            }
        }

        private static void RefreshPlayers()
        {
            _rowePlayers.Clear();

            NetworkPlayer[] players = UnityEngine.Object.FindObjectsOfType<NetworkPlayer>(true);
            foreach (NetworkPlayer player in players)
            {
                if (player == null || player.gameObject == null)
                    continue;

                if (!HasRoweModMarker(player.gameObject))
                    continue;

                string name = ResolvePlayerDisplayName(player);
                string key = ResolvePlayerKey(player, name);
                _rowePlayers.Add(new RowePlayer
                {
                    Key = key,
                    DisplayName = name,
                    NetworkPlayer = player
                });

                if (_activeChallenge != null && !_activeChallenge.PlayerCompleted.ContainsKey(key))
                {
                    _activeChallenge.PlayerCompleted[key] = false;
                }
            }

            AddLocalFallbackPlayerIfNeeded();
        }

        private static bool HasRoweModMarker(GameObject playerObject)
        {
            Transform[] children = playerObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child != null && string.Equals(child.name, RoweModMarkerName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void EnsureTrickNames()
        {
            if (_trickNames.Length > 0)
                return;

            _trickNames = ChallengeExtraTrickNames
                .Concat(TrickMods.GetAvailableTrickNames().OrderBy(name => name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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
            if (_activeChallenge == null || _activeChallenge.Tricks == null || _activeChallenge.Tricks.Count == 0)
                return;

            string localKey = GetLocalPlayerKey();
            if (string.IsNullOrEmpty(localKey))
                return;

            if (_activeChallenge.PlayerCompleted.TryGetValue(localKey, out bool alreadyComplete) && alreadyComplete)
                return;

            List<string> history = GetCurrentTrickHistory();
            if (history.Count == 0 || history.Count == _lastMatchedHistoryCount)
                return;

            if (!ContainsLineInOrder(history, _activeChallenge.Tricks))
                return;

            _lastMatchedHistoryCount = history.Count;
            _activeChallenge.PlayerCompleted[localKey] = true;
            ChallengeAreaManager.SetCompleted(true);
            PublishActiveChallengeState();
            Log.Msg($"[MPChallenge] Local line completed: {string.Join(" + ", _activeChallenge.Tricks)}");
        }

        private static List<string> GetCurrentTrickHistory()
        {
            if (_trickDetection == null)
            {
                _trickDetection = UnityEngine.Object.FindObjectOfType<TrickDetection>();
            }

            List<string> history = new List<string>();
            if (_trickDetection == null)
                return history;

            object rawHistory = TryGetMemberValue(_trickDetection, "_trickHistory") ??
                                TryGetMemberValue(_trickDetection, "trickHistory") ??
                                TryGetMemberValue(_trickDetection, "TrickHistory");

            if (rawHistory == null)
                return history;

            if (rawHistory is IEnumerable<string> managedStrings)
            {
                history.AddRange(managedStrings.Where(value => !string.IsNullOrWhiteSpace(value)));
                return history;
            }

            System.Collections.IEnumerable enumerable = rawHistory as System.Collections.IEnumerable;
            if (enumerable == null)
                return history;

            foreach (object item in enumerable)
            {
                string text = item?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    history.Add(text);
            }

            return history;
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
                return normalizedActual.Contains("nose manual");

            if (normalizedRequired == "manual")
                return normalizedActual.Contains("manual") && !normalizedActual.Contains("nose manual");

            return normalizedActual.Equals(normalizedRequired, StringComparison.OrdinalIgnoreCase) ||
                   normalizedActual.Contains(normalizedRequired) ||
                   normalizedRequired.Contains(normalizedActual);
        }

        private static string NormalizeTrickName(string trickName)
        {
            if (string.IsNullOrWhiteSpace(trickName))
                return string.Empty;

            string normalized = InsertWordBoundaries(trickName.Trim()).ToLowerInvariant();
            normalized = normalized.Replace("_", " ");
            normalized = normalized.Replace("-", " ");
            normalized = normalized.Replace("nosemanual", "nose manual");
            normalized = normalized.Replace("nose manual", "nose manual");
            normalized = normalized.Replace("degrees", string.Empty);
            normalized = normalized.Replace("degree", string.Empty);
            normalized = normalized.Replace("rotation", string.Empty);
            normalized = normalized.Replace("rot", string.Empty);
            normalized = normalized.Replace("spin", string.Empty);
            normalized = normalized.Replace("left", string.Empty);
            normalized = normalized.Replace("right", string.Empty);
            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");
            return normalized.Trim();
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

            string degreeText = degrees.ToString();
            for (int i = 0; i < ChallengeExtraTrickNames.Length; i++)
            {
                if (ChallengeExtraTrickNames[i] == degreeText)
                    return true;
            }

            return false;
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

        private static void PollNetworkState()
        {
            if (TryReadNetworkChallengeState(out NetworkChallengeState state))
            {
                ApplyNetworkState(state);
            }
        }

        private static bool TryReadNetworkChallengeState(out NetworkChallengeState state)
        {
            state = null;

            if (_networkSyncDisabled)
                return false;

            try
            {
                if (!NetworkedEventService.TryGetState(ChallengeEventKey, out MBNetworkedEventPayload payload))
                    return false;

                if (string.IsNullOrWhiteSpace(payload.StringValue))
                    return false;

                state = JsonConvert.DeserializeObject<NetworkChallengeState>(payload.StringValue);
                return true;
            }
            catch (Exception ex)
            {
                DisableNetworkSync($"read failed: {ex.Message}");
                return false;
            }
        }

        private static void HandleNetworkPayload(MBNetworkedEventPayload payload)
        {
            if (_networkSyncDisabled)
                return;

            try
            {
                if (string.IsNullOrWhiteSpace(payload.StringValue))
                    return;

                NetworkChallengeState state = JsonConvert.DeserializeObject<NetworkChallengeState>(payload.StringValue);
                ApplyNetworkState(state);
            }
            catch (Exception ex)
            {
                DisableNetworkSync($"payload handling failed: {ex.Message}");
            }
        }

        private static void ApplyNetworkState(NetworkChallengeState state)
        {
            if (state == null || state.Tricks == null || state.Tricks.Count == 0)
            {
                _activeChallenge = null;
                ChallengeAreaManager.DestroyActive();
                return;
            }

            _activeChallenge = new ActiveChallenge
            {
                CreatorKey = state.CreatorKey,
                CreatorName = state.CreatorName,
                Tricks = state.Tricks.ToList(),
                PlayerCompleted = state.PlayerCompleted != null
                    ? new Dictionary<string, bool>(state.PlayerCompleted, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
                Position = state.Position,
                Rotation = state.Rotation,
                Size = state.Size
            };

            SyncChallengeAreaFromActiveState();
        }

        private static void PublishActiveChallengeState()
        {
            if (_activeChallenge == null)
                return;

            NetworkChallengeState state = new NetworkChallengeState
            {
                CreatorKey = _activeChallenge.CreatorKey,
                CreatorName = _activeChallenge.CreatorName,
                Tricks = _activeChallenge.Tricks.ToList(),
                PlayerCompleted = new Dictionary<string, bool>(_activeChallenge.PlayerCompleted, StringComparer.OrdinalIgnoreCase),
                Position = _activeChallenge.Position,
                Rotation = _activeChallenge.Rotation,
                Size = _activeChallenge.Size
            };

            string json = JsonConvert.SerializeObject(state);
            TryPublishNetworkState(json);
        }

        private static bool TryPublishNetworkState(string json)
        {
            if (_networkSyncDisabled)
                return false;

            try
            {
                NetworkedEventService.SetState(MBNetworkedEventPayload.String(ChallengeEventKey, json));
                return true;
            }
            catch (Exception ex)
            {
                DisableNetworkSync($"publish failed: {ex.Message}");
                return false;
            }
        }

        private static void DisableNetworkSync(string reason)
        {
            if (_networkSyncDisabled)
                return;

            _networkSyncDisabled = true;
            Log.Warning($"[MPChallenge] Disabled network challenge sync because {reason}");
        }

        private static void SyncChallengeAreaFromActiveState()
        {
            if (_activeChallenge == null)
                return;

            Vector3 position = ArrayToVector3(_activeChallenge.Position, Vector3.zero);
            Quaternion rotation = ArrayToQuaternion(_activeChallenge.Rotation, Quaternion.identity);
            Vector3 size = ArrayToVector3(_activeChallenge.Size,
                new Vector3(Config.challengeSettings.challengeSizeX, Config.challengeSettings.challengeSizeY, Config.challengeSettings.challengeSizeZ));

            ChallengeArea active = ChallengeAreaManager.Active;
            if (active == null)
            {
                ChallengeAreaManager.Create(position, size, rotation, "VehicleColliders");
            }
            else
            {
                ChallengeAreaManager.SetPosition(position);
                ChallengeAreaManager.SetRotation(rotation);
                ChallengeAreaManager.SetSize(size);
            }

            ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
            ChallengeAreaManager.SetCompleted(IsLocalPlayerComplete());
        }

        private static bool IsLocalPlayerComplete()
        {
            if (_activeChallenge == null || _activeChallenge.PlayerCompleted == null)
                return false;

            string localKey = GetLocalPlayerKey();
            return !string.IsNullOrEmpty(localKey) &&
                   _activeChallenge.PlayerCompleted.TryGetValue(localKey, out bool completed) &&
                   completed;
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
            ChallengeAreaManager.DestroyActive();
            _activeChallenge = null;
            TryPublishNetworkState("{}");
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

            if (_activeChallenge != null && !_activeChallenge.PlayerCompleted.ContainsKey(key))
            {
                _activeChallenge.PlayerCompleted[key] = false;
            }
        }

        private static bool IsLocalCreator()
        {
            return _activeChallenge != null &&
                   string.Equals(_activeChallenge.CreatorKey, GetLocalPlayerKey(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLocalPlayerKey()
        {
            NetworkPlayer[] players = UnityEngine.Object.FindObjectsOfType<NetworkPlayer>(true);
            foreach (NetworkPlayer player in players)
            {
                if (player == null)
                    continue;

                if (!TryGetBoolMember(player, "_isLocal", out bool isLocal) || !isLocal)
                    continue;

                string name = ResolvePlayerDisplayName(player);
                return ResolvePlayerKey(player, name);
            }

            return Memory.physicsDrivenCharacter != null
                ? $"local:{Memory.physicsDrivenCharacter.GetInstanceID()}"
                : null;
        }

        private static string GetLocalPlayerName()
        {
            NetworkPlayer[] players = UnityEngine.Object.FindObjectsOfType<NetworkPlayer>(true);
            foreach (NetworkPlayer player in players)
            {
                if (player == null)
                    continue;

                if (TryGetBoolMember(player, "_isLocal", out bool isLocal) && isLocal)
                    return ResolvePlayerDisplayName(player);
            }

            return "Local Player";
        }

        private static string ResolvePlayerKey(NetworkPlayer player, string displayName)
        {
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
            string name = TryGetStringMember(player, "DisplayName") ??
                          TryGetStringMember(player, "PlayerName") ??
                          TryGetStringMember(player, "UserName") ??
                          TryGetStringMember(player, "Name") ??
                          TryGetStringMember(player, "_displayName") ??
                          TryGetStringMember(player, "_playerName") ??
                          TryGetStringMember(player, "_userName");

            if (!string.IsNullOrWhiteSpace(name))
                return name;

            return player.gameObject != null ? player.gameObject.name : "Unknown Player";
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
            string text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
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
