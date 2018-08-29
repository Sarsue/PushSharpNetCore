using System;
namespace PushSharp.Core
{
    public enum LogLevel
    {
        None,
        Warning,
        Error,
        Info,
        Debug,
    }
    public class Log
    {
        public static LogLevel Level;

        static Log()
        {
            Log.Logger = (ILogger)null;
        }

        public static ILogger Logger { get; set; }

        public static void Debug(string format, params object[] objs)
        {
            if (Log.Level < LogLevel.Debug)
                return;
            if (Log.Logger == null)
                Console.WriteLine("DEBUG [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + format, objs);
            else
                Log.Logger.Debug(format, objs);
        }

        public static void Info(string format, params object[] objs)
        {
            if (Log.Level < LogLevel.Info)
                return;
            if (Log.Logger == null)
                Console.WriteLine("INFO [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + format, objs);
            else
                Log.Logger.Info(format, objs);
        }

        public static void Warning(string format, params object[] objs)
        {
            if (Log.Level < LogLevel.Warning)
                return;
            if (Log.Logger == null)
                Console.WriteLine("WARN [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + format, objs);
            else
                Log.Logger.Warning(format, objs);
        }

        public static void Error(string format, params object[] objs)
        {
            if (Log.Level < LogLevel.Error)
                return;
            if (Log.Logger == null)
                Console.WriteLine("ERR  [" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + format, objs);
            else
                Log.Logger.Error(format, objs);
        }
    }
}