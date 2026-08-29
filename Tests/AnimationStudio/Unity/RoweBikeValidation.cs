using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoweMod.AnimationAuthoring;

// Run only in the isolated workbench validation project; creates separate recipes.
public static class RoweBikeValidation
{
    [Serializable] public class Report
    {
        public bool passed, joints, genericKeys, handTargetsFollow, savedPair, releasedContacts,
            pedalTargetsFollow, wholeBikeCarries, rebuildKeepsGuids, pairedExport, pairedTimeline,
            fullTurn, fullTurnUndo, linkedTurn, sourcePreserved, sceneRestored, loopExport, pinnedHipsAndContacts, pinnedHipsSaved;
        public float handReachError, savedHandError, turnBakeSeconds, pinnedHipError, pinnedContactError;
        public string error, recipePath;
    }
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Move(RoweIKPoseEditor editor,int index,Quaternion rotation)
    {
        var joint=Get<RoweIKSession>(editor,"session").bikeJoints[index];
        Call(editor,"MoveBikeControl",index,joint.position,rotation);
    }
    static Quaternion Rotation(AnimationClip clip,int part,float time)
    {
        float[] q=new[]{"x","y","z","w"}.Select(a=>AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(RoweBikeAnimation.Paths[part],typeof(Transform),"m_LocalRotation."+a)).Evaluate(time)).ToArray();
        return new Quaternion(q[0],q[1],q[2],q[3]).normalized;
    }
    static bool PhysicalFullTurn(RoweIKSession session,AnimationClip rider,AnimationClip before,AnimationClip after)
    {
        foreach(float time in new[]{.25f,.5f,.75f,1f})
        {
            session.Sample(rider,before,time,time);
            var frame=session.bikeJoints[RoweBikeAnimation.FrameIndex];
            var startPosition=frame.position;var startRotation=frame.rotation;
            RoweBikeAnimation.SteeringReference(session.bikeJoints,out var pivot,out var basis);
            var delta=Quaternion.AngleAxis(360*time/after.length,basis*Vector3.up);
            var expectedPosition=pivot+delta*(startPosition-pivot);var expectedRotation=delta*startRotation;
            session.Sample(rider,after,time,time);
            if(Vector3.Distance(frame.position,expectedPosition)>.0002f || Quaternion.Angle(frame.rotation,expectedRotation)>.1f)return false;
        }
        return true;
    }
    static bool CheckExport(RoweHeldTrick set,bool loop)
    {
        string path=RoweHeldTrickBuilder.Export(set,Path.Combine(RoweAnimationWorkspace.Workspace,"ValidationExports"));
        var bundle=AssetBundle.LoadFromFile(path);
        try
        {
            var assets=bundle.LoadAllAssets();var p=JsonUtility.FromJson<RoweAnimationPackage>(assets.OfType<TextAsset>().Single().text);
            var clips=assets.OfType<AnimationClip>().ToDictionary(c=>c.name);
            return assets.Length==13 && p.Validate((n,h)=>clips.TryGetValue(n,out var c)&&c.isHumanMotion==h)==null
                && p.bike!=null && p.bikeMirror!=null && !string.IsNullOrEmpty(p.bike.clips[loop?1:2])
                && string.IsNullOrEmpty(p.bike.clips[loop?2:1]) && clips[p.bike.clips[0]].length==clips[p.rider.clips[0]].length;
        }
        finally{bundle.Unload(true);}
    }
    public static void Run()
    {
        var r=new Report();RoweIKPoseEditor editor=null;
        try
        {
            EditorSceneManager.OpenScene(RowePairedWorkbench.ScenePath);
            var rider=RowePairedWorkbench.FindRig("Human Temp");var bike=RowePairedWorkbench.FindRig("Bike Skeleton");
            var original=new RowePairedWorkbench.Pose(rider.transform,bike.transform);
            const string sourcePath="Assets/CustomClips/RoweMod_Custom_My Trick 3.anim";
            string before=File.ReadAllText(sourcePath);var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
            var set=RoweHeldTrickBuilder.Create("Bike Wizard Validation",source,0,0,rider,bike);
            r.recipePath=AssetDatabase.GetAssetPath(set);
            RoweTrickStyles.Choose(set,RoweTrickStyle.HoldTweak);RoweBikeAnimation.Enable(set,rider,bike);
            r.joints=RoweBikeAnimation.FindJoints(bike).Length==8;
            r.genericKeys=!set.bikePoses.isHumanMotion && AnimationUtility.GetCurveBindings(set.bikePoses).Length==80 && AnimationUtility.GetAnimationEvents(set.bikePoses).Length==0;
            editor=RoweIKPoseEditor.OpenRecipe(set);Call(editor,"BeginStudioPose",1);
            var session=Get<RoweIKSession>(editor,"session");var bars=session.bikeJoints[2];
            var grip=session.positions.Take(2).Select(p=>bars.InverseTransformPoint(p)).ToArray();
            Move(editor,2,bars.rotation*Quaternion.Euler(0,10,0));
            r.handTargetsFollow=Enumerable.Range(0,2).All(i=>Vector3.Distance(session.positions[i],bars.TransformPoint(grip[i]))<.0001f);
            r.handReachError=Enumerable.Range(0,2).Max(i=>Vector3.Distance(session.bones[i].position,session.positions[i]));
            var savedHands=session.bones.Take(2).Select(t=>t.position).ToArray();var savedBars=bars.localRotation;
            Call(editor,"Commit");Call(editor,"Sample");
            r.savedHandError=Enumerable.Range(0,2).Max(i=>Vector3.Distance(savedHands[i],session.bones[i].position));
            r.savedPair=Quaternion.Angle(savedBars,bars.localRotation)<.05f && r.savedHandError<.02f;
            // Default whole-bike edit: pelvis stays fixed, all four contact goals follow bike.
            var pinnedHip=session.bones[4].position;var pinnedHipRotation=session.bones[4].rotation;
            var parents=new[]{session.bikeJoints[2],session.bikeJoints[2],session.bikeJoints[4],session.bikeJoints[5]};
            var contactOffsets=Enumerable.Range(0,4).Select(i=>parents[i].InverseTransformPoint(session.positions[i])).ToArray();
            Move(editor,0,session.bikeJoints[0].rotation*Quaternion.Euler(0,5,0));
            r.pinnedHipError=Vector3.Distance(session.bones[4].position,pinnedHip);
            r.pinnedContactError=Enumerable.Range(0,4).Max(i=>Vector3.Distance(session.bones[i].position,parents[i].TransformPoint(contactOffsets[i])));
            r.pinnedHipsAndContacts=set.keepHipsInPlace && r.pinnedHipError<.001f && Quaternion.Angle(session.bones[4].rotation,pinnedHipRotation)<.1f
                && Enumerable.Range(0,4).All(i=>Vector3.Distance(session.positions[i],parents[i].TransformPoint(contactOffsets[i]))<.0001f) && r.pinnedContactError<.005f;
            Call(editor,"Commit");Call(editor,"Sample");
            r.pinnedHipsSaved=Vector3.Distance(session.bones[4].position,pinnedHip)<.001f;
            set.handsFollowBars=false;set.feetFollowPedals=false;set.riderFollowsWholeBike=false;
            var beforeRider=new RowePairedWorkbench.Pose(rider.transform);var barsBefore=bars.rotation;
            Move(editor,1,session.bikeJoints[1].rotation*Quaternion.Euler(0,65,0));
            r.releasedContacts=beforeRider.Changed()==0 && Quaternion.Angle(barsBefore,bars.rotation)<.01f;
            Call(editor,"Sample");set.feetFollowPedals=true;
            var footOffsets=Enumerable.Range(0,2).Select(i=>session.bikeJoints[4+i].InverseTransformPoint(session.positions[2+i])).ToArray();
            Move(editor,3,session.bikeJoints[3].rotation*Quaternion.Euler(5,0,0));
            r.pedalTargetsFollow=Enumerable.Range(0,2).All(i=>Vector3.Distance(session.positions[i+2],session.bikeJoints[i+4].TransformPoint(footOffsets[i]))<.0001f);
            Call(editor,"Sample");set.riderFollowsWholeBike=true;set.keepHipsInPlace=false;
            var whole=session.bikeJoints[0];var oldMatrix=whole.localToWorldMatrix;var targets=session.positions.ToArray();
            Move(editor,0,whole.rotation*Quaternion.Euler(0,20,0));
            var delta=whole.localToWorldMatrix*oldMatrix.inverse;
            // Whole-bike carry respects master contact permissions too: hands
            // remain released while the eligible feet and pelvis carry rigidly.
            r.wholeBikeCarries=Enumerable.Range(0,2).All(i=>Vector3.Distance(session.positions[i],targets[i])<.0001f)
                && Enumerable.Range(2,3).All(i=>Vector3.Distance(session.positions[i],delta.MultiplyPoint3x4(targets[i]))<.0001f);
            Call(editor,"Commit");Call(editor,"Stop");
            var guids=set.bikePhases.Select(c=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c))).ToArray();
            RoweHeldTrickBuilder.Rebuild(set);
            r.rebuildKeepsGuids=set.bikePhases.Select(c=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c))).SequenceEqual(guids)
                && Mathf.Abs(set.bikePhases[2].length-set.tweakSeconds)<.001f;
            r.pairedExport=CheckExport(set,false);
            // Keep a strong asset reference through bundle builds and reload to avoid stale handles.
            set=AssetDatabase.LoadAssetAtPath<RoweHeldTrick>(r.recipePath);
            RoweTrickStyles.Choose(set,RoweTrickStyle.Loop);RoweTrickStyles.MakeMovementFromHeld(set,1);
            editor=RoweIKPoseEditor.OpenRecipe(set);Call(editor,"StartPairedMovement");
            Set(editor,"seconds",.5f);Call(editor,"Sample");session=Get<RoweIKSession>(editor,"session");
            set.handsFollowBars=false;set.feetFollowPedals=false;set.riderFollowsWholeBike=false;
            Move(editor,2,session.bikeJoints[2].rotation*Quaternion.Euler(0,8,0));
            var hipGoal=session.positions[4]+Vector3.up*.02f;session.MoveControl(4,hipGoal,session.rotations[4]);Set(editor,"pending",true);
            var keyedBars=session.bikeJoints[2].localRotation;var keyedHip=session.bones[4].position;
            Call(editor,"Commit");Call(editor,"Sample");
            r.pairedTimeline=Quaternion.Angle(keyedBars,session.bikeJoints[2].localRotation)<.05f && Vector3.Distance(keyedHip,session.bones[4].position)<.02f;
            Set(editor,"selectedBike",1);Set(editor,"turnAxis",1);Set(editor,"reverseTurn",false);
            var beforeTurn=UnityEngine.Object.Instantiate(set.bikeMotionLoop);string name=set.bikeMotionLoop.name;
            double begin=EditorApplication.timeSinceStartup;Call(editor,"BakeBikeTurn");r.turnBakeSeconds=(float)(EditorApplication.timeSinceStartup-begin);
            r.fullTurn=set.bikeMotionLoop.name==name && AnimationUtility.GetAnimationClipSettings(set.bikeMotionLoop).loopTime
                && PhysicalFullTurn(session,set.game.motionLoop,beforeTurn,set.bikeMotionLoop)
                && AnimationUtility.GetCurveBindings(set.bikeMotionLoop).All(b=>AnimationUtility.GetEditorCurve(set.bikeMotionLoop,b).length>=61);
            Undo.PerformUndo();
            r.fullTurnUndo=new[]{.25f,.5f,.75f,1f}.All(t=>Quaternion.Angle(Rotation(set.bikeMotionLoop,1,t),Rotation(beforeTurn,1,t))<.1f);
            UnityEngine.Object.DestroyImmediate(beforeTurn);
            // Linked bake must write finite human movement as well as bike movement.
            set.handsFollowBars=true;Set(editor,"selectedBike",2);Call(editor,"BakeBikeTurn");
            var muscle=EditorCurveBinding.FloatCurve("",typeof(Animator),HumanTrait.MuscleName[0]);
            r.linkedTurn=set.game.motionLoop.isHumanMotion
                && AnimationUtility.GetCurveBindings(set.game.motionLoop).All(b=>AnimationUtility.GetEditorCurve(set.game.motionLoop,b).keys.All(k=>float.IsFinite(k.value)))
                && AnimationUtility.GetCurveBindings(set.game.motionLoop).Any(b=>AnimationUtility.GetEditorCurve(set.game.motionLoop,b).length>=61)
                && Quaternion.Angle(Rotation(set.bikeMotionLoop,2,.5f),Rotation(set.bikeMotionLoop,2,0))>170;
            Call(editor,"Stop");r.sceneRestored=original.Changed()==0;
            r.loopExport=CheckExport(set,true);r.sourcePreserved=before==File.ReadAllText(sourcePath);
            r.passed=r.joints&&r.genericKeys&&r.handTargetsFollow&&r.savedPair&&r.releasedContacts&&r.pedalTargetsFollow&&r.wholeBikeCarries&&r.rebuildKeepsGuids&&r.pairedExport&&r.pairedTimeline&&r.fullTurn&&r.fullTurnUndo&&r.linkedTurn&&r.sourcePreserved&&r.sceneRestored&&r.loopExport&&r.pinnedHipsAndContacts&&r.pinnedHipsSaved;
            if(!r.passed)throw new InvalidOperationException("One or more bike authoring checks failed.");
        }
        catch(Exception ex){r.error=ex.ToString();Debug.LogException(ex);}
        finally{if(editor)editor.Close();}
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace,"studio-bike-validation.json"),JsonUtility.ToJson(r,true));
        Debug.Log("ROWE_BIKE_VALIDATION "+JsonUtility.ToJson(r));EditorApplication.Exit(r.passed?0:1);
    }
}
