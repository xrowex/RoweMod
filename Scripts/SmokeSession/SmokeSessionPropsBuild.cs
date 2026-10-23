using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class SmokeSessionPropsBuild {
const string Root="Assets/Output/";
    public static void Build() { var report=new List<string>();
        foreach(string file in Directory.GetFiles("Assets/SourceProps","*.obj"))
        {
            string stem=Path.GetFileNameWithoutExtension(file);
            var importer=(ModelImporter)AssetImporter.GetAtPath(file);importer.globalScale=1;importer.SaveAndReimport();
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(file);
            var instance=UnityEngine.Object.Instantiate(model);instance.name="SmokeSession_"+stem;
            string textureName=stem.Contains("ncam")?"t_noble_camera_cu":stem.Contains("njoint")?"t_noble_joint_cu":stem.Contains("rainbow")?"t_fx_gesture_rainbow_a":"t_fx_gesture_moneysign";
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceProps/"+textureName+".tga");
            var material=new Material(Shader.Find("Standard")){name=textureName,mainTexture=texture};
            material.SetFloat("_Glossiness",.15f);
            string matpath=Root+stem+".mat";AssetDatabase.DeleteAsset(matpath);AssetDatabase.CreateAsset(material,matpath);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            if (stem == "cmn_prp_own_bk_propsfurniture_generic_table01_mesh") SplitTableBong(instance);
            string prefab=Root+instance.name+".prefab";PrefabUtility.SaveAsPrefabAsset(instance,prefab);UnityEngine.Object.DestroyImmediate(instance);
            AssetImporter.GetAtPath(prefab).assetBundleName="rowemod_smoke_session";
            report.Add(stem+": mesh imported, texture="+textureName);
        }

AssetDatabase.SaveAssets();BuildPipeline.BuildAssetBundles("BundleOutput",BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64); }
    // The source combines furniture and bong in one section. Keep the furniture
    // fixed while exposing a pivot at the bong's base for the gesture attachment.
    static void SplitTableBong(GameObject table)
    {
        foreach (var filter in table.GetComponentsInChildren<MeshFilter>())
        {
            Mesh source = filter.sharedMesh;
            Vector3[] vertices = source.vertices; Vector2[] uv = source.uv;
            var furniture = new List<int>(); var bong = new List<int>();
            int[] triangles = source.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                bool isBong = true;
                for (int j = 0; j < 3; j++)
                {
                    int v = triangles[i + j];
                    isBong &= vertices[v].y > .70f && uv[v].y < .5001f;
                }
                var target = isBong ? bong : furniture;
                for (int j = 0; j < 3; j++) target.Add(triangles[i + j]);
            }
            if (bong.Count == 0 || furniture.Count == 0) throw new Exception("Table bong separation failed.");
            if (bong.Count != 9272 * 3 || furniture.Count + bong.Count != triangles.Length)
                throw new Exception("Unexpected table bong geometry.");
            Vector3 pivot = new Vector3(0f, .7900390625f, .484619140625f);
            filter.sharedMesh = SaveSubset(source, furniture, Vector3.zero, "SmokeSession_TableFurniture");
            var prop = new GameObject("SmokeSessionTableBong");
            prop.transform.SetParent(filter.transform, false); prop.transform.localPosition = pivot;
            prop.AddComponent<MeshFilter>().sharedMesh = SaveSubset(source, bong, pivot, "SmokeSession_TableBong");
            prop.AddComponent<MeshRenderer>().sharedMaterials = filter.GetComponent<Renderer>().sharedMaterials;
            Debug.Log("SMOKE_SESSION_TABLE_SPLIT furniture=" + furniture.Count / 3 + " bong=" + bong.Count / 3);
        }
    }

    static Mesh SaveSubset(Mesh source, List<int> triangles, Vector3 pivot, string name)
    {
        var oldVertices = source.vertices; var oldUv = source.uv;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
        var remap = new Dictionary<int, int>();
        foreach (int old in triangles)
        {
            if (!remap.TryGetValue(old, out int index))
            {
                index = vertices.Count; remap.Add(old, index);
                vertices.Add(oldVertices[old] - pivot); uv.Add(oldUv[old]);
            }
            indices.Add(index);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = Root + name + ".asset";
        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

}
public static class SmokeSessionPackBuild { public static void ValidatePackage() { var bundle=AssetBundle.LoadFromFile(Path.GetFullPath("BundleOutput/rowemod_smoke_session"));var clips=bundle.LoadAllAssets<AnimationClip>();if(clips.Length!=9 || clips.Any(c=>!c.humanMotion || c.frameRate!=30 || !c.name.StartsWith("RoweMod_Custom_Emote_SmokeSession_")))throw new Exception("Invalid gesture catalog"); Debug.Log("SMOKE_SESSION_VALIDATED nine Humanoid clips at 30 Hz");var table=bundle.LoadAsset<GameObject>("assets/output/smokesession_cmn_prp_own_bk_propsfurniture_generic_table01_mesh.prefab");if(table==null || !table.GetComponentsInChildren<Transform>().Any(t=>t.name=="SmokeSessionTableBong") || table.GetComponentsInChildren<Renderer>().Length!=2)throw new Exception("Table bong pivot missing");Debug.Log("SMOKE_SESSION_TABLE_PIVOT_VALIDATED");bundle.Unload(true); } public static void Build() { Directory.CreateDirectory("Assets/Output"); Directory.CreateDirectory("BundleOutput"); SmokeSessionDirectBuild.Build(); SmokeSessionPropsBuild.Build(); } }
