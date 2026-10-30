using System;
using UnityEngine;

namespace rowemod.Mods
{
    internal static partial class RuntimeAnimationStudio
    {
        private const int RingSegments = 72;
        private static readonly Vector3[] GizmoAxes = { Vector3.right, Vector3.up, Vector3.forward };
        private static readonly Color[] GizmoColors = { new(1, .25f, .2f), new(.25f, .95f, .35f), new(.25f, .55f, 1) };
        private static readonly Vector2[] ringPoints = new Vector2[3 * (RingSegments + 1)];
        private static readonly bool[] ringVisible = new bool[3 * (RingSegments + 1)];
        private static int ringFrame = -1, ringAxis, ringControl;
        private static string ringTarget;
        private static Vector3 ringCenter, ringNormal, ringU, ringV;
        private static Vector2 ringMouse, ringTangent;
        private static float ringLastAngle, ringDegrees;
        private static bool ringUsesPlane;
        private static Quaternion ringStartOffset;
        private static void ReleaseGizmo()
        {
            if (dragMode == 6 && GUIUtility.hotControl == ringControl) GUIUtility.hotControl = 0;
            if (dragMode == 6) { dragMode = 0; gesture = false; }
        }

        private static Vector2 GizmoScreen(Rect viewport, Vector3 world, out bool visible)
        {
            var p = Stage.Camera.WorldToViewportPoint(world); visible = p.z > 0;
            var screen = new Vector2(viewport.x + p.x * viewport.width, viewport.y + (1 - p.y) * viewport.height);
            visible &= viewport.Contains(screen); return screen;
        }
        private static bool RingAngle(Rect viewport, Vector2 mouse, out float angle)
        {
            var ray = Stage.Camera.ViewportPointToRay(new Vector3((mouse.x - viewport.x) / viewport.width, 1 - (mouse.y - viewport.y) / viewport.height, 0));
            float dot = Vector3.Dot(ray.direction, ringNormal); angle = 0;
            if (Mathf.Abs(dot) < .025f) return false;
            float distance = Vector3.Dot(ringCenter - ray.origin, ringNormal) / dot;
            if (distance < 0) return false;
            var direction = ray.origin + ray.direction * distance - ringCenter;
            if (direction.sqrMagnitude < .000001f) return false;
            angle = Mathf.Atan2(Vector3.Dot(direction, ringV), Vector3.Dot(direction, ringU)) * Mathf.Rad2Deg; return true;
        }
        private static void GizmoLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            var matrix = GUI.matrix;
            try
            {
                var delta = b - a;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
                Fill(new Rect(a.x, a.y - thickness / 2, delta.magnitude, thickness), color);
            }
            finally { GUI.matrix = matrix; }
        }
        private static void DrawRotationGizmo(Rect viewport)
        {
            if (Stage.Target(SelectedTarget) == null) return;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            Vector3 center = Stage.ControlPivot(SelectedTarget);
            var orientation = Stage.ControlRotation(SelectedTarget, draft);
            float radius = Mathf.Max(.02f, Vector3.Distance(Stage.Camera.transform.position, center) * Mathf.Tan(Stage.Camera.fieldOfView * .5f * Mathf.Deg2Rad) * 150 / viewport.height);
            if (ringFrame != UnityEngine.Time.frameCount || ringTarget != SelectedTarget)
            {
                ringFrame = UnityEngine.Time.frameCount; ringTarget = SelectedTarget;
                for (int axis = 0; axis < 3; axis++)
                {
                    var n = orientation * GizmoAxes[axis];
                    var u = orientation * (axis == 0 ? Vector3.up : Vector3.right); var v = Vector3.Cross(n, u);
                    for (int i = 0; i <= RingSegments; i++)
                    {
                        float theta = i * 2 * Mathf.PI / RingSegments;
                        ringPoints[axis * (RingSegments + 1) + i] = GizmoScreen(viewport, center + radius * (u * Mathf.Cos(theta) + v * Mathf.Sin(theta)), out ringVisible[axis * (RingSegments + 1) + i]);
                    }
                }
            }
            var e = Event.current; int hovered = -1, segment = 0; float nearest = 9;
            if (viewport.Contains(e.mousePosition))
                for (int axis = 0; axis < 3; axis++)
                    for (int i = 0; i < RingSegments; i++)
                    {
                        if (!ringVisible[axis * (RingSegments + 1) + i] || !ringVisible[axis * (RingSegments + 1) + i + 1]) continue;
                        var a = ringPoints[axis * (RingSegments + 1) + i]; var b = ringPoints[axis * (RingSegments + 1) + i + 1];
                        float distance = StudioGizmoMath.SegmentDistance(e.mousePosition.x, e.mousePosition.y, a.x, a.y, b.x, b.y, out _);
                        if (distance < nearest) { nearest = distance; hovered = axis; segment = i; }
                    }
            if (e.type == EventType.Repaint)
                for (int axis = 0; axis < 3; axis++)
                {
                    bool highlight = dragMode == 6 ? ringAxis == axis : hovered == axis;
                    var color = highlight ? new Color(1, .85f, .15f) : GizmoColors[axis];
                    for (int i = 0; i < RingSegments; i++)
                        if (ringVisible[axis * (RingSegments + 1) + i] && ringVisible[axis * (RingSegments + 1) + i + 1]) GizmoLine(ringPoints[axis * (RingSegments + 1) + i], ringPoints[axis * (RingSegments + 1) + i + 1], color, highlight ? 3 : 2);
                    var label = GizmoScreen(viewport, center + orientation * GizmoAxes[axis] * radius * 1.2f, out bool visible);
                    if (visible) { var old = GUI.color; GUI.color = GizmoColors[axis]; GUI.Label(new Rect(label.x - 6, label.y - 10, 24, 22), axis == 0 ? "X" : axis == 1 ? "Y" : "Z", small); GUI.color = old; }
                }
            if (e.type == EventType.MouseDown && e.button == 0 && hovered >= 0 && dragMode == 0)
            {
                ringAxis = hovered; ringControl = control; ringCenter = center;
                ringNormal = orientation * GizmoAxes[ringAxis]; ringU = orientation * (ringAxis == 0 ? Vector3.up : Vector3.right); ringV = Vector3.Cross(ringNormal, ringU);
                ringUsesPlane = RingAngle(viewport, e.mousePosition, out ringLastAngle);
                ringMouse = e.mousePosition; ringTangent = (ringPoints[ringAxis * (RingSegments + 1) + segment + 1] - ringPoints[ringAxis * (RingSegments + 1) + segment]).normalized;
                ringStartOffset = Quaternion.Euler(draft.Rotation.X, draft.Rotation.Y, draft.Rotation.Z); ringDegrees = 0; dragPose = draft;
                playing = false; dragMode = 6; GUI.FocusControl(""); GUIUtility.hotControl = control; e.Use();
            }
            if (e.type == EventType.MouseDrag && dragMode == 6)
            {
                if (ringUsesPlane && RingAngle(viewport, e.mousePosition, out float angle))
                { ringDegrees += StudioGizmoMath.AngleDelta(ringLastAngle, angle); ringLastAngle = angle; }
                else if (!ringUsesPlane) ringDegrees = Vector2.Dot(e.mousePosition - ringMouse, ringTangent) * 180 / (75 * Mathf.PI);
                var rotation = (ringStartOffset * Quaternion.AngleAxis(ringDegrees, GizmoAxes[ringAxis])).eulerAngles;
                var pose = dragPose;
                pose.Rotation = new StudioVector(dragPose.Rotation.X + StudioGizmoMath.AngleDelta(dragPose.Rotation.X, rotation.x),
                    dragPose.Rotation.Y + StudioGizmoMath.AngleDelta(dragPose.Rotation.Y, rotation.y), dragPose.Rotation.Z + StudioGizmoMath.AngleDelta(dragPose.Rotation.Z, rotation.z));
                ChangePose(pose); SyncFields(); e.Use();
            }
            if (e.rawType == EventType.MouseUp && dragMode == 6)
            { if (GUIUtility.hotControl == ringControl) GUIUtility.hotControl = 0; dragMode = 0; gesture = false; e.Use(); }
        }
    }
}
