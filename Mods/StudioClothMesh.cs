using System.Linq;
using Il2CppMagicaCloth2;
using UnityEngine;

namespace rowemod.Mods
{
    internal static class StudioClothMesh
    {
        internal static bool TrySource(SkinnedMeshRenderer renderer, out Mesh mesh, out Transform[] bones)
        {
            mesh = renderer.sharedMesh;
            bones = renderer.bones.ToArray();
            if (mesh == null) return false;
            var data = MagicaManager.Render?.GetRendererData(renderer.GetInstanceID());
            var original = data?.originalMesh;
            if (original == null || original == mesh) return true;
            // MagicaCloth's working mesh has simulated vertices weighted to an
            // added renderer-space bone. It belongs to the source simulation,
            // so use its untouched mesh and original bone slots in the studio.
            int count = original.bindposes.Length;
            if (count == 0 || count > bones.Length) return false;
            mesh = original;
            bones = bones.Take(count).ToArray();
            return true;
        }
    }
}
