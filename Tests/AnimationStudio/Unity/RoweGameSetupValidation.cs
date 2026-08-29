using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoweMod.AnimationAuthoring;

public static class RoweGameSetupValidation
{
    [Serializable] public class Report
    {
        public bool passed, sourcePreserved, recipeRoundtrip, manifestRoundtrip, onlyClipsAndJson, nativeHold,
            explicitNoTweak, timingAndRules, separateMirrors, customSourcesUntouched, rejectsWrongRig, rejectsMissingBikePath, rejectsMissingExit,
            rejectsBadTiming, legacyStillWorks, stableOutputGuids, restored, bikeBundleRoundtrip;
        public string recipe, bundle, error;
        public int assets;
    }
    static void Require(bool b,string message) {if(!b)throw new InvalidOperationException(message);}
    static bool Reject(Action a) {try{a();return false;}catch(InvalidOperationException){return true;}}
    public static void Run()
    {
        var r=new Report();
        try
        {
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider=RowePairedWorkbench.FindRig("Human Temp");var bike=RowePairedWorkbench.FindRig("Bike Skeleton");
            var scene=new RowePairedWorkbench.Pose(rider.transform,bike.transform);
            string sourcePath="Assets/CustomClips/RoweMod_Custom_My Trick 3.anim";
            string before=File.ReadAllText(sourcePath);
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
            var set=RoweHeldTrickBuilder.Create("Game Setup Validation",source,0,source.length,rider,bike);
            set.game.include=true;set.game.omitLoop=true;set.game.omitTweak=true;
            set.game.applyTiming=true;set.game.overallSpeed=1.25f;set.game.enterSpeed=1.4f;set.game.tweakSpeed=2;set.game.tweakAt=.55f;
            set.game.applyRules=true;set.game.onlyInAir=true;set.game.allowLandingHold=true;
            EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
            r.recipe=AssetDatabase.GetAssetPath(set);
            AssetDatabase.ImportAsset(r.recipe,ImportAssetOptions.ForceSynchronousImport);
            set=AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(r.recipe);
            r.recipeRoundtrip=set.game.omitTweak && set.game.omitLoop && set.game.tweakAt==.55f;
            string output=Path.Combine(RoweAnimationWorkspace.Workspace,"ValidationExports");
            r.bundle=RoweHeldTrickBuilder.Export(set,output);
            var bundle=AssetBundle.LoadFromFile(r.bundle);
            try
            {
                var assets=bundle.LoadAllAssets();r.assets=assets.Length;
                r.onlyClipsAndJson=assets.All(a=>a is AnimationClip || a is TextAsset) && assets.Count(a=>a is TextAsset)==1;
                var p=JsonUtility.FromJson<RoweAnimationPackage>(assets.OfType<TextAsset>().Single().text);
                var clips=assets.OfType<AnimationClip>().ToDictionary(c=>c.name);
                r.manifestRoundtrip=p.Validate((name,human)=>clips.TryGetValue(name,out var c)&&c.isHumanMotion==human)==null;
                r.nativeHold=p.rider.clips[1]==null || p.rider.clips[1]=="";
                r.explicitNoTweak=string.IsNullOrEmpty(p.rider.clips[2]) && string.IsNullOrEmpty(p.riderMirror.clips[2]);
                r.timingAndRules=p.applyTiming && p.overallSpeed==1.25f && p.tweakAt==.55f && p.applyRules && p.allowLandingHold;
                r.separateMirrors=p.riderMirror.replace && p.riderMirror.clips[0]!=p.rider.clips[0] && !p.bike.replace;
            }
            finally{bundle.Unload(true);}
            Debug.Log("STUDIO_AFTER_BUNDLE set="+set+" poses="+set.poses+" path="+AssetDatabase.GetAssetPath(set.poses)+" human="+(set.poses && set.poses.isHumanMotion)+" curves="+(set.poses?AnimationUtility.GetCurveBindings(set.poses).Length:0));
            string exportFolder=Path.GetDirectoryName(r.recipe).Replace('\\','/')+"/StudioExport";
            string[] paths=AssetDatabase.FindAssets("t:AnimationClip",new[]{exportFolder}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            RoweHeldTrickBuilder.Export(set,output);
            r.stableOutputGuids=guids.SequenceEqual(paths.Select(AssetDatabase.AssetPathToGUID));
            set.game.overallSpeed=float.NaN;r.rejectsBadTiming=Reject(()=>RoweHeldTrickBuilder.Export(set,output));set.game.overallSpeed=1;
            set.game.customRider=true;set.game.rider=new[]{source,(AnimationClip)null,(AnimationClip)null,source};
            string customBefore=File.ReadAllText(sourcePath);
            RoweHeldTrickBuilder.Export(set,output);
            r.customSourcesUntouched=customBefore==File.ReadAllText(sourcePath);
            set.game.rider[3]=null;r.rejectsMissingExit=Reject(()=>RoweHeldTrickBuilder.Export(set,output));set.game.rider[3]=source;
            set.game.customBike=true;set.game.bike=new[]{source,(AnimationClip)null,(AnimationClip)null,source};
            r.rejectsWrongRig=Reject(()=>RoweHeldTrickBuilder.Export(set,output));set.game.customBike=false;
            var badBike=new AnimationClip{name="Bad Bike",frameRate=60};
            AnimationUtility.SetEditorCurve(badBike,EditorCurveBinding.FloatCurve("MissingJoint",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,0,1,1));
            string badPath=AssetDatabase.GenerateUniqueAssetPath("Assets/CustomClips/BadBikeValidation.anim");AssetDatabase.CreateAsset(badBike,badPath);
            r.rejectsMissingBikePath=Reject(()=>RoweStudioGameExport.ValidateClip(badBike,false));
            var goodBike=new AnimationClip{name="Bike Validation",frameRate=60};
            AnimationUtility.SetEditorCurve(goodBike,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,0,1,.1f));
            string goodPath=AssetDatabase.GenerateUniqueAssetPath("Assets/CustomClips/BikeValidation.anim");AssetDatabase.CreateAsset(goodBike,goodPath);
            set.game.customBike=true;set.game.customBikeMirror=true;
            set.game.bike=new[]{goodBike,(AnimationClip)null,goodBike,goodBike};set.game.bikeMirror=set.game.bike.ToArray();
            r.bundle=RoweHeldTrickBuilder.Export(set,output);
            bundle=AssetBundle.LoadFromFile(r.bundle);
            try
            {
                var assets=bundle.LoadAllAssets();var p=JsonUtility.FromJson<RoweAnimationPackage>(assets.OfType<TextAsset>().Single().text);
                var clips=assets.OfType<AnimationClip>().ToDictionary(c=>c.name);
                r.bikeBundleRoundtrip=p.bike.replace && p.bikeMirror.replace && p.Validate((name,human)=>clips.TryGetValue(name,out var c)&&c.isHumanMotion==human)==null
                    && clips.Values.Count(c=>!c.isHumanMotion)==6 && string.IsNullOrEmpty(p.bike.clips[1]);
            }
            finally{bundle.Unload(true);}
            set.game.include=false;r.bundle=RoweHeldTrickBuilder.Export(set,output);
            bundle=AssetBundle.LoadFromFile(r.bundle);
            try{var assets=bundle.LoadAllAssets();r.legacyStillWorks=assets.Length==4 && assets.All(a=>a is AnimationClip c && c.isHumanMotion);}
            finally{bundle.Unload(true);}
            r.sourcePreserved=File.ReadAllText(sourcePath)==before;r.restored=scene.Changed()==0;
            r.passed=r.sourcePreserved&&r.recipeRoundtrip&&r.manifestRoundtrip&&r.onlyClipsAndJson&&r.nativeHold&&r.explicitNoTweak&&r.timingAndRules&&r.separateMirrors&&r.customSourcesUntouched&&r.rejectsWrongRig&&r.rejectsMissingBikePath&&r.rejectsMissingExit&&r.rejectsBadTiming&&r.legacyStillWorks&&r.stableOutputGuids&&r.restored&&r.bikeBundleRoundtrip;
            Require(r.passed,"One or more checks failed.");
        }
        catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace,"studio-game-setup-validation.json"),JsonUtility.ToJson(r,true));
        Debug.Log("ROWE_GAME_SETUP_VALIDATION "+JsonUtility.ToJson(r));
        EditorApplication.Exit(r.passed?0:1);
    }
}
