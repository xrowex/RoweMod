using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using GameReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;
using Il2CppMashBox.BMX_Physics_Development;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace rowemod.Mods
{
    /// <summary>
    /// Local-only peg grind visuals. BMXCollisionHandler owns all contact/grind state; RoweMod
    /// reads its four native peg contact objects and renders pooled VFX. No hierarchy scan,
    /// remote rider access, or networking is involved after the local rider has been resolved.
    /// </summary>
    public static class PegSparks
    {
        private const string BundleFileName = "rowemod_peg_sparks";
        private const string PrefabFileName = "rowemodpegsparks.prefab";
        private const string BundleDownloadUrl =
            "https://raw.githubusercontent.com/xrowex/RoweMod/master/Bundles/rowemod_peg_sparks";
        private const string BundleSha256 =
            "DD2C7AB0D16125F807D911A2EB07E5695A9555E12D2F611C323C38904C7C640F";
        private const int BundleDownloadTimeoutSeconds = 30;
        private const long MaxBundleDownloadBytes = 12L * 1024L * 1024L;
        private const float ReplaySampleExpirySeconds = 0.12f;
        private const float ReplaySeekResetSeconds = 0.35f;
        private const int MaxReplaySamples = 2400;
        private const int PegCount = 4;
        private static readonly string[] SparkColorProperties =
        {
            "Spark Color",
            "Sparks Color",
            "Spark Colour",
            "Sparks Colour",
            "Color",
            "Colour"
        };
        private static readonly string[] SparkGradientProperties =
        {
            "Spark Color Gradient",
            "Sparks Color Gradient",
            "Spark Colour Gradient",
            "Sparks Colour Gradient",
            "Color Gradient"
        };
        private static readonly Vector4 HotOrangeSparkColor =
            new Vector4(2.5f, 0.55f, 0.02f, 1f);
        private static Gradient _hotOrangeGradient;
        private static readonly PegRuntime[] Runtimes =
        {
            new PegRuntime("Front Right"),
            new PegRuntime("Front Left"),
            new PegRuntime("Rear Left"),
            new PegRuntime("Rear Right")
        };

        private static GameObject _localRoot;
        private static BMXCollisionHandler _collisionHandler;
        private static AssetBundle _bundle;
        private static bool _ownsBundle;
        private static bool _bundleDownloadInProgress;
        private static GameObject _rigPrefab;
        private static readonly List<ReplaySample> ReplaySamples =
            new List<ReplaySample>(MaxReplaySamples);
        private static readonly ReplaySample[] ReplayLatestSamples =
            new ReplaySample[PegCount];
        private static readonly bool[] HasReplayLatestSample = new bool[PegCount];
        private static bool _replayActive;
        private static bool _loggedFirstReplaySample;
        private static int _replayCursor;
        private static float _lastReplayPlaybackTime = float.NegativeInfinity;
        private static float _previewUntil;
        private static bool _previewOnNextLocalSpawn;
        private static string _status = "Waiting for the local rider.";

        public static string Status => _status;

        public static void OnReplayOpened()
        {
            _replayActive = true;
            ResetReplayPlayback();
            StopAllEffects(true);
            Log.Msg($"[PegSparks] Replay playback armed ({ReplaySamples.Count} recorded samples).");
        }

        public static void OnReplayClosed()
        {
            _replayActive = false;
            ResetReplayPlayback();
            StopAllEffects(true);
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            Cleanup(true);
            ClearReplaySamples();
            _status = gameplayScene
                ? "Waiting for the local rider."
                : "Peg sparks are available in gameplay.";
        }

        public static void OnLocalPlayerSpawned(GameObject localPlayer)
        {
            Cleanup(true);
            if (localPlayer == null || Memory.rMbCharacter == null)
            {
                _status = "Local rider is unavailable.";
                return;
            }

            _localRoot = Memory.rMbCharacter;
            _collisionHandler = Memory.collisionHandler ??
                                _localRoot.GetComponentInChildren<BMXCollisionHandler>(true);
            if (_collisionHandler == null)
            {
                _status = "Native BMXCollisionHandler is unavailable.";
                Log.Warning("[PegSparks] BMXCollisionHandler was not found under the local rider.");
                return;
            }

            bool rigsReady = Config.pegSparksSettings?.enabled != true || EnsureRigs();
            if (rigsReady)
                _status = "Native peg contact monitor ready (4 local peg states).";
            Log.Msg("[PegSparks] " + _status);

            if (_previewOnNextLocalSpawn)
            {
                _previewOnNextLocalSpawn = false;
                TriggerPreview();
            }
        }

        public static void OnSettingsChanged()
        {
            Config.NormalizePegSparksSettings(Config.pegSparksSettings);
            if (Config.pegSparksSettings?.enabled != true)
            {
                StopAllEffects(true);
                _status = "Disabled.";
                return;
            }

            if (_localRoot != null)
            {
                bool rigsReady = EnsureRigs();
                if (rigsReady)
                    _status = "Native peg contact monitor ready (4 local peg states).";
            }
        }

        public static void DrawControls()
        {
            PegSparksSettings settings = Config.pegSparksSettings ??= new PegSparksSettings();
            Config.NormalizePegSparksSettings(settings);

            Menu.BeginAltPane(
                "Peg Sparks",
                "High-quality local VFX, driven by BMXCollisionHandler's native peg-grind state.");

            bool enabled = settings.enabled;
            Menu.ModernToggle("Enable Peg Sparks", ref enabled, "peg_sparks_enabled");
            bool changed = enabled != settings.enabled;
            settings.enabled = enabled;

            float speed = settings.minimumSlideSpeed;
            Menu.ModernSlider("Minimum Slide Speed", ref speed, 0.1f, 12f, "peg_sparks_min_speed");
            changed |= !Mathf.Approximately(speed, settings.minimumSlideSpeed);
            settings.minimumSlideSpeed = speed;

            float intensity = settings.intensity;
            Menu.ModernSlider("Intensity", ref intensity, 0f, 3f, "peg_sparks_intensity");
            changed |= !Mathf.Approximately(intensity, settings.intensity);
            settings.intensity = intensity;

            float rate = settings.updateRate;
            Menu.ModernSlider("VFX Update Rate", ref rate, 10f, 60f, "peg_sparks_rate");
            changed |= !Mathf.Approximately(rate, settings.updateRate);
            settings.updateRate = rate;

            float sparkSize = settings.sparkSize;
            Menu.ModernSlider("Spark Size", ref sparkSize, 0.25f, 3f, "peg_sparks_size");
            changed |= !Mathf.Approximately(sparkSize, settings.sparkSize);
            settings.sparkSize = sparkSize;

            float sparkLifetime = settings.sparkLifetime;
            Menu.ModernSlider("Spark Lifetime", ref sparkLifetime, 0.25f, 2f, "peg_sparks_lifetime");
            changed |= !Mathf.Approximately(sparkLifetime, settings.sparkLifetime);
            settings.sparkLifetime = sparkLifetime;

            float sparkSpeed = settings.sparkSpeed;
            Menu.ModernSlider("Spark Speed", ref sparkSpeed, 0.25f, 3f, "peg_sparks_speed");
            changed |= !Mathf.Approximately(sparkSpeed, settings.sparkSpeed);
            settings.sparkSpeed = sparkSpeed;

            float trail = settings.trailSeconds;
            Menu.ModernSlider("Trail Length", ref trail, 0.05f, 1f, "peg_sparks_trail");
            changed |= !Mathf.Approximately(trail, settings.trailSeconds);
            settings.trailSeconds = trail;

            bool recordReplay = settings.recordInReplay;
            Menu.ModernToggle("Record for Replay", ref recordReplay, "peg_sparks_replay");
            changed |= recordReplay != settings.recordInReplay;
            settings.recordInReplay = recordReplay;

            if (Menu.SecondaryButton("Test Sparks (3 Seconds)", GUILayout.Height(30f)))
                TriggerPreview();

            GUILayout.Label("Status: " + Status, Menu.UiBadgeStyle);
            GUILayout.Label(
                "Hot-orange sparks spray backward and away from the contact surface. Native contact points choose the exact peg end when it is touching the rail.",
                Menu.UiMutedWrappedStyle);

            if (changed)
            {
                OnSettingsChanged();
                Config.RequestSave();
            }

            Menu.EndPane();
        }

        public static void Cleanup(bool preserveQueuedPreview = false)
        {
            StopAllEffects(true);
            _replayActive = false;
            _previewUntil = 0f;
            if (!preserveQueuedPreview)
                _previewOnNextLocalSpawn = false;
            ResetReplayPlayback();

            for (int i = 0; i < Runtimes.Length; i++)
                Runtimes[i].DestroyRig();

            _rigPrefab = null;
            _localRoot = null;
            _collisionHandler = null;
            if (_bundle != null && _ownsBundle)
                _bundle.Unload(false);
            _bundle = null;
            _ownsBundle = false;
        }

        public static void Update()
        {
            if (_previewUntil > 0f)
            {
                UpdatePreview();
                return;
            }

            if (_replayActive)
                UpdateReplayPlayback();
        }

        public static void FixedUpdate()
        {
            if (_previewUntil > 0f || _replayActive ||
                Config.pegSparksSettings?.enabled != true || _collisionHandler == null)
                return;

            float now = Time.unscaledTime;
            for (int i = 0; i < PegCount; i++)
                Runtimes[i].UpdateTrail(now);

            if (!IsNativePegGrinding())
            {
                StopAllEffects();
                return;
            }

            for (int i = 0; i < PegCount; i++)
                UpdateNativePeg(i);
        }

        public static void TriggerPreview()
        {
            if (Config.pegSparksSettings?.enabled != true)
            {
                _status = "Enable Peg Sparks before running the preview.";
                return;
            }

            if (_collisionHandler == null || !EnsureRigs())
            {
                if (!_bundleDownloadInProgress)
                    _status = "Mount a local bike before running the spark preview.";
                return;
            }

            StopAllEffects(true);
            _previewUntil = Time.unscaledTime + 3f;
            _status = "Playing a three-second spark preview.";
            Log.Msg("[PegSparks] Manual three-second VFX preview started.");
        }

        public static void QueuePreviewOnNextLocalSpawn()
        {
            _previewOnNextLocalSpawn = true;
            _status = "Spark preview queued for the next local rider spawn.";
            Log.Msg("[PegSparks] Three-second VFX preview queued for the next local rider spawn.");
        }

        public static void CancelQueuedPreview()
        {
            _previewOnNextLocalSpawn = false;
        }

        private static void UpdatePreview()
        {
            if (_collisionHandler == null || Time.unscaledTime >= _previewUntil)
            {
                _previewUntil = 0f;
                StopAllEffects(false);
                _status = "Spark preview complete.";
                return;
            }

            PegRuntime runtime = Runtimes[3];
            if (!EnsureRig(runtime))
                return;

            Transform bike = _collisionHandler.transform;
            Vector3 normal = Vector3.up;
            Vector3 tangent = Vector3.ProjectOnPlane(bike.forward, normal);
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.forward;
            else
                tangent.Normalize();

            Vector3 position = bike.position + bike.right * 0.65f +
                               Vector3.up * 0.2f - tangent * 0.25f;
            PegSparksSettings settings = Config.pegSparksSettings;
            runtime.UpdateContinuous(position, normal, tangent, settings);
        }

        private static void UpdateNativePeg(int pegIndex)
        {
            bool hasContact;
            Vector3 point;
            Vector3 normal;
            try
            {
                switch (pegIndex)
                {
                    case 0:
                        hasContact = BMXCollisionHandler.HasContact(_collisionHandler._frontRightPegContactInfoObj);
                        point = BMXCollisionHandler.ContactPoint(_collisionHandler._frontRightPegContactInfoObj);
                        normal = BMXCollisionHandler.ContactNormal(_collisionHandler._frontRightPegContactInfoObj);
                        break;
                    case 1:
                        hasContact = BMXCollisionHandler.HasContact(_collisionHandler._frontLeftPegContactInfoObj);
                        point = BMXCollisionHandler.ContactPoint(_collisionHandler._frontLeftPegContactInfoObj);
                        normal = BMXCollisionHandler.ContactNormal(_collisionHandler._frontLeftPegContactInfoObj);
                        break;
                    case 2:
                        hasContact = BMXCollisionHandler.HasContact(_collisionHandler._backLeftPegContactInfoObj);
                        point = BMXCollisionHandler.ContactPoint(_collisionHandler._backLeftPegContactInfoObj);
                        normal = BMXCollisionHandler.ContactNormal(_collisionHandler._backLeftPegContactInfoObj);
                        break;
                    case 3:
                        hasContact = BMXCollisionHandler.HasContact(_collisionHandler._backRightPegContactInfoObj);
                        point = BMXCollisionHandler.ContactPoint(_collisionHandler._backRightPegContactInfoObj);
                        normal = BMXCollisionHandler.ContactNormal(_collisionHandler._backRightPegContactInfoObj);
                        break;
                    default:
                        return;
                }
            }
            catch (Exception ex)
            {
                _status = "Native peg contact read failed.";
                Log.Warning("[PegSparks] Native peg contact read failed: " + ex.Message);
                return;
            }

            PegRuntime runtime = Runtimes[pegIndex];
            if (!hasContact)
            {
                runtime.StopContinuous(false);
                return;
            }

            if (normal.sqrMagnitude < 0.001f)
                normal = GetNativeHitNormal(pegIndex);
            else
                normal.Normalize();

            Vector3 velocity = _collisionHandler._thisBody != null
                ? _collisionHandler._thisBody.velocity
                : (Memory.vehicleController != null ? Memory.vehicleController.Velocity : Vector3.zero);
            Vector3 tangent = Vector3.ProjectOnPlane(velocity, normal);
            PegSparksSettings settings = Config.pegSparksSettings;
            float slideSpeed = tangent.magnitude;
            if (slideSpeed < settings.minimumSlideSpeed)
            {
                runtime.StopContinuous(false);
                return;
            }

            float now = Time.unscaledTime;
            Vector3 normalizedTangent = tangent.normalized;
            if (now >= runtime.NextUpdateTime)
            {
                runtime.NextUpdateTime = now + (1f / Config.pegSparksSettings.updateRate);
                if (EnsureRig(runtime))
                    runtime.UpdateContinuous(point, normal, normalizedTangent, settings);

                RecordReplaySample(pegIndex, point, normal, normalizedTangent);
            }

        }

        private static bool IsNativePegGrinding()
        {
            try
            {
                // The game exposes this exact native predicate; it excludes ordinary peg contact
                // when no peg is in its grind state.
                return _collisionHandler != null &&
                       _collisionHandler.AnyPegColliding() &&
                       !_collisionHandler.NoPegsGrinding();
            }
            catch (Exception ex)
            {
                _status = "Native peg state read failed.";
                Log.Warning("[PegSparks] Native grind-state read failed: " + ex.Message);
                return false;
            }
        }

        private static Vector3 GetNativeHitNormal(int pegIndex)
        {
            try
            {
                Vector3 normal = pegIndex switch
                {
                    0 => _collisionHandler.FrontRightPegHitNormal(),
                    1 => _collisionHandler.FrontLeftPegHitNormal(),
                    2 => _collisionHandler.BackLeftPegHitNormal(),
                    3 => _collisionHandler.BackRightPegHitNormal(),
                    _ => Vector3.up
                };
                return normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            }
            catch
            {
                return Vector3.up;
            }
        }

        private static bool EnsureRig(PegRuntime runtime)
        {
            if (runtime.Rig != null)
                return true;

            if (!EnsureRigs() || _rigPrefab == null)
                return false;

            runtime.CreateRig(_rigPrefab);
            return runtime.Rig != null;
        }

        private static bool EnsureRigs()
        {
            if (_rigPrefab != null)
                return true;

            string path = Path.Combine(Memory.bundlesFolderPath, BundleFileName);
            _bundle = FindAlreadyLoadedBundle();
            if (_bundle == null && File.Exists(path))
            {
                _bundle = AssetBundle.LoadFromFile(path);
                _ownsBundle = _bundle != null;
            }

            if (_bundle == null)
            {
                BeginBundleDownload(path);
                return false;
            }

            string prefabPath = null;
            foreach (string assetName in _bundle.GetAllAssetNames())
            {
                if (assetName.EndsWith(PrefabFileName, StringComparison.OrdinalIgnoreCase))
                {
                    prefabPath = assetName;
                    break;
                }
            }

            _rigPrefab = prefabPath == null ? null : _bundle.LoadAsset<GameObject>(prefabPath);
            if (_rigPrefab == null)
            {
                _status = "Peg Sparks prefab is missing from its asset bundle.";
                return false;
            }

            return true;
        }

        private static void BeginBundleDownload(string bundlePath)
        {
            if (_bundleDownloadInProgress)
                return;

            _bundleDownloadInProgress = true;
            _status = "Downloading the Peg Sparks visual bundle...";
            MelonCoroutines.Start(DownloadBundle(bundlePath));
        }

        private static System.Collections.IEnumerator DownloadBundle(string bundlePath)
        {
            UnityWebRequest request = UnityWebRequest.Get(BundleDownloadUrl);
            request.timeout = BundleDownloadTimeoutSeconds;
            yield return request.SendWebRequest();

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _status = "Could not download the Peg Sparks visual bundle.";
                    Log.Warning("[PegSparks] Bundle download failed: " + request.error);
                    yield break;
                }

                byte[] data = request.downloadHandler.data;
                if (data == null || data.Length == 0 || data.Length > MaxBundleDownloadBytes)
                {
                    _status = "Peg Sparks bundle download was invalid.";
                    Log.Warning("[PegSparks] Bundle download was empty or exceeded the size limit.");
                    yield break;
                }

                string actualHash;
                using (SHA256 sha256 = SHA256.Create())
                    actualHash = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", string.Empty);

                if (!string.Equals(actualHash, BundleSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _status = "Peg Sparks bundle verification failed.";
                    Log.Warning($"[PegSparks] Bundle hash mismatch. expected={BundleSha256}, actual={actualHash}");
                    yield break;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(bundlePath));
                string stagingPath = bundlePath + ".download";
                File.WriteAllBytes(stagingPath, data);
                if (File.Exists(bundlePath))
                    File.Delete(bundlePath);
                File.Move(stagingPath, bundlePath);

                _status = "Peg Sparks visual bundle downloaded. Initializing...";
                Log.Msg("[PegSparks] Visual bundle downloaded and verified.");
            }
            catch (Exception ex)
            {
                _status = "Could not save the Peg Sparks visual bundle.";
                Log.Warning("[PegSparks] Bundle install failed: " + ex.Message);
            }
            finally
            {
                _bundleDownloadInProgress = false;
                request.Dispose();
            }
        }

        private static AssetBundle FindAlreadyLoadedBundle()
        {
            foreach (AssetBundle bundle in Memory.loadedBundles)
            {
                if (bundle == null)
                    continue;

                foreach (string assetName in bundle.GetAllAssetNames())
                {
                    if (assetName.EndsWith(PrefabFileName, StringComparison.OrdinalIgnoreCase))
                        return bundle;
                }
            }

            return null;
        }

        private static void RecordReplaySample(
            int pegIndex,
            Vector3 point,
            Vector3 normal,
            Vector3 tangent)
        {
            PegSparksSettings settings = Config.pegSparksSettings;
            if (_replayActive || settings?.recordInReplay != true)
                return;

            try
            {
                GameReplaySystem replay = GameReplaySystem.Instance;
                // ReplayState.Recording is zero in the current wrappers; do not
                // use a positive-state test here or all live samples are skipped.
                if (replay == null || (int)replay.State != 0)
                    return;

                float recordTime = replay.CurrentRecordTime;
                if (float.IsNaN(recordTime) || float.IsInfinity(recordTime) || recordTime < 0f)
                    return;

                if (ReplaySamples.Count >= MaxReplaySamples)
                    ReplaySamples.RemoveRange(0, Mathf.Min(128, ReplaySamples.Count));

                ReplaySamples.Add(new ReplaySample(
                    recordTime,
                    pegIndex,
                    point,
                    normal,
                    tangent));
                if (!_loggedFirstReplaySample)
                {
                    _loggedFirstReplaySample = true;
                    Log.Msg("[PegSparks] Recording native peg samples for Replay.");
                }
            }
            catch
            {
                // Replay can be torn down between a native contact and this optional recording.
            }
        }

        private static void UpdateReplayPlayback()
        {
            if (Config.pegSparksSettings?.enabled != true)
                return;

            GameReplaySystem replay;
            float playbackTime;
            try
            {
                replay = GameReplaySystem.Instance;
                if (replay == null)
                    return;
                playbackTime = replay.CurrentPlaybackTime;
            }
            catch
            {
                return;
            }

            if (float.IsNaN(playbackTime) || float.IsInfinity(playbackTime))
                return;

            bool seeked = float.IsNegativeInfinity(_lastReplayPlaybackTime) ||
                          playbackTime < _lastReplayPlaybackTime ||
                          playbackTime - _lastReplayPlaybackTime > ReplaySeekResetSeconds;
            if (seeked)
                SeekReplayPlayback(playbackTime);
            else
                AdvanceReplayPlayback(playbackTime);

            PegSparksSettings settings = Config.pegSparksSettings;
            for (int i = 0; i < PegCount; i++)
            {
                if (HasReplayLatestSample[i] &&
                    playbackTime - ReplayLatestSamples[i].Time <= ReplaySampleExpirySeconds &&
                    EnsureRig(Runtimes[i]))
                {
                    ReplaySample sample = ReplayLatestSamples[i];
                    Runtimes[i].UpdateContinuous(
                        sample.Position,
                        sample.Normal,
                        sample.Tangent,
                        settings);
                }
                else
                {
                    Runtimes[i].StopContinuous(false);
                }
            }

            _lastReplayPlaybackTime = playbackTime;
            _status = $"Replay sparks: {ReplaySamples.Count} recorded samples.";
        }

        private static void SeekReplayPlayback(float playbackTime)
        {
            StopAllEffects(true);
            Array.Clear(HasReplayLatestSample, 0, HasReplayLatestSample.Length);
            _replayCursor = 0;
            for (int i = 0; i < ReplaySamples.Count; i++)
            {
                ReplaySample sample = ReplaySamples[i];
                if (sample.Time > playbackTime + 0.006f)
                {
                    _replayCursor = i;
                    break;
                }

                ApplyReplaySample(sample);
                _replayCursor = i + 1;
            }
        }

        private static void AdvanceReplayPlayback(float playbackTime)
        {
            while (_replayCursor < ReplaySamples.Count &&
                   ReplaySamples[_replayCursor].Time <= playbackTime + 0.006f)
            {
                ApplyReplaySample(ReplaySamples[_replayCursor]);
                _replayCursor++;
            }
        }

        private static void ApplyReplaySample(ReplaySample sample)
        {
            if (sample.PegIndex < 0 || sample.PegIndex >= PegCount)
                return;

            ReplayLatestSamples[sample.PegIndex] = sample;
            HasReplayLatestSample[sample.PegIndex] = true;
        }

        private static void ResetReplayPlayback()
        {
            _replayCursor = 0;
            _lastReplayPlaybackTime = float.NegativeInfinity;
            Array.Clear(HasReplayLatestSample, 0, HasReplayLatestSample.Length);
        }

        private static void ClearReplaySamples()
        {
            ReplaySamples.Clear();
            _loggedFirstReplaySample = false;
            ResetReplayPlayback();
        }

        private static void StopAllEffects(bool immediate = false)
        {
            foreach (PegRuntime runtime in Runtimes)
                runtime.StopContinuous(immediate);
        }

        private readonly struct ReplaySample
        {
            public readonly float Time;
            public readonly int PegIndex;
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector3 Tangent;

            public ReplaySample(
                float time,
                int pegIndex,
                Vector3 position,
                Vector3 normal,
                Vector3 tangent)
            {
                Time = time;
                PegIndex = pegIndex;
                Position = position;
                Normal = normal;
                Tangent = tangent;
            }
        }

        private sealed class PegRuntime
        {
            public readonly string Name;
            public GameObject Rig;
            public VisualEffect Continuous;
            public float NextUpdateTime;
            public float TrailEndTime;
            private bool _continuousPlaying;

            public PegRuntime(string name)
            {
                Name = name;
            }

            public void CreateRig(GameObject prefab)
            {
                Rig = Object.Instantiate(prefab);
                Rig.name = "RoweMod Peg Sparks - " + Name;
                Rig.SetActive(true);
                Transform continuous = Rig.transform.Find("Continuous");
                Transform impact = Rig.transform.Find("Impact");
                Continuous = continuous != null ? continuous.GetComponent<VisualEffect>() : null;
                SetSparkColor(Continuous);
                StopContinuous(true);
                if (impact != null)
                {
                    VisualEffect impactEffect = impact.GetComponent<VisualEffect>();
                    if (impactEffect != null)
                    {
                        impactEffect.Stop();
                        impactEffect.enabled = false;
                    }
                }
            }

            public void UpdateContinuous(
                Vector3 position,
                Vector3 normal,
                Vector3 tangent,
                PegSparksSettings settings)
            {
                if (Continuous == null)
                    return;

                SetTransform(Continuous.transform, position, normal, tangent);
                SetVisualSettings(Continuous, settings);
                if (!_continuousPlaying)
                {
                    Continuous.enabled = true;
                    Continuous.Reinit();
                    Continuous.Play();
                    _continuousPlaying = true;
                }

                TrailEndTime = 0f;
            }

            public void StopContinuous(bool immediate)
            {
                NextUpdateTime = 0f;
                if (Continuous == null || !_continuousPlaying)
                    return;

                Continuous.Stop();
                _continuousPlaying = false;
                if (immediate)
                {
                    Continuous.enabled = false;
                    TrailEndTime = 0f;
                    return;
                }

                TrailEndTime = Time.unscaledTime +
                               (Config.pegSparksSettings?.trailSeconds ?? 0.3f);
            }

            public void UpdateTrail(float now)
            {
                if (!_continuousPlaying && TrailEndTime > 0f && now >= TrailEndTime)
                {
                    if (Continuous != null)
                        Continuous.enabled = false;
                    TrailEndTime = 0f;
                }

            }

            public void DestroyRig()
            {
                if (Rig != null)
                    Object.Destroy(Rig);
                Rig = null;
                Continuous = null;
                NextUpdateTime = 0f;
                TrailEndTime = 0f;
                _continuousPlaying = false;
            }

            private static void SetTransform(Transform transform, Vector3 position, Vector3 normal, Vector3 tangent)
            {
                transform.position = position;
                transform.rotation = Quaternion.LookRotation(tangent, normal);
            }

            private static void SetSparkColor(VisualEffect effect)
            {
                if (effect == null)
                    return;

                for (int i = 0; i < SparkGradientProperties.Length; i++)
                {
                    string propertyName = SparkGradientProperties[i];
                    if (!effect.HasGradient(propertyName))
                        continue;

                    effect.SetGradient(propertyName, GetHotOrangeGradient());
                    return;
                }

                for (int i = 0; i < SparkColorProperties.Length; i++)
                {
                    string propertyName = SparkColorProperties[i];
                    if (!effect.HasVector4(propertyName))
                        continue;

                    effect.SetVector4(propertyName, HotOrangeSparkColor);
                    return;
                }
            }

            private static Gradient GetHotOrangeGradient()
            {
                if (_hotOrangeGradient != null)
                    return _hotOrangeGradient;

                _hotOrangeGradient = new Gradient();
                var colors = new Il2CppStructArray<GradientColorKey>(
                    new[]
                    {
                        new GradientColorKey(new Color(2.5f, 0.65f, 0.03f), 0f),
                        new GradientColorKey(new Color(1f, 0.24f, 0.01f), 0.35f),
                        new GradientColorKey(new Color(0.35f, 0.015f, 0f), 1f)
                    });
                var alpha = new Il2CppStructArray<GradientAlphaKey>(
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 0.55f),
                        new GradientAlphaKey(0f, 1f)
                    });
                _hotOrangeGradient.SetKeys(colors, alpha);
                return _hotOrangeGradient;
            }

            private static void SetVisualSettings(VisualEffect effect, PegSparksSettings settings)
            {
                if (effect.HasFloat("Sparks Particle Spawn Rate"))
                    effect.SetFloat("Sparks Particle Spawn Rate", 180f * settings.intensity);
                if (effect.HasVector2("Spark Particle Size"))
                    effect.SetVector2(
                        "Spark Particle Size",
                        new Vector2(0.005f, 0.01f) * settings.sparkSize);
                if (effect.HasVector2("Spark Lifetime Min/Max"))
                    effect.SetVector2(
                        "Spark Lifetime Min/Max",
                        new Vector2(0.3f, 1f) * settings.sparkLifetime);
                if (effect.HasVector2("Smoke Lifetime Min/Max"))
                    effect.SetVector2(
                        "Smoke Lifetime Min/Max",
                        new Vector2(0.08f, 0.35f) * settings.sparkLifetime);
                if (effect.HasVector3("Spark Initial Velocity"))
                    effect.SetVector3(
                        "Spark Initial Velocity",
                        new Vector3(0.35f, 1.15f, -3f) * settings.sparkSpeed);
            }
        }
    }

}
