using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// Read-only asset probe. Never saves or opens the creator's scene; never changes
// the source mesh/importer, target skeleton, Avatar, materials or bind poses.
public static class RoweRiderModelProbe
{
    const string ModelPath = "Assets/Reference/ROWEBODYV2.fbx";
    const string VariantPath = "Assets/Reference/ROWEBODYV2.prefab";
    const string BaseBodyPath = "Assets/Reference/RecoveredBody.prefab";
    [Serializable] public class BoneRow
    {
        public string name, sourcePath, targetPath;
        public int index, weightedVertices;
        public float weightSum, sourceCurrentToBindPosition, sourceCurrentToBindAngle,
            alignedBindPositionError, alignedBindAngleError, targetCurrentToBindPosition,
            targetCurrentToBindAngle;
        public bool mapped, canonicalBindFromReference;
        public Matrix4x4 sourceBindPose, sourceBoneInModel, sourceBindBoneInModel,
            canonicalBindBoneInRider, targetBoneInRider, remappedRestMeshMatrix;
    }
    [Serializable] public class SkinRow
    {
        public string name, path, rootBone, targetRootBone;
        public bool readable, allWeightedMapped, allWeightedHaveCanonicalBind, commonRestMatrix;
        public int vertices, submeshes, bones, bindPoses, weightedBones, unmatchedWeightedBones;
        public string[] missingBones, ambiguousBones, materials;
        public Matrix4x4 meshInSourceRoot, hipsAlignment, expectedMeshInRider;
        public Vector3 sourceMeshLocalSize, sourceRestRootSize, alignedRestRootSize,
            directCanonicalRestSize, directCurrentTargetSize;
        public float maxSourceBindPositionError, maxSourceBindAngleError,
            maxAlignedBindPositionError, maxAlignedBindAngleError,
            maxCommonMatrixPositionError, maxCommonMatrixAngleError,
            vertexMeanAlignedError, vertexRmsAlignedError, vertexMaxAlignedError;
        public BoneRow[] boneRows;
    }
    [Serializable] public class Report
    {
        public bool passed, inputFilesUnchanged;
        public string error, unityVersion, importerType, sourceRootName, canonicalRiderName;
        public Vector3 sourceRootPosition, sourceRootScale, prefabPlacement;
        public int sourceTransforms, targetTransforms, targetReferenceBindBones;
        public string[] dependencies;
        public SkinRow[] skins;
    }
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    static Vector3 Position(Matrix4x4 m) => m.MultiplyPoint3x4(Vector3.zero);
    static float Angle(Matrix4x4 a, Matrix4x4 b) => Quaternion.Angle(a.rotation, b.rotation);
    static void Accumulate(BoneWeight weight, Action<int, float> use)
    {
        if (weight.weight0 > 0) use(weight.boneIndex0, weight.weight0);
        if (weight.weight1 > 0) use(weight.boneIndex1, weight.weight1);
        if (weight.weight2 > 0) use(weight.boneIndex2, weight.weight2);
        if (weight.weight3 > 0) use(weight.boneIndex3, weight.weight3);
    }
    static Bounds BoundsOf(Vector3[] points)
    {
        var result = new Bounds(points[0], Vector3.zero);
        foreach (var point in points.Skip(1)) result.Encapsulate(point);
        return result;
    }

    public static void Run()
    {
        var r = new Report { unityVersion = Application.unityVersion };
        var inputs = new[] { ModelPath, ModelPath + ".meta", VariantPath, VariantPath + ".meta",
            BaseBodyPath, RoweAnimationWorkspace.PrefabPath, RowePairedWorkbench.ScenePath };
        Dictionary<string, string> hashes = null;
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run only in the isolated Validation-* project in batch mode.");
            hashes = inputs.ToDictionary(p => p, Hash);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(RoweAnimationWorkspace.PrefabPath);
            var baseBody = AssetDatabase.LoadAssetAtPath<GameObject>(BaseBodyPath);
            if (!model || !variant || !canonical || !baseBody) throw new InvalidOperationException("A staged model or canonical reference prefab did not import.");
            var target = canonical.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Human Temp");
            var sourceBones = model.GetComponentsInChildren<Transform>(true);
            var targetBones = target.GetComponentsInChildren<Transform>(true);
            var targetNames = targetBones.GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.ToArray());
            r.sourceRootName = model.name; r.canonicalRiderName = target.name;
            r.sourceRootPosition = model.transform.localPosition; r.sourceRootScale = model.transform.localScale;
            r.prefabPlacement = variant.transform.localPosition;
            r.sourceTransforms = sourceBones.Length; r.targetTransforms = targetBones.Length;
            r.importerType = ((ModelImporter)AssetImporter.GetAtPath(ModelPath)).animationType.ToString();
            r.dependencies = AssetDatabase.GetDependencies(new[] { ModelPath, VariantPath }, true);

            // A prefab's current Transform pose need not be its skin bind pose.
            // Recover canonical bind frames from the already working base mesh.
            var canonicalBind = new Dictionary<string, Matrix4x4>();
            foreach (var skin in baseBody.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.sharedMesh) continue;
                var bind = skin.sharedMesh.bindposes;
                var meshInRoot = baseBody.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                for (int i = 0; i < Math.Min(bind.Length, skin.bones.Length); i++)
                    if (skin.bones[i] && !canonicalBind.ContainsKey(skin.bones[i].name))
                        canonicalBind.Add(skin.bones[i].name, meshInRoot * bind[i].inverse);
            }
            r.targetReferenceBindBones = canonicalBind.Count;
            var reports = new List<SkinRow>();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.sharedMesh) continue;
                Mesh mesh = skin.sharedMesh;
                var vertices = mesh.vertices; var weights = mesh.boneWeights; var bind = mesh.bindposes;
                if (vertices.Length == 0 || weights.Length != vertices.Length || bind.Length != skin.bones.Length)
                    throw new InvalidOperationException("Unexpected source skin layout: " + skin.name);
                var row = new SkinRow {
                    name = skin.name, path = AnimationUtility.CalculateTransformPath(skin.transform, model.transform),
                    rootBone = skin.rootBone ? skin.rootBone.name : "<none>", readable = mesh.isReadable,
                    vertices = vertices.Length, submeshes = mesh.subMeshCount, bones = skin.bones.Length, bindPoses = bind.Length,
                    meshInSourceRoot = model.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix,
                    sourceMeshLocalSize = mesh.bounds.size,
                    materials = skin.sharedMaterials.Select(m => m ? AssetDatabase.GetAssetPath(m) + "#" + m.name : "<missing>").ToArray()
                };
                row.targetRootBone = skin.rootBone && targetNames.TryGetValue(skin.rootBone.name, out var roots) && roots.Length == 1
                    ? AnimationUtility.CalculateTransformPath(roots[0], target) : "<missing or ambiguous>";
                var weightSums = new float[bind.Length]; var weightedVertices = new int[bind.Length];
                foreach (var weight in weights) Accumulate(weight, (index, value) => { weightSums[index] += value; weightedVertices[index]++; });
                var rows = new BoneRow[bind.Length];
                var missing = new List<string>(); var ambiguous = new List<string>();
                for (int i = 0; i < rows.Length; i++)
                {
                    var bone = skin.bones[i];
                    var entry = new BoneRow {
                        index = i, name = bone ? bone.name : "<null>", weightedVertices = weightedVertices[i], weightSum = weightSums[i],
                        sourcePath = bone ? AnimationUtility.CalculateTransformPath(bone, model.transform) : "<null>",
                        sourceBindPose = bind[i], sourceBindBoneInModel = row.meshInSourceRoot * bind[i].inverse,
                        sourceBoneInModel = bone ? model.transform.worldToLocalMatrix * bone.localToWorldMatrix : Matrix4x4.identity
                    };
                    entry.sourceCurrentToBindPosition = Vector3.Distance(Position(entry.sourceBoneInModel), Position(entry.sourceBindBoneInModel));
                    entry.sourceCurrentToBindAngle = Angle(entry.sourceBoneInModel, entry.sourceBindBoneInModel);
                    if (bone && targetNames.TryGetValue(bone.name, out var matches) && matches.Length == 1)
                    {
                        entry.mapped = true;
                        entry.targetPath = AnimationUtility.CalculateTransformPath(matches[0], target);
                        entry.targetBoneInRider = target.worldToLocalMatrix * matches[0].localToWorldMatrix;
                        entry.canonicalBindFromReference = canonicalBind.TryGetValue(bone.name, out var canonicalFrame);
                        entry.canonicalBindBoneInRider = entry.canonicalBindFromReference ? canonicalFrame : entry.targetBoneInRider;
                        entry.targetCurrentToBindPosition = Vector3.Distance(Position(entry.targetBoneInRider), Position(entry.canonicalBindBoneInRider));
                        entry.targetCurrentToBindAngle = Angle(entry.targetBoneInRider, entry.canonicalBindBoneInRider);
                        entry.remappedRestMeshMatrix = entry.canonicalBindBoneInRider * bind[i];
                    }
                    else if (bone && targetNames.ContainsKey(bone.name)) ambiguous.Add(bone.name);
                    else missing.Add(entry.name);
                    rows[i] = entry;
                }
                var hip = rows.FirstOrDefault(b => b.name == "mixamorig:Hips" && b.mapped);
                if (hip == null) throw new InvalidOperationException("Source skin has no mapped hip bind frame.");
                row.hipsAlignment = hip.canonicalBindBoneInRider * hip.sourceBindBoneInModel.inverse;
                row.expectedMeshInRider = row.hipsAlignment * row.meshInSourceRoot;
                foreach (var entry in rows.Where(b => b.mapped && b.weightSum > 0))
                {
                    var aligned = row.hipsAlignment * entry.sourceBindBoneInModel;
                    entry.alignedBindPositionError = Vector3.Distance(Position(aligned), Position(entry.canonicalBindBoneInRider));
                    entry.alignedBindAngleError = Angle(aligned, entry.canonicalBindBoneInRider);
                    row.maxAlignedBindPositionError = Mathf.Max(row.maxAlignedBindPositionError, entry.alignedBindPositionError);
                    row.maxAlignedBindAngleError = Mathf.Max(row.maxAlignedBindAngleError, entry.alignedBindAngleError);
                    row.maxSourceBindPositionError = Mathf.Max(row.maxSourceBindPositionError, entry.sourceCurrentToBindPosition);
                    row.maxSourceBindAngleError = Mathf.Max(row.maxSourceBindAngleError, entry.sourceCurrentToBindAngle);
                    row.maxCommonMatrixPositionError = Mathf.Max(row.maxCommonMatrixPositionError, Vector3.Distance(Position(entry.remappedRestMeshMatrix), Position(row.expectedMeshInRider)));
                    row.maxCommonMatrixAngleError = Mathf.Max(row.maxCommonMatrixAngleError, Angle(entry.remappedRestMeshMatrix, row.expectedMeshInRider));
                }
                row.missingBones = missing.Distinct().ToArray(); row.ambiguousBones = ambiguous.Distinct().ToArray();
                row.weightedBones = rows.Count(b => b.weightSum > 0);
                row.unmatchedWeightedBones = rows.Count(b => b.weightSum > 0 && !b.mapped);
                row.allWeightedMapped = row.unmatchedWeightedBones == 0;
                row.allWeightedHaveCanonicalBind = rows.Where(b => b.weightSum > 0).All(b => b.canonicalBindFromReference);
                row.commonRestMatrix = row.allWeightedMapped && row.maxCommonMatrixPositionError < .001f && row.maxCommonMatrixAngleError < .1f;
                row.boneRows = rows;
                var sourcePoints = new Vector3[vertices.Length]; var alignedPoints = new Vector3[vertices.Length];
                var directRest = new Vector3[vertices.Length]; var directCurrent = new Vector3[vertices.Length];
                double errorSum = 0, errorSquared = 0;
                for (int v = 0; v < vertices.Length; v++)
                {
                    sourcePoints[v] = row.meshInSourceRoot.MultiplyPoint3x4(vertices[v]);
                    alignedPoints[v] = row.hipsAlignment.MultiplyPoint3x4(sourcePoints[v]);
                    int vertex = v;
                    Accumulate(weights[v], (i, w) => {
                        directRest[vertex] += (rows[i].canonicalBindBoneInRider * bind[i]).MultiplyPoint3x4(vertices[vertex]) * w;
                        directCurrent[vertex] += (rows[i].targetBoneInRider * bind[i]).MultiplyPoint3x4(vertices[vertex]) * w;
                    });
                    float error = Vector3.Distance(directRest[v], alignedPoints[v]);
                    errorSum += error; errorSquared += error * error;
                    row.vertexMaxAlignedError = Mathf.Max(row.vertexMaxAlignedError, error);
                }
                row.vertexMeanAlignedError = (float)(errorSum / vertices.Length);
                row.vertexRmsAlignedError = (float)Math.Sqrt(errorSquared / vertices.Length);
                row.sourceRestRootSize = BoundsOf(sourcePoints).size;
                row.alignedRestRootSize = BoundsOf(alignedPoints).size;
                row.directCanonicalRestSize = BoundsOf(directRest).size;
                row.directCurrentTargetSize = BoundsOf(directCurrent).size;
                reports.Add(row);
            }
            r.skins = reports.ToArray();
            r.passed = r.skins.Length > 0;
        }
        catch (Exception ex) { r.error = ex.ToString(); Debug.LogException(ex); }
        finally
        {
            r.inputFilesUnchanged = hashes != null && hashes.All(kv => File.Exists(kv.Key) && Hash(kv.Key) == kv.Value);
            r.passed &= r.inputFilesUnchanged;
        }
        string output = Path.Combine(RoweAnimationWorkspace.Workspace, "studio-rider-model-probe.json");
        File.WriteAllText(output, JsonUtility.ToJson(r, true));
        Debug.Log("ROWE_RIDER_MODEL_PROBE passed=" + r.passed + " skins=" + (r.skins?.Length ?? 0) + " report=" + output);
        EditorApplication.Exit(r.passed ? 0 : 1);
    }
}
