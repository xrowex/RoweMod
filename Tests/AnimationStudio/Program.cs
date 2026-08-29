using Newtonsoft.Json;
using RoweMod.AnimationAuthoring;
using UnityEngine;
using Il2CppMashBox.Core.Runtime.TrickSystem;
using rowemod.Mods;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
var p = new RoweAnimationPackage { id="RoweMod_Custom_Test_123",title="Test",rider=new RoweAnimationTrack{replace=true} };
var clips = new Dictionary<string,AnimationClip>();
foreach(var phase in new[]{(0,"Enter"),(2,"Tweak"),(3,"Exit")}){string n=p.id+"_Rider_"+phase.Item2;p.rider.clips[phase.Item1]=n;clips.Add(n,new AnimationClip{name=n,humanMotion=true});}
Func<string,bool,bool> exists=(n,h)=>clips.TryGetValue(n,out var c)&&c.humanMotion==h;
Check(p.Validate(exists)==null,"native hold accepts omitted Loop");
var restored=JsonConvert.DeserializeObject<RoweAnimationPackage>(JsonConvert.SerializeObject(p));
Check(restored.rider.replace && restored.rider.clips[1]==null && !restored.bike.replace,"JSON roundtrip preserves clear vs keep");
p.applyTiming=true;p.overallSpeed=float.NaN;Check(p.Validate(exists)!=null,"NaN rejected");
p.overallSpeed=1;p.tweakAt=1.1f;Check(p.Validate(exists)!=null,"invalid normalized time rejected");p.tweakAt=.6f;
p.rider.clips[3]=null;Check(p.Validate(exists)!=null,"missing Exit rejected");p.rider.clips[3]=p.id+"_Rider_Exit";
Check(p.Validate((n,h)=>false)!=null,"missing clip rejected");
p.version=100;Check(p.Validate(exists)!=null,"unknown version rejected");p.version=1;
p.bike.replace=true;p.bike.clips=p.rider.clips.ToArray();Check(p.Validate(exists)!=null,"Humanoid on bike rejected");p.bike.replace=false;
var stock=new AnimationClip{name="Stock",humanMotion=true};
var stockBike=new AnimationClip{name="Bike",humanMotion=false};
var stockMirror=new AnimationClip{name="Mirror",humanMotion=true};
var sharedRider=new TrickAnimationData{_enterAnimationClip=stock,_loopAnimationClip=stock,_tweakAnimationClip=stock,_exitAnimationClip=stock};
var sharedBike=new TrickAnimationData{_enterAnimationClip=stockBike,_exitAnimationClip=stockBike};
var stockPartner=new SyncTrickAnimationData{_playerAnimationData=new TrickAnimationData{_enterAnimationClip=stockMirror,_exitAnimationClip=stockMirror},_vehicleAnimationData=sharedBike};
var target=new SyncTrickAnimationData{_playerAnimationData=sharedRider,_vehicleAnimationData=sharedBike,_mirrorSyncData=stockPartner,_overallSpeedMult=2};
var other=new SyncTrickAnimationData{_playerAnimationData=sharedRider,_vehicleAnimationData=sharedBike,_mirrorSyncData=stockPartner};
p.applyTiming=false;p.applyRules=false;
TrickAnimationEditor.TestApply(target,p,clips);
Check(TrickAnimationEditor.Saves==1,"explicit apply saves once");
Check(target._playerAnimationData!=sharedRider && other._playerAnimationData._enterAnimationClip==stock,"only selected trick changes");
Check(target._playerAnimationData._loopAnimationClip==null,"empty Loop actually clears");
Check(target._vehicleAnimationData._enterAnimationClip==stockBike,"keep bike preserves clips");
Check(target._overallSpeedMult==2 && target._mirrorSyncData==stockPartner,"keep timing and mirror partner");
Check(TrickAnimationEditor.TestPackageMatches(target,p,clips),"package card recognizes applied normal tracks");
var appliedEnter=target._playerAnimationData._enterAnimationClip;target._playerAnimationData._enterAnimationClip=stock;
Check(!TrickAnimationEditor.TestPackageMatches(target,p,clips),"package card rejects different clip assignment");
target._playerAnimationData._enterAnimationClip=appliedEnter;
p.riderMirror=new RoweAnimationTrack{replace=true,clips=p.rider.clips.ToArray()};
p.applyTiming=true;p.overallSpeed=1.2f;p.tweakSpeed=2.5f;p.applyRules=true;p.onlyInAir=true;
TrickAnimationEditor.TestApply(target,p,clips);
Check(target._mirrorSyncData!=stockPartner && target._mirrorSyncData._playerAnimationData._enterAnimationClip==clips[p.rider.clips[0]],"V2 linked mirror uses replacement");
Check(other._mirrorSyncData._playerAnimationData._enterAnimationClip==stockMirror,"stock mirror partner remains untouched");
Check(target._mirrorSyncData._vehicleAnimationData._enterAnimationClip==stockBike,"kept mirror bike preserved");
Check(TrickAnimationEditor.Saved.studioMirrorRouting && TrickAnimationEditor.Saved.playerMirrorLoopClip==null,"saved mirror routing includes deliberate empty Loop");
Check(target._overallSpeedMult==1.2f && target._tweakSpeedMult==2.5f && target._onlyFireIfInAir,"timing and rule fields applied");
Check(TrickAnimationEditor.TestPackageMatches(target,p,clips),"package card recognizes applied mirror tracks");
var ownRider=target._playerAnimationData;var ownMirror=target._mirrorSyncData;
TrickAnimationEditor.TestApply(target,p,clips);
Check(target._playerAnimationData==ownRider && target._mirrorSyncData==ownMirror,"repeat apply reuses private data");
TrickAnimationEditor.TestRestoreMirror(target);Check(target._mirrorSyncData==stockPartner,"reset can restore original mirror route");
TrickAnimationEditor.TestRelease();
Check(target._playerAnimationData==sharedRider && target._vehicleAnimationData==sharedBike && ownRider.destroyed && ownMirror.destroyed,"scene cleanup restores stock references and destroys clones");
var paired=new RoweAnimationPackage{id="RoweMod_Custom_Paired_456",title="Paired",rider=new RoweAnimationTrack{replace=true},bike=new RoweAnimationTrack{replace=true}};
var pairedClips=new Dictionary<string,AnimationClip>();
foreach(var role in new[]{"Rider","Bike"}) foreach(var phase in new[]{(0,"Enter"),(3,"Exit")})
{
    string n=paired.id+"_"+role+"_"+phase.Item2;
    (role=="Rider"?paired.rider:paired.bike).clips[phase.Item1]=n;
    // IL2CPP reports the exported Generic bike clips as humanMotion=true.
    pairedClips.Add(n,new AnimationClip{name=n,humanMotion=true});
}
var pairedTarget=new SyncTrickAnimationData{_playerAnimationData=new TrickAnimationData(),_vehicleAnimationData=new TrickAnimationData()};
int pairedSaveStart=TrickAnimationEditor.Saves;
TrickAnimationEditor.TestApply(pairedTarget,paired,pairedClips);
Check(TrickAnimationEditor.Saves==pairedSaveStart+1 && pairedTarget._playerAnimationData._enterAnimationClip==pairedClips[paired.rider.clips[0]] && pairedTarget._vehicleAnimationData._enterAnimationClip==pairedClips[paired.bike.clips[0]],"one apply assigns rider and runtime-reported bike clips");
TrickAnimationEditor.TestRelease();
Console.WriteLine($"{checks} checks passed. Fake Unity objects; not an IL2CPP runtime test.");
