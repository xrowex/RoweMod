using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Generic local-joint animation. Never key the physical vehicle or mesh transforms.
public static class RoweBikeAnimation
{
    public const int FrameIndex = 1;
    public const int BarsIndex = 2;
    public const string StaticReferencePath = "Assets/Reference/BikeStaticReference.prefab";
    public static readonly string[] Paths = { "Joints", "Joints/Frame_Joint", "Joints/Bars_Joint", "Joints/Frame_Joint/DriveTrain_Joint", "Joints/Frame_Joint/DriveTrain_Joint/LeftPedal_Joint", "Joints/Frame_Joint/DriveTrain_Joint/RightPedal_Joint", "Joints/Bars_Joint/FrontWheel_Joint", "Joints/Frame_Joint/BackWheel_Joint" };
    public static readonly string[] Labels = { "Whole bike", "Frame", "Bars / forks", "Cranks", "Left pedal", "Right pedal", "Front wheel", "Rear wheel" };
    static readonly string[] Properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
    static bool steeringReferenceReady;
    static Vector3 steeringPivotInJoints;
    static Quaternion steeringBasisInJoints, frameControlOffset, barsControlOffset;
    static string Folder(RoweHeldTrick set) => Path.GetDirectoryName(AssetDatabase.GetAssetPath(set)).Replace('\\', '/');
    public static bool Editable(AnimationClip clip) => clip && !clip.isHumanMotion && !clip.legacy && AssetDatabase.GetAssetPath(clip).StartsWith("Assets/CustomClips/", StringComparison.Ordinal) && AssetDatabase.GetAssetPath(clip).EndsWith(".anim", StringComparison.OrdinalIgnoreCase);
    public static Transform[] FindJoints(Animator bike)
    {
        if (!bike) throw new InvalidOperationException("Assign the Bike Skeleton Animator under Rig setup first.");
        var joints = Paths.Select(p => bike.transform.Find(p)).ToArray();
        for (int i = 0; i < joints.Length; i++) if (!joints[i]) throw new InvalidOperationException("Bike rig is missing " + Paths[i]);
        return joints;
    }
    public static bool UsesSteeringPivot(int index) => index == FrameIndex || index == BarsIndex;
    static void EnsureSteeringReference()
    {
        if (steeringReferenceReady) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StaticReferencePath);
        if (!prefab) throw new InvalidOperationException("Missing " + StaticReferencePath + ". Restore the Animation Studio bike reference before posing the bike.");
        var all = prefab.GetComponentsInChildren<Transform>(true);
        var lower = all.Where(t => t.name == "Forks_Anchor").ToArray();
        var upper = all.Where(t => t.name == "Headset_Anchor").ToArray();
        var joints = all.Where(t => t.name == "Joints" && t.Find("Frame_Joint") && t.Find("Bars_Joint")).ToArray();
        if (lower.Length != 1 || upper.Length != 1 || joints.Length != 1 || lower[0].parent != upper[0].parent)
            throw new InvalidOperationException("BikeStaticReference must contain one equipped frame with sibling Forks_Anchor and Headset_Anchor transforms, plus the Streets Joints rig.");
        var axis = upper[0].position - lower[0].position;
        if (axis.sqrMagnitude < .000001f) throw new InvalidOperationException("The frame's Forks_Anchor and Headset_Anchor must be separated.");
        axis.Normalize();
        var forward = Vector3.ProjectOnPlane(lower[0].parent.forward, axis);
        if (forward.sqrMagnitude < .000001f) throw new InvalidOperationException("The frame anchor basis is invalid.");
        forward.Normalize();
        var basis = Quaternion.LookRotation(forward, axis);
        var root = joints[0];
        var bars = root.Find("Bars_Joint");
        steeringPivotInJoints = root.InverseTransformPoint((lower[0].position + upper[0].position) * .5f);
        steeringBasisInJoints = Quaternion.Inverse(root.rotation) * basis;
        frameControlOffset = Quaternion.Inverse(root.Find("Frame_Joint").localRotation) * steeringBasisInJoints;
        barsControlOffset = Quaternion.Inverse(bars.localRotation) * steeringBasisInJoints;
        steeringReferenceReady = true;
    }
    // MashBike frames define one physical steering line from Forks_Anchor to
    // Headset_Anchor. Streets' Frame_Joint and Bars_Joint are both offset from
    // it, so their animation transforms orbit this non-keyed editor reference.
    // Exported clips still contain only the original Streets joint paths.
    public static void SteeringReference(Transform[] joints, out Vector3 pivot, out Quaternion basis)
    {
        if (joints == null || joints.Length <= BarsIndex || !joints[0]) throw new InvalidOperationException("Bike rig is missing Joints.");
        EnsureSteeringReference();
        pivot = joints[0].TransformPoint(steeringPivotInJoints);
        basis = joints[0].rotation * steeringBasisInJoints;
    }
    public static Vector3 ControlPivot(Transform[] joints, int index)
    {
        if (joints == null || index < 0 || index >= joints.Length || !joints[index]) throw new ArgumentOutOfRangeException(nameof(index));
        if (!UsesSteeringPivot(index)) return joints[index].position;
        SteeringReference(joints, out var pivot, out _);
        return pivot;
    }
    public static Quaternion ControlRotation(Transform[] joints, int index)
    {
        if (joints == null || index < 0 || index >= joints.Length || !joints[index]) throw new ArgumentOutOfRangeException(nameof(index));
        if (!UsesSteeringPivot(index)) return joints[index].rotation;
        EnsureSteeringReference();
        return joints[index].rotation * (index == FrameIndex ? frameControlOffset : barsControlOffset);
    }
    public static Quaternion JointRotationFromControl(Transform[] joints, int index, Quaternion controlRotation)
    {
        if (!UsesSteeringPivot(index)) return controlRotation;
        EnsureSteeringReference();
        return controlRotation * Quaternion.Inverse(index == FrameIndex ? frameControlOffset : barsControlOffset);
    }
    public static Vector3 PivotedPosition(Transform[] joints, int index, Vector3 requestedPosition, Quaternion requestedRotation)
    {
        if (!UsesSteeringPivot(index)) return requestedPosition;
        var joint = joints[index];
        var pivot = ControlPivot(joints, index);
        var delta = requestedRotation * Quaternion.Inverse(joint.rotation);
        return pivot + delta * (requestedPosition - pivot);
    }
    public static AnimationClip Capture(Transform[] joints)
    {
        var clip = new AnimationClip { frameRate = 60 };
        for (int i = 0; i < joints.Length; i++)
        {
            var p = joints[i].localPosition; var q = joints[i].localRotation; var s = joints[i].localScale;
            float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z };
            for (int j = 0; j < Properties.Length; j++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(Paths[i], typeof(Transform), Properties[j]), AnimationCurve.Constant(0, 1, values[j]));
        }
        return clip;
    }
    public static void Write(AnimationClip target, AnimationClip pose, float time, bool undo = true, bool continuity = true)
    {
        if (undo) Undo.RegisterCompleteObjectUndo(target, "Key bike pose");
        foreach (var binding in AnimationUtility.GetCurveBindings(pose))
        {
            var curve = AnimationUtility.GetEditorCurve(target, binding) ?? new AnimationCurve();
            float value = AnimationUtility.GetEditorCurve(pose, binding).Evaluate(0);
            int index = Array.FindIndex(curve.keys, k => Mathf.Abs(k.time-time)<.0001f);
            index = index >= 0 ? curve.MoveKey(index, new Keyframe(time, value)) : curve.AddKey(new Keyframe(time, value));
            AnimationUtility.SetKeyLeftTangentMode(curve,index,AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(curve,index,AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetEditorCurve(target,binding,curve);
        }
        if(continuity)target.EnsureQuaternionContinuity(); EditorUtility.SetDirty(target);
    }
    static void Timing(AnimationClip clip, float duration, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.startTime=0; settings.stopTime=duration; settings.loopTime=loop; settings.loopBlend=false;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
    }
    static AnimationClip Transition(AnimationClip a, float from, AnimationClip b, float to, float duration, bool loop)
    {
        var clip = new AnimationClip { frameRate=60 };
        foreach (string path in Paths)
        {
            float dot=0;
            foreach (string prop in Properties.Skip(3).Take(4))
            {
                var binding=EditorCurveBinding.FloatCurve(path,typeof(Transform),prop);
                var x=AnimationUtility.GetEditorCurve(a,binding);var y=AnimationUtility.GetEditorCurve(b,binding);
                if (x==null||y==null) throw new InvalidOperationException("Bike pose is missing joint curves: "+path);
                dot+=x.Evaluate(from)*y.Evaluate(to);
            }
            for(int i=0;i<Properties.Length;i++)
            {
                var binding=EditorCurveBinding.FloatCurve(path,typeof(Transform),Properties[i]);
                var x=AnimationUtility.GetEditorCurve(a,binding);var y=AnimationUtility.GetEditorCurve(b,binding);
                if(x==null||y==null) throw new InvalidOperationException("Bike pose is missing joint curves: "+path);
                float end=y.Evaluate(to)*(i>=3&&i<=6&&dot<0?-1:1);
                AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.EaseInOut(0,x.Evaluate(from),duration,end));
            }
        }
        Timing(clip,duration,loop);return clip;
    }
    public static void Enable(RoweHeldTrick set, Animator rider, Animator bike)
    {
        RoweHeldTrickBuilder.ValidateRecipe(set); var joints=FindJoints(bike);
        if (!set.bikePoses)
        {
            var poses=new AnimationClip { name="StudioBikePoses",frameRate=60 };
            using(var session=new RoweIKSession(rider,bike))
                foreach(float t in new[]{0f,1f})
                {
                    var seed=set.bikeEnterPreview;
                    session.Sample(set.poses,seed,t,seed?t*seed.length:0);
                    var pose=Capture(joints);try {Write(poses,pose,t,false);}finally {UnityEngine.Object.DestroyImmediate(pose);}
                }
            Timing(poses,1,false);
            AssetDatabase.CreateAsset(poses,AssetDatabase.GenerateUniqueAssetPath(Folder(set)+"/StudioBikePoses.anim"));
            set.bikePoses=poses;
        }
        Undo.RecordObject(set,"Enable bike authoring");set.authorBike=true;set.game.include=true;
        Rebuild(set);EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
    }
    public static void EnsureTweak(RoweHeldTrick set)
    {
        if(!set.authorBike || set.bikeTweakPose || !set.bikePoses)return;
        var pose=Transition(set.bikePoses,1,set.bikePoses,1,1,false);pose.name="StudioBikeTweak";
        AssetDatabase.CreateAsset(pose,AssetDatabase.GenerateUniqueAssetPath(Folder(set)+"/StudioBikeTweak.anim"));
        Undo.RecordObject(set,"Create bike Tweak pose");set.bikeTweakPose=pose;set.bikeTweakHoldSeed=RoweTweakFromHold.Signature(pose);EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
    }
    public static AnimationClip HeldPose(RoweHeldTrick set) => Transition(set.bikePoses,1,set.bikePoses,1,1,false);
    public static AnimationClip RiderMovement(RoweHeldTrick set) => set.game.customRider ? set.game.rider[1] : set.game.motionLoop;
    public static void EnsureMovement(RoweHeldTrick set)
    {
        var rider=RiderMovement(set);
        if(!set.authorBike || set.bikeMotionLoop || !rider)return;
        var motion=Transition(set.bikePoses,1,set.bikePoses,1,Mathf.Max(.05f,rider.length),true);motion.name=set.clipPrefix+"_BikeMovement";
        AssetDatabase.CreateAsset(motion,AssetDatabase.GenerateUniqueAssetPath(Folder(set)+"/BikeMovement.anim"));
        Undo.RecordObject(set,"Create paired bike movement");set.bikeMotionLoop=motion;EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
    }
    public static AnimationClip[] Clips(RoweHeldTrick set)
    {
        var clips=set.bikePhases.ToArray();
        if(set.game.style==RoweTrickStyle.Loop)clips[1]=set.bikeMotionLoop;
        return clips;
    }
    public static void Rebuild(RoweHeldTrick set)
    {
        if(!set.authorBike)return;
        if(!Editable(set.bikePoses) || Path.GetDirectoryName(AssetDatabase.GetAssetPath(set.bikePoses)).Replace('\\','/')!=Folder(set))throw new InvalidOperationException("Bike pose data must belong to this saved trick.");
        if(set.bikePhases==null||set.bikePhases.Length!=4)set.bikePhases=new AnimationClip[4];
        foreach(var existing in set.bikePhases.Where(c=>c))
            if(Path.GetDirectoryName(AssetDatabase.GetAssetPath(existing)).Replace('\\','/')!=Folder(set)||!existing.name.StartsWith(set.clipPrefix+"_Bike_",StringComparison.Ordinal))throw new InvalidOperationException("A generated bike clip points outside this recipe.");
        if(set.useCustomTweak)EnsureTweak(set);
        if(set.game.style==RoweTrickStyle.Loop)EnsureMovement(set);
        for(int i=0;i<4;i++)
        {
            float duration=i==0?set.enterSeconds:i==3?set.exitSeconds:i==2&&set.useCustomTweak?set.tweakSeconds:.5f;
            var next=i==2&&set.useCustomTweak?Transition(set.bikePoses,1,set.bikeTweakPose,0,duration,false):Transition(set.bikePoses,i==0?0:1,set.bikePoses,i==3?0:1,duration,i==1);
            next.name=set.clipPrefix+"_Bike"+RoweHeldTrickBuilder.Suffixes[i];
            if(set.bikePhases[i]) {EditorUtility.CopySerialized(next,set.bikePhases[i]);UnityEngine.Object.DestroyImmediate(next);}
            else
            {
                string path=Folder(set)+"/"+next.name+".anim";
                if(File.Exists(path))throw new InvalidOperationException("A file already uses this bike output name: "+path);
                AssetDatabase.CreateAsset(next,path);set.bikePhases[i]=next;
            }
            EditorUtility.SetDirty(set.bikePhases[i]);AssetDatabase.SaveAssetIfDirty(set.bikePhases[i]);
        }
        EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
    }
}

public sealed partial class RoweIKPoseEditor
{
    [SerializeField] bool bikeSelected, bikeLocalAxes=true, bikeGizmos=true;
    [SerializeField] int selectedBike;
    [SerializeField] int turnAxis=1;
    [SerializeField] bool reverseTurn;
    bool pendingBike;
    bool PairedMovement => recipe && clip && clip==RoweBikeAnimation.RiderMovement(recipe) && IsLoopStyle;
    AnimationClip CurrentBikeWorking => !recipe || !recipe.authorBike ? null : clip==recipe.poses ? recipe.bikePoses : clip==recipe.tweakPose ? recipe.bikeTweakPose : PairedMovement ? recipe.bikeMotionLoop : null;
    void EnableBikePosing()
    {
        Stop();
        ResolveStudioRigs(true);
        RoweBikeAnimation.Enable(recipe,studioRider,studioBike);
        if(IsLoopStyle && studioStep==2)StartPairedMovement();else BeginStudioPose(studioStep<3?studioStep:1);
        bikeSelected=true;exportedBundle=null;status="Bike authoring enabled. Cyan controls key the animation joints, not the scene meshes.";
    }
    void CommitBike()
    {
        if(!pendingBike || session==null)return;
        var target=CurrentBikeWorking;
        if(!RoweBikeAnimation.Editable(target))throw new InvalidOperationException("Open this recipe's bike pose or paired movement before keying.");
        var pose=RoweBikeAnimation.Capture(session.bikeJoints);
        try {RoweBikeAnimation.Write(target,pose,seconds);AssetDatabase.SaveAssetIfDirty(target);}
        finally {UnityEngine.Object.DestroyImmediate(pose);}
        pendingBike=false;exportedBundle=null;status="Saved bike pose at "+seconds.ToString("F2")+" s.";
    }
    void MoveBikeControl(int index, Vector3 position, Quaternion rotation)
    {
        if(!CurrentBikeWorking || session?.bikeJoints==null)throw new InvalidOperationException("Enable bike authoring and start posing first.");
        if(!float.IsFinite(position.x)||!float.IsFinite(position.y)||!float.IsFinite(position.z)||!float.IsFinite(rotation.x)||!float.IsFinite(rotation.y)||!float.IsFinite(rotation.z)||!float.IsFinite(rotation.w))throw new InvalidOperationException("Bike transform must be finite.");
        var joints=session.bikeJoints;
        var oldWhole=joints[0].localToWorldMatrix;var oldWholeRotation=joints[0].rotation;
        var parents=new[]{joints[2],joints[2],joints[4],joints[5]};
        var contactPositions=new Vector3[4];var contactRotations=new Quaternion[4];
        var follows=new bool[4];
        for(int i=0;i<4;i++)
        {
            // Classify before changing the bike: a large barspin must not itself
            // release a valid grip. Preserve nearby offsets instead of snapping.
            follows[i]=RoweBikeContacts.ShouldFollow(recipe,session,i);
            contactPositions[i]=parents[i].InverseTransformPoint(session.positions[i]);
            contactRotations[i]=Quaternion.Inverse(parents[i].rotation)*session.rotations[i];
        }
        position=RoweBikeAnimation.PivotedPosition(joints,index,position,rotation);
        joints[index].SetPositionAndRotation(position,rotation);pendingBike=true;
        bool follow=false;
        if(index==0 && recipe.riderFollowsWholeBike && !recipe.keepHipsInPlace)
        {
            var delta=joints[0].localToWorldMatrix*oldWhole.inverse;var turn=joints[0].rotation*Quaternion.Inverse(oldWholeRotation);
            for(int i=0;i<session.positions.Length;i++)
            {
                if(i<4 && !follows[i])continue;
                session.positions[i]=delta.MultiplyPoint3x4(session.positions[i]);session.rotations[i]=turn*session.rotations[i];
            }
            follow=true;
        }
        else for(int i=0;i<4;i++)
        {
            if(!follows[i])continue;
            var next=parents[i].TransformPoint(contactPositions[i]);var turn=parents[i].rotation*contactRotations[i];
            if(Vector3.Distance(next,session.positions[i])<.000001f && Quaternion.Angle(turn,session.rotations[i])<.0001f)continue;
            session.positions[i]=next;session.rotations[i]=turn;follow=true;
        }
        if(follow){session.Solve();pending=true;}
    }
    void DrawBikeControls()
    {
        if(!recipe)return;
        EditorGUILayout.Space(8);EditorGUILayout.LabelField("BIKE / animation joints",EditorStyles.boldLabel);
        if(!recipe.authorBike)
        {
            EditorGUILayout.HelpBox("Pose the bike beside the rider and export both together. Your original clips stay untouched.",MessageType.None);
            if(GUILayout.Button(recipe.bikePoses?"Resume saved bike posing":"Add bike posing to this trick",GUILayout.Height(32)))Run(EnableBikePosing);
            return;
        }
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("Disable bike posing (keep edits)"))Run(()=>{Stop();Undo.RecordObject(recipe,"Disable bike authoring");recipe.authorBike=false;EditorUtility.SetDirty(recipe);AssetDatabase.SaveAssetIfDirty(recipe);exportedBundle=null;});
            bool both=EditorGUILayout.ToggleLeft("Same bike both directions",recipe.sameBikeBothDirections);
            if(both!=recipe.sameBikeBothDirections) {Undo.RecordObject(recipe,"Change bike mirrors");recipe.sameBikeBothDirections=both;EditorUtility.SetDirty(recipe);AssetDatabase.SaveAssetIfDirty(recipe);exportedBundle=null;}
        }
        using(new EditorGUI.DisabledScope(session==null || !CurrentBikeWorking))
        {
            for(int row=0;row<3;row++)using(new EditorGUILayout.HorizontalScope())
                for(int i=row*3;i<Mathf.Min(row*3+3,RoweBikeAnimation.Labels.Length);i++)
                    if(GUILayout.Toggle(bikeSelected&&selectedBike==i,RoweBikeAnimation.Labels[i],RoweControlStyle,GUILayout.Height(28))) {bikeSelected=true;selectedBike=i;Repaint();SceneView.RepaintAll();}
            if(bikeSelected)
            {
                handleMode=RoweControlTabs(handleMode,"Move bike part","Rotate bike part");
                bikeLocalAxes=EditorGUILayout.ToggleLeft("Use part's local axes",bikeLocalAxes);
                if(session?.bikeJoints!=null)
                {
                    var joint=session.bikeJoints[selectedBike];
                    EditorGUI.BeginChangeCheck();
                    var p=EditorGUILayout.Vector3Field("Local position",joint.localPosition);var r=EditorGUILayout.Vector3Field("Local rotation",joint.localEulerAngles);
                    if(EditorGUI.EndChangeCheck())Run(()=>{MoveBikeControl(selectedBike,joint.parent.TransformPoint(p),joint.parent.rotation*Quaternion.Euler(r));if(autoKey)Commit();});
                }
                if(RoweBikeAnimation.UsesSteeringPivot(selectedBike))
                    EditorGUILayout.HelpBox(RoweBikeAnimation.Labels[selectedBike]+" rotation uses the shared headset / fork steering line (MachBike Forks_Anchor to Headset_Anchor). The other branch stays fixed while this branch orbits the physical connection.",MessageType.None);
            }
        }
        bikeGizmos=EditorGUILayout.ToggleLeft("Show cyan bike gizmos and joint guides",bikeGizmos);
        EditorGUILayout.LabelField("RIDER CONTACTS",EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();
        bool hands=EditorGUILayout.ToggleLeft("Hands follow bars / forks",recipe.handsFollowBars);
        bool feet=EditorGUILayout.ToggleLeft("Feet follow pedals",recipe.feetFollowPedals);
        bool pinHips=EditorGUILayout.ToggleLeft("Keep hips in place",recipe.keepHipsInPlace);
        bool release=EditorGUILayout.ToggleLeft("Auto-release hands / feet when moved away",recipe.autoReleaseContacts);
        float releaseDistance=RoweBikeContacts.ReleaseDistance(recipe);
        using(new EditorGUI.DisabledScope(!release))
            releaseDistance=EditorGUILayout.Slider("Release distance (cm)",releaseDistance*100,RoweBikeContacts.MinReleaseDistance*100,RoweBikeContacts.MaxReleaseDistance*100)/100;
        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(recipe,"Change rider contacts");recipe.handsFollowBars=hands;recipe.feetFollowPedals=feet;recipe.keepHipsInPlace=pinHips;recipe.riderFollowsWholeBike=true;
            recipe.autoReleaseContacts=release;recipe.contactReleaseDistance=releaseDistance;EditorUtility.SetDirty(recipe);AssetDatabase.SaveAssetIfDirty(recipe);Repaint();SceneView.RepaintAll();
        }
        if(session!=null && CurrentBikeWorking)
        {
            for(int row=0;row<2;row++)using(new EditorGUILayout.HorizontalScope())
                for(int i=row*2;i<row*2+2;i++)
                {
                    bool enabled=i<2?recipe.handsFollowBars:recipe.feetFollowPedals;
                    string state=!enabled?"Follow off":RoweBikeContacts.ShouldFollow(recipe,session,i)?"Following":"Released";
                    EditorGUILayout.LabelField(RoweIKSession.Labels[i]+": "+state,EditorStyles.miniBoldLabel);
                }
        }
        EditorGUILayout.HelpBox("With auto-release on, move a hand or foot beyond the release distance from its normal Riding contact to leave it free. Bring it back within range to follow again; small grip offsets are preserved, never snapped. Each limb releases independently. Hips stay pinned unless you turn off Keep hips in place; released hands/feet still stay where you posed them. No extra IK runs in Streets.",MessageType.None);
        if(PairedMovement && bikeSelected)
        {
            EditorGUILayout.LabelField("ONE-CLICK FULL TURN",EditorStyles.miniBoldLabel);
            turnAxis=EditorGUILayout.Popup("Part's local spin axis",turnAxis,new[]{"X","Y","Z"});
            reverseTurn=EditorGUILayout.ToggleLeft("Reverse direction",reverseTurn);
            if(RowePrimaryButton("Add 360° to "+RoweBikeAnimation.Labels[selectedBike]))Run(BakeBikeTurn);
            EditorGUILayout.HelpBox("Adds a full turn over this loop and bakes linked hands/feet at 60 samples per second (maximum 10 seconds). Ctrl+Z undoes the bake. For a tailwhip select Frame and release feet; Frame turns around the shared headset / fork line. For a barspin select Bars / forks. Check the axis in the gizmo.",MessageType.None);
        }
    }
    void DrawBikeHandles()
    {
        if(!bikeGizmos || !CurrentBikeWorking || session?.bikeJoints==null)return;
        Handles.color=new Color(.1f,.9f,1f);
        for(int i=0;i<session.bikeJoints.Length;i++)
        {
            var joint=session.bikeJoints[i];float size=HandleUtility.GetHandleSize(joint.position)*.055f;
            if(Handles.Button(joint.position,Quaternion.identity,size,size*1.3f,Handles.CubeHandleCap)){bikeSelected=true;selectedBike=i;Repaint();}
            if(bikeSelected&&selectedBike==i)Handles.Label(joint.position+Vector3.up*size*2,"Bike: "+RoweBikeAnimation.Labels[i]);
            if(i>0)Handles.DrawDottedLine(joint.parent.position,joint.position,4);
        }
        if(!bikeSelected)return;
        var selectedJoint=session.bikeJoints[selectedBike];
        var pivot=RoweBikeAnimation.ControlPivot(session.bikeJoints,selectedBike);
        if(RoweBikeAnimation.UsesSteeringPivot(selectedBike))
        {
            RoweBikeAnimation.SteeringReference(session.bikeJoints,out _,out var steeringBasis);
            var axis=steeringBasis*Vector3.up;
            float half=Mathf.Clamp(Vector3.Distance(session.bikeJoints[RoweBikeAnimation.BarsIndex].position,session.bikeJoints[6].position)*.15f,.04f,.075f);
            Handles.color=new Color(1f,.55f,.2f);
            Handles.DrawAAPolyLine(4f,pivot-axis*half,pivot+axis*half);
            Handles.SphereHandleCap(0,pivot,Quaternion.identity,HandleUtility.GetHandleSize(pivot)*.065f,EventType.Repaint);
            Handles.Label(pivot+axis*half,"Shared headset / fork pivot");
            Handles.color=new Color(.1f,.9f,1f);
        }
        EditorGUI.BeginChangeCheck();var position=selectedJoint.position;var rotation=selectedJoint.rotation;
        if(handleMode==0)position=Handles.PositionHandle(position,bikeLocalAxes?rotation:Quaternion.identity);
        else
        {
            var controlRotation=RoweBikeAnimation.ControlRotation(session.bikeJoints,selectedBike);
            var nextControlRotation=Handles.RotationHandle(controlRotation,pivot);
            rotation=RoweBikeAnimation.JointRotationFromControl(session.bikeJoints,selectedBike,nextControlRotation);
        }
        if(EditorGUI.EndChangeCheck())Run(()=>MoveBikeControl(selectedBike,position,rotation));
    }
    void StartPairedMovement()
    {
        Stop();var movement=RoweBikeAnimation.RiderMovement(recipe);
        if(!RoweIKKeys.Editable(movement))throw new InvalidOperationException("Choose an editable Humanoid movement clip first.");
        RoweBikeAnimation.EnsureMovement(recipe);clip=movement;seconds=0;guided=true;studioStep=2;Sample();
    }
    void BakeBikeTurn()
    {
        Commit();
        if(!PairedMovement || !CurrentBikeWorking)throw new InvalidOperationException("Open paired movement with bike authoring enabled first.");
        RoweBikeContacts.EnsureReference(recipe,EnsureSession());
        var riderTarget=clip;var bikeTarget=CurrentBikeWorking;
        if(clip.length<=0 || clip.length>10)throw new InvalidOperationException("One-click turns support movement clips up to 10 seconds. Shorten the loop first.");
        var riderSource=Instantiate(riderTarget);var bikeSource=Instantiate(bikeTarget);
        var riderResult=Instantiate(riderTarget);var bikeResult=Instantiate(bikeTarget);
        riderResult.name=riderTarget.name;bikeResult.name=bikeTarget.name;
        bool linked=selectedBike==0?(!recipe.keepHipsInPlace&&recipe.riderFollowsWholeBike)||recipe.handsFollowBars||recipe.feetFollowPedals:selectedBike==2?recipe.handsFollowBars:selectedBike<6&&recipe.feetFollowPedals;
        try
        {
            bikeResult.ClearCurves();if(linked)riderResult.ClearCurves();
            var axis=turnAxis==0?Vector3.right:turnAxis==1?Vector3.up:Vector3.forward;
            int count=Mathf.CeilToInt(riderSource.length*60);
            for(int i=0;i<=count;i++)
            {
                if(!Application.isBatchMode && i%15==0 && EditorUtility.DisplayCancelableProgressBar("RoweMods · Baking full turn","Building bike animation and rider contacts…",(float)i/count))throw new OperationCanceledException("Bake cancelled. Original clips were not changed.");
                float t=(float)i/count*riderSource.length;
                EnsureSession().Sample(riderSource,bikeSource,t,t);
                var joint=session.bikeJoints[selectedBike];
                Quaternion nextRotation;
                if(RoweBikeAnimation.UsesSteeringPivot(selectedBike))
                {
                    RoweBikeAnimation.SteeringReference(session.bikeJoints,out _,out var basis);
                    var worldAxis=basis*axis;
                    var delta=Quaternion.AngleAxis((reverseTurn?-360:360)*(float)i/count,worldAxis);
                    nextRotation=delta*joint.rotation;
                }
                else
                {
                    var spin=joint.localRotation*Quaternion.AngleAxis((reverseTurn?-360:360)*(float)i/count,axis);
                    nextRotation=joint.parent.rotation*spin;
                }
                MoveBikeControl(selectedBike,joint.position,nextRotation);
                var bikePose=RoweBikeAnimation.Capture(session.bikeJoints);
                try {RoweBikeAnimation.Write(bikeResult,bikePose,t,false,false);}finally{DestroyImmediate(bikePose);}
                if(linked)
                {
                    var riderPose=session.Capture(t);try{RoweIKKeys.Write(riderResult,riderPose,t,false);}finally{DestroyImmediate(riderPose);}
                }
            }
            bikeResult.EnsureQuaternionContinuity();
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Bake bike full turn");
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]{riderTarget,bikeTarget},"Bake bike full turn");
            EditorUtility.CopySerialized(bikeResult,bikeTarget);
            if(linked)EditorUtility.CopySerialized(riderResult,riderTarget);
            EditorUtility.SetDirty(bikeTarget);EditorUtility.SetDirty(riderTarget);AssetDatabase.SaveAssetIfDirty(bikeTarget);AssetDatabase.SaveAssetIfDirty(riderTarget);
            Undo.CollapseUndoOperations(group);exportedBundle=null;
        }
        finally
        {
            pending=false;pendingBike=false;StopWithoutCommit();
            if(!Application.isBatchMode)EditorUtility.ClearProgressBar();
            DestroyImmediate(riderSource);DestroyImmediate(bikeSource);DestroyImmediate(riderResult);DestroyImmediate(bikeResult);
        }
        seconds=0;Sample();status="Full turn baked into rider + bike clips. Preview it; Ctrl+Z undoes this bake.";
    }
    void DrawPairedMovement()
    {
        if(GUILayout.Button("Pose rider + bike on one timeline",GUILayout.Height(34)))Run(StartPairedMovement);
        if(PairedMovement)
        {
            EditorGUI.BeginChangeCheck();float t=EditorGUILayout.Slider("Shared time (seconds)",seconds,0,clip.length);
            if(EditorGUI.EndChangeCheck())Run(()=>{Commit();playing=false;seconds=Mathf.Round(t*clip.frameRate)/clip.frameRate;Sample();});
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(playing?"Pause":"Play both"))Run(()=>{Commit();if(session==null)Sample();playing=!playing;lastTick=EditorApplication.timeSinceStartup;});
                if(GUILayout.Button("Save rider + bike key"))Run(()=>{if(session==null)Sample();pending=true;pendingBike=CurrentBikeWorking;Commit();});
                if(GUILayout.Button("Stop / restore"))Run(Stop);
            }
            EditorGUILayout.LabelField("RIDER / IK controls",EditorStyles.boldLabel);
            ControlRow(0,1,4);ControlRow(2,3);ControlRow(5,6);ControlRow(7,8);
            if(!bikeSelected&&selected<5)handleMode=RoweControlTabs(handleMode,"Move","Rotate");
            autoKey=EditorGUILayout.ToggleLeft("Save after dragging",autoKey);
            DrawWristControls();
        }
        DrawBikeControls();
    }
}
