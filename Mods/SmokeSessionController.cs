using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using rowemod.Utils;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation;
using Il2CppMashBox.Addons.CharacterController;
using GameReplaySystem = Il2CppMashBox.Core.Runtime.ReplaySystem.ReplaySystem;

namespace rowemod.Mods
{
    // Local Humanoid emotes and temporary hand props. Nothing enters the object dropper.
    internal static class SmokeSessionController
    {
        internal const string Prefix = "RoweMod_Custom_Emote_SmokeSession_";
        private const string ExpectedBundleSha256 = "9B7A31E04457547B478054BED04BAFA400C984430F47BE7A382C39EB9DB88D71";
        private static AssetBundle _bundle;
        private static bool _loadFailed;
        private static GameObject _root;
        private static Transform _hand;
        private static Transform _finger;
        private static Transform _palm;
        private static Animator _animator;
        private static AnimatedRagdoll _ragdoll;
        private static EmoteSystem _nativeEmotes;
        private static bool _wasKinematic;
        private static float _restorePhysicsAt = -1f;
        private static bool _usingLeftHand;
        private static string _gesture;
        private static float _startedAt;
        private static bool _sawActive;
        private static Vector3 _tablePosition;
        private static Quaternion _tableRotation;
        private static Vector3 _propForward;
        private static bool _smoking, _exhaling;
        private static float _nextTrail, _nextExhale;
        private static Transform _bowl;
        private static Material _smokeMaterial;
        private static Mesh _smokeQuad;
        private static readonly MaterialPropertyBlock SmokeColor = new MaterialPropertyBlock();
        private sealed class Puff
        {
            internal GameObject Object;
            internal Renderer Renderer;
            internal Vector3 Origin, Velocity;
            internal float Born, Lifetime, Size, Spin, Opacity;
        }
        private static readonly List<Puff> Puffs = new List<Puff>();
        private static Transform _head;
        private static Texture2D _smokeTexture;
        private static readonly List<Material> Materials = new List<Material>();
        private readonly struct PropPose
        {
            internal readonly string Gesture;
            internal readonly Vector3 Position;
            internal readonly Quaternion Rotation;
            internal PropPose(string gesture, Vector3 position, Quaternion rotation)
            { Gesture = gesture; Position = position; Rotation = rotation; }
        }
        private readonly struct RecordedPuff
        {
            internal readonly float Time;
            internal readonly Puff Data;
            internal RecordedPuff(float time, Puff data) { Time = time; Data = data; }
        }
        private static readonly ReplayPropTrack<PropPose> ReplayTrack = new ReplayPropTrack<PropPose>();
        private static readonly List<RecordedPuff> RecordedPuffs = new List<RecordedPuff>();
        private static readonly List<Puff> ReplayPuffs = new List<Puff>();
        private static readonly List<Material> ReplayMaterials = new List<Material>();
        private static bool _replayActive;
        private static GameObject _replayRoot;
        private static string _replayGesture;
        private static int _segment;
        private static float _recordTime = -1f;
        private static bool _loggedReplayRecording;
        public static string Status { get; private set; } = "Smoke Session pack has not been loaded.";

        public static bool EnsureAvailable()
        {
            if (_bundle != null)
            {
                if (Memory.loadedBundles.Contains(_bundle)) return false;
                Memory.loadedBundles.Add(_bundle);
                TrickAnimationEditor.NotifyAssetBundlesReloaded();
                return true;
            }
            if (_loadFailed) return false;
            string path = Path.Combine(Memory.bundlesFolderPath, "rowemod_smoke_session");

            try
            {
                using (Stream stream = typeof(SmokeSessionController).Assembly.GetManifestResourceStream("rowemod.smoke_session.bundle")
                    ?? (File.Exists(path) ? File.OpenRead(path) : null))
                {
                    if (stream == null) return false;
                    if (stream.Length > 16L * 1024L * 1024L)
                        throw new InvalidDataException("The Smoke Session bundle exceeds its size limit.");
                    using (SHA256 sha = SHA256.Create())
                        if (!string.Equals(Convert.ToHexString(sha.ComputeHash(stream)), ExpectedBundleSha256, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("The Smoke Session bundle failed verification.");
                    stream.Position = 0;
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes);
                        _bundle = AssetBundle.LoadFromMemory(bytes.ToArray());
                    }
                }
                if (_bundle == null) throw new InvalidDataException("Unity could not load the Smoke Session bundle.");
                Memory.loadedBundles.Add(_bundle);
                TrickAnimationEditor.NotifyAssetBundlesReloaded();
                Status = "Smoke Session ready: nine local gestures with temporary emote props.";
                Log.Msg("[SmokeSession] " + Status);
                return true;
            }
            catch (Exception ex)
            {
                _loadFailed = true;
                Status = "Smoke Session unavailable: " + ex.Message;
                Log.Warning("[SmokeSession] " + Status);
                return false;
            }
        }

        public static void Begin(string clipName)
        {
            if (_replayActive) return;
            try { BeginInternal(clipName); }
            catch (Exception ex) { Stop(); Log.Warning("[SmokeSession] Prop setup failed: " + ex.Message); }
        }

        private static void BeginInternal(string clipName)
        {
            Stop();
            if (clipName == null || !clipName.StartsWith(Prefix, StringComparison.Ordinal)) return;
            _gesture = clipName.Substring(Prefix.Length);
            if (_bundle == null || Memory.rMbCharacter == null) return;
            Animator animator = null;
            foreach (Animator candidate in Memory.rMbCharacter.GetComponentsInChildren<Animator>(true))
                if (candidate != null && candidate.isHuman) { animator = candidate; break; }
            if (animator == null) return;
            if (_ragdoll == null)
            {
                _ragdoll = Memory.rMbCharacter.GetComponentInChildren<AnimatedRagdoll>(true);
                if (_ragdoll != null) _wasKinematic = _ragdoll.Kinematic;
            }
            if (_ragdoll != null) _ragdoll.Kinematic = true;
            _nativeEmotes = Memory.rMbCharacter.GetComponentInChildren<EmoteSystem>(true);
            _restorePhysicsAt = -1f;
            _animator = animator;
            _startedAt = Time.unscaledTime;
            _propForward = Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up).normalized;
            BindHand(_gesture.StartsWith("Smoke_", StringComparison.Ordinal));
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (_hand == null) return;
            _segment++;
            _root = CreateProp(_gesture, Materials, out _bowl);
            if (_gesture == "Bong_Table" && _root != null)
            {
                Vector3 hips=animator.GetBoneTransform(HumanBodyBones.Hips).position;
                Vector3 forward=Vector3.ProjectOnPlane(animator.transform.forward,Vector3.up).normalized;
                _tableRotation=Quaternion.LookRotation(forward,Vector3.up); _tablePosition=hips;
                float distance=float.MaxValue;
                foreach(var hit in UnityEngine.Physics.RaycastAll(hips+Vector3.up,Vector3.down,5f,~0,QueryTriggerInteraction.Ignore))
                    if(hit.collider!=null && !hit.collider.transform.IsChildOf(Memory.rMbCharacter.transform) && hit.distance<distance) {distance=hit.distance;_tablePosition.y=hit.point.y;}
                if(distance==float.MaxValue) _tablePosition.y=hips.y-.95f;
            }
            _startedAt = Time.unscaledTime;
            _sawActive = false;
            if (_gesture.StartsWith("Smoke_", StringComparison.Ordinal) || _gesture.StartsWith("Bong_", StringComparison.Ordinal))
            {
                EnsureSmokeResources();
                _smoking = true;
                _nextTrail = _nextExhale = Time.unscaledTime;
            }
            LateUpdate();
            Log.Msg($"[SmokeSession] Attached {_gesture}: renderers={(_root == null ? 0 : _root.GetComponentsInChildren<Renderer>().Length)}.");
        }

        private static GameObject CreateProp(string gesture, List<Material> materials, out Transform bowl)
        {
            bowl = null;
            string mesh = null;
            float size = 1f;
            if (gesture.StartsWith("Smoke_", StringComparison.Ordinal)) { mesh = "sm_njoint_mesh"; size = .09f; }
            else if (gesture == "Bong_Session") { mesh = "sm_noble_hand_bong_mesh"; size = .35f; }
            else if (gesture == "Bong_Table") { mesh = "cmn_prp_own_bk_propsfurniture_generic_table01_mesh"; size = 1.2f; }
            else if (gesture == "Beer_Shotgun") { mesh = "sm_fx_gesture_rainbow_a_mesh"; size = .17f; }
            else if (gesture == "Camera") { mesh = "sm_ncam_a_mesh"; size = .26f; }
            if (mesh == null) return null; // Hand signs and Twerk have no hand prop.
            string assetName = null;
            foreach (string name in _bundle.GetAllAssetNames())
                if (name.EndsWith("smokesession_" + mesh + ".prefab", StringComparison.OrdinalIgnoreCase)) { assetName = name; break; }
            GameObject prefab = assetName == null ? null : _bundle.LoadAsset<GameObject>(assetName);
            if (prefab == null) { Log.Warning("[SmokeSession] Missing prop " + mesh); return null; }
            GameObject root = new GameObject("RoweMod Smoke Session - " + gesture);
            try
            {
                root.hideFlags = HideFlags.HideAndDontSave;
                GameObject visual = UnityEngine.Object.Instantiate(prefab);
                visual.transform.SetParent(root.transform, false);
                Bounds bounds = new Bounds(); bool first = true;
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                    var source = renderer.sharedMaterials;
                    var converted = new Material[source.Length];
                    for (int i = 0; i < source.Length; i++)
                    {
                        Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                        if (shader == null) throw new InvalidOperationException("No supported prop shader.");
                        var material = new Material(shader) { name = "Smoke Session " + mesh, renderQueue = (int)RenderQueue.Geometry };
                        Texture texture = source[i] == null ? null : source[i].mainTexture;
                        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                        if (material.HasProperty("_BaseColorMap"))
                        {
                            material.SetTexture("_BaseColorMap", texture);
                            if (gesture.StartsWith("Bong_", StringComparison.Ordinal)) { material.SetTextureScale("_BaseColorMap",new Vector2(1,-1)); material.SetTextureOffset("_BaseColorMap",new Vector2(0,1)); }
                        }
                        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                        if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 0);
                        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1);
                        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .2f);
                        converted[i] = material; materials.Add(material);
                    }
                    renderer.sharedMaterials = converted;
                }
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                float scale = size / Mathf.Max(.0001f, longest);
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = -bounds.center * scale;
                if (gesture == "Camera")
                {
                    // The imported mesh is tilted in its source coordinates. Normalize the lens
                    // and top handle before attaching the handle (not the body centre) to the hand.
                    Quaternion upright = Quaternion.Inverse(Quaternion.LookRotation(
                        new Vector3(0f, -.65f, -.76f), new Vector3(0f, -.76f, .65f)));
                    visual.transform.localRotation = upright;
                    visual.transform.localPosition = -(upright * new Vector3(-.02f, -.06f, -.04f)) * scale;
                }
                if (gesture == "Bong_Session")
                {
                    Quaternion turn = Quaternion.Euler(0f, 180f, 0f);
                    visual.transform.localRotation = turn;
                    visual.transform.localPosition = -(turn * bounds.center) * scale;
                    bowl = new GameObject("Smoke Session bowl").transform;
                    bowl.SetParent(visual.transform, false);
                    bowl.localPosition = new Vector3(0f, 1.025f, .415f);
                }
                if (gesture == "Bong_Table")
                {
                    visual.transform.localScale = Vector3.one; visual.transform.localPosition = Vector3.zero;
                    Transform bong = null;
                    foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                        if (child.name == "SmokeSessionTableBong") { bong = child; break; }
                    if (bong == null) throw new InvalidDataException("The table bong pivot is missing.");
                    bong.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    bowl = new GameObject("Smoke Session bowl").transform;
                    bowl.SetParent(bong, false);
                    bowl.localPosition = new Vector3(0f, 1.025f - .7900390625f, .415f - .484619140625f);
                }
                return root;
            }
            catch
            {
                UnityEngine.Object.Destroy(root);
                throw;
            }
        }

        public static void Update(bool nativeActive)
        {
            if (_replayActive) return;
            if (_gesture == null) return;
            if (nativeActive) _sawActive = true;
            else if (_sawActive || Time.unscaledTime - _startedAt > 1f) Stop();
            if (_gesture == "Smoke_Session_Sit")
            {
                bool left = GestureTime() < 7f;
                if (left != _usingLeftHand) BindHand(left);
            }
            if (_smoking)
            {
                float t = GestureTime();
                bool puff = _gesture == "Smoke_On_The_Go" ? (t > 8f && t < 12f) || (t > 21f && t < 27f)
                    : _gesture == "Smoke_Session_Sit" ? t > 5f && t < 9f
                    : _gesture == "Bong_Table" ? t > 12f && t < 16f : t > 16f && t < 21f;
                _exhaling = puff;
            }
        }

        private static float GestureTime()
        {
            float elapsed = Time.unscaledTime - _startedAt;
            float duration = _nativeEmotes?._emoteLayer?.CurrentState?.Clip?.length ?? 0f;
            return duration > 0f ? elapsed % duration : elapsed;
        }

        public static void LateUpdate()
        {
            if (_replayActive) { UpdateReplay(); return; }
            ObserveRecordClock();
            if (_restorePhysicsAt >= 0f && Time.unscaledTime >= _restorePhysicsAt)
                RestorePhysics();
            UpdateSmoke(); // Detached puffs keep fading after Stop removes the prop.
            if (_root == null || _hand == null)
            {
                // Replay exit can fire before the native clock resumes recording.
                // End the prop on the first recording frame even when no live prop remains.
                RecordPropPose(false);
                return;
            }
            Vector3 along = _finger != null ? _finger.position - _hand.position : _hand.forward;
            if (along.sqrMagnitude < .000001f) along = _hand.forward;
            Vector3 across = _palm != null && _finger != null ? _palm.position - _finger.position : _hand.right;
            Vector3 normal = Vector3.Cross(along, across);
            if (normal.sqrMagnitude < .000001f) normal = _hand.up;
            if (_gesture == "Bong_Table") _root.transform.SetPositionAndRotation(_tablePosition,_tableRotation);
            else if (_gesture == "Bong_Session")
            {
                // Its curved mouthpiece points down local -Z after the mesh turn.
                // Keep that side toward the face throughout the moving hand pose.
                Vector3 away = Vector3.ProjectOnPlane(_hand.position - (_head != null ? _head.position : _animator.transform.position), Vector3.up);
                if (away.sqrMagnitude < .001f) away = -_propForward;
                _root.transform.SetPositionAndRotation(_hand.position + Vector3.up * .10f,
                    Quaternion.LookRotation(away.normalized, Vector3.up));
            }
            else if (_gesture == "Camera")
            {
                Vector3 outward = Vector3.ProjectOnPlane(_hand.position - _animator.GetBoneTransform(HumanBodyBones.Hips).position, Vector3.up);
                if (outward.sqrMagnitude < .001f) outward = _propForward;
                _root.transform.SetPositionAndRotation((_finger ?? _hand).position,
                    Quaternion.LookRotation(outward.normalized, Vector3.up));
            }
            else _root.transform.SetPositionAndRotation((_finger ?? _hand).position + along.normalized * .025f,
                Quaternion.LookRotation(along.normalized, normal.normalized));
            if (_smoking)
            {
                float now = Time.unscaledTime;
                if (now >= _nextTrail)
                {
                    Vector3 source = _bowl != null ? _bowl.position : _root.transform.position + _root.transform.forward * .035f;
                    EmitSmoke(source, Vector3.up * .11f, false);
                    _nextTrail = now + .18f;
                }
                if (_exhaling && _head != null && now >= _nextExhale)
                {
                    EmitSmoke(_head.position + _head.forward * .10f - Vector3.up * .035f,
                        _head.forward * .25f + Vector3.up * .055f, true);
                    _nextExhale = now + .065f;
                }
            }
            RecordPropPose();
        }

        private static void BindHand(bool left)
        {
            _usingLeftHand = left;
            _hand = _animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            _finger = _animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            _palm = _animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        }

        private static void EnsureSmokeResources()
        {
            if (_smokeMaterial != null) return;
            // HDRP/Unlit does not consume the built-in particle vertex colour. Use a
            // shared billboard mesh and explicit per-puff colour so opacity really fades.
            _smokeTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            _smokeTexture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float r = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                float wisps = .82f + .18f * Mathf.PerlinNoise(x * .14f, y * .14f);
                _smokeTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r * r), 2) * wisps));
            }
            _smokeTexture.Apply();
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) throw new InvalidOperationException("Smoke requires Streets' HDRP/Unlit shader.");
            _smokeMaterial = new Material(shader) { name = "Smoke Session haze" };
            _smokeMaterial.SetTexture("_UnlitColorMap", _smokeTexture);
            _smokeMaterial.SetColor("_UnlitColor", Color.white);
            _smokeMaterial.SetFloat("_SurfaceType", 1);
            _smokeMaterial.SetFloat("_BlendMode", 0); // HDRP alpha blending.
            _smokeMaterial.SetFloat("_ZWrite", 0);
            _smokeMaterial.SetFloat("_TransparentZWrite", 0);
            _smokeMaterial.SetFloat("_CullMode", (float)CullMode.Off);
            _smokeMaterial.SetFloat("_CullModeForward", (float)CullMode.Off);
            _smokeMaterial.SetFloat("_DoubleSidedEnable", 1);
            HDMaterial.ValidateMaterial(_smokeMaterial);
            _smokeMaterial.renderQueue = (int)RenderQueue.Transparent;
            _smokeQuad = new Mesh { name = "Smoke Session billboard" };
            _smokeQuad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            _smokeQuad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            _smokeQuad.triangles = new[] { 0,2,1,0,3,2 };
            _smokeQuad.RecalculateBounds();
        }

        private static void EmitSmoke(Vector3 origin, Vector3 velocity, bool exhale)
        {
            if (_smokeMaterial == null || Puffs.Count >= 96) return;
            var puff = new Puff { Origin = origin,
                Velocity = velocity + UnityEngine.Random.insideUnitSphere * .025f,
                Born = Time.unscaledTime, Lifetime = exhale ? 3.2f : 2.4f,
                Size = exhale ? .13f : .045f, Spin = UnityEngine.Random.Range(0f, 360f),
                Opacity = exhale ? .48f : .34f };
            CreatePuffVisual(puff);
            Puffs.Add(puff);
            if (!_replayActive && _recordTime >= 0f)
            {
                if (RecordedPuffs.Count >= 65536) RecordedPuffs.RemoveRange(0, 256);
                RecordedPuffs.Add(new RecordedPuff(_recordTime, new Puff { Origin = puff.Origin,
                    Velocity = puff.Velocity, Lifetime = puff.Lifetime, Size = puff.Size,
                    Spin = puff.Spin, Opacity = puff.Opacity }));
            }
        }

        private static void CreatePuffVisual(Puff puff)
        {
            var go = new GameObject("Smoke Session puff") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<MeshFilter>().sharedMesh = _smokeQuad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _smokeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Start hidden until its first billboard/opacity update.
            renderer.enabled = false;
            puff.Object = go;
            puff.Renderer = renderer;
        }

        private static void UpdateSmoke()
        {
            UnityEngine.Camera camera = Puffs.Count > 0 ? UnityEngine.Camera.main : null;
            for (int i = Puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = Puffs[i];
                float age = Time.unscaledTime - puff.Born, t = age / puff.Lifetime;
                if (puff.Object == null || t >= 1f)
                {
                    if (puff.Object != null) UnityEngine.Object.Destroy(puff.Object);
                    Puffs.RemoveAt(i); continue;
                }
                RenderPuff(puff, puff, age, camera);
            }
            if (!_smoking && Puffs.Count == 0) ClearSmoke();
        }

        private static void RenderPuff(Puff visual, Puff data, float age, UnityEngine.Camera camera)
        {
            float t = Mathf.Clamp01(age / data.Lifetime);
            visual.Object.transform.position = data.Origin + data.Velocity * age + Vector3.up * (.025f * age * age);
            visual.Object.transform.localScale = Vector3.one * data.Size * (1f + t * 3f);
            if (camera != null) visual.Object.transform.rotation = camera.transform.rotation * Quaternion.AngleAxis(data.Spin + age * 12f, Vector3.forward);
            float alpha = data.Opacity * Mathf.Clamp01(age * 12f) * Mathf.Pow(1f - t, 1.5f);
            SmokeColor.SetColor("_UnlitColor", new Color(.82f, .84f, .86f, alpha));
            visual.Renderer.SetPropertyBlock(SmokeColor);
            visual.Renderer.enabled = camera != null;
        }

        private static void ObserveRecordClock()
        {
            _recordTime = -1f;
            try
            {
                GameReplaySystem replay = GameReplaySystem.Instance;
                if (_replayActive || replay == null || (int)replay.State != 0) return;
                float time = replay.CurrentRecordTime;
                if (!float.IsFinite(time) || time < 0f) return;
                float history = Mathf.Clamp(replay._replayManagerData?._maxRecordTimeBuffer ?? 600f, 1f, 1800f);
                bool rewound = ReplayTrack.Observe(time, history);
                if (rewound) RecordedPuffs.RemoveAll(puff => puff.Time > time);
                int expired = 0;
                while (expired < RecordedPuffs.Count && RecordedPuffs[expired].Time < time - history - 3.2f) expired++;
                if (expired > 0) RecordedPuffs.RemoveRange(0, expired);
                _recordTime = time;
            }
            catch { } // The scene-owned replay clock can disappear during teardown.
        }

        private static void RecordPropPose(bool visible = true)
        {
            if (_replayActive) return;
            ObserveRecordClock();
            if (_recordTime < 0f) return;
            visible &= _root != null;
            ReplayTrack.Record(_recordTime, _segment, visible, visible
                ? new PropPose(_gesture, _root.transform.position, _root.transform.rotation) : default);
            if (visible && !_loggedReplayRecording)
            {
                _loggedReplayRecording = true;
                Log.Msg("[SmokeSession] Recording props and smoke on the native replay timeline.");
            }
        }

        internal static void OnReplayOpened()
        {
            _replayActive = true; // Teardown must not append a stop event to the recorded timeline.
            ReleaseLive();
            ClearReplayVisuals();
            Log.Msg($"[SmokeSession] Replay armed: {ReplayTrack.Count} prop samples, {RecordedPuffs.Count} smoke events.");
        }

        internal static void OnReplayClosed()
        {
            ClearReplayVisuals();
            ClearSmoke();
            _replayActive = false;
            RecordPropPose(false);
        }

        private static void UpdateReplay()
        {
            try
            {
                GameReplaySystem replay = GameReplaySystem.Instance;
                if (replay == null) { ClearReplayVisuals(); return; }
                float time = replay.CurrentPlaybackTime;
                if (!float.IsFinite(time)) { ClearReplayVisuals(); return; }
                if (ReplayTrack.Evaluate(time, out var from, out var to, out float blend))
                {
                    if (_replayRoot == null || _replayGesture != from.Value.Gesture)
                    {
                        ClearReplayProp();
                        EnsureAvailable();
                        _replayGesture = from.Value.Gesture;
                        _replayRoot = CreateProp(_replayGesture, ReplayMaterials, out _);
                        if (_replayRoot != null) _replayRoot.name = "RoweMod Replay Smoke Session - " + _replayGesture;
                    }
                    if (_replayRoot != null)
                    {
                        _replayRoot.SetActive(true);
                        _replayRoot.transform.SetPositionAndRotation(
                            Vector3.LerpUnclamped(from.Value.Position, to.Value.Position, blend),
                            Quaternion.SlerpUnclamped(from.Value.Rotation, to.Value.Rotation, blend));
                    }
                }
                else if (_replayRoot != null) _replayRoot.SetActive(false);

                // Reconstruct puffs from their recorded birth data, never wall-clock time.
                // A pooled set of billboards supports paused, backward, and arbitrary seeks.
                int low = 0, high = RecordedPuffs.Count;
                while (low < high)
                {
                    int middle = low + ((high - low) >> 1);
                    if (RecordedPuffs[middle].Time < time - 3.2f) low = middle + 1;
                    else high = middle;
                }
                int used = 0;
                UnityEngine.Camera camera = UnityEngine.Camera.main;
                for (int i = low; i < RecordedPuffs.Count && RecordedPuffs[i].Time <= time && used < 96; i++)
                {
                    var puff = RecordedPuffs[i];
                    float age = time - puff.Time;
                    if (age >= puff.Data.Lifetime) continue;
                    EnsureSmokeResources();
                    if (used == ReplayPuffs.Count)
                    {
                        var visual = new Puff();
                        CreatePuffVisual(visual);
                        ReplayPuffs.Add(visual);
                    }
                    ReplayPuffs[used].Object.SetActive(true);
                    RenderPuff(ReplayPuffs[used++], puff.Data, age, camera);
                }
                for (int i = used; i < ReplayPuffs.Count; i++) ReplayPuffs[i].Object.SetActive(false);
            }
            catch (Exception ex)
            {
                ClearReplayVisuals();
                Log.Warning("[SmokeSession] Replay prop update failed: " + ex.Message);
            }
        }

        private static void ClearReplayProp()
        {
            if (_replayRoot != null) UnityEngine.Object.Destroy(_replayRoot);
            foreach (Material material in ReplayMaterials) if (material != null) UnityEngine.Object.Destroy(material);
            ReplayMaterials.Clear(); _replayRoot = null; _replayGesture = null;
        }

        private static void ClearReplayVisuals()
        {
            ClearReplayProp();
            foreach (Puff puff in ReplayPuffs) if (puff.Object != null) UnityEngine.Object.Destroy(puff.Object);
            ReplayPuffs.Clear();
        }

        private static void ClearSmoke()
        {
            foreach (Puff puff in Puffs) if (puff.Object != null) UnityEngine.Object.Destroy(puff.Object);
            Puffs.Clear();
            if (_smokeMaterial != null) UnityEngine.Object.Destroy(_smokeMaterial);
            if (_smokeTexture != null) UnityEngine.Object.Destroy(_smokeTexture);
            if (_smokeQuad != null) UnityEngine.Object.Destroy(_smokeQuad);
            _smokeMaterial = null; _smokeTexture = null; _smokeQuad = null;
        }

        public static void Release()
        {
            ReleaseLive();
            ClearReplayVisuals();
            _replayActive = false;
            ReplayTrack.Clear(); RecordedPuffs.Clear();
            _recordTime = -1f; _loggedReplayRecording = false;
        }

        private static void ReleaseLive()
        {
            // Replay/mod shutdown may stop our LateUpdate before the normal fade finishes.
            // Evaluate the native standing pose before releasing its physics skeleton.
            if (_ragdoll != null && _nativeEmotes != null)
            {
                _nativeEmotes.CancelEmote();
                if (_nativeEmotes._emoteLayer != null) _nativeEmotes._emoteLayer.Weight = 0f;
                _nativeEmotes._animancerComponent?.Evaluate(0f);
            }
            Stop();
            ClearSmoke();
            RestorePhysics();
        }

        private static void RestorePhysics()
        {
            if (_ragdoll != null)
            {
                _ragdoll.MatchPhysicsSkeletonToAnimatedPose();
                _ragdoll.Kinematic = _wasKinematic;
            }
            _ragdoll = null;
            _nativeEmotes = null;
            _restorePhysicsAt = -1f;
        }

        public static void Stop()
        {
            if (_root != null) RecordPropPose(false);
            if (_root != null) UnityEngine.Object.Destroy(_root);
            foreach (Material material in Materials) if (material != null) UnityEngine.Object.Destroy(material);
            Materials.Clear(); _root = null; _hand = null; _finger = null; _palm = null; _gesture = null; _sawActive = false;
            _animator = null;
            if (_ragdoll != null) _restorePhysicsAt = Time.unscaledTime + .2f;
            _smoking = _exhaling = false; _head = null; _bowl = null;
        }
    }
}
