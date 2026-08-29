using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Runs only when opening/saving/rebuilding a pose, never in an editor tick or game hook.
// A seed signature proves that a Tweak is still the untouched initial copy.
// Missing signatures (older recipes) and edited curves are preserved, never guessed at.
public static class RoweTweakFromHold
{
    public static string Signature(AnimationClip clip)
    {
        if (!clip) return "";
        var text = new StringBuilder();
        foreach (var b in AnimationUtility.GetCurveBindings(clip).OrderBy(b => b.path, StringComparer.Ordinal).ThenBy(b => b.propertyName, StringComparer.Ordinal))
        {
            text.Append(b.path).Append('|').Append(b.type.FullName).Append('|').Append(b.propertyName).Append(':');
            foreach (var k in AnimationUtility.GetEditorCurve(clip, b).keys)
            {
                foreach (float value in new[] { k.time, k.value, k.inTangent, k.outTangent, k.inWeight, k.outWeight })
                    text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                text.Append((int)k.weightedMode).Append(';');
            }
        }
        // Extra object curves/events are not part of a generated pose: preserve them too.
        if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0 || AnimationUtility.GetAnimationEvents(clip).Length != 0)
            return "authored-extras";
        return Hash128.Compute(text.ToString()).ToString();
    }

    static void ValidateOwned(RoweHeldTrick set, AnimationClip target, bool bike)
    {
        if (!(bike ? RoweBikeAnimation.Editable(target) : RoweIKKeys.Editable(target)) ||
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(target)) != Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)))
            throw new InvalidOperationException("Tweak pose must belong to this saved trick.");
    }

    public static void Synchronize(RoweHeldTrick set)
    {
        if (!set || !set.useCustomTweak) return;
        SyncTrack(set, set.tweakPose, ref set.tweakHoldSeed, false);
        if (set.authorBike) SyncTrack(set, set.bikeTweakPose, ref set.bikeTweakHoldSeed, true);
    }

    static void SyncTrack(RoweHeldTrick set, AnimationClip target, ref string seed, bool bike)
    {
        if (!target || string.IsNullOrEmpty(seed) || seed != Signature(target)) return;
        ValidateOwned(set, target, bike);
        var next = bike ? RoweBikeAnimation.HeldPose(set) : RoweHeldTrickBuilder.MakeTransition(set.poses, 1, 1, 1, false);
        try
        {
            if (Signature(next) == seed) return;
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { set, target }, "Follow core Hold pose");
            next.name = target.name;
            EditorUtility.CopySerialized(next, target);
            seed = Signature(target);
            EditorUtility.SetDirty(target); EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(target); AssetDatabase.SaveAssetIfDirty(set);
        }
        finally { UnityEngine.Object.DestroyImmediate(next); }
    }

    public static void Restart(RoweHeldTrick set)
    {
        RoweHeldTrickBuilder.ValidateRecipe(set);
        RoweHeldTrickBuilder.EnableCustomTweak(set);
        ValidateOwned(set, set.tweakPose, false);
        if (set.authorBike) ValidateOwned(set, set.bikeTweakPose, true);
        AnimationClip rider = null, bike = null;
        try
        {
            rider = RoweHeldTrickBuilder.MakeTransition(set.poses, 1, 1, 1, false);
            if (set.authorBike) bike = RoweBikeAnimation.HeldPose(set);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restart Tweak from core Hold");
            Undo.RegisterCompleteObjectUndo(bike ? new UnityEngine.Object[] { set, set.tweakPose, set.bikeTweakPose } : new UnityEngine.Object[] { set, set.tweakPose }, "Restart Tweak from core Hold");
            try
            {
                rider.name = set.tweakPose.name; EditorUtility.CopySerialized(rider, set.tweakPose);
                set.tweakHoldSeed = Signature(set.tweakPose);
                EditorUtility.SetDirty(set.tweakPose); AssetDatabase.SaveAssetIfDirty(set.tweakPose);
                if (bike)
                {
                    bike.name = set.bikeTweakPose.name; EditorUtility.CopySerialized(bike, set.bikeTweakPose);
                    set.bikeTweakHoldSeed = Signature(set.bikeTweakPose);
                    EditorUtility.SetDirty(set.bikeTweakPose); AssetDatabase.SaveAssetIfDirty(set.bikeTweakPose);
                }
                EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }
        finally { if (rider) UnityEngine.Object.DestroyImmediate(rider); if (bike) UnityEngine.Object.DestroyImmediate(bike); }
    }
}
