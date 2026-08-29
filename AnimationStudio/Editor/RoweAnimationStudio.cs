using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class RoweIKPoseEditor
{
    [SerializeField] bool guided = true;
    [SerializeField] RoweHeldTrick recipe;
    [SerializeField] Animator studioRider, studioBike;
    [SerializeField] AnimationClip importClip;
    [SerializeField] string trickTitle = "My Held Trick";
    [SerializeField] float importNormalTime, importHeldTime = -1;
    [SerializeField] int studioStep = 1, poseChoice = 1;
    [SerializeField] int studioLayoutVersion;
    [SerializeField] bool studioSetup;
    bool studioRunning;
    int previewPhase;
    bool exitAfterTweakReturn;
    const int PreviewEnter = 0, PreviewHold = 1, PreviewTweak = 2, PreviewExit = 3, PreviewTweaked = 4, PreviewReturn = 5, PreviewLoop = 6;
    const int GameSetupStep = 3, PreviewStep = 4;
    float previewSeconds;
    string exportedBundle;
    Vector2 studioScroll;
    AnimationClip previewMovement, previewBikeMovement;

    public static RoweIKPoseEditor OpenRecipe(RoweHeldTrick set)
    {
        Open();
        var window = GetWindow<RoweIKPoseEditor>();
        window.Run(() => { window.Stop(); window.recipe = set; window.guided = true; window.studioStep = 1; window.poseChoice = 1; window.clip = set.poses; window.seconds = 1; });
        window.Repaint();
        return window;
    }

    void DrawStudio()
    {
        studioScroll = EditorGUILayout.BeginScrollView(studioScroll);
        EditorGUILayout.Space(8);
        DrawRoweHeader();
        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            var opened = (RoweHeldTrick)EditorGUILayout.ObjectField("Saved trick", recipe, typeof(RoweHeldTrick), false);
            if (EditorGUI.EndChangeCheck()) Run(() => { Stop(); recipe = opened; if (recipe) SelectStudioPoseClip(poseChoice); exportedBundle = null; });
            if (GUILayout.Button("New", GUILayout.Width(60))) Run(() => { Stop(); recipe = null; importClip = clip; importHeldTime = -1; createStage = 0; newTrickStyle = RoweTrickStyle.Custom; studioScroll = Vector2.zero; });
        }

        studioSetup = EditorGUILayout.Foldout(studioSetup, "Rig setup / sharing", true);
        if (studioSetup)
        {
            using (new EditorGUI.DisabledScope(session != null))
            {
                studioRider = (Animator)EditorGUILayout.ObjectField("Rider (Humanoid)", studioRider, typeof(Animator), true);
                studioBike = (Animator)EditorGUILayout.ObjectField("Bike animation rig", studioBike, typeof(Animator), true);
            }
            if (GUILayout.Button("Use workbench rider + bike")) Run(() =>
            {
                Stop(); RoweStudioRigs.UseWorkbench(ref studioRider, ref studioBike);
                status = "Rider: " + studioRider.name + " | Bike: " + studioBike.name;
            });
            DrawPreviewCharacter();
            EditorGUILayout.HelpBox("The local workbench is detected automatically. Other creators can assign their own Humanoid rig. Final IK and Baker must be installed under their own licence. Game rigs and paid source files are not included in the tools-only export.", MessageType.Info);
            if (GUILayout.Button("Export editor tools only…")) Run(() =>
            {
                string folder = EditorUtility.OpenFolderPanel("Export Animation Studio tools", RoweAnimationWorkspace.Workspace, "");
                if (folder.Length == 0) return;
                string path = RoweHeldTrickBuilder.ExportTool(folder);
                status = "Tools exported without Final IK or game assets: " + path;
                EditorUtility.RevealInFinder(path);
            });
        }

        if (!recipe)
        {
            DrawCreateHeldTrick();
            EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView(); return;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(recipe.displayName, EditorStyles.largeLabel);
        DrawSavedStyle();
        int[] steps = recipe.game.style == RoweTrickStyle.Hold ? new[] { 0, 1, GameSetupStep, PreviewStep } : new[] { 0, 1, 2, GameSetupStep, PreviewStep };
        string[] titles = { "Riding", "Core Hold", IsLoopStyle ? "Movement" : "Tweak", "Game setup", "Export" };
        int selectedStep = Mathf.Max(0, Array.IndexOf(steps, studioStep));
        EditorGUILayout.LabelField("STEP " + (selectedStep + 1) + " / " + steps.Length + "  ·  " + titles[studioStep], EditorStyles.boldLabel);
        using(new EditorGUILayout.VerticalScope(roweCard))
        {
        if (studioStep < 2) DrawStudioPose();
        else if (studioStep == 2) { if (IsLoopStyle) DrawStudioMovement(); else DrawStudioTweak(); }
        else if (studioStep == GameSetupStep) DrawStudioGameSetup();
        else DrawStudioExport();
        }
        DrawWizardNavigation(steps,titles);
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    void DrawCreateHeldTrick()
    {
        EditorGUILayout.Space(12);
        if(createStage==0)
        {
            var picked=DrawStyleChoices(RoweTrickStyle.Custom);
            if(picked!=RoweTrickStyle.Custom){newTrickStyle=picked;createStage=1;studioScroll=Vector2.zero;Repaint();}
            return;
        }
        if(GUILayout.Button("← Back",GUILayout.Width(95))) {createStage--;studioScroll=Vector2.zero;Repaint();return;}
        EditorGUILayout.LabelField("Style: "+RoweTrickStyles.Title(newTrickStyle),EditorStyles.boldLabel);
        if(createStage==1)
        {
            EditorGUILayout.LabelField("What would you like to animate?",roweTitle,GUILayout.Height(30));
            if(GUILayout.Button("Rider + bike\nBody IK, bike gizmos and contacts in the same workspace.",roweChoice,GUILayout.MinHeight(76))) {createWithBike=true;createStage=2;Repaint();}
            if(GUILayout.Button("Rider only\nMake a body animation and keep the game's bike clips.",roweChoice,GUILayout.MinHeight(76))) {createWithBike=false;createStage=2;Repaint();}
            return;
        }
        EditorGUILayout.LabelField("Start with an animation you already made", EditorStyles.boldLabel);
        if (!importClip && clip) { importClip = clip; importHeldTime = clip.length; }
        if (!importClip && source) { importClip = source; importHeldTime = source.length; }
        trickTitle = EditorGUILayout.TextField("Trick name", trickTitle);
        EditorGUI.BeginChangeCheck();
        importClip = (AnimationClip)EditorGUILayout.ObjectField("Starting animation", importClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck()) { importNormalTime = 0; importHeldTime = importClip ? importClip.length : 0; }
        if (importHeldTime < 0 && importClip) importHeldTime = importClip.length;
        importNormalTime = EditorGUILayout.Slider("Riding pose at", importNormalTime, 0, importClip ? importClip.length : 1);
        importHeldTime = EditorGUILayout.Slider("Held pose at", importHeldTime, 0, importClip ? importClip.length : 1);
        EditorGUILayout.HelpBox("For your two-pose animation, use the first frame for Riding and the last frame for Held. This makes a separate saved trick; it never edits the source animation.", MessageType.Info);
        if (newTrickStyle == RoweTrickStyle.Custom) EditorGUILayout.HelpBox("Choose one of the three styles above to continue.", MessageType.Info);
        using (new EditorGUI.DisabledScope(newTrickStyle == RoweTrickStyle.Custom || !importClip || !importClip.isHumanMotion || string.IsNullOrWhiteSpace(trickTitle)))
            if (RowePrimaryButton("Create my trick →")) Run(() =>
            {
                Stop();
                ResolveStudioRigs(createWithBike);
                if(createWithBike)RoweBikeAnimation.FindJoints(studioBike);
                recipe = RoweHeldTrickBuilder.Create(trickTitle, importClip, importNormalTime, importHeldTime,
                    studioRider, studioBike);
                RoweTrickStyles.Choose(recipe, newTrickStyle);
                if(createWithBike)RoweBikeAnimation.Enable(recipe,studioRider,studioBike);
                studioStep = 0; BeginStudioPose(0);studioScroll=Vector2.zero;
                status = "Created " + recipe.displayName + " — " + RoweTrickStyles.Title(newTrickStyle) + ". Follow the steps above.";
            });
    }

    void BeginStudioPose(int which)
    {
        Commit(); studioRunning = false; playing = false;
        SelectStudioPoseClip(which);
        Sample();
    }

    void SelectStudioPoseClip(int which)
    {
        poseChoice = Mathf.Clamp(which, 0, 2);
        if(poseChoice==2)
        {
            if(!recipe.tweakPose)RoweHeldTrickBuilder.EnableCustomTweak(recipe);
            RoweTweakFromHold.Synchronize(recipe);
        }
        clip = poseChoice == 2 && recipe.tweakPose ? recipe.tweakPose : recipe.poses;
        bikeClip = null;
        seconds = clip == recipe.tweakPose ? 0 : Mathf.Min(poseChoice, 1);
    }

    void DrawStudioTweak()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("Tweak starts from your core Hold, for both rider and bike. Make the exaggerated changes here. Untouched Tweak poses follow Hold edits; once you edit a Tweak, it is kept.",MessageType.None);
        if (!recipe.tweakPose)
        {
            EditorGUILayout.HelpBox("Give this trick a separate, exaggerated pose. Start with an exact copy of Held, then move the body controls. Riding and Held are never changed.", MessageType.Info);
            if (GUILayout.Button("Create Tweak pose from Held", GUILayout.Height(42))) Run(() =>
            {
                Stop(); RoweHeldTrickBuilder.EnableCustomTweak(recipe); BeginStudioPose(2);
                status = "Tweak starts as Held. Move a hand, foot, hips, elbow or knee to make it different.";
            });
            return;
        }
        EditorGUI.BeginChangeCheck();
        bool enabled = recipe.game.style == RoweTrickStyle.Custom ? EditorGUILayout.ToggleLeft("Use my custom Tweak pose", recipe.useCustomTweak) : recipe.useCustomTweak;
        if (EditorGUI.EndChangeCheck()) Run(() =>
        {
            Stop();
            if (enabled) RoweHeldTrickBuilder.EnableCustomTweak(recipe);
            else { Undo.RecordObject(recipe, "Disable custom Tweak"); recipe.useCustomTweak = false; EditorUtility.SetDirty(recipe); AssetDatabase.SaveAssetIfDirty(recipe); }
        });
        if (recipe.useCustomTweak)
        {
            if(GUILayout.Button("Restart Tweak from core Hold",GUILayout.Height(32)))Run(()=>
            {
                if(!EditorUtility.DisplayDialog("Restart Tweak?","Replace the current rider and bike Tweak with the latest core Hold? Your core Hold is unchanged. Ctrl+Z can undo this restart.","Restart from Hold","Cancel"))return;
                Stop();RoweTweakFromHold.Restart(recipe);BeginStudioPose(2);exportedBundle=null;
                status="Tweak now starts from your current core Hold. Adjust only what should be exaggerated.";
            });
            DrawStudioPose();
        }
        else
        {
            EditorGUILayout.HelpBox("Your Tweak pose is saved but disabled. Enable it to use this style, or choose Hold a pose to leave Tweak out.", MessageType.Info);
            if (GUILayout.Button("Enable my saved Tweak pose")) Run(() => { Stop(); RoweHeldTrickBuilder.EnableCustomTweak(recipe); });
        }
    }

    void DrawStudioPose()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(studioStep == 0 ? "How the rider starts and finishes" : studioStep == 2 ? "Your exaggerated Tweak pose" : "The pose to keep while holding the trick", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(studioStep == 0 ? "This pose is used before Enter and after Exit. Keep it close to the game's normal riding pose." : studioStep == 2 ? "Edit only what changes from Held. Export builds a smooth Held → Tweak transition. The game decides when Tweak triggers." : "Pose the body once. The editor automatically builds Enter, a steady Hold, and Exit around it.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(session == null ? "Start posing" : "Reload saved pose", GUILayout.Height(32))) Run(() => BeginStudioPose(studioStep));
            if (GUILayout.Button("Frame rider", GUILayout.Height(32))) Run(() =>
            {
                if (session == null) BeginStudioPose(studioStep);
                SceneView.lastActiveSceneView?.Frame(new Bounds(session.bones[4].position, Vector3.one * 2), false);
            });
            if (GUILayout.Button("Save rider + bike pose", GUILayout.Height(32))) Run(() => { if (session == null) BeginStudioPose(studioStep); pending = true; pendingBike = CurrentBikeWorking; Commit(); });
        }
        using (new EditorGUI.DisabledScope(session == null))
        {
            selected = Mathf.Clamp(selected, 0, RoweIKSession.Labels.Length - 1);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("RIDER / MOVE & ROTATE", EditorStyles.miniBoldLabel);
            ControlRow(0, 1, 4); ControlRow(2, 3);
            EditorGUILayout.LabelField("AIM ELBOWS / KNEES", EditorStyles.miniBoldLabel);
            ControlRow(5, 6); ControlRow(7, 8);
            if (!bikeSelected && selected < 5) handleMode = RoweControlTabs(handleMode,"Move","Rotate");
            else if(!bikeSelected) EditorGUILayout.HelpBox("Drag the bend control around the limb. Hands/feet stay planted; a straight limb needs bending first.", MessageType.None);
            autoKey = EditorGUILayout.ToggleLeft("Save automatically when I release a handle", autoKey);
        }
        EditorGUILayout.LabelField("Blue = left   /   Orange = right   /   Yellow = hips", EditorStyles.miniLabel);
        DrawWristControls();
        DrawBikeControls();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Stop / restore scene")) Run(Stop);
            if (GUILayout.Button("Preview held trick", GUILayout.Height(32))) Run(() => { Commit(); StopWithoutCommit(); studioStep = PreviewStep; });
        }
    }

    void DrawStudioExport()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(studioRunning))
        {
            EditorGUI.BeginChangeCheck();
            float enter = EditorGUILayout.Slider("Time to reach pose", recipe.enterSeconds, .05f, 3);
            float tweak = recipe.tweakSeconds;
            if (HasTweakStep && recipe.useCustomTweak) tweak = EditorGUILayout.Slider("Time to Tweak", recipe.tweakSeconds, .05f, 3);
            float exit = EditorGUILayout.Slider("Time to return", recipe.exitSeconds, .05f, 3);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(recipe, "Change held trick timing");
                recipe.enterSeconds = enter; recipe.tweakSeconds = tweak; recipe.exitSeconds = exit;
                EditorUtility.SetDirty(recipe); AssetDatabase.SaveAssetIfDirty(recipe);
            }
        }
        EditorGUILayout.HelpBox(IsLoopStyle ? "Preview Enter, your repeating movement, then Release to return. Match the loop's first/last frames and Held pose to avoid jumps." : "Hold lasts as long as the input is held—not a fixed two seconds. Preview it, then press Release to return to the riding pose.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(IsLoopStyle ? "Preview: enter & loop" : "Preview: enter & hold", GUILayout.Height(38))) Run(StartHeldPreview);
            using (new EditorGUI.DisabledScope(!studioRunning || previewPhase == PreviewEnter || previewPhase == PreviewExit || previewPhase == PreviewReturn))
                if (GUILayout.Button("Release → exit", GUILayout.Height(38))) Run(ReleaseHeldPreview);
            if (GUILayout.Button("Stop", GUILayout.Width(65), GUILayout.Height(38))) Run(Stop);
        }
        if (HasTweakStep) using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!studioRunning || previewPhase != PreviewHold || !recipe.useCustomTweak))
                if (GUILayout.Button("Preview Tweak", GUILayout.Height(32))) Run(StartTweakPreview);
            using (new EditorGUI.DisabledScope(!studioRunning || (previewPhase != PreviewTweak && previewPhase != PreviewTweaked)))
                if (GUILayout.Button("Back to Held", GUILayout.Height(32))) Run(() => ReturnFromTweak(false));
        }
        string phaseLabel = previewPhase == PreviewLoop ? "LOOPING — Release when ready" : previewPhase == PreviewEnter ? "ENTERING…" : previewPhase == PreviewHold ? (HasTweakStep ? "HOLDING — Tweak or Release" : "HOLDING — Release when ready") : previewPhase == PreviewTweak ? "MOVING INTO TWEAK…" : previewPhase == PreviewTweaked ? "TWEAKED — Back to Held or Release" : previewPhase == PreviewReturn ? "RETURNING TO HELD…" : "EXITING…";
        EditorGUILayout.LabelField(studioRunning ? phaseLabel : "Preview stopped", EditorStyles.boldLabel);
        if (HasTweakStep && recipe.useCustomTweak) EditorGUILayout.HelpBox("Preview returns from Tweak to Held before Exit. This reverse transition is a preview aid, not an extra exported clip; Streets uses its own release and blending rules.", MessageType.None);
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Ready for RoweMod", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(recipe.game.style != RoweTrickStyle.Custom ? "Style: " + RoweTrickStyles.Title(recipe.game.style) + "\n" + RoweTrickStyles.Summary(recipe.game.style) + "\nThe package applies these phase choices automatically; unused clips are left out. Game input, blends and base-trick hold/loop permissions remain native." : recipe.game.include ? "Studio package: exports your manual phase assignments and opted-in timing/rules." : "Legacy clip-only bundle: exports four generated rider clips without Game setup.", MessageType.None);
        string problem = RoweTrickStyles.Readiness(recipe);
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        using (new EditorGUI.DisabledScope(problem != null)) if (RowePrimaryButton("Build animation bundle…")) Run(() =>
        {
            Commit(); StopWithoutCommit();
            string folder = EditorUtility.OpenFolderPanel("Where should the animation bundle go?", Path.Combine(RoweAnimationWorkspace.Workspace, "BundleOutput"), "");
            if (folder.Length == 0) return;
            exportedBundle = RoweHeldTrickBuilder.Export(recipe, folder);
            status = "Bundle built. Nothing installed into Streets. Installation steps are beside it in INSTALL.txt.";
            EditorUtility.RevealInFinder(exportedBundle);
        });
        if (!string.IsNullOrEmpty(exportedBundle)) EditorGUILayout.SelectableLabel(exportedBundle, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(34));
        EditorGUILayout.HelpBox("In Streets: select a base trick in Custom Animation Clips, then Apply Studio Package (or Apply Complete Held Trick for a legacy bundle). Game setup is applied once and saved; game input and blending still control playback.", MessageType.None);
        if (GUILayout.Button("Save / rebuild phase clips only")) Run(() => { Commit(); StopWithoutCommit(); RoweHeldTrickBuilder.Rebuild(recipe); status = "Saved all four phase clips. Source animation is unchanged."; });
    }

    void StartHeldPreview()
    {
        Commit(); StopWithoutCommit();
        RoweHeldTrickBuilder.Rebuild(recipe);
        if (IsLoopStyle)
        {
            string problem = RoweTrickStyles.Readiness(recipe);
            if (problem != null) throw new InvalidOperationException(problem);
            RoweStudioGameExport.ValidateClip(RoweStudioGameExport.Resolve(recipe)[0][1], true);
            var phases = RoweStudioGameExport.Resolve(recipe);
            previewMovement = phases[0][1];
            previewBikeMovement = phases[1] == null ? null : phases[1][1];
        }
        EnsureSession();
        studioRunning = true; playing = true; previewPhase = PreviewEnter; previewSeconds = 0; exitAfterTweakReturn = false;
        lastTick = EditorApplication.timeSinceStartup;
        SampleHeldPreview();
    }

    void SampleHeldPreview()
    {
        bool tweaking = previewPhase == PreviewTweak || previewPhase == PreviewTweaked || previewPhase == PreviewReturn;
        var riderClip = previewPhase == PreviewLoop ? previewMovement : previewPhase == PreviewEnter ? recipe.enter : previewPhase == PreviewExit ? recipe.exit : tweaking ? recipe.tweak : recipe.loop;
        var bikePreview = previewPhase == PreviewLoop && previewBikeMovement ? previewBikeMovement : previewPhase == PreviewExit ? recipe.bikeExitPreview : recipe.bikeEnterPreview;
        float bikeTime = !bikePreview ? 0 : previewPhase == PreviewLoop && previewBikeMovement ? Mathf.Min(previewSeconds, bikePreview.length) : previewPhase == PreviewHold || previewPhase == PreviewLoop || tweaking ? bikePreview.length : previewSeconds / riderClip.length * bikePreview.length;
        if (recipe.authorBike)
        {
            bikePreview = previewPhase == PreviewLoop ? previewBikeMovement : recipe.bikePhases[previewPhase == PreviewEnter ? 0 : previewPhase == PreviewExit ? 3 : tweaking ? 2 : 1];
            bikeTime = previewSeconds;
        }
        EnsureSession().Sample(riderClip, bikePreview, previewSeconds, bikeTime);
        SceneView.RepaintAll();
    }

    void ReleaseHeldPreview()
    {
        if (!studioRunning) return;
        if (previewPhase == PreviewTweak || previewPhase == PreviewTweaked) { ReturnFromTweak(true); return; }
        if (previewPhase != PreviewHold && previewPhase != PreviewLoop) return;
        previewPhase = PreviewExit; previewSeconds = 0;
        lastTick = EditorApplication.timeSinceStartup;
        SampleHeldPreview();
    }

    void StartTweakPreview()
    {
        if (!studioRunning || previewPhase != PreviewHold || !recipe.useCustomTweak || !HasTweakStep) return;
        previewPhase = PreviewTweak; previewSeconds = 0; exitAfterTweakReturn = false;
        lastTick = EditorApplication.timeSinceStartup; SampleHeldPreview();
    }

    void ReturnFromTweak(bool thenExit)
    {
        if (!studioRunning || (previewPhase != PreviewTweak && previewPhase != PreviewTweaked)) return;
        // Rewind the existing Tweak clip; no continuous solver or extra export.
        previewPhase = PreviewReturn; exitAfterTweakReturn = thenExit;
        lastTick = EditorApplication.timeSinceStartup; SampleHeldPreview();
    }

    void TickHeldPreview()
    {
        if (!studioRunning) return;
        if (session == null || !session.rider || !AnimationMode.InAnimationMode() || !recipe) { StopWithoutCommit(); return; }
        if (previewPhase == PreviewHold || previewPhase == PreviewTweaked) return; // No resampling or solving while held.
        double now = EditorApplication.timeSinceStartup;
        float delta = (float)Math.Min(.1, Math.Max(0, now - lastTick)); lastTick = now;
        if (previewPhase == PreviewLoop)
        {
            var movement = previewMovement;
            if (!movement || movement.length <= 0) { StopWithoutCommit(); return; }
            previewSeconds = Mathf.Repeat(previewSeconds + delta, movement.length); SampleHeldPreview(); return;
        }
        if (previewPhase == PreviewReturn)
        {
            previewSeconds = Mathf.Max(0, previewSeconds - delta); SampleHeldPreview();
            if (previewSeconds == 0)
            {
                previewPhase = PreviewHold; SampleHeldPreview();
                if (exitAfterTweakReturn) ReleaseHeldPreview();
            }
            return;
        }
        previewSeconds += delta;
        float end = previewPhase == PreviewEnter ? recipe.enter.length : previewPhase == PreviewTweak ? recipe.tweak.length : recipe.exit.length;
        if (previewSeconds >= end)
        {
            previewSeconds = end; SampleHeldPreview();
            if (previewPhase == PreviewEnter) { previewPhase = IsLoopStyle ? PreviewLoop : PreviewHold; previewSeconds = 0; SampleHeldPreview(); }
            else if (previewPhase == PreviewTweak) { previewPhase = PreviewTweaked; SampleHeldPreview(); }
            else
            {
                studioRunning = false; playing = false;
                clip = recipe.poses; seconds = 0; poseChoice = 0;
                status = "Preview finished: back in the riding pose. Stop restores the scene.";
            }
        }
        else SampleHeldPreview();
    }
}
