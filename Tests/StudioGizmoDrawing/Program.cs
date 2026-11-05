using System;
using rowemod.Mods;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static bool Near(Vector3 a, Vector3 b) => MathF.Abs(a.x - b.x) < .003f && MathF.Abs(a.y - b.y) < .003f && MathF.Abs(a.z - b.z) < .003f;
    private static void Main()
    {
        var oldColor = new Color(.2f, .3f, .4f); var axisColor = new Color(1, .2f, .1f);
        foreach (float scale in new[] { .8f, 1, 1.2f, 1312f / 900, 1.6f, 2.4f })
        {
            var parent = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.matrix = parent; GUI.color = oldColor;
            bool endpoints = true, restored = true, thickness = true;
            // A ring at the right side of the editor reproduces the reported
            // fragments leaving the viewport at non-900-pixel window heights.
            for (int i = 0; i < 72; i++)
            {
                float from = i * 2 * MathF.PI / 72, to = (i + 1) * 2 * MathF.PI / 72;
                var a = new Vector2(1080 + 75 * MathF.Cos(from), 400 + 75 * MathF.Sin(from));
                var b = new Vector2(1080 + 75 * MathF.Cos(to), 400 + 75 * MathF.Sin(to));
                StudioGizmoDrawing.Line(a, b, axisColor, 2);
                var draw = GUI.Draws[^1];
                endpoints &= Near(draw.matrix.MultiplyPoint(new Vector3(0, 0, 0)), parent.MultiplyPoint(new Vector3(a.x, a.y, 0))) &&
                    Near(draw.matrix.MultiplyPoint(new Vector3(draw.rect.width, 0, 0)), parent.MultiplyPoint(new Vector3(b.x, b.y, 0)));
                restored &= GUI.matrix.value == parent.value && GUI.color.r == oldColor.r && GUI.color.g == oldColor.g && GUI.color.b == oldColor.b;
                thickness &= draw.rect.x == 0 && draw.rect.y == -1 && draw.rect.height == 2 && draw.color.r == axisColor.r;
            }
            Check(endpoints, $"ring segment endpoints remain at projected joint coordinates at scale {scale}");
            Check(restored, $"each segment restores the surrounding UI matrix/color at scale {scale}");
            Check(thickness, $"stroke stays centered and uses its axis color at scale {scale}");
        }
        var a0 = new Vector2(1080, 400); var b0 = new Vector2(1086, 406);
        var scaled = Matrix4x4.Scale(new Vector3(1312f / 900, 1312f / 900, 1));
        // The previous RotateAroundPivot path pre-multiplied the pivot transform.
        var old = Matrix4x4.TRS(new Vector3(a0.x, a0.y, 0), Quaternion.Euler(0, 0, 45), Vector3.one) *
            Matrix4x4.TRS(new Vector3(-a0.x, -a0.y, 0), Quaternion.Euler(0, 0, 0), Vector3.one) * scaled;
        var oldStart = old.MultiplyPoint(new Vector3(a0.x, a0.y, 0));
        var expected = scaled.MultiplyPoint(new Vector3(a0.x, a0.y, 0));
        Check(MathF.Abs(oldStart.x - expected.x) > 100, "the old pivot composition reproduces large off-joint displacement at screenshot resolution");

        GUI.matrix = Matrix4x4.TRS(new Vector3(25, 30, 0), Quaternion.Euler(0, 0, 13), new Vector3(1.4f, .9f, 1));
        var inherited = GUI.matrix; StudioGizmoDrawing.Line(a0, b0, axisColor, 3);
        var last = GUI.Draws[^1];
        Check(Near(last.matrix.MultiplyPoint(new Vector3(last.rect.width, 0, 0)), inherited.MultiplyPoint(new Vector3(b0.x, b0.y, 0))), "translated, rotated and nonuniform parent UI transforms compose correctly");
        int count = GUI.Draws.Count;
        StudioGizmoDrawing.Line(a0, a0, axisColor, 2); StudioGizmoDrawing.Line(a0, b0, axisColor, 0);
        Check(GUI.Draws.Count == count, "collapsed and zero-thickness segments do not issue invalid draws");
        GUI.color = oldColor; GUI.ThrowOnDraw = true;
        try { StudioGizmoDrawing.Line(a0, b0, axisColor, 2); }
        catch (InvalidOperationException) { }
        finally { GUI.ThrowOnDraw = false; }
        Check(GUI.matrix.value == inherited.value && GUI.color.r == oldColor.r && GUI.color.g == oldColor.g && GUI.color.b == oldColor.b,
            "draw errors cannot leak a segment transform or color into the rest of the menu");
        Console.WriteLine($"PASS: {checks} gizmo drawing transform checks across six UI scales. Stubbed GUI capture; native rendering remains to be verified.");
    }
}
