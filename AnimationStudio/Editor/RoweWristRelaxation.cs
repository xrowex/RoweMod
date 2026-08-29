using System;
using UnityEditor;
using UnityEngine;

public static class RoweWristRelaxation
{
    public static bool Valid(Quaternion value)
    {
        float lengthSquared = Quaternion.Dot(value, value);
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z) && float.IsFinite(value.w)
            && float.IsFinite(lengthSquared) && lengthSquared > .000001f;
    }

    public static bool HasReference(RoweHeldTrick set)
        => set && set.ridingWristRotations != null && set.ridingWristRotations.Length == 2
            && Valid(set.ridingWristRotations[0]) && Valid(set.ridingWristRotations[1]);

    // Capture each hand relative to its own forearm. World Euler zero is not
    // a neutral wrist, and the two sides of a Humanoid rig need different bases.
    // Caller samples the requested editing pose immediately after this method.
    public static void EnsureReference(RoweHeldTrick set, RoweIKSession session)
    {
        if (!set || HasReference(set)) return;
        if (!set.poses || session == null) throw new InvalidOperationException("Start posing a saved trick to set its normal wrist alignment.");
        session.Sample(set.poses, null, 0);
        var rotations = new Quaternion[2];
        for (int i = 0; i < rotations.Length; i++)
            rotations[i] = (Quaternion.Inverse(session.bones[5 + i].rotation) * session.bones[i].rotation).normalized;
        if (!Valid(rotations[0]) || !Valid(rotations[1])) throw new InvalidOperationException("The Riding pose has an invalid wrist alignment.");
        // Derived metadata must not add Undo entries during an UndoRedo sample.
        set.ridingWristRotations = rotations;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssetIfDirty(set);
    }
}

public sealed partial class RoweIKPoseEditor
{
    [SerializeField] float wristRelaxation = .5f;
    bool EditingRecipePose => recipe && clip && (clip == recipe.poses || clip == recipe.tweakPose || PairedMovement);

    void ApplyWristRelaxation(int hand)
    {
        if (!EditingRecipePose || session == null || !RoweWristRelaxation.HasReference(recipe))
            throw new InvalidOperationException("Start posing or reload the saved pose before relaxing wrists.");
        // Classify both hands against the same pre-edit pose. The first snap
        // must not change whether the second hand was originally in range.
        Vector3? leftGrip = null, rightGrip = null;
        if (CurrentBikeWorking)
        {
            if (hand != 1 && RoweBikeContacts.TryGripSnapTarget(recipe, session, 0, out var left)) leftGrip = left;
            if (hand != 0 && RoweBikeContacts.TryGripSnapTarget(recipe, session, 1, out var right)) rightGrip = right;
        }
        if (!session.SnapAndRelaxWrists(recipe.ridingWristRotations, wristRelaxation, hand,
            CurrentBikeWorking && RoweBikeContacts.ShouldFollow(recipe, session, 0),
            CurrentBikeWorking && RoweBikeContacts.ShouldFollow(recipe, session, 1), leftGrip, rightGrip))
        {
            status = "No pose change. The hand is already seated, outside the snap range, or cannot reach/relax further safely. Try adjusting the elbow or Relax amount.";
            return;
        }
        pending = true;
        if (autoKey) Commit();
        int snapCount = (leftGrip.HasValue ? 1 : 0) + (rightGrip.HasValue ? 1 : 0);
        status = (hand < 0 ? "Both wrists" : RoweIKSession.Labels[hand]) + " adjusted. "
            + (snapCount > 0 ? snapCount + " nearby hand(s) seated on the bars. " : "No hands were snapped to the bars. ")
            + "Hands outside the release range are never snapped. Ctrl+Z undoes a saved pose.";
    }

    void DrawWristControls()
    {
        if (!EditingRecipePose) return;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("WRIST COMFORT", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(session == null || playing || studioRunning))
        {
            wristRelaxation = EditorGUILayout.Slider("Relax amount (%)", wristRelaxation * 100, 0, 100) / 100;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Relax both wrists", GUILayout.Height(28))) Run(() => ApplyWristRelaxation(-1));
                if (!bikeSelected && selected < 2 && GUILayout.Button("Relax " + RoweIKSession.Labels[selected].ToLowerInvariant(), GUILayout.Height(28)))
                    Run(() => ApplyWristRelaxation(selected));
            }
        }
        EditorGUILayout.HelpBox("Each click snaps selected hands within the Release distance back to their normal grip on the current bars, then relaxes the wrists around that grip. Hands outside the range are never snapped, even with auto-release off. Hands follow bars must be enabled. At 0%, nearby hands still snap without requesting a wrist-angle change. This runs only when clicked.", MessageType.None);
    }
}
