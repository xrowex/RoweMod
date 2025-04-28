using MelonLoader;
using System.Reflection;

// CREDIT:
// This is a slightly modified version of Lineryders Logger class 
// https://github.com/thejeffreyallen/MapDLLInjector/blob/master/src/Logger.cs

namespace rowemod.Utils
{
    /// <summary>
    /// Provides a utility class for logging normal and error messages with color differentiation.
    /// </summary>
    /// <remarks>
    /// This class utilizes the MelonLogger framework to log messages. There are two loggers instantiated:
    /// one for normal messages, displayed in green, one for error messages, displayed in red, and one for warnings displayed in yellow.
    /// Each logger instance is associated with the name of the currently executing assembly.
    /// The class provides static methods to log standard messages and error messages.
    /// </remarks>
    public static class Log
    {
        static string _moduleName = Assembly.GetExecutingAssembly().GetName().Name;

        // toggle for console logs
        static bool _bDebug = true;

        static MelonLogger.Instance _loggerInstance = new MelonLogger.Instance(_moduleName);
        static MelonLogger.Instance _warningInstance = new MelonLogger.Instance(_moduleName);
        static MelonLogger.Instance _errorInstance = new MelonLogger.Instance(_moduleName);

        public static void Msg(object msg)
        {
            if (_bDebug) _loggerInstance.Msg(msg);
        }

        public static void Warning(object msg)
        {
            if (_bDebug) _warningInstance.Msg(msg);
        }

        public static void Error(object msg)
        {
            if (_bDebug) _errorInstance.Msg(msg);
        }


    }
}
