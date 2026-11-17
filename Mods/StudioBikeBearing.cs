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
        internal static (Vector3 point, Quaternion basis) RebaseSteering(Transform physicalFrame, Transform physicalSteering,
            Transform animatedFrame, Transform animatedSteering, Vector3 point, Quaternion basis)
        {
            // The chassis and steering bodies can move independently of the animation skeleton.
            // Transfer only the socket's offset from the physical hinge, in chassis space.
            var offset = physicalFrame.InverseTransformPoint(point) - physicalFrame.InverseTransformPoint(physicalSteering.position);
            return (animatedSteering.position + animatedFrame.TransformPoint(offset) - animatedFrame.position,
                animatedFrame.rotation * Quaternion.Inverse(physicalFrame.rotation) * basis);
        }
        internal static void Apply(Transform node, Vector3 pivot, Quaternion basis, Vector3 rotation, Vector3 position, float weight,
            Transform follower = null)
        {
            // The native animation binds the frame and steering as sibling paths.
            // Carry the steering with the frame without changing either clip binding.
            Vector3 followerPosition = follower == null ? default : node.InverseTransformPoint(follower.position);
            Quaternion followerRotation = follower == null ? default : Quaternion.Inverse(node.rotation) * follower.rotation;
            var delta = basis * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(rotation.x, rotation.y, rotation.z), weight) * Quaternion.Inverse(basis);
            node.position = pivot + delta * (node.position - pivot);
            node.rotation = delta * node.rotation;
            node.localPosition += position * weight;
            if (follower != null)
            {
                follower.position = node.TransformPoint(followerPosition);
                follower.rotation = node.rotation * followerRotation;
            }
        }
    }
}
