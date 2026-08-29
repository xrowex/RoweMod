using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public enum RoweClipKind { Unknown, Rider, Bike }

public static class RoweClipBinding
{
    public static RoweClipKind Kind(AnimationClip clip)
    {
        if (!clip) return RoweClipKind.Unknown;
        if (clip.isHumanMotion) return RoweClipKind.Rider;
        var paths = AnimationUtility.GetCurveBindings(clip).Select(b => b.path).Where(p => p.Length > 0).ToArray();
        if (paths.Length > 0 && paths.All(p => p == "Joints" || p.StartsWith("Joints/") || p == "Controls" || p.StartsWith("Controls/"))) return RoweClipKind.Bike;
        if (paths.Any(p => p.StartsWith("Character_Reference/"))) return RoweClipKind.Rider;
        return RoweClipKind.Unknown;
    }

    public static string Error(AnimationClip clip, Animator animator, RoweClipKind expected)
    {
        if (!clip || !animator) return "Choose a clip and open the paired workbench.";
        if (Kind(clip) != expected) return expected == RoweClipKind.Rider ? "Choose a PLAYER / rider clip, not a BMX / bike clip." : "Choose a BMX / bike clip, not a player clip.";
        var missing = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip))
            .Where(b => b.path.Length > 0 && !animator.transform.Find(b.path)).Select(b => b.path).Distinct().ToArray();
        return missing.Length == 0 ? null : "Clip paths do not match this rig: " + string.Join(", ", missing.Take(3));
    }

    [Serializable] class Origin { public string sourceGuid; }
    public static void RememberSource(AnimationClip copy, AnimationClip source)
    {
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(copy));
        importer.userData = JsonUtility.ToJson(new Origin { sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) });
        AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
    }
    public static AnimationClip Counterpart(AnimationClip clip)
    {
        if (!clip) return null;
        var original = clip;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
        if (importer && !string.IsNullOrEmpty(importer.userData))
        {
            try { var data = JsonUtility.FromJson<Origin>(importer.userData); if (data != null && !string.IsNullOrEmpty(data.sourceGuid)) original = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(data.sourceGuid)) ?? clip; } catch { }
        }
        string name = original.name;
        string other = name.StartsWith("Player@") ? "BMX@" + name.Substring(7) : name.StartsWith("BMX@") ? "Player@" + name.Substring(4) : null;
        if (other == null || other.EndsWith("_Mirror", StringComparison.Ordinal)) return null;
        return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Recovered/AnimationClip/" + other + ".anim");
    }
}

public static class RowePairedWorkbench
{
    public const string ScenePath = "Assets/Scenes/PairedAnimationWorkbench.unity";
    const string Frame = "Joints/Frame_Joint";
    const string Bars = "Joints/Bars_Joint";
    const string Cranks = Frame + "/DriveTrain_Joint";
    public static readonly string[] JointPaths = { Frame, Bars, Frame + "/BackWheel_Joint", Bars + "/FrontWheel_Joint", Cranks, Cranks + "/LeftPedal_Joint", Cranks + "/RightPedal_Joint" };

    [Serializable] public class Part { public string source, joint; public string mesh; }
    [Serializable] public class JointTest { public string joint; public int directlyAttachedParts, respondingParts; }
    [Serializable] public class Report
    {
        public bool passed;
        public string scene, riderClip, bikeClip;
        public int parts, missingScripts, riderTransformsMoved, bikeRenderersMoved;
        public bool wrongTargetRejected, transformsRestored;
        public bool wheelPegsStayFixed;
        public JointTest[] jointTests;
        public Part[] bindings;
        public string[] emptySourceRenderers;
    }

    public static Animator FindRig(string name) => Resources.FindObjectsOfTypeAll<Animator>().FirstOrDefault(a => a.gameObject.scene.IsValid() && a.gameObject.scene.isLoaded && a.name == name);

    // Map by the recovered physical visual groups, not by loose mesh-name guesses.
    static string JointFor(string path)
    {
        if (path.StartsWith("Root/Left Pedal Visuals/")) return Cranks + "/LeftPedal_Joint";
        if (path.StartsWith("Root/Right Pedal Visuals/")) return Cranks + "/RightPedal_Joint";
        if (path.StartsWith("Root/BB Visuals/BB/")) return Frame; // Bearing stays in the frame.
        if (path.StartsWith("Root/BB Visuals/")) return Cranks;
        if (path.StartsWith("Root/Back Wheel Visuals/")) return Frame + "/BackWheel_Joint";
        if (path.StartsWith("Root/Back Wheel Back/")) return Frame; // Pegs must not spin with the tire.
        if (path.StartsWith("Root/Front Wheel Front/") && path.Contains("/Wheel Mesh/")) return Bars + "/FrontWheel_Joint";
        if (path.StartsWith("Root/Front Wheel Front/") && path.Contains("/Pegs/")) return Bars;
        if (path.StartsWith("Root/Front Wheel Visuals/") || path.StartsWith("Root/Headset Visuals/")) return Bars;
        if (path.StartsWith("Root/Chassis Visuals/")) return Frame;
        throw new Exception("Unmapped visible bike part: " + path);
    }

    [MenuItem("Tools/RoweMod/Open Paired Animation Workbench")]
    public static void Open()
    {
        if (AnimationMode.InAnimationMode()) { EditorUtility.DisplayDialog("Stop preview first", "Turn off Preview/Record in the Animation window before opening the paired workbench.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) Build();
        else EditorSceneManager.OpenScene(ScenePath);
        RowePairedPreview.Open();
    }

    // Run only in the isolated validation project; does not overwrite the user's scene.
    public static void BuildBatch()
    {
        try { Build(); Validate(); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    public static void Build()
    {
        if (File.Exists(ScenePath) && !Application.isBatchMode) throw new Exception("Paired scene already exists; open it instead of rebuilding over edits.");
        var scene = EditorSceneManager.OpenScene(RoweAnimationWorkspace.ScenePath);
        var rider = FindRig("Human Temp");
        var bike = FindRig("Bike Skeleton");
        if (!rider || !bike) throw new Exception("Missing rider or bike animation root");
        rider.runtimeAnimatorController = null;
        bike.runtimeAnimatorController = null;
        var model = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Bike Geometry - STATIC"));
        model.transform.SetPositionAndRotation(bike.transform.position, bike.transform.rotation);
        model.transform.localScale = bike.transform.lossyScale;
        Directory.CreateDirectory("Assets/Reference/ConnectedBikeMeshes");
        AssetDatabase.Refresh();
        var bindings = new List<Part>();
        var emptySources = new List<string>();
        var originals = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        foreach (var source in originals)
        {
            string path = AnimationUtility.CalculateTransformPath(source.transform, model.transform);
            string bonePath = JointFor(path);
            var bone = bike.transform.Find(bonePath);
            if (!bone) throw new Exception("Missing joint " + bonePath);
            Mesh mesh;
            if (source is SkinnedMeshRenderer skin)
            {
                if (!skin.sharedMesh || skin.sharedMesh.vertexCount == 0) { emptySources.Add(path); continue; }
                mesh = new Mesh { name = "Baked_" + source.name };
                skin.BakeMesh(mesh); // Retain the equipped tire shape without a second skeleton.
                string assetPath = "Assets/Reference/ConnectedBikeMeshes/Tire_" + bindings.Count + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath)) AssetDatabase.DeleteAsset(assetPath); // This build's generated tire mesh only.
                AssetDatabase.CreateAsset(mesh, assetPath);
            }
            else mesh = source.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh || mesh.vertexCount == 0) { emptySources.Add(path); continue; } // Runtime-populated source slots (e.g. chain).
            var part = new GameObject("Visual - " + source.name);
            part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            part.transform.localScale = source.transform.lossyScale;
            part.transform.SetParent(bone, true); // Capture the rest offset once. No Update/LateUpdate follower.
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            bindings.Add(new Part { source = path, joint = bonePath, mesh = mesh.name });
        }
        UnityEngine.Object.DestroyImmediate(model); // Only the temporary source in this NEW scene.
        bike.gameObject.name = "Bike Skeleton";
        var camera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).First(c => c);
        var visible = rider.GetComponentsInChildren<Renderer>().Concat(bike.GetComponentsInChildren<Renderer>()).Where(r => r.enabled).ToArray();
        var bounds = visible[0].bounds;
        foreach (var r in visible.Skip(1)) bounds.Encapsulate(r.bounds);
        camera.transform.position = bounds.center + new Vector3(1.15f, .25f, 1.3f).normalized * Mathf.Max(3.8f, bounds.size.magnitude * 1.65f);
        camera.transform.LookAt(bounds.center);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "bike-bindings.json"), JsonUtility.ToJson(new Report { bindings = bindings.ToArray(), parts = bindings.Count, emptySourceRenderers = emptySources.ToArray() }, true));
    }

    public sealed class Pose
    {
        readonly Transform[] transforms;
        readonly Vector3[] positions, scales;
        readonly Quaternion[] rotations;
        public Pose(params Transform[] roots)
        {
            transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            positions = transforms.Select(t => t.localPosition).ToArray();
            rotations = transforms.Select(t => t.localRotation).ToArray();
            scales = transforms.Select(t => t.localScale).ToArray();
        }
        public int Changed() => transforms.Where((t,i) => t && (Vector3.Distance(t.localPosition, positions[i]) > .0001f || Quaternion.Angle(t.localRotation, rotations[i]) > .1f || Vector3.Distance(t.localScale, scales[i]) > .0001f)).Count();
        public void Restore() { for (int i = 0; i < transforms.Length; i++) if (transforms[i]) { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i]; } }
    }

    public static void Sample(Animator rider, AnimationClip riderClip, Animator bike, AnimationClip bikeClip, float seconds)
    {
        AnimationMode.BeginSampling();
        try
        {
            AnimationMode.SampleAnimationClip(rider.gameObject, riderClip, Mathf.Clamp(seconds, 0, riderClip.length));
            AnimationMode.SampleAnimationClip(bike.gameObject, bikeClip, Mathf.Clamp(seconds, 0, bikeClip.length));
        }
        finally { AnimationMode.EndSampling(); }
    }

    public static void Validate()
    {
        var rider = FindRig("Human Temp"); var bike = FindRig("Bike Skeleton");
        var riderClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Recovered/AnimationClip/Player@BMX_Superman_Enter.anim");
        var bikeClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Recovered/AnimationClip/BMX@BMX_Superman_Enter.anim");
        if (RoweClipBinding.Error(riderClip, rider, RoweClipKind.Rider) != null || RoweClipBinding.Error(bikeClip, bike, RoweClipKind.Bike) != null) throw new Exception("Paired clip binding validation failed");
        var renderers = bike.GetComponentsInChildren<MeshRenderer>();
        var rest = new Pose(rider.transform, bike.transform);
        var tests = new List<JointTest>();
        bool pegsFixed = true;
        foreach (string path in JointPaths)
        {
            var bone = bike.transform.Find(path);
            var direct = renderers.Where(r => r.transform.parent == bone).ToArray();
            var before = direct.Select(r => r.transform.rotation).ToArray();
            var pegs = renderers.Where(r => r.name.Contains("Peg_")).ToArray();
            var pegRotations = pegs.Select(r => r.transform.rotation).ToArray();
            var pegPositions = pegs.Select(r => r.transform.position).ToArray();
            bone.localRotation *= Quaternion.Euler(21, 0, 0);
            if (path.EndsWith("Wheel_Joint")) pegsFixed &= pegs.Where((r,i) => Quaternion.Angle(r.transform.rotation, pegRotations[i]) > .01f || Vector3.Distance(r.transform.position, pegPositions[i]) > .00001f).Count() == 0;
            tests.Add(new JointTest { joint = path, directlyAttachedParts = direct.Length, respondingParts = direct.Where((r,i) => Quaternion.Angle(r.transform.rotation, before[i]) > 10).Count() });
            rest.Restore();
        }
        var camera = Camera.allCameras.FirstOrDefault() ?? UnityEngine.Object.FindObjectOfType<Camera>();
        var beforePositions = renderers.Select(r => r.transform.position).ToArray();
        var beforeRotations = renderers.Select(r => r.transform.rotation).ToArray();
        var report = new Report { scene = ScenePath, riderClip = riderClip.name, bikeClip = bikeClip.name, parts = renderers.Length, jointTests = tests.ToArray(), wrongTargetRejected = RoweClipBinding.Error(bikeClip, rider, RoweClipKind.Rider) != null && RoweClipBinding.Error(riderClip, bike, RoweClipKind.Bike) != null };
        report.wheelPegsStayFixed = pegsFixed;
        var mapping = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "bike-bindings.json")));
        report.emptySourceRenderers = mapping.emptySourceRenderers;
        try
        {
            AnimationMode.StartAnimationMode();
            Sample(rider, riderClip, bike, bikeClip, .05f);
            beforePositions = renderers.Select(r => r.transform.position).ToArray();
            beforeRotations = renderers.Select(r => r.transform.rotation).ToArray();
            Render(camera, Path.Combine(RoweAnimationWorkspace.Workspace, "PairedPreview-Start.png"));
            var riderRest = new Pose(rider.transform);
            Sample(rider, riderClip, bike, bikeClip, Mathf.Min(riderClip.length, bikeClip.length) * .90f);
            report.riderTransformsMoved = riderRest.Changed();
            report.bikeRenderersMoved = renderers.Where((r,i) => Vector3.Distance(r.transform.position, beforePositions[i]) > .001f || Quaternion.Angle(r.transform.rotation, beforeRotations[i]) > .1f).Count();
            Render(camera, Path.Combine(RoweAnimationWorkspace.Workspace, "PairedPreview-Superman.png"));
        }
        finally { AnimationMode.StopAnimationMode(); rest.Restore(); }
        report.transformsRestored = rest.Changed() == 0;
        report.missingScripts = UnityEngine.Object.FindObjectsOfType<Transform>().Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        report.passed = report.parts == mapping.parts && report.parts >= 40 && report.parts + report.emptySourceRenderers.Length == 50 && tests.All(t => t.directlyAttachedParts > 0 && t.respondingParts == t.directlyAttachedParts) && report.wrongTargetRejected && report.transformsRestored && report.wheelPegsStayFixed && report.riderTransformsMoved > 0 && report.bikeRenderersMoved > 0 && report.missingScripts == 0;
        File.WriteAllText(Path.Combine(RoweAnimationWorkspace.Workspace, "paired-validation.json"), JsonUtility.ToJson(report, true));
        if (!report.passed) throw new Exception("Paired validation failed; see paired-validation.json");
        Debug.Log("ROWE_PAIRED_VALIDATED " + JsonUtility.ToJson(report));
    }
    static void Render(Camera camera, string path)
    {
        var rt = new RenderTexture(1280, 960, 24); var texture = new Texture2D(1280, 960, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
        finally { camera.targetTexture = null; RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt); }
    }
}

public class RowePairedPreview : EditorWindow
{
    [SerializeField] AnimationClip riderClip, bikeClip;
    [SerializeField] float seconds;
    [SerializeField] bool loop = true;
    bool ownsPreview, playing;
    double lastTime;
    Animator rider, bike;
    RowePairedWorkbench.Pose rest;
    string status = "Play or scrub both clips together. Stop this preview before editing keys in the Animation window.";

    [MenuItem("Tools/RoweMod/Paired Rider + Bike Preview")]
    public static void Open() => GetWindow<RowePairedPreview>("Rider + Bike");
    void OnEnable() { minSize = new Vector2(410, 310); EditorApplication.update += Tick; AssemblyReloadEvents.beforeAssemblyReload += Stop; EditorApplication.playModeStateChanged += PlayModeChanged; }
    void OnDisable() { Stop(); EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= Stop; EditorApplication.playModeStateChanged -= PlayModeChanged; }
    void PlayModeChanged(PlayModeStateChange state) { Stop(); }
    void OnGUI()
    {
        EditorGUILayout.LabelField("RIDER + BIKE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Connected animation preview — no game physics. Both clips use the same time; shorter clips hold their last pose.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        var newRider = (AnimationClip)EditorGUILayout.ObjectField("Rider clip", riderClip, typeof(AnimationClip), false);
        var newBike = (AnimationClip)EditorGUILayout.ObjectField("Bike clip", bikeClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck()) { Stop(); riderClip = newRider; bikeClip = newBike; seconds = 0; }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Match bike to rider")) { Stop(); var found = RoweClipBinding.Counterpart(riderClip); if (found) bikeClip = found; else status = "No exact source pair found. Choose the bike clip manually (mirrored clips need explicit pairing)."; }
            if (GUILayout.Button("Superman example")) { Stop(); riderClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Recovered/AnimationClip/Player@BMX_Superman_Enter.anim"); bikeClip = RoweClipBinding.Counterpart(riderClip); seconds = 0; }
        }
        float duration = Mathf.Max(riderClip ? riderClip.length : 0, bikeClip ? bikeClip.length : 0, .01f);
        EditorGUI.BeginChangeCheck();
        float time = EditorGUILayout.Slider("Time (seconds)", seconds, 0, duration);
        if (EditorGUI.EndChangeCheck()) { playing = false; seconds = time; Preview(); }
        loop = EditorGUILayout.Toggle("Loop playback", loop);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "Pause" : "Play both", GUILayout.Height(32))) { if (playing) playing = false; else if (Preview()) { playing = true; lastTime = EditorApplication.timeSinceStartup; } }
            if (GUILayout.Button("Stop / restore pose", GUILayout.Height(32))) Stop();
        }
        if (GUILayout.Button("Use selected custom clip"))
        {
            var clip = Selection.activeObject as AnimationClip;
            if (clip) { Stop(); if (RoweClipBinding.Kind(clip) == RoweClipKind.Rider) riderClip = clip; else if (RoweClipBinding.Kind(clip) == RoweClipKind.Bike) bikeClip = clip; else status = "Selected clip does not match these rigs."; }
        }
        EditorGUILayout.HelpBox(status, MessageType.None);
    }
    bool Preview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { status = "Use this tool in Edit Mode."; return false; }
        rider = RowePairedWorkbench.FindRig("Human Temp"); bike = RowePairedWorkbench.FindRig("Bike Skeleton");
        string error = RoweClipBinding.Error(riderClip, rider, RoweClipKind.Rider) ?? RoweClipBinding.Error(bikeClip, bike, RoweClipKind.Bike);
        if (error != null) { status = error; playing = false; return false; }
        if (bike.GetComponentsInChildren<MeshRenderer>().Length == 0) { status = "Open the PAIRED Animation Workbench first; this scene still has the static bike."; return false; }
        if (!ownsPreview)
        {
            if (AnimationMode.InAnimationMode()) { status = "Turn off Preview/Record in the Animation window first. This tool will not take over an existing preview."; return false; }
            rest = new RowePairedWorkbench.Pose(rider.transform, bike.transform);
            AnimationMode.StartAnimationMode(); ownsPreview = true;
        }
        try { RowePairedWorkbench.Sample(rider, riderClip, bike, bikeClip, seconds); SceneView.RepaintAll(); Repaint(); status = "Both rigs sampled at " + seconds.ToString("F2") + " s. Stop restores the original pose."; return true; }
        catch (Exception ex) { Stop(); status = ex.Message; return false; }
    }
    void Tick()
    {
        if (!playing) return;
        if (!ownsPreview || !AnimationMode.InAnimationMode()) { Stop(); return; }
        double now = EditorApplication.timeSinceStartup;
        seconds += (float)Math.Min(.1, now - lastTime); lastTime = now;
        float duration = Mathf.Max(riderClip ? riderClip.length : 0, bikeClip ? bikeClip.length : 0);
        if (seconds > duration) { if (loop && duration > 0) seconds %= duration; else { seconds = duration; playing = false; } }
        Preview();
    }
    void Stop()
    {
        playing = false;
        if (ownsPreview) { if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); rest?.Restore(); }
        ownsPreview = false; rest = null;
        SceneView.RepaintAll();
    }
}
