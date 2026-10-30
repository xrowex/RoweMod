using System;
namespace rowemod.Mods
{
    internal static class StudioGizmoMath
    {
        internal static float SegmentDistance(float x, float y, float ax, float ay, float bx, float by, out float fraction)
        {
            float dx = bx - ax, dy = by - ay, length = dx * dx + dy * dy;
            fraction = length < .000001f ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / length, 0, 1);
            dx = x - ax - fraction * dx; dy = y - ay - fraction * dy;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
        internal static float AngleDelta(float from, float to)
        {
            float delta = (to - from) % 360;
            return delta > 180 ? delta - 360 : delta < -180 ? delta + 360 : delta;
        }
    }
}
