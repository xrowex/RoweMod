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
        static string moduleName = Assembly.GetExecutingAssembly().GetName().Name;

        // toggle for console logs
        static bool bDebug = true;

        static MelonLogger.Instance loggerInstance = new MelonLogger.Instance(moduleName);
        static MelonLogger.Instance warningInstance = new MelonLogger.Instance(moduleName);
        static MelonLogger.Instance errorInstance = new MelonLogger.Instance(moduleName);

        public static void Msg(object msg)
        {
            if (bDebug) loggerInstance.Msg(msg);
        }

        public static void Warning(object msg)
        {
            if (bDebug) warningInstance.Msg(msg);
        }

        public static void Error(object msg)
        {
            if (bDebug) errorInstance.Msg(msg);
        }


    }
}
