using rowemod;
using rowemod.Mods;
using rowemod.Utils;
using UnityEngine;
using Il2CppMashBox.BMX_Physics_Development;
using Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2;
using Il2CppMashBox.Core.Runtime.TrickSystem;

static class Program
{
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static InputHoldPoint Point(float delay) => new() { _minHoldValue=.2f, _maxHoldValue=.8f, _minCancelValue=.2f, _maxCancelValue=1.1f, _minHoldTime=delay, _maxInputVelocity=1f };
    static void Reset()
    {
        ManualActivationControl.Release();
        Config.physics = new rowemod.Physics(); Config.Saves=0;
        Menu.Edits.Clear(); Menu.Buttons.Clear(); Menu.Ranges.Clear();
        Memory.rMbCharacter = new GameObject(); Memory.vehicleController=null;
        Memory.playerInputBehaviour = new FakePlayerInput { _hasInputAuthority=true, _mannyInputHoldPoint=Point(.2f), _noseyInputHoldPoint=Point(.1f) };
    }
    static void Main()
    {
        string[] stock = { "Left Shoulder Right Trigger", "Right Shoulder Left Trigger", "Left Right Shoulder", "Left Shoulder", "Right Shoulder", "Left Right Trigger", "Left Trigger", "Right Trigger" };
        string[] actions = { "LS RT", "RS LT", "LS RS", "LS", "RS", "LT RT", "LT", "RT" };
        Check(stock.Select(TrickPresetCompatibility.SetIdentity).Distinct().Count()==8, "all eight input combinations keep distinct identities");
        for (int i=0;i<8;i++)
        {
            Check(TrickPresetCompatibility.SetIdentity(stock[i]+"_TrickSetData_BMX (Clone)")==TrickPresetCompatibility.SetIdentity(actions[i]), "stock name, cloned name and native input label agree");
        }
        var saved = new Dictionary<string,List<TrickEntry>> { ["LT"] = new(){new(){Name="Can Left",Enabled=true}}, ["RT"] = new(){new(){Name="Can Right",Enabled=true}}, ["Unknown future set"] = new(){new(){Name="Future trick",Enabled=false}} };
        Check(TrickPresetCompatibility.TryFindSet(saved,"Left Trigger_TrickSetData_BMX",out var key,out var entries) && key=="LT" && entries[0].Name=="Can Left", "LT alias restores its own mapping");
        Check(TrickPresetCompatibility.TryFindSet(saved,"Right Trigger_TrickSetData_BMX",out key,out entries) && key=="RT" && entries[0].Name=="Can Right", "RT alias never resolves to LT");
        var copy=TrickPresetCompatibility.CopySets(saved); copy["LT"][0].Name="Changed";
        Check(saved["LT"][0].Name=="Can Left" && copy.ContainsKey("Unknown future set"), "copy preserves unloaded sets and is independent of the original preset");
        saved["Left Trigger_TrickSetData_BMX"]=new(){new(){Name="Exact",Enabled=false}};
        Check(TrickPresetCompatibility.TryFindSet(saved,"Left Trigger_TrickSetData_BMX",out _,out entries) && entries[0].Name=="Exact", "exact key wins over an alias");
        Check(!TrickPresetCompatibility.TryFindSet(saved,"Left Trigger_TrickSetData_BMX (Clone)",out _,out _), "ambiguous aliases cannot silently choose a mapping");

        Reset(); var input=Memory.playerInputBehaviour;
        ManualActivationControl.Apply();
        Check(input._mannyInputHoldPoint._minHoldTime==.2f && input._noseyInputHoldPoint._minHoldTime==.1f, "default-off leaves the distinct native hold times untouched");
        Check(ManualActivationControl.BeginCustomization(), "customization starts for the authoritative local rider");
        Check(Config.physics.manualActivationDelay==.2f && Config.physics.noseManualActivationDelay==.1f, "enable captures manual and nose-manual independently");
        Config.physics.manualActivationDelay=.04f; Config.physics.noseManualActivationDelay=0f;
        Config.physics.manualActivationThreshold=.1f; Config.physics.noseManualActivationThreshold=.15f;
        ManualActivationControl.Apply();
        Check(input._mannyInputHoldPoint._minHoldTime==.04f && input._noseyInputHoldPoint._minHoldTime==0f, "delay changes include valid zero delay");
        Check(input._mannyInputHoldPoint._minHoldValue==.1f && input._mannyInputHoldPoint._minCancelValue==.1f, "lower sensitivity threshold also moves cancellation gate");
        Check(input._noseyInputHoldPoint._maxHoldValue==.8f && input._noseyInputHoldPoint._maxCancelValue==1.1f && input._noseyInputHoldPoint._maxInputVelocity==1f, "native upper window, cancel maximum and velocity gate are preserved");
        ManualActivationControl.Release();
        Check(input._mannyInputHoldPoint._minHoldTime==.2f && input._noseyInputHoldPoint._minHoldTime==.1f && input._mannyInputHoldPoint._minCancelValue==.2f, "restore puts back each original timing and cancellation gate");
        Config.physics.manualActivationEnabled=false; input._hasInputAuthority=false;
        Check(!ManualActivationControl.BeginCustomization(), "remote input cannot be customized");
        input._hasInputAuthority=true; Memory.rMbCharacter=null;
        Check(!ManualActivationControl.BeginCustomization(), "missing local rider context does not write settings");

        Reset(); input=Memory.playerInputBehaviour; ManualActivationControl.BeginCustomization();
        Config.physics.manualActivationDelay=float.NaN; Config.physics.manualActivationThreshold=float.NaN;
        Config.physics.noseManualActivationDelay=99; Config.physics.noseManualActivationThreshold=99;
        ManualActivationControl.Apply();
        Check(input._mannyInputHoldPoint._minHoldTime==.2f && input._mannyInputHoldPoint._minHoldValue==.2f, "nonfinite values fall back to captured native settings");
        Check(input._noseyInputHoldPoint._minHoldTime==1f && Math.Abs(input._noseyInputHoldPoint._minHoldValue-.79f)<.0001f, "saved out-of-range values stay inside native activation window");
        var old=input._mannyInputHoldPoint; input._mannyInputHoldPoint=Point(.3f); ManualActivationControl.Apply();
        Check(old._minHoldTime==.2f && old._minHoldValue==.2f, "replacing a hold point releases the old point first");
        Config.physics.manualActivationEnabled=false; ManualActivationControl.Apply();
        Check(input._mannyInputHoldPoint._minHoldTime==.3f, "disable restores replacement point's own native delay");

        Reset(); input=Memory.playerInputBehaviour; ManualActivationControl.BeginCustomization();
        Menu.Edits["Manual Delay (ms)"]=30f; Menu.Edits["Manual Stick Threshold"]=.1f;
        int before=Config.Saves; ManualActivationControl.DrawControls();
        Check(Math.Abs(input._mannyInputHoldPoint._minHoldTime-.03f)<.0001f && input._mannyInputHoldPoint._minHoldValue==.1f && Config.Saves>before, "UI converts milliseconds and queues persistence for edits");

        var active=new TrickSystemBrainV2 { enabled=true, gameObject=new GameObject(), _trickDataSets=new TrickDataSets() };
        var disabled=new TrickSystemBrainV2 { enabled=false, gameObject=new GameObject() };
        Memory.rMbCharacter.ExtraComponents=new UnityEngine.Object[]{disabled,active};
        TrickRuntimeContext.Refresh();
        Check(active.Refreshes==1 && disabled.Refreshes==0 && !disabled.enabled, "refresh reaches only the enabled local brain without enabling another component");
        var authoritative=new TrickDataSets { TrickSets=new[]{new TrickSetData()} };
        Memory.vehicleController=new FakeVehicle { VehicleTrickDataSets=authoritative };
        Check(TrickRuntimeContext.LocalDataSets()==authoritative, "mapping follows the native vehicle provider before the brain's fallback table");
        for (int i=1;i<=8;i++)
        {
            var action=new UnityEngine.InputSystem.InputAction();
            input.GetType().GetProperty($"_trickSet{i}InputAction").SetValue(input,action);
            Check(TrickRuntimeContext.LocalInputAction(i)==action, "every native input action including LT/RT is read from the local owner");
        }
        ManualActivationControl.Release();
        Console.WriteLine($"PASS: {checks} trick-key, local-refresh, eight-action, and manual-activation checks. Stubbed game objects; physical trigger/riding acceptance remains pending.");
    }
}
