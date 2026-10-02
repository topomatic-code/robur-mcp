using System;

namespace Topomatic.ToolBridge.Infrastructure
{
    public interface IToolBridgeLogger
    {
        bool SystemLoggingEnabled { get; }

        void PublicInfo(string message);
        void PublicWarning(string message);
        void PublicError(string message);
        void SystemInfo(string message);
        void SystemWarning(string message, Exception exception = null);
        void SystemError(string message, Exception exception = null);
        void EnableSystemLogging(string systemLogDirectoryPath);
        string CreateLogString(string message);
    }
}
