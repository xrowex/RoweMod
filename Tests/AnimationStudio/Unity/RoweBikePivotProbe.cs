using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoweBikePivotProbe
{
    [Serializable] sealed class Joint
    {
        public string name, path;
        public Vector3 worldPosition, localPosition, worldEuler, localEuler;
    }

    [Serializable] sealed class Report
    {
        public Joint[] joints;
        public Vector3 frameToBars, barsToFrontWheel, frameVisualCenter, barsVisualCenter;
        public float frameToBarsDistance, forkRightAlignment, forkUpAlignment, forkForwardAlignment;
        public int frameVisuals, barsVisuals;
    }

    public static void Run()
    {
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run only in the isolated Validation-* project.");
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            var joints = RoweBikeAnimation.FindJoints(bike);
            Joint Describe(Transform t, int i) => new Joint {
                name = RoweBikeAnimation.Labels[i], path = RoweBikeAnimation.Paths[i],
                worldPosition = t.position, localPosition = t.localPosition,
                worldEuler = t.rotation.eulerAngles, localEuler = t.localEulerAngles
            };
            Vector3 Center(Renderer[] renderers) => renderers.Aggregate(renderers[0].bounds, (bounds, renderer) => { bounds.Encapsulate(renderer.bounds); return bounds; }).center;
            var frameVisuals = joints[1].GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(joints[3]) && !r.transform.IsChildOf(joints[7])).ToArray();
            var barsVisuals = joints[2].GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(joints[6])).ToArray();
            var fork = (joints[6].position - joints[2].position).normalized;
            var report = new Report {
                joints = joints.Select(Describe).ToArray(), frameToBars = joints[2].position - joints[1].position,
                barsToFrontWheel = joints[6].position - joints[2].position,
                frameToBarsDistance = Vector3.Distance(joints[1].position, joints[2].position),
                forkRightAlignment = Mathf.Abs(Vector3.Dot(fork, joints[2].right)),
                forkUpAlignment = Mathf.Abs(Vector3.Dot(fork, joints[2].up)),
                forkForwardAlignment = Mathf.Abs(Vector3.Dot(fork, joints[2].forward)),
                frameVisualCenter = Center(frameVisuals), barsVisualCenter = Center(barsVisuals),
                frameVisuals = frameVisuals.Length, barsVisuals = barsVisuals.Length
            };
            string output = Path.Combine(RoweAnimationWorkspace.Workspace, "studio-bike-pivot-probe.json");
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("ROWE_BIKE_PIVOT_PROBE " + JsonUtility.ToJson(report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
