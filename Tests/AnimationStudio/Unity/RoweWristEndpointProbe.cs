using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only pose diagnostics, restricted to the isolated authoring test project.
public static class RoweWristEndpointProbe
{
    [Serializable] public class Measurement
    {
        public string pose, operation;
        public float leftWrist, rightWrist, leftPalm, rightPalm, feet, hips;
        public float leftAngleBefore, leftAngleAfter, rightAngleBefore, rightAngleAfter;
    }
    [Serializable] public class Report { public List<Measurement> measurements = new List<Measurement>(); public string error; }
    static Vector3 Palm(RoweIKSession s, int hand)
    {
        var finger = s.rider.GetBoneTransform(hand == 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        return finger ? Vector3.Lerp(s.bones[hand].position, finger.position, .5f) : s.bones[hand].position;
    }
    static float Angle(RoweIKSession s, Quaternion[] reference, int hand)
        => Quaternion.Angle(Quaternion.Inverse(s.bones[5 + hand].rotation) * s.bones[hand].rotation, reference[hand]);
    public static void Run()
    {
        var report = new Report();
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Validation project in batch mode only.");
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider = RowePairedWorkbench.FindRig("Human Temp"); var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            var set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>("Assets/CustomClips/HeldTricks/table_tweakn/table_tweakn.asset");
            if (!set || !RoweWristRelaxation.HasReference(set)) throw new InvalidOperationException("Missing table fixture.");
            foreach (bool tweak in new[] { false, true })
            foreach (string op in new[] { "solve-no-edit", "relax-50", "relax-100", "grip-50", "grip-100", "grip-single-50", "grip-single-100", "rotate-only-50", "rotate-only-100" })
            using (var s = new RoweIKSession(rider, bike))
            {
                s.Sample(tweak ? set.tweakPose : set.poses, tweak ? set.bikeTweakPose : set.bikePoses, 1, 1);
                var points = s.bones.Select(b => b.position).ToArray();
                var palms = new[] { Palm(s, 0), Palm(s, 1) };
                var m = new Measurement { pose = tweak ? "tweak" : "hold", operation = op,
                    leftAngleBefore = Angle(s, set.ridingWristRotations, 0), rightAngleBefore = Angle(s, set.ridingWristRotations, 1) };
                float strength = op.EndsWith("100", StringComparison.Ordinal) ? 1 : .5f;
                if (op == "solve-no-edit") s.Solve();
                else if (op.StartsWith("relax-", StringComparison.Ordinal)) s.RelaxWrists(set.ridingWristRotations, strength);
                else if (op.StartsWith("grip-single-", StringComparison.Ordinal))
                {
                    var offsets = new[] { Quaternion.Inverse(s.bones[0].rotation) * (palms[0] - points[0]), Quaternion.Inverse(s.bones[1].rotation) * (palms[1] - points[1]) };
                    for (int hand = 0; hand < 2; hand++)
                    {
                        s.rotations[hand] = Quaternion.Slerp(s.bones[hand].rotation, s.bones[5 + hand].rotation * set.ridingWristRotations[hand], strength);
                        s.positions[hand] = palms[hand] - s.rotations[hand] * offsets[hand];
                    }
                    s.Solve();
                    for (int pass = 0; pass < 4; pass++)
                    {
                        for (int hand = 0; hand < 2; hand++) s.positions[hand] += palms[hand] - Palm(s, hand);
                        s.Solve();
                    }
                }
                else if (op.StartsWith("grip-", StringComparison.Ordinal)) s.RelaxWrists(set.ridingWristRotations, strength, -1, true, true);
                else
                {
                    var starting = new[] { s.bones[0].rotation, s.bones[1].rotation };
                    var human = (HumanPoseHandler)typeof(RoweIKSession).GetField("human", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int hand = 0; hand < 2; hand++)
                            s.bones[hand].rotation = Quaternion.Slerp(starting[hand], s.bones[5 + hand].rotation * set.ridingWristRotations[hand], strength);
                        var p = new HumanPose(); human.GetHumanPose(ref p); human.SetHumanPose(ref p);
                    }
                }
                m.leftWrist = Vector3.Distance(points[0], s.bones[0].position); m.rightWrist = Vector3.Distance(points[1], s.bones[1].position);
                m.leftPalm = Vector3.Distance(palms[0], Palm(s, 0)); m.rightPalm = Vector3.Distance(palms[1], Palm(s, 1));
                m.feet = Mathf.Max(Vector3.Distance(points[2], s.bones[2].position), Vector3.Distance(points[3], s.bones[3].position));
                m.hips = Vector3.Distance(points[4], s.bones[4].position);
                m.leftAngleAfter = Angle(s, set.ridingWristRotations, 0); m.rightAngleAfter = Angle(s, set.ridingWristRotations, 1);
                report.measurements.Add(m);
            }
        }
        catch (Exception ex) { report.error = ex.ToString(); Debug.LogException(ex); }
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "studio-wrist-endpoint-probe.json"), JsonUtility.ToJson(report, true));
        Debug.Log("ROWE_WRIST_ENDPOINT_PROBE " + JsonUtility.ToJson(report));
        EditorApplication.Exit(report.error == null ? 0 : 1);
    }
}
