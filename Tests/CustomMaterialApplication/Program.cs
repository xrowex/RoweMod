using System;
using System.Linq;
using rowemod.Mods;
using UnityEngine;

internal static class Program
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static void Main()
    {
        var original = new Material(); var selected = new Material();
        var visible = new Renderer { sharedMaterials = new[] { original, original } };
        var inactive = new Renderer { sharedMaterials = new[] { original }, enabled = false };
        var empty = new Renderer { sharedMaterials = Array.Empty<Material>() };
        var foreign = new Renderer { sharedMaterials = new[] { original } };
        var item = new GameObject { Renderers = new[] { visible, null, inactive, empty } };
        Check(CustomMaterialApplication.Apply(item, selected) == 3, "all equipped-item renderers receive override");
        Check(visible.sharedMaterials.Length == 2 && visible.sharedMaterials.All(m => m == selected), "all submesh slots receive material");
        Check(inactive.sharedMaterial == selected && !inactive.enabled, "inactive renderer receives material without being enabled");
        Check(empty.sharedMaterial == selected, "empty material array uses first slot");
        Check(foreign.sharedMaterial == original, "outside equipped item remains untouched");
        Check(CustomMaterialApplication.Apply(null, selected) == 0, "missing item is safe");
        Check(CustomMaterialApplication.Apply(item, null) == 0 && visible.sharedMaterial == selected, "missing material preserves existing assignment");
        Check(CustomMaterialApplication.Apply(new GameObject(), selected) == 0, "no renderers is safe");
        Console.WriteLine($"PASS: {checks} equipped-item material checks. Stubbed Unity; native renderers tested separately.");
    }
}
namespace UnityEngine
{
    internal class Material { }
    internal class Renderer
    {
        internal bool enabled = true;
        internal Material[] sharedMaterials;
        internal Material sharedMaterial
        {
            get => sharedMaterials?.Length > 0 ? sharedMaterials[0] : null;
            set { if (sharedMaterials == null || sharedMaterials.Length == 0) sharedMaterials = new[] { value }; else sharedMaterials[0] = value; }
        }
    }
    internal class GameObject
    {
        internal Renderer[] Renderers = Array.Empty<Renderer>();
        internal T[] GetComponentsInChildren<T>(bool includeInactive) where T : class =>
            Renderers.Where(r => r == null || includeInactive || r.enabled).Cast<T>().ToArray();
    }
}
