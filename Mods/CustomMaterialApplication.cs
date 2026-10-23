using UnityEngine;

namespace rowemod.Mods
{
    internal static class CustomMaterialApplication
    {
        internal static int Apply(GameObject equippedItem, Material material)
        {
            if (equippedItem == null || material == null) return 0;
            int applied = 0;
            foreach (var renderer in equippedItem.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    renderer.sharedMaterial = material;
                else
                {
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                applied++;
            }
            return applied;
        }
    }
}
