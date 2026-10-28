using System;
using rowemod.Mods;

internal static class Program
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Main()
    {
        Check(!CustomEquipReadiness.CanSubmit(null), "missing slot is not ready");
        Check(CustomEquipReadiness.CanSubmit(new LegacySlot()), "original wrapper without completion property remains supported");
        Check(CustomEquipReadiness.CanSubmit(new IncompleteSchema { IsEquipRequestComplete = true }), "native completion remains authoritative");
        Check(!CustomEquipReadiness.CanSubmit(new IncompleteSchema()), "missing fields cannot establish idle");
        var empty = new Slot();
        Check(CustomEquipReadiness.CanSubmit(empty), "cleared, empty, idle slot accepts its first custom item despite stale nonces");
        Check(empty._requestNonce == 2 && empty._completedRequestNonce == 0 && empty._lastLoadHandle.ValidCalls == 1,
            "readiness reads native state without rewriting nonces");
        Action<Slot>[] pending = {
            s => s._isAlive = false, s => s._isBusyEquipping = true,
            s => s._go = new object(), s => s._pendingInstance = new object(), s => s._equippingCo = new object(),
            s => s._currentEquipUid = 1, s => s._EquippedUid = 1,
            s => s._equipNextLoad = true, s => s._localReloadPending = true,
            s => s._currentSelectedData = new object(), s => s._lastLoadHandle.Valid = true,
            s => s._lastLoadHandle = null, s => s._lastLoadHandle.Throw = true
        };
        foreach (var mutate in pending)
        {
            var slot = new Slot(); mutate(slot);
            Check(!CustomEquipReadiness.CanSubmit(slot), "active, pending or unknown native work cannot be bypassed");
        }
        Check(CustomEquipReadiness.CanSubmit(new Slot()), "another slot of the cached wrapper type uses its own live state");
        Console.WriteLine($"PASS: {checks} native equip readiness checks. Stubbed slot/handle state; live empty-slot equip verified separately.");
    }
    private sealed class LegacySlot { }
    private sealed class IncompleteSchema { public bool IsEquipRequestComplete { get; set; } }
    private sealed class Slot
    {
        public bool IsEquipRequestComplete => false;
        public bool _isAlive { get; set; } = true;
        public bool _isBusyEquipping { get; set; }
        public object _go { get; set; }
        public object _pendingInstance { get; set; }
        public object _equippingCo { get; set; }
        public ulong _currentEquipUid { get; set; }
        public ulong _EquippedUid { get; set; }
        public bool _equipNextLoad { get; set; }
        public bool _localReloadPending { get; set; }
        public object _currentSelectedData { get; set; }
        public Handle _lastLoadHandle { get; set; } = new Handle();
        public int _requestNonce { get; set; } = 2;
        public int _completedRequestNonce { get; set; }
    }
    private sealed class Handle
    {
        internal bool Valid, Throw;
        internal int ValidCalls;
        public bool IsValid() { ValidCalls++; if (Throw) throw new InvalidOperationException(); return Valid; }
    }
}
