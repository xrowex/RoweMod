using System;
using Il2CppInterop.Runtime;
using MelonLoader;
using rowemod.Utils;
using UnityEngine;

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
        
        private BoxCollider _trigger;
        private Material _material;
        private Transform _visual;
        private readonly List<MeshRenderer> _edgeRenderers = new List<MeshRenderer>();
        private bool _initialized;
        private bool _completed;

        public ChallengeArea(IntPtr ptr) : base(ptr)
        {
        }

        public static ChallengeArea CreateChallengeArea(Vector3 position, Vector3 size, Quaternion rotation, string playerTag = "Player", Color? color = null)
        {
            var root = new GameObject("ChallengeArea");
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.transform.localScale = size;

            if (root.GetComponent<BoxCollider>() == null)
                root.AddComponent(Il2CppType.Of<BoxCollider>());

            root.GetComponent<BoxCollider>().isTrigger = true;
            
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

            // Trigger collider (the actual challenge volume)
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
            _trigger.size = Vector3.one;
            
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
            
            SetVisible(visible);
        }

        private void Update()
        {
            if (!_initialized || _completed || _edgeRenderers.Count == 0)
                return;

            float pulse = (Mathf.Sin(Time.time * ColorPulseSpeed) + 1f) * 0.5f;
            SetMaterialColor(Color.Lerp(IncompleteDarkBlue, IncompleteLightBlue, pulse));
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
                renderer.material = _material;
                _edgeRenderers.Add(renderer);
            }
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

        private void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            {
                // Hook: player entered area - start tracking points or mark "armed"
                Debug.Log("[ChallengeArea] Player ENTER");
                ChallengeAreaManager.NotifyAreaEnter(this, other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            {
                // Hook: player left area - finalize / fail if needed
                Debug.Log("[ChallengeArea] Player EXIT");
                ChallengeAreaManager.NotifyAreaExit(this, other);
            }
        }
    }
}
