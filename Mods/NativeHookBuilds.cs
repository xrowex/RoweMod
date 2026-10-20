using System;
using System.Collections.Generic;

namespace rowemod.Mods
{
    // Every address in each profile is audited across all Cpp2IL metadata methods.
    // The original RVA identifies a target; it is never accepted on an unknown hash.
    internal static class NativeHookBuilds
    {
        internal const string OriginalHash = "3D304228003AEEB7E96EE7588AD92526D67EADD3C5DB530F7BC0D661399D5EF1";
        internal const string UpdatedHash = "2D70F57905A2F3A79A2BC75FA503F9AB251AEACFC66CC72F78670ECBFAA8EEC2";

        private static readonly Dictionary<long, long> UpdatedRvas = new Dictionary<long, long>
        {
            [0x8B0A50] = 0x9716F0, // TrickControllerV2.RequestTweak
            [0x8B0E30] = 0x971AD0, // TrickControllerV2.Tweak
            [0x85BE70] = 0x8D2950, // VehicleAnimationInputHandler.SetInteger
            [0xA61F70] = 0xBEA6F0, // FusionBootstrap.DrawServerBrowserHostMapControls
            [0xD53AA0] = 0xEC9A10, // NetworkRunner.StartGame
            [0x9F47C0] = 0xB19D80, // NetworkPlayer.Spawned
            [0x7DE910] = 0x854F80, // EquipSlot.ApplyRenderPolicy
            [0x7F15C0] = 0x87B630, // EquipSlot.<EnumEquip>d__111.MoveNext
            [0x2983750] = 0x2C571A0, // HumanIK.OnAnimatorIK
            [0x298C9E0] = 0x2C631A0, // UnityIKLimb.UpdateIK
            [0x89A960] = 0x901490, // VehicleFootPedalAnimationRig.LateUpdate
            [0x8077B0] = 0x892840, // BikeGrindPoser.SetInputData
            [0x837760] = 0x8A5A50, // QuaternionPDDrive.Tick (two arguments)
            [0x90DA80] = 0x9A9D30, // DroneController.LocalSpawnBullet (four arguments)
            [0x90E100] = 0x9AA540, // DroneController.RPC_FireBullet (five arguments)
            [0x8544D0] = 0x8C9BA0, // TrickDetection.RecordTrickToHistory
            [0x9F3220] = 0xB176F0, // NetworkPlayer.Despawned
        };

        internal static bool IsSupported(string hash) => hash == OriginalHash || hash == UpdatedHash;

        internal static long ResolveRva(string hash, long originalRva)
        {
            if (!IsSupported(hash))
                throw new InvalidOperationException("This game build has not been audited for native RoweMod hooks.");
            if (!UpdatedRvas.TryGetValue(originalRva, out long updatedRva))
                throw new InvalidOperationException($"Native target 0x{originalRva:X} has not been audited.");
            return hash == OriginalHash ? originalRva : updatedRva;
        }
    }
}
