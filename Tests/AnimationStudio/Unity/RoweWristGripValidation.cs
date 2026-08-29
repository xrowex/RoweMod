using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Low-level fixed-current-grip regression using copies of the paired table pose.
// Calls RelaxWrists directly, then exercises normal clip save / Undo / reopen.
// The UI's snap-to-normal-grip policy is separate coverage, not asserted here.
// Run only in Validation-*; creates one separate recipe and never edits its source.
public static class RoweWristGripValidation
{
    const string SourceRecipe = "Assets/CustomClips/HeldTricks/table_tweakn/table_tweakn.asset";
    const float GripTolerance = .001f;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly HumanBodyBones[][] Fingers = {
        new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal },
        new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal }
    };
    [Serializable] public class Hand
    {
        public Vector3 bone, target, barLocalBone, gripPoint, barLocalGripPoint, palmMidpoint, barLocalPalmMidpoint;
        public Vector3[] proximalBases;
        public Quaternion rotation;
        public float normalContactDistance, wristAlignmentError, gripApiError;
        public bool follows, allFingerBasesFound;
    }
    sealed class Snapshot
    {
        public Hand[] hands;
        public Vector3[] body, bike;
        public Quaternion[] bodyRotations, bikeRotations;
        public string metadata;
    }
    [Serializable] sealed class Metadata
    {
        public bool hands, feet, autoRelease, hipsPinned, carriesRider;
        public float distance;
        public Vector3[] contacts, palms;
        public Quaternion[] wrists;
    }
    [Serializable] public class HandStage
    {
        public Hand observed;
        public bool pinsPalm;
        public float boneWorldDrift, boneBarAnchorError, targetDrift, wristRotationChange,
            gripWorldDrift, gripBarAnchorError, pivotWorldDrift, pivotBarAnchorError,
            palmWorldDrift, palmBarAnchorError, proximalBaseMaxDrift, wristAlignmentChange;
    }
    [Serializable] public class Stage
    {
        public string name;
        public HandStage[] hands;
        public float feetDrift, hipsDrift, feetRotationDrift, hipsRotationDrift, bikeDrift, bikeRotationDrift;
        public bool metadataUnchanged, gripPassed, bodyAndBikePassed, unselectedHandPassed,
            followingHandsKept, wristAlignmentPassed;
    }
    [Serializable] public class Case
    {
        public string name, recipePath, error;
        public int pose, hand;
        public float strength, releaseDistanceMeters, maxHandDrift, maxGripDrift, maxPivotDrift, maxPalmDrift, maxWristAlignmentWorsening;
        public bool passed, releasedFixture, releaseRemainsFree, sourcePoseCopiedExactly,
            undoRestoresClip, redoRestoresClip, reopenKeepsClip, bothPalmLocks, minimumReleaseDistance,
            actionChanged, undoStepExercised, noOpLeavesUndoAlone;
        public Hand[] before;
        public Stage[] stages;
    }
    [Serializable] public class Report
    {
        public string coverage = "Low-level fixed-current-grip solver and clip persistence; not the UI snap policy.";
        public bool passed, usedRealTablePose, sourceFilesUnchanged, sceneRestored,
            attachedGripExercised, releasedWristExercised, bothPalmLocksExercised, minimumReleaseDistanceExercised;
        public string error, sourceRecipe, fixtureRecipe;
        public float toleranceMeters;
        public Case[] cases;
    }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    static string Signature(AnimationClip clip) => RoweTweakFromHold.Signature(clip);
    static string Meta(RoweHeldTrick set) => JsonUtility.ToJson(new Metadata {
        hands = set.handsFollowBars, feet = set.feetFollowPedals, autoRelease = set.autoReleaseContacts,
        hipsPinned = set.keepHipsInPlace, carriesRider = set.riderFollowsWholeBike,
        distance = set.contactReleaseDistance, contacts = set.ridingContactPoints,
        palms = set.ridingPalmContactPoints, wrists = set.ridingWristRotations
    });
    static void CopyClip(AnimationClip source, AnimationClip target)
    {
        string name = target.name;
        EditorUtility.CopySerialized(source, target); target.name = name;
        EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
    }
    static void ResetFixture(RoweHeldTrick source, RoweHeldTrick set)
    {
        CopyClip(source.poses, set.poses); CopyClip(source.bikePoses, set.bikePoses);
        CopyClip(source.tweakPose, set.tweakPose); CopyClip(source.bikeTweakPose, set.bikeTweakPose);
        set.game.style = RoweTrickStyle.HoldTweak; set.authorBike = true; set.useCustomTweak = true;
        set.handsFollowBars = source.handsFollowBars; set.feetFollowPedals = source.feetFollowPedals;
        set.autoReleaseContacts = source.autoReleaseContacts; set.contactReleaseDistance = source.contactReleaseDistance;
        set.keepHipsInPlace = source.keepHipsInPlace; set.riderFollowsWholeBike = source.riderFollowsWholeBike;
        set.ridingContactPoints = source.ridingContactPoints?.ToArray();
        set.ridingPalmContactPoints = source.ridingPalmContactPoints?.ToArray();
        set.ridingWristRotations = source.ridingWristRotations?.ToArray();
        // Treat the copied Tweak as explicitly authored. A Hold edit in this
        // test must not silently refresh or replace the copied source Tweak.
        set.tweakHoldSeed = ""; set.bikeTweakHoldSeed = "";
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
    }
    static Snapshot Capture(RoweHeldTrick set, RoweIKSession session)
    {
        var bars = session.bikeJoints[2]; var all = session.rider.GetComponentsInChildren<Transform>(true);
        var hands = new Hand[2];
        for (int side = 0; side < 2; side++)
        {
            var proximal = new Vector3[3]; bool found = true;
            for (int finger = 0; finger < 3; finger++)
            {
                var bone = session.rider.GetBoneTransform(Fingers[side][finger]);
                if (!bone)
                {
                    string name = "mixamorig:" + (side == 0 ? "Left" : "Right") + "Hand" + new[] { "Index", "Middle", "Ring" }[finger] + "1";
                    bone = all.FirstOrDefault(t => t.name == name);
                }
                found &= bone != null;
                proximal[finger] = bone ? bone.position : session.bones[side].position;
            }
            var wrist = session.bones[side].position;
            // Match the production palm pivot with independent bone observation;
            // the averaged knuckles below are additional visual diagnostics.
            var grip = Vector3.Lerp(wrist, proximal[1], .5f);
            var palm = Vector3.Lerp(wrist, (proximal[0] + proximal[1] + proximal[2]) / 3, .5f);
            hands[side] = new Hand {
                bone = wrist, target = session.positions[side], rotation = session.bones[side].rotation,
                barLocalBone = bars.InverseTransformPoint(wrist), gripPoint = grip,
                barLocalGripPoint = bars.InverseTransformPoint(grip), palmMidpoint = palm,
                barLocalPalmMidpoint = bars.InverseTransformPoint(palm), proximalBases = proximal,
                allFingerBasesFound = found, follows = RoweBikeContacts.ShouldFollow(set, session, side),
                normalContactDistance = RoweBikeContacts.Distance(set, session, side),
                wristAlignmentError = Quaternion.Angle(session.bones[side].rotation,
                    session.bones[5 + side].rotation * set.ridingWristRotations[side]),
                gripApiError = Vector3.Distance(grip, session.GripPoint(side))
            };
        }
        return new Snapshot {
            hands = hands, body = session.bones.Take(5).Select(b => b.position).ToArray(),
            bodyRotations = session.bones.Take(5).Select(b => b.rotation).ToArray(),
            bike = session.bikeJoints.Select(b => b.position).ToArray(),
            bikeRotations = session.bikeJoints.Select(b => b.rotation).ToArray(), metadata = Meta(set)
        };
    }
    static Stage Measure(string name, Snapshot before, RoweHeldTrick set, RoweIKSession session, int selectedHand)
    {
        var observed = Capture(set, session); var bars = session.bikeJoints[2];
        var stage = new Stage { name = name, metadataUnchanged = before.metadata == observed.metadata, hands = new HandStage[2] };
        for (int side = 0; side < 2; side++)
        {
            var first = before.hands[side]; var next = observed.hands[side];
            stage.hands[side] = new HandStage {
                observed = next, pinsPalm = first.follows && (selectedHand < 0 || selectedHand == side),
                boneWorldDrift = Vector3.Distance(first.bone, next.bone),
                boneBarAnchorError = Vector3.Distance(bars.TransformPoint(first.barLocalBone), next.bone),
                targetDrift = Vector3.Distance(first.target, next.target), wristRotationChange = Quaternion.Angle(first.rotation, next.rotation),
                gripWorldDrift = Vector3.Distance(first.gripPoint, next.gripPoint),
                gripBarAnchorError = Vector3.Distance(bars.TransformPoint(first.barLocalGripPoint), next.gripPoint),
                palmWorldDrift = Vector3.Distance(first.palmMidpoint, next.palmMidpoint),
                palmBarAnchorError = Vector3.Distance(bars.TransformPoint(first.barLocalPalmMidpoint), next.palmMidpoint),
                proximalBaseMaxDrift = first.proximalBases.Zip(next.proximalBases, Vector3.Distance).Max(),
                wristAlignmentChange = next.wristAlignmentError - first.wristAlignmentError
            };
            var measured = stage.hands[side];
            // Planted palms must stay on the same point, so their wrist SHOULD
            // translate as it rotates. Free and unselected hands keep the wrist.
            measured.pivotWorldDrift = measured.pinsPalm ? measured.gripWorldDrift : measured.boneWorldDrift;
            measured.pivotBarAnchorError = measured.pinsPalm ? measured.gripBarAnchorError : measured.boneBarAnchorError;
        }
        stage.feetDrift = Enumerable.Range(2, 2).Max(i => Vector3.Distance(before.body[i], observed.body[i]));
        stage.hipsDrift = Vector3.Distance(before.body[4], observed.body[4]);
        stage.feetRotationDrift = Enumerable.Range(2, 2).Max(i => Quaternion.Angle(before.bodyRotations[i], observed.bodyRotations[i]));
        stage.hipsRotationDrift = Quaternion.Angle(before.bodyRotations[4], observed.bodyRotations[4]);
        stage.bikeDrift = before.bike.Zip(observed.bike, Vector3.Distance).Max();
        stage.bikeRotationDrift = before.bikeRotations.Zip(observed.bikeRotations, Quaternion.Angle).Max();
        stage.gripPassed = stage.hands.All(h => h.pivotWorldDrift <= GripTolerance && h.pivotBarAnchorError <= GripTolerance
            && h.observed.allFingerBasesFound && h.observed.gripApiError < .00001f);
        stage.unselectedHandPassed = selectedHand < 0 || stage.hands[1 - selectedHand].wristRotationChange <= .25f;
        stage.followingHandsKept = Enumerable.Range(0, 2).All(i => !before.hands[i].follows || observed.hands[i].follows);
        stage.wristAlignmentPassed = name == "Undo" || Enumerable.Range(0, 2)
            .Where(i => selectedHand < 0 || selectedHand == i).All(i => stage.hands[i].wristAlignmentChange <= .25f);
        stage.bodyAndBikePassed = stage.feetDrift <= GripTolerance && stage.hipsDrift <= GripTolerance
            && stage.feetRotationDrift <= .25f && stage.hipsRotationDrift <= .25f
            && stage.bikeDrift <= .00001f && stage.bikeRotationDrift <= .1f;
        return stage;
    }
    static Case RunCase(RoweHeldTrick source, string fixturePath, int pose, float strength, int hand, bool released,
        bool bothPalmLocks = false, bool minimumReleaseDistance = false)
    {
        var result = new Case { name = (pose == 1 ? "Hold" : "Tweak") + "-" + (strength * 100) + "-" + (hand < 0 ? "Both" : hand == 0 ? "Left" : "Right")
                + (released ? "-ReleasedLeft" : "") + (bothPalmLocks ? "-BothPalmLocks" : "")
                + (minimumReleaseDistance ? "-Minimum2cm" : ""),
            recipePath = fixturePath, pose = pose, strength = strength, hand = hand,
            bothPalmLocks = bothPalmLocks, minimumReleaseDistance = minimumReleaseDistance };
        RoweIKPoseEditor editor = null;
        var stages = new List<Stage>();
        try
        {
            var set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(fixturePath);
            ResetFixture(source, set);
            if (bothPalmLocks)
            {
                // The saved real table's right hand is >15cm from its Riding
                // anchor. Exercise BOTH lock paths in this separate copy only.
                set.autoReleaseContacts = false;
                EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
            }
            result.sourcePoseCopiedExactly = Signature(set.poses) == Signature(source.poses)
                && Signature(set.bikePoses) == Signature(source.bikePoses)
                && Signature(set.tweakPose) == Signature(source.tweakPose)
                && Signature(set.bikeTweakPose) == Signature(source.bikeTweakPose);
            editor = RoweIKPoseEditor.OpenRecipe(set); Set(editor, "autoKey", false); Set(editor, "wristRelaxation", strength);
            Call(editor, "BeginStudioPose", pose); var session = Get<RoweIKSession>(editor, "session");
            if (minimumReleaseDistance)
            {
                // Isolated boundary fixture: both current palms define its
                // normal bar grips. Wrist compensation must not release them
                // at the minimum distance, including after save and reopen.
                set.autoReleaseContacts = true;
                set.contactReleaseDistance = .02f;
                set.ridingPalmContactPoints = Enumerable.Range(0, 2)
                    .Select(i => session.bikeJoints[2].InverseTransformPoint(session.GripPoint(i))).ToArray();
                EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
            }
            result.releaseDistanceMeters = RoweBikeContacts.ReleaseDistance(set);
            if (released)
            {
                var towardElbow = (session.bones[5].position - session.bones[0].position).normalized;
                session.MoveControl(0, session.positions[0] + towardElbow * .25f, session.rotations[0]);
                Set(editor, "pending", true); Call(editor, "Commit"); Call(editor, "Sample");
                result.releasedFixture = !RoweBikeContacts.ShouldFollow(set, session, 0);
            }
            else result.releasedFixture = true;
            var before = Capture(set, session); result.before = before.hands;
            var targetClip = Get<AnimationClip>(editor, "clip"); string beforeClip = Signature(targetClip);
            int beforeUndoGroup = Undo.GetCurrentGroup();
            // Preserve the explicitly chosen CURRENT grips at the solver level.
            // The UI now snaps nearby palms to their normal bar contacts instead.
            if (session.RelaxWrists(set.ridingWristRotations, strength, hand,
                before.hands[0].follows, before.hands[1].follows))
                Set(editor, "pending", true);
            result.undoStepExercised = Get<bool>(editor, "pending");
            stages.Add(Measure("Immediate", before, set, session, hand));
            Call(editor, "Commit"); Call(editor, "Sample");
            stages.Add(Measure("CommitAndSample", before, set, session, hand));
            string afterClip = Signature(targetClip);
            result.actionChanged = beforeClip != afterClip;
            if (result.undoStepExercised)
            {
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Call(editor, "Sample");
                result.undoRestoresClip = Signature(targetClip) == beforeClip;
                stages.Add(Measure("Undo", before, set, session, hand));
                Undo.PerformRedo(); Call(editor, "Sample");
                result.redoRestoresClip = Signature(targetClip) == afterClip;
                stages.Add(Measure("Redo", before, set, session, hand));
                result.noOpLeavesUndoAlone = true;
            }
            else
            {
                // A safe backoff may legitimately leave an extreme pose alone.
                // There must be no key/Undo item; do not undo the prior fixture edit.
                result.noOpLeavesUndoAlone = !result.actionChanged && beforeUndoGroup == Undo.GetCurrentGroup();
                result.undoRestoresClip = result.redoRestoresClip = !result.actionChanged;
                stages.Add(Measure("NoOpNoUndoItem", before, set, session, hand));
            }
            string targetPath = AssetDatabase.GetAssetPath(targetClip);
            Call(editor, "Stop"); editor.Close(); editor = null;
            AssetDatabase.SaveAssetIfDirty(targetClip); AssetDatabase.SaveAssetIfDirty(set);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(fixturePath, ImportAssetOptions.ForceSynchronousImport);
            set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(fixturePath);
            editor = RoweIKPoseEditor.OpenRecipe(set); Set(editor, "autoKey", false); Call(editor, "BeginStudioPose", pose);
            session = Get<RoweIKSession>(editor, "session");
            result.reopenKeepsClip = Signature(Get<AnimationClip>(editor, "clip")) == afterClip;
            stages.Add(Measure("Reopen", before, set, session, hand));
            result.releaseRemainsFree = !released || stages.All(s => !s.hands[0].observed.follows);
            result.passed = result.sourcePoseCopiedExactly && result.releasedFixture && result.releaseRemainsFree
                && result.undoRestoresClip && result.redoRestoresClip && result.reopenKeepsClip
                && result.noOpLeavesUndoAlone
                && stages.All(s => s.gripPassed && s.bodyAndBikePassed && s.unselectedHandPassed
                    && s.metadataUnchanged && s.followingHandsKept && s.wristAlignmentPassed);
        }
        catch (Exception ex) { result.error = ex.ToString(); }
        finally
        {
            if (editor) { Call(editor, "StopWithoutCommit"); editor.Close(); }
            result.stages = stages.ToArray();
            if (stages.Count > 0)
            {
                result.maxHandDrift = stages.SelectMany(s => s.hands).Max(h => Math.Max(h.boneWorldDrift, h.boneBarAnchorError));
                result.maxGripDrift = stages.SelectMany(s => s.hands).Max(h => Math.Max(h.gripWorldDrift, h.gripBarAnchorError));
                result.maxPivotDrift = stages.SelectMany(s => s.hands).Max(h => Math.Max(h.pivotWorldDrift, h.pivotBarAnchorError));
                result.maxPalmDrift = stages.SelectMany(s => s.hands).Max(h => h.palmBarAnchorError);
                result.maxWristAlignmentWorsening = stages.SelectMany(s => s.hands).Max(h => Math.Max(0, h.wristAlignmentChange));
            }
        }
        Debug.Log("ROWE_WRIST_GRIP_CASE " + result.name + " passed=" + result.passed + " maxPivot=" + result.maxPivotDrift
            + " maxWrist=" + result.maxHandDrift + " maxPalm=" + result.maxPalmDrift);
        return result;
    }
    public static void Run()
    {
        var report = new Report { sourceRecipe = SourceRecipe, toleranceMeters = GripTolerance };
        Dictionary<string, string> sourceHashes = null;
        RowePairedWorkbench.Pose original = null;
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run only in the isolated Validation-* project.");
            var source = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(SourceRecipe);
            if (!source || !source.poses || !source.bikePoses || !source.tweakPose || !source.bikeTweakPose)
                throw new InvalidOperationException("Stage the real table_tweakn recipe and its four authoring clips into validation first; this test will not substitute an unrelated pose.");
            sourceHashes = Directory.GetFiles(Path.GetDirectoryName(SourceRecipe), "*", SearchOption.TopDirectoryOnly).ToDictionary(p => p, Hash);
            report.usedRealTablePose = true;
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider = RowePairedWorkbench.FindRig("Human Temp"); var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            original = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            var fixture = RoweHeldTrickBuilder.Create("Wrist Grip table Validation", source.poses, 0, 1, rider, bike);
            RoweTrickStyles.Choose(fixture, RoweTrickStyle.HoldTweak); RoweBikeAnimation.Enable(fixture, rider, bike);
            report.fixtureRecipe = AssetDatabase.GetAssetPath(fixture);
            var cases = new List<Case>();
            foreach (int pose in new[] { 1, 2 })
                foreach (float strength in new[] { .5f, 1f })
                    foreach (int hand in new[] { -1, 0, 1 }) cases.Add(RunCase(source, report.fixtureRecipe, pose, strength, hand, false));
            foreach (float strength in new[] { .5f, 1f }) cases.Add(RunCase(source, report.fixtureRecipe, 1, strength, -1, true));
            foreach (int pose in new[] { 1, 2 })
                foreach (float strength in new[] { .5f, 1f }) cases.Add(RunCase(source, report.fixtureRecipe, pose, strength, -1, false, true));
            foreach (int pose in new[] { 1, 2 }) cases.Add(RunCase(source, report.fixtureRecipe, pose, 1, -1, false, false, true));
            report.cases = cases.ToArray();
            report.attachedGripExercised = cases.Any(c => c.stages.Any(s => s.hands.Any(h => h.pinsPalm)));
            report.releasedWristExercised = cases.Where(c => c.name.EndsWith("ReleasedLeft", StringComparison.Ordinal))
                .All(c => c.releasedFixture && c.releaseRemainsFree && c.stages.Any(s => !s.hands[0].pinsPalm));
            report.bothPalmLocksExercised = cases.Count(c => c.bothPalmLocks) == 4 && cases.Where(c => c.bothPalmLocks)
                .All(c => c.before != null && c.before.All(h => h.follows) && c.stages.Any(s => s.hands.All(h => h.pinsPalm)));
            report.minimumReleaseDistanceExercised = cases.Count(c => c.minimumReleaseDistance) == 2
                && cases.Where(c => c.minimumReleaseDistance).All(c => Math.Abs(c.releaseDistanceMeters - .02f) < .000001f
                    && c.before != null && c.before.All(h => h.follows && h.normalContactDistance < .000001f)
                    && c.stages.Any(s => s.name == "Reopen" && s.followingHandsKept));
        }
        catch (Exception ex) { report.error = ex.ToString(); Debug.LogException(ex); }
        finally
        {
            report.sourceFilesUnchanged = sourceHashes != null && sourceHashes.All(kv => File.Exists(kv.Key) && Hash(kv.Key) == kv.Value);
            report.sceneRestored = original != null && original.Changed() == 0;
        }
        report.passed = report.error == null && report.usedRealTablePose && report.sourceFilesUnchanged && report.sceneRestored
            && report.attachedGripExercised && report.releasedWristExercised && report.bothPalmLocksExercised
            && report.minimumReleaseDistanceExercised
            && report.cases != null && report.cases.Length == 20 && report.cases.All(c => c.passed);
        string output = Path.Combine(RoweAnimationWorkspace.Workspace, "studio-wrist-grip-validation.json");
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log("ROWE_WRIST_GRIP_VALIDATION passed=" + report.passed + " cases=" + (report.cases?.Length ?? 0) + " report=" + output);
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
