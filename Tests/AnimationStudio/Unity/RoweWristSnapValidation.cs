using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated authoring regression. All edits belong to a generated recipe; the
// real table clips, their metadata, and the workbench scene remain untouched.
public static class RoweWristSnapValidation
{
    const string SourceRecipe = "Assets/CustomClips/HeldTricks/table_tweakn/table_tweakn.asset";
    const float Tolerance = .001f;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable] public class Spec
    {
        public string name;
        public int pose = 1, hand = -1, boundary;
        public bool realPose, follow = true, autoRelease = true, movedBars, repeat;
        public float leftOffset = .06f, rightOffset = .06f, strength = .5f, range = .15f;
    }
    [Serializable] sealed class Metadata
    {
        public bool hands, feet, autoRelease, hipsPinned, carriesRider;
        public float distance;
        public Vector3[] contacts, palms;
        public Quaternion[] wrists;
    }
    [Serializable] public class Hand
    {
        public Vector3 wrist, palm, normalGrip;
        public Quaternion rotation;
        public float distance;
        public bool follows, helperEligible, independentEligible;
    }
    sealed class Snapshot
    {
        public Hand[] hands;
        public Vector3[] body, bike;
        public Quaternion[] bodyRotations, bikeRotations;
        public string metadata;
    }
    [Serializable] public class HandStage
    {
        public Hand observed;
        public string expectedPivot;
        public float expectedPivotError, normalGripError, wristDrift, palmDrift, rotationChange;
    }
    [Serializable] public class Stage
    {
        public string name;
        public HandStage[] hands;
        public float feetError, hipsError, bodyRotationError, bikeError, bikeRotationError;
        public bool pivotsPassed, unselectedPassed, zeroStrengthKeepsRotation, bodyAndBikePassed, metadataKept;
    }
    [Serializable] public class Case
    {
        public Spec spec;
        public string error;
        public Hand[] before;
        public bool[] selectedSnap;
        public Stage[] stages;
        public bool passed, copiedSourceExactly, syntheticOffsetsReachable, helperAgrees, boundaryVerified,
            currentBarsExercised, undoRestoresClip, redoRestoresClip, noOpKeepsUndo,
            firstActionKeyed, firstActionChanged, reopenKeepsClip;
        public float releaseDistance, barAnchorMovement, maxPivotError, maxSnappedGripError;
    }
    [Serializable] public class Report
    {
        public bool passed, realSourceUsed, sourceFilesUnchanged, sceneRestored,
            nearExercised, farExercised, mixedExercised, minimumAndBoundaryExercised, zeroAndRepeatExercised,
            directNoOpRestoresTargets, unreachableSnapRestoresTargets;
        public string error, sourceRecipe, fixtureRecipe;
        public float toleranceMeters;
        public Case[] cases;
    }

    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static RoweIKSession Session(RoweIKPoseEditor editor) => Get<RoweIKSession>(editor, "session");
    static string Signature(AnimationClip clip) => RoweTweakFromHold.Signature(clip);
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    static string Meta(RoweHeldTrick set) => JsonUtility.ToJson(new Metadata {
        hands = set.handsFollowBars, feet = set.feetFollowPedals, autoRelease = set.autoReleaseContacts,
        hipsPinned = set.keepHipsInPlace, carriesRider = set.riderFollowsWholeBike,
        distance = set.contactReleaseDistance, contacts = set.ridingContactPoints,
        palms = set.ridingPalmContactPoints, wrists = set.ridingWristRotations
    });
    static Vector3 Palm(RoweIKSession session, int hand)
    {
        var knuckle = session.rider.GetBoneTransform(hand == 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        if (!knuckle) throw new InvalidOperationException("Validation rider needs the actual middle-finger proximal bone.");
        return Vector3.Lerp(session.bones[hand].position, knuckle.position, .5f);
    }
    static Vector3 NormalGrip(RoweHeldTrick set, RoweIKSession session, int hand)
        => session.bikeJoints[2].TransformPoint(set.ridingPalmContactPoints[hand]);
    static void CopyClip(AnimationClip source, AnimationClip target)
    {
        string name = target.name;
        EditorUtility.CopySerialized(source, target); target.name = name;
        EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
    }
    static void ResetFixture(RoweHeldTrick source, RoweHeldTrick set, Spec spec)
    {
        CopyClip(source.poses, set.poses); CopyClip(source.bikePoses, set.bikePoses);
        CopyClip(source.tweakPose, set.tweakPose); CopyClip(source.bikeTweakPose, set.bikeTweakPose);
        set.game.style = RoweTrickStyle.HoldTweak; set.authorBike = true; set.useCustomTweak = true;
        set.handsFollowBars = spec.follow; set.feetFollowPedals = source.feetFollowPedals;
        set.autoReleaseContacts = spec.autoRelease; set.contactReleaseDistance = spec.range;
        set.keepHipsInPlace = source.keepHipsInPlace; set.riderFollowsWholeBike = source.riderFollowsWholeBike;
        set.ridingContactPoints = source.ridingContactPoints?.ToArray();
        set.ridingPalmContactPoints = source.ridingPalmContactPoints?.ToArray();
        set.ridingWristRotations = source.ridingWristRotations?.ToArray();
        set.tweakHoldSeed = ""; set.bikeTweakHoldSeed = "";
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
    }
    static Snapshot Capture(RoweHeldTrick set, RoweIKSession session)
    {
        var hands = new Hand[2];
        for (int i = 0; i < 2; i++)
        {
            Vector3 palm = Palm(session, i), normal = NormalGrip(set, session, i);
            float distance = Vector3.Distance(palm, normal);
            bool eligible = set.authorBike && set.handsFollowBars && distance <= RoweBikeContacts.ReleaseDistance(set);
            bool helper = RoweBikeContacts.TryGripSnapTarget(set, session, i, out var helperTarget);
            if (helper && Vector3.Distance(helperTarget, normal) > .000001f)
                throw new InvalidOperationException("Snap helper returned a point other than the saved palm in CURRENT bar space.");
            hands[i] = new Hand { wrist = session.bones[i].position, palm = palm, normalGrip = normal,
                rotation = session.bones[i].rotation, distance = distance, independentEligible = eligible,
                helperEligible = helper, follows = RoweBikeContacts.ShouldFollow(set, session, i) };
        }
        return new Snapshot { hands = hands, body = session.bones.Take(5).Select(t => t.position).ToArray(),
            bodyRotations = session.bones.Take(5).Select(t => t.rotation).ToArray(),
            bike = session.bikeJoints.Select(t => t.position).ToArray(), bikeRotations = session.bikeJoints.Select(t => t.rotation).ToArray(),
            metadata = Meta(set) };
    }
    static void MovePalm(RoweIKSession session, int side, Vector3 target)
    {
        // A small bounded fixture solve uses achievable observed hand positions,
        // never assumes changing the target array moved the rendered palm.
        for (int pass = 0; pass < 5; pass++)
        {
            var error = target - Palm(session, side);
            if (error.magnitude < .0001f) return;
            session.MoveControl(side, session.positions[side] + error, session.rotations[side]);
        }
        if (Vector3.Distance(target, Palm(session, side)) > Tolerance)
            throw new InvalidOperationException("Synthetic hand offset was not reachable; do not treat this as a snap result.");
    }
    static void PrepareSynthetic(RoweIKPoseEditor editor, RoweHeldTrick set, Spec spec, Case result)
    {
        var session = Session(editor);
        // Use the real recipe's Riding pose as the reachable synthetic Hold,
        // retaining its actual Riding anchors rather than inventing new ones.
        session.Sample(set.poses, set.bikePoses, 0, 0);
        Set(editor, "pending", true); Set(editor, "pendingBike", true); Call(editor, "Commit"); Call(editor, "Sample");
        var oldNormal = Enumerable.Range(0, 2).Select(i => NormalGrip(set, session, i)).ToArray();
        if (spec.movedBars)
        {
            var bars = session.bikeJoints[2];
            Call(editor, "MoveBikeControl", 2, bars.position + new Vector3(.025f, .008f, .012f), bars.rotation * Quaternion.Euler(0, 14, 0));
            Call(editor, "Commit"); Call(editor, "Sample");
        }
        result.barAnchorMovement = Enumerable.Range(0, 2).Max(i => Vector3.Distance(oldNormal[i], NormalGrip(set, session, i)));
        result.currentBarsExercised = !spec.movedBars || result.barAnchorMovement > .01f;
        for (int i = 0; i < 2; i++)
        {
            float offset = i == 0 ? spec.leftOffset : spec.rightOffset;
            var towardElbow = (session.bones[5 + i].position - session.bones[i].position).normalized;
            MovePalm(session, i, NormalGrip(set, session, i) + towardElbow * offset);
        }
        Set(editor, "pending", true); Call(editor, "Commit"); Call(editor, "Sample");
        var observed = Capture(set, session);
        result.syntheticOffsetsReachable = Math.Abs(observed.hands[0].distance - spec.leftOffset) <= .003f
            && Math.Abs(observed.hands[1].distance - spec.rightOffset) <= .003f;
        if (spec.boundary != 0)
        {
            // Set the inclusive boundary from the same ACTUAL float distance
            // observed by the game helper, not a desired target before baking.
            set.contactReleaseDistance = observed.hands[0].distance - (spec.boundary < 0 ? .0005f : 0);
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
        }
    }
    static Stage Measure(string name, Snapshot before, RoweHeldTrick set, RoweIKSession session, Spec spec, bool undo = false)
    {
        var observed = Capture(set, session);
        var stage = new Stage { name = name, hands = new HandStage[2], metadataKept = before.metadata == observed.metadata };
        for (int i = 0; i < 2; i++)
        {
            var first = before.hands[i]; var next = observed.hands[i];
            bool selected = spec.hand < 0 || spec.hand == i;
            bool snap = selected && first.independentEligible && !undo;
            // A distant forced-follow hand may still pivot at its OLD palm;
            // autoRelease=false never makes it eligible for a NORMAL-grip snap.
            bool keepOldPalm = selected && first.follows && !snap && !undo;
            Vector3 expected = snap ? NormalGrip(set, session, i) : keepOldPalm ? first.palm : first.wrist;
            Vector3 actual = snap || keepOldPalm ? next.palm : next.wrist;
            stage.hands[i] = new HandStage { observed = next, expectedPivot = snap ? "Normal bar grip" : keepOldPalm ? "Existing unsnapped palm" : "Original wrist",
                expectedPivotError = Vector3.Distance(expected, actual), normalGripError = Vector3.Distance(next.palm, NormalGrip(set, session, i)),
                wristDrift = Vector3.Distance(first.wrist, next.wrist), palmDrift = Vector3.Distance(first.palm, next.palm),
                rotationChange = Quaternion.Angle(first.rotation, next.rotation) };
        }
        stage.pivotsPassed = stage.hands.All(h => h.expectedPivotError <= Tolerance);
        stage.unselectedPassed = spec.hand < 0 || stage.hands[1 - spec.hand].rotationChange <= .25f;
        stage.zeroStrengthKeepsRotation = spec.strength != 0 || stage.hands.All(h => h.rotationChange <= .25f);
        stage.feetError = Enumerable.Range(2, 2).Max(i => Vector3.Distance(before.body[i], observed.body[i]));
        stage.hipsError = Vector3.Distance(before.body[4], observed.body[4]);
        stage.bodyRotationError = Enumerable.Range(2, 3).Max(i => Quaternion.Angle(before.bodyRotations[i], observed.bodyRotations[i]));
        stage.bikeError = before.bike.Zip(observed.bike, Vector3.Distance).Max();
        stage.bikeRotationError = before.bikeRotations.Zip(observed.bikeRotations, Quaternion.Angle).Max();
        stage.bodyAndBikePassed = stage.feetError <= Tolerance && stage.hipsError <= Tolerance && stage.bodyRotationError <= .25f
            && stage.bikeError <= .00001f && stage.bikeRotationError <= .1f;
        return stage;
    }
    static Case RunCase(RoweHeldTrick source, string fixturePath, Spec spec)
    {
        var result = new Case { spec = spec };
        var stages = new List<Stage>(); RoweIKPoseEditor editor = null;
        try
        {
            var set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(fixturePath);
            ResetFixture(source, set, spec);
            result.copiedSourceExactly = Signature(set.poses) == Signature(source.poses) && Signature(set.bikePoses) == Signature(source.bikePoses)
                && Signature(set.tweakPose) == Signature(source.tweakPose) && Signature(set.bikeTweakPose) == Signature(source.bikeTweakPose);
            editor = RoweIKPoseEditor.OpenRecipe(set); Set(editor, "autoKey", false); Set(editor, "wristRelaxation", spec.strength);
            Call(editor, "BeginStudioPose", spec.pose);
            if (!spec.realPose) PrepareSynthetic(editor, set, spec, result);
            else result.syntheticOffsetsReachable = result.currentBarsExercised = true;
            var session = Session(editor); var before = Capture(set, session);
            result.before = before.hands; result.releaseDistance = RoweBikeContacts.ReleaseDistance(set);
            result.selectedSnap = Enumerable.Range(0, 2).Select(i => (spec.hand < 0 || spec.hand == i) && before.hands[i].independentEligible).ToArray();
            result.helperAgrees = before.hands.All(h => h.helperEligible == h.independentEligible);
            result.boundaryVerified = spec.boundary == 0 || (spec.boundary > 0
                ? before.hands[0].distance == result.releaseDistance && before.hands[0].helperEligible
                : before.hands[0].distance > result.releaseDistance && !before.hands[0].helperEligible);
            var targetClip = Get<AnimationClip>(editor, "clip"); string beforeClip = Signature(targetClip);
            int beforeUndoGroup = Undo.GetCurrentGroup();
            Call(editor, "ApplyWristRelaxation", spec.hand); result.firstActionKeyed = Get<bool>(editor, "pending");
            stages.Add(Measure("Immediate", before, set, session, spec));
            Call(editor, "Commit"); Call(editor, "Sample"); stages.Add(Measure("CommitAndSample", before, set, session, spec));
            string afterClip = Signature(targetClip); result.firstActionChanged = beforeClip != afterClip;
            if (result.firstActionKeyed)
            {
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Call(editor, "Sample");
                result.undoRestoresClip = Signature(targetClip) == beforeClip;
                stages.Add(Measure("Undo", before, set, session, spec, true));
                Undo.PerformRedo(); Call(editor, "Sample"); result.redoRestoresClip = Signature(targetClip) == afterClip;
                stages.Add(Measure("Redo", before, set, session, spec)); result.noOpKeepsUndo = true;
            }
            else
            {
                result.undoRestoresClip = result.redoRestoresClip = !result.firstActionChanged;
                result.noOpKeepsUndo = !result.firstActionChanged && beforeUndoGroup == Undo.GetCurrentGroup();
            }
            if (spec.repeat)
                for (int press = 2; press <= 3; press++)
                {
                    Call(editor, "ApplyWristRelaxation", spec.hand);
                    stages.Add(Measure("RepeatPress" + press, before, set, session, spec));
                    Call(editor, "Commit"); Call(editor, "Sample");
                    stages.Add(Measure("RepeatSaved" + press, before, set, session, spec));
                }
            string finalClip = Signature(targetClip), targetPath = AssetDatabase.GetAssetPath(targetClip);
            Call(editor, "Stop"); editor.Close(); editor = null;
            AssetDatabase.SaveAssetIfDirty(targetClip); AssetDatabase.SaveAssetIfDirty(set);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(fixturePath, ImportAssetOptions.ForceSynchronousImport);
            set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(fixturePath);
            editor = RoweIKPoseEditor.OpenRecipe(set); Set(editor, "autoKey", false); Call(editor, "BeginStudioPose", spec.pose);
            session = Session(editor); result.reopenKeepsClip = Signature(Get<AnimationClip>(editor, "clip")) == finalClip;
            stages.Add(Measure("Reopen", before, set, session, spec));
            result.passed = result.copiedSourceExactly && result.syntheticOffsetsReachable && result.currentBarsExercised
                && result.helperAgrees && result.boundaryVerified && result.undoRestoresClip && result.redoRestoresClip
                && result.noOpKeepsUndo && result.reopenKeepsClip
                && stages.All(s => s.pivotsPassed && s.unselectedPassed && s.zeroStrengthKeepsRotation && s.bodyAndBikePassed && s.metadataKept);
        }
        catch (Exception ex) { result.error = ex.ToString(); }
        finally
        {
            if (editor) { Call(editor, "StopWithoutCommit"); editor.Close(); }
            result.stages = stages.ToArray();
            if (stages.Count > 0)
            {
                result.maxPivotError = stages.SelectMany(s => s.hands).Max(h => h.expectedPivotError);
                result.maxSnappedGripError = stages.Where(s => s.name != "Undo").SelectMany(s => s.hands)
                    .Where(h => h.expectedPivot == "Normal bar grip").Select(h => h.normalGripError).DefaultIfEmpty(0).Max();
            }
        }
        Debug.Log("ROWE_WRIST_SNAP_CASE " + spec.name + " passed=" + result.passed + " maxPivot=" + result.maxPivotError + " maxSnap=" + result.maxSnappedGripError);
        return result;
    }
    static Spec[] Specifications() => new[] {
        new Spec { name = "RealHold50", realPose = true },
        new Spec { name = "RealHold100", realPose = true, strength = 1 },
        new Spec { name = "RealTweak50", realPose = true, pose = 2 },
        new Spec { name = "RealTweak100", realPose = true, pose = 2, strength = 1 },
        new Spec { name = "NearBothAndRepeat", repeat = true },
        new Spec { name = "FarBoth", leftOffset = .25f, rightOffset = .25f },
        new Spec { name = "NearLeftFarRight", rightOffset = .25f },
        new Spec { name = "SelectedLeftOnly", hand = 0 },
        new Spec { name = "SelectedRightOnly", hand = 1 },
        new Spec { name = "HandsFollowingOff", follow = false },
        new Spec { name = "AutoReleaseOffFar", autoRelease = false, leftOffset = .25f, rightOffset = .25f },
        new Spec { name = "AutoReleaseOffMixed", autoRelease = false, rightOffset = .25f },
        new Spec { name = "MovedAndRotatedCurrentBars", movedBars = true, leftOffset = .04f },
        new Spec { name = "Minimum2cmMixed", range = .02f, leftOffset = .012f, rightOffset = .07f },
        new Spec { name = "InclusiveBoundary", boundary = 1, rightOffset = .25f },
        new Spec { name = "JustOutsideBoundary", boundary = -1, rightOffset = .25f },
        new Spec { name = "ZeroAngleStillSnapsAndRepeats", strength = 0, repeat = true },
        new Spec { name = "AlreadyNormalZeroAngleNoOp", strength = 0, leftOffset = 0, rightOffset = 0, repeat = true }
    };
    static void CheckDirectSafety(RoweHeldTrick set, Animator rider, Animator bike, Report report)
    {
        using (var session = new RoweIKSession(rider, bike))
        {
            session.Sample(set.poses, set.bikePoses, 1, 1);
            // Cached IK targets may differ from the visible Humanoid pose.
            // A no-op must not silently replace those pending control values.
            session.positions[0] += new Vector3(.002f, 0, 0);
            session.rotations[0] *= Quaternion.Euler(12, 0, 0);
            var positions = session.positions.ToArray(); var rotations = session.rotations.ToArray();
            var bones = session.bones.Select(t => t.position).ToArray();
            var pose = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            string metadata = Meta(set);
            bool Restored() => pose.Changed() == 0 && Meta(set) == metadata
                && positions.Zip(session.positions, Vector3.Distance).All(d => d < .000001f)
                && rotations.Zip(session.rotations, Quaternion.Angle).All(d => d < .05f)
                && bones.Zip(session.bones.Select(t => t.position), Vector3.Distance).All(d => d < .000001f);
            bool changed = session.SnapAndRelaxWrists(set.ridingWristRotations, 0, -1, false, false, Palm(session, 0), Palm(session, 1));
            report.directNoOpRestoresTargets = !changed && Restored();
            changed = session.SnapAndRelaxWrists(set.ridingWristRotations, 0, 0, true, false, Palm(session, 0) + Vector3.up * 100, null);
            report.unreachableSnapRestoresTargets = !changed && Restored();
        }
    }
    public static void Run()
    {
        var report = new Report { sourceRecipe = SourceRecipe, toleranceMeters = Tolerance };
        Dictionary<string, string> sourceHashes = null; RowePairedWorkbench.Pose original = null;
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run only in the isolated Validation-* project.");
            var source = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(SourceRecipe);
            if (!source || !source.poses || !source.bikePoses || !source.tweakPose || !source.bikeTweakPose)
                throw new InvalidOperationException("Stage the real table_tweakn recipe and its four authoring clips first.");
            sourceHashes = Directory.GetFiles(Path.GetDirectoryName(SourceRecipe), "*", SearchOption.TopDirectoryOnly).ToDictionary(p => p, Hash);
            report.realSourceUsed = true;
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider = RowePairedWorkbench.FindRig("Human Temp"); var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            original = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            var fixture = RoweHeldTrickBuilder.Create("Wrist Snap table Validation", source.poses, 0, 1, rider, bike);
            RoweTrickStyles.Choose(fixture, RoweTrickStyle.HoldTweak); RoweBikeAnimation.Enable(fixture, rider, bike);
            report.fixtureRecipe = AssetDatabase.GetAssetPath(fixture);
            var specs = Specifications(); report.cases = specs.Select(s => RunCase(source, report.fixtureRecipe, s)).ToArray();
            report.nearExercised = report.cases.Any(c => c.selectedSnap != null && c.selectedSnap.All(v => v));
            report.farExercised = report.cases.Any(c => c.spec.name == "FarBoth" && c.before != null && c.before.All(h => !h.independentEligible));
            report.mixedExercised = report.cases.Any(c => c.spec.name == "NearLeftFarRight" && c.selectedSnap != null && c.selectedSnap[0] && !c.selectedSnap[1]);
            report.minimumAndBoundaryExercised = report.cases.Where(c => c.spec.range == .02f || c.spec.boundary != 0).All(c => c.passed);
            report.zeroAndRepeatExercised = report.cases.Where(c => c.spec.strength == 0 || c.spec.repeat).All(c => c.passed);
            CheckDirectSafety(AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(report.fixtureRecipe), rider, bike, report);
        }
        catch (Exception ex) { report.error = ex.ToString(); Debug.LogException(ex); }
        finally
        {
            report.sourceFilesUnchanged = sourceHashes != null && sourceHashes.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value);
            report.sceneRestored = original != null && original.Changed() == 0;
        }
        report.passed = report.error == null && report.realSourceUsed && report.sourceFilesUnchanged && report.sceneRestored
            && report.nearExercised && report.farExercised && report.mixedExercised && report.minimumAndBoundaryExercised && report.zeroAndRepeatExercised
            && report.directNoOpRestoresTargets && report.unreachableSnapRestoresTargets
            && report.cases != null && report.cases.Length == Specifications().Length && report.cases.All(c => c.passed);
        string path = Path.Combine(RoweAnimationWorkspace.Workspace, "studio-wrist-snap-validation.json");
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("ROWE_WRIST_SNAP_VALIDATION passed=" + report.passed + " cases=" + (report.cases?.Length ?? 0) + " report=" + path);
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
