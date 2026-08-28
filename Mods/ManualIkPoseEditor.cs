using System;
using Il2CppMashBox.Addons.PhysicsDrivenAnimation;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Animator_Motion_Systems;
using Il2CppMashBox.Core.Runtime.Common;
using Il2CppMashBox.Core.Runtime.InverseKinematics;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace rowemod.Mods
{
    /// <summary>
    /// Manual/nose-manual foot and hip pose override based on DeeTRIX's Rocket
    /// Manual target ownership pattern. References are resolved only when the
    /// pose starts and all native targets are restored when it ends.
    /// </summary>
    public static class ManualIkPoseEditor
    {
        private enum HandleKind { None, LeftFoot, RightFoot, Hips }

        private static VehicleController _vehicle;
        private static Animator _animator;
        private static HumanIK _humanIk;
        private static Transform _leftPeg;
        private static Transform _rightPeg;
        private static Transform _leftFoot;
        private static Transform _rightFoot;
        private static Transform _leftKnee;
        private static Transform _rightKnee;
        private static Transform _hips;
        private static IIKChain _leftChain;
        private static IIKChain _rightChain;
        private static UnityIKLimb _leftLimb;
        private static UnityIKLimb _rightLimb;
        private static VehicleFootPedalAnimationRig _pedalRig;
        private static Transform _throttleTarget;
        private static Transform _brakeTarget;
        private static bool _pedalTargetsCaptured;
        private static Vector3 _throttleTargetLocalPosition;
        private static Vector3 _brakeTargetLocalPosition;
        private static Quaternion _throttleTargetLocalRotation;
        private static Quaternion _brakeTargetLocalRotation;
        private static FullBodyMotionBehaviour _fullBodyMotion;
        private static IKTarget _leftTarget;
        private static IKTarget _rightTarget;
        private static bool _leftTargetCaptured;
        private static bool _rightTargetCaptured;
        private static Vector3 _leftTargetLocalPosition;
        private static Vector3 _rightTargetLocalPosition;
        private static Quaternion _leftTargetLocalRotation;
        private static Quaternion _rightTargetLocalRotation;
        private static Quaternion _leftFootRootRotation;
        private static Quaternion _rightFootRootRotation;
        private static Vector3 _leftKneeRootSpace;
        private static Vector3 _rightKneeRootSpace;
        private static Vector3 _neutralHipsRootSpace;
        private static Quaternion _hipsRootRotation;
        private static Vector3 _smoothedHipsOffset;
        private static bool _active;
        private static bool _editing;
        private static string _status = "Enter a map to resolve rider IK targets.";

        private static GameObject _leftHandle;
        private static GameObject _rightHandle;
        private static GameObject _hipsHandle;
        private static HandleKind _dragging;
        private static Plane _dragPlane;
        private static Vector3 _dragOffset;

        public static bool RuntimeEnabled =>
            LateNativeHooks.ManualIkReady && (_editing || (Config.manualIkPoseSettings?.enabled ?? false));

        public static bool IsEditing => _editing;
        public static string Status => LateNativeHooks.ManualIkReady ? _status :
            "Manual IK hooks unavailable on this game build; pose editing is inactive. Check the log.";

        public static void SetEditing(bool editing)
        {
            _editing = editing;
            if (!editing)
            {
                _dragging = HandleKind.None;
                DestroyHandles();
            }
        }

        public static void NotifySettingsChanged()
        {
            if (!RuntimeEnabled)
                ReleasePose();
        }

        public static void OnSceneInitialized(bool gameplayScene)
        {
            Cleanup();
            _status = gameplayScene
                ? "Ready. Start a manual or open the world editor."
                : "Enter a map to resolve rider IK targets.";
        }

        public static void LateUpdate()
        {
            ManualIkPoseSettings settings = Config.manualIkPoseSettings ??= new ManualIkPoseSettings();
            VehicleController vehicle = VehicleController.LocalDriverVehicle;
            bool preview = _editing && Menu.isOpen && Menu.currentTab == Menu.Tab.RiderTools;
            if (_editing && !preview)
            {
                _editing = false;
                _dragging = HandleKind.None;
                DestroyHandles();
            }
            bool manualActive = settings.enabled && vehicle != null &&
                ((settings.applyDuringManual && vehicle.IsManny) ||
                 (settings.applyDuringNoseManual && vehicle.IsNosey));

            if (!preview && !manualActive)
            {
                ReleasePose();
                return;
            }

            if (vehicle == null)
            {
                _status = "Waiting for the local bike and rider.";
                ReleasePose();
                return;
            }

            if (!_active || _vehicle != vehicle)
            {
                ReleasePose();
                if (!ResolvePose(vehicle))
                    return;
            }

            ApplyChainTargets();
            // The native pedal rig can retarget the feet again when drive direction
            // changes. DeeTRIX's Rocket Manual owns these two targets through the end
            // of LateUpdate, then reapplies the captured neutral foot rotations.
            ApplyPedalTargets();
            ApplyFootTransforms();
            ApplyHipsTransform();
            if (preview)
            {
                EnsureHandles();
                UpdateHandles();
                HandleDragging();
            }
            else
            {
                DestroyHandles();
            }

            Main.NotifyRuntimeContributionApplied();
        }

        public static void FixedUpdate()
        {
            if (!_active || _fullBodyMotion == null)
                return;

            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            Vector3 wanted = settings != null && settings.hipsEnabled
                ? new Vector3(settings.hipsX, settings.hipsY, settings.hipsZ)
                : Vector3.zero;
            float t = 1f - Mathf.Exp(-10f * Mathf.Max(Time.fixedDeltaTime, 0.0001f));
            _smoothedHipsOffset = Vector3.Lerp(_smoothedHipsOffset, wanted, t);
            if (_smoothedHipsOffset.sqrMagnitude > 0.00000001f)
                _fullBodyMotion.AddToWantedHipsPos(_smoothedHipsOffset);
        }

        public static void Cleanup()
        {
            _editing = false;
            ReleasePose();
            DestroyHandles();
        }

        private static bool ResolvePose(VehicleController vehicle)
        {
            try
            {
                VehicleAnimationInputHandler handler = vehicle.GetComponent<VehicleAnimationInputHandler>() ??
                    vehicle.GetComponentInChildren<VehicleAnimationInputHandler>(true) ??
                    vehicle.GetComponentInParent<VehicleAnimationInputHandler>();
                if (handler == null || handler._humanAnimator == null)
                {
                    _status = "Rider animator is not available yet.";
                    return false;
                }

                _vehicle = vehicle;
                _animator = handler._humanAnimator.Animator;
                _humanIk = handler._humanAnimator.GetComponent<HumanIK>() ??
                    handler._humanAnimator.GetComponentInChildren<HumanIK>() ??
                    handler._humanAnimator.GetComponentInParent<HumanIK>();
                FindRearPegs(vehicle, out _leftPeg, out _rightPeg);
                if (_animator == null || _humanIk == null || _leftPeg == null || _rightPeg == null)
                {
                    _status = "Could not resolve animator, HumanIK, or both rear pegs.";
                    ClearReferences();
                    return false;
                }

                _leftLimb = _humanIk._leftFootIK?.UnityObject as UnityIKLimb;
                _rightLimb = _humanIk._rightFootIK?.UnityObject as UnityIKLimb;
                _leftChain = _humanIk._leftFootIK?.Interface;
                _rightChain = _humanIk._rightFootIK?.Interface;
                _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                _leftKnee = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _rightKnee = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
                if (_leftFoot == null || _rightFoot == null || _leftKnee == null ||
                    _rightKnee == null || _hips == null)
                {
                    _status = "The rider humanoid bones are incomplete.";
                    ClearReferences();
                    return false;
                }

                Transform root = _animator.transform;
                _leftFootRootRotation = Quaternion.Inverse(root.rotation) * _leftFoot.rotation;
                _rightFootRootRotation = Quaternion.Inverse(root.rotation) * _rightFoot.rotation;
                _leftKneeRootSpace = root.InverseTransformPoint(_leftKnee.position);
                _rightKneeRootSpace = root.InverseTransformPoint(_rightKnee.position);
                _neutralHipsRootSpace = root.InverseTransformPoint(_hips.position);
                _hipsRootRotation = Quaternion.Inverse(root.rotation) * _hips.rotation;
                _pedalRig = vehicle.GetComponentInChildren<VehicleFootPedalAnimationRig>(true);
                _throttleTarget = _pedalRig != null ? _pedalRig._throttleTarget : null;
                _brakeTarget = _pedalRig != null ? _pedalRig._brakeTarget : null;
                if (_throttleTarget != null)
                {
                    _throttleTargetLocalPosition = _throttleTarget.localPosition;
                    _throttleTargetLocalRotation = _throttleTarget.localRotation;
                }
                if (_brakeTarget != null)
                {
                    _brakeTargetLocalPosition = _brakeTarget.localPosition;
                    _brakeTargetLocalRotation = _brakeTarget.localRotation;
                }
                _pedalTargetsCaptured = _throttleTarget != null || _brakeTarget != null;
                _fullBodyMotion = FindFullBodyMotion(root);
                _smoothedHipsOffset = Vector3.zero;
                _active = true;
                _status = "IK targets ready. Blue = left foot, red = right foot, yellow = hips.";
                Log.Msg("[ManualIK] Resolved local rider IK, rear pegs, and hip motion target.");
                return true;
            }
            catch (Exception ex)
            {
                _status = "IK setup failed; see the log.";
                Log.Error("[ManualIK] Resolve failed: " + ex);
                ClearReferences();
                return false;
            }
        }

        private static void FindRearPegs(VehicleController vehicle, out Transform left, out Transform right)
        {
            left = null;
            right = null;
            foreach (Transform candidate in vehicle.GetComponentsInChildren<Transform>(true))
            {
                string name = candidate.name.ToLowerInvariant();
                if (!name.Contains("peg") || (!name.Contains("rear") && !name.Contains("back")))
                    continue;
                if (name.Contains("left")) left = candidate;
                if (name.Contains("right")) right = candidate;
            }
        }

        private static FullBodyMotionBehaviour FindFullBodyMotion(Transform owner)
        {
            FullBodyMotionBehaviour closest = null;
            float best = float.MaxValue;
            Transform ownerRoot = owner != null ? owner.root : null;
            foreach (FullBodyMotionBehaviour candidate in UnityEngine.Object.FindObjectsOfType<FullBodyMotionBehaviour>())
            {
                if (candidate == null || (ownerRoot != null && candidate.transform.root != ownerRoot))
                    continue;
                float distance = (candidate.transform.position - owner.position).sqrMagnitude;
                if (closest == null || distance < best)
                {
                    closest = candidate;
                    best = distance;
                }
            }
            return closest;
        }

        private static Vector3 FootWorldPosition(bool left)
        {
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            Transform peg = left ? _leftPeg : _rightPeg;
            Vector3 offset = left
                ? new Vector3(settings.leftFootX, settings.leftFootY, settings.leftFootZ)
                : new Vector3(settings.rightFootX, settings.rightFootY, settings.rightFootZ);
            return peg.position + _animator.transform.TransformVector(offset);
        }

        private static Vector3 HipsWorldPosition()
        {
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            return _animator.transform.TransformPoint(_neutralHipsRootSpace +
                new Vector3(settings.hipsX, settings.hipsY, settings.hipsZ));
        }

        private static Quaternion FootWorldRotation(bool left)
        {
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            Quaternion baseRotation = left ? _leftFootRootRotation : _rightFootRootRotation;
            Vector3 euler = left
                ? new Vector3(settings.leftFootPitch, settings.leftFootYaw, settings.leftFootRoll)
                : new Vector3(settings.rightFootPitch, settings.rightFootYaw, settings.rightFootRoll);
            return _animator.transform.rotation * baseRotation * Quaternion.Euler(euler);
        }

        private static Quaternion HipsWorldRotation()
        {
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            Vector3 euler = new Vector3(settings.hipsPitch, settings.hipsYaw, settings.hipsRoll);
            return _animator.transform.rotation * _hipsRootRotation * Quaternion.Euler(euler);
        }

        private static void ApplyChainTargets()
        {
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            if (settings.leftFootEnabled)
                MoveChainTarget(_leftChain, FootWorldPosition(true), FootWorldRotation(true), ref _leftTarget,
                    ref _leftTargetCaptured, ref _leftTargetLocalPosition, ref _leftTargetLocalRotation);
            else
                RestoreTarget(ref _leftTarget, ref _leftTargetCaptured,
                    _leftTargetLocalPosition, _leftTargetLocalRotation);

            if (settings.rightFootEnabled)
                MoveChainTarget(_rightChain, FootWorldPosition(false), FootWorldRotation(false), ref _rightTarget,
                    ref _rightTargetCaptured, ref _rightTargetLocalPosition, ref _rightTargetLocalRotation);
            else
                RestoreTarget(ref _rightTarget, ref _rightTargetCaptured,
                    _rightTargetLocalPosition, _rightTargetLocalRotation);
        }

        private static void MoveChainTarget(IIKChain chain, Vector3 position, Quaternion rotation,
            ref IKTarget cached, ref bool captured, ref Vector3 originalPosition,
            ref Quaternion originalRotation)
        {
            if (chain?.Target == null) return;
            IKTarget target = chain.Target;
            if (cached != target)
            {
                RestoreTarget(ref cached, ref captured, originalPosition, originalRotation);
                cached = target;
                originalPosition = target.transform.localPosition;
                originalRotation = target.transform.localRotation;
                captured = true;
            }
            target.transform.position = position;
            target.transform.rotation = rotation;
        }

        private static void RestoreTarget(ref IKTarget target, ref bool captured,
            Vector3 position, Quaternion rotation)
        {
            if (captured && target != null)
            {
                target.transform.localPosition = position;
                target.transform.localRotation = rotation;
            }
            target = null;
            captured = false;
        }

        internal static void BeforeAnimatorIk(HumanIK instance)
        {
            if (_active && instance == _humanIk)
                ApplyChainTargets();
        }

        internal static void AfterAnimatorIk(HumanIK instance)
        {
            if (!_active || instance != _humanIk || _animator == null) return;
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            Vector3 hipWorldOffset = _animator.transform.TransformVector(_smoothedHipsOffset);
            if (settings.leftFootEnabled)
                ApplyAnimatorFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee,
                    FootWorldPosition(true), FootWorldRotation(true), _leftKneeRootSpace, hipWorldOffset);
            if (settings.rightFootEnabled)
                ApplyAnimatorFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee,
                    FootWorldPosition(false), FootWorldRotation(false), _rightKneeRootSpace, hipWorldOffset);
            ApplyHipsTransform();
        }

        private static void ApplyAnimatorFoot(AvatarIKGoal goal, AvatarIKHint hint,
            Vector3 position, Quaternion rotation, Vector3 kneeRootSpace, Vector3 hipWorldOffset)
        {
            _animator.SetIKPositionWeight(goal, 1f);
            _animator.SetIKRotationWeight(goal, 1f);
            _animator.SetIKPosition(goal, position);
            _animator.SetIKRotation(goal, rotation);
            _animator.SetIKHintPositionWeight(hint, 1f);
            _animator.SetIKHintPosition(hint,
                _animator.transform.TransformPoint(kneeRootSpace) + hipWorldOffset);
        }

        internal static void BeforeLimbUpdate(UnityIKLimb limb)
        {
            if (!_active || limb == null || _animator == null) return;
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            if (limb == _leftLimb && settings.leftFootEnabled)
                ApplyLimb(limb, FootWorldPosition(true), FootWorldRotation(true));
            else if (limb == _rightLimb && settings.rightFootEnabled)
                ApplyLimb(limb, FootWorldPosition(false), FootWorldRotation(false));
        }

        private static void ApplyLimb(UnityIKLimb limb, Vector3 position, Quaternion rotation)
        {
            limb._goalPosition = position;
            limb._goalRotation = rotation;
            limb.PositionWeight = 1f;
            limb.RotationWeight = 1f;
        }

        private static void ApplyPedalTargets()
        {
            if (!_active || !_pedalTargetsCaptured)
                return;

            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            if (_throttleTarget != null)
            {
                if (settings.rightFootEnabled)
                {
                    _throttleTarget.position = FootWorldPosition(false);
                    _throttleTarget.rotation = FootWorldRotation(false);
                }
                else
                {
                    _throttleTarget.localPosition = _throttleTargetLocalPosition;
                    _throttleTarget.localRotation = _throttleTargetLocalRotation;
                }
            }
            if (_brakeTarget != null)
            {
                if (settings.leftFootEnabled)
                {
                    _brakeTarget.position = FootWorldPosition(true);
                    _brakeTarget.rotation = FootWorldRotation(true);
                }
                else
                {
                    _brakeTarget.localPosition = _brakeTargetLocalPosition;
                    _brakeTarget.localRotation = _brakeTargetLocalRotation;
                }
            }
        }

        private static void ApplyFootTransforms()
        {
            if (!_active || _animator == null)
                return;

            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            if (settings.leftFootEnabled && _leftFoot != null)
                _leftFoot.rotation = FootWorldRotation(true);
            if (settings.rightFootEnabled && _rightFoot != null)
                _rightFoot.rotation = FootWorldRotation(false);
        }

        private static void ApplyHipsTransform()
        {
            if (!_active || _animator == null || _hips == null)
                return;

            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            if (settings.hipsEnabled)
                _hips.rotation = HipsWorldRotation();
        }

        internal static bool AllowPedalRigUpdate(VehicleFootPedalAnimationRig instance)
        {
            if (!_active || instance != _pedalRig) return true;
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            return !settings.leftFootEnabled && !settings.rightFootEnabled;
        }

        private static void EnsureHandles()
        {
            _leftHandle ??= CreateHandle("RoweMod Manual IK - Left Foot", new Color(0.1f, 0.55f, 1f, 0.9f));
            _rightHandle ??= CreateHandle("RoweMod Manual IK - Right Foot", new Color(1f, 0.2f, 0.15f, 0.9f));
            _hipsHandle ??= CreateHandle("RoweMod Manual IK - Hips", new Color(1f, 0.75f, 0.05f, 0.9f));
        }

        private static GameObject CreateHandle(string name, Color color)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = name;
            Collider collider = handle.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            Renderer renderer = handle.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    renderer.material = new Material(shader);
                    renderer.material.color = color;
                }
            }
            return handle;
        }

        private static void UpdateHandles()
        {
            if (_leftHandle != null) SetHandle(_leftHandle, FootWorldPosition(true));
            if (_rightHandle != null) SetHandle(_rightHandle, FootWorldPosition(false));
            if (_hipsHandle != null) SetHandle(_hipsHandle, HipsWorldPosition());
        }

        private static void SetHandle(GameObject handle, Vector3 position)
        {
            handle.transform.position = position;
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            // Keep handles readable at gameplay camera distances. These bounds are
            // intentionally 1.5x the original editor size.
            float scale = camera != null
                ? Mathf.Clamp(Vector3.Distance(camera.transform.position, position) * 0.027f, 0.0675f, 0.27f)
                : 0.12f;
            handle.transform.localScale = Vector3.one * scale;
        }

        private static void HandleDragging()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            Mouse mouse = Mouse.current;
            if (camera == null || mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            Vector2 guiPointer = new Vector2(pointer.x, Screen.height - pointer.y);
            bool overMenu = Menu.windowRect.Contains(guiPointer);

            if (mouse.leftButton.wasPressedThisFrame && !overMenu)
            {
                _dragging = PickHandle(camera, pointer);
                GameObject selected = GetHandle(_dragging);
                if (selected != null)
                {
                    _dragPlane = new Plane(camera.transform.forward, selected.transform.position);
                    Ray ray = camera.ScreenPointToRay(pointer);
                    if (_dragPlane.Raycast(ray, out float enter))
                        _dragOffset = selected.transform.position - ray.GetPoint(enter);
                }
            }

            if (_dragging != HandleKind.None && mouse.leftButton.isPressed)
            {
                Ray ray = camera.ScreenPointToRay(pointer);
                if (_dragPlane.Raycast(ray, out float enter))
                    SetOffsetFromWorld(_dragging, ray.GetPoint(enter) + _dragOffset);
            }

            if (_dragging != HandleKind.None && mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = HandleKind.None;
                Config.RequestSave();
            }
        }

        private static HandleKind PickHandle(UnityEngine.Camera camera, Vector2 pointer)
        {
            HandleKind bestKind = HandleKind.None;
            float best = 34f * 34f;
            CheckHandle(camera, pointer, _leftHandle, HandleKind.LeftFoot, ref bestKind, ref best);
            CheckHandle(camera, pointer, _rightHandle, HandleKind.RightFoot, ref bestKind, ref best);
            CheckHandle(camera, pointer, _hipsHandle, HandleKind.Hips, ref bestKind, ref best);
            return bestKind;
        }

        private static void CheckHandle(UnityEngine.Camera camera, Vector2 pointer, GameObject handle,
            HandleKind kind, ref HandleKind bestKind, ref float best)
        {
            if (handle == null) return;
            Vector3 screen = camera.WorldToScreenPoint(handle.transform.position);
            if (screen.z <= 0f) return;
            float distance = ((Vector2)screen - pointer).sqrMagnitude;
            if (distance < best) { best = distance; bestKind = kind; }
        }

        private static GameObject GetHandle(HandleKind kind) => kind switch
        {
            HandleKind.LeftFoot => _leftHandle,
            HandleKind.RightFoot => _rightHandle,
            HandleKind.Hips => _hipsHandle,
            _ => null
        };

        private static void SetOffsetFromWorld(HandleKind kind, Vector3 world)
        {
            ManualIkPoseSettings settings = Config.manualIkPoseSettings;
            Vector3 local;
            if (kind == HandleKind.LeftFoot)
            {
                local = _animator.transform.InverseTransformVector(world - _leftPeg.position);
                settings.leftFootX = local.x; settings.leftFootY = local.y; settings.leftFootZ = local.z;
            }
            else if (kind == HandleKind.RightFoot)
            {
                local = _animator.transform.InverseTransformVector(world - _rightPeg.position);
                settings.rightFootX = local.x; settings.rightFootY = local.y; settings.rightFootZ = local.z;
            }
            else if (kind == HandleKind.Hips)
            {
                local = _animator.transform.InverseTransformPoint(world) - _neutralHipsRootSpace;
                settings.hipsX = local.x; settings.hipsY = local.y; settings.hipsZ = local.z;
            }
        }

        private static void ReleasePose()
        {
            if (!_active) return;
            RestorePedalTargets();
            RestoreTarget(ref _leftTarget, ref _leftTargetCaptured, _leftTargetLocalPosition, _leftTargetLocalRotation);
            RestoreTarget(ref _rightTarget, ref _rightTargetCaptured, _rightTargetLocalPosition, _rightTargetLocalRotation);
            Log.Msg("[ManualIK] Restored native rider IK targets.");
            ClearReferences();
        }

        private static void ClearReferences()
        {
            _active = false;
            _vehicle = null;
            _animator = null;
            _humanIk = null;
            _leftPeg = null; _rightPeg = null;
            _leftFoot = null; _rightFoot = null;
            _leftKnee = null; _rightKnee = null; _hips = null;
            _leftChain = null; _rightChain = null;
            _leftLimb = null; _rightLimb = null;
            _pedalRig = null;
            _throttleTarget = null;
            _brakeTarget = null;
            _pedalTargetsCaptured = false;
            _fullBodyMotion = null;
            _smoothedHipsOffset = Vector3.zero;
        }

        private static void RestorePedalTargets()
        {
            if (!_pedalTargetsCaptured)
                return;

            if (_throttleTarget != null)
            {
                _throttleTarget.localPosition = _throttleTargetLocalPosition;
                _throttleTarget.localRotation = _throttleTargetLocalRotation;
            }
            if (_brakeTarget != null)
            {
                _brakeTarget.localPosition = _brakeTargetLocalPosition;
                _brakeTarget.localRotation = _brakeTargetLocalRotation;
            }
            _pedalTargetsCaptured = false;
        }

        private static void DestroyHandles()
        {
            DestroyHandle(ref _leftHandle);
            DestroyHandle(ref _rightHandle);
            DestroyHandle(ref _hipsHandle);
        }

        private static void DestroyHandle(ref GameObject handle)
        {
            if (handle != null) UnityEngine.Object.Destroy(handle);
            handle = null;
        }
    }

    internal static class ManualIkHumanPatch
    {
        private static void Prefix(HumanIK __instance) => ManualIkPoseEditor.BeforeAnimatorIk(__instance);
        private static void Postfix(HumanIK __instance) => ManualIkPoseEditor.AfterAnimatorIk(__instance);
    }

    internal static class ManualIkNativeLimbPatch
    {
        private static void Prefix(UnityIKLimb __instance) => ManualIkPoseEditor.BeforeLimbUpdate(__instance);
    }

    internal static class ManualIkPedalRigPatch
    {
        private static bool Prefix(VehicleFootPedalAnimationRig __instance) => ManualIkPoseEditor.AllowPedalRigUpdate(__instance);
    }
}
