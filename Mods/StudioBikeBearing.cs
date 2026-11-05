using UnityEngine;

namespace rowemod.Mods
{
    // A rigid assembly may have an animation joint away from its physical axle.
    // Keep the equipped visual's axle on the parent socket while retaining spin.
    internal sealed class StudioBikeBearing
    {
        private readonly Transform parent, child;
        private readonly Vector3 parentPoint, childPoint, parentAxis, childAxis;
        private readonly Quaternion localBasis;
        internal Transform Child => child;
        internal Vector3 Pivot => parent.TransformPoint(parentPoint);
        internal Quaternion Basis => parent.rotation * localBasis;
        internal StudioBikeBearing(Transform parent, Transform child, Vector3 socket, Vector3 axle, Vector3 axis, Quaternion basis)
        {
            this.parent = parent; this.child = child;
            parentPoint = parent.InverseTransformPoint(socket); childPoint = child.InverseTransformPoint(axle);
            parentAxis = Quaternion.Inverse(parent.rotation) * axis.normalized;
            childAxis = Quaternion.Inverse(child.rotation) * axis.normalized;
            localBasis = Quaternion.Inverse(parent.rotation) * basis;
        }
        internal void Constrain()
        {
            child.rotation = Quaternion.FromToRotation(child.rotation * childAxis, parent.rotation * parentAxis) * child.rotation;
            child.position += Pivot - child.TransformPoint(childPoint);
        }
        internal static void Apply(Transform node, Vector3 pivot, Quaternion basis, Vector3 rotation, Vector3 position, float weight)
        {
            var delta = basis * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(rotation.x, rotation.y, rotation.z), weight) * Quaternion.Inverse(basis);
            node.position = pivot + delta * (node.position - pivot);
            node.rotation = delta * node.rotation;
            node.localPosition += position * weight;
        }
    }
}
