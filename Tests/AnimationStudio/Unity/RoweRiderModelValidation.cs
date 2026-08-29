using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoweRiderModelValidation
{
    [Serializable] public class Report
    {
        public bool passed, rigResolution, boundAllWeightedBones, preservedSourceMesh, preservedMaterials,
            hiddenMannequinAndHead, hiddenSeparateSource, sourcePlacementIgnored, noExtraAnimator,
            bonesAndAvatarUnchanged, undoRestores, redoConnects, reapplySinglePreview,
            skinMovesWithClip, skinMovesWithIK, previewRestores, restoreMannequin,
            restoreUndoRedo, persistedScene, restoredAfterReload, invalidRejectedAtomically,
            sourceFilesUnchanged;
        public Vector3 bodySize;
        public float clipVertexMovement, ikVertexMovement;
        public string error, validationScene, visibilityTrace;
    }

    static Vector3[] Vertices(SkinnedMeshRenderer skin)
    {
        var baked = new Mesh();
        try
        {
            // true uses the renderer's complete frame (including FBX unit
            // scale); false would be multiplied by that scale a second time.
            skin.BakeMesh(baked, true);
            return baked.vertices.Select(skin.transform.TransformPoint).ToArray();
        }
        finally { UnityEngine.Object.DestroyImmediate(baked); }
    }

    static float Difference(Vector3[] before, Vector3[] after) => before.Zip(after, Vector3.Distance).Max();
    static bool OriginalVisibility(Renderer[] renderers, bool[] enabled)
        => renderers.Select((r, i) => r && r.enabled == enabled[i]).All(v => v);
    static bool AllMapped(RowePreviewModelState state)
        => state.previewSkins.All(s => s.rootBone.IsChildOf(state.rider.transform)
            && s.bones.All(b => b && b.IsChildOf(state.rider.transform) && !b.GetComponentInParent<RowePreviewModelState>()));
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    public static void Run()
    {
        var report = new Report();
        RoweIKSession session = null;
        try
        {
            Assert(Application.isBatchMode && Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-"), "Run only in isolated Validation-* project.");
            var sources = new[] { "Assets/Reference/ROWEBODYV2.fbx", "Assets/Reference/ROWEBODYV2.fbx.meta", "Assets/Reference/ROWEBODYV2.prefab", "Assets/CustomClips/RoweMod_Custom_My Trick 3.anim" };
            var originals = sources.Select(File.ReadAllBytes).ToArray();
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            RoweStudioRigsValidation.RunAssertions(); report.rigResolution = true;
            var rider = RowePairedWorkbench.FindRig("Human Temp"); var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            var avatar = rider.avatar; var bones = RoweIKSession.Bones.Select(rider.GetBoneTransform).ToArray();
            var originalPose = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            var renderers = rider.GetComponentsInChildren<Renderer>(true); var enabled = renderers.Select(r => r.enabled).ToArray();
            int animatorCount = rider.GetComponentsInChildren<Animator>(true).Length;
            var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reference/ROWEBODYV2.prefab"));
            source.name = "ROWEBODYV2 - validation";
            source.transform.position = new Vector3(15, 8, -13);
            source.transform.rotation = Quaternion.Euler(0, 63, 0);
            var sourceSkin = source.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var state = RoweRiderModelPreview.Connect(rider, source);
            report.visibilityTrace = "initial=" + state.sourceWasActive + "; enabled=" + string.Join(",", state.originalEnabled);
            var skin = state.previewSkins.Single();
            report.boundAllWeightedBones = AllMapped(state);
            report.preservedSourceMesh = skin.sharedMesh == sourceSkin.sharedMesh;
            report.preservedMaterials = skin.sharedMaterials.SequenceEqual(sourceSkin.sharedMaterials);
            report.hiddenMannequinAndHead = renderers.All(r => !r.enabled) && renderers.Any(r => r.name.Contains("Head motion marker"));
            report.hiddenSeparateSource = !source.activeSelf;
            report.noExtraAnimator = rider.GetComponentsInChildren<Animator>(true).Length == animatorCount && state.GetComponentsInChildren<Animator>(true).Length == 0;
            report.bonesAndAvatarUnchanged = originalPose.Changed() == 0 && avatar == rider.avatar && bones.SequenceEqual(RoweIKSession.Bones.Select(rider.GetBoneTransform));
            var restVertices = Vertices(skin);
            var bounds = new Bounds(restVertices[0], Vector3.zero); foreach (var v in restVertices) bounds.Encapsulate(v);
            report.bodySize = bounds.size;
            report.sourcePlacementIgnored = bounds.size.y > 1 && bounds.size.y < 2.5f && bounds.size.x < 2.5f
                && Vector3.Distance(bounds.center, rider.GetBoneTransform(HumanBodyBones.Hips).position) < 1;
            Assert(report.sourcePlacementIgnored, "Preview geometry has wrong scale or follows source placement.");

            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            report.undoRestores = !RoweRiderModelPreview.Current(rider) && source.activeSelf && OriginalVisibility(renderers, enabled);
            Undo.PerformRedo(); state = RoweRiderModelPreview.Current(rider);
            report.visibilityTrace += " | redo=" + state.sourceWasActive + "; enabled=" + string.Join(",", state.originalEnabled);
            report.redoConnects = state && state.sourceWasActive && state.previewSkins.Length == 1
                && AllMapped(state) && !source.activeSelf && renderers.All(r => !r.enabled);
            Assert(report.redoConnects, "Character rebind did not survive Redo.");
            state = RoweRiderModelPreview.Connect(rider, source);
            report.visibilityTrace += " | reapply=" + state.sourceWasActive + "; enabled=" + string.Join(",", state.originalEnabled);
            report.reapplySinglePreview = rider.GetComponentsInChildren<RowePreviewModelState>(true).Length == 1 && state.sourceWasActive && !source.activeSelf;
            skin = state.previewSkins.Single(); restVertices = Vertices(skin);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sources[3]);
            session = new RoweIKSession(rider, bike);
            session.Sample(clip, null, 1);
            var posed = Vertices(skin);
            report.clipVertexMovement = Difference(restVertices, posed);
            report.skinMovesWithClip = report.clipVertexMovement > .05f;
            session.MoveControl(0, session.positions[0] + new Vector3(.03f, .12f, .09f), session.rotations[0]);
            report.ikVertexMovement = Difference(posed, Vertices(skin));
            report.skinMovesWithIK = report.ikVertexMovement > .015f;
            session.Dispose(); session = null;
            report.previewRestores = originalPose.Changed() == 0 && Difference(restVertices, Vertices(skin)) < .0001f;

            var invalid = new GameObject("Invalid preview source");
            try { RoweRiderModelPreview.Connect(rider, invalid); }
            catch (InvalidOperationException) { report.invalidRejectedAtomically = state == RoweRiderModelPreview.Current(rider) && !source.activeSelf; }
            UnityEngine.Object.DestroyImmediate(invalid);
            RoweRiderModelPreview.Restore(rider);
            report.visibilityTrace += " | restore source=" + source.activeSelf + "; enabled=" + string.Join(",", renderers.Select(r => r.enabled));
            report.restoreMannequin = !RoweRiderModelPreview.Current(rider) && source.activeSelf && OriginalVisibility(renderers, enabled);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            bool restoreUndo = RoweRiderModelPreview.Current(rider) && !source.activeSelf;
            Undo.PerformRedo();
            report.restoreUndoRedo = restoreUndo && !RoweRiderModelPreview.Current(rider) && source.activeSelf && OriginalVisibility(renderers, enabled);

            state = RoweRiderModelPreview.Connect(rider, source);
            report.validationScene = "Assets/Scenes/Validation-PreviewCharacter-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            EditorSceneManager.SaveScene(rider.gameObject.scene, report.validationScene);
            EditorSceneManager.OpenScene(report.validationScene);
            rider = RowePairedWorkbench.FindRig("Human Temp");
            state = RoweRiderModelPreview.Current(rider);
            report.persistedScene = state && AllMapped(state) && !state.sourceSceneObject.activeSelf
                && state.originalRenderers.All(r => r && !r.enabled) && state.previewSkins.All(s => s.sharedMesh);
            Assert(report.persistedScene, "Saved scene lost the model connection or restoration metadata.");
            var savedRenderers = state.originalRenderers.ToArray(); var savedEnabled = state.originalEnabled.ToArray();
            var savedSource = state.sourceSceneObject;
            RoweRiderModelPreview.Restore(rider);
            report.restoredAfterReload = savedSource.activeSelf && OriginalVisibility(savedRenderers, savedEnabled);
            report.sourceFilesUnchanged = sources.Select((p, i) => File.ReadAllBytes(p).SequenceEqual(originals[i])).All(v => v);
            report.passed = typeof(Report).GetFields().Where(f => f.FieldType == typeof(bool) && f.Name != "passed").All(f => (bool)f.GetValue(report));
        }
        catch (Exception ex) { report.error = ex.ToString(); }
        finally
        {
            session?.Dispose();
            File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "studio-rider-model-validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("ROWE_RIDER_MODEL_VALIDATION " + JsonUtility.ToJson(report));
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
}
