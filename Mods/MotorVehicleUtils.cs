using Il2CppMashBox.Core.Runtime.Physics.Vehicle;
using rowemod.Utils; // ensure this is included if not already

namespace rowemod.Mods
{
    public static class MotorVehicleUtils
    {
        public static MotorVehicleSettings mxVehicleSettings;

        public static void FindMxVehicleSettings()
        {
            if (Memory.vehicleSettingsInstances == null || Memory.vehicleSettingsInstances.Length == 0)
            {
                Log.Warning("[rowemod] MotorVehicleSettings array is empty. Call Memory.FindObjects() first.");
                return;
            }

            foreach (var vehicle in Memory.vehicleSettingsInstances)
            {
                if (vehicle != null && vehicle.name.Contains("MotorVehicleSettings_MX"))
                {
                    mxVehicleSettings = vehicle;
                    Log.Msg($"[rowemod] Found MX MotorVehicleSettings: {vehicle.name}");
                    return;
                }
            }

            Log.Warning("[rowemod] No MX Vehicle available to display settings.");
        }
    }
}