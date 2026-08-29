using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RoweMod.AnimationAuthoring;

[Serializable]
public sealed class RoweStudioGameOptions
{
    // Zero preserves recipes made before the friendly style picker existed.
    public RoweTrickStyle style;
    public AnimationClip motionLoop;
    public bool include;
    public bool omitLoop, omitTweak;
    public bool customRider, customBike, customRiderMirror, customBikeMirror;
    public bool sameRiderMirror = true;
    public AnimationClip[] rider = new AnimationClip[4], bike = new AnimationClip[4];
    public AnimationClip[] riderMirror = new AnimationClip[4], bikeMirror = new AnimationClip[4];
    public bool applyTiming, applyRules;
    public float overallSpeed = 1, enterSpeed = 1, loopSpeed = 1, tweakSpeed = 1, exitSpeed = 1;
    public float tweakAt = .7f;
    public bool onlyInAir = true, allowLandingHold;
}

public sealed partial class RoweIKPoseEditor
{
    bool gameHelp, controllerHelp, advancedGameSetup;
    int inspectPhase;
    float inspectTime;
    static readonly string[] PhaseNames = { "Enter", "Loop", "Tweak", "Exit" };

    void DrawStudioGameSetup()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("GAME SETUP / saved with this trick", EditorStyles.boldLabel);
        bool friendly = recipe.game.style != RoweTrickStyle.Custom;
        if (friendly)
        {
            EditorGUILayout.HelpBox(RoweTrickStyles.Summary(recipe.game.style) + "\nYour style handles the Loop and Tweak slots automatically. " + (recipe.authorBike ? "Your authored bike is included. Timing stays inherited unless you customize it." : "Keep the game's bike and timing unless you want to customize them."), MessageType.Info);
            string problem = RoweTrickStyles.Readiness(recipe);
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            advancedGameSetup = EditorGUILayout.Foldout(advancedGameSetup, "Optional: bike clips, mirrors, timing and game rules", true);
            if (!advancedGameSetup) { DrawGamePhaseInspection(); return; }
        }
        gameHelp = EditorGUILayout.Foldout(gameHelp, "How to build a Streets trick", true);
        if (gameHelp)
            EditorGUILayout.HelpBox("1. Pose Riding, Held and optionally Tweak in the first three tabs.\n2. Choose native hold (no Loop) for a steady pose, or supply a repeating Loop.\n3. Keep the game's bike clips, or supply matching Generic bike clips below.\n4. Leave timing/rules off to inherit the selected game trick, or include your own values.\n5. Build in Preview / export. In RoweMod choose the base trick, then Apply Studio Package.\n\nEnter → Tweak (when requested/automatic) → hold OR loop → Exit. The game owns input and transitions; animation clips do not move the physical bike.", MessageType.Info);

        var serialized = new SerializedObject(recipe);
        var g = serialized.FindProperty("game");
        if (!friendly) Field(g, "include", "Include Game Setup in bundle", "Exports explicit phase assignments and any opted-in timing/rules. Older four-clip recipes remain available when off.");
        using (new EditorGUI.DisabledScope(!friendly && !g.FindPropertyRelative("include").boolValue))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Rider / Humanoid", EditorStyles.boldLabel);
            Field(g, "customRider", "Use my own phase clips", "Off: build phases from your three poses. On: use editable multi-key Humanoid clips in CustomClips; rebuilding never changes those sources.");
            if (g.FindPropertyRelative("customRider").boolValue) PhaseFields(g.FindPropertyRelative("rider"), recipe.game.style);
            else
            {
                using (new EditorGUI.DisabledScope(true))
                    for (int i = 0; i < 4; i++)
                        if (RoweTrickStyles.UsesPhase(recipe.game.style, i)) EditorGUILayout.ObjectField(PhaseNames[i], RoweStudioGameExport.Resolve(recipe)[0][i], typeof(AnimationClip), false);
                if (!friendly)
                {
                    Field(g, "omitLoop", "Native steady hold (no Loop)", "Exports an empty Loop slot. Streets can hold the last Enter/Tweak pose when holdIfNoLoop is enabled. This is different from a looping animation.");
                    Field(g, "omitTweak", "No Tweak animation", "Exports an empty Tweak slot. Unlike a Tweak clip containing the Held pose, this removes this trick's V2 Tweak animation path.");
                }
            }
            EditorGUILayout.HelpBox("For a repeating trick, assign a custom Loop and match its first/last poses. Native Tweak requests are processed in Launch/Hold, not the Loop phase. Enter and Exit are required; blank Loop/Tweak deliberately removes that phase.", MessageType.None);
            Field(g, "sameRiderMirror", "Same rider clips both directions", "Reuses clip references. This does NOT create mirrored poses. Off keeps the game's opposite-direction clips unless custom mirrors are supplied.");
            using (new EditorGUI.DisabledScope(g.FindPropertyRelative("sameRiderMirror").boolValue))
            {
                Field(g, "customRiderMirror", "Use custom mirrored rider clips");
                if (g.FindPropertyRelative("customRiderMirror").boolValue) PhaseFields(g.FindPropertyRelative("riderMirror"), recipe.game.style);
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Bike / Generic joint curves", EditorStyles.boldLabel);
            if (recipe.authorBike) EditorGUILayout.HelpBox("Your bike poses are included automatically. Edit them beside the rider in Riding, Held, Tweak or Movement. Imported bike assignments are kept but inactive while bike authoring is enabled.", MessageType.Info);
            else
            {
                Field(g, "customBike", "Replace bike animation clips", "Off keeps every game bike phase. On replaces four slots; clips must target the exact Bike Skeleton hierarchy.");
                if (g.FindPropertyRelative("customBike").boolValue) PhaseFields(g.FindPropertyRelative("bike"), recipe.game.style);
            }
            Field(g, "customBikeMirror", "Replace mirrored bike clips");
            if (g.FindPropertyRelative("customBikeMirror").boolValue) PhaseFields(g.FindPropertyRelative("bikeMirror"), recipe.game.style);
            EditorGUILayout.HelpBox("Keep rider and bike phase lengths aligned: V2 applies the SAME speed to both, rather than stretching one to match. Bike joint paths are validated against the local Bike Skeleton before export. Preview-only bike references are never silently bundled.", MessageType.None);
            EditorGUILayout.Space(8);
            Field(g, "applyTiming", "Include my timing settings", "Off keeps the selected base trick's timing, not necessarily 1×.");
            using (new EditorGUI.DisabledScope(!g.FindPropertyRelative("applyTiming").boolValue))
            {
                Slider(g, "overallSpeed", "All phases speed", .05f, 10);
                Slider(g, "enterSpeed", "Enter speed", .05f, 10);
                if (!friendly || recipe.game.style == RoweTrickStyle.Loop) Slider(g, "loopSpeed", "Loop speed", .05f, 10);
                if (!friendly || recipe.game.style == RoweTrickStyle.HoldTweak) Slider(g, "tweakSpeed", "Tweak speed", .05f, 10);
                Slider(g, "exitSpeed", "Exit speed", .05f, 10);
                if (!friendly || recipe.game.style == RoweTrickStyle.HoldTweak) Slider(g, "tweakAt", "Tweak start (% of Enter)", 0, 1, true);
            }
            EditorGUILayout.HelpBox("1× = authored speed. Enter/Loop speed = character master × all-phases × phase speed. In this game build, Tweak/Exit apply all-phases TWICE: master × all-phases² × phase speed. Keep all-phases at 1 and tune individual phases for predictable timing. Tweak start is progress through Enter (70% = 0.7), not seconds or fade duration. Native Tweak fade is 0.15 seconds.", MessageType.None);
            Field(g, "applyRules", "Include my gameplay rules", "Off preserves the selected base trick's air/landing rules.");
            using (new EditorGUI.DisabledScope(!g.FindPropertyRelative("applyRules").boolValue))
            {
                Field(g, "onlyInAir", "Air-only flag (native data)", "Saved to OnlyFireIfInAir, but not read by the inspected V2 Fire/Brain paths. This does not override the game's input rules.");
                Field(g, "allowLandingHold", "Allow landing while holding");
            }
            EditorGUILayout.HelpBox("Landing hold is used by the V2 brain. The air-only data flag is saved, but was not read in the audited V2 firing path; don't rely on it to enforce an airborne restriction.", MessageType.None);
        }
        if (serialized.ApplyModifiedProperties()) { AssetDatabase.SaveAssetIfDirty(recipe); exportedBundle = null; }
        controllerHelp = EditorGUILayout.Foldout(controllerHelp, "Controller / blend settings: what belongs to the game?", true);
        if (controllerHelp)
            EditorGUILayout.HelpBox("REFERENCE ONLY — these are character-wide, not per-trick clip settings.\n\nTrickControllerV2: useLoopPhase, holdIfNoLoop, autoTweak, layerBringUp/down, launchFade, exitStartBiasFromHold and exitFadeFromHold. They determine phase choice and blending. autoTweakAt exists but the inspected Update reads the per-trick Tweak start instead.\n\nTrickAnimator: layer indexes, masks, additive flags, fade modes, default phase fades, base loops/speed and cache pruning.\n\nMirror playback delegates to a linked mirrorSyncData trick, NOT the old mirror clip fields. Studio creates a private linked partner only if mirrored tracks are replaced; it does not edit the stock partner.\n\nPer-track overallSpeedMult exists, but V2 ApplySpeed does not use it. Exposing that as a working slider would be misleading. Global controller values stay untouched; no new hooks or constant overrides are installed.", MessageType.Info);
        DrawGamePhaseInspection();
    }

    static void Field(SerializedProperty g, string name, string label, string help = "")
    {
        var p = g.FindPropertyRelative(name);
        p.boolValue = EditorGUILayout.ToggleLeft(new GUIContent(label, help), p.boolValue);
    }
    static void Slider(SerializedProperty g, string name, string label, float min, float max, bool percent = false)
    {
        var p = g.FindPropertyRelative(name);
        float width = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 210;
        try { p.floatValue = percent ? EditorGUILayout.Slider(label, p.floatValue * 100, min * 100, max * 100) / 100 : EditorGUILayout.Slider(label, p.floatValue, min, max); }
        finally { EditorGUIUtility.labelWidth = width; }
    }
    static void PhaseFields(SerializedProperty array, RoweTrickStyle style = RoweTrickStyle.Custom)
    {
        if (array.arraySize != 4) array.arraySize = 4;
        for (int i = 0; i < 4; i++)
            if (RoweTrickStyles.UsesPhase(style, i)) EditorGUILayout.PropertyField(array.GetArrayElementAtIndex(i), new GUIContent(PhaseNames[i] + (i == 0 || i == 3 ? " (required)" : " (optional)")));
    }
    void DrawGamePhaseInspection()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Check a paired phase", EditorStyles.boldLabel);
        int[] phases = Enumerable.Range(0, 4).Where(i => RoweTrickStyles.UsesPhase(recipe.game.style, i)).ToArray();
        int selectedPhase = Mathf.Max(0, Array.IndexOf(phases, inspectPhase));
        inspectPhase = phases[GUILayout.Toolbar(selectedPhase, phases.Select(i => PhaseNames[i]).ToArray(), GUILayout.Height(28))];
        inspectTime = EditorGUILayout.Slider("Phase progress", inspectTime, 0, 1);
        if (GUILayout.Button("Sample rider + bike at this point")) Run(() =>
        {
            Stop();
            var tracks = RoweStudioGameExport.Resolve(recipe);
            var riderClip = tracks[0][inspectPhase];
            var vehicleClip = tracks[1] == null ? null : tracks[1][inspectPhase];
            if (!riderClip) throw new InvalidOperationException("This rider phase is intentionally empty. Use another phase to inspect the pose.");
            var live = EnsureSession();
            live.Sample(riderClip, vehicleClip, riderClip.length * inspectTime, vehicleClip ? Mathf.Min(riderClip.length * inspectTime, vehicleClip.length) : 0);
            status = "Sampled authored clips at equal elapsed time. This does not simulate the game's blends, input or IK.";
            SceneView.RepaintAll();
        });
        if (GUILayout.Button("Stop / restore scene")) Run(Stop);
    }
}

public static class RoweStudioGameExport
{
    public static AnimationClip[][] Resolve(RoweHeldTrick set)
    {
        var g = set.game;
        var rider = g.customRider ? g.rider.ToArray() : RoweHeldTrickBuilder.Clips(set);
        if (g.style == RoweTrickStyle.Custom && !g.customRider) { if (g.omitLoop) rider[1] = null; if (g.omitTweak) rider[2] = null; }
        if (g.style == RoweTrickStyle.Loop && !g.customRider) rider[1] = g.motionLoop;
        var bike = set.authorBike ? RoweBikeAnimation.Clips(set) : g.customBike ? g.bike.ToArray() : null;
        var tracks = new[] { rider, bike,
            g.sameRiderMirror ? rider.ToArray() : g.customRiderMirror ? g.riderMirror : null,
            g.customBikeMirror ? g.bikeMirror.ToArray() : set.authorBike && set.sameBikeBothDirections ? bike.ToArray() : null };
        // Work on copies: style switches never erase authored slots or poses.
        for (int i = 0; i < tracks.Length; i++)
        {
            if (tracks[i] == null) continue;
            tracks[i] = tracks[i].ToArray();
            for (int phase = 0; phase < 4; phase++)
                if (!RoweTrickStyles.UsesPhase(g.style, phase)) tracks[i][phase] = null;
        }
        return tracks;
    }

    public static void ValidateClip(AnimationClip clip, bool human)
    {
        if (!clip) return;
        if (clip.legacy || clip.isHumanMotion != human || clip.length <= 0 || AnimationUtility.GetAnimationEvents(clip).Length > 0 || AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0)
            throw new InvalidOperationException("Use a non-legacy " + (human ? "Humanoid rider" : "Generic bike") + " clip without events or object references: " + clip.name);
        string path = AssetDatabase.GetAssetPath(clip);
        if (!path.StartsWith("Assets/CustomClips/", StringComparison.Ordinal) || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Make an editable copy in Assets/CustomClips before exporting: " + clip.name);
        var bindings = AnimationUtility.GetCurveBindings(clip);
        if (bindings.Length == 0) throw new InvalidOperationException("The clip has no curves: " + clip.name);
        if (!human)
        {
            var bike = RowePairedWorkbench.FindRig("Bike Skeleton");
            if (!bike) throw new InvalidOperationException("Open the paired workbench to validate custom bike joint paths.");
            foreach (var b in bindings)
                if (b.type != typeof(Transform) || (b.path.Length > 0 && !bike.transform.Find(b.path)))
                    throw new InvalidOperationException("Missing/unsupported bike binding: " + b.path + " / " + b.propertyName);
        }
    }

    public static string Export(RoweHeldTrick set, string outputRoot)
    {
        string readiness = RoweTrickStyles.Readiness(set);
        if (readiness != null) throw new InvalidOperationException(readiness);
        var g = set.game;
        var sourceTracks = Resolve(set);
        string[] labels = { "Rider", "Bike", "RiderMirror", "BikeMirror" };
        // Validate everything BEFORE creating or changing any export files.
        for (int i = 0; i < 4; i++)
        {
            var source = sourceTracks[i];
            if (source == null) continue;
            if (source.Length != 4 || !source[0] || !source[3]) throw new InvalidOperationException(labels[i] + " needs Enter and Exit.");
            foreach (var clip in source) ValidateClip(clip, i % 2 == 0);
        }
        var package = new RoweAnimationPackage { id = set.clipPrefix, title = set.displayName,
            applyTiming = g.applyTiming, overallSpeed = g.overallSpeed, enterSpeed = g.enterSpeed, loopSpeed = g.loopSpeed,
            tweakSpeed = g.tweakSpeed, exitSpeed = g.exitSpeed, tweakAt = g.tweakAt,
            applyRules = g.applyRules, onlyInAir = g.onlyInAir, allowLandingHold = g.allowLandingHold };
        var tracks = package.Tracks().ToArray();
        var exports = new Dictionary<string, AnimationClip>();
        for (int i = 0; i < 4; i++)
        {
            if (sourceTracks[i] == null) continue;
            tracks[i].replace = true;
            for (int j = 0; j < 4; j++)
            {
                var clip = sourceTracks[i][j];
                if (!clip) continue;
                string name = set.clipPrefix + "_" + labels[i] + RoweHeldTrickBuilder.Suffixes[j];
                tracks[i].clips[j] = name;
                exports.Add(name, clip);
            }
        }
        string error = package.Validate((name, human) => exports.TryGetValue(name, out var c) && c.isHumanMotion == human);
        if (error != null) throw new InvalidOperationException(error);
        string assetFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)).Replace('\\', '/') + "/StudioExport";
        RoweHeldTrickBuilder.EnsureFolder(assetFolder);
        var paths = new List<string>();
        foreach (var pair in exports)
        {
            string path = assetFolder + "/" + pair.Key + ".anim";
            var copy = UnityEngine.Object.Instantiate(pair.Value); copy.name = pair.Key;
            if (g.style == RoweTrickStyle.Loop && pair.Key.EndsWith("_Loop", StringComparison.Ordinal))
            {
                var timing = AnimationUtility.GetAnimationClipSettings(copy);
                timing.loopTime = true; timing.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(copy, timing);
            }
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing) { EditorUtility.CopySerialized(copy, existing); UnityEngine.Object.DestroyImmediate(copy); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); }
            else AssetDatabase.CreateAsset(copy, path);
            paths.Add(path);
        }
        string jsonPath = assetFolder + "/" + RoweAnimationPackage.AssetFileName;
        File.WriteAllText(jsonPath, JsonUtility.ToJson(package, true));
        AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceSynchronousImport);
        paths.Add(jsonPath);
        var dependencies = AssetDatabase.GetDependencies(paths.ToArray(), true);
        if (dependencies.Except(paths).Any()) throw new InvalidOperationException("Unexpected bundle dependencies: " + string.Join(", ", dependencies.Except(paths)));
        string folder = Path.Combine(outputRoot, set.clipPrefix.Substring("RoweMod_Custom_".Length));
        Directory.CreateDirectory(folder);
        string bundle = set.clipPrefix.ToLowerInvariant();
        if (!BuildPipeline.BuildAssetBundles(folder, new[] { new AssetBundleBuild { assetBundleName = bundle, assetNames = paths.ToArray() } }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Unity could not build the Studio package.");
        File.WriteAllText(Path.Combine(folder, "INSTALL.txt"), "RoweMod Animation Studio: " + set.displayName + "\n\nCopy only " + bundle + " to BMX Streets/Mods/rowemod/Bundles with the game closed.\nLoad a map. In RoweMod's trick editor choose an existing base trick, select any clip from this package, then Apply Studio Package. This requires a RoweMod build with Studio Package support.\nPhase slots and opted-in timing/rules are saved to the selected trick's config. Keep tracks preserve the game; empty slots on replaced tracks clear Loop/Tweak. Use Reset Trick Animation to restore captured game defaults.\nGlobal controller input, hold/loop policy, masks and fades remain owned by Streets. No controller hooks are added.\nThe bundle contains only selected plain animation clips and a JSON settings asset. No rigs, paid code or recipe dependencies. Check rights before sharing derived animations.\n");
        return Path.Combine(folder, bundle);
    }
}
