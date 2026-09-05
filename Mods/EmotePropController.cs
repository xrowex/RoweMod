using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Il2CppFMODUnity;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using GameReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;

namespace rowemod.Mods
{
    /// <summary>
    /// Small local-only props for authored emotes. They use normal opaque, depth-tested
    /// geometry so a prop is occluded by the rider and bike instead of drawing over them.
    /// </summary>
    internal static class EmotePropController
    {
        private enum PropKind
        {
            None,
            Cigarette,
            Firecracker,
            Radio
        }

        private sealed class ReplayRadioSegment
        {
            public string Path;
            public float StartTime;
            public float EndTime;
        }

        private static PropKind _kind;
        private static GameObject _root;
        private static Transform _anchorBase;
        private static Transform _anchorTip;
        private static float _startedAt;
        private static float _releaseAt;
        private static float _destroyAt;
        private static bool _sawNativeActive;
        private static bool _detached;
        private static bool _exploded;
        private static float _explodeAt;
        private static readonly List<Material> Materials = new List<Material>();
        private static Il2CppFMOD.Sound _fireSound;
        private static bool _fireSoundReady;
        private static Il2CppFMOD.Sound _radioSound;
        private static Il2CppFMOD.Channel _radioChannel;
        private static string _radioChannelPath;
        private static bool _radioChannelIsReplay;
        private static bool _radioPlaybackEnded;
        private static int _activeRadioSegmentIndex = -1;
        private const string FireSoundFileName = "SFX_Firecracker_Pop.wav";
        private const float ReplaySampleInterval = 1f / 30f;
        private const float ReplaySeekThreshold = 0.35f;
        private const long ReplayDriftCorrectionMs = 1000L;
        private const float ReplayMinimumPlaybackPitch = 0.1f;
        private const float ReplayMaximumPlaybackPitch = 4f;
        private const float RadioMinimumDistance = 0.75f;
        private const float RadioMaximumDistance = 24f;
        private const string RadioBundleFileName = "rowemod_native_boombox";
        private const string RadioPrefabFileName = "rowemod_native_boombox.prefab";
        private const string RadioBundleDownloadUrl =
            "https://raw.githubusercontent.com/xrowex/RoweMod/master/Bundles/rowemod_native_boombox";
        private const string RadioBundleSha256 =
            "3D05C12B29633116791AA44E6A58A1EBAF33939793032A77C812E99B0F989DB0";
        private const int RadioBundleDownloadTimeoutSeconds = 30;
        private const long MaximumRadioBundleBytes = 16L * 1024L * 1024L;
        private const int MaxReplaySamples = 12000;
        private static readonly List<ReplayPropSample> ReplaySamples = new List<ReplayPropSample>(2048);
        private static readonly List<ReplayRadioSegment> ReplayRadioSegments = new List<ReplayRadioSegment>();
        private static bool _replayActive;
        private static float _lastRecordedReplayTime = float.NegativeInfinity;
        private static float _lastReplayPlaybackTime = float.NegativeInfinity;
        private static bool _replayWasScrubbing;
        private static bool _radioChannelPaused;
        private static float _radioReplayPitch = 1f;
        private static bool _loggedReplayRecording;
        private static string _testStatus = "Firecracker test has not run yet.";
        private static string _radioVisualStatus = "Native boombox has not been resolved yet.";
        private static AssetBundle _radioVisualBundle;
        private static GameObject _bundledBoomboxPrefab;
        private static bool _radioBundleDownloadInProgress;

        public static string TestStatus => _testStatus;
        public static string RadioVisualStatus => _radioVisualStatus;
        public static bool IsRadioPlaying => _kind == PropKind.Radio && _root != null && !_replayActive;

        public static bool EnsureRadioVisual()
        {
            if (_bundledBoomboxPrefab != null)
                return true;

            string path = Path.Combine(Memory.bundlesFolderPath, RadioBundleFileName);
            if (File.Exists(path))
            {
                string hash = ComputeFileSha256(path);
                if (string.Equals(hash, RadioBundleSha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (_radioVisualBundle == null)
                        _radioVisualBundle = AssetBundle.LoadFromFile(path);
                    if (_radioVisualBundle != null)
                    {
                        string prefabPath = null;
                        string[] assetNames = _radioVisualBundle.GetAllAssetNames();
                        for (int i = 0; i < assetNames.Length; i++)
                        {
                            if (assetNames[i].EndsWith(RadioPrefabFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                prefabPath = assetNames[i];
                                break;
                            }
                        }

                        _bundledBoomboxPrefab = prefabPath == null
                            ? null
                            : _radioVisualBundle.LoadAsset<GameObject>(prefabPath);
                        if (_bundledBoomboxPrefab != null)
                        {
                            _radioVisualStatus = "Native BMX Streets boombox bundle ready on every map.";
                            Log.Msg($"[EmoteProps] Loaded verified native boombox bundle ({hash}).");
                            return true;
                        }
                    }

                    _radioVisualStatus = "The native boombox bundle could not be loaded.";
                    Log.Warning("[EmoteProps] Verified native boombox bundle did not expose its prefab.");
                    return false;
                }

                Log.Warning($"[EmoteProps] Replacing stale native boombox bundle. expected={RadioBundleSha256}, actual={hash}");
            }

            if (!_radioBundleDownloadInProgress)
            {
                _radioBundleDownloadInProgress = true;
                _radioVisualStatus = "Downloading the native boombox model for all maps...";
                MelonCoroutines.Start(DownloadRadioBundle(path));
            }
            return false;
        }

        public static void SetRadioVolume(float volume)
        {
            Config.replaySettings.radioVolume = Mathf.Clamp01(volume);
            if (!_radioChannel.hasHandle())
                return;

            try
            {
                _radioChannel.setVolume(Config.replaySettings.radioVolume);
            }
            catch (Exception ex)
            {
                Log.Warning($"[EmoteProps] Could not change radio volume: {ex.Message}");
            }
        }

        public static bool BeginRadioTrack(string audioPath, float expectedDuration)
        {
            return BeginRadioTrackInternal(
                audioPath,
                expectedDuration,
                0u,
                false,
                Vector3.zero,
                Quaternion.identity,
                Config.replaySettings.multiplayerMediaEnabled);
        }

        public static bool BeginRadioTrackAt(
            string audioPath,
            float expectedDuration,
            double startSeconds,
            Vector3 position,
            Quaternion rotation,
            bool kinematic)
        {
            uint positionMs = startSeconds <= 0d
                ? 0u
                : (uint)Math.Min(uint.MaxValue, Math.Round(startSeconds * 1000d));
            return BeginRadioTrackInternal(
                audioPath,
                expectedDuration,
                positionMs,
                true,
                position,
                rotation,
                kinematic);
        }

        public static bool TryGetLiveRadioPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (_kind != PropKind.Radio || _root == null || _replayActive)
                return false;
            position = _root.transform.position;
            rotation = _root.transform.rotation;
            return true;
        }

        private static bool BeginRadioTrackInternal(
            string audioPath,
            float expectedDuration,
            uint positionMs,
            bool hasExplicitPose,
            Vector3 position,
            Quaternion rotation,
            bool kinematic)
        {
            Stop();
            if (_replayActive || Memory.rMbCharacter == null || string.IsNullOrWhiteSpace(audioPath) ||
                !File.Exists(audioPath) || !EnsureRadioVisual())
                return false;

            _kind = PropKind.Radio;
            _root = new GameObject("RoweMod Emote Prop - Radio");
            _root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_root);
            if (!BuildRadio())
            {
                Stop();
                return false;
            }
            if (hasExplicitPose)
                _root.transform.SetPositionAndRotation(position, rotation);
            else
                PlaceRadioNearRider();
            ConfigureLiveRadioPhysics(kinematic);
            if (!TryStartRadioAudio(audioPath, positionMs, false))
            {
                Stop();
                return false;
            }

            _startedAt = Time.unscaledTime;
            _sawNativeActive = true;
            _radioPlaybackEnded = false;
            try
            {
                if (_radioSound.hasHandle() &&
                    _radioSound.getLength(out uint lengthMs, Il2CppFMOD.TIMEUNIT.MS) == Il2CppFMOD.RESULT.OK &&
                    lengthMs > 0u)
                    expectedDuration = lengthMs / 1000f;
            }
            catch { }
            RecordReplaySample(true, false, true);
            float recordTime = TryGetRecordTime();
            if (recordTime >= 0f)
            {
                _activeRadioSegmentIndex = ReplayRadioSegments.Count;
                ReplayRadioSegments.Add(new ReplayRadioSegment
                {
                    Path = System.IO.Path.GetFullPath(audioPath),
                    StartTime = recordTime,
                    EndTime = recordTime + Mathf.Max(0.1f, expectedDuration)
                });
            }
            Log.Msg(
                $"[EmoteProps] Started 3D radio track: {audioPath}, offsetMs={positionMs}, " +
                $"kinematic={kinematic}, position={_root.transform.position}");
            return true;
        }

        public static bool ConsumeRadioPlaybackEnded()
        {
            bool value = _radioPlaybackEnded;
            _radioPlaybackEnded = false;
            return value;
        }

        public static void StopRadio()
        {
            if (_kind == PropKind.Radio)
                Stop();
        }

        public static void StopEmoteProp()
        {
            if (_kind != PropKind.Radio)
                Stop();
        }

        public static void Begin(string clipName, AnimationClip clip, bool looping)
        {
            if (_replayActive)
                return;

            PropKind requestedKind;
            if (clipName != null && clipName.IndexOf("Smoking_Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                requestedKind = PropKind.Cigarette;
            else if (clipName != null && clipName.IndexOf("Firecracker_Throw", StringComparison.OrdinalIgnoreCase) >= 0)
                requestedKind = PropKind.Firecracker;
            else
                return;

            Stop();
            _kind = requestedKind;

            if (!ResolveHandAnchors())
            {
                _kind = PropKind.None;
                Log.Warning($"[EmoteProps] Could not resolve the local rider's right-hand finger bones for {clipName}.");
                return;
            }

            _root = new GameObject($"RoweMod Emote Prop - {_kind}");
            _root.hideFlags = HideFlags.HideAndDontSave;
            if (_kind == PropKind.Cigarette)
                BuildCigarette();
            else
                BuildFirecracker();

            _startedAt = Time.unscaledTime;
            float length = Mathf.Max(0.25f, clip == null ? 1f : clip.length);
            _releaseAt = _kind == PropKind.Firecracker
                ? _startedAt + length * 0.72f
                : float.PositiveInfinity;
            _destroyAt = float.PositiveInfinity;
            _sawNativeActive = false;
            _detached = false;
            _exploded = false;
            _explodeAt = float.PositiveInfinity;
            UpdateAttachedPose();
            RecordReplaySample(true, false, true);

            Log.Msg($"[EmoteProps] Attached {_kind} for {clipName}; loop={looping}, clipLength={length:0.###}s.");
        }

        public static void Update(bool nativeActive)
        {
            if (_replayActive)
            {
                UpdateReplayPlayback();
                return;
            }

            if (_root == null)
                return;

            if (_kind == PropKind.Radio)
            {
                UpdateLiveRadio();
                return;
            }

            float now = Time.unscaledTime;
            if (_detached)
            {
                if (!_exploded && now >= _explodeAt)
                    ExplodeFirecracker();
                if (_root != null && !_exploded)
                    RecordReplaySample(true, false, false);
                if (now >= _destroyAt)
                    Stop();
                return;
            }

            UpdateAttachedPose();
            RecordReplaySample(true, false, false);
            if (nativeActive)
                _sawNativeActive = true;

            if (_kind == PropKind.Firecracker && _sawNativeActive && now >= _releaseAt)
            {
                ReleaseFirecracker();
                return;
            }

            if (!_sawNativeActive)
            {
                if (now - _startedAt > 1f)
                    Stop();
                return;
            }

            if (!nativeActive)
                Stop();
        }

        public static void Stop()
        {
            if (!_replayActive && _root != null && _root.activeSelf)
            {
                RecordReplaySample(false, false, true);
                if (_kind == PropKind.Radio && _activeRadioSegmentIndex >= 0 &&
                    _activeRadioSegmentIndex < ReplayRadioSegments.Count)
                {
                    float endTime = TryGetRecordTime();
                    if (endTime >= ReplayRadioSegments[_activeRadioSegmentIndex].StartTime)
                        ReplayRadioSegments[_activeRadioSegmentIndex].EndTime = endTime;
                }
            }

            StopRadioAudio();

            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            for (int i = 0; i < Materials.Count; i++)
            {
                if (Materials[i] != null)
                    UnityEngine.Object.Destroy(Materials[i]);
            }
            Materials.Clear();

            _kind = PropKind.None;
            _root = null;
            _anchorBase = null;
            _anchorTip = null;
            _startedAt = 0f;
            _releaseAt = 0f;
            _destroyAt = 0f;
            _sawNativeActive = false;
            _detached = false;
            _exploded = false;
            _explodeAt = 0f;
            _activeRadioSegmentIndex = -1;
        }

        public static void Release()
        {
            Stop();
            ClearReplaySamples();
        }

        public static void OnReplayOpened()
        {
            _replayActive = true;
            Stop();
            _lastReplayPlaybackTime = float.NegativeInfinity;
            _replayWasScrubbing = false;
            Log.Msg($"[EmoteProps] Replay prop track armed ({ReplaySamples.Count} samples).");
        }

        public static void OnReplayClosed()
        {
            Stop();
            _replayActive = false;
            _lastReplayPlaybackTime = float.NegativeInfinity;
            _replayWasScrubbing = false;
        }

        public static bool TestFirecrackerFx()
        {
            Vector3 position = Memory.rMbCharacter == null
                ? Vector3.zero
                : Memory.rMbCharacter.transform.position +
                  Memory.rMbCharacter.transform.forward * 1.5f +
                  Vector3.up * 1.1f;
            Vector3 direction = Memory.rMbCharacter == null
                ? Vector3.forward
                : Memory.rMbCharacter.transform.forward;

            bool sparks = PegSparks.PlayEmoteBurst(position, direction);
            bool sound = TryPlayFireSound(position);
            _testStatus = $"Firecracker test: sparks={(sparks ? "played" : "unavailable")}, " +
                          $"FMOD Core WAV={(sound ? "played" : "failed")}.";
            Log.Msg("[EmoteProps] " + _testStatus);
            return sparks || sound;
        }

        private static bool ResolveHandAnchors()
        {
            if (Memory.rMbCharacter == null)
                return false;

            Animator[] animators = Memory.rMbCharacter.GetComponentsInChildren<Animator>(true);
            if (animators == null)
                return false;

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || !animator.isHuman)
                    continue;

                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand == null)
                    continue;

                Transform proximal = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                Transform distal = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
                _anchorBase = proximal ?? hand;
                _anchorTip = distal ?? animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate) ?? hand;
                return true;
            }

            return false;
        }

        private static void UpdateAttachedPose()
        {
            if (_root == null || _anchorBase == null || _anchorTip == null)
                return;

            Vector3 direction = _anchorTip.position - _anchorBase.position;
            if (direction.sqrMagnitude < 0.000001f)
                direction = _anchorTip.forward;
            direction.Normalize();

            _root.transform.position = _anchorTip.position + direction * 0.008f;
            _root.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        }

        private static void BuildCigarette()
        {
            CreateSegment("Paper", new Vector3(0f, 0.027f, 0f), new Vector3(0.0042f, 0.027f, 0.0042f), new Color(0.92f, 0.92f, 0.88f, 1f));
            CreateSegment("Filter", new Vector3(0f, -0.009f, 0f), new Vector3(0.0044f, 0.009f, 0.0044f), new Color(0.68f, 0.34f, 0.12f, 1f));
            CreateTip("Ember", new Vector3(0f, 0.057f, 0f), 0.0055f, new Color(1f, 0.18f, 0.02f, 1f));
        }

        private static void BuildFirecracker()
        {
            CreateSegment("Body", new Vector3(0f, 0.055f, 0f), new Vector3(0.017f, 0.055f, 0.017f), new Color(0.75f, 0.025f, 0.02f, 1f));
            CreateSegment("Band Top", new Vector3(0f, 0.102f, 0f), new Vector3(0.018f, 0.005f, 0.018f), new Color(0.93f, 0.72f, 0.12f, 1f));
            CreateSegment("Band Bottom", new Vector3(0f, 0.008f, 0f), new Vector3(0.018f, 0.005f, 0.018f), new Color(0.93f, 0.72f, 0.12f, 1f));
            CreateTip("Fuse", new Vector3(0f, 0.116f, 0f), 0.006f, new Color(1f, 0.24f, 0.02f, 1f));
        }

        private static bool BuildRadio()
        {
            GameObject nativeVisual = FindNativeBoomboxVisual();
            if (nativeVisual != null)
            {
                GameObject visual = UnityEngine.Object.Instantiate(nativeVisual);
                visual.name = "BMX Streets Prop_Boombox Visual";
                visual.transform.SetParent(_root.transform, false);
                visual.transform.localPosition = nativeVisual.transform.localPosition;
                visual.transform.localRotation = nativeVisual.transform.localRotation;
                visual.transform.localScale = nativeVisual.transform.localScale;
                SetLayerRecursively(visual, 0);
                visual.SetActive(true);
                PrepareBundledBoomboxMaterials(visual);
                _radioVisualStatus = "Using the native BMX Streets Prop_Boombox visual with its original rigidbody dimensions.";
                Log.Msg("[EmoteProps] Built the radio from the bundled native Prop_Boombox game mesh.");
                return true;
            }

            _radioVisualStatus = "Native boombox model is unavailable; radio playback was not started.";
            return false;
        }

        private static GameObject FindNativeBoomboxVisual()
        {
            return EnsureRadioVisual() ? _bundledBoomboxPrefab : null;
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
                Log.Warning("[EmoteProps] Could not hash the native boombox bundle: " + ex.Message);
                return string.Empty;
            }
        }

        private static System.Collections.IEnumerator DownloadRadioBundle(string bundlePath)
        {
            UnityWebRequest request = UnityWebRequest.Get(RadioBundleDownloadUrl);
            request.timeout = RadioBundleDownloadTimeoutSeconds;
            yield return request.SendWebRequest();
            bool installed = false;
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _radioVisualStatus = "Native boombox download failed: " + request.error;
                    Log.Warning("[EmoteProps] Native boombox download failed: " + request.error);
                    yield break;
                }

                byte[] data = request.downloadHandler.data;
                if (data == null || data.Length == 0 || data.Length > MaximumRadioBundleBytes)
                {
                    _radioVisualStatus = "Native boombox download was empty or too large.";
                    yield break;
                }

                string actualHash;
                using (SHA256 sha256 = SHA256.Create())
                    actualHash = BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", string.Empty);
                if (!string.Equals(actualHash, RadioBundleSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _radioVisualStatus = "Native boombox download failed verification.";
                    Log.Warning($"[EmoteProps] Native boombox hash mismatch. expected={RadioBundleSha256}, actual={actualHash}");
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
                _radioVisualStatus = "Could not install the native boombox model.";
                Log.Warning("[EmoteProps] Native boombox install failed: " + ex.Message);
            }
            finally
            {
                _radioBundleDownloadInProgress = false;
                request.Dispose();
            }

            if (installed && EnsureRadioVisual())
                Log.Msg("[EmoteProps] Native boombox bundle downloaded, verified, and initialized.");
        }

        private static void ConfigureLiveRadioPhysics(bool kinematic)
        {
            if (_root == null || _replayActive)
                return;

            // These are the dimensions and body values used by the game's Prop_Boombox.
            // The replay copy stays transform-driven and intentionally receives no Rigidbody.
            BoxCollider collider = _root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.1924896f, -0.02778762f);
            collider.size = new Vector3(0.6633263f, 0.3659897f, 0.2070922f);

            Rigidbody body = _root.AddComponent<Rigidbody>();
            body.mass = 2f;
            body.drag = 0.1f;
            body.angularDrag = 0.1f;
            body.useGravity = !kinematic;
            body.isKinematic = kinematic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Log.Msg(
                $"[EmoteProps] Native boombox physics enabled: mass=2kg, gravity={!kinematic}, " +
                $"kinematic={kinematic}, continuous collision.");
        }

        private static void PrepareBundledBoomboxMaterials(GameObject visual)
        {
            if (visual == null)
                return;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.enabled = true;
                Material source = renderer.sharedMaterial;
                if (shader == null || source == null)
                    continue;

                Material material = new Material(shader)
                {
                    name = "RoweMod Native Boombox Runtime Material",
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Geometry
                };
                CopyTexture(source, material, "_BaseColorMap");
                CopyTexture(source, material, "_NormalMap");
                CopyTexture(source, material, "_MaskMap");
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                if (material.HasProperty("_NormalScale")) material.SetFloat("_NormalScale", 1f);
                if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 0f);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
                material.EnableKeyword("_NORMALMAP");
                renderer.material = material;
                Materials.Add(material);
            }

            Log.Msg($"[EmoteProps] Prepared native boombox renderers={renderers.Length}, " +
                    $"shader={(shader == null ? "missing" : shader.name)}.");
        }

        private static void CopyTexture(Material source, Material destination, string property)
        {
            if (source == null || destination == null ||
                !source.HasProperty(property) || !destination.HasProperty(property))
                return;
            Texture texture = source.GetTexture(property);
            if (texture != null)
                destination.SetTexture(property, texture);
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

        private static void CreateRadioSpeaker(string name, Vector3 localPosition)
        {
            GameObject speaker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            speaker.name = name;
            speaker.transform.SetParent(_root.transform, false);
            speaker.transform.localPosition = localPosition;
            speaker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            speaker.transform.localScale = new Vector3(0.105f, 0.012f, 0.105f);
            RemoveCollider(speaker);
            ApplyDepthTestedMaterial(speaker.GetComponent<Renderer>(), new Color(0.025f, 0.025f, 0.028f, 1f), name);
        }

        private static void CreateBox(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            Quaternion? localRotation = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(_root.transform, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = localRotation ?? Quaternion.identity;
            box.transform.localScale = localScale;
            RemoveCollider(box);
            ApplyDepthTestedMaterial(box.GetComponent<Renderer>(), color, name);
        }

        private static void PlaceRadioNearRider()
        {
            Transform placementAnchor = Memory.chassisRb != null
                ? Memory.chassisRb.transform
                : Memory.rMbCharacter.transform;
            Vector3 anchorPosition = Memory.chassisRb != null
                ? Memory.chassisRb.position
                : placementAnchor.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(placementAnchor.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.01f)
                flatForward = Vector3.forward;
            Vector3 target = anchorPosition + flatForward * 1.6f;
            bool grounded = UnityEngine.Physics.Raycast(
                target + Vector3.up * 2f,
                Vector3.down,
                out RaycastHit hit,
                5f,
                UnityEngine.Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (grounded)
                target.y = hit.point.y;
            else
                target.y = anchorPosition.y + 0.15f;
            _root.transform.SetPositionAndRotation(target, Quaternion.LookRotation(-flatForward, Vector3.up));

            Renderer[] renderers = _root.GetComponentsInChildren<Renderer>(true);
            string bounds = renderers.Length > 0 && renderers[0] != null
                ? $"center={renderers[0].bounds.center}, size={renderers[0].bounds.size}"
                : "no renderer bounds";
            Log.Msg($"[EmoteProps] Placed boombox 1.6m in front of rider: " +
                    $"anchor={placementAnchor.name}, anchorPosition={anchorPosition}, " +
                    $"target={target}, grounded={grounded}, " +
                    $"renderers={renderers.Length}, {bounds}.");
        }

        private static void CreateSegment(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = name;
            segment.transform.SetParent(_root.transform, false);
            segment.transform.localPosition = localPosition;
            segment.transform.localRotation = Quaternion.identity;
            segment.transform.localScale = localScale;
            RemoveCollider(segment);
            ApplyDepthTestedMaterial(segment.GetComponent<Renderer>(), color, name);
        }

        private static void CreateTip(string name, Vector3 localPosition, float scale, Color color)
        {
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = name;
            tip.transform.SetParent(_root.transform, false);
            tip.transform.localPosition = localPosition;
            tip.transform.localScale = Vector3.one * scale;
            RemoveCollider(tip);
            ApplyDepthTestedMaterial(tip.GetComponent<Renderer>(), color, name);
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
        }

        private static void ApplyDepthTestedMaterial(Renderer renderer, Color color, string name)
        {
            if (renderer == null)
                return;

            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                return;

            Material material = new Material(shader)
            {
                name = $"RoweMod Emote {name}",
                color = color,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Geometry
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            renderer.material = material;
            Materials.Add(material);
        }

        private static void ReleaseFirecracker()
        {
            if (_root == null || _detached)
                return;

            _detached = true;
            _explodeAt = Time.unscaledTime + 0.65f;
            _destroyAt = _explodeAt + 1.25f;
            Rigidbody body = _root.AddComponent<Rigidbody>();
            CapsuleCollider collider = _root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = 0.018f;
            collider.height = 0.12f;
            collider.center = new Vector3(0f, 0.055f, 0f);
            body.mass = 0.08f;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Vector3 forward = Memory.rMbCharacter == null ? _root.transform.up : Memory.rMbCharacter.transform.forward;
            body.velocity = forward * 4.5f + Vector3.up * 2.2f;
            body.angularVelocity = new Vector3(5f, 7f, 3f);
            Log.Msg("[EmoteProps] Released the firecracker prop from the rider's hand.");
        }

        private static void ExplodeFirecracker()
        {
            if (_root == null || _exploded)
                return;

            _exploded = true;
            Vector3 position = _root.transform.position;
            Vector3 direction = _root.transform.up;
            RecordReplaySample(false, true, true);
            bool sparks = PegSparks.PlayEmoteBurst(position, direction);
            bool sound = TryPlayFireSound(position);
            _root.SetActive(false);
            _testStatus = $"Firecracker burst: sparks={(sparks ? "played" : "unavailable")}, " +
                          $"FMOD Core WAV={(sound ? "played" : "failed")}.";
            Log.Msg("[EmoteProps] " + _testStatus);
        }

        private static void UpdateLiveRadio()
        {
            RecordReplaySample(true, false, false);
            UpdateRadioChannelPosition(_root.transform.position);
            try
            {
                if (!_radioChannel.hasHandle())
                {
                    FinishLiveRadio();
                    return;
                }

                Il2CppFMOD.RESULT result = _radioChannel.isPlaying(out bool playing);
                if (result != Il2CppFMOD.RESULT.OK || !playing)
                    FinishLiveRadio();
            }
            catch
            {
                FinishLiveRadio();
            }
        }

        private static void FinishLiveRadio()
        {
            if (_kind != PropKind.Radio || _replayActive)
                return;
            _radioPlaybackEnded = true;
            Log.Msg("[EmoteProps] The 3D radio track reached its end.");
            Stop();
        }

        private static void RecordReplaySample(bool visible, bool burst, bool force)
        {
            if (_replayActive || _root == null || _kind == PropKind.None)
                return;

            try
            {
                GameReplaySystem replay = GameReplaySystem.Instance;
                if (replay == null || (int)replay.State != 0)
                    return;

                float recordTime = replay.CurrentRecordTime;
                if (float.IsNaN(recordTime) || float.IsInfinity(recordTime) || recordTime < 0f)
                    return;

                if (recordTime + 0.01f < _lastRecordedReplayTime)
                    ClearReplaySamples();
                if (!force && recordTime - _lastRecordedReplayTime < ReplaySampleInterval)
                    return;

                if (ReplaySamples.Count >= MaxReplaySamples)
                    ReplaySamples.RemoveRange(0, Mathf.Min(256, ReplaySamples.Count));

                ReplaySamples.Add(new ReplayPropSample(
                    recordTime,
                    _kind,
                    _root.transform.position,
                    _root.transform.rotation,
                    visible,
                    burst));
                _lastRecordedReplayTime = recordTime;

                if (!_loggedReplayRecording)
                {
                    _loggedReplayRecording = true;
                    Log.Msg("[EmoteProps] Recording emote props on the native Replay timeline.");
                }
            }
            catch
            {
                // The native replay system may disappear between an emote update and teardown.
            }
        }

        private static void UpdateReplayPlayback()
        {
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
                          playbackTime - _lastReplayPlaybackTime > ReplaySeekThreshold;
            if (!seeked)
                PlayReplayBurstEvents(_lastReplayPlaybackTime, playbackTime);

            int sampleIndex = FindLastReplaySampleAtOrBefore(playbackTime);
            if (sampleIndex < 0 || !ReplaySamples[sampleIndex].Visible)
            {
                if (_root != null)
                    _root.SetActive(false);
                if (_radioChannelIsReplay)
                    StopRadioAudio();
                _lastReplayPlaybackTime = playbackTime;
                return;
            }

            ReplayPropSample sample = ReplaySamples[sampleIndex];
            Vector3 position = sample.Position;
            Quaternion rotation = sample.Rotation;
            int nextIndex = sampleIndex + 1;
            if (nextIndex < ReplaySamples.Count)
            {
                ReplayPropSample next = ReplaySamples[nextIndex];
                if (next.Visible && next.Kind == sample.Kind && next.Time > sample.Time)
                {
                    float blend = Mathf.InverseLerp(sample.Time, next.Time, playbackTime);
                    position = Vector3.LerpUnclamped(sample.Position, next.Position, blend);
                    rotation = Quaternion.SlerpUnclamped(sample.Rotation, next.Rotation, blend);
                }
            }

            EnsureReplayProp(sample.Kind);
            if (_root != null)
            {
                _root.SetActive(true);
                _root.transform.SetPositionAndRotation(position, rotation);
            }

            if (sample.Kind == PropKind.Radio)
                SyncReplayRadioAudio(replay, playbackTime, seeked, position);
            else if (_radioChannelIsReplay)
                StopRadioAudio();

            _lastReplayPlaybackTime = playbackTime;
        }

        private static int FindLastReplaySampleAtOrBefore(float playbackTime)
        {
            int low = 0;
            int high = ReplaySamples.Count - 1;
            int result = -1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                if (ReplaySamples[middle].Time <= playbackTime + 0.006f)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return result;
        }

        private static void PlayReplayBurstEvents(float fromTime, float toTime)
        {
            if (toTime <= fromTime)
                return;

            int index = FindLastReplaySampleAtOrBefore(fromTime) + 1;
            while (index < ReplaySamples.Count && ReplaySamples[index].Time <= toTime + 0.006f)
            {
                ReplayPropSample sample = ReplaySamples[index];
                if (sample.Burst)
                {
                    Vector3 direction = sample.Rotation * Vector3.up;
                    PegSparks.PlayEmoteBurst(sample.Position, direction);
                    TryPlayFireSound(sample.Position);
                    Log.Msg($"[EmoteProps] Replayed firecracker burst at {sample.Time:0.000}s.");
                }
                index++;
            }
        }

        private static void EnsureReplayProp(PropKind kind)
        {
            if (_root != null && _kind == kind)
                return;

            Stop();
            _kind = kind;
            _root = new GameObject($"RoweMod Replay Emote Prop - {kind}");
            _root.hideFlags = HideFlags.HideAndDontSave;
            if (kind == PropKind.Cigarette)
                BuildCigarette();
            else if (kind == PropKind.Firecracker)
                BuildFirecracker();
            else if (kind == PropKind.Radio)
            {
                if (!BuildRadio())
                {
                    UnityEngine.Object.Destroy(_root);
                    _root = null;
                    _kind = PropKind.None;
                }
            }
        }

        private static void ClearReplaySamples()
        {
            ReplaySamples.Clear();
            ReplayRadioSegments.Clear();
            _lastRecordedReplayTime = float.NegativeInfinity;
            _lastReplayPlaybackTime = float.NegativeInfinity;
            _replayWasScrubbing = false;
            _loggedReplayRecording = false;
            _activeRadioSegmentIndex = -1;
        }

        private static float TryGetRecordTime()
        {
            try
            {
                GameReplaySystem replay = GameReplaySystem.Instance;
                if (replay == null || (int)replay.State != 0)
                    return -1f;
                float value = replay.CurrentRecordTime;
                return float.IsNaN(value) || float.IsInfinity(value) ? -1f : value;
            }
            catch
            {
                return -1f;
            }
        }

        private static void SyncReplayRadioAudio(
            GameReplaySystem replay,
            float playbackTime,
            bool seeked,
            Vector3 position)
        {
            ReplayRadioSegment segment = null;
            for (int i = ReplayRadioSegments.Count - 1; i >= 0; i--)
            {
                ReplayRadioSegment candidate = ReplayRadioSegments[i];
                if (playbackTime >= candidate.StartTime - 0.01f && playbackTime <= candidate.EndTime + 0.01f)
                {
                    segment = candidate;
                    break;
                }
            }

            if (segment == null || !System.IO.File.Exists(segment.Path))
            {
                if (_radioChannelIsReplay)
                    StopRadioAudio();
                return;
            }

            uint desiredMs = (uint)Mathf.Max(0f, (playbackTime - segment.StartTime) * 1000f);
            bool startedNow = false;
            if (!_radioChannelIsReplay || !_radioChannel.hasHandle() ||
                !string.Equals(_radioChannelPath, segment.Path, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryStartRadioAudio(segment.Path, desiredMs, true))
                    return;
                startedNow = true;
            }

            UpdateRadioChannelPosition(position);
            try
            {
                bool scrubbing = replay != null && replay.IsScrubbing;
                bool paused = replay == null || replay._isPaused;
                float playbackSpeed = replay == null ? 1f : replay.PlaybackSpeed;
                if (!float.IsFinite(playbackSpeed))
                    playbackSpeed = 1f;

                // During a scrub the channel stays quiet and untouched. Perform one seek when
                // the user releases the needle instead of repeatedly flushing the FMOD stream.
                if (_replayWasScrubbing && !scrubbing)
                    seeked = true;
                _replayWasScrubbing = scrubbing;

                bool forwardPlayback = !scrubbing && !paused && playbackSpeed > 0.001f;
                float desiredPitch = Mathf.Clamp(
                    playbackSpeed,
                    ReplayMinimumPlaybackPitch,
                    ReplayMaximumPlaybackPitch);
                if (forwardPlayback && !Mathf.Approximately(desiredPitch, _radioReplayPitch))
                {
                    _radioChannel.setPitch(desiredPitch);
                    _radioReplayPitch = desiredPitch;
                }

                if (!startedNow && seeked && !scrubbing)
                {
                    _radioChannel.setPosition(desiredMs, Il2CppFMOD.TIMEUNIT.MS);
                }
                else if (!scrubbing &&
                         _radioChannel.getPosition(out uint currentMs, Il2CppFMOD.TIMEUNIT.MS) == Il2CppFMOD.RESULT.OK &&
                         Math.Abs((long)currentMs - desiredMs) > ReplayDriftCorrectionMs)
                {
                    _radioChannel.setPosition(desiredMs, Il2CppFMOD.TIMEUNIT.MS);
                }

                SetReplayRadioPaused(!forwardPlayback);
            }
            catch (Exception ex)
            {
                Log.Warning($"[EmoteProps] Replay radio sync failed: {ex.Message}");
                StopRadioAudio();
            }
        }

        private static bool TryStartRadioAudio(string audioPath, uint positionMs, bool replay)
        {
            StopRadioAudio();
            try
            {
                Il2CppFMOD.System coreSystem = RuntimeManager.CoreSystem;
                if (!coreSystem.hasHandle())
                    return false;

                Il2CppFMOD.MODE mode = Il2CppFMOD.MODE.LOOP_OFF |
                                         Il2CppFMOD.MODE._3D |
                                         Il2CppFMOD.MODE._3D_WORLDRELATIVE |
                                         Il2CppFMOD.MODE._3D_LINEARROLLOFF |
                                         Il2CppFMOD.MODE.CREATESTREAM;
                Il2CppFMOD.RESULT result = coreSystem.createStream(audioPath, mode, out _radioSound);
                if (result != Il2CppFMOD.RESULT.OK || !_radioSound.hasHandle())
                {
                    Log.Warning($"[EmoteProps] FMOD Core could not stream radio audio ({result}): {audioPath}");
                    return false;
                }

                result = _radioSound.set3DMinMaxDistance(RadioMinimumDistance, RadioMaximumDistance);
                if (result == Il2CppFMOD.RESULT.OK)
                    result = coreSystem.playSound(_radioSound, default(Il2CppFMOD.ChannelGroup), true, out _radioChannel);
                if (result != Il2CppFMOD.RESULT.OK || !_radioChannel.hasHandle())
                {
                    StopRadioAudio();
                    Log.Warning($"[EmoteProps] FMOD Core could not create the radio channel ({result}).");
                    return false;
                }

                _radioChannelPath = System.IO.Path.GetFullPath(audioPath);
                _radioChannelIsReplay = replay;
                _radioChannel.set3DMinMaxDistance(RadioMinimumDistance, RadioMaximumDistance);
                _radioChannel.set3DLevel(1f);
                _radioChannel.set3DSpread(0f);
                _radioChannel.set3DDopplerLevel(0f);
                _radioChannel.set3DConeSettings(180f, 300f, 0.35f);
                _radioChannel.setVolume(Mathf.Clamp01(Config.replaySettings.radioVolume));
                if (positionMs > 0)
                    _radioChannel.setPosition(positionMs, Il2CppFMOD.TIMEUNIT.MS);
                if (_root != null)
                    UpdateRadioChannelPosition(_root.transform.position);
                _radioChannel.setPaused(false);
                _radioChannelPaused = false;
                _radioReplayPitch = 1f;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EmoteProps] FMOD Core radio playback failed: {ex.Message}");
                StopRadioAudio();
                return false;
            }
        }

        private static void UpdateRadioChannelPosition(Vector3 position)
        {
            if (!_radioChannel.hasHandle())
                return;
            try
            {
                Il2CppFMOD.VECTOR fmodPosition = RuntimeUtils.ToFMODVector(position);
                Il2CppFMOD.VECTOR fmodVelocity = RuntimeUtils.ToFMODVector(Vector3.zero);
                _radioChannel.set3DAttributes(ref fmodPosition, ref fmodVelocity);
                if (_root != null)
                {
                    Il2CppFMOD.VECTOR fmodForward = RuntimeUtils.ToFMODVector(_root.transform.forward);
                    _radioChannel.set3DConeOrientation(ref fmodForward);
                }
            }
            catch { }
        }

        private static void SetReplayRadioPaused(bool paused)
        {
            if (!_radioChannel.hasHandle() || _radioChannelPaused == paused)
                return;
            _radioChannel.setPaused(paused);
            _radioChannelPaused = paused;
        }

        private static void StopRadioAudio()
        {
            try
            {
                if (_radioChannel.hasHandle())
                    _radioChannel.stop();
            }
            catch { }
            try
            {
                if (_radioSound.hasHandle())
                    _radioSound.release();
            }
            catch { }
            _radioChannel = default;
            _radioSound = default;
            _radioChannelPath = null;
            _radioChannelIsReplay = false;
            _radioChannelPaused = false;
            _radioReplayPitch = 1f;
        }

        private readonly struct ReplayPropSample
        {
            public readonly float Time;
            public readonly PropKind Kind;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly bool Visible;
            public readonly bool Burst;

            public ReplayPropSample(
                float time,
                PropKind kind,
                Vector3 position,
                Quaternion rotation,
                bool visible,
                bool burst)
            {
                Time = time;
                Kind = kind;
                Position = position;
                Rotation = rotation;
                Visible = visible;
                Burst = burst;
            }
        }

        private static bool TryPlayFireSound(Vector3 position)
        {
            try
            {
                Il2CppFMOD.System coreSystem = RuntimeManager.CoreSystem;
                if (!coreSystem.hasHandle())
                {
                    Log.Warning("[EmoteProps] The game's FMOD Core System is invalid.");
                    return false;
                }

                if (!_fireSoundReady || !_fireSound.hasHandle())
                {
                    string soundPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        Application.dataPath,
                        "..",
                        "Mods",
                        "RoweMod",
                        "Audio",
                        FireSoundFileName));
                    if (!System.IO.File.Exists(soundPath))
                    {
                        Log.Warning($"[EmoteProps] Firecracker WAV is missing: {soundPath}");
                        return false;
                    }

                    Il2CppFMOD.MODE mode = Il2CppFMOD.MODE.LOOP_OFF |
                                             Il2CppFMOD.MODE._3D |
                                             Il2CppFMOD.MODE._3D_WORLDRELATIVE |
                                             Il2CppFMOD.MODE._3D_LINEARROLLOFF |
                                             Il2CppFMOD.MODE.CREATESAMPLE;
                    Il2CppFMOD.RESULT createResult = coreSystem.createSound(
                        soundPath,
                        mode,
                        out _fireSound);
                    if (createResult != Il2CppFMOD.RESULT.OK || !_fireSound.hasHandle())
                    {
                        Log.Warning($"[EmoteProps] FMOD Core createSound returned {createResult}: {soundPath}");
                        return false;
                    }

                    Il2CppFMOD.RESULT distanceResult = _fireSound.set3DMinMaxDistance(0.75f, 32f);
                    if (distanceResult != Il2CppFMOD.RESULT.OK)
                    {
                        Log.Warning($"[EmoteProps] FMOD Core set3DMinMaxDistance returned {distanceResult}.");
                        return false;
                    }

                    _fireSoundReady = true;
                    Log.Msg($"[EmoteProps] Loaded firecracker WAV through FMOD Core: {soundPath}");
                }

                Il2CppFMOD.RESULT result = coreSystem.playSound(
                    _fireSound,
                    default(Il2CppFMOD.ChannelGroup),
                    true,
                    out Il2CppFMOD.Channel channel);
                if (result != Il2CppFMOD.RESULT.OK || !channel.hasHandle())
                {
                    Log.Warning($"[EmoteProps] FMOD Core playSound returned {result}.");
                    return false;
                }

                Il2CppFMOD.VECTOR fmodPosition = RuntimeUtils.ToFMODVector(position);
                Il2CppFMOD.VECTOR fmodVelocity = RuntimeUtils.ToFMODVector(Vector3.zero);
                result = channel.set3DAttributes(ref fmodPosition, ref fmodVelocity);
                if (result == Il2CppFMOD.RESULT.OK)
                    result = channel.setVolume(1f);
                if (result == Il2CppFMOD.RESULT.OK)
                    result = channel.setPaused(false);
                if (result != Il2CppFMOD.RESULT.OK)
                {
                    channel.stop();
                    Log.Warning($"[EmoteProps] FMOD Core 3D playback setup returned {result}.");
                    return false;
                }

                Log.Msg($"[EmoteProps] Triggered FMOD Core firecracker WAV at {position}.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[EmoteProps] FMOD Core firecracker playback failed: {ex.Message}");
                return false;
            }
        }
    }
}
