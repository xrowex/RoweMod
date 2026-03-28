using rowemod.Utils;
using UnityEngine;

namespace rowemod.Challenges
{
    public class ChallengeArea : MonoBehaviour
    {
        public string playerTag = "VehicleColliders";      // Make this match your game’s player tag (or set at runtime)
        public bool visible = true;
        public Color areaColor = new Color(0f, 1f, 0.6f, 0.22f);

        private BoxCollider _trigger;
        private MeshRenderer _renderer;
        private Transform _visual;
        private bool _initialized;

        public static ChallengeArea CreateChallengeArea(Vector3 position, Vector3 size, Quaternion rotation, string playerTag = "Player", Color? color = null)
        {
            var root = new GameObject("ChallengeArea");
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.transform.localScale = size;

            if (root.GetComponent<BoxCollider>() == null)
                root.AddComponent<BoxCollider>();

            root.GetComponent<BoxCollider>().isTrigger = true;
            
            var area = root.AddComponent<ChallengeArea>();
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
            
            // Build a visible child cube that matches the collider
            var visualGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (visualGo.GetComponent<Collider>())
                Destroy(visualGo.GetComponent<Collider>()); 
            
            visualGo.name = "ChallengeArea_Visual";
            visualGo.transform.SetParent(transform, false);
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localRotation = Quaternion.identity;
            visualGo.transform.localScale = Vector3.one; // matches parent scale
            _visual = visualGo.transform;
            
            _renderer = visualGo.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            
            // Transparent material (HDRP-ready; falls back if HDRP/Lit is missing)
            _renderer.sharedMaterial = BuildTransparentMat(areaColor);
            
            SetVisible(visible);
        }

        public void SetSize(Vector3 size)
        {
            transform.localScale = size;
        }

        public void SetVisible(bool v)
        {
            visible = v;
            if (_renderer != null) _renderer.enabled = v;
        }

        public void SetColor(Color color)
        {
            areaColor = color;
            if (_renderer != null)
            {
                _renderer.sharedMaterial = BuildTransparentMat(color);
            }
        }

        // HDRP transparent material helper
        private static Material BuildTransparentMat(Color color)
        {
            Shader hdrp = Shader.Find("HDRP/Lit");
            if (hdrp != null)
            {
                var m = new Material(hdrp);
                // Core HDRP properties for transparency
                m.SetFloat("_SurfaceType", 1f);      // 0=Opaque, 1=Transparent
                m.SetFloat("_BlendMode", 0f);        // 0=Alpha, 1=Additive, 2=Premultiplied
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_CullMode", 2f);         // 0=Off,1=Front,2=Back
                m.SetFloat("_EnableFogOnTransparent", 1f);
                m.SetColor("_BaseColor", color);
                // Keyword nudges (some HDRP versions need these)
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_BLENDMODE_ALPHA");
                return m;
            }
            else
            {
                Log.Error("HDRP shader null!");
                return null;
            }
            
        }

        private void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            {
                // Hook: player entered area — start tracking points or mark "armed"
                Debug.Log("[ChallengeArea] Player ENTER");
                ChallengeAreaManager.NotifyAreaEnter(this, other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            {
                // Hook: player left area — finalize / fail if needed
                Debug.Log("[ChallengeArea] Player EXIT");
                ChallengeAreaManager.NotifyAreaExit(this, other);
            }
        }
    }
}
