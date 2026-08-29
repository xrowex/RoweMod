using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only editor regression; owns a newly generated recipe and its clips.
public static class RoweWristRelaxationValidation
{
    [Serializable] public class Report
    {
        public bool passed, referencesFromRiding, defaultHalfStrength, crampedFixture,
            zeroIsNoOp, leftOnlyImproves, unselectedHandKept, positionsKept,
            feetAndHipsKept, partialNotCompounded, fullImprovesFurther,
            bothHandsImprove, riderOnlyWorks, autoKeySaved, savedPoseImproves,
            undoRestoresPose, redoRestoresRelaxation, initializationKeepsRedo,
            savedReferencesPersist, reopenMatches, pairedWorks, contactAnchorsKept,
            manualRotationStillWorks, sourcePreserved, sceneRestored;
        public float startingLeftError, startingRightError, partialLeftError, fullLeftError,
            selectedPositionError, unselectedRotationError, partialExpectedTargetError,
            partialExpectedBoneError, savedLeftError, reopenedLeftError, manualRotationChange;
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
    static Quaternion Relative(RoweIKSession session, int hand)
        => Quaternion.Inverse(session.bones[5 + hand].rotation) * session.bones[hand].rotation;
    static float WristError(RoweHeldTrick set, RoweIKSession session, int hand)
        => Quaternion.Angle(Relative(session, hand), set.ridingWristRotations[hand]);
    static bool SameRotations(Quaternion[] a, Quaternion[] b)
        => a != null && b != null && a.Length == b.Length
            && Enumerable.Range(0, a.Length).All(i => Quaternion.Angle(a[i], b[i]) < .05f);
    static bool SamePoints(Vector3[] a, Vector3[] b)
        => a != null && b != null && a.Length == b.Length
            && Enumerable.Range(0, a.Length).All(i => Vector3.Distance(a[i], b[i]) < .000001f);
    static void Cramp(RoweHeldTrick set, RoweIKSession session)
    {
        // Deliberate local wrist flexion, without moving either grip target.
        for (int hand = 0; hand < 2; hand++)
        {
            var offset = Quaternion.Euler(hand == 0 ? 55 : -50, 0, hand == 0 ? 25 : -25);
            session.MoveControl(hand, session.positions[hand],
                session.bones[5 + hand].rotation * set.ridingWristRotations[hand] * offset);
        }
    }
    static void FreshCramp(RoweIKPoseEditor editor, RoweHeldTrick set)
    {
        Call(editor, "Sample");
        Cramp(set, Session(editor));
    }

    public static void Run()
    {
        var r = new Report();
        RoweIKPoseEditor editor = null;
        RowePairedWorkbench.Pose original = null;
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
            var set = RoweHeldTrickBuilder.Create("Wrist Relaxation Validation", source, 0, 0, rider, bike);
            r.recipePath = AssetDatabase.GetAssetPath(set);
            RoweTrickStyles.Choose(set, RoweTrickStyle.Hold);
            editor = RoweIKPoseEditor.OpenRecipe(set); Call(editor, "BeginStudioPose", 1);
            var session = Session(editor);
            var reference = set.ridingWristRotations.ToArray();
            r.referencesFromRiding = RoweWristRelaxation.HasReference(set)
                && Enumerable.Range(0, 2).All(i => WristError(set, session, i) < .1f);
            r.defaultHalfStrength = Mathf.Abs(Get<float>(editor, "wristRelaxation") - .5f) < .00001f;
            r.riderOnlyWorks = !set.authorBike && !set.bikePoses && RoweWristRelaxation.HasReference(set);

            Cramp(set, session);
            r.startingLeftError = WristError(set, session, 0); r.startingRightError = WristError(set, session, 1);
            r.crampedFixture = r.startingLeftError > 15 && r.startingRightError > 15;
            var beforePositions = session.positions.ToArray(); var beforeRotations = session.rotations.ToArray();
            var beforeBones = session.bones.Select(t => t.position).ToArray();
            var beforeBoneRotations = session.bones.Select(t => t.rotation).ToArray();
            session.RelaxWrists(reference, 0, -1);
            r.zeroIsNoOp = SamePoints(beforePositions, session.positions) && SameRotations(beforeRotations, session.rotations)
                && SamePoints(beforeBones, session.bones.Select(t => t.position).ToArray())
                && SameRotations(beforeBoneRotations, session.bones.Select(t => t.rotation).ToArray());

            session.RelaxWrists(reference, .5f, 0);
            r.partialLeftError = WristError(set, session, 0);
            r.leftOnlyImproves = r.partialLeftError < r.startingLeftError - 3;
            r.unselectedRotationError = Quaternion.Angle(beforeBoneRotations[1], session.bones[1].rotation);
            r.unselectedHandKept = Quaternion.Angle(beforeRotations[1], session.rotations[1]) < .05f
                && r.unselectedRotationError < 1;
            r.selectedPositionError = Enumerable.Range(0, 5).Max(i => Vector3.Distance(beforeBones[i], session.bones[i].position));
            r.positionsKept = SamePoints(beforePositions.Take(5).ToArray(), session.positions.Take(5).ToArray())
                && r.selectedPositionError < .015f;
            r.feetAndHipsKept = Enumerable.Range(2, 3).All(i => Quaternion.Angle(beforeRotations[i], session.rotations[i]) < .05f
                && Quaternion.Angle(beforeBoneRotations[i], session.bones[i].rotation) < 1);

            // Independently construct the expected two-pass half-strength edit.
            // Both passes blend from the same original visible hand, not from
            // the preceding blend (which would incorrectly compound to 75%).
            FreshCramp(editor, set);
            Quaternion starting = session.bones[0].rotation;
            Quaternion expected = Quaternion.identity;
            for (int pass = 0; pass < 2; pass++)
            {
                expected = Quaternion.Slerp(starting, session.bones[5].rotation * reference[0], .5f);
                session.MoveControl(0, session.positions[0], expected);
            }
            Quaternion expectedBone = session.bones[0].rotation;
            FreshCramp(editor, set);
            session.RelaxWrists(reference, .5f, 0);
            r.partialExpectedTargetError = Quaternion.Angle(expected, session.rotations[0]);
            r.partialExpectedBoneError = Quaternion.Angle(expectedBone, session.bones[0].rotation);
            r.partialNotCompounded = r.partialExpectedTargetError < .2f && r.partialExpectedBoneError < .5f;
            FreshCramp(editor, set);
            session.RelaxWrists(reference, 1, 0); r.fullLeftError = WristError(set, session, 0);
            r.fullImprovesFurther = r.fullLeftError < r.partialLeftError - 2 && r.fullLeftError < r.startingLeftError * .65f;
            FreshCramp(editor, set); session.RelaxWrists(reference, .5f, -1);
            r.bothHandsImprove = WristError(set, session, 0) < r.startingLeftError - 3
                && WristError(set, session, 1) < r.startingRightError - 3;

            // Save a cramped pose, then use the same auto-key command the GUI
            // invokes. This checks actual saved bones, not just target quaternions.
            FreshCramp(editor, set); Set(editor, "pending", true); Call(editor, "Commit"); Call(editor, "Sample");
            string crampedClip = RoweTweakFromHold.Signature(set.poses);
            float savedCrampError = WristError(set, session, 0);
            Set(editor, "wristRelaxation", .5f); Set(editor, "autoKey", true);
            Call(editor, "ApplyWristRelaxation", 0);
            string relaxedClip = RoweTweakFromHold.Signature(set.poses);
            r.autoKeySaved = crampedClip != relaxedClip && !Get<bool>(editor, "pending");
            Call(editor, "Sample"); r.savedLeftError = WristError(set, session, 0);
            r.savedPoseImproves = r.savedLeftError < savedCrampError - 3;
            Undo.FlushUndoRecordObjects();
            // Lazy initialization inside an Undo sample must not create a new
            // undo branch. This rider-only Hold has no Tweak recipe undo entry.
            set.ridingWristRotations = null;
            Undo.PerformUndo(); Call(editor, "Sample");
            r.undoRestoresPose = RoweTweakFromHold.Signature(set.poses) == crampedClip
                && Mathf.Abs(WristError(set, session, 0) - savedCrampError) < 1;
            Undo.PerformRedo(); Call(editor, "Sample");
            r.redoRestoresRelaxation = RoweTweakFromHold.Signature(set.poses) == relaxedClip
                && Mathf.Abs(WristError(set, session, 0) - r.savedLeftError) < 1;
            r.initializationKeepsRedo = r.redoRestoresRelaxation && SameRotations(reference, set.ridingWristRotations);

            Call(editor, "Stop"); editor.Close(); editor = null;
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(set.poses), ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(r.recipePath, ImportAssetOptions.ForceSynchronousImport);
            set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(r.recipePath);
            r.savedReferencesPersist = RoweWristRelaxation.HasReference(set) && SameRotations(reference, set.ridingWristRotations);
            editor = RoweIKPoseEditor.OpenRecipe(set); Call(editor, "BeginStudioPose", 1); session = Session(editor);
            r.reopenedLeftError = WristError(set, session, 0);
            r.reopenMatches = RoweTweakFromHold.Signature(set.poses) == relaxedClip
                && Mathf.Abs(r.reopenedLeftError - r.savedLeftError) < 1;

            Call(editor, "Stop"); RoweBikeAnimation.Enable(set, rider, bike); Call(editor, "BeginStudioPose", 1); session = Session(editor);
            var points = set.ridingContactPoints.ToArray();
            var palmPoints = set.ridingPalmContactPoints.ToArray();
            beforeBones = session.bones.Take(5).Select(t => t.position).ToArray();
            // Capture both eligibility decisions and the normal CURRENT-bar
            // targets before the UI command can move either hand. Auto-release
            // being off must not make a distant hand eligible for this snap.
            var normalGrips = palmPoints.Select(p => session.bikeJoints[2].TransformPoint(p)).ToArray();
            var snapHands = Enumerable.Range(0, 2).Select(i => set.handsFollowBars
                && Vector3.Distance(session.GripPoint(i), normalGrips[i]) <= RoweBikeContacts.ReleaseDistance(set)).ToArray();
            Set(editor, "autoKey", false); Call(editor, "ApplyWristRelaxation", -1);
            r.pairedWorks = set.authorBike && set.bikePoses && snapHands.Any(eligible => eligible) && Get<bool>(editor, "pending")
                // The UI snaps eligible palms to normal grips rather than
                // preserving their previous offsets. Free wrists stay put.
                && Enumerable.Range(0, 2).All(i => snapHands[i]
                    ? Vector3.Distance(normalGrips[i], session.GripPoint(i)) <= .001f
                    : Vector3.Distance(beforeBones[i], session.bones[i].position) <= .001f)
                && Enumerable.Range(2, 3).All(i => Vector3.Distance(beforeBones[i], session.bones[i].position) < .015f);
            r.contactAnchorsKept = RoweBikeContacts.HasReference(set) && SamePoints(points, set.ridingContactPoints)
                && RoweBikeContacts.HasPalmReference(set) && SamePoints(palmPoints, set.ridingPalmContactPoints)
                && SameRotations(reference, set.ridingWristRotations);

            // A one-time command must leave normal hand rotation tools usable.
            Quaternion beforeManual = session.bones[0].rotation;
            Quaternion requested = beforeManual * Quaternion.Euler(25, 0, 0);
            session.MoveControl(0, session.positions[0], requested);
            RoweWristRelaxation.EnsureReference(set, session);
            session.Solve();
            r.manualRotationChange = Quaternion.Angle(beforeManual, session.bones[0].rotation);
            r.manualRotationStillWorks = Quaternion.Angle(session.rotations[0], requested) < .05f && r.manualRotationChange > 10;
            Call(editor, "Sample");
        }
        catch (Exception ex) { r.error = ex.ToString(); Debug.LogException(ex); }
        finally
        {
            if (editor) { Call(editor, "StopWithoutCommit"); editor.Close(); }
            r.sourcePreserved = sourceBefore != null && File.ReadAllText(SourcePath) == sourceBefore;
            r.sceneRestored = original != null && original.Changed() == 0;
        }
        r.passed = r.error == null && new[] {
            r.referencesFromRiding, r.defaultHalfStrength, r.crampedFixture,
            r.zeroIsNoOp, r.leftOnlyImproves, r.unselectedHandKept, r.positionsKept,
            r.feetAndHipsKept, r.partialNotCompounded, r.fullImprovesFurther,
            r.bothHandsImprove, r.riderOnlyWorks, r.autoKeySaved, r.savedPoseImproves,
            r.undoRestoresPose, r.redoRestoresRelaxation, r.initializationKeepsRedo,
            r.savedReferencesPersist, r.reopenMatches, r.pairedWorks, r.contactAnchorsKept,
            r.manualRotationStillWorks, r.sourcePreserved, r.sceneRestored
        }.All(value => value);
        if (!r.passed && r.error == null) r.error = "One or more wrist checks failed; see measured angles and individual flags.";
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "studio-wrist-relaxation-validation.json"), JsonUtility.ToJson(r, true));
        Debug.Log("ROWE_WRIST_RELAXATION_VALIDATION " + JsonUtility.ToJson(r));
        EditorApplication.Exit(r.passed ? 0 : 1);
    }
}
