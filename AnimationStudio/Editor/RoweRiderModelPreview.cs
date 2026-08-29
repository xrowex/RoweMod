using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// A one-time skin rebind. The original Humanoid and its bones remain the sole
// animation/IK rig; there is no extra Animator or per-frame bone follower.
public static class RoweRiderModelPreview
{
    sealed class SkinBinding
    {
        public SkinnedMeshRenderer source;
        public Transform[] bones;
        public Transform rootBone;
    }

    public static RowePreviewModelState Current(Animator rider) => rider
        ? rider.GetComponentsInChildren<RowePreviewModelState>(true).SingleOrDefault(s => s.rider == rider)
        : null;

    static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode())
            throw new InvalidOperationException("Stop posing / animation preview and leave Play Mode before changing the preview character.");
    }

    static bool Finite(Matrix4x4 matrix)
    {
        for (int i = 0; i < 16; i++) if (!float.IsFinite(matrix[i])) return false;
        return Mathf.Abs(matrix.determinant) > .00000001f;
    }

    static List<SkinBinding> Validate(Animator rider, GameObject model)
    {
        RequireEditMode();
        if (!RoweStudioRigs.IsRider(rider)) throw new InvalidOperationException("Choose the working scene Humanoid as Rider, not the imported model's Animator.");
        if (!model) throw new InvalidOperationException("Choose your character model or its scene object first.");
        if (model.transform.IsChildOf(rider.transform) || rider.transform.IsChildOf(model.transform))
            throw new InvalidOperationException("Choose a separate model, not the workbench rider or an already connected preview.");
        if (!EditorUtility.IsPersistent(model) && (!model.scene.IsValid() || !model.scene.isLoaded || model.scene != rider.gameObject.scene))
            throw new InvalidOperationException("The character must be a project model or an object in the rider's scene.");

        var targets = rider.GetComponentsInChildren<Transform>(true)
            .Where(t => !t.GetComponentInParent<RowePreviewModelState>())
            .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        Transform Map(Transform bone, bool weighted)
        {
            if (bone && targets.TryGetValue(bone.name, out var matches) && matches.Length == 1) return matches[0];
            if (!weighted) return null;
            throw new InvalidOperationException("Cannot connect skin bone '" + (bone ? bone.name : "missing")
                + "': it must match exactly one bone in the rider. Use a model skinned to this skeleton.");
        }

        var result = new List<SkinBinding>();
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = skin.sharedMesh;
            if (!mesh || mesh.vertexCount == 0) continue;
            var sourceBones = skin.bones;
            var bindPoses = mesh.bindposes;
            if (sourceBones.Length == 0 || sourceBones.Length != bindPoses.Length || bindPoses.Any(p => !Finite(p)))
                throw new InvalidOperationException(skin.name + " has missing or invalid skin bind poses.");
            var weighted = new bool[sourceBones.Length];
            using (var weights = mesh.GetAllBoneWeights())
                foreach (var weight in weights)
                {
                    if (!float.IsFinite(weight.weight) || weight.weight < 0 || weight.boneIndex < 0 || weight.boneIndex >= weighted.Length)
                        throw new InvalidOperationException(skin.name + " has invalid bone weights.");
                    if (weight.weight > 0) weighted[weight.boneIndex] = true;
                }
            if (!weighted.Any(w => w)) throw new InvalidOperationException(skin.name + " is not a weighted character skin.");
            var root = skin.rootBone ? Map(skin.rootBone, true) : rider.transform;
            var bones = sourceBones.Select((b, i) => Map(b, weighted[i]) ?? root).ToArray();
            result.Add(new SkinBinding { source = skin, bones = bones, rootBone = root });
        }
        if (result.Count == 0) throw new InvalidOperationException("This selection has no skinned meshes. Select the character's root object or prefab.");
        if (!result.Any(b => SkinVisible(b.source, model.transform)))
            throw new InvalidOperationException("This character has no enabled, visible skins. Enable the body mesh before connecting it.");
        return result;
    }

    // Stop at the selected root: an already connected source can be inactive.
    static bool SkinVisible(SkinnedMeshRenderer skin, Transform root)
    {
        if (!skin.enabled) return false;
        for (var t = skin.transform; t && t != root; t = t.parent) if (!t.gameObject.activeSelf) return false;
        return true;
    }

    public static RowePreviewModelState Connect(Animator rider, GameObject model)
    {
        var bindings = Validate(rider, model); // No mutation if any skin is incompatible.
        var old = Current(rider);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Connect preview character");
        try
        {
            if (old) RestoreInternal(old);
            var previous = rider.GetComponentsInChildren<Renderer>(true);
            var root = new GameObject("Preview Character - " + model.name) { tag = "EditorOnly" };
            Undo.RegisterCreatedObjectUndo(root, "Connect preview character");
            root.transform.SetParent(rider.transform, false);
            var state = Undo.AddComponent<RowePreviewModelState>(root);
            var previews = new List<SkinnedMeshRenderer>();
            foreach (var binding in bindings)
            {
                var source = binding.source;
                var part = new GameObject(source.name);
                Undo.RegisterCreatedObjectUndo(part, "Connect preview skin");
                part.transform.SetParent(root.transform, false);
                // Preserve the imported mesh's own frame (including FBX units),
                // excluding the source object's scene placement. Do not infer
                // a bind frame from either skeleton's currently displayed pose.
                var frame = model.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                part.transform.localPosition = frame.GetColumn(3);
                part.transform.localRotation = frame.rotation;
                part.transform.localScale = frame.lossyScale;
                var preview = Undo.AddComponent<SkinnedMeshRenderer>(part);
                Undo.RegisterCompleteObjectUndo(preview, "Connect preview skin");
                preview.sharedMesh = source.sharedMesh;
                preview.sharedMaterials = source.sharedMaterials;
                preview.bones = binding.bones;
                preview.rootBone = binding.rootBone;
                preview.localBounds = source.localBounds;
                preview.quality = source.quality;
                preview.shadowCastingMode = source.shadowCastingMode;
                preview.receiveShadows = source.receiveShadows;
                preview.updateWhenOffscreen = true;
                preview.enabled = SkinVisible(source, model.transform);
                for (int i = 0; i < source.sharedMesh.blendShapeCount; i++) preview.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
                previews.Add(preview);
            }
            // RegisterCreatedObjectUndo / AddComponent flush existing undo
            // recordings. Record bookkeeping AFTER all children are created,
            // otherwise Redo loses late fields such as sourceWasActive.
            Undo.RegisterCompleteObjectUndo(state, "Remember preview character");
            state.rider = rider;
            state.sourceModel = model;
            state.originalRenderers = previous;
            state.originalEnabled = previous.Select(r => r.enabled).ToArray();
            state.sourceSceneObject = EditorUtility.IsPersistent(model) ? null : model;
            state.sourceWasActive = model.activeSelf;
            state.previewSkins = previews.ToArray();
            foreach (var renderer in previous)
            {
                Undo.RecordObject(renderer, "Hide mannequin");
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if (!EditorUtility.IsPersistent(model))
            {
                Undo.RecordObject(model, "Hide separate source character");
                model.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            }
            EditorUtility.SetDirty(state);
            EditorSceneManager.MarkSceneDirty(rider.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return state;
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    static void RestoreInternal(RowePreviewModelState state)
    {
        for (int i = 0; i < state.originalRenderers.Length; i++)
        {
            var renderer = state.originalRenderers[i];
            if (!renderer) continue;
            Undo.RecordObject(renderer, "Restore mannequin");
            renderer.enabled = state.originalEnabled[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        if (state.sourceSceneObject)
        {
            Undo.RecordObject(state.sourceSceneObject, "Restore source character");
            state.sourceSceneObject.SetActive(state.sourceWasActive);
            PrefabUtility.RecordPrefabInstancePropertyModifications(state.sourceSceneObject);
        }
        Undo.DestroyObjectImmediate(state.gameObject); // Only the owned preview; Undo restores it.
    }

    public static void Restore(Animator rider)
    {
        RequireEditMode();
        var state = Current(rider);
        if (!state) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Show original mannequin");
        try
        {
            RestoreInternal(state);
            EditorSceneManager.MarkSceneDirty(rider.gameObject.scene);
            Undo.CollapseUndoOperations(group);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
}

public sealed partial class RoweIKPoseEditor
{
    [SerializeField] GameObject studioModel;

    void ResolveStudioRigs(bool requireBike = false) => RoweStudioRigs.Resolve(ref studioRider, ref studioBike, requireBike);

    public void ConnectPreviewCharacter(GameObject model)
    {
        Stop();
        ResolveStudioRigs();
        RoweRiderModelPreview.Connect(studioRider, model);
        studioModel = model;
        status = model.name + " connected. The existing Humanoid, IK controls and animation clips are unchanged. Ctrl+Z undoes the character swap.";
        Repaint(); SceneView.RepaintAll();
    }

    void DrawPreviewCharacter()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Preview character", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use a model skinned to this rider's bones. This changes only its appearance; keep Human Temp as Rider. It does not put the character mesh in exported animations.", MessageType.None);
        studioModel = (GameObject)EditorGUILayout.ObjectField("Your model", studioModel, typeof(GameObject), true);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!Selection.activeGameObject))
                if (GUILayout.Button("Use selected model")) studioModel = Selection.activeGameObject;
            using (new EditorGUI.DisabledScope(!studioModel))
                if (GUILayout.Button("Connect character")) Run(() => ConnectPreviewCharacter(studioModel));
            using (new EditorGUI.DisabledScope(!studioRider || !RoweRiderModelPreview.Current(studioRider)))
                if (GUILayout.Button("Show mannequin")) Run(() => { Stop(); RoweRiderModelPreview.Restore(studioRider); status = "Original mannequin restored. Your character and saved animations were kept."; });
        }
        var connected = RoweRiderModelPreview.Current(studioRider);
        if (connected) EditorGUILayout.LabelField("Connected: " + (connected.sourceModel ? connected.sourceModel.name : connected.name), EditorStyles.miniLabel);
    }
}
