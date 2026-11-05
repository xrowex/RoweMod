using UnityEngine;

namespace rowemod.Mods
{
    internal static class StudioGizmoDrawing
    {
        internal static void Line(Vector2 a, Vector2 b, Color color, float thickness)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < .000001f || thickness <= 0) return;
            var matrix = GUI.matrix;
            var previousColor = GUI.color;
            try
            {
                // Compose the segment in logical UI coordinates, then apply the
                // editor's screen scale. RotateAroundPivot pre-multiplies its
                // rotation in screen space, displacing scaled segment pivots.
                GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(a.x, a.y, 0),
                    Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
                GUI.color = color;
                GUI.DrawTexture(new Rect(0, -thickness / 2, delta.magnitude, thickness), Texture2D.whiteTexture);
            }
            finally { GUI.matrix = matrix; GUI.color = previousColor; }
        }
    }
}
