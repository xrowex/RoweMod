using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoweMod.AnimationAuthoring;

// Run only in an isolated Validation-* project. The game/SDK frame contract
// supplies Forks_Anchor and Headset_Anchor; together they define the physical
// head-tube axis. The paired workbench strips those content anchors, so map
// their saved Joints-root coordinates onto the live Joints transform. This is
// a neutral third reference: rotating Frame or Bars cannot move its own pivot.
public static class RoweFrameForkPivotValidation
{
    [Serializable]
    public sealed class Report
    {
        public bool passed, sdkAxisRepresented, nonVacuousFixture,
            frameVisualOrbits, frameJointOrbits, rearAndCranksFollow,
            forkBranchFixed, frontAssemblyFixed,
            barsVisualOrbits, barsJointOrbits, barsOriginNotPivot, frontFollowsBars, frameBranchFixedForBars,
            enabledContactsClassified, enabledHandsStayOnBars, enabledFeetFollowFrame,
            enabledBonesReachContacts, disabledContactsClassified, disabledRiderStays,
            barsContactsClassified, barsHandsFollow, barsFeetStay, barsBonesReachContacts,
            plainTranslationLiteral, physicalFullTurnCloses,
            savedPose, undoRestores, redoRestores, reopenedPose,
            sourcePreserved, referencePreserved, sceneRestored;
        public int frameRenderers, barsRenderers, rearRenderers, crankRenderers, frontRenderers;
        public float frameAxisRadius, physicalAxisLength, barsAxisOffset, forkAxisAlignment, frameAxisAlignment,
            frameVisualMotion, frameVisualOrbitError, frameJointPositionError, frameJointRotationError,
            rearOrbitError, crankOrbitError, barsFixedError, frontFixedError, frontWouldOrbitDistance,
            barsVisualMotion, barsVisualOrbitError, barsJointMotion, barsJointPositionError, barsJointRotationError,
            frontOrbitError, frameFixedForBarsError, rearFixedForBarsError, crankFixedForBarsError,
            enabledContactPositionError, enabledContactRotationError, enabledBoneReachError,
            barsContactPositionError, barsContactRotationError, barsBoneReachError,
            disabledTargetError, disabledBoneError, savedBikeError, savedRiderError,
            frameTranslationError, barsTranslationError, fullTurnQuarterMotion, fullTurnBikeError, fullTurnVisualError,
            undoBikeError, undoRiderError, redoBikeError, redoRiderError,
            reopenedBikeError, reopenedRiderError;
        public string invariant, recipePath, error;
    }

    sealed class JointPose
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public JointPose(Transform transform) { position = transform.position; rotation = transform.rotation; }
    }

    sealed class VisualPoint
    {
        public readonly Transform transform;
        public readonly Vector3 local;
        public VisualPoint(Transform transform, Vector3 local) { this.transform = transform; this.local = local; }
        public Vector3 World => transform.TransformPoint(local);
    }

    sealed class VisualCloud
    {
        readonly VisualPoint[] points;
        public readonly int rendererCount;
        public int Count => points.Length;

        public VisualCloud(Transform joint, bool directOnly)
        {
            var renderers = joint.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.enabled && (!directOnly || renderer.transform.parent == joint))
                .OrderBy(renderer => AnimationUtility.CalculateTransformPath(renderer.transform, joint.root), StringComparer.Ordinal)
                .ToArray();
            rendererCount = renderers.Length;
            var list = new List<VisualPoint>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                list.Add(new VisualPoint(renderer.transform, bounds.center));
                Vector3 e = bounds.extents;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            list.Add(new VisualPoint(renderer.transform, bounds.center + Vector3.Scale(e, new Vector3(x, y, z))));
            }
            points = list.ToArray();
        }

        public Vector3[] Capture() => points.Select(point => point.World).ToArray();
    }

    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string SourcePath = "Assets/CustomClips/RoweMod_Custom_My Trick 3.anim";
    const string StaticReferencePath = "Assets/Reference/BikeStaticReference.prefab";
    const string FrameReferencePath = "Root/Chassis Visuals/Parts/Frame/BMX_Frame_Vanilla_Standard";
    const string ReferenceJointsPath = "Root/Bike Skeleton/Joints";
    const string Invariant = "Load Assets/Reference/BikeStaticReference.prefab, read Root/Chassis Visuals/Parts/Frame/BMX_Frame_Vanilla_Standard/Forks_Anchor and Headset_Anchor, convert both to Root/Bike Skeleton/Joints local coordinates, then transform them through the live Joints transform. That fixed Forks_Anchor-to-Headset_Anchor line is the physical head-tube axis. A Frame rotation maps frame/rear/crank points x to P + delta * (x - P) while the Bars/front branch stays fixed; a Bars rotation applies that same mapping to Bars/forks/front while the Frame/rear/crank branch stays fixed. Bars_Joint is deliberately not the pivot.";

    static object Call(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, Private);
        if (method == null) throw new MissingMethodException(target.GetType().FullName, name);
        return method.Invoke(target, args);
    }

    static T Get<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, Private);
        if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
        return (T)field.GetValue(target);
    }

    static RoweIKSession Session(RoweIKPoseEditor editor) => Get<RoweIKSession>(editor, "session");

    static byte[] Bytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
    static bool SameBytes(byte[] before, string path)
        => before != null && File.Exists(path) && before.SequenceEqual(File.ReadAllBytes(path));

    static float PositionError(Vector3[] actual, Vector3[] expected)
    {
        if (actual == null || expected == null || actual.Length != expected.Length || actual.Length == 0) return float.PositiveInfinity;
        return Enumerable.Range(0, actual.Length).Max(i => Vector3.Distance(actual[i], expected[i]));
    }

    static float PositionError(Vector3[] actual, Vector3[] expected, IEnumerable<int> indices)
        => indices.Max(i => Vector3.Distance(actual[i], expected[i]));

    static float RotationError(Quaternion[] actual, Quaternion[] expected, IEnumerable<int> indices)
        => indices.Max(i => Quaternion.Angle(actual[i], expected[i]));

    static Vector3 Orbit(Vector3 point, Vector3 pivot, Quaternion delta) => pivot + delta * (point - pivot);
    static Vector3[] Orbit(Vector3[] points, Vector3 pivot, Quaternion delta)
        => points.Select(point => Orbit(point, pivot, delta)).ToArray();
    static Vector3[] Translate(Vector3[] points, Vector3 translation)
        => points.Select(point => point + translation).ToArray();

    static float Motion(Vector3[] before, Vector3[] after)
        => before.Length == after.Length && before.Length > 0
            ? Enumerable.Range(0, before.Length).Max(i => Vector3.Distance(before[i], after[i]))
            : float.PositiveInfinity;

    static float RadialDistance(Vector3 point, Vector3 pivot, Vector3 axis)
        => Vector3.ProjectOnPlane(point - pivot, axis.normalized).magnitude;

    static float DistanceToAxis(Vector3 point, Vector3 pivot, Vector3 axis) => RadialDistance(point, pivot, axis);

    static void PhysicalAxis(Transform liveJoints, out Vector3 forks, out Vector3 headset)
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(StaticReferencePath);
        if (!reference) throw new FileNotFoundException("Missing the read-only static bike reference", StaticReferencePath);
        Transform referenceJoints = reference.transform.Find(ReferenceJointsPath);
        Transform frame = reference.transform.Find(FrameReferencePath);
        Transform referenceForks = frame ? frame.Find("Forks_Anchor") : null;
        Transform referenceHeadset = frame ? frame.Find("Headset_Anchor") : null;
        if (!referenceJoints || !referenceForks || !referenceHeadset)
            throw new InvalidOperationException("BikeStaticReference no longer matches the canonical bike/frame anchor paths.");
        Vector3 forksInJoints = referenceJoints.InverseTransformPoint(referenceForks.position);
        Vector3 headsetInJoints = referenceJoints.InverseTransformPoint(referenceHeadset.position);
        forks = liveJoints.TransformPoint(forksInJoints);
        headset = liveJoints.TransformPoint(headsetInJoints);
    }

    static Quaternion MoveSteeringBranch(RoweIKPoseEditor editor, int index, float degrees, Vector3 axis)
    {
        var joints = Session(editor).bikeJoints;
        Transform joint = joints[index];
        Quaternion delta = Quaternion.AngleAxis(degrees, axis);
        // This is the exact rotation-handle input: the handle reports a changed
        // rotation but the old joint origin. Production must orbit that origin
        // around the fixed head-tube axis before applying the pose.
        Call(editor, "MoveBikeControl", index, joint.position, delta * joint.rotation);
        return delta;
    }

    static Quaternion MoveFrame(RoweIKPoseEditor editor, float degrees, Vector3 pivot, Vector3 axis)
        => MoveSteeringBranch(editor, RoweBikeAnimation.FrameIndex, degrees, axis);

    static Quaternion MoveBars(RoweIKPoseEditor editor, float degrees, Vector3 pivot, Vector3 axis)
        => MoveSteeringBranch(editor, RoweBikeAnimation.BarsIndex, degrees, axis);

    static float JointPositionError(Transform[] joints, JointPose[] before, int[] indices, Vector3 pivot, Quaternion delta)
        => indices.Max(i => Vector3.Distance(joints[i].position, Orbit(before[i].position, pivot, delta)));

    static float JointRotationError(Transform[] joints, JointPose[] before, int[] indices, Quaternion delta)
        => indices.Max(i => Quaternion.Angle(joints[i].rotation, delta * before[i].rotation));

    static float FixedJointError(Transform[] joints, JointPose[] before, int[] indices)
        => indices.Max(i => Mathf.Max(Vector3.Distance(joints[i].position, before[i].position), Quaternion.Angle(joints[i].rotation, before[i].rotation) / 1000f));

    static float BikeError(Transform[] joints, JointPose[] expected)
        => Enumerable.Range(0, joints.Length).Max(i => Mathf.Max(
            Vector3.Distance(joints[i].position, expected[i].position),
            Quaternion.Angle(joints[i].rotation, expected[i].rotation) / 1000f));

    static float RiderError(RoweIKSession session, Vector3[] expectedPositions, Quaternion[] expectedRotations)
        => Enumerable.Range(0, 5).Max(i => Mathf.Max(
            Vector3.Distance(session.bones[i].position, expectedPositions[i]),
            Quaternion.Angle(session.bones[i].rotation, expectedRotations[i]) / 1000f));

    static JointPose[] CaptureJoints(Transform[] joints) => joints.Select(joint => new JointPose(joint)).ToArray();
    static Vector3[] CaptureBones(RoweIKSession session) => session.bones.Take(5).Select(bone => bone.position).ToArray();
    static Quaternion[] CaptureBoneRotations(RoweIKSession session) => session.bones.Take(5).Select(bone => bone.rotation).ToArray();

    public static void Run()
    {
        var report = new Report { invariant = Invariant };
        RoweIKPoseEditor editor = null;
        RowePairedWorkbench.Pose original = null;
        byte[] sourceBefore = null, sourceMetaBefore = null, referenceBefore = null, referenceMetaBefore = null, sceneBefore = null;
        try
        {
            string projectName = Path.GetFileName(Directory.GetParent(Application.dataPath).FullName);
            if (!Application.isBatchMode || !projectName.StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run only in a separate Validation-* Unity project in batch mode.");

            sceneBefore = Bytes(RowePairedWorkbench.ScenePath);
            sourceBefore = Bytes(SourcePath);
            sourceMetaBefore = Bytes(SourcePath + ".meta");
            referenceBefore = Bytes(StaticReferencePath);
            referenceMetaBefore = Bytes(StaticReferencePath + ".meta");
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider = RowePairedWorkbench.FindRig("Human Temp");
            var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            if (!rider || !bike) throw new InvalidOperationException("The paired rider/bike workbench fixture is missing.");
            original = new RowePairedWorkbench.Pose(rider.transform, bike.transform);

            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourcePath);
            if (!source) throw new FileNotFoundException("Missing read-only rider source fixture", SourcePath);
            string recipeName = "Frame Fork Pivot Validation " + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var set = RoweHeldTrickBuilder.Create(recipeName, source, 0, 0, rider, bike);
            report.recipePath = AssetDatabase.GetAssetPath(set);
            RoweTrickStyles.Choose(set, RoweTrickStyle.HoldTweak);
            RoweBikeAnimation.Enable(set, rider, bike);
            set.autoReleaseContacts = false;
            set.handsFollowBars = true;
            set.feetFollowPedals = true;
            set.keepHipsInPlace = true;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);

            editor = RoweIKPoseEditor.OpenRecipe(set);
            Call(editor, "BeginStudioPose", 1);
            var session = Session(editor);
            var joints = session.bikeJoints;
            if (joints == null || joints.Length != RoweBikeAnimation.Paths.Length) throw new InvalidOperationException("Bike animation joints are incomplete.");

            var frameVisual = new VisualCloud(joints[1], true);
            // Include the complete steering child branch here so nested forks
            // are measured as well as the direct Bars mesh. FrontWheel is also
            // measured independently below to prevent hierarchy-only passes.
            var barsVisual = new VisualCloud(joints[2], false);
            var rearVisual = new VisualCloud(joints[7], false);
            var crankVisual = new VisualCloud(joints[3], false);
            var frontVisual = new VisualCloud(joints[6], false);
            report.frameRenderers = frameVisual.rendererCount;
            report.barsRenderers = barsVisual.rendererCount;
            report.rearRenderers = rearVisual.rendererCount;
            report.crankRenderers = crankVisual.rendererCount;
            report.frontRenderers = frontVisual.rendererCount;
            if (new[] { frameVisual.Count, barsVisual.Count, rearVisual.Count, crankVisual.Count, frontVisual.Count }.Any(count => count == 0))
                throw new InvalidOperationException("Every physical bike branch must contain visible mesh geometry in the paired workbench.");

            PhysicalAxis(joints[0], out Vector3 physicalForks, out Vector3 physicalHeadset);
            Vector3 neutralAxis = (physicalHeadset - physicalForks).normalized;
            Vector3 forkDirection = (joints[6].position - joints[2].position).normalized;
            report.physicalAxisLength = Vector3.Distance(physicalForks, physicalHeadset);
            report.barsAxisOffset = DistanceToAxis(joints[2].position, physicalForks, neutralAxis);
            report.forkAxisAlignment = Mathf.Abs(Vector3.Dot(neutralAxis, forkDirection));
            report.frameAxisAlignment = Mathf.Abs(Vector3.Dot(neutralAxis, joints[1].up.normalized));
            report.frameAxisRadius = RadialDistance(joints[1].position, physicalForks, neutralAxis);
            report.sdkAxisRepresented = report.physicalAxisLength > .05f && report.physicalAxisLength < .25f
                && report.barsAxisOffset > .02f && report.barsAxisOffset < .05f
                && report.forkAxisAlignment > .99f && report.frameAxisAlignment > .999f;
            // The canonical fixture measures 51.0 mm from Frame_Joint to the
            // SDK head-tube axis. Keep a narrow-but-valid 70 mm ceiling.
            report.nonVacuousFixture = report.frameAxisRadius > .005f && report.frameAxisRadius < .07f;

            // Strong visual orbit: no rider carry, so geometry and hierarchy are
            // measured independently of IK reach.
            set.handsFollowBars = false;
            set.feetFollowPedals = false;
            Call(editor, "Sample");
            JointPose[] visualJointsBefore = CaptureJoints(joints);
            Vector3[] frameBefore = frameVisual.Capture(), barsBefore = barsVisual.Capture(),
                rearBefore = rearVisual.Capture(), crankBefore = crankVisual.Capture(), frontBefore = frontVisual.Capture();
            PhysicalAxis(joints[0], out Vector3 visualPivot, out Vector3 visualHeadset);
            Vector3 visualAxis = (visualHeadset - visualPivot).normalized;
            Quaternion visualDelta = MoveFrame(editor, 67f, visualPivot, visualAxis);
            Vector3[] frameAfter = frameVisual.Capture(), barsAfter = barsVisual.Capture(),
                rearAfter = rearVisual.Capture(), crankAfter = crankVisual.Capture(), frontAfter = frontVisual.Capture();
            report.frameVisualMotion = Motion(frameBefore, frameAfter);
            report.frameVisualOrbitError = PositionError(frameAfter, Orbit(frameBefore, visualPivot, visualDelta));
            report.frameJointPositionError = JointPositionError(joints, visualJointsBefore, new[] { 1 }, visualPivot, visualDelta);
            report.frameJointRotationError = JointRotationError(joints, visualJointsBefore, new[] { 1 }, visualDelta);
            report.rearOrbitError = Mathf.Max(PositionError(rearAfter, Orbit(rearBefore, visualPivot, visualDelta)),
                JointPositionError(joints, visualJointsBefore, new[] { 7 }, visualPivot, visualDelta));
            report.crankOrbitError = Mathf.Max(PositionError(crankAfter, Orbit(crankBefore, visualPivot, visualDelta)),
                JointPositionError(joints, visualJointsBefore, new[] { 3, 4, 5 }, visualPivot, visualDelta));
            report.barsFixedError = Mathf.Max(PositionError(barsAfter, barsBefore), FixedJointError(joints, visualJointsBefore, new[] { 2 }));
            report.frontFixedError = Mathf.Max(PositionError(frontAfter, frontBefore), FixedJointError(joints, visualJointsBefore, new[] { 6 }));
            report.frontWouldOrbitDistance = Motion(frontBefore, Orbit(frontBefore, visualPivot, visualDelta));
            report.frameVisualOrbits = report.frameVisualMotion > .02f && report.frameVisualOrbitError < .0002f;
            report.frameJointOrbits = report.frameJointPositionError < .0001f && report.frameJointRotationError < .02f;
            report.rearAndCranksFollow = report.rearOrbitError < .0002f && report.crankOrbitError < .0002f;
            report.forkBranchFixed = report.barsFixedError < .0001f;
            report.frontAssemblyFixed = report.frontFixedError < .0001f && report.frontWouldOrbitDistance > .005f;

            // Mirrored steering-branch orbit. The pivot is re-derived from the
            // neutral SDK anchors after Sample; it is never Bars_Joint. Bars,
            // forks and front wheel must orbit, while the rear branch stays put.
            Call(editor, "Sample");
            set.handsFollowBars = false;
            set.feetFollowPedals = false;
            JointPose[] barsJointsBefore = CaptureJoints(joints);
            frameBefore = frameVisual.Capture(); barsBefore = barsVisual.Capture();
            rearBefore = rearVisual.Capture(); crankBefore = crankVisual.Capture(); frontBefore = frontVisual.Capture();
            PhysicalAxis(joints[0], out Vector3 barsPivot, out Vector3 barsHeadset);
            Vector3 barsAxis = (barsHeadset - barsPivot).normalized;
            float currentBarsAxisOffset = DistanceToAxis(joints[2].position, barsPivot, barsAxis);
            Quaternion barsDelta = MoveBars(editor, 67f, barsPivot, barsAxis);
            frameAfter = frameVisual.Capture(); barsAfter = barsVisual.Capture();
            rearAfter = rearVisual.Capture(); crankAfter = crankVisual.Capture(); frontAfter = frontVisual.Capture();
            report.barsVisualMotion = Motion(barsBefore, barsAfter);
            report.barsVisualOrbitError = PositionError(barsAfter, Orbit(barsBefore, barsPivot, barsDelta));
            report.barsJointMotion = Vector3.Distance(joints[2].position, barsJointsBefore[2].position);
            report.barsJointPositionError = JointPositionError(joints, barsJointsBefore, new[] { 2 }, barsPivot, barsDelta);
            report.barsJointRotationError = JointRotationError(joints, barsJointsBefore, new[] { 2 }, barsDelta);
            report.frontOrbitError = Mathf.Max(
                PositionError(frontAfter, Orbit(frontBefore, barsPivot, barsDelta)),
                Mathf.Max(JointPositionError(joints, barsJointsBefore, new[] { 6 }, barsPivot, barsDelta),
                    JointRotationError(joints, barsJointsBefore, new[] { 6 }, barsDelta) / 1000f));
            report.frameFixedForBarsError = Mathf.Max(PositionError(frameAfter, frameBefore),
                FixedJointError(joints, barsJointsBefore, new[] { 0, 1 }));
            report.rearFixedForBarsError = Mathf.Max(PositionError(rearAfter, rearBefore),
                FixedJointError(joints, barsJointsBefore, new[] { 7 }));
            report.crankFixedForBarsError = Mathf.Max(PositionError(crankAfter, crankBefore),
                FixedJointError(joints, barsJointsBefore, new[] { 3, 4, 5 }));
            report.barsVisualOrbits = report.barsVisualMotion > .02f && report.barsVisualOrbitError < .0002f;
            report.barsJointOrbits = report.barsJointPositionError < .0001f && report.barsJointRotationError < .02f;
            report.barsOriginNotPivot = currentBarsAxisOffset > .02f && report.barsJointMotion > .02f;
            report.frontFollowsBars = report.frontOrbitError < .0002f && Motion(frontBefore, frontAfter) > .02f;
            report.frameBranchFixedForBars = report.frameFixedForBarsError < .0001f
                && report.rearFixedForBarsError < .0001f && report.crankFixedForBarsError < .0001f;

            // Contacts enabled: bars stay fixed, so hands stay fixed; pedals are
            // in the frame branch, so both feet carry through the same delta.
            Call(editor, "Sample");
            set.handsFollowBars = true;
            set.feetFollowPedals = true;
            set.autoReleaseContacts = false;
            Vector3[] enabledTargetsBefore = session.positions.Take(4).ToArray();
            Quaternion[] enabledRotationsBefore = session.rotations.Take(4).ToArray();
            report.enabledContactsClassified = Enumerable.Range(0, 4).All(i => RoweBikeContacts.ShouldFollow(set, session, i));
            PhysicalAxis(joints[0], out Vector3 contactPivot, out Vector3 contactHeadset);
            Vector3 contactAxis = (contactHeadset - contactPivot).normalized;
            Quaternion contactDelta = MoveFrame(editor, 8f, contactPivot, contactAxis);
            Vector3[] expectedEnabledTargets = enabledTargetsBefore.ToArray();
            Quaternion[] expectedEnabledRotations = enabledRotationsBefore.ToArray();
            for (int i = 2; i < 4; i++)
            {
                expectedEnabledTargets[i] = Orbit(enabledTargetsBefore[i], contactPivot, contactDelta);
                expectedEnabledRotations[i] = contactDelta * enabledRotationsBefore[i];
            }
            report.enabledContactPositionError = PositionError(session.positions, expectedEnabledTargets, Enumerable.Range(0, 4));
            report.enabledContactRotationError = RotationError(session.rotations, expectedEnabledRotations, Enumerable.Range(0, 4));
            report.enabledBoneReachError = Enumerable.Range(0, 4).Max(i => Vector3.Distance(session.bones[i].position, session.positions[i]));
            report.enabledHandsStayOnBars = Enumerable.Range(0, 2).All(i => Vector3.Distance(session.positions[i], enabledTargetsBefore[i]) < .0001f
                && Quaternion.Angle(session.rotations[i], enabledRotationsBefore[i]) < .02f);
            report.enabledFeetFollowFrame = Enumerable.Range(2, 2).All(i => Vector3.Distance(session.positions[i], expectedEnabledTargets[i]) < .0001f
                && Quaternion.Angle(session.rotations[i], expectedEnabledRotations[i]) < .02f)
                && Enumerable.Range(2, 2).Any(i => Vector3.Distance(session.positions[i], enabledTargetsBefore[i]) > .02f);
            report.enabledBonesReachContacts = report.enabledBoneReachError < .015f;

            // Mirrored contact ownership: Bars carries only the two hands.
            // Pedal targets stay literal world poses because Frame is fixed.
            Call(editor, "Sample");
            set.handsFollowBars = true;
            set.feetFollowPedals = true;
            set.autoReleaseContacts = false;
            Vector3[] barsTargetsBefore = session.positions.Take(4).ToArray();
            Quaternion[] barsRotationsBefore = session.rotations.Take(4).ToArray();
            report.barsContactsClassified = Enumerable.Range(0, 4).All(i => RoweBikeContacts.ShouldFollow(set, session, i));
            PhysicalAxis(joints[0], out Vector3 barsContactPivot, out Vector3 barsContactHeadset);
            Vector3 barsContactAxis = (barsContactHeadset - barsContactPivot).normalized;
            Quaternion barsContactDelta = MoveBars(editor, 8f, barsContactPivot, barsContactAxis);
            Vector3[] expectedBarsTargets = barsTargetsBefore.ToArray();
            Quaternion[] expectedBarsRotations = barsRotationsBefore.ToArray();
            for (int i = 0; i < 2; i++)
            {
                expectedBarsTargets[i] = Orbit(barsTargetsBefore[i], barsContactPivot, barsContactDelta);
                expectedBarsRotations[i] = barsContactDelta * barsRotationsBefore[i];
            }
            report.barsContactPositionError = PositionError(session.positions, expectedBarsTargets, Enumerable.Range(0, 4));
            report.barsContactRotationError = RotationError(session.rotations, expectedBarsRotations, Enumerable.Range(0, 4));
            report.barsBoneReachError = Enumerable.Range(0, 4).Max(i => Vector3.Distance(session.bones[i].position, session.positions[i]));
            report.barsHandsFollow = Enumerable.Range(0, 2).All(i => Vector3.Distance(session.positions[i], expectedBarsTargets[i]) < .0001f
                && Quaternion.Angle(session.rotations[i], expectedBarsRotations[i]) < .02f)
                && Enumerable.Range(0, 2).Any(i => Vector3.Distance(session.positions[i], barsTargetsBefore[i]) > .01f);
            report.barsFeetStay = Enumerable.Range(2, 2).All(i => Vector3.Distance(session.positions[i], barsTargetsBefore[i]) < .0001f
                && Quaternion.Angle(session.rotations[i], barsRotationsBefore[i]) < .02f);
            report.barsBonesReachContacts = report.barsBoneReachError < .015f;

            // Contacts disabled: the bike still pivots, but neither IK targets
            // nor observed endpoint bones are allowed to move.
            Call(editor, "Sample");
            set.handsFollowBars = false;
            set.feetFollowPedals = false;
            Vector3[] disabledTargetsBefore = session.positions.Take(4).ToArray();
            Quaternion[] disabledRotationsBefore = session.rotations.Take(4).ToArray();
            Vector3[] disabledBonesBefore = session.bones.Take(4).Select(bone => bone.position).ToArray();
            Quaternion[] disabledBoneRotationsBefore = session.bones.Take(4).Select(bone => bone.rotation).ToArray();
            report.disabledContactsClassified = Enumerable.Range(0, 4).All(i => !RoweBikeContacts.ShouldFollow(set, session, i));
            PhysicalAxis(joints[0], out Vector3 disabledPivot, out Vector3 disabledHeadset);
            MoveFrame(editor, 8f, disabledPivot, (disabledHeadset - disabledPivot).normalized);
            report.disabledTargetError = Mathf.Max(
                PositionError(session.positions, disabledTargetsBefore, Enumerable.Range(0, 4)),
                RotationError(session.rotations, disabledRotationsBefore, Enumerable.Range(0, 4)) / 1000f);
            report.disabledBoneError = Mathf.Max(
                Enumerable.Range(0, 4).Max(i => Vector3.Distance(session.bones[i].position, disabledBonesBefore[i])),
                Enumerable.Range(0, 4).Max(i => Quaternion.Angle(session.bones[i].rotation, disabledBoneRotationsBefore[i])) / 1000f);
            report.disabledRiderStays = report.disabledTargetError < .0001f && report.disabledBoneError < .0001f;

            // Literal translation must not be reinterpreted as a pivot orbit.
            // Test both physical-pivot controls and their complete child branch.
            Call(editor, "Sample");
            set.handsFollowBars = false;
            set.feetFollowPedals = false;
            JointPose[] frameTranslationJoints = CaptureJoints(joints);
            frameBefore = frameVisual.Capture(); barsBefore = barsVisual.Capture();
            rearBefore = rearVisual.Capture(); crankBefore = crankVisual.Capture(); frontBefore = frontVisual.Capture();
            Vector3 frameShift = new Vector3(.013f, -.009f, .017f);
            Call(editor, "MoveBikeControl", RoweBikeAnimation.FrameIndex,
                frameTranslationJoints[1].position + frameShift, frameTranslationJoints[1].rotation);
            frameAfter = frameVisual.Capture(); barsAfter = barsVisual.Capture();
            rearAfter = rearVisual.Capture(); crankAfter = crankVisual.Capture(); frontAfter = frontVisual.Capture();
            report.frameTranslationError = new[] {
                Vector3.Distance(joints[1].position, frameTranslationJoints[1].position + frameShift),
                Quaternion.Angle(joints[1].rotation, frameTranslationJoints[1].rotation) / 1000f,
                PositionError(frameAfter, Translate(frameBefore, frameShift)),
                PositionError(rearAfter, Translate(rearBefore, frameShift)),
                PositionError(crankAfter, Translate(crankBefore, frameShift)),
                PositionError(barsAfter, barsBefore), PositionError(frontAfter, frontBefore),
                FixedJointError(joints, frameTranslationJoints, new[] { 0, 2, 6 })
            }.Max();

            Call(editor, "Sample");
            JointPose[] barsTranslationJoints = CaptureJoints(joints);
            frameBefore = frameVisual.Capture(); barsBefore = barsVisual.Capture();
            rearBefore = rearVisual.Capture(); crankBefore = crankVisual.Capture(); frontBefore = frontVisual.Capture();
            Vector3 barsShift = new Vector3(-.011f, .014f, -.008f);
            Call(editor, "MoveBikeControl", RoweBikeAnimation.BarsIndex,
                barsTranslationJoints[2].position + barsShift, barsTranslationJoints[2].rotation);
            frameAfter = frameVisual.Capture(); barsAfter = barsVisual.Capture();
            rearAfter = rearVisual.Capture(); crankAfter = crankVisual.Capture(); frontAfter = frontVisual.Capture();
            report.barsTranslationError = new[] {
                Vector3.Distance(joints[2].position, barsTranslationJoints[2].position + barsShift),
                Quaternion.Angle(joints[2].rotation, barsTranslationJoints[2].rotation) / 1000f,
                Vector3.Distance(joints[6].position, barsTranslationJoints[6].position + barsShift),
                Quaternion.Angle(joints[6].rotation, barsTranslationJoints[6].rotation) / 1000f,
                PositionError(barsAfter, Translate(barsBefore, barsShift)),
                PositionError(frontAfter, Translate(frontBefore, barsShift)),
                PositionError(frameAfter, frameBefore), PositionError(rearAfter, rearBefore), PositionError(crankAfter, crankBefore),
                FixedJointError(joints, barsTranslationJoints, new[] { 0, 1, 3, 4, 5, 7 })
            }.Max();
            report.plainTranslationLiteral = report.frameTranslationError < .0002f && report.barsTranslationError < .0002f;

            // Four actual 90-degree edits are intentionally used instead of a
            // single 360 quaternion (which could vacuously reduce to identity).
            // Quarter one must visibly move; all four must close in world space.
            Call(editor, "Sample");
            JointPose[] fullTurnJoints = CaptureJoints(joints);
            frameBefore = frameVisual.Capture(); barsBefore = barsVisual.Capture();
            rearBefore = rearVisual.Capture(); crankBefore = crankVisual.Capture(); frontBefore = frontVisual.Capture();
            PhysicalAxis(joints[0], out Vector3 fullTurnPivot, out Vector3 fullTurnHeadset);
            Vector3 fullTurnAxis = (fullTurnHeadset - fullTurnPivot).normalized;
            for (int quarter = 0; quarter < 4; quarter++)
            {
                Vector3[] beforeQuarter = quarter == 0 ? barsVisual.Capture() : null;
                MoveBars(editor, 90f, fullTurnPivot, fullTurnAxis);
                if (quarter == 0) report.fullTurnQuarterMotion = Motion(beforeQuarter, barsVisual.Capture());
            }
            frameAfter = frameVisual.Capture(); barsAfter = barsVisual.Capture();
            rearAfter = rearVisual.Capture(); crankAfter = crankVisual.Capture(); frontAfter = frontVisual.Capture();
            report.fullTurnBikeError = BikeError(joints, fullTurnJoints);
            report.fullTurnVisualError = new[] {
                PositionError(frameAfter, frameBefore), PositionError(barsAfter, barsBefore),
                PositionError(rearAfter, rearBefore), PositionError(crankAfter, crankBefore),
                PositionError(frontAfter, frontBefore)
            }.Max();
            report.physicalFullTurnCloses = report.fullTurnQuarterMotion > .02f
                && report.fullTurnBikeError < .0005f && report.fullTurnVisualError < .0005f;

            // Save reachable edits on both physical-pivot branches, then verify
            // the same actual bike and rider state through Sample, Undo/Redo,
            // asset reload, and editor reopen.
            Call(editor, "Sample");
            set.handsFollowBars = true;
            set.feetFollowPedals = true;
            set.autoReleaseContacts = false;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            JointPose[] undoBike = CaptureJoints(joints);
            Vector3[] undoRider = CaptureBones(session);
            Quaternion[] undoRiderRotations = CaptureBoneRotations(session);
            string undoBikeSignature = RoweTweakFromHold.Signature(set.bikePoses);
            string undoRiderSignature = RoweTweakFromHold.Signature(set.poses);
            PhysicalAxis(joints[0], out Vector3 savedPivot, out Vector3 savedHeadset);
            Vector3 savedAxis = (savedHeadset - savedPivot).normalized;
            MoveFrame(editor, 8f, savedPivot, savedAxis);
            MoveBars(editor, -7f, savedPivot, savedAxis);
            JointPose[] savedBike = CaptureJoints(joints);
            Vector3[] savedRider = CaptureBones(session);
            Quaternion[] savedRiderRotations = CaptureBoneRotations(session);
            Call(editor, "Commit");
            Undo.FlushUndoRecordObjects();
            string savedBikeSignature = RoweTweakFromHold.Signature(set.bikePoses);
            string savedRiderSignature = RoweTweakFromHold.Signature(set.poses);
            Call(editor, "Sample");
            report.savedBikeError = BikeError(joints, savedBike);
            report.savedRiderError = RiderError(session, savedRider, savedRiderRotations);
            report.savedPose = savedBikeSignature != undoBikeSignature && savedRiderSignature != undoRiderSignature
                && report.savedBikeError < .0002f && report.savedRiderError < .02f;

            Undo.PerformUndo();
            Call(editor, "Sample");
            report.undoBikeError = BikeError(joints, undoBike);
            report.undoRiderError = RiderError(session, undoRider, undoRiderRotations);
            report.undoRestores = RoweTweakFromHold.Signature(set.bikePoses) == undoBikeSignature
                && RoweTweakFromHold.Signature(set.poses) == undoRiderSignature
                && report.undoBikeError < .0002f && report.undoRiderError < .02f;

            Undo.PerformRedo();
            Call(editor, "Sample");
            report.redoBikeError = BikeError(joints, savedBike);
            report.redoRiderError = RiderError(session, savedRider, savedRiderRotations);
            report.redoRestores = RoweTweakFromHold.Signature(set.bikePoses) == savedBikeSignature
                && RoweTweakFromHold.Signature(set.poses) == savedRiderSignature
                && report.redoBikeError < .0002f && report.redoRiderError < .02f;

            string bikePosePath = AssetDatabase.GetAssetPath(set.bikePoses);
            string riderPosePath = AssetDatabase.GetAssetPath(set.poses);
            Call(editor, "StopWithoutCommit");
            editor.Close();
            editor = null;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(bikePosePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(riderPosePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(report.recipePath, ImportAssetOptions.ForceSynchronousImport);
            set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(report.recipePath);
            editor = RoweIKPoseEditor.OpenRecipe(set);
            Call(editor, "BeginStudioPose", 1);
            session = Session(editor);
            joints = session.bikeJoints;
            report.reopenedBikeError = BikeError(joints, savedBike);
            report.reopenedRiderError = RiderError(session, savedRider, savedRiderRotations);
            report.reopenedPose = RoweTweakFromHold.Signature(set.bikePoses) == savedBikeSignature
                && RoweTweakFromHold.Signature(set.poses) == savedRiderSignature
                && report.reopenedBikeError < .0002f && report.reopenedRiderError < .02f;

            Call(editor, "StopWithoutCommit");
            editor.Close();
            editor = null;
        }
        catch (Exception ex)
        {
            report.error = ex.ToString();
            Debug.LogException(ex);
        }
        finally
        {
            if (editor)
            {
                try { Call(editor, "StopWithoutCommit"); }
                catch (Exception ex) { if (report.error == null) report.error = ex.ToString(); }
                editor.Close();
            }
            report.sourcePreserved = SameBytes(sourceBefore, SourcePath) && SameBytes(sourceMetaBefore, SourcePath + ".meta");
            report.referencePreserved = SameBytes(referenceBefore, StaticReferencePath) && SameBytes(referenceMetaBefore, StaticReferencePath + ".meta");
            report.sceneRestored = original != null && original.Changed() == 0 && SameBytes(sceneBefore, RowePairedWorkbench.ScenePath);
            if (original != null && original.Changed() != 0) original.Restore();
        }

        report.passed = report.error == null && new[] {
            report.sdkAxisRepresented, report.nonVacuousFixture,
            report.frameVisualOrbits, report.frameJointOrbits, report.rearAndCranksFollow,
            report.forkBranchFixed, report.frontAssemblyFixed,
            report.barsVisualOrbits, report.barsJointOrbits, report.barsOriginNotPivot,
            report.frontFollowsBars, report.frameBranchFixedForBars,
            report.enabledContactsClassified, report.enabledHandsStayOnBars, report.enabledFeetFollowFrame,
            report.enabledBonesReachContacts, report.disabledContactsClassified, report.disabledRiderStays,
            report.barsContactsClassified, report.barsHandsFollow, report.barsFeetStay, report.barsBonesReachContacts,
            report.plainTranslationLiteral, report.physicalFullTurnCloses,
            report.savedPose, report.undoRestores, report.redoRestores, report.reopenedPose,
            report.sourcePreserved, report.referencePreserved, report.sceneRestored
        }.All(value => value);
        if (!report.passed && report.error == null)
            report.error = "One or more physical head-tube pivot checks failed; inspect the measured Frame/Bars branch, contact, translation, and closure errors.";
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "studio-frame-fork-pivot-validation.json"), JsonUtility.ToJson(report, true));
        Debug.Log("ROWE_FRAME_FORK_PIVOT_VALIDATION " + JsonUtility.ToJson(report));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
