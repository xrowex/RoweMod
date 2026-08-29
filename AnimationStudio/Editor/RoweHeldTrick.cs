using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Authoring recipe only. Export contains four plain AnimationClips, never this
// asset, its source reference, its rig, or the Final IK implementation.
public sealed class RoweHeldTrick : ScriptableObject
{
    public string displayName;
    public string clipPrefix;
    public AnimationClip poses, enter, loop, tweak, exit;
    public AnimationClip tweakPose;
    public bool useCustomTweak;
    public float tweakSeconds = .2f;
    public AnimationClip sourceReference, bikeEnterPreview, bikeExitPreview;
    public float enterSeconds = .35f, exitSeconds = .3f;
    public int formatVersion = 1;
    public RoweStudioGameOptions game = new RoweStudioGameOptions();
    public bool authorBike;
    public bool sameBikeBothDirections = true;
    public bool handsFollowBars = true, feetFollowPedals = true, riderFollowsWholeBike = true;
    public bool keepHipsInPlace = true;
    public bool autoReleaseContacts = true;
    public float contactReleaseDistance = .15f;
    // Normal Riding grip/foot points, in the respective bars/pedal joint space.
    // Keep these independent of Hold/Tweak edits so a lifted limb stays released.
    [HideInInspector] public Vector3[] ridingContactPoints = Array.Empty<Vector3>();
    // Palm contacts are separate from legacy wrist anchors: relaxing around a
    // planted palm moves the wrist, but must not accidentally release the hand.
    [HideInInspector] public Vector3[] ridingPalmContactPoints = Array.Empty<Vector3>();
    [HideInInspector] public Quaternion[] ridingWristRotations = Array.Empty<Quaternion>();
    public AnimationClip bikePoses, bikeTweakPose, bikeMotionLoop;
    public AnimationClip[] bikePhases = new AnimationClip[4];
    // Empty on older recipes: preserve their Tweak edits until an explicit restart.
    [HideInInspector] public string tweakHoldSeed, bikeTweakHoldSeed;
}

public static class RoweHeldTrickBuilder
{
    public static readonly string[] Suffixes = { "_Enter", "_Loop", "_Tweak", "_Exit" };
    public static string SafeName(string name)
    {
        string value = string.Concat((name ?? "").Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_')).Trim('_');
        if (value.Length == 0) throw new InvalidOperationException("Give your trick a name first.");
        return value.Length > 60 ? value.Substring(0, 60) : value;
    }

    public static RoweHeldTrick Create(string name, AnimationClip source, float normalTime, float heldTime, Animator rider, Animator bike)
    {
        if (!source || !source.isHumanMotion) throw new InvalidOperationException("Choose a Humanoid rider animation as your starting point.");
        AnimationClip normal = null, held = null;
        try
        {
            using (var session = new RoweIKSession(rider, bike))
            {
                session.Sample(source, null, normalTime); normal = session.Capture(0);
                session.Sample(source, null, heldTime); held = session.Capture(1);
            }
            EnsureFolder("Assets/CustomClips/HeldTricks");
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/CustomClips/HeldTricks/" + SafeName(name));
            AssetDatabase.CreateFolder("Assets/CustomClips/HeldTricks", Path.GetFileName(folder));
            var set = ScriptableObject.CreateInstance<RoweHeldTrick>();
            set.displayName = name.Trim();
            // Collision-resistant clip names: configs resolve clips by name, not GUID.
            set.clipPrefix = "RoweMod_Custom_" + SafeName(name) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            set.sourceReference = source;
            set.game.include = true;
            set.game.omitLoop = true;
            set.bikeEnterPreview = RoweClipBinding.Counterpart(source);
            if (set.bikeEnterPreview)
            {
                string bikePath = AssetDatabase.GetAssetPath(set.bikeEnterPreview);
                set.bikeExitPreview = AssetDatabase.LoadAssetAtPath<AnimationClip>(bikePath.Replace("_Enter.anim", "_Exit.anim"));
            }
            AssetDatabase.CreateAsset(set, folder + "/" + SafeName(name) + ".asset");
            set.poses = new AnimationClip { name = "StudioPoses_" + SafeName(name), frameRate = 60 };
            RoweIKKeys.Write(set.poses, normal, 0, false);
            RoweIKKeys.Write(set.poses, held, 1, false);
            SetTiming(set.poses, 1, false);
            AssetDatabase.CreateAsset(set.poses, folder + "/StudioPoses.anim");
            EditorUtility.SetDirty(set);
            Rebuild(set);
            AssetDatabase.SaveAssets();
            return set;
        }
        finally
        {
            if (normal) UnityEngine.Object.DestroyImmediate(normal);
            if (held) UnityEngine.Object.DestroyImmediate(held);
        }
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static AnimationClip[] Clips(RoweHeldTrick set) => new[] { set.enter, set.loop, set.tweak, set.exit };

    public static void EnableCustomTweak(RoweHeldTrick set)
    {
        ValidateRecipe(set);
        if (!set.tweakPose)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)).Replace('\\', '/');
            var pose = MakeTransition(set.poses, 1, 1, 1, false);
            pose.name = "StudioTweakPose_" + SafeName(set.displayName);
            AssetDatabase.CreateAsset(pose, AssetDatabase.GenerateUniqueAssetPath(folder + "/StudioTweakPose.anim"));
            Undo.RecordObject(set, "Enable custom Tweak");
            set.tweakPose = pose;
            set.tweakHoldSeed = RoweTweakFromHold.Signature(pose);
        }
        else Undo.RecordObject(set, "Enable custom Tweak");
        set.useCustomTweak = true;
        RoweBikeAnimation.EnsureTweak(set);
        set.formatVersion = 2;
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
    }

    public static void Rebuild(RoweHeldTrick set)
    {
        ValidateRecipe(set);
        RoweTweakFromHold.Synchronize(set);
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)).Replace('\\', '/');
        AnimationClip[] clips = Clips(set);
        // Check every destination before touching any generated clip.
        foreach (var clip in clips.Where(c => c))
            if (Path.GetDirectoryName(AssetDatabase.GetAssetPath(clip)).Replace('\\', '/') != folder || !clip.name.StartsWith(set.clipPrefix + "_", StringComparison.Ordinal))
                throw new InvalidOperationException("A generated clip reference points outside this trick. Restore its original reference before rebuilding.");
        for (int phase = 0; phase < clips.Length; phase++)
        {
            float duration = phase == 0 ? set.enterSeconds : phase == 3 ? set.exitSeconds : phase == 2 && set.useCustomTweak ? set.tweakSeconds : .5f;
            float from = phase == 0 ? 0 : 1, to = phase == 3 ? 0 : 1;
            AnimationClip next = phase == 2 && set.useCustomTweak
                ? MakeTransition(set.poses, 1, set.tweakPose, 0, duration, false)
                : MakeTransition(set.poses, from, to, duration, phase == 1);
            next.name = set.clipPrefix + Suffixes[phase];
            if (clips[phase])
            {
                EditorUtility.CopySerialized(next, clips[phase]); // Keep GUIDs stable.
                UnityEngine.Object.DestroyImmediate(next);
            }
            else
            {
                string path = folder + "/" + next.name + ".anim";
                if (File.Exists(path)) throw new InvalidOperationException("An unrelated file already uses the generated clip name: " + path);
                AssetDatabase.CreateAsset(next, path); clips[phase] = next;
            }
            EditorUtility.SetDirty(clips[phase]);
            AssetDatabase.SaveAssetIfDirty(clips[phase]);
        }
        set.enter = clips[0]; set.loop = clips[1]; set.tweak = clips[2]; set.exit = clips[3];
        RoweBikeAnimation.Rebuild(set);
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
    }

    public static void ValidateRecipe(RoweHeldTrick set)
    {
        if (!set || !RoweIKKeys.Editable(set.poses)) throw new InvalidOperationException("Open a saved trick with valid rider poses first.");
        if (!AssetDatabase.GetAssetPath(set).StartsWith("Assets/CustomClips/HeldTricks/", StringComparison.Ordinal)) throw new InvalidOperationException("Save the recipe in CustomClips/HeldTricks.");
        if (string.IsNullOrEmpty(set.clipPrefix) || !set.clipPrefix.StartsWith("RoweMod_Custom_", StringComparison.Ordinal)) throw new InvalidOperationException("This trick is missing its export identity.");
        if (!Finite(set.enterSeconds) || !Finite(set.exitSeconds) || set.enterSeconds < .05f || set.exitSeconds < .05f || set.enterSeconds > 3 || set.exitSeconds > 3)
            throw new InvalidOperationException("Enter and Exit times must be between 0.05 and 3 seconds.");
        if (set.useCustomTweak)
        {
            if (!RoweIKKeys.Editable(set.tweakPose) || Path.GetDirectoryName(AssetDatabase.GetAssetPath(set.tweakPose)) != Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)))
                throw new InvalidOperationException("The custom Tweak pose must belong to this saved trick.");
            if (!Finite(set.tweakSeconds) || set.tweakSeconds < .05f || set.tweakSeconds > 3)
                throw new InvalidOperationException("Tweak time must be between 0.05 and 3 seconds.");
        }
    }

    static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

    public static AnimationClip MakeTransition(AnimationClip poses, float from, float to, float duration, bool looping)
        => MakeTransition(poses, from, poses, to, duration, looping);

    public static AnimationClip MakeTransition(AnimationClip start, float from, AnimationClip finish, float to, float duration, bool looping)
    {
        var clip = new AnimationClip { frameRate = 60 };
        try
        {
            if (!start || !finish || !Finite(duration) || duration <= 0) throw new InvalidOperationException("Choose valid poses and a positive transition time.");
            var bindings = AnimationUtility.GetCurveBindings(start);
            var endBindings = AnimationUtility.GetCurveBindings(finish);
            if (bindings.Length == 0 || AnimationUtility.GetObjectReferenceCurveBindings(start).Length > 0 || AnimationUtility.GetObjectReferenceCurveBindings(finish).Length > 0)
                throw new InvalidOperationException("The pose clips must contain only rider animation curves.");
            if (bindings.Length != endBindings.Length || bindings.Except(endBindings).Any())
                throw new InvalidOperationException("The two poses must have matching Humanoid curves. Recreate the Tweak pose from Held.");
            // Use the shortest quaternion arc across the two endpoints.
            var flipPrefixes = new System.Collections.Generic.HashSet<string>();
            foreach (string prefix in new[] { "Root", "LeftHand", "RightHand", "LeftFoot", "RightFoot" })
            {
                float dot = 0;
                foreach (string axis in new[] { "x", "y", "z", "w" })
                {
                    var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), prefix + "Q." + axis);
                    var a = AnimationUtility.GetEditorCurve(start, binding);
                    var b = AnimationUtility.GetEditorCurve(finish, binding);
                    if (a != null && b != null) dot += a.Evaluate(from) * b.Evaluate(to);
                }
                if (dot < 0) flipPrefixes.Add(prefix);
            }
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(Animator) || binding.path.Length != 0) throw new InvalidOperationException("The pose clip contains non-Humanoid bindings.");
                float a = AnimationUtility.GetEditorCurve(start, binding).Evaluate(from);
                float b = AnimationUtility.GetEditorCurve(finish, binding).Evaluate(to);
                if (flipPrefixes.Any(p => binding.propertyName.StartsWith(p + "Q.", StringComparison.Ordinal))) b = -b;
                if (!Finite(a) || !Finite(b)) throw new InvalidOperationException("The pose contains an invalid curve value.");
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.EaseInOut(0, a, duration, b));
            }
            SetTiming(clip, duration, looping);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            return clip;
        }
        catch { UnityEngine.Object.DestroyImmediate(clip); throw; }
    }

    static void SetTiming(AnimationClip clip, float seconds, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0; settings.stopTime = seconds;
        settings.loopTime = loop; settings.loopBlend = false;
        settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true;
        settings.keepOriginalOrientation = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    public static string Export(RoweHeldTrick set, string outputRoot)
    {
        if (!set) throw new InvalidOperationException("Open a saved trick first.");
        // BuildPipeline can unload unused editor assets. A batch caller's managed
        // local is not a Unity asset reference, so pin the recipe only for the build.
        var flags = set.hideFlags;
        set.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        try { return ExportInternal(set, outputRoot); }
        finally { if (set) set.hideFlags = flags; }
    }

    static string ExportInternal(RoweHeldTrick set, string outputRoot)
    {
        Rebuild(set);
        if (set.game != null && (set.game.include || set.authorBike || set.game.style != RoweTrickStyle.Custom)) return RoweStudioGameExport.Export(set, outputRoot);
        var clips = Clips(set);
        string[] paths = clips.Select(AssetDatabase.GetAssetPath).ToArray();
        foreach (var clip in clips)
        {
            if (!clip.isHumanMotion || AnimationUtility.GetAnimationEvents(clip).Length != 0) throw new InvalidOperationException("Export validation failed: expected a clean Humanoid clip.");
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0) throw new InvalidOperationException("Object references cannot be exported.");
        }
        // A narrow allowlist, never IncludeDependencies on the authoring recipe.
        var dependencies = AssetDatabase.GetDependencies(paths, true);
        if (dependencies.Except(paths).Any()) throw new InvalidOperationException("Unexpected export dependency: " + string.Join(", ", dependencies.Except(paths)));
        string folder = Path.Combine(outputRoot, set.clipPrefix.Substring("RoweMod_Custom_".Length));
        Directory.CreateDirectory(folder);
        string bundle = set.clipPrefix.ToLowerInvariant();
        var manifest = BuildPipeline.BuildAssetBundles(folder, new[] { new AssetBundleBuild { assetBundleName = bundle, assetNames = paths } }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new InvalidOperationException("Unity could not build the animation bundle. Check the Console.");
        File.WriteAllText(Path.Combine(folder, "INSTALL.txt"), string.Join("\n", new[] {
            "RoweMod held trick: " + set.displayName, "",
            "Close Streets. Copy only " + bundle + " into BMX Streets/Mods/rowemod/Bundles.",
            "Start the game and load a map. Open the trick editor and choose the trick to replace.",
            "Select one clip from this set under Custom Animation Clips, then Apply Complete Held Trick.",
            "Older RoweMod versions: assign _Enter to Enter, _Loop to Loop, _Tweak to Tweak, and _Exit to Exit individually.",
            "Use an existing holdable trick as the base. Bike clips, native input, timing multipliers and mirrored direction behavior are not replaced.",
            "This set contains a static held pose, not a repeating movement.",
            set.useCustomTweak ? "Custom Tweak is enabled: _Tweak transitions from Held into a separately authored pose." : "Custom Tweak is off: _Tweak stays in the ordinary Held pose.",
            "The game decides when Tweak/auto-tweak activates and how release blends into Exit. Exit starts at Held.",
            "The editor's reverse-Tweak return is only a preview aid, not an additional exported clip. Test release with your chosen base trick in Streets.", "",
            "This export contains four AnimationClips only. It does not contain Final IK code, rigs, meshes, source clips or editor tools.",
            "Review your rights to any source animation before publishing a derived animation.", ""
        }));
        return Path.Combine(folder, bundle);
    }

    public static string ExportTool(string outputRoot)
    {
        // Deliberately excludes paid vendor sources and all recovered game assets.
        string[] files = { "Assets/Editor/RoweHeldTrick.cs", "Assets/Editor/RoweAnimationStudio.cs", "Assets/Editor/RoweStudioGameSetup.cs", "Assets/Editor/RoweStudioStyle.cs", "Assets/Editor/RoweStudioTheme.cs", "Assets/Editor/RoweBikeAnimation.cs", "Assets/Editor/RoweBikeContacts.cs", "Assets/Editor/RoweWristRelaxation.cs", "Assets/Editor/RoweTweakFromHold.cs", "Assets/Editor/RoweAnimationPackage.cs", "Assets/Editor/RoweIKPoseEditor.cs", "Assets/Editor/RoweAnimationWorkspace.cs", "Assets/Editor/RowePairedWorkbench.cs", "Assets/Editor/ROWE_ANIMATION_STUDIO.md" };
        files = files.Concat(new[] { "Assets/Editor/RoweStudioRigs.cs", "Assets/Editor/RoweRiderModelPreview.cs", "Assets/RoweAnimationStudio/RowePreviewModelState.cs" }).ToArray();
        foreach (string file in files) if (!File.Exists(file)) throw new InvalidOperationException("Missing editor source: " + file);
        Directory.CreateDirectory(outputRoot);
        string path = Path.Combine(outputRoot, "RoweMod_AnimationStudio_ToolsOnly.unitypackage");
        AssetDatabase.ExportPackage(files, path, ExportPackageOptions.Default);
        return path;
    }
}
