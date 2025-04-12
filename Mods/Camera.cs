using Il2CppCinemachine;
using rowemod.Utils;
using static rowemod.Utils.Memory;
using static rowemod.Config;
using UnityEngine;
using Il2CppMashBox.Core.Runtime.Common.Extension_Methods;

namespace rowemod.Mods
{
    public static class Camera
    {

        public static void Update()
        {
            try
            {
                // Update Camera settings
                if (camTarget != null)
                {
                    camTarget._rotLerp = camLerp;
                    //Log.Msg( $"Camera Lerp: {camLerp}");
                }

                if (tpCameraData != null)
                {
                    tpCameraData._autoRecenterSpeed = tpRecenterSpeed;
                    tpCameraData._offset = tpCameraOffset;
                    tpCameraData._defualtPitch = tpCameraPitch;
                }
                else
                {
                    Log.Warning("tpCameraData component is not initialized.");
                }

                if (tpVirtualCam != null)
                {
                    if (tpFovValue == 0f)
                        tpFovValue = 10f;
                    LensSettings tpLensSettings = tpVirtualCam.m_Lens;
                    tpLensSettings.FieldOfView = tpFovValue;
                    tpVirtualCam.m_Lens = tpLensSettings;
                }
                else
                {
                    Log.Warning("tpVirtualCam component is not initialized.");
                }
                
                if (virtualCam != null)
                {
                    LensSettings lensSettings = virtualCam.m_Lens;
                    lensSettings.FieldOfView = fovValue;
                    virtualCam.m_Lens = lensSettings;
                    
                            
                    if (bFpvCamera)
                    {
                        gameplayCameraBrain.enabled = false;
                            
                        Transform headGearTransform = Memory.physicsDrivenCharacter?.transform.FindDeepChild("HeadGear");
                        if (headGearTransform != null)
                        {
                            gameplayCameraBrain.transform.SetParent(headGearTransform, false);
                            gameplayCameraBrain.transform.localPosition = fpvOffset;
                            gameplayCameraBrain.transform.eulerAngles = new Vector3(fpvRotation.x,physicsDrivenCharacter.transform.rotation.y,fpvRotation.z);
                        }
                        else
                        {
                            Log.Error("HeadGear transform not found on the player character!");
                        }
                    }
                    else
                    {
                        gameplayCameraBrain.enabled = true;
                                
                    }
            
                }
            
                if (camTranspose != null)
                {
                    camTranspose.m_FollowOffset = camOffset;
                    //Log.Msg($"Cam Offset: {camOffset}");
                }
                
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
            
        }

    }
}
