using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAnimancer;
using Il2CppMashBox.Core.Runtime.InverseKinematics;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace rowemod.Mods
{
    internal sealed class StudioPreviewStage : IDisposable
    {
        internal const string SceneName = "RoweMod_AnimationStudio";
        private const int PreviewLayer = 30;
        private const int PreviewMask = 1 << PreviewLayer;
        private const float PreviewLightDistance = 18f;
        private readonly Vector3 origin = new(10000, 10000, 10000);
        private readonly List<UnityEngine.Object> owned = new();
        private readonly List<(Transform node, Vector3 position, Quaternion rotation)> rest = new();
        private readonly Dictionary<int, AnimationClip> previewClips = new();
        private readonly List<SkinnedMeshRenderer> skins = new();
        private readonly Transform[] contactTargets = new Transform[4];
        private readonly Dictionary<(int limb, int id), Transform> bikeContacts = new();
        private readonly Dictionary<Transform, (Transform pivot, Quaternion basis, float offset)> wristRollers = new();
        internal readonly float[] ContactWeights = new float[4];
        internal readonly float[] ContactGaps = new float[4];
        private Scene scene;
        private GameObject root;
        private AnimancerComponent riderPlayer;
        private StudioBikeGeometry geometry;
        private readonly Dictionary<string, Quaternion> editBases = new();
        internal Animator Rider, Bike;
        internal UnityEngine.Camera Camera;
        internal RenderTexture Image;
        internal float Yaw = 35, Pitch = 12, Distance = 3.6f;
        internal Vector3 Pan = Vector3.zero;

        internal void Create()
        {
            scene = SceneManager.CreateScene(SceneName);
            root = new GameObject("RoweMod Animation Studio"); SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = origin;
            var animators = Resources.FindObjectsOfTypeAll<Animator>().Where(a => a != null && a.gameObject.scene.IsValid() && a.gameObject.scene.isLoaded && a.gameObject.scene.name != SceneName).ToArray();
            var sourceRider = Memory.menuPlayer?.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman)
                ?? animators.FirstOrDefault(a => a.isHuman && a.gameObject.scene.name == "MainMenu" && a.transform.parent?.name == "Custom Character")
                ?? animators.FirstOrDefault(a => a.isHuman && a.gameObject.scene.name == "MainMenu");
            var sourceBike = animators.FirstOrDefault(a => a.name == "Bike Skeleton" && a.gameObject.scene.name == "MainMenu");
            if (sourceRider == null || sourceBike == null) throw new InvalidOperationException("The menu rider and BMX are still loading. Try again in a moment.");
            var riderMap = new Dictionary<int, Transform>();
            var riderRoot = CloneTree(sourceRider.transform, root.transform, riderMap);
            riderRoot.gameObject.SetActive(true);
            riderRoot.localPosition = Vector3.zero; riderRoot.localRotation = Quaternion.identity; riderRoot.localScale = sourceRider.transform.lossyScale;
            var riderBinding = new StudioSkinBinding(sourceRider, riderMap);
            CopyRenderers(sourceRider.transform, riderMap, riderBinding);
            Rider = riderRoot.gameObject.AddComponent<Animator>(); Rider.avatar = sourceRider.avatar;
            Rider.applyRootMotion = false; Rider.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var headGear = riderRoot.Find("HeadGear"); var head = Rider.GetBoneTransform(HumanBodyBones.Head);
            if (headGear != null && head != null) headGear.SetParent(head, true);
            riderPlayer = riderRoot.gameObject.AddComponent<AnimancerComponent>(); riderPlayer.Animator = Rider;

            Transform sourceBikeRoot = sourceBike.transform.parent;
            var bikeMap = new Dictionary<int, Transform>();
            var bikeRoot = CloneTree(sourceBikeRoot, root.transform, bikeMap);
            bikeRoot.gameObject.SetActive(true);
            bikeRoot.localPosition = Vector3.zero; bikeRoot.localRotation = Quaternion.identity; bikeRoot.localScale = sourceBikeRoot.lossyScale;
            CopyRenderers(sourceBikeRoot, bikeMap);
            Transform bikeRig = bikeMap[sourceBike.transform.GetInstanceID()];
            bikeRig.gameObject.SetActive(true);
            foreach (Transform joint in bikeRig.GetComponentsInChildren<Transform>(true)) joint.gameObject.SetActive(true);
            Bike = bikeRig.gameObject.AddComponent<Animator>(); Bike.avatar = sourceBike.avatar; Bike.applyRootMotion = false; Bike.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Bind physical visuals against a neutral animation pose, rather
            // than whichever unanimated rig pose the menu happened to leave.
            var neutral = Resources.FindObjectsOfTypeAll<AnimationClip>().FirstOrDefault(c => c.name == "BMX@BMX_Moto Left_Enter");
            if (neutral != null) ClipCopy(neutral).SampleAnimation(Bike.gameObject, 0);
            AttachBikeVisuals(sourceBikeRoot, bikeMap);
            geometry = StudioBikeGeometry.Get(Bike, bikeRoot);
            for (int i = 0; i < 4; i++)
            {
                var original = sourceBikeRoot.Find(StudioContacts.BikeTargets[i]);
                var parent = StudioContacts.BikeJoint(Bike, i, 0);
                if (original != null && parent != null && bikeMap.TryGetValue(original.GetInstanceID(), out var target))
                {
                    BindContact(target, parent, original.GetComponent<IKTarget>());
                    bikeContacts[(i, 0)] = target;
                }
            }
            // Streets also animates target IDs: hands can move to the seat and
            // feet to the rear tire, front pegs or front tire. Keep those goals
            // on the steering/frame assembly rather than a spinning wheel.
            foreach (var original in sourceBikeRoot.GetComponentsInChildren<IKTarget>(true))
            {
                if (original.TargetID == 0) continue; // Use the actual BMX pedals/grips above, excluding car duplicates.
                int limb = original.GoalType switch { AvatarIKGoal.LeftHand => 0, AvatarIKGoal.RightHand => 1, AvatarIKGoal.LeftFoot => 2, AvatarIKGoal.RightFoot => 3, _ => -1 };
                if (limb < 0 || !bikeMap.TryGetValue(original.transform.GetInstanceID(), out var target)) continue;
                var parent = Bike.transform.Find(StudioTargets.BikePaths[limb >= 2 && original.TargetID >= 2 ? 1 : 0]);
                if (!original.gameObject.activeInHierarchy || parent == null || bikeContacts.ContainsKey((limb, original.TargetID))) continue;
                BindContact(target, parent, original);
                bikeContacts[(limb, original.TargetID)] = target;
            }
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) rest.Add((node, node.localPosition, node.localRotation));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Studio floor";
            floor.transform.SetParent(root.transform, false); floor.transform.localPosition = new Vector3(0, -.08f, 0); floor.transform.localScale = new Vector3(18, .1f, 18);
            UnityEngine.Object.Destroy(floor.GetComponent<Collider>());
            var material = new Material(Shader.Find("HDRP/Lit")); material.SetColor("_BaseColor", new Color(.13f, .15f, .18f));
            material.SetFloat("_Smoothness", .2f); floor.GetComponent<Renderer>().sharedMaterial = material; owned.Add(material);
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;

            var cameraObject = new GameObject("Studio camera"); cameraObject.transform.SetParent(root.transform, false);
            Camera = cameraObject.AddComponent<UnityEngine.Camera>(); Camera.cullingMask = PreviewMask;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.07f, .085f, .11f);
            Camera.nearClipPlane = .02f; Camera.farClipPlane = 60; Camera.fieldOfView = 38;
            var hd = cameraObject.AddComponent<HDAdditionalCameraData>(); hd.volumeLayerMask = PreviewMask;
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color; hd.backgroundColorHDR = new Color(.07f, .085f, .11f);
            var volumeObject = new GameObject("RoweMod Studio exposure") { layer = PreviewLayer };
            volumeObject.transform.SetParent(root.transform, false);
            // Keep the fixed exposure local even for game cameras whose volume mask includes the preview layer.
            var volumeBounds = volumeObject.AddComponent<BoxCollider>();
            volumeBounds.isTrigger = true;
            volumeBounds.size = Vector3.one * 24f;
            var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = false; volume.priority = 10000;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.sharedProfile = profile;
            var exposure = profile.Add<Exposure>(); exposure.active = true;
            exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(13f);
            AddPreviewLight("Key", new Vector3(35, -35, 0), 28000, new Color(1f, .93f, .84f));
            AddPreviewLight("Fill", new Vector3(25, 145, 0), 14000, new Color(.63f, .76f, 1f));
            Resize(1100, 800); Orbit();
            Log.Msg($"[AnimationStudio] Source rider={sourceRider.name} active={sourceRider.gameObject.activeInHierarchy}; bike={sourceBikeRoot.name} active={sourceBikeRoot.gameObject.activeInHierarchy}; bike renderers={sourceBikeRoot.GetComponentsInChildren<Renderer>(true).Length}.");
            Log.Msg($"[AnimationStudio] Visual-only stage ready: {skins.Count} skinned renderers; clothing bones rebound={riderBinding.Rebound}, helpers reparented={riderBinding.Reparented}, helpers added={riderBinding.AddedHelpers}; no gameplay or network components cloned.");
        }
        private void BindContact(Transform target, Transform bikeJoint, IKTarget native)
        {
            if (native == null || !native._hasWristRollParent)
            {
                target.SetParent(bikeJoint, true);
                return;
            }

            // Keep the target under its roller. Use the grip's unrolled basis
            // when carrying the roller along with the animated bike joint.
            var pivot = target.parent;
            pivot.localRotation = Quaternion.identity;
            pivot.SetParent(bikeJoint, true);
            float offset = native.WristRollForwardOffset;
            wristRollers[target] = (pivot, pivot.localRotation,
                (native._isLeftWrist ? 180f + offset : -offset) + native.ExtraWristRoll);
        }

        private void RollWrist(int limb, Transform target)
        {
            if (!wristRollers.TryGetValue(target, out var roller)) return;
            var elbow = Rider.GetBoneTransform(StudioContacts.End[limb])?.parent;
            if (elbow == null) return;

            // UnityIKLimb projects the elbow onto the plane perpendicular to
            // the unrolled grip axis before turning the roller about local Z.
            var pivot = roller.pivot;
            pivot.localRotation = roller.basis;
            Vector3 axis = pivot.forward;
            Vector3 forearm = Vector3.ProjectOnPlane(elbow.position - pivot.position, axis);
            float roll = Vector3.SignedAngle(forearm, -pivot.right, -axis) + roller.offset;
            pivot.localRotation = roller.basis * Quaternion.AngleAxis(roll, Vector3.forward);
        }
        private void AddPreviewLight(string name, Vector3 rotation, float luxAtCenter, Color color)
        {
            // HDRP directional lights have infinite reach. Aim bounded spots at the isolated preview stage.
            var lightObject = new GameObject(name) { layer = PreviewLayer };
            lightObject.transform.SetParent(root.transform, false);
            var orientation = Quaternion.Euler(rotation);
            lightObject.transform.localRotation = orientation;
            lightObject.transform.localPosition = Vector3.up * .95f - orientation * Vector3.forward * PreviewLightDistance;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.shadows = LightShadows.None;
            light.cullingMask = PreviewMask;
            light.bounceIntensity = 0f;

            var hdLight = lightObject.AddComponent<HDAdditionalLightData>();
            hdLight.range = 44f;
            hdLight.SetSpotAngle(100f, 75f);
            hdLight.SetSpotLightLuxAt(luxAtCenter, PreviewLightDistance);
            hdLight.fadeDistance = 60f;
            hdLight.volumetricDimmer = 0f;
        }
        private Transform CloneTree(Transform source, Transform parent, Dictionary<int, Transform> map)
        {
            var copy = new GameObject(source.name); copy.layer = PreviewLayer;
            copy.transform.SetParent(parent, false); copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation; copy.transform.localScale = source.localScale;
            map[source.GetInstanceID()] = copy.transform;
            for (int i = 0; i < source.childCount; i++) CloneTree(source.GetChild(i), copy.transform, map);
            copy.SetActive(source.gameObject.activeSelf); return copy.transform;
        }
        private void CopyRenderers(Transform source, Dictionary<int, Transform> map, StudioSkinBinding riderBinding = null)
        {
            // Native equips may point at an equivalent live skeleton outside their
            // menu subtree. Bind those references to this preview's canonical rig.
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var reference = source.Find("Character_Reference") ?? source;
            foreach (Transform bone in reference.GetComponentsInChildren<Transform>(true))
                if (map.TryGetValue(bone.GetInstanceID(), out var copy)) byName.TryAdd(bone.name, copy);
            foreach (Transform bone in source.GetComponentsInChildren<Transform>(true))
                if (map.TryGetValue(bone.GetInstanceID(), out var copy)) byName.TryAdd(bone.name, copy);
            Transform Resolve(Transform bone) => riderBinding != null ? riderBinding.Resolve(bone) :
                bone == null ? null : map.TryGetValue(bone.GetInstanceID(), out var copy) ? copy : byName.TryGetValue(bone.name, out copy) ? copy : null;
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (!map.TryGetValue(renderer.transform.GetInstanceID(), out var target)) continue;
                var skin = renderer.TryCast<SkinnedMeshRenderer>();
                Renderer result;
                if (skin != null)
                {
                    if (!StudioClothMesh.TrySource(skin, out var sourceMesh, out var sourceBones))
                    {
                        Log.Warning($"[AnimationStudio] Skipping preview renderer {skin.name}: no valid original skin mesh/bone slots.");
                        continue;
                    }
                    var bones = sourceBones.Select(Resolve).ToArray();
                    var missing = sourceBones.Where((b, i) => b != null && bones[i] == null).Select(b => b.name).Take(8).ToArray();
                    if (missing.Length > 0)
                    {
                        Log.Warning($"[AnimationStudio] Skipping preview renderer {skin.name} with unmapped bones: {string.Join(", ", missing)}");
                        continue;
                    }
                    var copy = target.gameObject.AddComponent<SkinnedMeshRenderer>(); copy.sharedMesh = sourceMesh;
                    copy.bones = bones; copy.rootBone = Resolve(skin.rootBone) ?? target;
                    copy.localBounds = sourceMesh == skin.sharedMesh ? skin.localBounds : sourceMesh.bounds;
                    copy.updateWhenOffscreen = true; skins.Add(copy); result = copy;
                    if (sourceMesh != skin.sharedMesh)
                        Log.Msg($"[AnimationStudio] Restored pre-simulation cloth mesh for {skin.name}: {sourceMesh.name}, {bones.Length} original bone slots.");
                    if (riderBinding != null)
                    {
                        int rebound = sourceBones.Where((b, i) => b != null &&
                            (!map.TryGetValue(b.GetInstanceID(), out var cloned) || bones[i] != cloned)).Count();
                        if (rebound > 0) Log.Msg($"[AnimationStudio] Bound {skin.name} ({skin.sharedMesh?.name}) to preview avatar: {rebound}/{bones.Length} bone references redirected from another armature.");
                    }
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>(); if (filter?.sharedMesh == null) continue;
                    target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh; result = target.gameObject.AddComponent<MeshRenderer>();
                }
                result.sharedMaterials = renderer.sharedMaterials; result.enabled = renderer.enabled;
                result.forceRenderingOff = renderer.forceRenderingOff;
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties); result.SetPropertyBlock(properties);
                result.shadowCastingMode = renderer.shadowCastingMode; result.receiveShadows = renderer.receiveShadows;
            }
        }
        private void AttachBikeVisuals(Transform sourceRoot, Dictionary<int, Transform> map)
        {
            // Reuse the paired Unity workbench's physical visual-group mapping.
            string[] groups = { "Chassis Visuals", "Headset Visuals", "Front Wheel Visuals", "Back Wheel Visuals", "Back Wheel Back", "BB Visuals", "Left Pedal Visuals", "Right Pedal Visuals" };
            int[] targets = { 0, 1, 1, 6, 0, 2, 3, 4 };
            for (int i = 0; i < groups.Length; i++)
            {
                var source = sourceRoot.Find(groups[i]); var joint = Bike.transform.Find(StudioTargets.BikePaths[targets[i]]);
                if (source != null && joint != null && map.TryGetValue(source.GetInstanceID(), out var visual)) visual.SetParent(joint, true);
            }
            var front = sourceRoot.Find("Front Wheel Front");
            if (front != null)
                foreach (Transform node in front.GetComponentsInChildren<Transform>(true))
                {
                    var joint = Bike.transform.Find(StudioTargets.BikePaths[node.name.Contains("Pegs") ? 1 : 5]);
                    if ((node.name == "Wheel Mesh" || node.name.Contains("Pegs")) && joint != null && map.TryGetValue(node.GetInstanceID(), out var visual)) visual.SetParent(joint, true);
                }
            // Split fixed bearings and pedal axles out of rotating visual groups.
            // Check every rendered part so no mesh silently keeps a wrong joint.
            foreach (var renderer in sourceRoot.GetComponentsInChildren<Renderer>(true))
            {
                var names = new Stack<string>();
                for (var node = renderer.transform; node != null && node != sourceRoot; node = node.parent) names.Push(node.name);
                string path = string.Join("/", names);
                int index = StudioBikePartMap.Joint(path);
                if (index < 0)
                {
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                        Log.Warning($"[AnimationStudio] Unmapped bike renderer: {path}");
                    continue;
                }
                if (!map.TryGetValue(renderer.transform.GetInstanceID(), out var visual)) continue;
                var joint = Bike.transform.Find(StudioTargets.BikePaths[index]);
                if (joint == null) continue;
                for (var parent = visual.parent; parent != null; parent = parent.parent)
                {
                    if (parent == joint) break;
                    if (Array.IndexOf(StudioTargets.BikePaths, RelativeBikePath(parent)) >= 0)
                    { visual.SetParent(joint, true); break; }
                }
            }
        }
        private string RelativeBikePath(Transform node)
        {
            var names = new Stack<string>();
            for (; node != null && node != Bike.transform; node = node.parent) names.Push(node.name);
            return string.Join("/", names);
        }
        private AnimationClip ClipCopy(AnimationClip original)
        {
            if (original == null) return null;
            int id = original.GetInstanceID();
            if (!previewClips.TryGetValue(id, out var clip))
            {
                clip = UnityEngine.Object.Instantiate(original); clip.events = Array.Empty<AnimationEvent>();
                previewClips[id] = clip; owned.Add(clip);
            }
            return clip;
        }
        private void SampleClips(StudioSource source, int phase, float time)
        {
            foreach (var pose in rest) if (pose.node != null) { pose.node.localPosition = pose.position; pose.node.localRotation = pose.rotation; }
            var riderClip = ClipCopy(source.Clip(phase, false)); var bikeClip = ClipCopy(source.Clip(phase, true));
            if (riderClip != null) { var state = riderPlayer.Play(riderClip); state.Speed = 0; state.Time = time * riderClip.length; riderPlayer.Evaluate(0); }
            // Sample Generic Joints/Controls curves directly on a private clip
            // copy so arbitrary scrubbing evaluates the complete hierarchy.
            if (bikeClip != null) bikeClip.SampleAnimation(Bike.gameObject, time * bikeClip.length);
        }
        internal void Sample(StudioSource source, StudioVariation variation, int phase, float time, string draftTarget, StudioPose? draft)
        {
            SamplePose(source, variation, phase, time, draftTarget, draft, true);
        }

        private void SamplePose(StudioSource source, StudioVariation variation, int phase, float time, string draftTarget,
            StudioPose? draft, bool solveContacts)
        {
            if (source == null || root == null) return;
            SampleClips(source, phase, time);
            editBases.Clear();
            var goals = new (Transform joint, Vector3 local, Quaternion rotation)[4];
            for (int i = 0; i < 4; i++)
            {
                var control = Rider.transform.Find(StudioTargets.ContactPaths[i]);
                int id = SampledContactTarget(i);
                var contactKey = variation?.ContactKeyAt(phase, i, time);
                bool anchored = contactKey?.Mode == StudioContactMode.Locked && contactKey.HasAnchor;
                var joint = StudioContacts.BikeJoint(Bike, i, anchored ? contactKey.TargetId : id);
                var end = Rider.GetBoneTransform(StudioContacts.End[i]);
                ContactWeights[i] = StudioContacts.Weight(variation, phase, i, time, control?.localPosition.y ?? 0);
                if (joint != null && anchored)
                    goals[i] = (joint, new Vector3(contactKey.AnchorPosition.X, contactKey.AnchorPosition.Y, contactKey.AnchorPosition.Z),
                        Quaternion.Euler(contactKey.AnchorRotation.X, contactKey.AnchorRotation.Y, contactKey.AnchorRotation.Z));
                else if (ContactWeights[i] > .0001f && bikeContacts.TryGetValue((i, contactKey?.UseBikeTarget == true ? contactKey.TargetId : id), out var target))
                    goals[i] = (target, Vector3.zero, StudioContacts.BoneRotationFromGoal(i, Quaternion.identity));
                else if (joint != null && end != null)
                    goals[i] = (joint, joint.InverseTransformPoint(end.position), Quaternion.Inverse(joint.rotation) * end.rotation);
            }
            // Equip targets follow their physical joints through bearing repair and authored offsets.
            geometry?.ConstrainNativeBike();
            foreach (string target in StudioTargets.Rider.Concat(StudioTargets.Bike))
            {
                var node = Target(target); if (node == null) continue;
                editBases[target] = geometry != null && target.StartsWith("Bike:", StringComparison.Ordinal) ? geometry.ControlBasis(node) : node.rotation;
                var pose = draft.HasValue && draftTarget == target ? draft.Value : variation?.Evaluate(phase, target, time) ?? default;
                if (geometry != null && target.StartsWith("Bike:", StringComparison.Ordinal)) geometry.Apply(node, pose, 1);
                else { node.localRotation *= Quaternion.Euler(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z);
                    node.localPosition += new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z); }
            }
            var blend = variation?.BlendInto(phase);
            if (blend != null)
            {
                float weight = blend.Weight(blend.FromPhase, time);
                if (weight > 0f)
                    foreach (var offset in blend.Offsets)
                        StudioRuntimePlayer.ApplyOffset(Target(offset.Target), new StudioPose(offset.Rotation, offset.Position), weight,
                            offset.Target.StartsWith("Bike:", StringComparison.Ordinal) ? geometry : null);
            }
            if (!solveContacts) return;
            for (int i = 0; i < 4; i++)
            {
                var goal = goals[i]; if (goal.joint == null) { ContactGaps[i] = 0f; continue; }
                RollWrist(i, goal.joint);
                var position = goal.joint.TransformPoint(goal.local); var rotation = goal.joint.rotation * goal.rotation;
                StudioContacts.Solve(Rider, i, position, rotation, ContactWeights[i], 1, false);
                ContactGaps[i] = StudioContacts.Gap(Rider, i, position, rotation, false);
            }
            Orbit();
        }

        internal StudioPhaseBlend BuildBlend(StudioSource source, StudioVariation variation, int fromPhase, int toPhase, float duration)
        {
            SamplePose(source, variation, fromPhase, 1f, null, null, false);
            var outgoing = new List<(string target, Transform node, Vector3 position, Quaternion rotation)>();
            foreach (string target in StudioTargets.Rider.Concat(StudioTargets.Bike))
            {
                var node = Target(target);
                if (node != null) outgoing.Add((target, node, node.localPosition, node.localRotation));
            }

            // Measure the correction against the incoming authored pose. Keep
            // the original curves so the correction can be adjusted or removed.
            SamplePose(source, variation, toPhase, 0f, null, null, false);
            var blend = new StudioPhaseBlend { FromPhase = fromPhase, Phase = toPhase, Duration = duration };
            foreach (var (target, node, position, rotation) in outgoing)
            {
                var bikeGeometry = target.StartsWith("Bike:", StringComparison.Ordinal) ? geometry : null;
                var correction = RebasePose(node, position, rotation, bikeGeometry);
                blend.Offsets.Add(new StudioBlendOffset { Target = target, Rotation = correction.Rotation, Position = correction.Position });
            }
            return blend;
        }

        private static StudioPose RebasePose(Transform node, Vector3 position, Quaternion rotation, StudioBikeGeometry bikeGeometry)
        {
            Quaternion delta = Quaternion.Inverse(node.localRotation) * rotation;
            if (bikeGeometry != null)
            {
                Quaternion basis = bikeGeometry.ControlBasis(node);
                Quaternion desiredWorld = node.parent.rotation * rotation;
                delta = Quaternion.Inverse(basis) * desiredWorld * Quaternion.Inverse(node.rotation) * basis;
                Vector3 bikeAngles = delta.eulerAngles;
                bikeGeometry.Apply(node, new StudioPose(new StudioVector(bikeAngles.x, bikeAngles.y, bikeAngles.z), default), 1f);
            }
            Vector3 angles = delta.eulerAngles;
            Vector3 offset = position - node.localPosition;
            if (bikeGeometry == null)
            {
                node.localPosition = position;
                node.localRotation = rotation;
            }
            else
            {
                // Apply translation through the bearing so frame followers move too.
                bikeGeometry.Apply(node, new StudioPose(default, new StudioVector(offset.x, offset.y, offset.z)), 1f);
            }
            return new StudioPose(new StudioVector(Mathf.DeltaAngle(0f, angles.x), Mathf.DeltaAngle(0f, angles.y),
                Mathf.DeltaAngle(0f, angles.z)), new StudioVector(offset.x, offset.y, offset.z));
        }
        internal void CopyEndToExit(StudioSource source, StudioVariation variation, int fromPhase, float nextExitFrame)
        {
            const int exit = 3;
            Sample(source, variation, fromPhase, 1f, null, null);
            var frame = new List<(string target, Transform node, Vector3 position, Quaternion rotation)>();
            foreach (string target in StudioTargets.Rider.Concat(StudioTargets.Bike))
            {
                var node = Target(target);
                if (node != null) frame.Add((target, node, node.localPosition, node.localRotation));
            }

            for (int limb = 0; limb < StudioTargets.Effectors.Length; limb++)
            {
                var previous = variation.ContactKeyAt(exit, limb, 0f);
                if (!variation.Contacts.Any(key => key.Phase == exit && key.Limb == limb && key.Time > .00001f && key.Time <= nextExitFrame + .00001f))
                {
                    variation.SetContact(exit, limb, nextExitFrame, previous?.Mode ?? StudioContactMode.Auto);
                    var restore = variation.ContactKeyAt(exit, limb, nextExitFrame);
                    restore.UseBikeTarget = previous?.UseBikeTarget == true;
                    restore.TargetId = previous?.TargetId ?? 0;
                    if (previous?.HasAnchor == true)
                    {
                        restore.HasAnchor = true;
                        restore.TargetId = previous.TargetId;
                        restore.AnchorPosition = previous.AnchorPosition;
                        restore.AnchorRotation = previous.AnchorRotation;
                    }
                }
                bool planted = ContactWeights[limb] >= .95f;
                variation.SetContact(exit, limb, 0f, planted ? StudioContactMode.Locked : StudioContactMode.Free);
                if (planted)
                {
                    var sourceContact = variation.ContactKeyAt(fromPhase, limb, 1f);
                    var copied = variation.ContactKeyAt(exit, limb, 0f);
                    copied.UseBikeTarget = sourceContact?.UseBikeTarget == true;
                    copied.TargetId = sourceContact != null && (sourceContact.HasAnchor || sourceContact.UseBikeTarget) ?
                        sourceContact.TargetId : SampledContactTarget(limb);
                }
            }

            SampleClips(source, exit, 0f);
            geometry?.ConstrainNativeBike();
            // Rebase the sampled local pose against Exit's own first frame.
            // Bike keys rotate around the physical bearing in its control basis.
            foreach (var (target, node, position, rotation) in frame)
            {
                var bikeGeometry = target.StartsWith("Bike:", StringComparison.Ordinal) ? geometry : null;
                variation.SetKey(exit, target, 0f, RebasePose(node, position, rotation, bikeGeometry));
            }

            // Measure the planted contacts against the rebased frame. Unkeyed parent
            // transforms can differ between clips even when the copied local pose matches.
            for (int limb = 0; limb < StudioTargets.Effectors.Length; limb++)
            {
                var key = variation.ContactKeyAt(exit, limb, 0f);
                if (key.Mode == StudioContactMode.Locked) CaptureContact(key, key.TargetId);
            }
        }
        internal Transform Target(string name) => StudioRuntimePlayer.Target(Rider, Bike, name);
        internal bool HasContactTarget(int limb, int id) => bikeContacts.ContainsKey((limb, id));
        internal int SampledContactTarget(int limb) =>
            StudioContacts.TargetIDFromControl(Rider.transform.Find(StudioTargets.ContactIdPaths[limb])?.localPosition.y ?? 0);
        internal Vector3 ControlPivot(string target) => geometry != null && target.StartsWith("Bike:", StringComparison.Ordinal) ? geometry.ControlPivot(Target(target)) : Target(target)?.position ?? origin;
        internal Quaternion ControlRotation(string target, StudioPose offset) =>
            (editBases.TryGetValue(target, out var basis) ? basis : Target(target)?.rotation ?? Quaternion.identity) * Quaternion.Euler(offset.Rotation.X, offset.Rotation.Y, offset.Rotation.Z);
        internal void CaptureContact(StudioContactKey key)
        {
            CaptureContact(key, SampledContactTarget(key.Limb));
        }
        private void CaptureContact(StudioContactKey key, int id)
        {
            var joint = StudioContacts.BikeJoint(Bike, key.Limb, id); var end = Rider.GetBoneTransform(StudioContacts.End[key.Limb]);
            if (joint == null || end == null) return;
            var p = joint.InverseTransformPoint(end.position); var r = (Quaternion.Inverse(joint.rotation) * end.rotation).eulerAngles;
            key.HasAnchor = true; key.TargetId = id; key.AnchorPosition = new StudioVector(p.x, p.y, p.z); key.AnchorRotation = new StudioVector(r.x, r.y, r.z);
        }
        internal void Orbit()
        {
            if (Camera == null) return;
            Vector3 center = origin + Vector3.up * .95f + Pan;
            Camera.transform.position = center + Quaternion.Euler(Pitch, Yaw, 0) * new Vector3(0, 0, -Distance);
            Camera.transform.LookAt(center);
        }
        internal void Resize(int width, int height)
        {
            float fit = Mathf.Min(1f, 2000f / Mathf.Max(1, width), 1600f / Mathf.Max(1, height));
            width = Mathf.Max(32, Mathf.RoundToInt(width * fit)); height = Mathf.Max(32, Mathf.RoundToInt(height * fit));
            if (Image != null && Image.width == width && Image.height == height) return;
            if (Image != null) { Camera.targetTexture = null; Image.Release(); UnityEngine.Object.Destroy(Image); }
            Image = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); Image.Create(); Camera.targetTexture = Image;
        }
        public void Dispose()
        {
            if (Camera != null) { Camera.enabled = false; Camera.targetTexture = null; }
            if (riderPlayer != null) riderPlayer.Stop();
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            if (Image != null) { Image.Release(); UnityEngine.Object.Destroy(Image); Image = null; }
            foreach (var value in owned) if (value != null) UnityEngine.Object.Destroy(value); owned.Clear();
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
