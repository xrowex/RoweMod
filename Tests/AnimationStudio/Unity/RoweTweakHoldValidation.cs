using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run only in the isolated workbench validation project. Creates its own recipe;
// never opens, changes or resets an existing creator's held trick.
public static class RoweTweakHoldValidation
{
    [Serializable] public class Report
    {
        public bool passed, earlySeeds, heldChangedAfterSeeding, riderStartsAtCoreHold,
            bikeStartsAtCoreHold, selectedTweakNotHold, sampledPairMatchesHold,
            riderEditDetected, independentTracks, bikeEditDetected, authoredTweaksPreserved,
            legacyPreserved, restartCopiesBoth, restartKeepsGuids, restartLeavesHoldAlone,
            restartUndo, restartRedo, persists, reopenedSeedStillFollowsHold,
            transitionStartsAtHold, transitionEndsAtTweak, noExportedLoop,
            sourcePreserved, sceneRestored;
        public string error, recipePath;
    }

    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static T Get<T>(object target, string name)
        => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void EnterTweak(RoweIKPoseEditor editor)
    {
        editor.GetType().GetField("studioStep", Private).SetValue(editor, 2);
        Call(editor, "BeginStudioPose", 2);
    }
    static string Signature(AnimationClip clip) => RoweTweakFromHold.Signature(clip);
    static string GuidOf(AnimationClip clip)
        => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
    static EditorCurveBinding RiderPosition(string axis)
        => EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + axis);
    static EditorCurveBinding BikePosition(string axis)
        => EditorCurveBinding.FloatCurve(RoweBikeAnimation.Paths[2], typeof(Transform), "m_LocalPosition." + axis);

    // Directly edits a saved key, like using Unity's Animation window. This also
    // proves the seed protection does not depend on our GUI setting an edited flag.
    static void Nudge(AnimationClip clip, EditorCurveBinding binding, float time, float amount)
    {
        var curve = AnimationUtility.GetEditorCurve(clip, binding);
        if (curve == null) throw new InvalidOperationException("Expected curve is missing: " + binding.propertyName);
        int index = Array.FindIndex(curve.keys, k => Mathf.Abs(k.time - time) < .0001f);
        if (index < 0) throw new InvalidOperationException("Expected pose key is missing at " + time);
        var key = curve[index];
        key.value += amount;
        curve.MoveKey(index, key);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
    }

    static string Key(EditorCurveBinding binding)
        => binding.path + "|" + binding.type.FullName + "|" + binding.propertyName;

    // Compare sampled values rather than helper hashes: a refreshed signature is
    // not proof that the new pose actually came from Held.
    static bool SamePose(AnimationClip a, float timeA, AnimationClip b, float timeB)
    {
        if (!a || !b) return false;
        var left = AnimationUtility.GetCurveBindings(a);
        var right = AnimationUtility.GetCurveBindings(b).ToDictionary(Key);
        if (left.Length == 0 || left.Length != right.Count) return false;
        foreach (var binding in left)
        {
            if (!right.TryGetValue(Key(binding), out var other)) return false;
            float x = AnimationUtility.GetEditorCurve(a, binding).Evaluate(timeA);
            float y = AnimationUtility.GetEditorCurve(b, other).Evaluate(timeB);
            if (!float.IsFinite(x) || !float.IsFinite(y) || Mathf.Abs(x - y) > .00001f) return false;
        }
        return true;
    }

    public static void Run()
    {
        var r = new Report();
        RoweIKPoseEditor editor = null;
        try
        {
            if (!Application.isBatchMode || !Path.GetFileName(Directory.GetParent(Application.dataPath).FullName).StartsWith("Validation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Run this test only in a separate Validation-* project in batch mode.");
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider = RowePairedWorkbench.FindRig("Human Temp");
            var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            var original = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            const string sourcePath = "Assets/CustomClips/RoweMod_Custom_My Trick 3.anim";
            string sourceBefore = File.ReadAllText(sourcePath);
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
            var set = RoweHeldTrickBuilder.Create("Tweak Core Hold Validation", source, 0, 0, rider, bike);
            r.recipePath = AssetDatabase.GetAssetPath(set);

            // The original bug: selecting the style creates Tweak before the user
            // finishes editing either core Held pose.
            RoweTrickStyles.Choose(set, RoweTrickStyle.HoldTweak);
            RoweBikeAnimation.Enable(set, rider, bike);
            r.earlySeeds = set.tweakPose && set.bikeTweakPose
                && !string.IsNullOrEmpty(set.tweakHoldSeed) && !string.IsNullOrEmpty(set.bikeTweakHoldSeed)
                && set.tweakHoldSeed == Signature(set.tweakPose) && set.bikeTweakHoldSeed == Signature(set.bikeTweakPose);
            Nudge(set.poses, RiderPosition("x"), 1, .05f);
            Nudge(set.bikePoses, BikePosition("y"), 1, .04f);
            r.heldChangedAfterSeeding = !SamePose(set.poses, 1, set.tweakPose, 0)
                && !SamePose(set.bikePoses, 1, set.bikeTweakPose, 0);
            editor = RoweIKPoseEditor.OpenRecipe(set);
            Call(editor, "BeginStudioPose", 1);
            var heldScene = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            EnterTweak(editor);
            r.riderStartsAtCoreHold = SamePose(set.poses, 1, set.tweakPose, 0);
            r.bikeStartsAtCoreHold = SamePose(set.bikePoses, 1, set.bikeTweakPose, 0);
            r.selectedTweakNotHold = Get<AnimationClip>(editor, "clip") == set.tweakPose
                && Mathf.Abs(Get<float>(editor, "seconds")) < .0001f;
            r.sampledPairMatchesHold = heldScene.Changed() == 0;
            Call(editor, "Stop");

            // Rider and bike own separate seeds. Editing one must not freeze the
            // other track's untouched starter pose.
            Nudge(set.tweakPose, RiderPosition("z"), 0, .025f);
            string editedRider = Signature(set.tweakPose);
            r.riderEditDetected = editedRider != set.tweakHoldSeed;
            Nudge(set.poses, RiderPosition("x"), 1, .03f);
            Nudge(set.bikePoses, BikePosition("y"), 1, .02f);
            RoweTweakFromHold.Synchronize(set);
            r.independentTracks = Signature(set.tweakPose) == editedRider
                && SamePose(set.bikePoses, 1, set.bikeTweakPose, 0);

            Nudge(set.bikeTweakPose, BikePosition("x"), 0, .045f);
            string editedBike = Signature(set.bikeTweakPose);
            r.bikeEditDetected = editedBike != set.bikeTweakHoldSeed;
            Nudge(set.poses, RiderPosition("y"), 1, .02f);
            Nudge(set.bikePoses, BikePosition("z"), 1, .03f);
            RoweHeldTrickBuilder.Rebuild(set);
            RoweTrickStyles.Choose(set, RoweTrickStyle.Hold);
            RoweTrickStyles.Choose(set, RoweTrickStyle.HoldTweak);
            EnterTweak(editor);
            r.authoredTweaksPreserved = Signature(set.tweakPose) == editedRider
                && Signature(set.bikeTweakPose) == editedBike;
            Call(editor, "Stop");

            // Empty seeds represent pre-upgrade recipes: ownership cannot be
            // inferred, so their existing poses must remain untouched.
            set.tweakHoldSeed = "";
            set.bikeTweakHoldSeed = "";
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            Nudge(set.poses, RiderPosition("z"), 1, .01f);
            Nudge(set.bikePoses, BikePosition("y"), 1, .01f);
            RoweTweakFromHold.Synchronize(set);
            RoweHeldTrickBuilder.Rebuild(set);
            r.legacyPreserved = Signature(set.tweakPose) == editedRider && Signature(set.bikeTweakPose) == editedBike
                && string.IsNullOrEmpty(set.tweakHoldSeed) && string.IsNullOrEmpty(set.bikeTweakHoldSeed);

            string riderHold = Signature(set.poses), bikeHold = Signature(set.bikePoses);
            string riderGuid = GuidOf(set.tweakPose), bikeGuid = GuidOf(set.bikeTweakPose);
            RoweTweakFromHold.Restart(set);
            Undo.FlushUndoRecordObjects();
            r.restartCopiesBoth = SamePose(set.poses, 1, set.tweakPose, 0) && SamePose(set.bikePoses, 1, set.bikeTweakPose, 0);
            r.restartKeepsGuids = GuidOf(set.tweakPose) == riderGuid && GuidOf(set.bikeTweakPose) == bikeGuid;
            r.restartLeavesHoldAlone = Signature(set.poses) == riderHold && Signature(set.bikePoses) == bikeHold;
            Undo.PerformUndo();
            r.restartUndo = Signature(set.tweakPose) == editedRider && Signature(set.bikeTweakPose) == editedBike
                && string.IsNullOrEmpty(set.tweakHoldSeed) && string.IsNullOrEmpty(set.bikeTweakHoldSeed)
                && Signature(set.poses) == riderHold && Signature(set.bikePoses) == bikeHold;
            Undo.PerformRedo();
            r.restartRedo = SamePose(set.poses, 1, set.tweakPose, 0) && SamePose(set.bikePoses, 1, set.bikeTweakPose, 0)
                && Signature(set.poses) == riderHold && Signature(set.bikePoses) == bikeHold;

            // Save and reimport both track assets and the recipe, then prove the
            // stored signatures still recognize unedited Tweak after reopening.
            editor.Close(); editor = null;
            string riderSeed = set.tweakHoldSeed, bikeSeed = set.bikeTweakHoldSeed;
            AssetDatabase.SaveAssets();
            foreach (var clip in new[] { set.poses, set.bikePoses, set.tweakPose, set.bikeTweakPose })
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(clip), ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(r.recipePath, ImportAssetOptions.ForceSynchronousImport);
            set = AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(r.recipePath);
            r.persists = !string.IsNullOrEmpty(riderSeed) && !string.IsNullOrEmpty(bikeSeed)
                && set.tweakHoldSeed == riderSeed && set.bikeTweakHoldSeed == bikeSeed
                && riderSeed == Signature(set.tweakPose) && bikeSeed == Signature(set.bikeTweakPose)
                && GuidOf(set.tweakPose) == riderGuid && GuidOf(set.bikeTweakPose) == bikeGuid;
            Nudge(set.poses, RiderPosition("x"), 1, .008f);
            Nudge(set.bikePoses, BikePosition("z"), 1, .012f);
            editor = RoweIKPoseEditor.OpenRecipe(set);
            EnterTweak(editor);
            r.reopenedSeedStillFollowsHold = SamePose(set.poses, 1, set.tweakPose, 0)
                && SamePose(set.bikePoses, 1, set.bikeTweakPose, 0);
            Call(editor, "Stop");
            RoweHeldTrickBuilder.Rebuild(set);
            r.transitionStartsAtHold = SamePose(set.poses, 1, set.tweak, 0)
                && SamePose(set.bikePoses, 1, set.bikePhases[2], 0);
            r.transitionEndsAtTweak = SamePose(set.tweakPose, 0, set.tweak, set.tweak.length)
                && SamePose(set.bikeTweakPose, 0, set.bikePhases[2], set.bikePhases[2].length);
            var tracks = RoweStudioGameExport.Resolve(set);
            r.noExportedLoop = !tracks[0][1] && !tracks[1][1];
            r.sourcePreserved = File.ReadAllText(sourcePath) == sourceBefore;
            r.sceneRestored = original.Changed() == 0;
            r.passed = new[] {
                r.earlySeeds, r.heldChangedAfterSeeding, r.riderStartsAtCoreHold, r.bikeStartsAtCoreHold,
                r.selectedTweakNotHold, r.sampledPairMatchesHold, r.riderEditDetected, r.independentTracks,
                r.bikeEditDetected, r.authoredTweaksPreserved, r.legacyPreserved, r.restartCopiesBoth,
                r.restartKeepsGuids, r.restartLeavesHoldAlone, r.restartUndo, r.restartRedo, r.persists,
                r.reopenedSeedStillFollowsHold, r.transitionStartsAtHold, r.transitionEndsAtTweak,
                r.noExportedLoop, r.sourcePreserved, r.sceneRestored
            }.All(value => value);
            if (!r.passed) throw new InvalidOperationException("One or more Tweak/core Hold checks failed.");
        }
        catch (Exception ex) { r.error = ex.ToString(); Debug.LogException(ex); }
        finally { if (editor) editor.Close(); }
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "studio-tweak-hold-validation.json"), JsonUtility.ToJson(r, true));
        Debug.Log("ROWE_TWEAK_HOLD_VALIDATION " + JsonUtility.ToJson(r));
        EditorApplication.Exit(r.passed ? 0 : 1);
    }
}
