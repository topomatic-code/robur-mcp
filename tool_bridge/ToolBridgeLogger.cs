using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Topomatic.Cad.View;

namespace Topomatic.ToolBridge
{
    /// <summary>
    /// Направляет публичные сообщения в консоль Robur, а системные — в файловый журнал.
    /// </summary>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class ToolBridgeLogger
    {
        private const string LogSource = "Robur tool bridge";
        private const string SystemLogFileNamePrefix = "tool_bridge";

        private static readonly Lazy<ToolBridgeLogger> m_Instance = new Lazy<ToolBridgeLogger>(() => new ToolBridgeLogger());
        private static readonly Encoding m_FileEncoding = new UTF8Encoding(false);

        private readonly object m_SyncRoot = new object();

        private string m_SystemLogDirectoryPath;
        private string m_SystemLogFilePath;

        public static ToolBridgeLogger Instance => m_Instance.Value;

        internal bool SystemLoggingEnabled
        {
            get
            {
                lock (m_SyncRoot)
                {
                    return m_SystemLogDirectoryPath != null;
                }
            }
        }

        private ToolBridgeLogger()
        {
        }

        /// <summary>
        /// Включает системный журнал. Файл будет создан при первой системной записи.
        /// </summary>
        public void EnableSystemLogging(string systemLogDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(systemLogDirectoryPath))
                throw new ArgumentException("System log directory path is required.", nameof(systemLogDirectoryPath));

            lock (m_SyncRoot)
            {
                if (string.Equals(m_SystemLogDirectoryPath, systemLogDirectoryPath, StringComparison.OrdinalIgnoreCase))
                    return;
                m_SystemLogDirectoryPath = systemLogDirectoryPath;
                m_SystemLogFilePath = null;
            }
        }

        public string CreateLogString(string message) => $"[{DateTime.Now:HH:mm:ss}] [{LogSource}]: {message}";

        public void PublicInfo(string message) => WritePublic(LogLevel.Info, message);
        public void PublicWarning(string message) => WritePublic(LogLevel.Warning, message);
        public void PublicError(string message) => WritePublic(LogLevel.Error, message);
        public void SystemInfo(string message) => WriteSystem(LogLevel.Info, message, null);
        public void SystemWarning(string message, Exception exception = null) => WriteSystem(LogLevel.Warning, message, exception);
        public void SystemError(string message, Exception exception = null) => WriteSystem(LogLevel.Error, message, exception);

        private void WritePublic(LogLevel level, string message)
        {
            var levelPrefix = level == LogLevel.Info ? string.Empty : $"[{GetLevelName(level)}] ";
            WriteConsole(levelPrefix + message);
        }

        private void WriteSystem(LogLevel level, string message, Exception exception)
        {
            Exception writeException = null;
            lock (m_SyncRoot)
            {
                if (m_SystemLogDirectoryPath == null)
                    return;
                try
                {
                    var entry = CreateSystemLogEntry(level, message, exception);
                    if (m_SystemLogFilePath == null)
                    {
                        Directory.CreateDirectory(m_SystemLogDirectoryPath);
                        m_SystemLogFilePath = CreateSystemLogFile(m_SystemLogDirectoryPath, entry);
                    }
                    else
                    {
                        File.AppendAllText(m_SystemLogFilePath, entry, m_FileEncoding);
                    }
                }
                catch (Exception ex)
                {
                    writeException = ex;
                    m_SystemLogDirectoryPath = null;
                    m_SystemLogFilePath = null;
                }
            }
            if (writeException != null)
                ReportSystemLogFailure(writeException);
        }

        private static string CreateSystemLogFile(string directoryPath, string firstEntry)
        {
            var createdAt = DateTime.Now;
            var processId = GetCurrentProcessId();
            var fileNamePrefix = $"{SystemLogFileNamePrefix}_{createdAt:yyyyMMdd_HHmmss_fff}_{processId}";
            for (var suffix = 0; ; suffix++)
            {
                var fileName = suffix == 0 ? fileNamePrefix + ".log" : $"{fileNamePrefix}_{suffix}.log";
                var filePath = Path.Combine(directoryPath, fileName);
                FileStream stream;
                try
                {
                    stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
                catch (IOException) when (File.Exists(filePath))
                {
                    // Подбираем свободное имя, если файл уже создан другим экземпляром.
                    continue;
                }
                using (stream)
                using (var writer = new StreamWriter(stream, m_FileEncoding))
                {
                    writer.Write(firstEntry);
                }
                return filePath;
            }
        }

        private static int GetCurrentProcessId()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.Id;
            }
        }

        private static string CreateSystemLogEntry(LogLevel level, string message, Exception exception)
        {
            var builder = new StringBuilder();
            builder
                .Append('[')
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append("] [SYSTEM] [")
                .Append(GetLevelName(level))
                .Append("] [")
                .Append(LogSource)
                .Append("]: ")
                .AppendLine(message ?? string.Empty);

            if (exception != null)
                builder.AppendLine(exception.ToString());
            return builder.ToString();
        }

        private void ReportSystemLogFailure(Exception exception) =>
            WriteConsole("[ERROR] Failed to write the Tool Bridge system log: " + exception.Message);

        private void WriteConsole(string message)
        {
            lock (m_SyncRoot)
            {
                try
                {
                    ConsoleListner.Current.WriteLine(CreateLogString(message));
                }
                catch
                {
                    // Ошибка вывода журнала не должна нарушать работу Tool Bridge.
                }
            }
        }

        private static string GetLevelName(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Info:
                    return "INFO";
                case LogLevel.Warning:
                    return "WARNING";
                case LogLevel.Error:
                    return "ERROR";
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        private enum LogLevel
        {
            Info,
            Warning,
            Error
        }
    }
}
