using System;
using rowemod.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace rowemod.Challenges
{
    public static class ChallengeAreaManager
    {
        private static ChallengeArea _active;
        public static ChallengeArea Active => _active;

        // Create a new area at position/rotation with given size
        public static ChallengeArea Create(Vector3 position, Vector3 size, Quaternion? rotation = null, string playerTag = "Player")
        {
            Quaternion resolvedRotation = rotation ?? Quaternion.identity;
            Log.Msg($"[ChallengeArea] Create requested: pos={FormatVector3(position)}, rot={FormatQuaternion(resolvedRotation)}, size={FormatVector3(size)}, playerTag='{playerTag}'.");
            DestroyActive();

            _active = ChallengeArea.CreateChallengeArea(position, size, resolvedRotation, playerTag);
            Log.Msg($"[ChallengeArea] Created active area: id={(_active != null ? _active.GetInstanceID() : 0)}, active={(_active != null && _active.gameObject != null && _active.gameObject.activeSelf)}, pos={FormatVector3(_active != null ? _active.transform.position : Vector3.zero)}, size={FormatVector3(_active != null ? _active.transform.localScale : Vector3.zero)}.");
            return _active;
        }

        public static void DestroyActive()
        {
            Log.Msg($"[ChallengeArea] DestroyActive requested. activeExists={_active != null}.");
            ChallengeArea active = _active;
            _active = null;

            int destroyedCount = 0;
            if (DestroyArea(active))
                destroyedCount++;

            ChallengeArea[] areas = Object.FindObjectsOfType<ChallengeArea>(true);
            foreach (ChallengeArea area in areas)
            {
                if (active != null && area == active)
                    continue;

                if (DestroyArea(area))
                    destroyedCount++;
            }

            Log.Msg($"[ChallengeArea] DestroyActive finished. destroyedOrDisabled={destroyedCount}, orphanScanCount={(areas != null ? areas.Length : 0)}.");
        }

        private static bool DestroyArea(ChallengeArea area)
        {
            if (area == null || area.gameObject == null)
                return false;

            try
            {
                Log.Msg($"[ChallengeArea] Destroying area: id={area.GetInstanceID()}, go='{area.gameObject.name}', pos={FormatVector3(area.transform.position)}, size={FormatVector3(area.transform.localScale)}.");
                area.SetVisible(false);
                area.gameObject.SetActive(false);
                Object.Destroy(area.gameObject);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[ChallengeArea] Failed to destroy area: {ex.Message}");
                return false;
            }
        }

        public static void SetVisible(bool visible)
        {
            if (_active != null) _active.SetVisible(visible);
        }

        public static void SetPosition(Vector3 pos)
        {
            if (_active != null) _active.transform.position = pos;
        }

        public static void SetRotation(Quaternion rot)
        {
            if (_active != null) _active.transform.rotation = rot;
        }

        public static void SetSize(Vector3 worldSize)
        {
            if (_active != null) _active.transform.localScale = worldSize;
        }

        public static void SetColor(Color color)
        {
            if (_active != null) _active.SetColor(color);
        }

        public static void SetCompleted(bool completed)
        {
            if (_active != null) _active.SetCompleted(completed);
        }

        public static bool TryGetLocalPlayerPose(
            out Vector3 position,
            out Quaternion rotation,
            out string source)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            source = "none";

            if (TryGetRigidbodyPose(Memory.springBody, "springBody", out position, out rotation, out source))
                return true;

            if (TryGetRigidbodyPose(Memory.chassisRb, "chassisRb", out position, out rotation, out source))
                return true;

            GameObject searchRoot = Memory.rMbCharacter != null
                ? Memory.rMbCharacter
                : Memory.gamePlayer != null
                    ? Memory.gamePlayer
                    : Memory.physicsDrivenCharacter;
            if (searchRoot != null)
            {
                Rigidbody[] bodies = searchRoot.GetComponentsInChildren<Rigidbody>(true);
                Rigidbody movingBody = Array.Find(
                    bodies,
                    body => body != null && body.gameObject.activeInHierarchy && !body.isKinematic);
                if (movingBody == null)
                {
                    movingBody = Array.Find(
                        bodies,
                        body => body != null && body.gameObject.activeInHierarchy);
                }

                if (TryGetRigidbodyPose(
                        movingBody,
                        movingBody != null ? $"discovered Rigidbody '{movingBody.name}'" : "discovered Rigidbody",
                        out position,
                        out rotation,
                        out source))
                {
                    return true;
                }
            }

            if (TryGetTransformPose(
                    Memory.physicsDrivenCharacter != null ? Memory.physicsDrivenCharacter.transform : null,
                    "physicsDrivenCharacter",
                    out position,
                    out rotation,
                    out source))
            {
                return true;
            }

            if (TryGetTransformPose(
                    Memory.gamePlayer != null ? Memory.gamePlayer.transform : null,
                    "gamePlayer",
                    out position,
                    out rotation,
                    out source))
            {
                return true;
            }

            return TryGetTransformPose(
                Memory.rMbCharacter != null ? Memory.rMbCharacter.transform : null,
                "rMbCharacter",
                out position,
                out rotation,
                out source);
        }

        public static bool IsLocalPlayerInsideActiveArea()
        {
            if (_active == null ||
                !TryGetLocalPlayerPose(out Vector3 position, out _, out _))
            {
                return false;
            }

            return _active.ContainsWorldPoint(position);
        }

        private static bool TryGetRigidbodyPose(
            Rigidbody body,
            string sourceName,
            out Vector3 position,
            out Quaternion rotation,
            out string source)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            source = "none";
            if (body == null || body.gameObject == null || !body.gameObject.activeInHierarchy)
                return false;

            try
            {
                position = body.position;
                rotation = body.rotation;
                source = sourceName;
                return IsFinite(position) && IsFinite(rotation);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetTransformPose(
            Transform transform,
            string sourceName,
            out Vector3 position,
            out Quaternion rotation,
            out string source)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            source = "none";
            if (transform == null || transform.gameObject == null || !transform.gameObject.activeInHierarchy)
                return false;

            try
            {
                position = transform.position;
                rotation = transform.rotation;
                source = sourceName;
                return IsFinite(position) && IsFinite(rotation);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string FormatQuaternion(Quaternion value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###}, {value.w:0.###})";
        }
    }
}
