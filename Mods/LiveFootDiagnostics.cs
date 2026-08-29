using System;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Animator_Motion_Systems;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using Il2CppMashBox.Core.Runtime.InverseKinematics;
using rowemod.Utils;
using UnityEngine;

namespace rowemod.Mods
{
    /// <summary>
    /// Short, user-triggered capture of the live left-foot ownership chain.
    /// Reuses already verified native hooks and performs no pose writes.
    /// </summary>
    internal static class LiveFootDiagnostics
    {
        private const float CaptureSeconds = 12f;
        private const float ResolveWaitSeconds = 20f;
        private const float SampleInterval = 0.1f;
        private static bool _armed;
        private static bool _captureStarted;
        private static float _waitEndsAt;
        private static float _endsAt;
        private static float _nextSampleAt;
        private static int _sample;
        private static Animator _animator;
        private static VehicleController _vehicle;
        private static Transform _leftFoot;
        private static Transform _rightFoot;
        private static HumanIK _humanIk;
        private static UnityIKLimb _leftLimb;
        private static UnityIKLimb _rightLimb;
        private static VehicleFootPedalAnimationRig _pedalRig;
        private static TrickControllerV2 _controller;
        private static Vector3 _beforeHumanIk;
        private static Vector3 _afterHumanIk;
        private static int _humanIkCalls;
        private static int _pedalCalls;
        private static string _status = "Capture the live left foot while the custom trick is playing.";

        internal static string Status => _status;
        internal static bool IsCapturing => _armed;

        internal static void Arm()
        {
            _armed = true;
            _captureStarted = false;
            _waitEndsAt = Time.unscaledTime + ResolveWaitSeconds;
            _status = "Waiting for the riding view, then capturing both feet for 12 seconds.";
            Log.Msg("[LiveFoot] CAPTURE ARMED. Close the menu and reproduce the foot problem; waiting for the local rider IK chain.");
            if (TryResolveReferences())
                BeginCapture();
        }

        private static bool TryResolveReferences()
        {
            try
            {
                VehicleController vehicle = VehicleController.LocalDriverVehicle;
                VehicleAnimationInputHandler handler = vehicle == null ? null :
                    vehicle.GetComponent<VehicleAnimationInputHandler>() ??
                    vehicle.GetComponentInChildren<VehicleAnimationInputHandler>(true) ??
                    vehicle.GetComponentInParent<VehicleAnimationInputHandler>();
                Animator animator = handler?._humanAnimator?.Animator;
                HumanIK humanIk = handler?._humanAnimator == null ? null :
                    handler._humanAnimator.GetComponent<HumanIK>() ??
                    handler._humanAnimator.GetComponentInChildren<HumanIK>() ??
                    handler._humanAnimator.GetComponentInParent<HumanIK>();
                UnityIKLimb leftLimb = humanIk?._leftFootIK?.UnityObject as UnityIKLimb;
                UnityIKLimb rightLimb = humanIk?._rightFootIK?.UnityObject as UnityIKLimb;
                Transform leftFoot = animator?.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator?.GetBoneTransform(HumanBodyBones.RightFoot);

                if (vehicle == null || animator == null || leftFoot == null || rightFoot == null ||
                    humanIk == null || leftLimb == null || rightLimb == null)
                    return false;

                _vehicle = vehicle;
                _animator = animator;
                _humanIk = humanIk;
                _leftLimb = leftLimb;
                _rightLimb = rightLimb;
                _leftFoot = leftFoot;
                _rightFoot = rightFoot;
                _pedalRig = vehicle.GetComponentInChildren<VehicleFootPedalAnimationRig>(true);
                _controller = Memory.rMbCharacter == null ? null :
                    Memory.rMbCharacter.GetComponentInChildren<TrickControllerV2>(true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[LiveFoot] Rider IK resolve attempt failed: " + ex.Message);
                return false;
            }
        }

        private static void BeginCapture()
        {
            _captureStarted = true;
            _endsAt = Time.unscaledTime + CaptureSeconds;
            _nextSampleAt = Time.unscaledTime;
            _sample = 0;
            _humanIkCalls = 0;
            _pedalCalls = 0;
            _beforeHumanIk = _leftFoot.position;
            _afterHumanIk = _leftFoot.position;
            _status = "Capturing both feet for 12 seconds. Reproduce the problem now.";
            Log.Msg("[LiveFoot] CAPTURE START. Local rider IK resolved; no pose values are being changed.");
        }

        internal static void Update()
        {
            if (!_armed) return;
            if (!_captureStarted)
            {
                if (TryResolveReferences())
                {
                    BeginCapture();
                    return;
                }
                if (Time.unscaledTime >= _waitEndsAt)
                {
                    _armed = false;
                    _status = "Capture timed out before the local rider IK chain became available.";
                    Log.Warning("[LiveFoot] " + _status);
                }
                return;
            }
            if (Time.unscaledTime >= _endsAt)
            {
                _armed = false;
                _captureStarted = false;
                _status = $"Capture complete: {_sample} samples written to Latest.log.";
                Log.Msg($"[LiveFoot] CAPTURE END samples={_sample} humanIkCalls={_humanIkCalls} pedalRigCalls={_pedalCalls}.");
                return;
            }
            if (Time.unscaledTime < _nextSampleAt) return;
            _nextSampleAt = Time.unscaledTime + SampleInterval;
            WriteSample();
        }

        internal static void BeforeHumanIk(HumanIK instance)
        {
            if (!_captureStarted || instance != _humanIk || _leftFoot == null) return;
            _humanIkCalls++;
            _beforeHumanIk = _leftFoot.position;
        }

        internal static void AfterHumanIk(HumanIK instance)
        {
            if (!_captureStarted || instance != _humanIk || _leftFoot == null) return;
            _afterHumanIk = _leftFoot.position;
        }

        internal static void BeforePedalRig(VehicleFootPedalAnimationRig instance)
        {
            if (_captureStarted && instance == _pedalRig) _pedalCalls++;
        }

        private static void WriteSample()
        {
            try
            {
                _sample++;
                Vector3 bone = LocalPoint(_leftFoot?.position ?? Vector3.zero);
                Vector3 rightBone = LocalPoint(_rightFoot?.position ?? Vector3.zero);
                Vector3 before = LocalPoint(_beforeHumanIk);
                Vector3 after = LocalPoint(_afterHumanIk);
                Vector3 goal = LocalPoint(_leftLimb._goalPosition);
                IKTarget target = _leftLimb.Target;
                Vector3 targetPosition = LocalPoint(target == null ? Vector3.zero : target.transform.position);
                IKTarget rightTarget = _rightLimb.Target;
                Vector3 rightTargetPosition = LocalPoint(rightTarget == null ? Vector3.zero : rightTarget.transform.position);
                Transform throttle = _pedalRig?._throttleTarget;
                Vector3 throttlePosition = LocalPoint(throttle == null ? Vector3.zero : throttle.position);
                Transform brake = _pedalRig?._brakeTarget;
                Vector3 brakePosition = LocalPoint(brake == null ? Vector3.zero : brake.position);
                string phase = _controller == null ? "none" : _controller._phase.ToString();
                string activeClip = ActivePlayerClip(_controller);
                float humanWeight = _humanIk.LeftFootWeight;
                float positionWeight = _leftLimb.PositionWeight;
                float rotationWeight = _leftLimb.RotationWeight;
                float ikMove = Vector3.Distance(before, after);
                float boneGoal = Vector3.Distance(bone, goal);
                float boneTarget = target == null ? -1f : Vector3.Distance(bone, targetPosition);
                float boneBrake = brake == null ? -1f : Vector3.Distance(bone, brakePosition);
                bool manualOwns = ManualIkPoseEditor.PoseCurrentlyApplied;
                bool manny = Safe(() => _vehicle.IsManny, false);
                bool nosey = Safe(() => _vehicle.IsNosey, false);
                bool rearManual = Safe(() => ManualIkPoseEditor.IsRearWheelManual(_vehicle), false);
                bool frontManual = Safe(() => ManualIkPoseEditor.IsFrontWheelManual(_vehicle), false);
                float rearHold = ManualIkPoseEditor.RearManualHoldRemaining;
                float frontHold = ManualIkPoseEditor.FrontManualHoldRemaining;
                bool movingForward = Safe(() => _vehicle.MovingForwards, false);
                bool driverForward = Safe(() => _vehicle.DriverMovingForwards, false);
                float forwardSpeed = Safe(() => _vehicle.ForwardSpeed, 0f);
                bool goofy = Safe(() => _vehicle.IsGoofyStance, false);
                bool pedalBlocked = manualOwns &&
                    ((Config.manualIkPoseSettings?.leftFootEnabled ?? false) ||
                     (Config.manualIkPoseSettings?.rightFootEnabled ?? false));

                Log.Msg(
                    $"[LiveFoot] sample={_sample} phase={phase} tweaked={Safe(() => _controller._tweaked, false)} " +
                    $"clip={activeClip} manualOwns={manualOwns} inputManny={manny} inputNosey={nosey} " +
                    $"rearManual={rearManual} frontManual={frontManual} hold={rearHold:0.###}/{frontHold:0.###} " +
                    $"movingFwd={movingForward} driverFwd={driverForward} speed={forwardSpeed:0.###} goofy={goofy} " +
                    $"pedalBlocked={pedalBlocked} humanW={humanWeight:0.###} " +
                    $"limbW={positionWeight:0.###}/{rotationWeight:0.###} targetId={Safe(() => _leftLimb._currentTargetID, -1)} " +
                    $"leftBone={V(bone)} leftTarget={V(targetPosition)} rightBone={V(rightBone)} rightTarget={V(rightTargetPosition)} " +
                    $"throttle={V(throttlePosition)} brake={V(brakePosition)} goal={V(goal)} " +
                    $"humanMove={ikMove:0.####} dGoal={boneGoal:0.####} dTarget={boneTarget:0.####} dBrake={boneBrake:0.####} " +
                    $"humanCalls={_humanIkCalls} pedalCalls={_pedalCalls}");
            }
            catch (Exception ex)
            {
                _armed = false;
                _status = "Live capture stopped: " + ex.Message;
                Log.Warning("[LiveFoot] " + _status);
            }
        }

        private static string ActivePlayerClip(TrickControllerV2 controller)
        {
            if (controller == null) return "none";
            string phase = controller._phase.ToString();
            if (phase.IndexOf("Tweak", StringComparison.OrdinalIgnoreCase) >= 0) return Clip(controller._tweak.Player);
            if (phase.IndexOf("Loop", StringComparison.OrdinalIgnoreCase) >= 0) return Clip(controller._loop.Player);
            if (phase.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0) return Clip(controller._exit.Player);
            return Clip(controller._enter.Player);
        }

        private static string Clip(Il2CppAnimancer.AnimancerState state)
        {
            AnimationClip clip = state?.Clip;
            return clip == null ? "none" : clip.name + "@" + state.NormalizedTime.ToString("0.###");
        }

        private static Vector3 LocalPoint(Vector3 world) =>
            _animator == null ? world : _animator.transform.InverseTransformPoint(world);

        private static string V(Vector3 value) => $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";

        private static T Safe<T>(Func<T> read, T fallback)
        {
            try { return read(); }
            catch { return fallback; }
        }
    }
}
