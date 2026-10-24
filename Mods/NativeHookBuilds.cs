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
        internal const string October21Hash = "3EBD5AB08931C3BB35E1264739975F0EB6FD2AD56EDFD99E5FA48DA7EABD6E85";
        internal const string October22Hash = "D2B353D46A7B2E696736F4A5C39398E24DC13A66B5F956D696F553E42609C430";
        internal const string October23Hash = "6D42AEB57FA79C2C29014BD35D3693F4983F954A4108218662C6827BC340B460";

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

        private static readonly Dictionary<long, long> October21Rvas = new Dictionary<long, long>
        {
            [0x8B0A50] = 0x9950C0, // TrickControllerV2.RequestTweak
            [0x8B0E30] = 0x9954A0, // TrickControllerV2.Tweak
            [0x85BE70] = 0x918480, // VehicleAnimationInputHandler.SetInteger
            [0xA61F70] = 0xC4C7F0, // FusionBootstrap.DrawServerBrowserHostMapControls
            [0xD53AA0] = 0xF36250, // NetworkRunner.StartGame
            [0x9F47C0] = 0xB66E30, // NetworkPlayer.Spawned
            [0x7DE910] = 0x86A470, // EquipSlot.ApplyRenderPolicy
            [0x7F15C0] = 0x890E00, // EquipSlot.<EnumEquip>d__111.MoveNext
            [0x2983750] = 0x2D7A8B0, // HumanIK.OnAnimatorIK
            [0x298C9E0] = 0x2D86930, // UnityIKLimb.UpdateIK
            [0x89A960] = 0x91BDF0, // VehicleFootPedalAnimationRig.LateUpdate
            [0x8077B0] = 0x8A5DB0, // BikeGrindPoser.SetInputData
            [0x837760] = 0x8C7D10, // QuaternionPDDrive.Tick (two arguments)
            [0x90DA80] = 0x9C7800, // DroneController.LocalSpawnBullet (four arguments)
            [0x90E100] = 0x9C81D0, // DroneController.RPC_FireBullet (five arguments)
            [0x8544D0] = 0x8E2A30, // TrickDetection.RecordTrickToHistory
            [0x9F3220] = 0xB63010, // NetworkPlayer.Despawned
        };

        private static readonly Dictionary<long, long> October22Rvas = new Dictionary<long, long>
        {
            [0x8B0A50] = 0x9956B0, // TrickControllerV2.RequestTweak
            [0x8B0E30] = 0x995A90, // TrickControllerV2.Tweak
            [0x85BE70] = 0x918A50, // VehicleAnimationInputHandler.SetInteger
            [0xA61F70] = 0xC50D20, // FusionBootstrap.DrawServerBrowserHostMapControls
            [0xD53AA0] = 0xF3A5C0, // NetworkRunner.StartGame
            [0x9F47C0] = 0xB69CB0, // NetworkPlayer.Spawned
            [0x7DE910] = 0x86A5E0, // EquipSlot.ApplyRenderPolicy
            [0x7F15C0] = 0x890F70, // EquipSlot.<EnumEquip>d__111.MoveNext
            [0x2983750] = 0x2D7C9D0, // HumanIK.OnAnimatorIK
            [0x298C9E0] = 0x2D88A50, // UnityIKLimb.UpdateIK
            [0x89A960] = 0x91C3C0, // VehicleFootPedalAnimationRig.LateUpdate
            [0x8077B0] = 0x8A61B0, // BikeGrindPoser.SetInputData
            [0x837760] = 0x8C8110, // QuaternionPDDrive.Tick (two arguments)
            [0x90DA80] = 0x9C9220, // DroneController.LocalSpawnBullet (four arguments)
            [0x90E100] = 0x9C9BF0, // DroneController.RPC_FireBullet (five arguments)
            [0x8544D0] = 0x8E2E30, // TrickDetection.RecordTrickToHistory
            [0x9F3220] = 0xB65E80, // NetworkPlayer.Despawned
        };

        private static readonly Dictionary<long, long> October23Rvas = new Dictionary<long, long>
        {
            [0x8B0A50] = 0x993BD0, // TrickControllerV2.RequestTweak
            [0x8B0E30] = 0x993FB0, // TrickControllerV2.Tweak
            [0x85BE70] = 0x916AF0, // VehicleAnimationInputHandler.SetInteger
            [0xA61F70] = 0xC39570, // FusionBootstrap.DrawServerBrowserHostMapControls
            [0xD53AA0] = 0xF22F50, // NetworkRunner.StartGame
            [0x9F47C0] = 0xB526C0, // NetworkPlayer.Spawned
            [0x7DE910] = 0x8685E0, // EquipSlot.ApplyRenderPolicy
            [0x7F15C0] = 0x88EF70, // EquipSlot.<EnumEquip>d__111.MoveNext
            [0x2983750] = 0x2D6FE50, // HumanIK.OnAnimatorIK
            [0x298C9E0] = 0x2D7BED0, // UnityIKLimb.UpdateIK
            [0x89A960] = 0x91A460, // VehicleFootPedalAnimationRig.LateUpdate
            [0x8077B0] = 0x8A41B0, // BikeGrindPoser.SetInputData
            [0x837760] = 0x8C6110, // QuaternionPDDrive.Tick (two arguments)
            [0x90DA80] = 0x9C46D0, // DroneController.LocalSpawnBullet (four arguments)
            [0x90E100] = 0x9C50A0, // DroneController.RPC_FireBullet (five arguments)
            [0x8544D0] = 0x8E0EC0, // TrickDetection.RecordTrickToHistory
            [0x9F3220] = 0xB4E890, // NetworkPlayer.Despawned
        };

        internal static bool IsSupported(string hash) => hash == OriginalHash || hash == UpdatedHash || hash == October21Hash || hash == October22Hash || hash == October23Hash;

        internal static long ResolveRva(string hash, long originalRva)
        {
            if (!IsSupported(hash))
                throw new InvalidOperationException("This game build has not been audited for native RoweMod hooks.");
            var profile = hash == October23Hash ? October23Rvas : hash == October22Hash ? October22Rvas : hash == October21Hash ? October21Rvas : UpdatedRvas;
            if (!profile.TryGetValue(originalRva, out long updatedRva))
                throw new InvalidOperationException($"Native target 0x{originalRva:X} has not been audited.");
            return hash == OriginalHash ? originalRva : updatedRva;
        }
    }
}
