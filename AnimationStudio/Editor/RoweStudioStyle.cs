using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public enum RoweTrickStyle { Custom = 0, Hold = 1, HoldTweak = 2, Loop = 3 }

public static class RoweTrickStyles
{
    public static string Title(RoweTrickStyle style) => style == RoweTrickStyle.Hold ? "Hold a pose" : style == RoweTrickStyle.HoldTweak ? "Hold + Tweak" : style == RoweTrickStyle.Loop ? "Repeat a movement" : "Advanced / existing setup";
    public static string Summary(RoweTrickStyle style) => style == RoweTrickStyle.Hold ? "Enter → hold still → release → Exit. No Loop or Tweak clip."
        : style == RoweTrickStyle.HoldTweak ? "Enter → hold still → optional Tweak → release → Exit. No Loop clip."
        : style == RoweTrickStyle.Loop ? "Enter → repeat Loop → release → Exit. No Tweak clip."
        : "Existing phase assignments are preserved. Choose a style for the guided workflow.";
    public static bool UsesPhase(RoweTrickStyle style, int phase) => style == RoweTrickStyle.Custom || phase == 0 || phase == 3
        || (phase == 1 && style == RoweTrickStyle.Loop) || (phase == 2 && style == RoweTrickStyle.HoldTweak);

    public static void Choose(RoweHeldTrick set, RoweTrickStyle style)
    {
        if (style == RoweTrickStyle.HoldTweak && !set.game.customRider) RoweHeldTrickBuilder.EnableCustomTweak(set);
        Undo.RecordObject(set, "Change trick style");
        set.game.style = style;
        if (style != RoweTrickStyle.Custom) set.game.include = true;
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
    }

    public static string Readiness(RoweHeldTrick set)
    {
        var g = set.game;
        if (g.style == RoweTrickStyle.Custom || g.style == RoweTrickStyle.Hold) return null;
        var tracks = RoweStudioGameExport.Resolve(set);
        int required = g.style == RoweTrickStyle.Loop ? 1 : 2;
        if (!tracks[0][required]) return "Add your " + (required == 1 ? "Loop clip in the Movement step" : "Tweak clip or pose") + " before exporting.";
        if (g.style == RoweTrickStyle.HoldTweak && !g.customRider && (!set.useCustomTweak || !set.tweakPose))
            return "Create or enable your Tweak pose in the Tweak step before exporting.";
        if (g.customRiderMirror && !g.sameRiderMirror && !tracks[2][required]) return "The custom mirrored rider also needs a " + (required == 1 ? "Loop" : "Tweak") + " clip.";
        return null;
    }

    public static AnimationClip MakeLoopCopy(RoweHeldTrick set)
    {
        if (!set.sourceReference) throw new InvalidOperationException("Choose a starting animation first.");
        var copy = UnityEngine.Object.Instantiate(set.sourceReference);
        copy.name = set.clipPrefix + "_Movement";
        var timing = AnimationUtility.GetAnimationClipSettings(copy); timing.loopTime = true; timing.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(copy, timing);
        AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)).Replace('\\', '/');
        AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(folder + "/Movement.anim"));
        Undo.RecordObject(set, "Create movement copy"); set.game.motionLoop = copy;
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
        return copy;
    }

    public static AnimationClip MakeMovementFromHeld(RoweHeldTrick set, float seconds)
    {
        RoweHeldTrickBuilder.ValidateRecipe(set);
        if(!float.IsFinite(seconds)||seconds<.1f||seconds>10)throw new InvalidOperationException("Movement length must be between 0.1 and 10 seconds.");
        var motion=RoweHeldTrickBuilder.MakeTransition(set.poses,1,1,seconds,true);
        motion.name=set.clipPrefix+"_Movement";
        string folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)).Replace('\\','/');
        AssetDatabase.CreateAsset(motion,AssetDatabase.GenerateUniqueAssetPath(folder+"/Movement.anim"));
        Undo.RecordObject(set,"Create movement from Held");set.game.motionLoop=motion;
        EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
        RoweBikeAnimation.EnsureMovement(set);
        return motion;
    }
}

public sealed partial class RoweIKPoseEditor
{
    [SerializeField] RoweTrickStyle newTrickStyle;
    [SerializeField] int createStage;
    [SerializeField] bool createWithBike = true;
    [SerializeField] float newMovementSeconds=1;
    bool showStyleChoices, styleAdvanced;
    bool IsLoopStyle => recipe && recipe.game.style == RoweTrickStyle.Loop;
    bool HasTweakStep => recipe && (recipe.game.style == RoweTrickStyle.HoldTweak || recipe.game.style == RoweTrickStyle.Custom);

    RoweTrickStyle DrawStyleChoices(RoweTrickStyle current)
    {
        EditorGUILayout.LabelField("How should this trick behave?", EditorStyles.boldLabel);
        string[] descriptions = { "Stay in one pose until you release. The simplest option.", "Hold a pose, then exaggerate it with Tweak. Ideal for held tricks.", "Keep repeating an animated movement. No Tweak while looping." };
        EnsureRoweTheme();
        for (int i = 1; i <= 3; i++)
        {
            var choice = (RoweTrickStyle)i;
            if (GUILayout.Button((current == choice ? "✓  " : "") + RoweTrickStyles.Title(choice) + "\n" + descriptions[i - 1], current == choice ? roweSelected : roweChoice, GUILayout.MinHeight(66))) current = choice;
        }
        EditorGUILayout.LabelField("You can change this later without deleting your work.", EditorStyles.wordWrappedMiniLabel);
        return current;
    }

    void DrawSavedStyle()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Style: " + RoweTrickStyles.Title(recipe.game.style), EditorStyles.boldLabel);
            if (GUILayout.Button(showStyleChoices ? "Done" : "Change style", GUILayout.Width(105))) showStyleChoices = !showStyleChoices;
        }
        if (showStyleChoices || recipe.game.style == RoweTrickStyle.Custom)
        {
            var chosen = DrawStyleChoices(recipe.game.style);
            if (chosen != recipe.game.style) Run(() =>
            {
                Commit(); StopWithoutCommit(); RoweTrickStyles.Choose(recipe, chosen);
                if (studioStep == 2 && chosen == RoweTrickStyle.Hold) studioStep = 1;
                showStyleChoices = false; exportedBundle = null;
                status = "Style saved. Your existing poses and clip assignments have been kept.";
            });
            styleAdvanced = EditorGUILayout.Foldout(styleAdvanced, "Advanced / legacy compatibility", true);
            if (styleAdvanced && GUILayout.Button("Use manual phase setup (keep existing assignments)")) Run(() =>
            { Stop(); RoweTrickStyles.Choose(recipe, RoweTrickStyle.Custom); exportedBundle = null; });
        }
        EditorGUILayout.HelpBox(RoweTrickStyles.Summary(recipe.game.style), MessageType.None);
        if (recipe.game.customRider)
            EditorGUILayout.HelpBox("You are using supplied rider phase clips in Game setup. The Riding/Held/Tweak poses do not replace those clips. Turn off 'Use my own phase clips' there to return to generated poses.", MessageType.Info);
    }

    void DrawStudioMovement()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Your repeating movement", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This needs an animation with movement, not just a held pose. Make the first and last frames match so it repeats smoothly. Use Held as the pose where you enter and leave the loop.", MessageType.Info);
        var g = recipe.game;
        EditorGUI.BeginChangeCheck();
        var selectedLoop = (AnimationClip)EditorGUILayout.ObjectField("Loop animation", g.customRider ? g.rider[1] : g.motionLoop, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck()) Run(() =>
        {
            Stop(); Undo.RecordObject(recipe, "Choose movement loop");
            if (g.customRider) g.rider[1] = selectedLoop; else g.motionLoop = selectedLoop;
            EditorUtility.SetDirty(recipe); AssetDatabase.SaveAssetIfDirty(recipe); exportedBundle = null;
        });
        if (!selectedLoop && !g.customRider)
        {
            newMovementSeconds=EditorGUILayout.Slider("New movement length",newMovementSeconds,.1f,10);
            if(RowePrimaryButton("Start a new movement from Held"))Run(()=>{Stop();RoweTrickStyles.MakeMovementFromHeld(recipe,newMovementSeconds);StartPairedMovement();exportedBundle=null;status="Blank movement created. Pose keys on the shared timeline or add a bike full turn.";});
            if(GUILayout.Button("Or copy my starting animation",GUILayout.Height(30)))Run(()=>{Stop();RoweTrickStyles.MakeLoopCopy(recipe);exportedBundle=null;});
        }
        using (new EditorGUI.DisabledScope(!selectedLoop))
        {
            if (GUILayout.Button("Match Held pose to the first loop frame")) Run(() =>
            {
                Stop(); var live = EnsureSession(); live.Sample(selectedLoop, null, 0);
                var pose = live.Capture(1);
                try { Undo.RecordObject(recipe.poses, "Match loop starting pose"); RoweIKKeys.Write(recipe.poses, pose, 1, false); AssetDatabase.SaveAssetIfDirty(recipe.poses); }
                finally { UnityEngine.Object.DestroyImmediate(pose); StopWithoutCommit(); }
                status = "Held now matches the loop start. Match the final loop frame too to avoid a jump.";
            });
        }
        DrawPairedMovement();
        EditorGUILayout.HelpBox("The game must allow Loop playback on the chosen base trick. Tweak is removed from this export; its saved pose stays available if you switch styles.", MessageType.None);
    }
}
