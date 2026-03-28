using UnityEngine;
using Object = UnityEngine.Object;

namespace rowemod.Challenges
{
    public static class ChallengeAreaManager
    {
        private static ChallengeArea _active;
        public static ChallengeArea Active => _active;

        // Create a new area at position/rotation with given size
        public static ChallengeArea Create(Vector3 position, Vector3 size, Quaternion? rotation = null, string playerTag = "Player")
        {
            if (_active != null)
            {
                Object.Destroy(_active.gameObject);
                _active = null;
            }

            _active = ChallengeArea.CreateChallengeArea(position, size, rotation ?? Quaternion.identity, playerTag);
            return _active;
        }

        public static void DestroyActive()
        {
            if (_active != null)
            {
                Object.Destroy(_active.gameObject);
                _active = null;
            }
        }

        public static void SetVisible(bool visible)
        {
            if (_active != null) _active.SetVisible(visible);
        }

        public static void SetPosition(Vector3 pos)
        {
            if (_active != null) _active.transform.position = pos;
        }

        public static void SetRotation(Quaternion rot)
        {
            if (_active != null) _active.transform.rotation = rot;
        }

        public static void SetSize(Vector3 worldSize)
        {
            if (_active != null) _active.transform.localScale = worldSize;
        }

        public static void SetColor(Color color)
        {
            if (_active != null) _active.SetColor(color);
        }

        // Callbacks into your challenge/points logic
        public static void NotifyAreaEnter(ChallengeArea area, Collider who)
        {
            // Example: mark “in zone” so your points watcher knows attempts count
            InZone = true;
            
        }

        public static void NotifyAreaExit(ChallengeArea area, Collider who)
        {
            InZone = false;
        }

        public static bool InZone { get; private set; }
    }
}
