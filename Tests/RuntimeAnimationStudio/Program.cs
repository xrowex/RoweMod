using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using rowemod.Mods;

internal static class Program
{
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
    static StudioVariation New(string name = "My variation") => new() { Name = name, TrickKey = "Native_Trick", RiderClips = new[] { "r_enter", null, "r_tweak", "r_exit" } };
    static void Main()
    {
        Check(Near(StudioGizmoMath.SegmentDistance(5, 3, 0, 0, 10, 0, out float fraction), 3) && Near(fraction, .5f), "gizmo hit testing measures distance to the visible ring segment");
        Check(Near(StudioGizmoMath.SegmentDistance(12, 0, 0, 0, 10, 0, out fraction), 2) && fraction == 1, "gizmo picking clamps to the segment endpoints");
        Check(Near(StudioGizmoMath.SegmentDistance(3, 4, 0, 0, 0, 0, out fraction), 5), "edge-on collapsed ring segments remain pickable without NaN");
        Check(Near(StudioGizmoMath.AngleDelta(179, -179), 2) && Near(StudioGizmoMath.AngleDelta(-179, 179), -2), "ring dragging crosses angle wrap without jumping backwards");
        Check(Near(StudioGizmoMath.AngleDelta(0, 90) + StudioGizmoMath.AngleDelta(90, 180) + StudioGizmoMath.AngleDelta(180, 270) + StudioGizmoMath.AngleDelta(270, 0), 360), "ring drag increments retain a complete turn");
        var variation = New();
        Check(variation.Contact(0, 0, .5f) == StudioContactMode.Auto, "old variations retain the game's original contacts");
        variation.SetContact(0, 0, .25f, StudioContactMode.Locked);
        variation.SetContact(0, 0, .75f, StudioContactMode.Free);
        variation.SetContact(0, 0, 1, StudioContactMode.Auto);
        Check(variation.Validate() == null && variation.Version == 2, "contact tracks have a recognized format version");
        Check(variation.Contact(0, 0, .1f) == StudioContactMode.Auto && variation.Contact(0, 0, .5f) == StudioContactMode.Locked, "contact changes hold from their key without changing earlier motion");
        Check(variation.Contact(0, 0, .8f) == StudioContactMode.Free && variation.Contact(0, 0, 1) == StudioContactMode.Auto, "release and return to native contact are independent keys");
        Check(variation.Contact(1, 0, .5f) == StudioContactMode.Auto && variation.Contact(0, 1, .5f) == StudioContactMode.Auto, "contacts are isolated by limb and phase");
        Check(variation.AdjacentKey(0, .25f, 1) == .75f && variation.AdjacentKey(0, .75f, -1) == .25f, "navigation includes exact contact keys in both directions");
        Check(variation.AdjacentKey(0, 1, 1) == null && variation.AdjacentKey(0, 0, -1) == null, "navigation at boundaries does not wrap unexpectedly");
        Check(variation.AdjacentKey(0, .5f, 1, "LeftHand") == .75f && variation.AdjacentKey(0, .5f, 1, "RightHand") == null, "selected-joint navigation filters contact tracks");
        var badContact = variation.Copy(); badContact.Contacts[0].Mode = (StudioContactMode)99;
        Check(badContact.Validate() != null, "unknown contact mode rejected");
        badContact = variation.Copy(); badContact.Contacts[0].Limb = 4;
        Check(badContact.Validate() != null, "invalid limb rejected");
        badContact = variation.Copy(); badContact.Contacts[0].Time = float.NaN;
        Check(badContact.Validate() != null, "invalid contact time rejected");
        badContact = variation.Copy(); badContact.Contacts.Add(badContact.Contacts[0]);
        Check(badContact.Validate() != null, "overlapping contact keys rejected");
        var anchored = variation.Copy();
        var anchor = anchored.ContactKeyAt(0, 0, .5f);
        anchor.HasAnchor = true; anchor.TargetId = 0; anchor.AnchorPosition = new StudioVector(.2f, .3f, -.1f); anchor.AnchorRotation = new StudioVector(10, 20, 30);
        var anchorCopy = anchored.Copy().ContactKeyAt(0, 0, .6f);
        Check(anchorCopy.HasAnchor && anchorCopy.AnchorPosition.X == .2f && anchorCopy.AnchorRotation.Y == 20, "captured bike-relative lock anchor survives persistence and holds between keys");
        anchor.AnchorPosition.X = float.NaN;
        Check(anchored.Validate() != null, "invalid lock anchor rejected");
        anchor.AnchorPosition.X = .2f; anchor.TargetId = 99;
        Check(anchored.Validate() != null, "unknown lock target rejected");
        variation.Contacts.Clear();
        Check(variation.Validate() == null, "empty named variation is valid");
        var pose = new StudioPose(new StudioVector(60, 20, -10), new StudioVector(.1f, .2f, .3f));
        variation.SetKey(0, "Head", .5f, pose);
        Check(variation.Validate() == null, "known humanoid pose accepted");
        Check(Near(variation.Evaluate(0, "Head", .5f).Rotation.X, 60), "exact key pose reproduced");
        Check(Near(variation.Evaluate(0, "Head", 0).Rotation.X, 0) && Near(variation.Evaluate(0, "Head", 1).Rotation.X, 0), "unkeyed phase boundaries use native pose");
        Check(Near(variation.Evaluate(0, "Head", .25f).Rotation.X, 30), "interior interpolation blends from native pose");
        Check(Near(variation.Evaluate(0, "Head", .75f).Position.Y, .1f), "position interpolates back toward native pose");
        Check(variation.Evaluate(1, "Head", .5f).Rotation.X == 0, "keys stay within their phase");
        var held = New(); held.SetKey(1, "Chest", .836f, pose); held.SetKey(2, "LeftUpperLeg", .98f, pose);
        Check(held.Evaluate(1, "Chest", 0).Rotation.X == 60 && held.Evaluate(1, "Chest", 1).Rotation.X == 60, "a single Loop pose is held instead of falling back to stock every cycle");
        Check(held.Evaluate(2, "LeftUpperLeg", 0).Rotation.X == 60 && held.Evaluate(2, "LeftUpperLeg", 1).Rotation.X == 60, "a late Tweak pose remains active when the native tweak state holds its last frame");
        held.SetKey(1, "Head", .25f, default); held.SetKey(1, "Head", .75f, pose);
        Check(Near(held.Evaluate(1, "Head", 0).Rotation.X, 30) && Near(held.Evaluate(1, "Head", 1).Rotation.X, 30), "Loop interpolation wraps between actual keys across the seam");
        Check(Near(held.Evaluate(1, "Head", .5f).Rotation.X, 30), "Loop interior interpolation remains unchanged");
        Check(held.Evaluate(0, "Chest", .5f).Rotation.X == 0 && held.Evaluate(3, "Chest", .5f).Rotation.X == 0, "held Loop and Tweak keys do not overwrite Enter or Exit phases");
        Check(variation.Evaluate(0, "RightHand", .5f).Rotation.X == 0, "keys do not affect unedited joints");
        Check(variation.Evaluate(0, "Head", float.NaN).Rotation.X == 0, "invalid playback time cannot produce NaN transforms");
        variation.SetKey(0, "Head", .5f, new StudioPose(new StudioVector(80, 0, 0), default));
        Check(variation.Keys.Count == 1 && variation.Evaluate(0, "Head", .5f).Rotation.X == 80, "editing one key replaces its prior pose");
        variation.SetKey(0, "Head", 1, pose);
        Check(variation.Evaluate(0, "Head", 2).Rotation.X == 60, "explicit final pose can be held");
        Check(!variation.MoveKey(0, "Head", .5f, 1) && variation.Keys.Count == 2, "retime collision preserves both keys");
        Check(variation.MoveKey(0, "Head", .5f, .25f) && variation.Keys.Any(k => k.Time == .25f), "retime updates the actual evaluated key");
        Check(!variation.MoveKey(0, "Head", .25f, float.NaN), "invalid retime is rejected");
        var copy = variation.Copy(); copy.Keys[0].Rotation.X = 999;
        Check(variation.Keys[0].Rotation.X != 999, "editing a copy preserves the saved/native source");
        var mirrored = StudioTargets.Reflect(StudioTargets.Reflect(pose));
        Check(mirrored.Rotation.Y == pose.Rotation.Y && mirrored.Position.X == pose.Position.X, "pose reflection is reversible");
        Check(StudioTargets.Mirror("LeftHand") == "RightHand" && StudioTargets.Mirror("RightFoot") == "LeftFoot", "mirror routes to the matching physical limb");
        Check(StudioTargets.Mirror("Bike:LeftPedal") == "Bike:RightPedal", "bike contacts mirror to the other pedal");
        Check(StudioTargets.Reflect(pose).Rotation.Y == -20 && StudioTargets.Reflect(pose).Position.X == -.1f, "mirror reflects sagittal pose axes");
        foreach (Action<StudioVariation> corrupt in new Action<StudioVariation>[] {
            v => v.Id = "../escape", v => v.Name = "", v => v.Version = 99, v => v.TrickKey = "",
            v => v.RiderClips = new string[2], v => v.Keys[0].Target = "UnknownJoint", v => v.Keys[0].Phase = 5,
            v => v.Keys[0].Time = float.PositiveInfinity, v => v.Keys[0].Rotation.X = float.NaN,
            v => v.Keys[0].Position.Z = 500, v => v.Keys.Add(v.Keys[0]) })
        { var bad = variation.Copy(); corrupt(bad); Check(bad.Validate() != null, "invalid project data rejected"); }

        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rowemod-studio-test-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            var library = new StudioLibrary(root); library.Save(variation);
            var contactSaved = New("Contact style"); contactSaved.TrickKey = "Contact_Trick"; contactSaved.SetContact(2, 3, .5f, StudioContactMode.Free);
            library.Save(contactSaved); library.Assign(contactSaved);
            var contactReloaded = new StudioLibrary(root); contactReloaded.Load();
            Check(contactReloaded.Active(contactSaved.TrickKey).Contact(2, 3, .75f) == StudioContactMode.Free, "contact mode survives saved and applied snapshot reload");
            library.Reset(contactSaved.TrickKey); library.Variations.RemoveAll(v => v.Id == contactSaved.Id);
            File.Delete(Path.Combine(root, contactSaved.Id + ".variation.json"));
            Check(library.Bindings.Count == 0, "saving does not activate a variation");
            library.Assign(variation);
            Check(library.Active(variation.TrickKey)?.Id == variation.Id, "explicit Apply selects the variation");
            var revised = variation.Copy(); revised.Keys[0].Rotation.X = 123; library.Save(revised);
            Check(library.Active(variation.TrickKey).Keys[0].Rotation.X != 123, "saving edits to an assigned variation does not silently apply them");
            var savedOnly = new StudioLibrary(root); savedOnly.Load();
            Check(savedOnly.Variations.Single(v => v.Id == revised.Id).Keys[0].Rotation.X == 123 &&
                savedOnly.Active(variation.TrickKey).Keys[0].Rotation.X != 123, "saved and applied poses remain separate after restart");
            library.Assign(revised);
            Check(library.Active(variation.TrickKey).Keys[0].Rotation.X == 123, "Apply explicitly replaces the active pose snapshot");
            revised.Keys[0].Rotation.X = 321;
            Check(library.Active(variation.TrickKey).Keys[0].Rotation.X == 123, "editing a working copy cannot mutate the applied pose");
            var second = New("Another style"); library.Save(second); library.Assign(second);
            Check(library.Variations.Count == 2 && library.Active(second.TrickKey).Id == second.Id, "multiple variations coexist with one chosen version per trick");
            var other = New("Other trick"); other.TrickKey = "Native_Other"; library.Save(other); library.Assign(other);
            Check(library.Bindings.Count == 2, "another trick has an independent assignment");
            var reopened = new StudioLibrary(root); reopened.Load();
            Check(reopened.Variations.Count == 3 && reopened.Active(second.TrickKey).Id == second.Id, "save and assignment survive restart");
            Check(reopened.Variations.Single(v => v.Id == variation.Id).Keys.Count == variation.Keys.Count, "rider pose keys survive file roundtrip");
            reopened.Reset(second.TrickKey);
            Check(reopened.Active(second.TrickKey) == null && reopened.Variations.Count == 3, "Use original keeps the variation library");
            Check(reopened.Active(other.TrickKey)?.Id == other.Id, "reset does not change another trick");
            string invalid = Path.Combine(root, Guid.NewGuid().ToString("N") + ".variation.json"); File.WriteAllText(invalid, "{bad json");
            reopened.Load(); Check(reopened.Variations.Count == 3 && File.Exists(invalid), "malformed file is ignored and preserved");
            string before = File.ReadAllText(Path.Combine(root, variation.Id + ".variation.json"));
            variation.Name = "Updated style"; reopened.Save(variation);
            Check(File.ReadAllText(Path.Combine(root, variation.Id + ".variation.json.bak")) == before, "overwriting a variation retains its prior file");
            var large = New("Detailed variation"); large.TrickKey = "Detailed_Trick";
            for (int i = 0; i < 8192; i++) large.SetKey(0, "Head", i / 8191f, pose);
            reopened.Save(large); reopened.Assign(large);
            var detailed = new StudioLibrary(root); detailed.Load();
            Check(detailed.Active(large.TrickKey)?.Keys.Count == 8192, "largest valid pose track remains assigned after restart");
            File.WriteAllText(Path.Combine(root, "assignments.json"), JsonConvert.SerializeObject(new System.Collections.Generic.Dictionary<string, string> { [large.TrickKey] = large.Id }));
            var prototype = new StudioLibrary(root); prototype.Load();
            Check(prototype.Active(large.TrickKey)?.Keys.Count == 8192, "first prototype assignments remain readable");
            var legacyEdit = large.Copy(); legacyEdit.Keys[0].Rotation.X = 777; prototype.Save(legacyEdit);
            var migrated = new StudioLibrary(root); migrated.Load();
            Check(migrated.Active(large.TrickKey).Keys[0].Rotation.X != 777 && migrated.Variations.Single(v => v.Id == large.Id).Keys[0].Rotation.X == 777,
                "saving an old prototype project freezes the prior applied pose before migration");
        }
        finally
        {
            if (!root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("rowemod-studio-test-", StringComparison.Ordinal))
                throw new Exception("Test cleanup path escaped its temporary root");
            Directory.Delete(root, true);
        }
        Console.WriteLine($"PASS: {checks} runtime Studio timeline, validation, save/restore and assignment checks. Live preview/gameplay tested separately.");
    }
}
