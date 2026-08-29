using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Transient diagnostic in the isolated test scene. Does not save scenes/assets
// or modify production code; compares native BakeMesh and actual camera output.
public static class RoweRiderModelBakeProbe
{
    [Serializable] public class ErrorRow
    {
        public string frame;
        public float mean, max;
        public Vector3 size, center;
    }
    [Serializable] public class CaseRow
    {
        public string name, rootBone, image;
        public Matrix4x4 transformMatrix, rendererMatrix, rootBoneMatrix;
        public Vector3 localScale, lossyScale, nativeBoundsSize, nativeBoundsCenter,
            localBoundsSize, cpuSize, cpuCenter, bakeFalseSize, bakeTrueSize;
        public ErrorRow[] bakeFalse, bakeTrue;
        public int drawnPixels, minX, maxX, minY, maxY;
        public bool touchesBorder;
    }
    [Serializable] public class Report
    {
        public bool passed;
        public string error, quality;
        public CaseRow[] cases;
    }
    static Bounds Bound(Vector3[] points)
    {
        var b = new Bounds(points[0], Vector3.zero);
        foreach (var p in points.Skip(1)) b.Encapsulate(p);
        return b;
    }
    static Vector3[] Cpu(SkinnedMeshRenderer skin)
    {
        var mesh = skin.sharedMesh; var bind = mesh.bindposes; var vertices = mesh.vertices; var weights = mesh.boneWeights;
        var matrices = skin.bones.Select((bone, i) => bone.localToWorldMatrix * bind[i]).ToArray();
        var result = new Vector3[vertices.Length];
        for (int i = 0; i < result.Length; i++)
        {
            var w = weights[i]; var p = vertices[i];
            if (w.weight0 > 0) result[i] += matrices[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0;
            if (w.weight1 > 0) result[i] += matrices[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1;
            if (w.weight2 > 0) result[i] += matrices[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2;
            if (w.weight3 > 0) result[i] += matrices[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
        }
        return result;
    }
    static Vector3[] Bake(SkinnedMeshRenderer skin, bool scale)
    {
        var mesh = new Mesh();
        try { skin.BakeMesh(mesh, scale); return mesh.vertices; }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }
    static ErrorRow Error(string label, Vector3[] baked, Matrix4x4 frame, Vector3[] cpu)
    {
        var world = baked.Select(frame.MultiplyPoint3x4).ToArray(); var bounds = Bound(world);
        var errors = world.Zip(cpu, Vector3.Distance).ToArray();
        return new ErrorRow { frame = label, mean = errors.Average(), max = errors.Max(), size = bounds.size, center = bounds.center };
    }
    static CaseRow Inspect(string name, SkinnedMeshRenderer skin, Transform modelRoot, bool render)
    {
        var cpu = Cpu(skin); var bounds = Bound(cpu);
        var withoutScale = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
        var root = skin.rootBone ? skin.rootBone.localToWorldMatrix : Matrix4x4.identity;
        var frames = new[] {
            new KeyValuePair<string, Matrix4x4>("raw", Matrix4x4.identity),
            new KeyValuePair<string, Matrix4x4>("transform", skin.transform.localToWorldMatrix),
            new KeyValuePair<string, Matrix4x4>("transformWithoutScale", withoutScale),
            new KeyValuePair<string, Matrix4x4>("renderer", skin.localToWorldMatrix),
            new KeyValuePair<string, Matrix4x4>("rootBone", root),
            new KeyValuePair<string, Matrix4x4>("modelRoot", modelRoot.localToWorldMatrix)
        };
        var rawFalse = Bake(skin, false); var rawTrue = Bake(skin, true);
        var r = new CaseRow {
            name = name, rootBone = skin.rootBone ? skin.rootBone.name : "<none>",
            transformMatrix = skin.transform.localToWorldMatrix, rendererMatrix = skin.localToWorldMatrix, rootBoneMatrix = root,
            localScale = skin.transform.localScale, lossyScale = skin.transform.lossyScale,
            nativeBoundsSize = skin.bounds.size, nativeBoundsCenter = skin.bounds.center,
            localBoundsSize = skin.localBounds.size, cpuSize = bounds.size, cpuCenter = bounds.center,
            bakeFalseSize = Bound(rawFalse).size, bakeTrueSize = Bound(rawTrue).size,
            bakeFalse = frames.Select(f => Error(f.Key, rawFalse, f.Value, cpu)).ToArray(),
            bakeTrue = frames.Select(f => Error(f.Key, rawTrue, f.Value, cpu)).ToArray()
        };
        if (render) Render(r, skin, bounds);
        return r;
    }
    static void Render(CaseRow report, SkinnedMeshRenderer skin, Bounds expected)
    {
        var oldMaterials = skin.sharedMaterials; int oldLayer = skin.gameObject.layer;
        var material = new Material(Shader.Find("Unlit/Color")) { color = Color.white };
        var cameraObject = new GameObject("Temporary Rowe model measurement camera");
        var camera = cameraObject.AddComponent<Camera>();
        var rt = new RenderTexture(512, 512, 24); var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMesh.subMeshCount).ToArray();
            skin.gameObject.layer = 31;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(expected.size.x, expected.size.y, expected.size.z) * .8f;
            camera.transform.position = expected.center + new Vector3(2, 1, 3).normalized * 8;
            camera.transform.LookAt(expected.center); camera.nearClipPlane = .01f; camera.farClipPlane = 1000;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply();
            var pixels = texture.GetPixels32(); report.minX = report.minY = 512; report.maxX = report.maxY = -1;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].r > 16 || pixels[i].g > 16 || pixels[i].b > 16)
                {
                    int x = i % 512, y = i / 512; report.drawnPixels++;
                    report.minX = Math.Min(report.minX, x); report.maxX = Math.Max(report.maxX, x);
                    report.minY = Math.Min(report.minY, y); report.maxY = Math.Max(report.maxY, y);
                }
            report.touchesBorder = report.minX == 0 || report.minY == 0 || report.maxX == 511 || report.maxY == 511;
            report.image = Path.Combine(RoweAnimationWorkspace.Workspace, "Logs", "RiderModelBake-" + report.name + ".png");
            File.WriteAllBytes(report.image, texture.EncodeToPNG());
        }
        finally
        {
            skin.sharedMaterials = oldMaterials; skin.gameObject.layer = oldLayer;
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(material);
        }
    }
    public static void Run()
    {
        var report = new Report { quality = QualitySettings.skinWeights.ToString() };
        GameObject source = null;
        RowePreviewModelState state = null;
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run only in the isolated Validation-* project.");
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider = RowePairedWorkbench.FindRig("Human Temp");
            source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reference/ROWEBODYV2.prefab"));
            source.transform.position = new Vector3(15, 8, -13); source.transform.rotation = Quaternion.Euler(0, 63, 0);
            var sourceSkin = source.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var cases = new List<CaseRow> { Inspect("SourcePlaced", sourceSkin, source.transform, false) };
            state = RoweRiderModelPreview.Connect(rider, source);
            var skin = state.previewSkins.Single();
            cases.Add(Inspect("PreviewScale100", skin, rider.transform, true));
            var position = skin.transform.localPosition; var rotation = skin.transform.localRotation; var scale = skin.transform.localScale;
            skin.transform.localScale = Vector3.one;
            cases.Add(Inspect("PreviewScale1", skin, rider.transform, true));
            skin.transform.localPosition = Vector3.zero; skin.transform.localRotation = Quaternion.identity;
            cases.Add(Inspect("PreviewIdentity", skin, rider.transform, true));
            skin.transform.localPosition = position; skin.transform.localRotation = rotation; skin.transform.localScale = scale;
            report.cases = cases.ToArray(); report.passed = true;
        }
        catch (Exception ex) { report.error = ex.ToString(); Debug.LogException(ex); }
        finally
        {
            if (state) RoweRiderModelPreview.Restore(state.rider);
            if (source) UnityEngine.Object.DestroyImmediate(source);
        }
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "studio-rider-model-bake-probe.json"), JsonUtility.ToJson(report, true));
        Debug.Log("ROWE_RIDER_MODEL_BAKE_PROBE " + JsonUtility.ToJson(report));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
