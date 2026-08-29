using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoweMod.AnimationAuthoring;

public static class RoweStyleValidation
{
    [Serializable] public class Report
    {
        public bool passed, legacyUnchanged, holdSlots, tweakSlots, loopSlots, sourcePreserved,
            editsPreserved, persists, missingLoopBlocked, missingMirrorBlocked, previewLoops, previewReleases,
            holdCannotTweak, sceneRestored, copiedLoopFlag;
        public string error;
    }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Advance(RoweIKPoseEditor editor, int frames)
    {
        for(int i=0;i<frames;i++)
        {
            editor.GetType().GetField("lastTick", Private).SetValue(editor, EditorApplication.timeSinceStartup-.2);
            Call(editor, "TickHeldPreview");
        }
    }
    static bool CheckExport(RoweHeldTrick set, bool loop, bool tweak)
    {
        var path=RoweHeldTrickBuilder.Export(set, Path.Combine(RoweAnimationWorkspace.Workspace,"ValidationExports"));
        var bundle=AssetBundle.LoadFromFile(path);
        try
        {
            var assets=bundle.LoadAllAssets();
            var p=JsonUtility.FromJson<RoweAnimationPackage>(assets.OfType<TextAsset>().Single().text);
            var clips=assets.OfType<AnimationClip>().ToDictionary(c=>c.name);
            return p.Validate((n,h)=>clips.TryGetValue(n,out var c)&&c.isHumanMotion==h)==null
                && !string.IsNullOrEmpty(p.rider.clips[1])==loop && !string.IsNullOrEmpty(p.rider.clips[2])==tweak
                && !string.IsNullOrEmpty(p.riderMirror.clips[1])==loop && !string.IsNullOrEmpty(p.riderMirror.clips[2])==tweak
                && assets.Length==(loop||tweak?7:5)
                && (!loop || AnimationUtility.GetAnimationClipSettings(clips[p.rider.clips[1]]).loopTime);
        }
        finally {bundle.Unload(true);}
    }
    public static void Run()
    {
        var r=new Report(); RoweIKPoseEditor editor=null;
        try
        {
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider=RowePairedWorkbench.FindRig("Human Temp"); var bike=RowePairedWorkbench.FindRig("Bike Skeleton");
            var scene=new RowePairedWorkbench.Pose(rider.transform,bike.transform);
            const string sourcePath="Assets/CustomClips/RoweMod_Custom_My Trick 3.anim";
            string before=File.ReadAllText(sourcePath);
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
            var set=RoweHeldTrickBuilder.Create("Style Validation",source,0,source.length,rider,bike);
            r.legacyUnchanged=set.game.style==RoweTrickStyle.Custom && RoweStudioGameExport.Resolve(set)[0][2]==set.tweak;
            RoweTrickStyles.Choose(set,RoweTrickStyle.Hold); r.holdSlots=CheckExport(set,false,false);
            RoweTrickStyles.Choose(set,RoweTrickStyle.HoldTweak); var savedTweak=set.tweakPose;
            r.tweakSlots=CheckExport(set,false,true);
            RoweTrickStyles.Choose(set,RoweTrickStyle.Loop);
            r.missingLoopBlocked=RoweTrickStyles.Readiness(set)!=null;
            RoweTrickStyles.MakeLoopCopy(set); var loop=set.game.motionLoop;
            r.copiedLoopFlag=loop!=source && AnimationUtility.GetAnimationClipSettings(loop).loopTime;
            r.loopSlots=CheckExport(set,true,false);
            set.game.sameRiderMirror=false;set.game.customRiderMirror=true;set.game.riderMirror=new[]{set.enter,(AnimationClip)null,set.tweak,set.exit};
            r.missingMirrorBlocked=RoweTrickStyles.Readiness(set)!=null;
            set.game.sameRiderMirror=true;set.game.customRiderMirror=false;
            RoweTrickStyles.Choose(set,RoweTrickStyle.Hold);
            r.editsPreserved=set.tweakPose==savedTweak && set.game.motionLoop==loop && set.game.riderMirror[2]==set.tweak;
            editor=RoweIKPoseEditor.OpenRecipe(set);Call(editor,"StartHeldPreview");Advance(editor,15);Call(editor,"StartTweakPreview");
            r.holdCannotTweak=Get<int>(editor,"previewPhase")==1;Call(editor,"Stop");
            RoweTrickStyles.Choose(set,RoweTrickStyle.Loop);
            string recipePath=AssetDatabase.GetAssetPath(set);AssetDatabase.ImportAsset(recipePath,ImportAssetOptions.ForceSynchronousImport);
            set=AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(recipePath);
            r.persists=set.game.style==RoweTrickStyle.Loop && set.game.motionLoop==loop && set.tweakPose==savedTweak;
            editor=RoweIKPoseEditor.OpenRecipe(set);Call(editor,"StartHeldPreview");Advance(editor,200);
            r.previewLoops=Get<int>(editor,"previewPhase")==6 && Get<bool>(editor,"studioRunning");
            Call(editor,"ReleaseHeldPreview");Advance(editor,50);r.previewReleases=!Get<bool>(editor,"studioRunning");Call(editor,"Stop");
            r.sourcePreserved=File.ReadAllText(sourcePath)==before;r.sceneRestored=scene.Changed()==0;
            r.passed=r.legacyUnchanged&&r.holdSlots&&r.tweakSlots&&r.loopSlots&&r.sourcePreserved&&r.editsPreserved&&r.persists&&r.missingLoopBlocked&&r.missingMirrorBlocked&&r.previewLoops&&r.previewReleases&&r.holdCannotTweak&&r.sceneRestored&&r.copiedLoopFlag;
            if(!r.passed)throw new InvalidOperationException("One or more style checks failed.");
        }
        catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}
        finally{if(editor)editor.Close();}
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace,"studio-style-validation.json"),JsonUtility.ToJson(r,true));
        Debug.Log("ROWE_STYLE_VALIDATION "+JsonUtility.ToJson(r));EditorApplication.Exit(r.passed?0:1);
    }
}
