using System;
using System.Text;
using UnityEditor;
using UnityEngine;

// Invoked by an isolated Unity validation harness with the paired workbench
// open. Does not open/save a scene, write an asset, or launch Unity itself.
public static class RoweStudioRigsValidation
{
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Rig resolution: " + message);
    }

    static void ExpectError(Action action, string description)
    {
        try { action(); }
        catch (InvalidOperationException ex)
        {
            Assert(!string.IsNullOrWhiteSpace(ex.Message), description + " must give a readable error.");
            return;
        }
        throw new InvalidOperationException("Rig resolution: " + description + " was not rejected.");
    }

    static string DescribeRider(Animator animator)
    {
        if (!animator) return "Animator=null";
        var avatar = animator.avatar;
        var text = new StringBuilder("name=" + animator.name
            + " scene=" + animator.gameObject.scene.name + " sceneValid=" + animator.gameObject.scene.IsValid()
            + " sceneLoaded=" + animator.gameObject.scene.isLoaded + " persistent=" + EditorUtility.IsPersistent(animator)
            + " enabled=" + animator.enabled + " activeSelf=" + animator.gameObject.activeSelf
            + " activeInHierarchy=" + animator.gameObject.activeInHierarchy + " avatar=" + (avatar ? avatar.name : "null")
            + " avatarHuman=" + (avatar && avatar.isHuman) + " avatarValid=" + (avatar && avatar.isValid));
        foreach (var bone in RoweIKSession.Bones)
        {
            try
            {
                var target = animator.GetBoneTransform(bone);
                text.Append(" | ").Append(bone).Append('=').Append(target ? target.name : "null");
            }
            catch (Exception ex) { text.Append(" | ").Append(bone).Append('=').Append(ex.Message); }
        }
        return text.ToString();
    }

    static Animator MakeBike(Transform parent, string name)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var animator = root.AddComponent<Animator>();
        foreach (string path in RoweBikeAnimation.Paths)
        {
            var joint = root.transform;
            foreach (string part in path.Split('/'))
            {
                var next = joint.Find(part);
                if (!next)
                {
                    next = new GameObject(part).transform;
                    next.SetParent(joint, false);
                }
                joint = next;
            }
        }
        return animator;
    }

    public static void RunAssertions()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode())
            throw new InvalidOperationException("Stop Play Mode and animation preview before running rig resolution assertions.");

        Animator canonicalRider = null, canonicalBike = null;
        RoweStudioRigs.UseWorkbench(ref canonicalRider, ref canonicalBike);
        string riderName = canonicalRider.name, bikeName = canonicalBike.name;
        GameObject fixtures = null;
        try
        {
            // Real scene fixtures: HideAndDontSave moves objects outside the
            // loaded scene, which is deliberately rejected by the resolver.
            fixtures = new GameObject("RoweStudioRigsValidation - temporary");
            fixtures.SetActive(false);
            // Use the initialized workbench Humanoid, temporarily renamed below
            // to test custom assignment semantics. A cloned Animator needs a new
            // native Avatar binding; that engine initialization is not resolver
            // behavior and must not turn this test into a false negative.
            var customRider = canonicalRider;
            var customBike = MakeBike(fixtures.transform, "Custom Bike - rig validation");
            var empty = new GameObject("Incomplete rig - validation");
            empty.transform.SetParent(fixtures.transform, false);
            var incomplete = empty.AddComponent<Animator>();

            Assert(RoweStudioRigs.IsRider(canonicalRider), "initialized canonical rider must be recognized: " + DescribeRider(canonicalRider));
            Assert(RoweStudioRigs.IsBike(canonicalBike) && RoweStudioRigs.IsBike(customBike), "valid canonical and custom bikes must be recognized.");
            Assert(!RoweStudioRigs.IsRider(null) && !RoweStudioRigs.IsBike(null), "empty slots are not valid rigs.");
            Assert(!RoweStudioRigs.IsRider(customBike) && !RoweStudioRigs.IsBike(customRider), "the roles must be distinct.");
            Assert(!RoweStudioRigs.IsRider(incomplete) && !RoweStudioRigs.IsBike(incomplete), "an empty Animator is not a complete rig.");

            Animator rider = null, bike = null;
            RoweStudioRigs.Resolve(ref rider, ref bike);
            Assert(rider == canonicalRider && bike == canonicalBike, "empty slots must use the exact workbench rigs.");

            canonicalRider.name = "Custom Rider - rig validation";
            Assert(RoweStudioRigs.IsRider(customRider), "a noncanonical rider name must retain the valid role: " + DescribeRider(customRider));
            rider = customRider; bike = customBike;
            RoweStudioRigs.Resolve(ref rider, ref bike, true);
            Assert(rider == customRider && bike == customBike, "valid custom assignments must be retained.");
            canonicalRider.name = riderName;

            rider = customBike; bike = customRider;
            RoweStudioRigs.Resolve(ref rider, ref bike, true);
            Assert(rider == customRider && bike == customBike, "an exact reversal must be corrected.");

            rider = customBike; bike = null;
            RoweStudioRigs.Resolve(ref rider, ref bike);
            Assert(rider == canonicalRider && bike == customBike, "a bike in Rider with an empty Bike slot must move to Bike.");

            rider = null; bike = customRider;
            RoweStudioRigs.Resolve(ref rider, ref bike);
            Assert(rider == customRider && bike == canonicalBike, "a rider in Bike with an empty Rider slot must move to Rider.");

            rider = customBike; bike = canonicalBike;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "two bikes in non-empty slots");
            Assert(rider == customBike && bike == canonicalBike, "a failed repair must not overwrite a non-empty counterpart.");

            rider = customRider; bike = canonicalRider;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "a supplied wrong Bike even when optional");
            Assert(rider == customRider && bike == canonicalRider, "a wrong non-empty Bike must not be silently dropped.");

            rider = incomplete; bike = customBike;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "a supplied invalid Rider");
            Assert(rider == incomplete && bike == customBike, "failed validation must leave both original slots unchanged.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoweAnimationWorkspace.PrefabPath);
            Assert(prefab, "the validation workbench source prefab is required.");
            var assetAnimators = prefab.GetComponentsInChildren<Animator>(true);
            Assert(assetAnimators.Length > 0, "the source prefab must contain an Animator for the persistent-asset check.");
            var persistent = assetAnimators[0];
            Assert(EditorUtility.IsPersistent(persistent), "the asset fixture must actually be persistent.");
            Assert(!RoweStudioRigs.IsRider(persistent) && !RoweStudioRigs.IsBike(persistent), "persistent prefab Animators must never count as scene rigs.");
            rider = persistent; bike = customBike;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "an imported/persistent Rider");
            rider = customRider; bike = persistent;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "an imported/persistent Bike");

            // A fully supplied valid pair must not care about fallback ambiguity.
            incomplete.name = riderName;
            rider = customRider; bike = customBike;
            RoweStudioRigs.Resolve(ref rider, ref bike, true);
            Assert(rider == customRider && bike == customBike, "valid assigned rigs must bypass fallback scans.");
            rider = null; bike = customBike;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "ambiguous exact-name Rider fallback");
            Assert(!rider && bike == customBike, "ambiguous fallback must be transactional.");
            incomplete.name = "Incomplete rig - validation";

            canonicalBike.name = "Hidden canonical bike name - validation";
            rider = customRider; bike = null;
            RoweStudioRigs.Resolve(ref rider, ref bike);
            Assert(rider == customRider && !bike, "rider-only work must allow a missing Bike without picking an arbitrary custom bike.");
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike, true), "a required missing Bike");
            Assert(rider == customRider && !bike, "a missing required Bike must not modify the slots.");
            rider = customRider; bike = customBike;
            ExpectError(() => RoweStudioRigs.UseWorkbench(ref rider, ref bike), "explicit workbench reset with no canonical Bike");
            Assert(rider == customRider && bike == customBike, "failed workbench reset must retain the custom pair.");
            canonicalBike.name = bikeName;

            canonicalRider.name = "Hidden canonical rider name - validation";
            rider = null; bike = customBike;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "missing exact-name Rider despite a differently named valid Humanoid");
            Assert(!rider && bike == customBike, "fallback must not choose a first arbitrary Humanoid.");
            rider = customBike; bike = null;
            ExpectError(() => RoweStudioRigs.Resolve(ref rider, ref bike), "failed one-slot relocation when canonical Rider is unavailable");
            Assert(rider == customBike && !bike, "failed relocation must restore the exact initial slots.");
            canonicalRider.name = riderName;

            rider = customRider; bike = customBike;
            RoweStudioRigs.UseWorkbench(ref rider, ref bike);
            Assert(rider == canonicalRider && bike == canonicalBike, "explicit workbench reset must select the original canonical pair.");
        }
        finally
        {
            if (canonicalRider) canonicalRider.name = riderName;
            if (canonicalBike) canonicalBike.name = bikeName;
            if (fixtures) UnityEngine.Object.DestroyImmediate(fixtures);
        }
        Debug.Log("ROWE_STUDIO_RIGS_ASSERTIONS passed=true");
    }
}
