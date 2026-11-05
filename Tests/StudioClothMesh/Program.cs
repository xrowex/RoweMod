using System;
using System.Collections.Generic;
using System.Linq;
using rowemod.Mods;
using UnityEngine;
using Il2CppMagicaCloth2;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Main()
    {
        var bones = Enumerable.Range(0, 63).Select(i => new GameObject("Bone" + i).transform).ToArray();
        var original = new Mesh { bindposes = new int[61] };
        var working = new Mesh { bindposes = new int[63] };
        var skin = new SkinnedMeshRenderer { sharedMesh = working, bones = bones };
        MagicaManager.Render = new RenderManager();
        MagicaManager.Render.data[skin.GetInstanceID()] = new RenderData { originalMesh = original };
        Check(StudioClothMesh.TrySource(skin, out var mesh, out var resolved) && mesh == original, "preview selects the pre-simulation mesh instead of frozen cloth output");
        Check(resolved.Length == 61 && resolved.Select((b, i) => b == bones[i]).All(x => x), "original bindpose slots retain their bone ordering and omit simulation slots");
        Check(skin.sharedMesh == working && ReferenceEquals(skin.bones, bones) && bones.Length == 63, "source simulation mesh and bone array are never replaced or shortened");
        resolved[0] = null;
        Check(skin.bones[0] != null, "preview bone-array edits cannot alter the source array");
        skin.bones[60] = null;
        Check(StudioClothMesh.TrySource(skin, out mesh, out resolved) && resolved[60] == null, "unused null original bone slots are preserved");
        var plain = new SkinnedMeshRenderer { sharedMesh = original, bones = bones.Take(61).ToArray() };
        Check(StudioClothMesh.TrySource(plain, out mesh, out resolved) && mesh == original && resolved.Length == 61, "ordinary skinned outfits use their existing mesh");
        MagicaManager.Render.data[plain.GetInstanceID()] = new RenderData { originalMesh = original };
        Check(StudioClothMesh.TrySource(plain, out mesh, out resolved) && mesh == original, "already-original cloth meshes require no replacement");
        MagicaManager.Render = null;
        Check(StudioClothMesh.TrySource(plain, out mesh, out resolved) && mesh == original, "outfits remain available when no cloth manager exists");
        MagicaManager.Render = new RenderManager();
        MagicaManager.Render.data[skin.GetInstanceID()] = new RenderData { originalMesh = new Mesh { bindposes = new int[64] } };
        Check(!StudioClothMesh.TrySource(skin, out mesh, out resolved), "invalid original slot counts are rejected without fabricating missing bones");
        MagicaManager.Render.data[skin.GetInstanceID()].originalMesh = new Mesh { bindposes = Array.Empty<int>() };
        Check(!StudioClothMesh.TrySource(skin, out mesh, out resolved), "an invalid unskinned original is rejected");
        skin.sharedMesh = null;
        Check(!StudioClothMesh.TrySource(skin, out mesh, out resolved), "missing render meshes are handled without reading cloth data");
        Console.WriteLine($"PASS: {checks} cloth mesh selection/isolation checks. Native cloth garments are tested in Streets separately.");
    }
}

namespace UnityEngine
{
    public sealed class Mesh { public int[] bindposes; }
    public sealed class SkinnedMeshRenderer
    {
        private static int next;
        private readonly int id = ++next;
        public Mesh sharedMesh;
        public Transform[] bones;
        public int GetInstanceID() => id;
    }
}
namespace Il2CppMagicaCloth2
{
    public sealed class RenderData { public Mesh originalMesh; }
    public sealed class RenderManager
    {
        public readonly Dictionary<int, RenderData> data = new();
        public RenderData GetRendererData(int id) => data.TryGetValue(id, out var value) ? value : null;
    }
    public static class MagicaManager { public static RenderManager Render; }
}
