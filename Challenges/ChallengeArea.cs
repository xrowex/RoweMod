using UnityEngine;

namespace rowemod.Challenges
{
    public class ChallengeArea : MonoBehaviour
    {
        public string playerTag = "Player";      // Make this match your game’s player tag (or set at runtime)
        public bool visible = true;

        private BoxCollider _trigger;
        private MeshRenderer _renderer;
        private Transform _visual;

        void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Default");

            // Trigger collider (the actual challenge volume)
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;

            // Build a visible child cube that matches the collider
            var visualGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visualGo.GetComponent<Collider>()); // we only want the parent collider
            visualGo.name = "ChallengeArea_Visual";
            visualGo.transform.SetParent(transform, false);
            visualGo.transform.localPosition = Vector3.zero;
            visualGo.transform.localRotation = Quaternion.identity;
            visualGo.transform.localScale = Vector3.one; // will be scaled by parent
            _visual = visualGo.transform;

            _renderer = visualGo.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            // Transparent material (HDRP-ready; falls back if HDRP/Lit is missing)
            _renderer.sharedMaterial = BuildTransparentMat(new Color(0f, 1f, 0.6f, 0.22f));

            SetVisible(visible);
        }

        public void SetSize(Vector3 size)
        {
            _trigger.size = size;
            // Visual is scaled by parent transform; keep local scale = 1 and scale the root instead (recommended)
        }

        public void SetVisible(bool v)
        {
            visible = v;
            if (_renderer != null) _renderer.enabled = v;
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

            // Fallback: Standard
            Shader std = Shader.Find("Standard");
            var stdMat = new Material(std);
            stdMat.SetColor("_Color", color);
            stdMat.SetFloat("_Mode", 3f); // Transparent
            stdMat.EnableKeyword("_ALPHABLEND_ON");
            stdMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            stdMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            stdMat.SetInt("_ZWrite", 0);
            stdMat.renderQueue = 3000;
            return stdMat;
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
