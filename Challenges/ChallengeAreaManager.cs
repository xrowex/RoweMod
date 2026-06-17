using System;
using rowemod.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace rowemod.Challenges
{
    public static class ChallengeAreaManager
    {
        private const float GroundProbeStartOffset = 2.5f;
        private const float GroundProbeDistance = 200f;
        private const float GroundClearance = 0.02f;
        private const float MinimumGroundNormalY = 0.1f;

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

        public static bool TryGetLocalPlayerGroundPlacement(
            Vector3 areaSize,
            out Vector3 position,
            out Quaternion rotation,
            out string source)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            source = "none";

            if (!TryGetLocalPlayerPose(
                    out Vector3 playerPosition,
                    out Quaternion playerRotation,
                    out string poseSource))
            {
                return false;
            }

            Vector3 forward = GetPreferredPlayerForward(playerRotation);
            bool foundGround = TryGetGroundAlignedPlacement(
                playerPosition,
                forward,
                areaSize,
                out position,
                out rotation,
                out string groundSource);

            source = foundGround
                ? $"{poseSource}, heading={GetPreferredPlayerForwardSource()}, ground={groundSource}"
                : $"{poseSource}, heading={GetPreferredPlayerForwardSource()}, ground=fallback";
            return true;
        }

        public static bool TryGetGroundAlignedPlacement(
            Vector3 referencePosition,
            Vector3 referenceForward,
            Vector3 areaSize,
            out Vector3 position,
            out Quaternion rotation,
            out string groundSource)
        {
            position = referencePosition;
            rotation = BuildGroundAlignedRotation(referenceForward, Vector3.up);
            groundSource = "none";

            Ray ray = new Ray(
                referencePosition + Vector3.up * GroundProbeStartOffset,
                Vector3.down);
            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(
                ray,
                GroundProbeStartOffset + GroundProbeDistance,
                UnityEngine.Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    hit.normal.y < MinimumGroundNormalY ||
                    ShouldIgnoreGroundHit(hit.collider.transform))
                {
                    continue;
                }

                Vector3 groundNormal = hit.normal.normalized;
                float halfHeight = Mathf.Max(Mathf.Abs(areaSize.y), 0.01f) * 0.5f;
                position = hit.point + groundNormal * (halfHeight + GroundClearance);
                rotation = BuildGroundAlignedRotation(referenceForward, groundNormal);
                groundSource =
                    $"{hit.collider.name ?? "unnamed collider"} normal={FormatVector3(groundNormal)}";
                return true;
            }

            return false;
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

        private static Vector3 GetPreferredPlayerForward(Quaternion fallbackRotation)
        {
            Transform transform = GetPreferredPlayerForwardTransform();
            if (transform != null)
            {
                Vector3 transformForward = transform.forward;
                if (IsFinite(transformForward) && transformForward.sqrMagnitude > 0.001f)
                    return transformForward.normalized;
            }

            Vector3 fallbackForward = fallbackRotation * Vector3.forward;
            return IsFinite(fallbackForward) && fallbackForward.sqrMagnitude > 0.001f
                ? fallbackForward.normalized
                : Vector3.forward;
        }

        private static string GetPreferredPlayerForwardSource()
        {
            Transform transform = GetPreferredPlayerForwardTransform();
            return transform != null ? transform.name : "pose rotation";
        }

        private static Transform GetPreferredPlayerForwardTransform()
        {
            if (IsUsableTransform(Memory.physicsDrivenCharacter != null
                    ? Memory.physicsDrivenCharacter.transform
                    : null))
            {
                return Memory.physicsDrivenCharacter.transform;
            }

            if (IsUsableTransform(Memory.gamePlayer != null ? Memory.gamePlayer.transform : null))
                return Memory.gamePlayer.transform;

            return IsUsableTransform(Memory.rMbCharacter != null ? Memory.rMbCharacter.transform : null)
                ? Memory.rMbCharacter.transform
                : null;
        }

        private static Quaternion BuildGroundAlignedRotation(Vector3 referenceForward, Vector3 groundNormal)
        {
            Vector3 up = IsFinite(groundNormal) && groundNormal.sqrMagnitude > 0.001f
                ? groundNormal.normalized
                : Vector3.up;
            Vector3 forward = IsFinite(referenceForward)
                ? Vector3.ProjectOnPlane(referenceForward, up)
                : Vector3.zero;

            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.right, up);

            return Quaternion.LookRotation(forward.normalized, up);
        }

        private static bool ShouldIgnoreGroundHit(Transform hitTransform)
        {
            if (hitTransform == null)
                return true;

            if (_active != null &&
                _active.transform != null &&
                (hitTransform == _active.transform || hitTransform.IsChildOf(_active.transform)))
            {
                return true;
            }

            return IsPartOfObject(hitTransform, Memory.rMbCharacter) ||
                   IsPartOfObject(hitTransform, Memory.gamePlayer) ||
                   IsPartOfObject(hitTransform, Memory.physicsDrivenCharacter) ||
                   IsPartOfBody(hitTransform, Memory.springBody) ||
                   IsPartOfBody(hitTransform, Memory.chassisRb);
        }

        private static bool IsPartOfObject(Transform hitTransform, GameObject root)
        {
            return root != null &&
                   root.transform != null &&
                   (hitTransform == root.transform || hitTransform.IsChildOf(root.transform));
        }

        private static bool IsPartOfBody(Transform hitTransform, Rigidbody body)
        {
            return body != null &&
                   body.transform != null &&
                   (hitTransform == body.transform || hitTransform.IsChildOf(body.transform));
        }

        private static bool IsUsableTransform(Transform transform)
        {
            return transform != null &&
                   transform.gameObject != null &&
                   transform.gameObject.activeInHierarchy;
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
