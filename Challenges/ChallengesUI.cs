using UnityEngine;
using rowemod;
using rowemod.Utils;

namespace rowemod.Challenges
{
    public static class ChallengesUI
    {
        private static float _posX;
        private static float _posY;
        private static float _posZ;
        private static int _activeInstanceId;
        private static bool _positionInitialized;

        public static void DrawChallengeTab()
        {
            GUILayout.Box("Challenge Settings", Menu.coloredBoxStyle, GUILayout.Height(Menu.coloredBoxStyle.fixedHeight), GUILayout.ExpandWidth(true));

            if (Menu.ModernButton("Spawn Challenge Area", 250f))
            {
                Vector3 areaSize = new Vector3(
                    Config.challengeSettings.challengeSizeX,
                    Config.challengeSettings.challengeSizeY,
                    Config.challengeSettings.challengeSizeZ);
                Vector3 spawnPos = Vector3.zero;
                Quaternion spawnRot = Quaternion.identity;

                if (ChallengeAreaManager.TryGetLocalPlayerGroundPlacement(
                        areaSize,
                        out Vector3 playerPosition,
                        out Quaternion playerRotation,
                        out _))
                {
                    spawnPos = playerPosition;
                    spawnRot = playerRotation;
                }
                else if (Camera.main != null)
                {
                    Vector3 cameraTarget =
                        Camera.main.transform.position + Camera.main.transform.forward * 5f;
                    if (!ChallengeAreaManager.TryGetGroundAlignedPlacement(
                            cameraTarget,
                            Camera.main.transform.forward,
                            areaSize,
                            out spawnPos,
                            out spawnRot,
                            out _))
                    {
                        spawnPos = cameraTarget;
                    }
                }

                ChallengeAreaManager.Create(
                    spawnPos,
                    areaSize,
                    spawnRot
                );

                ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
                SyncPositionFromActive();
            }

            var active = ChallengeAreaManager.Active;
            if (active != null)
            {
                if (Menu.ModernButton("Destroy Challenge Area", 250f))
                {
                    ChallengeAreaManager.DestroyActive();
                    ClearPositionCache();
                    return;
                }

                if (Menu.ModernToggle("Visible", ref Config.challengeSettings.challengeVisible))
                {
                    ChallengeAreaManager.SetVisible(Config.challengeSettings.challengeVisible);
                }

                DrawSizeControls();
                DrawPositionControls(active);

                if (Menu.ModernButton("Teleport to Me", 200f))
                {
                    if (ChallengeAreaManager.TryGetLocalPlayerGroundPlacement(
                            active.transform.localScale,
                            out Vector3 playerPosition,
                            out Quaternion playerRotation,
                            out _))
                    {
                        ChallengeAreaManager.SetPosition(playerPosition);
                        ChallengeAreaManager.SetRotation(playerRotation);
                        SyncPositionFromActive();
                    }
                }
            }
            else
            {
                GUILayout.Label("No active challenge area.", Menu.labelStyle);
            }
        }

        private static void DrawSizeControls()
        {
            GUILayout.Label("Size", Menu.labelStyle);

            float oldX = Config.challengeSettings.challengeSizeX;
            float oldY = Config.challengeSettings.challengeSizeY;
            float oldZ = Config.challengeSettings.challengeSizeZ;

            Menu.ModernSlider("Width", ref Config.challengeSettings.challengeSizeX, 1f, 50f);
            Menu.ModernSlider("Height", ref Config.challengeSettings.challengeSizeY, 1f, 50f);
            Menu.ModernSlider("Depth", ref Config.challengeSettings.challengeSizeZ, 1f, 50f);

            if (oldX != Config.challengeSettings.challengeSizeX ||
                oldY != Config.challengeSettings.challengeSizeY ||
                oldZ != Config.challengeSettings.challengeSizeZ)
            {
                ChallengeAreaManager.SetSize(new Vector3(
                    Config.challengeSettings.challengeSizeX,
                    Config.challengeSettings.challengeSizeY,
                    Config.challengeSettings.challengeSizeZ));
            }
        }

        private static void DrawPositionControls(ChallengeArea active)
        {
            EnsurePositionSynced(active);

            GUILayout.Label("Position", Menu.labelStyle);

            float oldX = _posX;
            float oldY = _posY;
            float oldZ = _posZ;

            Menu.ModernSlider("Position X", ref _posX, -500f, 500f);
            Menu.ModernSlider("Position Y", ref _posY, -500f, 500f);
            Menu.ModernSlider("Position Z", ref _posZ, -500f, 500f);

            if (oldX != _posX || oldY != _posY || oldZ != _posZ)
            {
                ChallengeAreaManager.SetPosition(new Vector3(_posX, _posY, _posZ));
            }
        }

        private static void EnsurePositionSynced(ChallengeArea active)
        {
            if (active == null) return;

            int id = active.GetInstanceID();
            if (_positionInitialized && id == _activeInstanceId) return;

            var pos = active.transform.position;
            _posX = pos.x;
            _posY = pos.y;
            _posZ = pos.z;
            _activeInstanceId = id;
            _positionInitialized = true;
        }

        private static void SyncPositionFromActive()
        {
            var active = ChallengeAreaManager.Active;
            if (active == null) return;

            var pos = active.transform.position;
            _posX = pos.x;
            _posY = pos.y;
            _posZ = pos.z;
            _activeInstanceId = active.GetInstanceID();
            _positionInitialized = true;
        }

        private static void ClearPositionCache()
        {
            _positionInitialized = false;
            _activeInstanceId = 0;
        }
    }
}
