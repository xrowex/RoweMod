using UnityEngine.InputSystem;
using rowemod.Utils;

namespace rowemod.Mods
{
    internal static class ReplayInputBindingDisabler
    {
        public static int DisableGeneratedReplayInputBindings(object replayInput)
        {
            if (replayInput == null)
                return 0;

            int removedCount = 0;
            removedCount += DisableGeneratedReplayOpenAction(replayInput, "m_ReplaySystem_OpenInstantReplay");
            removedCount += DisableGeneratedReplayOpenAction(replayInput, "m_ReplaySystem_OpenClipEdit");
            removedCount += DisableGeneratedReplayOpenAction(replayInput, "m_ReplaySystem_OpenClipPlayback");
            removedCount += DisableGeneratedReplayActionMapActions(replayInput, "ReplaySystem");
            return removedCount;
        }

        private static int DisableGeneratedReplayOpenAction(object replayInput, string propertyName)
        {
            try
            {
                System.Type type = replayInput.GetType();
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

                object rawAction = type.GetProperty(propertyName, flags)?.GetValue(replayInput)
                                   ?? type.GetField(propertyName, flags)?.GetValue(replayInput);

                return DisableReplayOpenAction(rawAction);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[PieMenu] Failed to disable generated replay input '{propertyName}': {ex.Message}");
                return 0;
            }
        }

        private static int DisableGeneratedReplayActionMapActions(object replayInput, string actionMapMemberName)
        {
            try
            {
                System.Type type = replayInput.GetType();
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

                object actionMap = type.GetProperty(actionMapMemberName, flags)?.GetValue(replayInput)
                                   ?? type.GetField(actionMapMemberName, flags)?.GetValue(replayInput);

                if (actionMap == null)
                {
                    Log.Msg($"[PieMenuDiag] Generated replay action map '{actionMapMemberName}' was not found.");
                    return 0;
                }

                int removedCount = 0;
                removedCount += DisableActionFromActionMap(actionMap, "OpenInstantReplay");
                removedCount += DisableActionFromActionMap(actionMap, "OpenClipEdit");
                removedCount += DisableActionFromActionMap(actionMap, "OpenClipPlayback");
                return removedCount;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[PieMenu] Failed to disable generated replay action map '{actionMapMemberName}': {ex.Message}");
                return 0;
            }
        }

        private static int DisableActionFromActionMap(object actionMap, string actionMemberName)
        {
            try
            {
                System.Type type = actionMap.GetType();
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

                object rawAction = type.GetProperty(actionMemberName, flags)?.GetValue(actionMap)
                                   ?? type.GetField(actionMemberName, flags)?.GetValue(actionMap);

                int removedCount = DisableReplayOpenAction(rawAction);
                Log.Msg($"[PieMenuDiag] Generated replay action '{actionMemberName}' removal count={removedCount}.");
                return removedCount;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[PieMenu] Failed to disable generated replay action '{actionMemberName}': {ex.Message}");
                return 0;
            }
        }

        private static int DisableReplayOpenAction(object rawAction)
        {
            InputAction action = rawAction as InputAction;
            if (action == null)
                return 0;

            int suppressedCount = PieMenu.SuppressGeneratedReplayAction(action);
            Log.Msg($"[PieMenuDiag] Suppressed generated replay action '{action.name}'. suppressedBindings={suppressedCount} enabledNow={action.enabled}.");
            return suppressedCount;
        }
    }
}
