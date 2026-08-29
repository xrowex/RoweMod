using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoweMod.AnimationAuthoring;

// Run only in the isolated Validation-* workbench, never the creator's project.
// Every clip edit belongs to a newly generated validation recipe.
public static class RoweContactReleaseValidation
{
    [Serializable] public class Report
    {
        public bool passed, defaults, parentMapping, independentRelease, adjustableDistance, movedBackReattaches, nearOffsetsPreserved,
            releasedTargetsStay, releasedBonesStay, largeMoveDoesNotSelfRelease,
            manualOffWins, autoReleaseOffFollows, unpinnedHonorsRelease, unpinnedHipsMove,
            resamplingReattaches, undoRestoresContact, redoReleasesContact,
            saveReopenClassification, tweakKeepsRidingAnchors, fixedReferences,
            bakeResamplesReleasedFoot, bikeFullTurn, bakeUndo, initializationKeepsRedo, clipsOnlyExport,
            sourcePreserved, sceneRestored;
        public float releasedHandDistance, releasedFootDistance, dragReachError,
            nearTargetError, releasedBoneError, reopenedHandDistance, reopenedFootDistance,
            bakeFootPositionError, bakeFootRotationError, sourceFootMotion;
        public string error, recipePath;
    }

    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string SourcePath = "Assets/CustomClips/RoweMod_Custom_My Trick 3.anim";
    static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static T Get<T>(object target, string name)
        => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value)
        => target.GetType().GetField(name, Private).SetValue(target, value);
    static RoweIKSession Session(RoweIKPoseEditor editor) => Get<RoweIKSession>(editor, "session");
    static bool SamePoints(Vector3[] a, Vector3[] b)
        => a != null && b != null && a.Length == b.Length
            && Enumerable.Range(0, a.Length).All(i => Vector3.Distance(a[i], b[i]) < .000001f);
    static Vector3 Reference(RoweHeldTrick set, RoweIKSession session, int index)
        => RoweBikeContacts.Parent(session, index).TransformPoint(set.ridingContactPoints[index]);
    static float ActualDistance(RoweHeldTrick set, RoweIKSession session, int index)
        => Vector3.Distance(session.bones[index].position, Reference(set, session, index));
    static void Move(RoweIKPoseEditor editor, int part, Vector3 shift, Quaternion turn)
    {
        var joint = Session(editor).bikeJoints[part];
        Call(editor, "MoveBikeControl", part, joint.position + shift, joint.rotation * turn);
    }
    static void Offset(RoweIKSession session, int index, float distance, float rotation = 0)
    {
        // Shorten the limb toward its own elbow/knee, so the desired away pose
        // remains reachable instead of testing an impossible stretched target.
        Vector3 direction = (session.bones[5 + index].position - session.bones[index].position).normalized;
        if (direction.sqrMagnitude < .9f) throw new InvalidOperationException("Missing limb direction.");
        session.MoveControl(index, session.positions[index] + direction * distance,
            session.rotations[index] * Quaternion.Euler(rotation, 0, 0));
    }
    static void AwayPair(RoweIKSession session)
    {
        Offset(session, 0, .25f, 12);
        Offset(session, 2, .25f, 10);
    }
    static bool ClassifiedPair(RoweHeldTrick set, RoweIKSession session)
        => !RoweBikeContacts.ShouldFollow(set, session, 0)
            && RoweBikeContacts.ShouldFollow(set, session, 1)
            && !RoweBikeContacts.ShouldFollow(set, session, 2)
            && RoweBikeContacts.ShouldFollow(set, session, 3);
    static Quaternion Rotation(AnimationClip clip, int part, float time)
    {
        var q = new[] { "x", "y", "z", "w" }.Select(axis => AnimationUtility.GetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(RoweBikeAnimation.Paths[part], typeof(Transform), "m_LocalRotation." + axis)).Evaluate(time)).ToArray();
        return new Quaternion(q[0], q[1], q[2], q[3]).normalized;
    }
    static bool CheckExport(RoweHeldTrick set)
    {
        string path = RoweHeldTrickBuilder.Export(set, Path.Combine(RoweAnimationWorkspace.Workspace, "ValidationExports"));
        var bundle = AssetBundle.LoadFromFile(path);
        if (!bundle) return false;
        try
        {
            var assets = bundle.LoadAllAssets();
            var text = assets.OfType<TextAsset>().Single().text;
            var package = JsonUtility.FromJson<RoweAnimationPackage>(text);
            var clips = assets.OfType<AnimationClip>().ToDictionary(c => c.name);
            return clips.Count > 0 && assets.All(a => a is AnimationClip || a is TextAsset)
                && package.Validate((name, human) => clips.TryGetValue(name, out var c) && c.isHumanMotion == human) == null
                && !text.Contains("ridingContactPoints") && !text.Contains("contactReleaseDistance");
        }
        finally { bundle.Unload(true); }
    }

    public static void Run()
    {
        var r = new Report();
        RoweIKPoseEditor editor = null;
        RowePairedWorkbench.Pose original = null;
        AnimationClip movementBefore = null, bikeBefore = null;
        string sourceBefore = null;
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run this only in a separate Validation-* project in batch mode.");
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider = RowePairedWorkbench.FindRig("Human Temp");
            var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            original = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            sourceBefore = File.ReadAllText(SourcePath);
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourcePath);
            var set = RoweHeldTrickBuilder.Create("Contact Release Validation", source, 0, 0, rider, bike);
            r.recipePath = AssetDatabase.GetAssetPath(set);
            RoweTrickStyles.Choose(set, RoweTrickStyle.HoldTweak);
            RoweBikeAnimation.Enable(set, rider, bike);
            editor = RoweIKPoseEditor.OpenRecipe(set);
            Call(editor, "BeginStudioPose", 1);
            var session = Session(editor);
            var anchors = set.ridingContactPoints.ToArray();
            r.defaults = set.autoReleaseContacts && Mathf.Abs(set.contactReleaseDistance - .15f) < .00001f
                && RoweBikeContacts.HasReference(set) && Enumerable.Range(0, 4).All(i => RoweBikeContacts.ShouldFollow(set, session, i));
            r.parentMapping = Enumerable.Range(0, 4).All(i => RoweBikeContacts.Parent(session, i) == session.bikeJoints[i < 2 ? 2 : i + 2]);

            AwayPair(session);
            Offset(session, 1, .025f);
            Offset(session, 3, .025f);
            r.releasedHandDistance = ActualDistance(set, session, 0);
            r.releasedFootDistance = ActualDistance(set, session, 2);
            r.dragReachError = Enumerable.Range(0, 4).Max(i => Vector3.Distance(session.positions[i], session.bones[i].position));
            r.independentRelease = ClassifiedPair(set, session)
                && r.releasedHandDistance > .19f && r.releasedFootDistance > .19f && r.dragReachError < .025f;
            set.contactReleaseDistance = .3f;
            r.adjustableDistance = Enumerable.Range(0, 4).All(i => RoweBikeContacts.ShouldFollow(set, session, i));
            set.contactReleaseDistance = .15f;
            r.adjustableDistance &= ClassifiedPair(set, session);
            var before = session.positions.ToArray();
            var beforeRotations = session.rotations.ToArray();
            var beforeBones = session.bones.Select(t => t.position).ToArray();
            var offsets = Enumerable.Range(0, 4).Select(i => RoweBikeContacts.Parent(session, i).InverseTransformPoint(before[i])).ToArray();
            Move(editor, 0, Vector3.up * .01f, Quaternion.Euler(0, 4, 0));
            r.nearTargetError = new[] { 1, 3 }.Max(i => Vector3.Distance(session.positions[i], RoweBikeContacts.Parent(session, i).TransformPoint(offsets[i])));
            r.nearOffsetsPreserved = r.nearTargetError < .0001f
                && new[] { 1, 3 }.All(i => Vector3.Distance(session.positions[i], Reference(set, session, i)) > .015f);
            r.releasedTargetsStay = new[] { 0, 2 }.All(i => Vector3.Distance(session.positions[i], before[i]) < .0001f
                && Quaternion.Angle(session.rotations[i], beforeRotations[i]) < .01f);
            r.releasedBoneError = new[] { 0, 2 }.Max(i => Vector3.Distance(session.bones[i].position, beforeBones[i]));
            r.releasedBonesStay = r.releasedBoneError < .025f;
            RoweBikeContacts.EnsureReference(set, session);
            r.fixedReferences = SamePoints(anchors, set.ridingContactPoints);

            // Classify before moving the bars. A single large bike drag should
            // not release contacts merely because the bars moved first.
            Call(editor, "Sample");
            before = session.positions.ToArray();
            offsets = Enumerable.Range(0, 2).Select(i => session.bikeJoints[2].InverseTransformPoint(before[i])).ToArray();
            Move(editor, 2, Vector3.zero, Quaternion.Euler(0, 145, 0));
            r.largeMoveDoesNotSelfRelease = Enumerable.Range(0, 2).All(i => Vector3.Distance(session.positions[i], session.bikeJoints[2].TransformPoint(offsets[i])) < .0001f)
                && Enumerable.Range(0, 2).Any(i => Vector3.Distance(before[i], session.positions[i]) > .15f);
            Call(editor, "Sample");
            r.resamplingReattaches = Enumerable.Range(0, 4).All(i => RoweBikeContacts.ShouldFollow(set, session, i));
            Offset(session, 0, .25f);
            bool wasReleased = !RoweBikeContacts.ShouldFollow(set, session, 0);
            var nearGrip = Reference(set, session, 0) + Vector3.up * .01f;
            session.MoveControl(0, nearGrip, session.rotations[0]);
            r.movedBackReattaches = wasReleased && RoweBikeContacts.ShouldFollow(set, session, 0)
                && Vector3.Distance(session.positions[0], nearGrip) < .0001f;
            Call(editor, "Sample");

            set.handsFollowBars = false; set.feetFollowPedals = false; set.autoReleaseContacts = false;
            before = session.positions.ToArray();
            Move(editor, 0, Vector3.up * .01f, Quaternion.Euler(0, 5, 0));
            r.manualOffWins = Enumerable.Range(0, 4).All(i => !RoweBikeContacts.ShouldFollow(set, session, i) && Vector3.Distance(before[i], session.positions[i]) < .0001f);
            Call(editor, "Sample");
            set.handsFollowBars = true; set.feetFollowPedals = true;
            AwayPair(session);
            offsets = Enumerable.Range(0, 4).Select(i => RoweBikeContacts.Parent(session, i).InverseTransformPoint(session.positions[i])).ToArray();
            Move(editor, 0, Vector3.up * .01f, Quaternion.Euler(0, 4, 0));
            r.autoReleaseOffFollows = Enumerable.Range(0, 4).All(i => RoweBikeContacts.ShouldFollow(set, session, i)
                && Vector3.Distance(session.positions[i], RoweBikeContacts.Parent(session, i).TransformPoint(offsets[i])) < .0001f);

            Call(editor, "Sample");
            set.autoReleaseContacts = true; set.keepHipsInPlace = false; set.riderFollowsWholeBike = true;
            AwayPair(session);
            before = session.positions.ToArray(); beforeRotations = session.rotations.ToArray();
            var whole = session.bikeJoints[0]; var oldMatrix = whole.localToWorldMatrix; var oldRotation = whole.rotation;
            Move(editor, 0, Vector3.up * .01f, Quaternion.Euler(0, 4, 0));
            var delta = whole.localToWorldMatrix * oldMatrix.inverse; var turn = whole.rotation * Quaternion.Inverse(oldRotation);
            r.unpinnedHonorsRelease = Enumerable.Range(0, 4).All(i => Vector3.Distance(session.positions[i], i == 0 || i == 2 ? before[i] : delta.MultiplyPoint3x4(before[i])) < .0001f
                && Quaternion.Angle(session.rotations[i], i == 0 || i == 2 ? beforeRotations[i] : turn * beforeRotations[i]) < .01f);
            r.unpinnedHipsMove = Vector3.Distance(session.positions[4], delta.MultiplyPoint3x4(before[4])) < .0001f
                && Vector3.Distance(session.positions[4], before[4]) > .005f;

            // Commit real attainable bones, then derive permissions from sampled
            // saved curves. No test-only release flags survive Undo or reopening.
            Call(editor, "Sample"); set.keepHipsInPlace = true;
            string originalHold = RoweTweakFromHold.Signature(set.poses);
            AwayPair(session); Set(editor, "pending", true); Call(editor, "Commit");
            Undo.FlushUndoRecordObjects(); Call(editor, "Sample");
            string awayHold = RoweTweakFromHold.Signature(set.poses);
            Undo.PerformUndo(); Call(editor, "Sample");
            r.undoRestoresContact = RoweTweakFromHold.Signature(set.poses) == originalHold
                && Enumerable.Range(0, 4).All(i => RoweBikeContacts.ShouldFollow(set, session, i));
            Undo.PerformRedo(); Call(editor, "Sample");
            r.redoReleasesContact = RoweTweakFromHold.Signature(set.poses) == awayHold && ClassifiedPair(set, session);
            Call(editor, "Stop"); editor.Close(); editor = null;
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(set.poses), ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(r.recipePath, ImportAssetOptions.ForceSynchronousImport);
            set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(r.recipePath);
            editor = RoweIKPoseEditor.OpenRecipe(set); Call(editor, "BeginStudioPose", 1); session = Session(editor);
            r.reopenedHandDistance = ActualDistance(set, session, 0); r.reopenedFootDistance = ActualDistance(set, session, 2);
            r.saveReopenClassification = ClassifiedPair(set, session) && r.reopenedHandDistance > .19f && r.reopenedFootDistance > .19f;
            Call(editor, "BeginStudioPose", 2);
            RoweBikeContacts.EnsureReference(set, session);
            r.tweakKeepsRidingAnchors = Get<AnimationClip>(editor, "clip") == set.tweakPose
                && ClassifiedPair(set, session) && SamePoints(anchors, set.ridingContactPoints);

            // Spin just the released pedal: all other contacts are unaffected.
            // Give the released foot real source motion, so a bake that carries
            // forward the last frame instead of resampling is also detected.
            Call(editor, "Stop");
            RoweTrickStyles.Choose(set, RoweTrickStyle.Loop); RoweTrickStyles.MakeMovementFromHeld(set, 1);
            Call(editor, "StartPairedMovement"); Set(editor, "seconds", .5f); Call(editor, "Sample"); session = Session(editor);
            Offset(session, 2, .035f, 5); Set(editor, "pending", true); Call(editor, "Commit");
            movementBefore = UnityEngine.Object.Instantiate(set.game.motionLoop);
            bikeBefore = UnityEngine.Object.Instantiate(set.bikeMotionLoop);
            string movementSignature = RoweTweakFromHold.Signature(set.game.motionLoop), bikeSignature = RoweTweakFromHold.Signature(set.bikeMotionLoop);
            float[] times = { 0, .25f, .5f, .75f, 1 };
            var footPositions = new Vector3[times.Length]; var footRotations = new Quaternion[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                session.Sample(movementBefore, bikeBefore, times[i], times[i]);
                if (RoweBikeContacts.ShouldFollow(set, session, 2)) throw new InvalidOperationException("Bake fixture foot must remain released over its source motion.");
                footPositions[i] = session.bones[2].position; footRotations[i] = session.bones[2].rotation;
            }
            r.sourceFootMotion = Vector3.Distance(footPositions[0], footPositions[2]);
            Set(editor, "selectedBike", 4); Set(editor, "turnAxis", 0); Set(editor, "reverseTurn", false);
            Call(editor, "BakeBikeTurn"); session = Session(editor);
            for (int i = 0; i < times.Length; i++)
            {
                session.Sample(set.game.motionLoop, set.bikeMotionLoop, times[i], times[i]);
                r.bakeFootPositionError = Mathf.Max(r.bakeFootPositionError, Vector3.Distance(session.bones[2].position, footPositions[i]));
                r.bakeFootRotationError = Mathf.Max(r.bakeFootRotationError, Quaternion.Angle(session.bones[2].rotation, footRotations[i]));
            }
            r.bakeResamplesReleasedFoot = r.sourceFootMotion > .015f && r.bakeFootPositionError < .02f && r.bakeFootRotationError < 1;
            r.bikeFullTurn = times.All(t => Quaternion.Angle(Rotation(set.bikeMotionLoop, 4, t), Rotation(bikeBefore, 4, t) * Quaternion.Euler(t * 360, 0, 0)) < .1f);
            string bakedMovement = RoweTweakFromHold.Signature(set.game.motionLoop), bakedBike = RoweTweakFromHold.Signature(set.bikeMotionLoop);
            // Emulate a pre-upgrade recipe with no saved contact metadata. Undo
            // resamples through the editor callback and lazily initializes it;
            // this derived metadata must not branch away from the pending Redo.
            set.ridingContactPoints = null;
            Undo.PerformUndo();
            r.bakeUndo = RoweTweakFromHold.Signature(set.game.motionLoop) == movementSignature && RoweTweakFromHold.Signature(set.bikeMotionLoop) == bikeSignature;
            Undo.PerformRedo();
            r.initializationKeepsRedo = RoweBikeContacts.HasReference(set) && SamePoints(anchors, set.ridingContactPoints)
                && RoweTweakFromHold.Signature(set.game.motionLoop) == bakedMovement && RoweTweakFromHold.Signature(set.bikeMotionLoop) == bakedBike;
            r.fixedReferences &= SamePoints(anchors, set.ridingContactPoints);
            Call(editor, "Stop"); editor.Close(); editor = null;
            r.clipsOnlyExport = CheckExport(set);
        }
        catch (Exception ex) { r.error = ex.ToString(); Debug.LogException(ex); }
        finally
        {
            if (editor) { Call(editor, "StopWithoutCommit"); editor.Close(); }
            if (movementBefore) UnityEngine.Object.DestroyImmediate(movementBefore);
            if (bikeBefore) UnityEngine.Object.DestroyImmediate(bikeBefore);
            r.sourcePreserved = sourceBefore != null && File.ReadAllText(SourcePath) == sourceBefore;
            r.sceneRestored = original != null && original.Changed() == 0;
        }
        r.passed = r.error == null && new[] {
            r.defaults, r.parentMapping, r.independentRelease, r.adjustableDistance, r.movedBackReattaches, r.nearOffsetsPreserved,
            r.releasedTargetsStay, r.releasedBonesStay, r.largeMoveDoesNotSelfRelease,
            r.manualOffWins, r.autoReleaseOffFollows, r.unpinnedHonorsRelease, r.unpinnedHipsMove,
            r.resamplingReattaches, r.undoRestoresContact, r.redoReleasesContact,
            r.saveReopenClassification, r.tweakKeepsRidingAnchors, r.fixedReferences,
            r.bakeResamplesReleasedFoot, r.bikeFullTurn, r.bakeUndo, r.initializationKeepsRedo, r.clipsOnlyExport,
            r.sourcePreserved, r.sceneRestored
        }.All(value => value);
        if (!r.passed && r.error == null) r.error = "One or more contact-release checks failed; see individual flags and measured errors.";
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "studio-contact-release-validation.json"), JsonUtility.ToJson(r, true));
        Debug.Log("ROWE_CONTACT_RELEASE_VALIDATION " + JsonUtility.ToJson(r));
        EditorApplication.Exit(r.passed ? 0 : 1);
    }
}
