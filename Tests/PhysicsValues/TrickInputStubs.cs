namespace Il2CppMashBox.BMX_Physics_Development
{
    public sealed class InputHoldPoint : UnityEngine.Object { public float _minHoldValue,_maxHoldValue,_minCancelValue,_maxCancelValue,_minHoldTime,_maxInputVelocity; }
}
namespace Il2CppMashBox.Core.Runtime.TrickSystem
{
    public sealed class TrickDataSets : UnityEngine.Object { public TrickSetData[] TrickSets; }
    public sealed class TrickSetData : UnityEngine.Object { }
}
namespace Il2CppMashBox.BMX_Physics_Development.Animancer_Test.Trick_System.v2
{
    public sealed class TrickSystemBrainV2 : UnityEngine.Object
    {
        public bool enabled; public UnityEngine.GameObject gameObject; public Il2CppMashBox.Core.Runtime.TrickSystem.TrickDataSets _trickDataSets; public int Refreshes;
        public void RefreshTrickSet() => Refreshes++;
    }
}
namespace UnityEngine.InputSystem { public sealed class InputAction { } }
