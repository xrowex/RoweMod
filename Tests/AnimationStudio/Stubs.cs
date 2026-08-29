using RoweMod.AnimationAuthoring;
using UnityEngine;
using Il2CppMashBox.Core.Runtime.TrickSystem;
namespace UnityEngine
{
    public class Object { static int next; int id = ++next; public int GetInstanceID()=>id; public bool destroyed; public static void Destroy(Object o) {o.destroyed=true;} public T TryCast<T>() where T:class => this as T; }
    public class ScriptableObject:Object { public static T CreateInstance<T>() where T:ScriptableObject,new()=>new(); }
    public class AnimationClip:Object { public string name; public bool humanMotion,legacy; }
    public class AssetBundle:Object { public Object LoadAsset(string s, Type t)=>null; }
    public class TextAsset:Object {public string text;}
    public static class Time {public static float unscaledTime;}
    public static class GUILayout
    {
        public static void Label(string s,object style){}
        public static void BeginVertical(object style){}
        public static void EndVertical(){}
        public static void Space(float value){}
        public static object ExpandWidth(bool b)=>null;
        public static object Height(float f)=>null;
    }
}
namespace Il2CppInterop.Runtime {public static class Il2CppType {public static Type Of<T>()=>typeof(T);}}
namespace rowemod.Utils {public static class Log {public static void Msg(string s){} public static void Warning(string s){}}}
namespace Il2CppMashBox.Core.Runtime.TrickSystem
{
    public class TrickAnimationData:ScriptableObject
    {
        public AnimationClip _enterAnimationClip,_loopAnimationClip,_tweakAnimationClip,_exitAnimationClip;
        public AnimationClip _enterAnimationClipMirror,_loopAnimationClipMirror,_tweakAnimationClipMirror,_exitAnimationClipMirror;
        public float _overallSpeedMult=1;
    }
    public class SyncTrickAnimationData:ScriptableObject
    {
        public TrickAnimationData _playerAnimationData,_vehicleAnimationData;
        public SyncTrickAnimationData _mirrorSyncData;
        public float _overallSpeedMult=1,_enterSpeedMult=1,_loopMult=1,_tweakSpeedMult=1,_exitSpeedMult=1,_tweakBeginBlendNormalizedTime=.7f;
        public bool _onlyFireIfInAir,_allowLandingHolding;
    }
}
namespace rowemod
{
    public class TrickAnimationOverride
    {
        public bool studioMirrorRouting;
        public string playerMirrorEnterClip,playerMirrorLoopClip,playerMirrorTweakClip,playerMirrorExitClip;
        public string vehicleMirrorEnterClip,vehicleMirrorLoopClip,vehicleMirrorTweakClip,vehicleMirrorExitClip;
    }
    public static class Menu
    {
        public static bool PrimaryButton(string s,params object[] p)=>false;
        public static bool SecondaryButton(string s,params object[] p)=>false;
    }
}
namespace rowemod.Mods
{
    internal static class LiveFootDiagnostics
    {
        public static string Status => "test";
        public static bool IsCapturing => false;
        public static void Arm() { }
    }

    internal static partial class TrickAnimationEditor
    {
        static object mutedStyle,headerStyle,panelAltStyle,activePillStyle; static string status; static float suppressAutoApplyUntil;
        public static TrickAnimationOverride Saved; public static int Saves;
        static string ClipName(AnimationClip c)=>c?.name;
        static void RememberRuntimeDefault(SyncTrickAnimationData t){}
        static string GetDataKey(SyncTrickAnimationData t)=>"test";
        static string TrickName(SyncTrickAnimationData t)=>"Test Trick";
        static TrickAnimationOverride CaptureOverride(SyncTrickAnimationData t,string k) {var v=new TrickAnimationOverride(); CaptureStudioMirror(t,v);return v;}
        static void SaveAnimationOverride(SyncTrickAnimationData t,string k,string s) {Saved=CaptureOverride(t,k);Saves++;}
        static void ApplyOverride(SyncTrickAnimationData t,TrickAnimationOverride v)=>throw new Exception("Unexpected package apply failure: "+status);
        static bool CopyAnimationData(TrickAnimationData s,TrickAnimationData t)
        {
            if(s==null)return false;
            t._enterAnimationClip=s._enterAnimationClip;t._loopAnimationClip=s._loopAnimationClip;t._tweakAnimationClip=s._tweakAnimationClip;t._exitAnimationClip=s._exitAnimationClip;
            t._enterAnimationClipMirror=s._enterAnimationClipMirror;t._loopAnimationClipMirror=s._loopAnimationClipMirror;t._tweakAnimationClipMirror=s._tweakAnimationClipMirror;t._exitAnimationClipMirror=s._exitAnimationClipMirror;
            t._overallSpeedMult=s._overallSpeedMult; return true;
        }
        static void ApplyClipNames(TrickAnimationData t,string a,string b,string c,string d,string e,string f,string g,string h,bool clear) {}
        public static void TestApply(SyncTrickAnimationData t,RoweAnimationPackage p,Dictionary<string,AnimationClip> clips)=>ApplyStudioPackage(t,new StudioPackage{Data=p,Clips=clips});
        public static void TestRelease()=>ReleaseStudioTrackCopies();
        public static void TestRestoreMirror(SyncTrickAnimationData t)=>SetStudioMirrorRouting(t,false);
        public static bool TestPackageMatches(SyncTrickAnimationData t,RoweAnimationPackage p,Dictionary<string,AnimationClip> clips)=>StudioPackageMatchesTarget(t,new StudioPackage{Data=p,Clips=clips});
    }
}
