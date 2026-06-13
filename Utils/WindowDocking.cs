using System;
using System.Reflection;
using UnityEngine;

namespace rowemod.Utils
{
    public static class WindowDocking
    {
        private static bool _applied;
        private static float _nextAttemptTime;
        private static GameObject _contentWindow;
        private static Component _dockSource;
        private static bool _loggedNoDock;

        private static readonly string[] InspectTokens = { "Inspect", "Inspector" };
        private static readonly string[] ContentTokens =
        {
            "MG Content Manager", "Content Manager", "MGContent", "MG_Content", "ContentManager"
        };

        public static void Update()
        {
            // Re-arm if the content window was destroyed.
            if (_applied && !_contentWindow)
            {
                _applied = false;
                _contentWindow = null;
                _dockSource = null;
            }

            if (_applied) return;
            if (Time.unscaledTime < _nextAttemptTime) return;

            _nextAttemptTime = Time.unscaledTime + 2f;
            TryApplyDocking();
        }

        private static void TryApplyDocking()
        {
            var inspectWindow = FindWindowByName(InspectTokens);
            if (!inspectWindow) return;

            var contentWindow = FindWindowByName(ContentTokens);
            if (!contentWindow) return;

            var dockSource = FindDockComponent(inspectWindow);
            if (dockSource == null)
            {
                if (!_loggedNoDock)
                {
                    _loggedNoDock = true;
                    Log.Warning("[WindowDocking] Inspect window found but no Dock* component detected.");
                }
                return;
            }

            var dockType = dockSource.GetType();
            if (contentWindow.GetComponent(dockType) == null)
            {
                var newComp = contentWindow.AddComponent(dockType);
                CopySimpleFields(dockSource, newComp);
            }

            _applied = true;
            _contentWindow = contentWindow;
            _dockSource = dockSource;

            Log.Msg("[WindowDocking] MG Content Manager dockable behavior applied.");
        }

        private static GameObject FindWindowByName(string[] tokens)
        {
            GameObject fallback = null;
            var all = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var go in all)
            {
                if (!go) continue;
                var name = go.name;
                if (string.IsNullOrEmpty(name)) continue;

                if (!TokensMatch(name, tokens)) continue;

                // Prefer UI objects.
                if (go.GetComponent<RectTransform>() != null)
                    return go;

                if (fallback == null)
                    fallback = go;
            }

            return fallback;
        }

        private static bool TokensMatch(string name, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static Component FindDockComponent(GameObject inspectWindow)
        {
            var components = inspectWindow.GetComponents<Component>();
            foreach (var c in components)
            {
                if (!c) continue;
                var typeName = c.GetType().Name;
                if (typeName.IndexOf("Dock", StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
            return null;
        }

        private static void CopySimpleFields(Component source, Component destination)
        {
            if (source == null || destination == null) return;
            var type = source.GetType();
            if (type != destination.GetType()) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = type.GetFields(flags);

            foreach (var f in fields)
            {
                if (f.IsStatic) continue;

                var ft = f.FieldType;
                if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) ||
                    ft == typeof(Vector2) || ft == typeof(Vector3) || ft == typeof(Vector4) ||
                    ft == typeof(Color) || ft == typeof(Rect) || ft == typeof(Quaternion))
                {
                    try
                    {
                        f.SetValue(destination, f.GetValue(source));
                    }
                    catch
                    {
                        // Best-effort copy only; ignore fields that throw.
                    }
                }
            }
        }
    }
}
