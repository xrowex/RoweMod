using System;
using UnityEditor;
using UnityEngine;

// Resolve authoring targets only when an editor action needs them. Never use
// an imported model's prefab Animator as the scene's driven rider skeleton.
public static class RoweStudioRigs
{
    const string RiderName = "Human Temp";
    const string BikeName = "Bike Skeleton";

    static bool IsSceneAnimator(Animator animator)
        => animator && !EditorUtility.IsPersistent(animator)
            && animator.gameObject.scene.IsValid() && animator.gameObject.scene.isLoaded;

    public static bool IsRider(Animator animator)
    {
        if (!IsSceneAnimator(animator) || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid) return false;
        foreach (var bone in RoweIKSession.Bones)
            if (!animator.GetBoneTransform(bone)) return false;
        return true;
    }

    public static bool IsBike(Animator animator)
    {
        if (!IsSceneAnimator(animator) || (animator.avatar && animator.avatar.isHuman)) return false;
        foreach (string path in RoweBikeAnimation.Paths)
            if (!animator.transform.Find(path)) return false;
        return true;
    }

    public static void Resolve(ref Animator rider, ref Animator bike, bool requireBike = false)
    {
        // Resolve transactionally: an invalid assignment must not partially
        // overwrite the user's slots before its error can be shown.
        var nextRider = rider;
        var nextBike = bike;
        if (IsBike(nextRider) && IsRider(nextBike))
        {
            nextRider = bike;
            nextBike = rider;
        }
        else if (IsBike(nextRider) && !nextBike)
        {
            nextBike = nextRider;
            nextRider = null;
        }
        else if (!nextRider && IsRider(nextBike))
        {
            nextRider = nextBike;
            nextBike = null;
        }

        if (nextRider && !IsRider(nextRider)) throw WrongRole(nextRider, true);
        if (nextBike && !IsBike(nextBike)) throw WrongRole(nextBike, false);

        // Fully assigned, valid custom rigs take this fast path without any
        // scene-wide search. A missing slot only falls back to the exact names.
        if (!nextRider || !nextBike)
        {
            var candidates = Resources.FindObjectsOfTypeAll<Animator>();
            if (!nextRider) nextRider = FindWorkbench(candidates, RiderName, true, true);
            if (!nextBike) nextBike = FindWorkbench(candidates, BikeName, false, requireBike);
        }

        if (!nextRider) throw new InvalidOperationException("Open the paired workbench or assign its Rider (Humanoid) from the Hierarchy.");
        if (requireBike && !nextBike) throw new InvalidOperationException("Open the paired workbench or assign Bike Skeleton to Bike animation rig.");
        rider = nextRider;
        bike = nextBike;
    }

    public static void UseWorkbench(ref Animator rider, ref Animator bike)
    {
        Animator nextRider = null, nextBike = null;
        Resolve(ref nextRider, ref nextBike, true);
        rider = nextRider;
        bike = nextBike;
    }

    static Animator FindWorkbench(Animator[] candidates, string name, bool rider, bool required)
    {
        Animator found = null;
        foreach (var candidate in candidates)
        {
            if (!IsSceneAnimator(candidate) || !string.Equals(candidate.name, name, StringComparison.Ordinal)) continue;
            if (found)
                throw new InvalidOperationException("More than one loaded scene object is named '" + name
                    + "'. Assign the intended " + (rider ? "Rider" : "Bike animation rig") + " explicitly in Rig setup.");
            found = candidate;
        }
        if (found && (rider ? IsRider(found) : IsBike(found))) return found;
        if (!required) return null;
        if (found) throw WrongRole(found, rider);
        throw new InvalidOperationException("No compatible '" + name + "' was found in the open scene. Open the paired workbench or assign "
            + (rider ? "a valid Humanoid Rider" : "the Bike animation rig") + " from the Hierarchy.");
    }

    static InvalidOperationException WrongRole(Animator animator, bool rider)
    {
        string slot = rider ? "Rider (Humanoid)" : "Bike animation rig";
        if (EditorUtility.IsPersistent(animator))
            return new InvalidOperationException(slot + " must be a scene object from the Hierarchy, not an FBX or prefab asset from the Project window.");
        if (!IsSceneAnimator(animator))
            return new InvalidOperationException(slot + " must belong to an open, loaded scene. Open the paired workbench and select its rig again.");
        if (rider && IsBike(animator))
            return new InvalidOperationException("'" + animator.name + "' is a bike rig. Put it in Bike animation rig and select Human Temp for Rider.");
        if (!rider && IsRider(animator))
            return new InvalidOperationException("'" + animator.name + "' is a Humanoid rider. Put it in Rider (Humanoid) and select Bike Skeleton for Bike animation rig.");
        return new InvalidOperationException(rider
            ? "'" + animator.name + "' needs a valid Humanoid Avatar with hands, feet, hips, elbows and knees to be the Rider. Select Human Temp or a compatible scene rig."
            : "'" + animator.name + "' is missing the expected bike animation joints. Select Bike Skeleton or a compatible scene bike rig.");
    }
}
