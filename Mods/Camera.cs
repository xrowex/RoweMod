using System;
using System.Reflection;
using Il2CppCinemachine;
using Il2CppMashBox.Core.Runtime.Camera;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using static rowemod.Utils.Memory;

namespace rowemod.Mods
{
    public class Camera
    {
        private const float InputCooldownSeconds = 0.25f;
        private const float MinMeaningfulOffset = 0.0001f;
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] FloatOffsetMembers =
        {
            "_horizontalOffset",
            "horizontalOffset",
            "HorizontalOffset",
            "_cameraOffset",
            "cameraOffset",
            "CameraOffset",
            "_leftOffset",
            "leftOffset",
            "LeftOffset",
            "_rightOffset",
            "rightOffset",
            "RightOffset",
            "_offsetX",
            "offsetX",
            "OffsetX"
        };

        private static readonly string[] VectorOffsetMembers =
        {
            "_offset",
            "offset",
            "Offset",
            "_lookOffset",
            "lookOffset",
            "LookOffset",
            "_followOffset",
            "followOffset",
            "FollowOffset",
            "_targetOffset",
            "targetOffset",
            "TargetOffset",
            "_initialOffset",
            "initialOffset",
            "InitialOffset",
            "m_FollowOffset",
            "m_Offset",
            "m_TrackedObjectOffset",
            "m_PositionOffset",
            "ShoulderOffset"
        };

        private static float nextAllowedInputTime;

        public static void Update()
        {
            if (Menu.isOpen)
                return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad?.leftStickButton.wasPressedThisFrame != true)
                return;

            if (Time.unscaledTime < nextAllowedInputTime)
                return;

            nextAllowedInputTime = Time.unscaledTime + InputCooldownSeconds;
            FlipCameraOffset();
        }

        private static void FlipCameraOffset()
        {
            try
            {
                if (TryFlipKnownCameraTargets(out string changedTarget))
                {
                    Log.Msg($"[CameraOffset] Flipped camera offset: {changedTarget}");
                    return;
                }

                Log.Warning("[CameraOffset] Left-stick click detected, but no editable camera offset was found.");
            }
            catch (Exception ex)
            {
                Log.Error($"[CameraOffset] Failed to flip camera offset: {ex.Message}");
            }
        }

        private static bool TryFlipKnownCameraTargets(out string changedTarget)
        {
            changedTarget = null;

            if (TryFlipCameraData(tpCameraData, "ThirdPerson CameraData", out changedTarget))
                return true;

            if (TryFlipCameraData(cameraData, "CameraData", out changedTarget))
                return true;

            if (TryFlipObjectOffsets(tpCameraData, "ThirdPerson CameraData", out changedTarget))
                return true;

            if (TryFlipObjectOffsets(cameraData, "CameraData", out changedTarget))
                return true;

            if (TryFlipObjectOffsets(tpCamera, "ThirdPersonCamera", out changedTarget))
                return true;

            if (TryFlipCinemachineOffsets(tpVirtualCam, "ThirdPerson vcam", out changedTarget))
                return true;

            if (TryFlipCinemachineOffsets(virtualCam, "VirtualCamera", out changedTarget))
                return true;

            CameraData[] cameraDatas = Resources.FindObjectsOfTypeAll<CameraData>();
            foreach (CameraData data in cameraDatas)
            {
                if (data == null || data == tpCameraData || data == cameraData)
                    continue;

                string label = string.IsNullOrWhiteSpace(data.name) ? "CameraData" : data.name;
                if (TryFlipCameraData(data, label, out changedTarget))
                    return true;
            }

            CinemachineVirtualCamera[] vcams = Resources.FindObjectsOfTypeAll<CinemachineVirtualCamera>();
            foreach (CinemachineVirtualCamera vcam in vcams)
            {
                if (vcam == null)
                    continue;

                string label = string.IsNullOrWhiteSpace(vcam.name) ? "CinemachineVirtualCamera" : vcam.name;
                if (TryFlipCinemachineOffsets(vcam, label, out changedTarget))
                    return true;
            }

            return false;
        }

        private static bool TryFlipCameraData(CameraData data, string label, out string changedTarget)
        {
            changedTarget = null;
            if (data == null)
                return false;

            if (TryFlipCameraDataSmartOffset(data, label, out changedTarget))
                return true;

            if (TryFlipCameraDataOffset(data, label, out changedTarget))
                return true;

            if (TryFlipVectorXMember(data, "_offset", label, out changedTarget))
                return true;

            Vector3 oldOffset = data.Offset;
            if (Mathf.Abs(oldOffset.x) >= MinMeaningfulOffset)
            {
                if (TryFlipVectorXMember(data, "Offset", label, out changedTarget))
                    return true;

                Log.Warning($"[CameraOffset] {label}.Offset.x is {oldOffset.x:0.###}, but the wrapper exposes it as read-only.");
                return false;
            }

            Log.Warning(
                $"[CameraOffset] {label} exists but OffsetX and Offset.x are both zero. " +
                $"OffsetX={data.OffsetX:0.###}, Offset=({oldOffset.x:0.###}, {oldOffset.y:0.###}, {oldOffset.z:0.###})");
            return false;
        }

        private static bool TryFlipCameraDataSmartOffset(CameraData data, string label, out string changedTarget)
        {
            changedTarget = null;

            if (data._offsetSmartData == null)
                return false;

            Vector3 oldOffset = data.Offset;
            float oldSmartValue = data._offsetSmartData.Value;
            float sourceValue = Mathf.Abs(oldSmartValue) >= MinMeaningfulOffset ? oldSmartValue : oldOffset.x;
            if (Mathf.Abs(sourceValue) < MinMeaningfulOffset)
                return false;

            float newValue = -sourceValue;
            data._offsetSmartData.Value = newValue;

            Vector3 verifiedOffset = data.Offset;
            float verifiedSmartValue = data._offsetSmartData.Value;
            if (Mathf.Abs(verifiedSmartValue - newValue) > MinMeaningfulOffset)
            {
                Log.Warning(
                    $"[CameraOffset] Tried to set {label}._offsetSmartData.Value from {oldSmartValue:0.###} to {newValue:0.###}, " +
                    $"but it read back as {verifiedSmartValue:0.###}.");
                return false;
            }

            changedTarget =
                $"{label}._offsetSmartData.Value: {oldSmartValue:0.###} -> {verifiedSmartValue:0.###}; " +
                $"Offset.x: {oldOffset.x:0.###} -> {verifiedOffset.x:0.###}";
            return true;
        }

        private static bool TryFlipCameraDataOffset(CameraData data, string label, out string changedTarget)
        {
            changedTarget = null;

            Vector3 oldValue = data._offset;
            if (Mathf.Abs(oldValue.x) < MinMeaningfulOffset)
                return false;

            Vector3 newValue = new Vector3(-oldValue.x, oldValue.y, oldValue.z);
            data._offset = newValue;

            Vector3 verifiedValue = data._offset;
            if (Mathf.Abs(verifiedValue.x - newValue.x) > MinMeaningfulOffset)
            {
                Log.Warning(
                    $"[CameraOffset] Tried to set {label}._offset.x from {oldValue.x:0.###} to {newValue.x:0.###}, " +
                    $"but it read back as {verifiedValue.x:0.###}.");
                return false;
            }

            changedTarget = $"{label}._offset.x: {oldValue.x:0.###} -> {newValue.x:0.###}";
            return true;
        }

        private static bool TryFlipCinemachineOffsets(CinemachineVirtualCamera vcam, string label, out string changedTarget)
        {
            changedTarget = null;
            if (vcam == null)
                return false;

            if (TryFlipObjectOffsets(vcam, label, out changedTarget))
                return true;

            CinemachineComponentBase[] components = vcam.GetComponentPipeline();
            if (components == null)
                return false;

            foreach (CinemachineComponentBase component in components)
            {
                if (component == null)
                    continue;

                string componentLabel = $"{label}.{component.GetType().Name}";
                if (TryFlipKnownCinemachineComponent(component, componentLabel, out changedTarget))
                    return true;

                if (TryFlipObjectOffsets(component, componentLabel, out changedTarget))
                    return true;
            }

            Il2Cpp.CinemachineCameraOffset[] cameraOffsets = vcam.GetComponents<Il2Cpp.CinemachineCameraOffset>();
            foreach (Il2Cpp.CinemachineCameraOffset cameraOffset in cameraOffsets)
            {
                if (cameraOffset == null)
                    continue;

                string offsetLabel = $"{label}.{cameraOffset.GetType().Name}";
                if (TryFlipCameraOffsetExtension(cameraOffset, offsetLabel, out changedTarget))
                    return true;
            }

            return false;
        }

        private static bool TryFlipKnownCinemachineComponent(CinemachineComponentBase component, string label, out string changedTarget)
        {
            changedTarget = null;

            CinemachineTransposer transposer = component.TryCast<CinemachineTransposer>();
            if (transposer != null && TryFlipVectorValue(
                    transposer.m_FollowOffset,
                    value => transposer.m_FollowOffset = value,
                    $"{label}.m_FollowOffset",
                    out changedTarget))
                return true;

            CinemachineFramingTransposer framingTransposer = component.TryCast<CinemachineFramingTransposer>();
            if (framingTransposer != null && TryFlipVectorValue(
                    framingTransposer.m_TrackedObjectOffset,
                    value => framingTransposer.m_TrackedObjectOffset = value,
                    $"{label}.m_TrackedObjectOffset",
                    out changedTarget))
                return true;

            return false;
        }

        private static bool TryFlipCameraOffsetExtension(Il2Cpp.CinemachineCameraOffset cameraOffset, string label, out string changedTarget)
        {
            return TryFlipVectorValue(
                cameraOffset.m_Offset,
                value => cameraOffset.m_Offset = value,
                $"{label}.m_Offset",
                out changedTarget);
        }

        private static bool TryFlipVectorValue(Vector3 oldValue, Action<Vector3> setter, string label, out string changedTarget)
        {
            changedTarget = null;
            if (Mathf.Abs(oldValue.x) < MinMeaningfulOffset)
                return false;

            Vector3 newValue = new Vector3(-oldValue.x, oldValue.y, oldValue.z);
            setter(newValue);
            changedTarget = $"{label}.x: {oldValue.x:0.###} -> {newValue.x:0.###}";
            return true;
        }

        private static bool TryFlipObjectOffsets(object target, string label, out string changedTarget)
        {
            changedTarget = null;
            if (target == null)
                return false;

            foreach (string memberName in FloatOffsetMembers)
            {
                if (TryFlipFloatMember(target, memberName, label, out changedTarget))
                    return true;
            }

            foreach (string memberName in VectorOffsetMembers)
            {
                if (TryFlipVectorXMember(target, memberName, label, out changedTarget))
                    return true;
            }

            return false;
        }

        private static bool TryFlipFloatMember(object target, string memberName, string label, out string changedTarget)
        {
            changedTarget = null;
            Type type = target.GetType();

            FieldInfo field = type.GetField(memberName, Flags);
            if (field != null && field.FieldType == typeof(float))
            {
                float oldValue = (float)field.GetValue(target);
                if (Mathf.Abs(oldValue) < MinMeaningfulOffset)
                    return false;

                float newValue = -oldValue;
                field.SetValue(target, newValue);
                changedTarget = $"{label}.{memberName}: {oldValue:0.###} -> {newValue:0.###}";
                return true;
            }

            PropertyInfo property = type.GetProperty(memberName, Flags);
            if (property != null && property.CanRead && property.CanWrite && property.PropertyType == typeof(float))
            {
                float oldValue = (float)property.GetValue(target, null);
                if (Mathf.Abs(oldValue) < MinMeaningfulOffset)
                    return false;

                float newValue = -oldValue;
                property.SetValue(target, newValue, null);
                changedTarget = $"{label}.{memberName}: {oldValue:0.###} -> {newValue:0.###}";
                return true;
            }

            return false;
        }

        private static bool TryFlipVectorXMember(object target, string memberName, string label, out string changedTarget)
        {
            changedTarget = null;
            Type type = target.GetType();

            FieldInfo field = type.GetField(memberName, Flags);
            if (field != null && field.FieldType == typeof(Vector3))
            {
                Vector3 oldValue = (Vector3)field.GetValue(target);
                if (Mathf.Abs(oldValue.x) < MinMeaningfulOffset)
                    return false;

                Vector3 newValue = new Vector3(-oldValue.x, oldValue.y, oldValue.z);
                field.SetValue(target, newValue);
                changedTarget = $"{label}.{memberName}.x: {oldValue.x:0.###} -> {newValue.x:0.###}";
                return true;
            }

            PropertyInfo property = type.GetProperty(memberName, Flags);
            if (property != null && property.CanRead && property.CanWrite && property.PropertyType == typeof(Vector3))
            {
                Vector3 oldValue = (Vector3)property.GetValue(target, null);
                if (Mathf.Abs(oldValue.x) < MinMeaningfulOffset)
                    return false;

                Vector3 newValue = new Vector3(-oldValue.x, oldValue.y, oldValue.z);
                property.SetValue(target, newValue, null);
                changedTarget = $"{label}.{memberName}.x: {oldValue.x:0.###} -> {newValue.x:0.###}";
                return true;
            }

            return false;
        }
    }
}
