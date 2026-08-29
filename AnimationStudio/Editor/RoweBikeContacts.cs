using System;
using UnityEditor;
using UnityEngine;

// Editor-only contact permissions. Pose data is baked into ordinary clips;
// no follower, proximity test or extra IK component is exported to the game.
public static class RoweBikeContacts
{
    public const float DefaultReleaseDistance = .15f;
    public const float MinReleaseDistance = .02f;
    public const float MaxReleaseDistance = 1f;

    static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    public static bool HasReference(RoweHeldTrick set)
    {
        if (!set || set.ridingContactPoints == null || set.ridingContactPoints.Length != 4) return false;
        foreach (var point in set.ridingContactPoints) if (!Finite(point)) return false;
        return true;
    }

    public static bool HasPalmReference(RoweHeldTrick set)
    {
        if (!set || set.ridingPalmContactPoints == null || set.ridingPalmContactPoints.Length != 2) return false;
        foreach (var point in set.ridingPalmContactPoints) if (!Finite(point)) return false;
        return true;
    }

    public static Transform Parent(RoweIKSession session, int contact)
    {
        if (session?.bikeJoints == null || contact < 0 || contact >= 4) return null;
        return session.bikeJoints[contact < 2 ? 2 : contact + 2];
    }

    // Call before sampling the requested pose, never in the middle of a drag.
    // Older recipes get a baseline from Riding, not their possibly lifted Hold.
    public static void EnsureReference(RoweHeldTrick set, RoweIKSession session)
    {
        if (!set || !set.authorBike || (HasReference(set) && HasPalmReference(set))) return;
        if (!set.poses || !set.bikePoses || session?.bikeJoints == null)
            throw new InvalidOperationException("Start paired rider + bike posing to set the normal contact points.");

        session.Sample(set.poses, set.bikePoses, 0, 0);
        var points = HasReference(set) ? set.ridingContactPoints : new Vector3[4];
        if (!HasReference(set)) for (int i = 0; i < points.Length; i++)
        {
            points[i] = Parent(session, i).InverseTransformPoint(session.positions[i]);
            if (!Finite(points[i])) throw new InvalidOperationException("The Riding pose has an invalid hand or foot contact point.");
        }
        var palms = HasPalmReference(set) ? set.ridingPalmContactPoints : new Vector3[2];
        if (!HasPalmReference(set)) for (int i = 0; i < palms.Length; i++)
        {
            palms[i] = Parent(session, i).InverseTransformPoint(session.GripPoint(i));
            if (!Finite(palms[i])) throw new InvalidOperationException("The Riding pose has an invalid palm contact point.");
        }
        // Derived migration metadata, not a user edit. Recording this in Undo
        // would recreate an undo item when UndoRedo resamples an older recipe.
        set.ridingContactPoints = points;
        set.ridingPalmContactPoints = palms;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssetIfDirty(set);
    }

    public static float ReleaseDistance(RoweHeldTrick set)
        => set && float.IsFinite(set.contactReleaseDistance)
            ? Mathf.Clamp(set.contactReleaseDistance, MinReleaseDistance, MaxReleaseDistance)
            : DefaultReleaseDistance;

    public static float Distance(RoweHeldTrick set, RoweIKSession session, int contact)
    {
        var parent = Parent(session, contact);
        if (!parent || !HasReference(set) || !Finite(session.positions[contact])) return float.PositiveInfinity;
        if (contact < 2 && HasPalmReference(set))
        {
            var palm = session.GripPoint(contact);
            return Finite(palm) ? Vector3.Distance(palm, parent.TransformPoint(set.ridingPalmContactPoints[contact])) : float.PositiveInfinity;
        }
        return Vector3.Distance(session.positions[contact], parent.TransformPoint(set.ridingContactPoints[contact]));
    }

    public static bool ShouldFollow(RoweHeldTrick set, RoweIKSession session, int contact)
    {
        if (!set || !Parent(session, contact) || (contact < 2 ? !set.handsFollowBars : !set.feetFollowPedals)) return false;
        return !set.autoReleaseContacts || Distance(set, session, contact) <= ReleaseDistance(set);
    }

    // Explicit Relax-button action only. Even forced following must not snap a
    // distant hand back to the bike; always check the configured range first.
    public static bool TryGripSnapTarget(RoweHeldTrick set, RoweIKSession session, int hand, out Vector3 target)
    {
        target = default;
        if (!set || !set.authorBike || !set.handsFollowBars || hand < 0 || hand > 1 || !HasPalmReference(set)) return false;
        var bars = Parent(session, hand);
        if (!bars || Distance(set, session, hand) > ReleaseDistance(set)) return false;
        target = bars.TransformPoint(set.ridingPalmContactPoints[hand]);
        return Finite(target);
    }
}
