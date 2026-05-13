using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BaseUtils.Logger.Impl
{
    public class ConsoleLogger : ILogLogger
    {
        private static readonly FileLogger fileLogger = new FileLogger();
        public void D(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            I(message, callerMemberName, callerLineNumber, callerFilePath); // Log to console and file
        }

        public void E(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            I(message, callerMemberName, callerLineNumber, callerFilePath); // Log to console and file
        }

        public void I(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss}] INFO: {message} (Caller: {callerMemberName}, Line: {callerLineNumber}, File: {callerFilePath})");
            fileLogger.I(message, callerMemberName, callerLineNumber, callerFilePath); // Log to file as well
        }
    }

    public class FileLogger : ILogLogger
    {
        private readonly string logFilePath = "application.log";
        private static readonly object fileLock = new object();
        public void D(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            throw new NotImplementedException();
        }

        public void E(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            throw new NotImplementedException();
        }

        public void I(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            lock (fileLock) // Ensure thread safety when writing to the file
            {
                try
                {
                    using (var writer = new StreamWriter(logFilePath, true))
                    {
                        writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] INFO: {message} (Caller: {callerMemberName}, Line: {callerLineNumber}, File: {callerFilePath})");
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions related to file writing, e.g., log to console or another file
                    Console.WriteLine($"Error Code : 47 | {ex.Message}");
                    Console.WriteLine($"Failed to write log: {ex.Message}");
                }
            }
        }
    }

    public static class DLog
    {
        private static readonly ILogLogger logger = new ConsoleLogger();
        public static void D(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            logger.D(message, callerMemberName, callerLineNumber, callerFilePath);
        }

        public static void E(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            logger.E(message, callerMemberName, callerLineNumber, callerFilePath);
        }

        public static void I(string message, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            logger.I(message, callerMemberName, callerLineNumber, callerFilePath);
        }
    }
}


