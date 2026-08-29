#if UNITY_EDITOR
using UnityEngine;

// Scene bookkeeping only: no Update, solver, Animator or runtime behaviour.
// This file stays outside Editor so Unity can serialize the component in the
// authoring scene. The owning visual root is also tagged EditorOnly.
[AddComponentMenu("")]
public sealed class RowePreviewModelState : MonoBehaviour
{
    [HideInInspector] public Animator rider;
    [HideInInspector] public GameObject sourceModel;
    [HideInInspector] public Renderer[] originalRenderers;
    [HideInInspector] public bool[] originalEnabled;
    [HideInInspector] public GameObject sourceSceneObject;
    [HideInInspector] public bool sourceWasActive;
    [HideInInspector] public SkinnedMeshRenderer[] previewSkins;
}
#endif
