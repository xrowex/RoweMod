using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using rowemod.Mods;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static Transform Node(string name, Transform parent = null, float x = 0)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = new Vector3(x, 0, 0); return t;
    }
    private static Transform Clone(Transform original, Transform parent, Dictionary<int, Transform> map)
    {
        var t = Node(original.name, parent); t.localPosition = original.localPosition;
        map[original.GetInstanceID()] = t;
        foreach (var child in original.GetComponentsInChildren<Transform>(true).Where(c => c.parent == original)) Clone(child, t, map);
        return t;
    }
    private static void Main()
    {
        var source = Node("Skeleton"); var rig = Node("Character_Reference", source); var hips = Node("Hips", rig);
        var left = Node("LeftLeg", hips, -1); var right = Node("RightLeg", hips, 1);
        var leftTip = Node("Tip", left); var rightTip = Node("Tip", right);
        Node("Ambiguous", left); Node("Ambiguous", left);
        var garment = Node("Pants", source); var duplicateRig = Node("Character_Reference", garment);
        var duplicateHips = Node("Hips", duplicateRig); var duplicateLeft = Node("LeftLeg", duplicateHips, -1);
        var duplicateRight = Node("RightLeg", duplicateHips, 1);
        var duplicateLeftTip = Node("Tip", duplicateLeft); var duplicateRightTip = Node("Tip", duplicateRight);
        var badTip = Node("Tip", duplicateHips); var ambiguous = Node("Ambiguous", duplicateLeft);
        var helper = Node("PantsCloth", duplicateLeft, .3f); var helperChild = Node("PantsHem", helper, .2f);
        var hat = Node("Hat", source);
        var map = new Dictionary<int, Transform>(); var stage = Node("Stage", null, 10000); Clone(source, stage, map);
        var binding = new StudioSkinBinding(new Animator { transform = source, hips = hips }, map);
        var originalPositions = source.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.GetInstanceID(), t => t.position);
        Check(binding.Resolve(left) == map[left.GetInstanceID()], "canonical body keeps its own preview bones");
        Check(binding.Resolve(duplicateLeft) == map[left.GetInstanceID()], "duplicate garment bone binds to animated body instead of frozen garment armature");
        Check(binding.Resolve(duplicateRig) == map[rig.GetInstanceID()], "duplicate clothing rootBone uses the canonical preview root");
        Check(binding.Resolve(duplicateLeftTip) == map[leftTip.GetInstanceID()] && binding.Resolve(duplicateRightTip) == map[rightTip.GetInstanceID()], "repeated names resolve by canonical parent without swapping sides");
        Check(binding.Resolve(badTip) == null && binding.Resolve(ambiguous) == null, "ambiguous names without a unique parent match are rejected");
        var before = map[helperChild.GetInstanceID()].position;
        var resolvedHelper = binding.Resolve(helperChild);
        Check(map[helper.GetInstanceID()].parent == map[left.GetInstanceID()] && resolvedHelper.parent == map[helper.GetInstanceID()], "garment-only helper chains move onto the animated skeleton");
        Check(resolvedHelper.position == before, "reparenting preserves an existing garment helper's rest offset");
        map[left.GetInstanceID()].localPosition += new Vector3(3, 0, 0);
        Check(resolvedHelper.position == before + new Vector3(3, 0, 0), "custom helper and hem follow later preview pose changes");
        Check(map[duplicateLeft.GetInstanceID()].position != binding.Resolve(duplicateLeft).position, "the regression fixture leaves the old duplicate armature frozen");
        Check(binding.Resolve(hat) == map[hat.GetInstanceID()], "non-skeletal accessories retain their preview hierarchy");
        Check(binding.Resolve(null) == null, "unused null bone slots remain null");

        var external = Node("External outfit"); var externalLeft = Node("LeftLeg", external);
        var externalHelper = Node("ExternalCloth", externalLeft, .7f); externalHelper.gameObject.SetActive(false);
        externalHelper.localScale = new Vector3(2, 3, 4); externalHelper.localRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, .5f);
        var added = binding.Resolve(externalHelper);
        Check(added != externalHelper && added.parent == map[left.GetInstanceID()], "external garment helpers are copied into the private stage beneath the canonical bone");
        Check(added.localPosition == externalHelper.localPosition && added.localRotation == externalHelper.localRotation && added.localScale == externalHelper.localScale, "external helpers preserve local bind transforms");
        Check(added.gameObject.layer == 30 && !added.gameObject.activeSelf, "copied helpers preserve activity and use the isolated preview layer");
        Check(binding.Resolve(Node("UnknownBone", Node("Unrelated scene"))) == null, "unrelated scene transforms are never borrowed as preview bones");
        var ordered = new[] { duplicateRight, helperChild, left, null, externalHelper }.Select(binding.Resolve).ToArray();
        Check(ordered.Length == 5 && ordered[0] == map[right.GetInstanceID()] && ordered[1] == resolvedHelper && ordered[2] == map[left.GetInstanceID()] && ordered[3] == null && ordered[4] == added, "skin bone index ordering stays intact");
        int helpers = binding.AddedHelpers, rebound = binding.Rebound, reparented = binding.Reparented;
        Check(binding.Resolve(externalHelper) == added && binding.AddedHelpers == helpers && binding.Rebound == rebound && binding.Reparented == reparented, "repeated renderer references reuse the same resolved bones");
        Check(source.GetComponentsInChildren<Transform>(true).All(t => originalPositions[t.GetInstanceID()] == t.position) && helper.parent == duplicateLeft, "binding never changes the original menu rider or clothing");

        var second = new Dictionary<int, Transform>(); Clone(source, Node("Second Stage"), second);
        var secondBinding = new StudioSkinBinding(new Animator { transform = source, hips = hips }, second);
        Check(secondBinding.Resolve(duplicateLeft) == second[left.GetInstanceID()] && secondBinding.Resolve(duplicateLeft) != binding.Resolve(duplicateLeft), "separate preview stages cannot share bone transforms");
        var custom = Node("Alternate skeleton"); var actualRig = Node("DifferentRootName", custom); var actualHips = Node("Hips", actualRig);
        var misleading = Node("Character_Reference", custom); Node("Hips", misleading);
        var alternate = new Dictionary<int, Transform>(); Clone(custom, Node("Alternate stage"), alternate);
        var alternateBinding = new StudioSkinBinding(new Animator { transform = custom, hips = actualHips }, alternate);
        Check(alternateBinding.Resolve(misleading.Find("Hips")) == alternate[actualHips.GetInstanceID()], "the avatar's humanoid hips identify the animated rig instead of a clothing root's label");
        Console.WriteLine($"PASS: {checks} preview clothing bone-binding checks. Stubbed transforms; native skin deformation still requires in-game confirmation.");
    }
}
