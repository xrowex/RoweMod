using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace rowemod.Challenges
{
    [RegisterTypeInIl2Cpp]
    public class ChallengeArea : MonoBehaviour
    {
        public string playerTag = "VehicleColliders";      // Make this match your game's player tag (or set at runtime)
        public bool visible = true;
        public Color areaColor = new Color(0.12f, 0.56f, 1f, 0.25f);

        private static readonly Color IncompleteLightBlue = new Color(0.38f, 0.82f, 1f, 0.25f);
        private static readonly Color IncompleteDarkBlue = new Color(0.04f, 0.16f, 0.62f, 0.25f);
        private static readonly Color CompletedGreen = new Color(0.1f, 1f, 0.34f, 0.25f);
        private const float ColorPulseSpeed = 1.4f;
        private const float PulseUpdateInterval = 0.1f;
        private const float MinimumDimension = 0.5f;
        private const float HandlePickRadius = 18f;
        private const int HandleCount = 6;
        private static readonly Color HandleColor = new Color(0.18f, 0.7f, 1f, 1f);
        private static readonly Color HandleHoverColor = new Color(1f, 0.82f, 0.2f, 1f);

        private Material _material;
        private Transform _visual;
        private readonly List<MeshRenderer> _edgeRenderers = new List<MeshRenderer>();
        private readonly List<Transform> _resizeHandles = new List<Transform>();
        private readonly List<MeshRenderer> _resizeHandleRenderers = new List<MeshRenderer>();
        private readonly List<Material> _resizeHandleMaterials = new List<Material>();
        private bool _initialized;
        private bool _completed;
        private float _nextPulseUpdateTime;
        private bool _resizeEditingEnabled;
        private bool _resizePointerBlocked;
        private bool _resizeDirty;
        private bool _resizeCommitPending;
        private bool _dragChanged;
        private int _hoveredHandle = -1;
        private int _draggedHandle = -1;
        private Plane _dragPlane;
        private Vector3 _dragAxisWorld;
        private Vector3 _dragStartPosition;
        private Vector3 _dragStartSize;
        private Vector3 _dragStartFacePosition;
        private float _dragStartAxisParameter;
        private UnityEngine.Camera _activeCamera;

        public ChallengeArea(IntPtr ptr) : base(ptr)
        {
        }

        public static ChallengeArea CreateChallengeArea(Vector3 position, Vector3 size, Quaternion rotation, string playerTag = "Player", Color? color = null)
        {
            var root = new GameObject("ChallengeArea");
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.transform.localScale = size;

            var area = root.AddComponent(Il2CppType.Of<ChallengeArea>()).Cast<ChallengeArea>();
            area.playerTag = playerTag;
            if (color.HasValue) area.areaColor = color.Value;
            area.Init();
            return area;
        }

        public void Init()
        {
            if (_initialized) return;
            _initialized = true;

            gameObject.layer = LayerMask.NameToLayer("Default");

            // Build a wireframe volume; solid transparent HDRP materials were unstable in-game.
            var visualGo = new GameObject("ChallengeArea_Visual");
            visualGo.name = "ChallengeArea_Visual";
            visualGo.transform.SetParent(transform, false);
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localRotation = Quaternion.identity;
            visualGo.transform.localScale = Vector3.one;
            _visual = visualGo.transform;

            _material = BuildLineMat(areaColor);
            BuildWireframeEdges(_visual);
            BuildResizeHandles(_visual);

            SetVisible(visible);
        }

        private void Update()
        {
            if (!_initialized)
                return;

            if (!_completed &&
                visible &&
                _edgeRenderers.Count > 0 &&
                Time.unscaledTime >= _nextPulseUpdateTime)
            {
                _nextPulseUpdateTime = Time.unscaledTime + PulseUpdateInterval;
                float pulse = (Mathf.Sin(Time.time * ColorPulseSpeed) + 1f) * 0.5f;
                SetMaterialColor(Color.Lerp(IncompleteDarkBlue, IncompleteLightBlue, pulse));
            }

            UpdateResizeHandles();
        }

        public void SetSize(Vector3 size)
        {
            transform.localScale = size;
        }

        public void SetVisible(bool v)
        {
            visible = v;
            foreach (MeshRenderer renderer in _edgeRenderers)
            {
                if (renderer != null)
                    renderer.enabled = v;
            }

            UpdateResizeHandleVisibility();
        }

        public void SetColor(Color color)
        {
            areaColor = color;
            SetMaterialColor(color);
        }

        public void SetCompleted(bool completed)
        {
            _completed = completed;
            SetMaterialColor(completed ? CompletedGreen : IncompleteLightBlue);
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(localPoint.x) <= 0.5f &&
                   Mathf.Abs(localPoint.y) <= 0.5f &&
                   Mathf.Abs(localPoint.z) <= 0.5f;
        }

        public void SetResizeEditing(bool enabled, bool pointerBlocked)
        {
            _resizePointerBlocked = pointerBlocked;
            if (_resizeEditingEnabled == enabled)
                return;

            _resizeEditingEnabled = enabled;
            if (!enabled)
                EndResizeDrag(true);
            UpdateResizeHandleVisibility();
        }

        public bool TryConsumeResizeUpdate(
            out Vector3 position,
            out Vector3 size,
            out bool committed)
        {
            position = transform.position;
            size = transform.localScale;
            committed = _resizeCommitPending;
            bool changed = _resizeDirty || committed;
            _resizeDirty = false;
            _resizeCommitPending = false;
            return changed;
        }

        private void SetMaterialColor(Color color)
        {
            areaColor = new Color(color.r, color.g, color.b, 0.25f);
            if (_material != null)
            {
                Color visibleColor = new Color(color.r, color.g, color.b, 1f);
                SetMaterialColorProperty(_material, visibleColor);
            }
        }

        private static Material BuildLineMat(Color color)
        {
            Shader shader = Shader.Find("HDRP/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Sprites/Default") ??
                            Shader.Find("Standard");
            if (shader == null)
            {
                Log.Error("No shader found for ChallengeArea wireframe.");
                return null;
            }

            var material = new Material(shader);
            SetMaterialColorProperty(material, new Color(color.r, color.g, color.b, 1f));
            return material;
        }

        private void BuildWireframeEdges(Transform parent)
        {
            const float thickness = 0.025f;
            Vector3[] positions =
            {
                new Vector3(0f, -0.5f, -0.5f), new Vector3(0f, -0.5f, 0.5f),
                new Vector3(0f, 0.5f, -0.5f), new Vector3(0f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };

            Vector3[] scales =
            {
                new Vector3(1f, thickness, thickness), new Vector3(1f, thickness, thickness),
                new Vector3(1f, thickness, thickness), new Vector3(1f, thickness, thickness),
                new Vector3(thickness, 1f, thickness), new Vector3(thickness, 1f, thickness),
                new Vector3(thickness, 1f, thickness), new Vector3(thickness, 1f, thickness),
                new Vector3(thickness, thickness, 1f), new Vector3(thickness, thickness, 1f),
                new Vector3(thickness, thickness, 1f), new Vector3(thickness, thickness, 1f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                edge.name = $"ChallengeArea_Edge_{i:00}";
                edge.transform.SetParent(parent, false);
                edge.transform.localPosition = positions[i];
                edge.transform.localRotation = Quaternion.identity;
                edge.transform.localScale = scales[i];

                Collider collider = edge.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                MeshRenderer renderer = edge.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.sharedMaterial = _material;
                _edgeRenderers.Add(renderer);
            }
        }

        private void BuildResizeHandles(Transform parent)
        {
            for (int handleIndex = 0; handleIndex < HandleCount; handleIndex++)
            {
                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = $"ChallengeArea_ResizeHandle_{handleIndex:00}";
                handle.transform.SetParent(parent, false);
                handle.transform.localPosition = GetHandleLocalPosition(handleIndex);
                handle.transform.localRotation = Quaternion.identity;

                Collider collider = handle.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                MeshRenderer renderer = handle.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material material = BuildLineMat(HandleColor);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    renderer.sharedMaterial = material;
                    _resizeHandleMaterials.Add(material);
                    _resizeHandleRenderers.Add(renderer);
                }

                _resizeHandles.Add(handle.transform);
            }

            UpdateResizeHandleVisibility();
        }

        private void UpdateResizeHandles()
        {
            bool handlesVisible = _resizeEditingEnabled && visible;
            if (!handlesVisible)
            {
                UpdateResizeHandleVisibility();
                return;
            }

            RefreshCamera();
            if (_activeCamera == null)
                return;

            UpdateResizeHandleGeometry();
            HandleResizeInput();
        }

        private void UpdateResizeHandleGeometry()
        {
            float distance = Vector3.Distance(_activeCamera.transform.position, transform.position);
            float squareSize = Mathf.Clamp(distance * 0.025f, 0.16f, 0.65f);
            float thickness = Mathf.Clamp(squareSize * 0.18f, 0.035f, 0.12f);
            Vector3 scale = transform.lossyScale;
            scale = new Vector3(
                Mathf.Max(Mathf.Abs(scale.x), 0.001f),
                Mathf.Max(Mathf.Abs(scale.y), 0.001f),
                Mathf.Max(Mathf.Abs(scale.z), 0.001f));

            for (int handleIndex = 0; handleIndex < _resizeHandles.Count; handleIndex++)
            {
                Transform handle = _resizeHandles[handleIndex];
                if (handle == null)
                    continue;

                int axis = GetHandleAxis(handleIndex);
                handle.localPosition = GetHandleLocalPosition(handleIndex);
                handle.localScale = axis switch
                {
                    0 => new Vector3(thickness / scale.x, squareSize / scale.y, squareSize / scale.z),
                    1 => new Vector3(squareSize / scale.x, thickness / scale.y, squareSize / scale.z),
                    _ => new Vector3(squareSize / scale.x, squareSize / scale.y, thickness / scale.z)
                };

                SetHandleColor(
                    handleIndex,
                    handleIndex == _hoveredHandle || handleIndex == _draggedHandle
                        ? HandleHoverColor
                        : HandleColor);
            }
        }

        private void HandleResizeInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 mousePosition = mouse.position.ReadValue();
            if (_draggedHandle < 0)
                _hoveredHandle = _resizePointerBlocked ? -1 : FindHoveredHandle(mousePosition);

            if (mouse.leftButton.wasPressedThisFrame &&
                _hoveredHandle >= 0 &&
                !_resizePointerBlocked)
            {
                BeginResizeDrag(_hoveredHandle, mousePosition);
            }

            if (_draggedHandle >= 0 && mouse.leftButton.isPressed)
                ContinueResizeDrag(mousePosition);

            if (_draggedHandle >= 0 && mouse.leftButton.wasReleasedThisFrame)
                EndResizeDrag(true);
        }

        private int FindHoveredHandle(Vector2 mousePosition)
        {
            float bestDistance = HandlePickRadius;
            int bestHandle = -1;
            for (int handleIndex = 0; handleIndex < _resizeHandles.Count; handleIndex++)
            {
                Transform handle = _resizeHandles[handleIndex];
                if (handle == null)
                    continue;

                Vector3 screenPoint = _activeCamera.WorldToScreenPoint(handle.position);
                if (screenPoint.z <= 0f)
                    continue;

                float distance = Vector2.Distance(mousePosition, new Vector2(screenPoint.x, screenPoint.y));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHandle = handleIndex;
                }
            }

            return bestHandle;
        }

        private void BeginResizeDrag(int handleIndex, Vector2 mousePosition)
        {
            _draggedHandle = handleIndex;
            _dragChanged = false;
            _dragStartPosition = transform.position;
            _dragStartSize = transform.localScale;
            _dragStartFacePosition = _resizeHandles[handleIndex].position;

            Vector3 localAxis = GetHandleLocalAxis(handleIndex);
            _dragAxisWorld = (transform.rotation * localAxis).normalized;
            Vector3 planeNormal = Vector3.Cross(
                _dragAxisWorld,
                Vector3.Cross(_activeCamera.transform.forward, _dragAxisWorld));
            if (planeNormal.sqrMagnitude < 0.001f)
            {
                planeNormal = Vector3.Cross(
                    _dragAxisWorld,
                    Vector3.Cross(_activeCamera.transform.up, _dragAxisWorld));
            }

            _dragPlane = new Plane(planeNormal.normalized, _dragStartFacePosition);
            Ray ray = _activeCamera.ScreenPointToRay(mousePosition);
            if (!_dragPlane.Raycast(ray, out float enter))
            {
                EndResizeDrag(false);
                return;
            }

            _dragStartAxisParameter =
                Vector3.Dot(ray.GetPoint(enter) - _dragStartFacePosition, _dragAxisWorld);
        }

        private void ContinueResizeDrag(Vector2 mousePosition)
        {
            Ray ray = _activeCamera.ScreenPointToRay(mousePosition);
            if (!_dragPlane.Raycast(ray, out float enter))
                return;

            float currentParameter =
                Vector3.Dot(ray.GetPoint(enter) - _dragStartFacePosition, _dragAxisWorld);
            float requestedDelta = currentParameter - _dragStartAxisParameter;
            int axis = GetHandleAxis(_draggedHandle);
            float startDimension = GetAxisValue(_dragStartSize, axis);
            float newDimension = Mathf.Max(MinimumDimension, startDimension + requestedDelta);
            float appliedDelta = newDimension - startDimension;

            Vector3 newSize = _dragStartSize;
            SetAxisValue(ref newSize, axis, newDimension);
            transform.localScale = newSize;
            transform.position = _dragStartPosition + _dragAxisWorld * (appliedDelta * 0.5f);

            if (Mathf.Abs(appliedDelta) > 0.0001f)
            {
                _dragChanged = true;
                _resizeDirty = true;
            }
        }

        private void EndResizeDrag(bool commit)
        {
            if (_draggedHandle >= 0 && commit && _dragChanged)
                _resizeCommitPending = true;

            _draggedHandle = -1;
            _hoveredHandle = -1;
            _dragChanged = false;
        }

        private void UpdateResizeHandleVisibility()
        {
            bool handlesVisible = _resizeEditingEnabled && visible;
            foreach (Transform handle in _resizeHandles)
            {
                if (handle != null && handle.gameObject.activeSelf != handlesVisible)
                    handle.gameObject.SetActive(handlesVisible);
            }
        }

        private void RefreshCamera()
        {
            if (_activeCamera != null && _activeCamera.isActiveAndEnabled)
                return;

            _activeCamera = UnityEngine.Camera.main;
            if (_activeCamera != null)
                return;

            Il2CppCinemachine.CinemachineBrain brain =
                GameObject.FindObjectOfType<Il2CppCinemachine.CinemachineBrain>();
            if (brain != null && brain.gameObject.TryGetComponent(out UnityEngine.Camera camera))
                _activeCamera = camera;
        }

        private void SetHandleColor(int handleIndex, Color color)
        {
            if (handleIndex < 0 || handleIndex >= _resizeHandleMaterials.Count)
                return;

            SetMaterialColorProperty(_resizeHandleMaterials[handleIndex], color);
        }

        private static Vector3 GetHandleLocalPosition(int handleIndex)
        {
            int axis = GetHandleAxis(handleIndex);
            float sign = GetHandleSign(handleIndex);
            return axis switch
            {
                0 => new Vector3(sign * 0.5f, 0f, 0f),
                1 => new Vector3(0f, sign * 0.5f, 0f),
                _ => new Vector3(0f, 0f, sign * 0.5f)
            };
        }

        private static Vector3 GetHandleLocalAxis(int handleIndex)
        {
            int axis = GetHandleAxis(handleIndex);
            float sign = GetHandleSign(handleIndex);
            return axis switch
            {
                0 => Vector3.right * sign,
                1 => Vector3.up * sign,
                _ => Vector3.forward * sign
            };
        }

        private static int GetHandleAxis(int handleIndex)
        {
            return Mathf.Clamp(handleIndex / 2, 0, 2);
        }

        private static float GetHandleSign(int handleIndex)
        {
            return handleIndex % 2 == 0 ? -1f : 1f;
        }

        private static float GetAxisValue(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static void SetAxisValue(ref Vector3 value, int axis, float axisValue)
        {
            if (axis == 0)
                value.x = axisValue;
            else if (axis == 1)
                value.y = axisValue;
            else
                value.z = axisValue;
        }

        private static void SetMaterialColorProperty(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_UnlitColor"))
                material.SetColor("_UnlitColor", color);

            material.color = color;
        }

        private void OnDestroy()
        {
            EndResizeDrag(false);
            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }

            foreach (Material material in _resizeHandleMaterials)
            {
                if (material != null)
                    Destroy(material);
            }
            _resizeHandleMaterials.Clear();
        }
    }
}
