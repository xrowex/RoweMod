using UnityEngine;

namespace rowemod
{
    internal class JsonData
    {
        // Physics variables
        public bool bSpinAssist { get; set; }
        public bool bSpinFlipFix { get; set; }
        public bool bDriftAbility { get; set; }
        public bool bFlightAugment { get; set; }
        public bool bBreakBike { get; set; }
        public bool allowTrickLanders { get; set; }
        public bool alwaysAllowFireTricks { get; set; }
        public bool bDisableLevelInAir { get; set; }
        public bool bManualMovement { get; set; }
        public int lastVehicle { get; set; }
        public float gravity { get; set; }
        public float smallHopForce { get; set; }
        //public float hopForce { get; set; }
        public float sideHopPower { get; set; }

        public float pumpForce { get; set; }
        public float spinTorque { get; set; }
        public float pedalForce { get; set; }
        public float maxSpeed { get; set; }
        public float steerDamp { get; set; }
        public float breakForce { get; set; }
        public float manualAngle { get; set; }
        public float noseManualAngle { get; set; }

        public float quickSpinMultiplier { get; set; }
        public bool bFpvCamera { get; set; }
        
        // Third Person Camera
        public bool bUseOldCam { get; set; }
        public Vector3 tpCameraOffset { get; set; }
        public float tpRecenterSpeed { get; set; }
        public float tpCameraPitch { get; set; }
        public float tpFovValue { get; set; }
        
        //custom session marker
        public string? customSessionMarker { get; set; }
        
        // Custom model and material paths
        public string? bodyModelPath { get; set; }
        public string? bodyMaterialPath { get; set; }
        public string? topModelPath { get; set; }
        public string? topMaterialPath { get; set; }
        public string? glovesModelPath { get; set; }
        public string? glovesMaterialPath { get; set; }
        public string? bottomsModelPath { get; set; }
        public string? bottomsMaterialPath { get; set; }
        public string? socksModelPath { get; set; }
        public string? socksMaterialPath { get; set; }
        public string? shoesModelPath { get; set; }
        public string? shoesMaterialPath { get; set; }
        public string? bustModelPath { get; set; }
        public string? bustMaterialPath { get; set; }
        public string? hatModelPath { get; set; }
        public string? hatMaterialPath { get; set; }
        public string? hairModelPath { get; set; }
        public string? hairMaterialPath { get; set; }
        public string? eyesModelPath { get; set; }
        public string? eyesMaterialPath { get; set; }

        public Dictionary<string, string> bikeMaterials { get; set; } = new Dictionary<string, string>();
        public string? lastLoadedPresetCharacter { get; set; }
        public string? lastLoadedPresetBike { get; set; }
        public float barRotationAngle { get; set; }
        public float seatHeight { get; set; }   
        public float seatRotationX { get; set; }
        // Trick sets (key = TrickSet name, value = list of trick names in slot order)
        public Dictionary<string, List<TrickEntry>> customTricks { get; set; }
            = new Dictionary<string, List<TrickEntry>>();



        // Camera
        public float camLerp { get; set; }
        public float fovValue { get; set; }
        public Vector3 camOffset { get; set; }

        // Misc
        public bool bNeverBail { get; set; }
        public bool bShowHUD { get; set; }
        public bool bDiscoMode { get; set; }
        public bool bVibration { get; set; }
        public float droneMass { get; set; }
        public bool droneBodyToggle { get; set; }
        public bool droneEmitterToggle { get; set; }
        public float menuAccentR { get; set; }
        public float menuAccentG { get; set; }
        public float menuAccentB { get; set; }
        public float sloMoTimer { get; set; }

        // Added for FreeCam collider toggle feature
        public bool bDisableFreeCamCollider { get; set; }

        // Added for Drone collider toggle feature
        public bool bDisableDroneCollider { get; set; }
    }
}
