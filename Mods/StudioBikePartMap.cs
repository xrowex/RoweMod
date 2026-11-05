using System;

namespace rowemod.Mods
{
    internal static class StudioBikePartMap
    {
        // Relative to the equipped physical Root; indexes match StudioTargets.Bike.
        internal static int Joint(string path)
        {
            bool Under(string prefix) => path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal);
            if (Under("Chassis Visuals") || Under("Back Wheel Back") || Under("BB Visuals/BB")) return 0;
            if (Under("BB Visuals")) return 2;
            bool axle = path.EndsWith("/Axle", StringComparison.Ordinal) || path.Contains("/Axle/", StringComparison.Ordinal);
            if (Under("Left Pedal Visuals")) return axle ? 2 : 3;
            if (Under("Right Pedal Visuals")) return axle ? 2 : 4;
            if (Under("Back Wheel Visuals")) return 6;
            if (Under("Front Wheel Front"))
            {
                // Tuned forks add Offset / Rotator Bone containers. The wheel
                // and fixed peg assemblies retain their own semantic roots.
                if (path.Contains("/Wheel Mesh/", StringComparison.Ordinal) || path.EndsWith("/Wheel Mesh", StringComparison.Ordinal)) return 5;
                if (path.Contains("/Pegs/", StringComparison.Ordinal) || path.EndsWith("/Pegs", StringComparison.Ordinal)) return 1;
            }
            if (Under("Headset Visuals") || Under("Front Wheel Visuals")) return 1;
            return -1;
        }
    }
}
