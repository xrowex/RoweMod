using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
public static class SmokeSessionDirectBuild {
 [Serializable] class Joint {public string name;public int parent;public float[] p,q;}
 [Serializable] class Frame {public float time;public float[] pose;}
 [Serializable] class Data {public string name;public Joint[] skeleton;public int[] joints;public Frame[] frames;}
 static Vector3 P(float[] p)=>new Vector3(-p[0],p[1],p[2]);
 static Quaternion Q(float[] q)=>new Quaternion(q[0],-q[1],-q[2],q[3]);

 static HumanBone[] CreateMapping(Transform[] transforms) {
  var map=new Dictionary<string,string>{{"Hips","Hips"},{"Spine","Spine"},{"Chest","Spine1"},{"UpperChest","Spine3"},{"Neck","Neck"},{"Head","Head"}};
  foreach(string side in new[]{"Left","Right"}) {
   map[side+"UpperLeg"]=side+"UpLeg";map[side+"LowerLeg"]=side+"Leg";map[side+"Foot"]=side+"Foot";map[side+"Toes"]=side+"ToeBase";
   map[side+"Shoulder"]=side+"Shoulder";map[side+"UpperArm"]=side+"Arm";map[side+"LowerArm"]=side+"ForeArm";map[side+"Hand"]=side+"Hand";
   foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"})for(int i=0;i<3;i++)map[side+finger+new[]{"Proximal","Intermediate","Distal"}[i]]=side+"Hand"+(finger=="Little"?"Pinky":finger)+(i+1);
  }
  return HumanTrait.BoneName.Where(n=>map.ContainsKey(n.Replace(" ",""))&&transforms.Any(t=>t.name==map[n.Replace(" ","")])).Select(n=>new HumanBone{humanName=n,boneName=map[n.Replace(" ","")],limit=new HumanLimit{useDefaultValues=true}}).ToArray();
 }

 public static void Build(){
  var report=new List<string>();
  var tool=typeof(ModelImporter).Assembly.GetType("UnityEditor.AvatarSetupTool",true);
  var wrapper=tool.GetNestedType("BoneWrapper",BindingFlags.Public|BindingFlags.NonPublic);
  var enforce=tool.GetMethod("MakePoseValid",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
  foreach(string file in Directory.GetFiles("Assets/DirectPoses","*.json")){
   var data=JsonUtility.FromJson<Data>(File.ReadAllText(file));var root=new GameObject("SmokeSessionSource");
   var ts=new Transform[data.skeleton.Length];
   for(int j=0;j<ts.Length;j++){var s=data.skeleton[j];ts[j]=new GameObject(s.name).transform;ts[j].SetParent(s.parent<0?root.transform:ts[s.parent],false);ts[j].localPosition=P(s.p);ts[j].localRotation=Q(s.q);}
   var human=CreateMapping(ts);
   var bones=Array.CreateInstance(wrapper,HumanTrait.BoneCount);
   for(int i=0;i<HumanTrait.BoneCount;i++){string hn=HumanTrait.BoneName[i];var h=human.FirstOrDefault(b=>b.humanName==hn);bones.SetValue(Activator.CreateInstance(wrapper,new object[]{hn,ts.FirstOrDefault(t=>t.name==h.boneName)}),i);}
   enforce.Invoke(null,new object[]{bones});
   var desc=new HumanDescription{human=human,skeleton=root.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0};
   var avatar=AvatarBuilder.BuildHumanAvatar(root,desc);if(!avatar.isHuman||!avatar.isValid)throw new Exception("Invalid direct avatar: "+data.name);
   var animator=root.AddComponent<Animator>();animator.avatar=avatar;
   var handler=new HumanPoseHandler(avatar,root.transform);var hp=new HumanPose();
   var curves=new List<Keyframe>[HumanTrait.MuscleCount+7];for(int i=0;i<curves.Length;i++)curves[i]=new List<Keyframe>();
   for(int j=0;j<ts.Length;j++){ts[j].localPosition=P(data.skeleton[j].p);ts[j].localRotation=Q(data.skeleton[j].q);}
   float maxMuscle=0,minHeight=float.MaxValue,maxHeight=float.MinValue;Vector3 origin=Vector3.zero;
   foreach(var frame in data.frames){
    for(int k=0;k<data.joints.Length;k++){int b=k*7;var p=frame.pose;ts[data.joints[k]].localPosition=new Vector3(-p[b],p[b+1],p[b+2]);ts[data.joints[k]].localRotation=new Quaternion(p[b+3],-p[b+4],-p[b+5],p[b+6]);}
    handler.GetHumanPose(ref hp);if(frame.time==0)origin=hp.bodyPosition;
    minHeight=Mathf.Min(minHeight,hp.bodyPosition.y);maxHeight=Mathf.Max(maxHeight,hp.bodyPosition.y);
    for(int i=0;i<HumanTrait.MuscleCount;i++){if(float.IsNaN(hp.muscles[i]))throw new Exception("NaN pose");curves[i].Add(new Keyframe(frame.time,hp.muscles[i]));maxMuscle=Mathf.Max(maxMuscle,Mathf.Abs(hp.muscles[i]));}
    float[] body={hp.bodyPosition.x-origin.x,hp.bodyPosition.y,hp.bodyPosition.z-origin.z,hp.bodyRotation.x,hp.bodyRotation.y,hp.bodyRotation.z,hp.bodyRotation.w};
    for(int i=0;i<7;i++)curves[HumanTrait.MuscleCount+i].Add(new Keyframe(frame.time,body[i]));
   }
   var clip=new AnimationClip{name="RoweMod_Custom_Emote_SmokeSession_"+data.name,frameRate=30};
   string[] bodyNames={"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"};
   for(int i=0;i<curves.Length;i++){var curve=new AnimationCurve(curves[i].ToArray());for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}clip.SetCurve("",typeof(Animator),i<HumanTrait.MuscleCount?HumanTrait.MuscleName[i]:bodyNames[i-HumanTrait.MuscleCount],curve);}
   var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.loopBlendOrientation=true;settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
   string output="Assets/Output/RoweMod_SmokeSession_"+data.name+".anim";AssetDatabase.DeleteAsset(output);AssetDatabase.CreateAsset(clip,output);clip.name="RoweMod_Custom_Emote_SmokeSession_"+data.name;EditorUtility.SetDirty(clip);AssetImporter.GetAtPath(output).assetBundleName="rowemod_smoke_session";
   report.Add(data.name+" frames="+data.frames.Length+" human="+clip.humanMotion+" height="+minHeight+".."+maxHeight+" muscleMax="+maxMuscle);
   handler.Dispose();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(avatar);AssetDatabase.SaveAssets();GC.Collect();
  }
  BuildPipeline.BuildAssetBundles("BundleOutput",BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);File.WriteAllLines("BundleOutput/direct-build-report.txt",report);Debug.Log(string.Join(";",report));
 }
}
