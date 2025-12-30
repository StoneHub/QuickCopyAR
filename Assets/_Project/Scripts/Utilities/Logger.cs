using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace QuickCopyAR.Utilities
{
    /// <summary>
    /// Structured logging utility for debugging and troubleshooting.
    /// Supports console output and persistent file logging.
    /// </summary>
    public static class Logger
    {
        public enum LogLevel
        {
            Verbose = 0,
            Debug = 1,
            Info = 2,
            Warning = 3,
            Error = 4
        }

        private static LogLevel currentLogLevel = LogLevel.Debug;
        private static bool logToFile = false;
        private static bool logToConsole = true;
        private static string logFilePath;
        private static StringBuilder logBuffer = new StringBuilder();
        private static int bufferFlushThreshold = 10;
        private static int bufferedLogCount = 0;
        private static object lockObject = new object();

        private static List<LogEntry> recentLogs = new List<LogEntry>();
        private static int maxRecentLogs = 100;

        private struct LogEntry
        {
            public DateTime Timestamp;
            public LogLevel Level;
            public string Category;
            public string Message;
        }

        /// <summary>
        /// Initialize the logger with file logging.
        /// </summary>
        public static void Initialize(bool enableFileLogging = false, LogLevel level = LogLevel.Debug)
        {
            currentLogLevel = level;
            logToFile = enableFileLogging;

            if (logToFile)
            {
                string fileName = $"quickcopy_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                logFilePath = Path.Combine(Application.persistentDataPath, fileName);

                try
                {
                    // Write header
                    File.WriteAllText(logFilePath,
                        $"QuickCopy AR Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Device: {SystemInfo.deviceModel}\n" +
                        $"OS: {SystemInfo.operatingSystem}\n" +
                        $"Unity: {Application.unityVersion}\n" +
                        "========================================\n\n");

                    Log("Logger", $"File logging initialized: {logFilePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Logger] Failed to initialize file logging: {e.Message}");
                    logToFile = false;
                }
            }
        }

        /// <summary>
        /// Log an info message.
        /// </summary>
        public static void Log(string category, string message)
        {
            LogMessage(LogLevel.Info, category, message);
        }

        /// <summary>
        /// Log a debug message.
        /// </summary>
        public static void LogDebug(string category, string message)
        {
            LogMessage(LogLevel.Debug, category, message);
        }

        /// <summary>
        /// Log a verbose message.
        /// </summary>
        public static void LogVerbose(string category, string message)
        {
            LogMessage(LogLevel.Verbose, category, message);
        }

        /// <summary>
        /// Log a warning message.
        /// </summary>
        public static void LogWarning(string category, string message)
        {
            LogMessage(LogLevel.Warning, category, message);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        public static void LogError(string category, string message)
        {
            LogMessage(LogLevel.Error, category, message);
        }

        /// <summary>
        /// Log an exception with stack trace.
        /// </summary>
        public static void LogException(string category, Exception e)
        {
            LogMessage(LogLevel.Error, category, $"{e.Message}\n{e.StackTrace}");
        }

        private static void LogMessage(LogLevel level, string category, string message)
        {
            if (level < currentLogLevel) return;

            lock (lockObject)
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Category = category,
                    Message = message
                };

                // Add to recent logs
                recentLogs.Add(entry);
                if (recentLogs.Count > maxRecentLogs)
                {
                    recentLogs.RemoveAt(0);
                }

                // Format log line
                string timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");
                string levelStr = GetLevelString(level);
                string formattedMessage = $"[{timestamp}] [{levelStr}] [{category}] {message}";

                // Console output
                if (logToConsole)
                {
                    switch (level)
                    {
                        case LogLevel.Warning:
                            Debug.LogWarning(formattedMessage);
                            break;
                        case LogLevel.Error:
                            Debug.LogError(formattedMessage);
                            break;
                        default:
                            Debug.Log(formattedMessage);
                            break;
                    }
                }

                // File output
                if (logToFile)
                {
                    logBuffer.AppendLine(formattedMessage);
                    bufferedLogCount++;

                    if (bufferedLogCount >= bufferFlushThreshold || level >= LogLevel.Error)
                    {
                        FlushBuffer();
                    }
                }
            }
        }

        private static string GetLevelString(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Verbose: return "VERB";
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Info: return "INFO";
                case LogLevel.Warning: return "WARN";
                case LogLevel.Error: return "ERROR";
                default: return "???";
            }
        }

        private static void FlushBuffer()
        {
            if (logBuffer.Length == 0 || string.IsNullOrEmpty(logFilePath)) return;

            try
            {
                File.AppendAllText(logFilePath, logBuffer.ToString());
                logBuffer.Clear();
                bufferedLogCount = 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Logger] Failed to write to log file: {e.Message}");
            }
        }

        /// <summary>
        /// Log timing information for performance tracking.
        /// </summary>
        public static void LogTiming(string category, string operation, float milliseconds)
        {
            LogMessage(LogLevel.Info, category, $"{operation}: {milliseconds:F1}ms");
        }

        /// <summary>
        /// Start a timing block. Returns a disposable that logs duration on dispose.
        /// </summary>
        public static TimingBlock StartTiming(string category, string operation)
        {
            return new TimingBlock(category, operation);
        }

        /// <summary>
        /// Get recent log entries.
        /// </summary>
        public static List<string> GetRecentLogs(int count = 50, LogLevel minLevel = LogLevel.Debug)
        {
            lock (lockObject)
            {
                var result = new List<string>();
                int start = Mathf.Max(0, recentLogs.Count - count);

                for (int i = start; i < recentLogs.Count; i++)
                {
                    var entry = recentLogs[i];
                    if (entry.Level >= minLevel)
                    {
                        result.Add($"[{entry.Timestamp:HH:mm:ss}] [{entry.Category}] {entry.Message}");
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Set the minimum log level.
        /// </summary>
        public static void SetLogLevel(LogLevel level)
        {
            currentLogLevel = level;
            Log("Logger", $"Log level set to: {level}");
        }

        /// <summary>
        /// Enable or disable console logging.
        /// </summary>
        public static void SetConsoleLogging(bool enabled)
        {
            logToConsole = enabled;
        }

        /// <summary>
        /// Force flush any buffered logs to file.
        /// </summary>
        public static void Flush()
        {
            lock (lockObject)
            {
                FlushBuffer();
            }
        }

        /// <summary>
        /// Get the log file path.
        /// </summary>
        public static string GetLogFilePath()
        {
            return logFilePath;
        }

        /// <summary>
        /// Clear all recent logs.
        /// </summary>
        public static void ClearRecentLogs()
        {
            lock (lockObject)
            {
                recentLogs.Clear();
            }
        }

        /// <summary>
        /// Helper class for timing code blocks.
        /// </summary>
        public class TimingBlock : IDisposable
        {
            private string category;
            private string operation;
            private DateTime startTime;
            private bool disposed = false;

            public TimingBlock(string category, string operation)
            {
                this.category = category;
                this.operation = operation;
                this.startTime = DateTime.Now;
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                    float duration = (float)(DateTime.Now - startTime).TotalMilliseconds;
                    LogTiming(category, operation, duration);
                }
            }
        }
    }
}
