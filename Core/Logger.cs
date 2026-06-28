using System.IO;

namespace TypeMate
{
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TypeMate",
            "typemate.log");

        private static readonly object LogLock = new object();

        static Logger()
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }
            }
            catch
            {
                // If we can't create the log directory, continue without logging
            }
        }

        public static void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public static void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public static void LogError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message}: {exception}" : message;
            WriteLog("ERROR", fullMessage);
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                lock (LogLock)
                {
                    var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                    
                    // Keep log file size reasonable (max 1MB)
                    var fileInfo = new FileInfo(LogFilePath);
                    if (fileInfo.Exists && fileInfo.Length > 1024 * 1024)
                    {
                        TruncateLogFile();
                    }
                }
            }
            catch
            {
                // If logging fails, continue silently
            }
        }

        private static void TruncateLogFile()
        {
            try
            {
                var lines = File.ReadAllLines(LogFilePath);
                var keepLines = lines.TakeLast(1000).ToArray(); // Keep last 1000 lines
                File.WriteAllLines(LogFilePath, keepLines);
            }
            catch
            {
                // If truncation fails, try to delete the file
                try
                {
                    File.Delete(LogFilePath);
                }
                catch
                {
                    // Continue silently if we can't manage the log file
                }
            }
        }
    }
}
