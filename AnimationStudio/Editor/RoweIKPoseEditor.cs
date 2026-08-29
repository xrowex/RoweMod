using System;
using System.IO;
using System.Linq;
using RootMotion;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Authoring only. Final IK and the baker helpers live below Editor as well:
// no IK component, extra rig, or vendor code is added to exported animations.
public sealed class RoweIKSession : IDisposable
{
    public static readonly string[] Labels = { "Left hand", "Right hand", "Left foot", "Right foot", "Hips", "Left elbow", "Right elbow", "Left knee", "Right knee" };
    public static readonly HumanBodyBones[] Bones = { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Hips, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
    public readonly Vector3[] positions = new Vector3[Labels.Length];
    public readonly Quaternion[] rotations = new Quaternion[Labels.Length];
    public readonly Animator rider, bike;
    public readonly Transform[] bikeJoints;
    public readonly Transform[] bones;
    readonly RowePairedWorkbench.Pose original;
    RowePairedWorkbench.Pose sampled;
    readonly IKSolverFullBodyBiped solver = new IKSolverFullBodyBiped();
    readonly FBIKChain[] limbs;
    readonly bool[] bendEdited = new bool[4];
    readonly HumanPoseHandler human;
    bool disposed;

    public RoweIKSession(Animator rider, Animator bike)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use the pose editor outside Play Mode.");
        if (AnimationMode.InAnimationMode()) throw new InvalidOperationException("Stop Animation Preview / Record and Paired Preview first, then try again.");
        if (!rider || !rider.avatar || !rider.avatar.isHuman || !rider.avatar.isValid) throw new InvalidOperationException("Open the animation workbench first. Its rider must have a valid Humanoid avatar.");
        this.rider = rider; this.bike = bike;
        if (bike)
        {
            var joints = RoweBikeAnimation.Paths.Select(p => bike.transform.Find(p)).ToArray();
            if (joints.All(t => t)) bikeJoints = joints;
        }
        original = new RowePairedWorkbench.Pose(bike ? new[] { rider.transform, bike.transform } : new[] { rider.transform });
        bones = Bones.Select(rider.GetBoneTransform).ToArray();
        if (bones.Any(b => !b)) throw new InvalidOperationException("The rider is missing a hand, foot, hips, elbow or knee bone.");
        try
        {
            Transform B(HumanBodyBones b) => rider.GetBoneTransform(b);
            var refs = new BipedReferences {
                root = rider.transform, pelvis = B(HumanBodyBones.Hips), head = B(HumanBodyBones.Head),
                leftThigh = B(HumanBodyBones.LeftUpperLeg), leftCalf = B(HumanBodyBones.LeftLowerLeg), leftFoot = bones[2],
                rightThigh = B(HumanBodyBones.RightUpperLeg), rightCalf = B(HumanBodyBones.RightLowerLeg), rightFoot = bones[3],
                leftUpperArm = B(HumanBodyBones.LeftUpperArm), leftForearm = B(HumanBodyBones.LeftLowerArm), leftHand = bones[0],
                rightUpperArm = B(HumanBodyBones.RightUpperArm), rightForearm = B(HumanBodyBones.RightLowerArm), rightHand = bones[1],
                spine = new[] { B(HumanBodyBones.Spine), B(HumanBodyBones.Chest), B(HumanBodyBones.UpperChest) }.Where(t => t).Distinct().ToArray()
            };
            string error = "";
            if (BipedReferences.SetupError(refs, ref error)) throw new InvalidOperationException(error);
            solver.SetToReferences(refs);
            limbs = new[] { solver.leftArmChain, solver.rightArmChain, solver.leftLegChain, solver.rightLegChain };
            solver.OnPreBend += ApplyBendDirections;
            solver.iterations = 8;
            solver.Initiate(rider.transform);
            if (!solver.initiated) throw new InvalidOperationException("Final IK could not initialize this skeleton.");
            human = new HumanPoseHandler(rider.avatar, rider.transform);
            AnimationMode.StartAnimationMode();
        }
        catch { human?.Dispose(); original.Restore(); throw; }
    }

    public void Sample(AnimationClip clip, AnimationClip bikeClip, float time, float? bikeTime = null)
    {
        string error = RoweClipBinding.Error(clip, rider, RoweClipKind.Rider);
        if (error != null || !clip.isHumanMotion) throw new InvalidOperationException(error ?? "Choose a Humanoid rider clip.");
        if (bikeClip && (!bike || RoweClipBinding.Error(bikeClip, bike, RoweClipKind.Bike) != null)) throw new InvalidOperationException("The optional bike clip does not match the bike rig.");
        original.Restore();
        AnimationMode.BeginSampling();
        try
        {
            AnimationMode.SampleAnimationClip(rider.gameObject, clip, Mathf.Clamp(time, 0, clip.length));
            if (bikeClip) AnimationMode.SampleAnimationClip(bike.gameObject, bikeClip, Mathf.Clamp(bikeTime ?? time, 0, bikeClip.length));
        }
        finally { AnimationMode.EndSampling(); }
        sampled = new RowePairedWorkbench.Pose(rider.transform);
        for (int i = 0; i < bones.Length; i++) { positions[i] = bones[i].position; rotations[i] = bones[i].rotation; }
        for (int i = 0; i < limbs.Length; i++)
        {
            bendEdited[i] = false;
            limbs[i].bendConstraint.weight = 0;
            FollowBendHandle(i);
        }
    }

    void FollowBendHandle(int i)
    {
        // Start on the actual elbow/knee, with no artificial outward offset.
        // Unedited handles follow the joint after a hand, foot or hips edit;
        // an explicitly dragged bend target stays pinned until the next sample.
        positions[5 + i] = bones[5 + i].position;
    }

    public void MoveControl(int index, Vector3 position, Quaternion rotation)
    {
        positions[index] = position;
        if (index < 5) rotations[index] = rotation;
        else bendEdited[index - 5] = true;
        Solve();
    }

    public Vector3 GripPoint(int hand)
    {
        if (hand < 0 || hand > 1) throw new ArgumentOutOfRangeException(nameof(hand));
        // A hand control is at the wrist, not the middle of the palm. Use the
        // middle-finger knuckle to locate a stable, scale-aware palm pivot.
        var knuckle = rider.GetBoneTransform(hand == 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        return knuckle ? Vector3.Lerp(bones[hand].position, knuckle.position, .5f) : bones[hand].position;
    }

    public bool RelaxWrists(Quaternion[] ridingRelative, float strength, int hand = -1,
        bool preserveLeftGrip = false, bool preserveRightGrip = false)
    {
        if (sampled == null) throw new InvalidOperationException("Sample a pose before relaxing wrists.");
        if (hand < -1 || hand > 1 || ridingRelative == null || ridingRelative.Length != 2
            || !RoweWristRelaxation.Valid(ridingRelative[0]) || !RoweWristRelaxation.Valid(ridingRelative[1]) || !float.IsFinite(strength))
            throw new ArgumentException("Choose valid wrist references, a hand and a finite relaxation amount.");
        strength = Mathf.Clamp01(strength);
        if (strength == 0) return false;
        var before = new RowePairedWorkbench.Pose(rider.transform);
        var beforePositions = (Vector3[])positions.Clone();
        var beforeRotations = (Quaternion[])rotations.Clone();
        var starting = new[] { bones[0].rotation, bones[1].rotation };
        float Error(int side) => Quaternion.Angle(bones[side].rotation, bones[5 + side].rotation * ridingRelative[side].normalized);
        var startingError = new[] { Error(0), Error(1) };
        var amounts = new[] { hand != 1 ? strength : 0, hand != 0 ? strength : 0 };
        var pinGrip = new[] { preserveLeftGrip && hand != 1, preserveRightGrip && hand != 0 };
        var grips = new[] { GripPoint(0), GripPoint(1) };
        var gripOffsets = new[] {
            Quaternion.Inverse(starting[0]) * (grips[0] - bones[0].position),
            Quaternion.Inverse(starting[1]) * (grips[1] - bones[1].position)
        };
        void RestoreBefore()
        {
            before.Restore();
            Array.Copy(beforePositions, positions, positions.Length);
            Array.Copy(beforeRotations, rotations, rotations.Length);
        }
        // Extreme Humanoid poses can make a requested full correction MORE
        // cramped. Back off only that hand, and never trade away its grip.
        // All work is bounded to this explicit command; there is no idle pass.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            RestoreBefore();
            if (amounts[0] == 0 && amounts[1] == 0) return false;
            // Both angular passes blend from the same original hand, so a
            // 50% request is not compounded. The current forearm accounts for
            // Humanoid's redistribution of arm twist after the preceding solve.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (hand < 0 || hand == i)
                        rotations[i] = Quaternion.Slerp(starting[i], bones[5 + i].rotation * ridingRelative[i].normalized, amounts[i]);
                    if (pinGrip[i]) positions[i] = grips[i] - rotations[i] * gripOffsets[i];
                }
                Solve();
                // Check the actual visible palm after Humanoid projection,
                // not just an unchanged target array. The palm may be offset
                // from the wrist by several centimetres on the imported rig.
                for (int correction = 0; correction < 4; correction++)
                {
                    bool moved = false;
                    for (int i = 0; i < 2; i++)
                    {
                        if (!pinGrip[i]) continue;
                        var error = grips[i] - GripPoint(i);
                        if (error.sqrMagnitude <= .00025f * .00025f) continue;
                        positions[i] += error;
                        moved = true;
                    }
                    if (!moved) break;
                    Solve();
                }
            }
            bool acceptable = true;
            for (int i = 0; i < 2; i++)
            {
                if (hand >= 0 && hand != i) continue;
                if (Error(i) <= startingError[i] + .1f && (!pinGrip[i] || Vector3.Distance(grips[i], GripPoint(i)) <= .001f)) continue;
                amounts[i] = attempt < 2 ? amounts[i] * .5f : 0;
                acceptable = false;
            }
            if (acceptable) return true;
        }
        RestoreBefore();
        return false;
    }

    public bool SnapAndRelaxWrists(Quaternion[] ridingRelative, float strength, int hand = -1,
        bool preserveLeftGrip = false, bool preserveRightGrip = false,
        Vector3? leftGripTarget = null, Vector3? rightGripTarget = null)
    {
        if (sampled == null) throw new InvalidOperationException("Sample a pose before relaxing wrists.");
        if (hand < -1 || hand > 1 || ridingRelative == null || ridingRelative.Length != 2
            || !RoweWristRelaxation.Valid(ridingRelative[0]) || !RoweWristRelaxation.Valid(ridingRelative[1]) || !float.IsFinite(strength))
            throw new ArgumentException("Choose valid wrist references, a hand and a finite relaxation amount.");
        if (hand == 1) leftGripTarget = null;
        if (hand == 0) rightGripTarget = null;
        if (!leftGripTarget.HasValue && !rightGripTarget.HasValue)
            return RelaxWrists(ridingRelative, strength, hand, preserveLeftGrip, preserveRightGrip);

        var targets = new[] { leftGripTarget, rightGripTarget };
        foreach (var target in targets)
            if (target.HasValue && (!float.IsFinite(target.Value.x) || !float.IsFinite(target.Value.y) || !float.IsFinite(target.Value.z)))
                throw new ArgumentException("Grip snap positions must be finite.");

        var before = new RowePairedWorkbench.Pose(rider.transform);
        var beforePositions = (Vector3[])positions.Clone();
        var beforeRotations = (Quaternion[])rotations.Clone();
        void RestoreBefore()
        {
            before.Restore();
            Array.Copy(beforePositions, positions, positions.Length);
            Array.Copy(beforeRotations, rotations, rotations.Length);
        }
        try
        {
            bool snapped = false;
            for (int i = 0; i < 2; i++)
                if (targets[i].HasValue) rotations[i] = bones[i].rotation;
            // Seat the palm first, without changing the visible hand angle.
            // RelaxWrists then evaluates comfort relative to this seated pose,
            // so its backoff cannot cancel a valid snap or undo the other hand.
            for (int pass = 0; pass < 6; pass++)
            {
                bool moved = false;
                for (int i = 0; i < 2; i++)
                {
                    if (!targets[i].HasValue) continue;
                    var error = targets[i].Value - GripPoint(i);
                    if (error.sqrMagnitude <= .00025f * .00025f) continue;
                    positions[i] += error;
                    moved = true;
                }
                if (!moved) break;
                Solve();
                snapped = true;
            }
            for (int i = 0; i < 2; i++)
                if (targets[i].HasValue && Vector3.Distance(targets[i].Value, GripPoint(i)) > .001f)
                {
                    RestoreBefore();
                    return false; // Keep the authored pose if a grip is unreachable.
                }
            var seated = new RowePairedWorkbench.Pose(rider.transform);
            var seatedPositions = (Vector3[])positions.Clone();
            var seatedRotations = (Quaternion[])rotations.Clone();
            bool relaxed = RelaxWrists(ridingRelative, strength, hand,
                preserveLeftGrip || leftGripTarget.HasValue, preserveRightGrip || rightGripTarget.HasValue);
            // Measure the final result against the original bar targets. The
            // seating and angular solves must not accumulate their tolerances.
            for (int i = 0; i < 2; i++)
                if (targets[i].HasValue && Vector3.Distance(targets[i].Value, GripPoint(i)) > .001f)
                {
                    seated.Restore();
                    Array.Copy(seatedPositions, positions, positions.Length);
                    Array.Copy(seatedRotations, rotations, rotations.Length);
                    relaxed = false;
                    break;
                }
            if (!snapped && !relaxed) RestoreBefore();
            return snapped || relaxed;
        }
        catch { RestoreBefore(); throw; }
    }

    void ApplyBendDirections()
    {
        // Called only inside an explicit drag solve, after full-body reach is solved.
        // The true solver shoulder/thigh positions keep the pole directions correct.
        for (int i = 0; i < limbs.Length; i++)
        {
            var limb = limbs[i];
            Vector3 origin = limb.nodes[0].solverPosition;
            Vector3 direction = Vector3.ProjectOnPlane(positions[5 + i] - origin, limb.nodes[2].solverPosition - origin);
            limb.bendConstraint.weight = bendEdited[i] && direction.sqrMagnitude > .000001f ? 1 : 0;
            if (limb.bendConstraint.weight > 0) limb.bendConstraint.direction = direction.normalized;
        }
    }

    public void Solve()
    {
        if (sampled == null) return;
        sampled.Restore(); // Solve from the sampled pose, never accumulate drag-frame drift.
        // The body effector is a spine node, not the hips. Move the real hips first;
        // let the solver keep the four limb endpoints on their targets.
        bones[4].SetPositionAndRotation(positions[4], rotations[4]);
        // Pin the pelvis plane too. Without these three anchors the full-body
        // solver can undo a hip drag while satisfying the four limb targets.
        foreach (var anchor in new[] { solver.bodyEffector, solver.leftThighEffector, solver.rightThighEffector })
        {
            anchor.position = anchor.bone.position;
            anchor.positionWeight = 1;
        }
        var effectors = new[] { solver.leftHandEffector, solver.rightHandEffector, solver.leftFootEffector, solver.rightFootEffector };
        for (int i = 0; i < 4; i++)
        {
            effectors[i].position = positions[i]; effectors[i].rotation = rotations[i];
            effectors[i].positionWeight = 1; effectors[i].rotationWeight = 1;
        }
        solver.Update();
        // Humanoid distributes twist between upper/lower limbs when it bakes.
        // Preview that same representation so elbows/knees do not visibly change
        // on the first scrub after saving a pose.
        var humanoidPose = new HumanPose();
        human.GetHumanPose(ref humanoidPose);
        human.SetHumanPose(ref humanoidPose);
        for (int i = 0; i < limbs.Length; i++) if (!bendEdited[i]) FollowBendHandle(i);
    }

    public AnimationClip Capture(float time)
    {
        var pose = new HumanPose();
        human.GetHumanPose(ref pose);
        var clip = new AnimationClip();
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            var muscle = new BakerMuscle(i); muscle.SetKeyframe(time, pose.muscles); muscle.SetCurves(ref clip, 0, 1);
        }
        var root = new BakerHumanoidQT("Root");
        root.SetKeyframes(time, pose.bodyPosition, pose.bodyRotation); root.SetCurves(ref clip, 0, 1);
        var goals = new[] { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
        var names = new[] { "LeftHand", "RightHand", "LeftFoot", "RightFoot" };
        for (int i = 0; i < 4; i++)
        {
            var qt = new BakerHumanoidQT(bones[i], goals[i], names[i]);
            qt.SetIKKeyframes(time, rider.avatar, rider.transform, rider.humanScale, pose.bodyPosition * rider.humanScale, pose.bodyRotation);
            qt.SetCurves(ref clip, 0, 1);
        }
        return clip;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); }
        finally { original.Restore(); human.Dispose(); }
    }
}

public static class RoweIKKeys
{
    public static bool Editable(AnimationClip clip) => clip && clip.isHumanMotion && AssetDatabase.GetAssetPath(clip).StartsWith("Assets/CustomClips/", StringComparison.Ordinal) && AssetDatabase.GetAssetPath(clip).EndsWith(".anim", StringComparison.OrdinalIgnoreCase);

    public static void Write(AnimationClip target, AnimationClip pose, float time, bool undo)
    {
        // Align the quaternion sign to the existing pose before writing components.
        foreach (string prefix in new[] { "Root", "LeftHand", "RightHand", "LeftFoot", "RightFoot" })
        {
            var bindings = new[] { "x", "y", "z", "w" }.Select(a => EditorCurveBinding.FloatCurve("", typeof(Animator), prefix + "Q." + a)).ToArray();
            var old = bindings.Select(b => AnimationUtility.GetEditorCurve(target, b)).ToArray();
            var next = bindings.Select(b => AnimationUtility.GetEditorCurve(pose, b)).ToArray();
            if (old.All(c => c != null) && next.All(c => c != null) && Enumerable.Range(0, 4).Sum(i => old[i].Evaluate(time) * next[i].Evaluate(time)) < 0)
                for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(pose, bindings[i], new AnimationCurve(new Keyframe(time, -next[i].Evaluate(time))));
        }
        if (undo) Undo.RegisterCompleteObjectUndo(target, "Key rider IK pose");
        foreach (var binding in AnimationUtility.GetCurveBindings(pose))
        {
            var curve = AnimationUtility.GetEditorCurve(target, binding) ?? new AnimationCurve();
            float value = AnimationUtility.GetEditorCurve(pose, binding).Evaluate(time);
            int existing = Array.FindIndex(curve.keys, k => Mathf.Abs(k.time - time) < .0001f);
            int index = existing >= 0 ? curve.MoveKey(existing, new Keyframe(time, value)) : curve.AddKey(time, value);
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetEditorCurve(target, binding, curve);
        }
        EditorUtility.SetDirty(target);
    }
}

public sealed partial class RoweIKPoseEditor : EditorWindow
{
    [SerializeField] AnimationClip source, clip, bikeClip;
    [SerializeField] string clipName = "My Trick";
    [SerializeField] float duration = 2, sourceTime, seconds;
    [SerializeField] int selected, handleMode;
    [SerializeField] bool autoKey = true;
    RoweIKSession session;
    bool pending, playing;
    double lastTick;
    string status = "Open the paired workbench. Choose a rider clip as the starting pose.";
    Vector2 scroll;
    static readonly Color[] Colors = { new Color(.2f,.8f,1), new Color(1,.55f,.2f), new Color(.2f,.8f,1), new Color(1,.55f,.2f), new Color(.9f,.8f,.2f), new Color(.2f,.8f,1), new Color(1,.55f,.2f), new Color(.2f,.8f,1), new Color(1,.55f,.2f) };

    [MenuItem("Tools/RoweMod/IK Pose Animator")]
    [MenuItem("Tools/RoweMod/Animation Studio")]
    public static void Open()
    {
        var window = GetWindow<RoweIKPoseEditor>("Animation Studio");
        window.minSize = new Vector2(560, 680);
    }

    void OnEnable()
    {
        ReleaseRoweTheme();
        minSize = new Vector2(560, 680);
        titleContent = new GUIContent("Animation Studio");
        if (studioLayoutVersion < 2) { if (studioStep == 2) studioStep = PreviewStep; studioLayoutVersion = 2; }
        if (studioLayoutVersion < 3) { if (studioStep == 3) studioStep = PreviewStep; studioLayoutVersion = 3; }
        SceneView.duringSceneGui += DrawHandles;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorSceneManager.sceneSaving += Saving;
        EditorSceneManager.sceneClosing += Closing;
        Undo.undoRedoPerformed += UndoRedo;
        if (!source) source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Recovered/AnimationClip/Player@BMX_Superman_Enter.anim");
    }
    void OnDisable()
    {
        Stop();
        ReleaseRoweTheme();
        SceneView.duringSceneGui -= DrawHandles;
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        EditorApplication.quitting -= Stop;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        EditorSceneManager.sceneSaving -= Saving;
        EditorSceneManager.sceneClosing -= Closing;
        Undo.undoRedoPerformed -= UndoRedo;
    }
    void PlayModeChanged(PlayModeStateChange _) => Stop();
    void Saving(UnityEngine.SceneManagement.Scene _, string path) => Stop();
    void Closing(UnityEngine.SceneManagement.Scene _, bool removing) => Stop();
    void UndoRedo() { pending = false; pendingBike = false; if (session != null) Run(() => Sample()); }
    void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { status = ex.Message; StopWithoutCommit(); Debug.LogWarning("Rowe IK: " + ex.Message); }
        Repaint(); SceneView.RepaintAll();
    }
    RoweIKSession EnsureSession()
    {
        if (session == null)
        {
            ResolveStudioRigs();
            session = new RoweIKSession(studioRider, studioBike);
        }
        return session;
    }
    void Sample()
    {
        if (EditingRecipePose) RoweWristRelaxation.EnsureReference(recipe, EnsureSession());
        if (CurrentBikeWorking) RoweBikeContacts.EnsureReference(recipe, EnsureSession());
        if (CurrentBikeWorking)
            EnsureSession().Sample(clip, CurrentBikeWorking, seconds, seconds);
        else if (guided && recipe && (clip == recipe.poses || clip == recipe.tweakPose))
            EnsureSession().Sample(clip, recipe.bikeEnterPreview, seconds, clip == recipe.poses && seconds < .5f ? 0 : recipe.bikeEnterPreview ? recipe.bikeEnterPreview.length : 0);
        else EnsureSession().Sample(clip, bikeClip, seconds);
        pending = false; pendingBike = false; SceneView.RepaintAll();
    }
    void Commit()
    {
        if(!pending&&!pendingBike)return;
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Key rider + bike pose");
        try
        {
        CommitBike();
        if (!pending || session == null) return;
        if (!RoweIKKeys.Editable(clip)) throw new InvalidOperationException("Create a working animation first.");
        var pose = session.Capture(seconds);
        try { RoweIKKeys.Write(clip, pose, seconds, true); AssetDatabase.SaveAssetIfDirty(clip); }
        finally { DestroyImmediate(pose); }
        pending = false; status = "Pose keyed at " + seconds.ToString("F2") + " s. Ctrl+Z undoes the last keyed pose.";
        }
        finally
        {
            try { if(recipe && clip==recipe.poses)RoweTweakFromHold.Synchronize(recipe); }
            finally { Undo.CollapseUndoOperations(group); }
        }
    }
    void Stop()
    {
        try { if (pending || pendingBike) Commit(); }
        finally { StopWithoutCommit(); }
    }
    void StopWithoutCommit()
    {
        playing = false; studioRunning = false; pending = false; pendingBike = false;
        session?.Dispose(); session = null;
        SceneView.RepaintAll();
    }
    void Create(bool copyFullAnimation)
    {
        Stop();
        if (!source || !source.isHumanMotion) throw new InvalidOperationException("Choose a PLAYER / Humanoid clip for the starting pose.");
        var working = EnsureSession();
        working.Sample(source, null, sourceTime);
        var result = copyFullAnimation ? Instantiate(source) : new AnimationClip { frameRate = 60 };
        result.name = "RoweMod_Custom_" + string.Join("_", clipName.Split(Path.GetInvalidFileNameChars())).Trim();
        if (!copyFullAnimation)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.startTime = 0; settings.stopTime = duration; settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            foreach (float time in new[] { 0f, duration })
            {
                var pose = working.Capture(time);
                try { RoweIKKeys.Write(result, pose, time, false); } finally { DestroyImmediate(pose); }
            }
        }
        Directory.CreateDirectory("Assets/CustomClips"); AssetDatabase.Refresh();
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CustomClips/" + result.name + ".anim");
        AssetDatabase.CreateAsset(result, path);
        RoweClipBinding.RememberSource(result, source);
        AssetDatabase.SaveAssets();
        clip = result; seconds = 0;
        bikeClip = copyFullAnimation ? RoweClipBinding.Counterpart(source) : null;
        Sample(); EditorGUIUtility.PingObject(clip);
        status = copyFullAnimation ? "Editing a COPY. Existing dense keys remain; edits affect nearby frames. Use a new pose animation for broad movements." : "New animation created. Drag a colored body control, then move the timeline and pose again.";
        SceneView.lastActiveSceneView?.Frame(new Bounds(session.bones[4].position, Vector3.one * 2), false);
    }

    void OnGUI()
    {
        int mode = RoweControlTabs(guided ? 0 : 1, "Trick Builder", "Advanced Clip Editor");
        if ((mode == 0) != guided) Run(() => { Stop(); guided = mode == 0; if (guided && recipe) SelectStudioPoseClip(poseChoice); });
        if (guided) { DrawStudio(); return; }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("DRAG TO POSE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag hands, feet and hips to pose the rider. Elbow and knee controls steer the bend while hands and feet stay pinned. Saved keyframes are normal Humanoid animation.", MessageType.Info);
        using (new EditorGUI.DisabledScope(session != null))
        {
            source = (AnimationClip)EditorGUILayout.ObjectField("Starting rider pose", source, typeof(AnimationClip), false);
            sourceTime = EditorGUILayout.Slider("Starting pose time", sourceTime, 0, source ? source.length : 0);
            clipName = EditorGUILayout.TextField("New animation name", clipName);
            duration = EditorGUILayout.Slider("New duration (seconds)", duration, .25f, 10);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("New pose animation", GUILayout.Height(30))) Run(() => Create(false));
            if (GUILayout.Button("Edit copy of entire clip", GUILayout.Height(30))) Run(() => Create(true));
        }
        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(session != null))
        {
            clip = (AnimationClip)EditorGUILayout.ObjectField("Working animation", clip, typeof(AnimationClip), false);
            bikeClip = (AnimationClip)EditorGUILayout.ObjectField("Bike preview (optional)", bikeClip, typeof(AnimationClip), false);
        }
        using (new EditorGUI.DisabledScope(!RoweIKKeys.Editable(clip)))
        {
            EditorGUI.BeginChangeCheck();
            float t = EditorGUILayout.Slider("Time (seconds)", seconds, 0, clip ? clip.length : 1);
            if (EditorGUI.EndChangeCheck()) Run(() => { Commit(); playing = false; seconds = Mathf.Round(t * clip.frameRate) / clip.frameRate; Sample(); });
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous key")) Run(() => SeekKey(-1));
                if (GUILayout.Button(playing ? "Pause" : "Play")) Run(() => { Commit(); if (session == null) Sample(); playing = !playing; lastTick = EditorApplication.timeSinceStartup; });
                if (GUILayout.Button("Next key")) Run(() => SeekKey(1));
            }
            selected = Mathf.Clamp(selected, 0, RoweIKSession.Labels.Length - 1);
            EditorGUILayout.LabelField("Position / rotation", EditorStyles.boldLabel);
            ControlRow(0, 1, 4);
            ControlRow(2, 3);
            EditorGUILayout.LabelField("Bend direction", EditorStyles.boldLabel);
            ControlRow(5, 6);
            ControlRow(7, 8);
            if (!bikeSelected && selected < 5) handleMode = GUILayout.Toolbar(handleMode, new[] { "Move", "Rotate" });
            else EditorGUILayout.HelpBox("Move this control around the limb to aim its bend. A fully straight arm or leg must bend first: move its hand or foot closer to the body.", MessageType.None);
            autoKey = EditorGUILayout.ToggleLeft("Auto-key after dragging (recommended)", autoKey);
            EditorGUILayout.HelpBox("Blue = left side. Orange = right side. Yellow = hips. Save Pose keys the whole body, including elbow/knee bends. Scrubbing or closing also saves an unfinished pose.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show controls", GUILayout.Height(32))) Run(() => { Commit(); playing = false; Sample(); });
                if (GUILayout.Button("Save pose key", GUILayout.Height(32))) Run(() => { if (session == null) Sample(); pending = true; pendingBike = CurrentBikeWorking; Commit(); });
                if (GUILayout.Button("Stop / restore scene", GUILayout.Height(32))) Run(Stop);
            }
            if (GUILayout.Button("Discard unkeyed pose")) Run(() => { pending = false; pendingBike = false; Sample(); });
            DrawWristControls();
            if (CurrentBikeWorking) DrawBikeControls();
        }
        if (clip) EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(clip), EditorStyles.miniLabel, GUILayout.Height(20));
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }
    void ControlRow(params int[] controls)
    {
        using (new EditorGUILayout.HorizontalScope())
            foreach (int index in controls)
                if (GUILayout.Toggle(!bikeSelected && selected == index, RoweIKSession.Labels[index], RoweControlStyle, GUILayout.Height(28))) { selected = index; bikeSelected = false; }
    }
    void SeekKey(int direction)
    {
        Commit(); playing = false;
        var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"));
        var times = curve?.keys.Select(k => k.time).ToArray() ?? new[] { 0f, clip.length };
        seconds = direction < 0 ? times.Where(t => t < seconds - .0001f).DefaultIfEmpty(0).Max() : times.Where(t => t > seconds + .0001f).DefaultIfEmpty(clip.length).Min();
        Sample();
    }
    void DrawHandles(SceneView view)
    {
        if (session == null || playing || (guided && recipe && (studioStep >= GameSetupStep || (studioStep == 2 && IsLoopStyle && !PairedMovement))) || !session.rider || !AnimationMode.InAnimationMode()) return;
        bool released = Event.current.rawType == EventType.MouseUp;
        var oldColor = Handles.color; var oldDepth = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        try
        {
            for (int i = 0; i < RoweIKSession.Labels.Length; i++)
            {
                Handles.color = Colors[i];
                float size = HandleUtility.GetHandleSize(session.positions[i]) * .055f;
                if (Handles.Button(session.positions[i], Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap)) { selected = i; bikeSelected = false; Repaint(); }
                Handles.Label(session.positions[i] + Vector3.up * size * 1.6f, RoweIKSession.Labels[i]);
                Handles.DrawDottedLine(session.bones[i].position, session.positions[i], 3);
            }
            DrawBikeHandles();
            if (!bikeSelected || !CurrentBikeWorking)
            {
                Handles.color = Colors[selected];
                EditorGUI.BeginChangeCheck();
                Vector3 position = session.positions[selected]; Quaternion rotation = session.rotations[selected];
                if (handleMode == 0 || selected >= 5) position = Handles.PositionHandle(position, Quaternion.identity);
                else rotation = Handles.RotationHandle(rotation, position);
                if (EditorGUI.EndChangeCheck()) Run(() => { session.MoveControl(selected, position, rotation); pending = true; });
            }
            // Unity handles can consume MouseUp before later scene callbacks.
            // Also commit on the first repaint with no captured drag control.
            if (autoKey && (pending || pendingBike) && (released || GUIUtility.hotControl == 0)) Run(Commit);
        }
        finally { Handles.color = oldColor; Handles.zTest = oldDepth; }
    }
    void Tick()
    {
        if (studioRunning)
        {
            if ((previewPhase != PreviewHold && previewPhase != PreviewTweaked) || session == null || !session.rider || !AnimationMode.InAnimationMode()) Run(TickHeldPreview);
            return;
        }
        if (!playing) return; // No solver updates while idle or window is closed.
        if (session == null || !session.rider || !AnimationMode.InAnimationMode()) { StopWithoutCommit(); return; }
        double now = EditorApplication.timeSinceStartup;
        seconds += (float)Math.Min(.1, now - lastTick); lastTick = now;
        if (seconds >= clip.length) { seconds = clip.length; playing = false; }
        Run(Sample);
    }
}
