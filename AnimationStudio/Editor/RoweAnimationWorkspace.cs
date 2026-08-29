using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only tooling. No game scripts, networking or physics are executed.
public static class RoweAnimationWorkspace
{
    public const string ScenePath = "Assets/Scenes/AnimationWorkbench.unity";
    public const string PrefabPath = "Assets/Reference/RiderAndBike.prefab";
    public static string Workspace => Directory.GetParent(Application.dataPath).Parent.FullName;

    [Serializable] public class RigReport
    {
        public string name, avatar;
        public bool validAvatar, humanoid;
        public int transforms, meshRenderers, skinnedMeshes, missingMeshes, missingBones, vertices;
    }
    [Serializable] public class Report
    {
        public string unityVersion, source = "Recovered level3 development scene; not verified against current spawned rider";
        public int animationClips, humanoidClips, missingScripts, omittedEmptyOrUnboundCostumeRenderers;
        public int staticBikeRenderers, riderBonesMovedInSample, bikeBonesMovedInSample;
        public string riderSampleClip, bikeSampleClip;
        public RigReport[] rigs;
        public string[] clipSamples;
        public bool passed;
    }

    // Invoked once in batch mode to validate imports and create the authoring scene.
    public static void Build()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Reference/Materials");
            Directory.CreateDirectory("Assets/CustomClips");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) throw new Exception("Recovered prefab did not import");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var all = root.GetComponentsInChildren<Transform>(true);
            var rider = all.Single(t => t.name == "Human Temp");
            var bike = all.Single(t => t.name == "Bike Skeleton");
            rider.gameObject.SetActive(true);
            bike.gameObject.SetActive(true);

            // Customizer slots in the source scene include two empty meshes and
            // inactive alternate costumes without bone bindings. Keep their
            // transforms, but not their unusable renderer components in the stage.
            int omitted = 0;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!skin.sharedMesh || skin.bones.Any(b => !b))
                {
                    if (skin.sharedMesh && skin.gameObject.activeInHierarchy)
                        throw new Exception("Active mesh has missing bone bindings: " + skin.name);
                    UnityEngine.Object.DestroyImmediate(skin);
                    omitted++;
                }

            var humanMaterial = MakeMaterial("Rider", new Color(.68f, .73f, .78f), .05f);
            var bikeMaterial = MakeMaterial("Bike", new Color(.92f, .31f, .08f), .45f);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                int slots = mesh ? Math.Max(1, mesh.subMeshCount) : 1;
                renderer.sharedMaterials = Enumerable.Repeat(renderer.transform.IsChildOf(rider) ? humanMaterial : bikeMaterial, slots).ToArray();
                if (renderer is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            // The development character has runtime-populated body slots. Bind
            // the separately recovered base body to the SAME existing skeleton.
            var bodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reference/RecoveredBody.prefab");
            var sourceSkin = bodyPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var riderBones = rider.GetComponentsInChildren<Transform>(true).ToDictionary(t => AnimationUtility.CalculateTransformPath(t, rider));
            Transform MapBone(Transform bone)
            {
                string bonePath = AnimationUtility.CalculateTransformPath(bone, bodyPrefab.transform);
                if (!riderBones.TryGetValue(bonePath, out var mapped)) throw new Exception("Base body bone not found: " + bonePath);
                return mapped;
            }
            foreach (var old in rider.GetComponentsInChildren<Renderer>(true)) old.enabled = false;
            var body = new GameObject("Reference Body").AddComponent<SkinnedMeshRenderer>();
            body.transform.SetParent(rider, false);
            body.transform.localPosition = sourceSkin.transform.localPosition;
            body.transform.localRotation = sourceSkin.transform.localRotation;
            body.transform.localScale = sourceSkin.transform.localScale;
            body.sharedMesh = sourceSkin.sharedMesh;
            body.bones = sourceSkin.bones.Select(MapBone).ToArray();
            body.rootBone = MapBone(sourceSkin.rootBone);
            body.localBounds = sourceSkin.localBounds;
            body.sharedMaterials = Enumerable.Repeat(humanMaterial, Math.Max(1, body.sharedMesh.subMeshCount)).ToArray();
            body.updateWhenOffscreen = true;
            // The base body deliberately excludes the separately equipped head.
            // This visibly simple marker is NOT recovered character geometry.
            var headBone = rider.GetComponentsInChildren<Transform>(true).Single(t => t.name == "mixamorig:Head");
            var headMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headMarker.name = "Head motion marker - placeholder";
            UnityEngine.Object.DestroyImmediate(headMarker.GetComponent<Collider>());
            headMarker.transform.SetParent(headBone, false);
            headMarker.transform.localPosition = new Vector3(0, .07f, 0);
            headMarker.transform.localScale = new Vector3(.17f, .22f, .19f);
            headMarker.GetComponent<Renderer>().sharedMaterial = humanMaterial;

            // Bike geometry is a separate physics-owned hierarchy in the game.
            // Keep a static scale/pose reference; do not invent animation wiring.
            var bikePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Reference/BikeStaticReference.prefab");
            var bikeModel = (GameObject)PrefabUtility.InstantiatePrefab(bikePrefab);
            bikeModel.name = "Bike Geometry - STATIC REFERENCE (not driven by clips)";
            bikeModel.SetActive(true);
            foreach (var renderer in bikeModel.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                renderer.sharedMaterials = Enumerable.Repeat(bikeMaterial, mesh ? Math.Max(1, mesh.subMeshCount) : 1).ToArray();
            }
            bikeModel.transform.position = rider.position + new Vector3(1.7f, -rider.position.y, 0);

            var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Recovered" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c).ToArray();
            var report = new Report
            {
                unityVersion = Application.unityVersion,
                animationClips = clips.Length,
                humanoidClips = clips.Count(c => c.isHumanMotion),
                missingScripts = all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                omittedEmptyOrUnboundCostumeRenderers = omitted,
                rigs = new[] { Describe(rider), Describe(bike) },
                staticBikeRenderers = bikeModel.GetComponentsInChildren<Renderer>().Count(r => r.enabled),
                clipSamples = clips.Take(12).Select(c => c.name + " | " + c.length.ToString("F3") + "s | " + AnimationUtility.GetCurveBindings(c).Length + " curves").ToArray()
            };
            var riderClip = clips.First(c => c.isHumanMotion && c.name == "backFlip");
            var bikeClip = clips.First(c => c.name == "BMX@BMX_Bar Hug_Enter");
            report.riderSampleClip = riderClip.name;
            report.bikeSampleClip = bikeClip.name;
            report.riderBonesMovedInSample = SampleAndCount(rider, riderClip);
            report.bikeBonesMovedInSample = SampleAndCount(bike, bikeClip);
            report.passed = report.missingScripts == 0 && report.rigs.All(r => r.validAvatar && r.missingMeshes == 0 && r.missingBones == 0) && report.animationClips > 0 && report.riderBonesMovedInSample > 0 && report.bikeBonesMovedInSample > 0;
            File.WriteAllText(Path.Combine(Workspace, "unity-validation.json"), JsonUtility.ToJson(report, true));
            if (!report.passed) throw new Exception("Rig validation failed; inspect unity-validation.json");

            var visible = root.GetComponentsInChildren<Renderer>().Concat(bikeModel.GetComponentsInChildren<Renderer>()).Where(r => r.enabled).ToArray();
            if (visible.Length == 0) throw new Exception("No active renderers in reference scene");
            var bounds = visible[0].bounds;
            foreach (var r in visible.Skip(1)) bounds.Encapsulate(r.bounds);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Reference Floor (no physics)";
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.position = new Vector3(bounds.center.x, bounds.min.y - .04f, bounds.center.z);
            float width = Math.Max(5f, bounds.size.magnitude * 2f);
            floor.transform.localScale = new Vector3(width, .05f, width);
            floor.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Floor", new Color(.16f, .18f, .21f), 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.52f, .52f, .52f);
            var light = new GameObject("Workbench Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            var camera = new GameObject("Workbench Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .075f, .10f);
            camera.fieldOfView = 38;
            float distance = Math.Max(3.3f, bounds.size.magnitude * 1.8f);
            camera.transform.position = bounds.center + new Vector3(1f, .35f, 1.4f).normalized * distance;
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = Math.Max(100, distance * 5);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            RenderPreview(camera, Path.Combine(Workspace, "RigPreview.png"));
            Debug.Log("ROWE_WORKSPACE_VALIDATED " + JsonUtility.ToJson(report));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    static int SampleAndCount(Transform root, AnimationClip clip)
    {
        var bones = root.GetComponentsInChildren<Transform>(true);
        var positions = bones.Select(t => t.localPosition).ToArray();
        var rotations = bones.Select(t => t.localRotation).ToArray();
        var scales = bones.Select(t => t.localScale).ToArray();
        try
        {
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(root.gameObject, clip, clip.length * .55f);
            AnimationMode.EndSampling();
            return bones.Where((t,i) => Vector3.Distance(t.localPosition, positions[i]) > .0001f || Quaternion.Angle(t.localRotation, rotations[i]) > .1f).Count();
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = positions[i];
                bones[i].localRotation = rotations[i];
                bones[i].localScale = scales[i];
            }
        }
    }

    static RigReport Describe(Transform root)
    {
        var animator = root.GetComponent<Animator>();
        var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        return new RigReport
        {
            name = root.name,
            avatar = animator && animator.avatar ? animator.avatar.name : "MISSING",
            validAvatar = animator && animator.avatar && animator.avatar.isValid,
            humanoid = animator && animator.avatar && animator.avatar.isHuman,
            transforms = root.GetComponentsInChildren<Transform>(true).Length,
            skinnedMeshes = skins.Length,
            meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true).Length,
            missingMeshes = skins.Count(s => !s.sharedMesh),
            missingBones = skins.Sum(s => s.bones.Count(b => !b)),
            vertices = skins.Where(s => s.sharedMesh).Sum(s => s.sharedMesh.vertexCount)
        };
    }

    static Material MakeMaterial(string name, Color color, float metallic)
    {
        string path = "Assets/Reference/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", .3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void RenderPreview(Camera camera, string path)
    {
        var rt = new RenderTexture(1280, 960, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1280, 960, TextureFormat.RGB24, false);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(rt);
    }

    [MenuItem("Tools/RoweMod/Open Animation Workbench")]
    static void OpenScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Tools/RoweMod/Build Custom Rider Clips Bundle")]
    static void BuildClips()
    {
        var candidates = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/CustomClips" })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p).name.StartsWith("RoweMod_Custom_", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var clips = candidates.Where(p => RoweClipBinding.Kind(AssetDatabase.LoadAssetAtPath<AnimationClip>(p)) == RoweClipKind.Rider).ToArray();
        if (clips.Length == 0) { EditorUtility.DisplayDialog("No custom rider clips", "Create and edit a rider clip first.", "OK"); return; }
        if (candidates.Length != clips.Length)
            Debug.LogWarning("Skipped non-rider clips with a RoweMod_Custom_ name: " + string.Join(", ", candidates.Except(clips)) + ". These files were left untouched.");
        var folder = Path.Combine(Workspace, "BundleOutput");
        Directory.CreateDirectory(folder);
        var manifest = BuildPipeline.BuildAssetBundles(folder, new[] {
            new AssetBundleBuild { assetBundleName = "rowemod_custom_authored_clips", assetNames = clips }
        }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Custom clip bundle build failed; inspect the Console.");
        EditorUtility.RevealInFinder(Path.Combine(folder, "rowemod_custom_authored_clips"));
        EditorUtility.DisplayDialog("Bundle ready", "Built " + clips.Length + " rider clip(s) in BundleOutput. Skipped " + (candidates.Length - clips.Length) + " non-rider clip(s). Nothing has been installed into the game.", "OK");
    }
}

public class RoweClipEditor : EditorWindow
{
    AnimationClip source;
    int target;
    string customName = "MyTrick";
    string status = "Select a recovered clip, then make your own editable copy.";

    [MenuItem("Tools/RoweMod/Create Editable Clip")]
    static void Open() => GetWindow<RoweClipEditor>("RoweMod Clips");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Custom Animation Clips", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Source clips remain untouched. The paired workbench animates both rider and visible bike. RoweMod currently imports only rider clips into the game.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        source = (AnimationClip)EditorGUILayout.ObjectField("Source Clip", source, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck() && source) target = RoweClipBinding.Kind(source) == RoweClipKind.Bike ? 1 : 0;
        target = EditorGUILayout.Popup("Animation Target", target, new[] { "Rider (Human Temp)", "Bike Skeleton (connected preview)" });
        if (source) EditorGUILayout.LabelField("Detected clip", RoweClipBinding.Kind(source).ToString());
        customName = EditorGUILayout.TextField("New Clip Name", customName);
        using (new EditorGUI.DisabledScope(!source || string.IsNullOrWhiteSpace(customName)))
            if (GUILayout.Button("Create Copy and Open Animation Window", GUILayout.Height(32))) Prepare();
        EditorGUILayout.HelpBox(status, MessageType.None);
    }

    void Prepare()
    {
        var rootName = target == 0 ? "Human Temp" : "Bike Skeleton";
        var root = Resources.FindObjectsOfTypeAll<Animator>().FirstOrDefault(a => a.gameObject.scene.IsValid() && a.name == rootName);
        if (!root) { status = "Open the AnimationWorkbench scene first."; return; }
        if (AnimationMode.InAnimationMode()) { status = "Stop the current Animation/Paired preview before creating a copy."; return; }
        string mismatch = RoweClipBinding.Error(source, root, target == 0 ? RoweClipKind.Rider : RoweClipKind.Bike);
        if (mismatch != null) { status = mismatch; return; }
        Directory.CreateDirectory("Assets/CustomClips");
        AssetDatabase.Refresh();
        var safeName = string.Concat(customName.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
        string prefix = target == 0 ? "RoweMod_Custom_" : "Bike_Working_";
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CustomClips/" + prefix + safeName + ".anim");
        var clip = Instantiate(source);
        clip.name = Path.GetFileNameWithoutExtension(path);
        clip.hideFlags = HideFlags.None;
        clip.legacy = false;
        AssetDatabase.CreateAsset(clip, path);
        RoweClipBinding.RememberSource(clip, source);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Path.ChangeExtension(path, ".controller"));
        var state = controller.layers[0].stateMachine.AddState(clip.name);
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        root.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
        status = "Created " + path + ". Edit its keys in the Animation window. For both rigs together, stop Animation Preview and open Tools > RoweMod > Paired Rider + Bike Preview.";
    }
}
