using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Video;
using rowemod.Utils;

namespace rowemod.Mods
{
    internal static class VideoTvController
    {
        private const string BundleFileName = "rowemod_video_tv";
        private const string PrefabFileName = "rowemod_video_tv.prefab";
        private const string ScreenName = "RoweMod_TV_Screen";
        private const string BundleDownloadUrl =
            "https://raw.githubusercontent.com/xrowex/RoweMod/master/Bundles/rowemod_video_tv";
        private const string BundleSha256 =
            "1D4718AB8F93B24C347FE9207581E02AD5883B4132B0DD982C94FB8F02422A15";
        private const int BundleDownloadTimeoutSeconds = 30;
        private const long MaximumBundleBytes = 4L * 1024L * 1024L;
        private const float PrepareTimeoutSeconds = 20f;

        private static readonly List<Material> RuntimeMaterials = new List<Material>();
        private static AssetBundle _bundle;
        private static GameObject _prefab;
        private static bool _bundleDownloadInProgress;
        private static GameObject _root;
        private static VideoPlayer _player;
        private static Renderer _screenRenderer;
        private static Material _screenMaterial;
        private static float _prepareStartedAt;
        private static float _playStartedAt;
        private static bool _preparing;
        private static bool _started;
        private static bool _firstFrameVisible;
        private static bool _playbackStartedSignal;
        private static bool _playbackFailedSignal;
        private static string _pendingVideoPath;
        private static string _pendingTitle;
        private static float _pendingDuration;
        private static bool _pendingHasExplicitPose;
        private static Vector3 _pendingPosition;
        private static Quaternion _pendingRotation = Quaternion.identity;
        private static double _pendingStartSeconds;
        private static double _requestedStartSeconds;
        private static string _currentTitle;
        private static string _status = "The video TV is ready when a compatible MP4 has been prepared.";
        private static string _visualStatus = "TV model has not been resolved yet.";

        public static bool IsActive => _root != null;
        public static bool IsPlaying => _root != null && (_preparing || _started);
        public static bool HasPendingVideo => !string.IsNullOrEmpty(_pendingVideoPath);
        public static string Status => _status;
        public static string VisualStatus => _visualStatus;

        public static bool EnsureVisual()
        {
            if (_prefab != null)
                return true;

            string path = Path.Combine(Memory.bundlesFolderPath, BundleFileName);
            if (File.Exists(path))
            {
                string hash = ComputeFileSha256(path);
                if (string.Equals(hash, BundleSha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (_bundle == null)
                        _bundle = AssetBundle.LoadFromFile(path);
                    if (_bundle != null)
                    {
                        string prefabPath = null;
                        string[] assetNames = _bundle.GetAllAssetNames();
                        for (int i = 0; i < assetNames.Length; i++)
                        {
                            if (assetNames[i].EndsWith(PrefabFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                prefabPath = assetNames[i];
                                break;
                            }
                        }

                        _prefab = prefabPath == null ? null : _bundle.LoadAsset<GameObject>(prefabPath);
                        if (_prefab != null)
                        {
                            _visualStatus = "Verified TV model is ready on every map.";
                            Log.Msg($"[VideoTV] Loaded verified TV bundle ({hash}).");
                            return true;
                        }
                    }

                    _visualStatus = "The verified TV bundle did not expose its prefab.";
                    Log.Warning("[VideoTV] Verified TV bundle did not expose its prefab.");
                    return false;
                }

                Log.Warning($"[VideoTV] Replacing stale TV bundle. expected={BundleSha256}, actual={hash}");
            }

            if (!_bundleDownloadInProgress)
            {
                _bundleDownloadInProgress = true;
                _visualStatus = "Downloading and verifying the TV model...";
                MelonCoroutines.Start(DownloadBundle(path));
            }
            return false;
        }

        public static bool BeginVideo(string videoPath, string title, float expectedDuration)
        {
            return BeginVideoInternal(
                videoPath,
                title,
                expectedDuration,
                false,
                Vector3.zero,
                Quaternion.identity,
                0d);
        }

        public static bool BeginVideoAt(
            string videoPath,
            string title,
            float expectedDuration,
            Vector3 position,
            Quaternion rotation,
            double startSeconds)
        {
            return BeginVideoInternal(
                videoPath,
                title,
                expectedDuration,
                true,
                position,
                rotation,
                Math.Max(0d, startSeconds));
        }

        public static bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (_root == null)
                return false;
            position = _root.transform.position;
            rotation = _root.transform.rotation;
            return true;
        }

        public static void SeekTo(double seconds)
        {
            if (_player == null || !_player.isPrepared)
                return;
            try
            {
                double duration = _player.length;
                double target = Math.Max(0d, seconds);
                if (duration > 0.05d)
                    target = Math.Min(target, duration - 0.05d);
                _player.time = target;
                Log.Msg($"[VideoTV] Synchronized decoder to {target:0.###}s.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[VideoTV] Could not seek synchronized video: {ex.Message}");
            }
        }

        private static bool BeginVideoInternal(
            string videoPath,
            string title,
            float expectedDuration,
            bool hasExplicitPose,
            Vector3 position,
            Quaternion rotation,
            double startSeconds)
        {
            _playbackStartedSignal = false;
            _playbackFailedSignal = false;
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                _status = "The prepared TV video file is missing.";
                return false;
            }
            if (Memory.chassisRb == null && Memory.rMbCharacter == null)
            {
                _status = "Enter a gameplay map before placing the TV.";
                return false;
            }

            ClearPending();
            StopPlaybackAndVisual();
            if (!EnsureVisual())
            {
                _pendingVideoPath = Path.GetFullPath(videoPath);
                _pendingTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(videoPath) : title.Trim();
                _pendingDuration = expectedDuration;
                _pendingHasExplicitPose = hasExplicitPose;
                _pendingPosition = position;
                _pendingRotation = rotation;
                _pendingStartSeconds = startSeconds;
                _status = "Video is ready; waiting for the verified TV model download.";
                return true;
            }

            return StartPreparedVideo(
                videoPath,
                title,
                expectedDuration,
                hasExplicitPose,
                position,
                rotation,
                startSeconds);
        }

        public static void Update()
        {
            if (!string.IsNullOrEmpty(_pendingVideoPath) && EnsureVisual())
            {
                string path = _pendingVideoPath;
                string title = _pendingTitle;
                float duration = _pendingDuration;
                bool hasExplicitPose = _pendingHasExplicitPose;
                Vector3 position = _pendingPosition;
                Quaternion rotation = _pendingRotation;
                double startSeconds = _pendingStartSeconds;
                ClearPending();
                StartPreparedVideo(path, title, duration, hasExplicitPose, position, rotation, startSeconds);
            }

            if (_player == null)
                return;

            if (_preparing)
            {
                if (_player.isPrepared)
                {
                    _preparing = false;
                    _started = true;
                    _firstFrameVisible = false;
                    _playStartedAt = Time.unscaledTime;
                    SeekTo(_requestedStartSeconds);
                    _player.Play();
                    _status = $"Decoder ready; waiting for the first visible frame of {_currentTitle}...";
                    Log.Msg($"[VideoTV] Decoder prepared; requested playback: {_currentTitle}");
                }
                else if (Time.unscaledTime - _prepareStartedAt > PrepareTimeoutSeconds)
                {
                    _preparing = false;
                    _started = false;
                    _playbackFailedSignal = true;
                    _status = "The TV timed out while preparing this video. Try a different YouTube video.";
                    Log.Warning("[VideoTV] VideoPlayer preparation timed out.");
                }
                return;
            }

            if (!_started)
                return;

            if (!_firstFrameVisible)
            {
                Texture decodedTexture = null;
                long decodedFrame = -1L;
                try
                {
                    decodedTexture = _player.texture;
                    decodedFrame = _player.frame;
                }
                catch { }

                if (decodedTexture != null && decodedTexture.width > 1 && decodedTexture.height > 1 && decodedFrame >= 0L)
                {
                    BindScreenTexture(decodedTexture);
                    _firstFrameVisible = true;
                    _playbackStartedSignal = true;
                    _status = $"Playing {_currentTitle} on the placed TV. Radio audio remains separate.";
                    Log.Msg(
                        $"[VideoTV] First frame is visible: title={_currentTitle}, frame={decodedFrame}, " +
                        $"texture={decodedTexture.width}x{decodedTexture.height}, playing={_player.isPlaying}.");
                }
                else if (Time.unscaledTime - _playStartedAt > PrepareTimeoutSeconds)
                {
                    _started = false;
                    _playbackFailedSignal = true;
                    _status = "The TV decoder started but did not produce a visible frame.";
                    Log.Warning(
                        $"[VideoTV] No visible frame after {PrepareTimeoutSeconds:0}s: " +
                        $"frame={decodedFrame}, texture={(decodedTexture == null ? "null" : decodedTexture.width + "x" + decodedTexture.height)}, " +
                        $"playing={_player.isPlaying}, prepared={_player.isPrepared}, url={_player.url}");
                }
                return;
            }

            if (Time.unscaledTime - _playStartedAt < 1f)
                return;

            try
            {
                if (!_player.isPlaying && _player.frameCount > 0 &&
                    _player.frame >= (long)_player.frameCount - 2L)
                {
                    _started = false;
                    _status = $"{_currentTitle} finished. The TV remains placed on its final frame.";
                }
            }
            catch
            {
                // Some platform decoders do not expose frameCount. Playback remains usable.
            }
        }

        public static void Stop()
        {
            bool wasWaiting = _preparing || HasPendingVideo;
            ClearPending();
            StopPlaybackAndVisual();
            if (wasWaiting)
                _playbackFailedSignal = true;
            _status = "TV stopped and removed. Radio and dance playback are unchanged.";
        }

        public static bool ConsumePlaybackStarted()
        {
            bool value = _playbackStartedSignal;
            _playbackStartedSignal = false;
            return value;
        }

        public static bool ConsumePlaybackFailed()
        {
            bool value = _playbackFailedSignal;
            _playbackFailedSignal = false;
            return value;
        }

        public static void OnSceneInitialized()
        {
            StopPlaybackAndVisual();
            ClearPending();
            _playbackStartedSignal = false;
            _playbackFailedSignal = false;
            _status = "Paste a YouTube link and choose Play Radio + TV in a gameplay map.";
        }

        private static bool StartPreparedVideo(
            string videoPath,
            string title,
            float expectedDuration,
            bool hasExplicitPose,
            Vector3 position,
            Quaternion rotation,
            double startSeconds)
        {
            try
            {
                if (_prefab == null || !File.Exists(videoPath))
                    return false;

                _root = UnityEngine.Object.Instantiate(_prefab);
                _root.name = "RoweMod Placed Video TV";
                _root.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(_root);
                SetLayerRecursively(_root, 0);
                PrepareBodyMaterials(_root);

                Transform screenTransform = FindDescendant(_root.transform, ScreenName);
                Renderer screenRenderer = screenTransform == null ? null : screenTransform.GetComponent<Renderer>();
                if (screenRenderer == null)
                    throw new InvalidOperationException("the TV prefab has no dedicated screen renderer");

                _screenRenderer = screenRenderer;
                _screenMaterial = PrepareScreenMaterial(screenRenderer, Texture2D.blackTexture);

                _player = _root.AddComponent<VideoPlayer>();
                _player.playOnAwake = false;
                _player.waitForFirstFrame = true;
                _player.skipOnDrop = true;
                _player.isLooping = false;
                _player.source = VideoSource.Url;
                // APIOnly exposes the decoder's native texture directly. This avoids the extra
                // RenderTexture copy that remained black on the game's HDRP player build.
                _player.renderMode = VideoRenderMode.APIOnly;
                _player.audioOutputMode = VideoAudioOutputMode.None;
                _player.url = new Uri(Path.GetFullPath(videoPath)).AbsoluteUri;
                if (hasExplicitPose)
                {
                    _root.transform.SetPositionAndRotation(position, rotation);
                    _root.SetActive(true);
                }
                else
                {
                    PlaceNearLiveRider();
                }

                _currentTitle = string.IsNullOrWhiteSpace(title)
                    ? Path.GetFileNameWithoutExtension(videoPath)
                    : title.Trim();
                _prepareStartedAt = Time.unscaledTime;
                _preparing = true;
                _started = false;
                _firstFrameVisible = false;
                _requestedStartSeconds = Math.Max(0d, startSeconds);
                _player.Prepare();
                _status = $"Preparing {_currentTitle} on the placed TV...";
                Log.Msg(
                    $"[VideoTV] Placed TV and preparing video: path={videoPath}, " +
                    $"expectedDuration={expectedDuration:0.##}s, offset={_requestedStartSeconds:0.###}s, " +
                    $"position={_root.transform.position}");
                return true;
            }
            catch (Exception ex)
            {
                _status = "Could not start the TV: " + CleanError(ex.Message);
                Log.Warning("[VideoTV] " + _status);
                StopPlaybackAndVisual();
                _playbackFailedSignal = true;
                return false;
            }
        }

        private static void PlaceNearLiveRider()
        {
            Transform anchor = Memory.chassisRb != null
                ? Memory.chassisRb.transform
                : Memory.rMbCharacter.transform;
            Vector3 anchorPosition = Memory.chassisRb != null
                ? Memory.chassisRb.position
                : anchor.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.01f)
                flatForward = Vector3.forward;

            Vector3 target = anchorPosition + flatForward * 2.6f;
            bool grounded = UnityEngine.Physics.Raycast(
                target + Vector3.up * 2.5f,
                Vector3.down,
                out RaycastHit hit,
                6f,
                UnityEngine.Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            target.y = grounded ? hit.point.y : anchorPosition.y;
            _root.transform.SetPositionAndRotation(target, Quaternion.LookRotation(-flatForward, Vector3.up));
            _root.SetActive(true);
            Log.Msg(
                $"[VideoTV] Placed TV 2.6m in front of live rider: anchor={anchor.name}, " +
                $"anchorPosition={anchorPosition}, target={target}, grounded={grounded}.");
        }

        private static void PrepareBodyMaterials(GameObject visual)
        {
            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                return;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.name.Equals(ScreenName, StringComparison.Ordinal))
                    continue;

                Material source = renderer.sharedMaterial;
                Color color = new Color(0.035f, 0.038f, 0.043f, 1f);
                if (source != null)
                {
                    if (source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
                    else if (source.HasProperty("_Color")) color = source.color;
                }

                Material material = new Material(shader)
                {
                    name = "RoweMod TV Runtime Body",
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Geometry
                };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
                if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 0f);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
                renderer.material = material;
                renderer.enabled = true;
                RuntimeMaterials.Add(material);
            }
        }

        private static Material PrepareScreenMaterial(Renderer renderer, Texture texture)
        {
            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("HDRP/Lit");
            if (shader == null)
                throw new InvalidOperationException("no compatible runtime screen shader is available");

            Material material = new Material(shader)
            {
                name = "RoweMod TV Runtime Screen",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture,
                renderQueue = (int)RenderQueue.Geometry
            };
            if (material.HasProperty("_UnlitColorMap")) material.SetTexture("_UnlitColorMap", texture);
            if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_EmissiveColorMap")) material.SetTexture("_EmissiveColorMap", texture);
            if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            if (material.HasProperty("_DoubleSidedEnable")) material.SetFloat("_DoubleSidedEnable", 1f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", (float)CullMode.Off);
            if (material.HasProperty("_CullModeForward")) material.SetFloat("_CullModeForward", (float)CullMode.Off);
            material.doubleSidedGI = true;
            renderer.material = material;
            renderer.enabled = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            RuntimeMaterials.Add(material);
            return material;
        }

        private static void BindScreenTexture(Texture texture)
        {
            if (_screenMaterial == null || texture == null)
                return;

            _screenMaterial.mainTexture = texture;
            if (_screenMaterial.HasProperty("_UnlitColorMap")) _screenMaterial.SetTexture("_UnlitColorMap", texture);
            if (_screenMaterial.HasProperty("_BaseColorMap")) _screenMaterial.SetTexture("_BaseColorMap", texture);
            if (_screenMaterial.HasProperty("_BaseMap")) _screenMaterial.SetTexture("_BaseMap", texture);
            if (_screenMaterial.HasProperty("_EmissiveColorMap")) _screenMaterial.SetTexture("_EmissiveColorMap", texture);
            if (_screenRenderer != null)
                _screenRenderer.enabled = true;
        }

        private static void StopPlaybackAndVisual()
        {
            _preparing = false;
            _started = false;
            _firstFrameVisible = false;
            try
            {
                if (_player != null)
                    _player.Stop();
            }
            catch { }
            _player = null;
            _screenRenderer = null;
            _screenMaterial = null;

            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;

            for (int i = 0; i < RuntimeMaterials.Count; i++)
            {
                if (RuntimeMaterials[i] != null)
                    UnityEngine.Object.Destroy(RuntimeMaterials[i]);
            }
            RuntimeMaterials.Clear();
            _currentTitle = null;
            _requestedStartSeconds = 0d;
        }

        private static void ClearPending()
        {
            _pendingVideoPath = null;
            _pendingTitle = null;
            _pendingDuration = 0f;
            _pendingHasExplicitPose = false;
            _pendingPosition = Vector3.zero;
            _pendingRotation = Quaternion.identity;
            _pendingStartSeconds = 0d;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name.Equals(name, StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;
            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }

        private static string ComputeFileSha256(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                using SHA256 sha256 = SHA256.Create();
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                Log.Warning("[VideoTV] Could not hash the TV bundle: " + ex.Message);
                return string.Empty;
            }
        }

        private static IEnumerator DownloadBundle(string bundlePath)
        {
            UnityWebRequest request = UnityWebRequest.Get(BundleDownloadUrl);
            request.timeout = BundleDownloadTimeoutSeconds;
            yield return request.SendWebRequest();
            bool installed = false;
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _visualStatus = "TV model download failed: " + request.error;
                    Log.Warning("[VideoTV] TV bundle download failed: " + request.error);
                    FailPendingVideo();
                    yield break;
                }

                byte[] data = request.downloadHandler.data;
                if (data == null || data.Length == 0 || data.LongLength > MaximumBundleBytes)
                {
                    _visualStatus = "TV model download was empty or too large.";
                    FailPendingVideo();
                    yield break;
                }

                string actualHash;
                using (SHA256 sha256 = SHA256.Create())
                    actualHash = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", string.Empty);
                if (!string.Equals(actualHash, BundleSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _visualStatus = "TV model download failed verification.";
                    Log.Warning($"[VideoTV] TV bundle hash mismatch. expected={BundleSha256}, actual={actualHash}");
                    FailPendingVideo();
                    yield break;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(bundlePath));
                string stagingPath = bundlePath + ".download";
                File.WriteAllBytes(stagingPath, data);
                if (File.Exists(bundlePath))
                    File.Delete(bundlePath);
                File.Move(stagingPath, bundlePath);
                installed = true;
            }
            catch (Exception ex)
            {
                _visualStatus = "Could not install the TV model.";
                Log.Warning("[VideoTV] TV bundle install failed: " + ex.Message);
                FailPendingVideo();
            }
            finally
            {
                _bundleDownloadInProgress = false;
                request.Dispose();
            }

            if (installed && EnsureVisual())
                Log.Msg("[VideoTV] TV bundle downloaded, verified, and initialized.");
        }

        private static string CleanError(string value)
        {
            string clean = (value ?? "unknown error").Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= 220 ? clean : clean.Substring(0, 217) + "...";
        }

        private static void FailPendingVideo()
        {
            if (!HasPendingVideo)
                return;
            ClearPending();
            _playbackFailedSignal = true;
        }
    }
}
