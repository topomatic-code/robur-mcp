using System;
using System.Collections.Generic;
using Topomatic.FoundationClasses.Diagnostics;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Services.Implementation
{
    internal sealed class RoburLogger : IRoburLogger, ILoggerListener, IDisposable
    {
        private readonly Logger m_Logger;
        private readonly Dictionary<TaskIdentity, List<TaskRecord>> m_Records;

        public RoburLogger(Logger logger)
        {
            m_Logger = logger ??
                throw new ArgumentException("Logger is null", nameof(logger));

            m_Logger.Register(this);
            m_Records = new Dictionary<TaskIdentity, List<TaskRecord>>();
        }

        public void Clear(TaskIdentity identity) => m_Records.Remove(identity);

        public void Write(TaskRecord[] records)
        {
            foreach (var record in records)
            {
                var tid = record.Identity;

                if (!m_Records.TryGetValue(tid, out var recordList))
                {
                    recordList = new List<TaskRecord>();
                    m_Records.Add(tid, recordList);
                }

                recordList.Add(record);
            }
        }

        public List<LogEntry> GetLog()
        {
            var log = new List<LogEntry>();

            foreach (var kvp in m_Records)
            {
                var tid = kvp.Key;
                var recordList = kvp.Value;
                foreach (var record in recordList)
                {
                    log.Add(
                        new LogEntry()
                        {
                            Source = tid.DisplayName,
                            Level = record.Level,
                            Message = record.Message
                        }
                    );
                }
            }

            return log;
        }

        public void Dispose() => m_Logger.Unregister(this);
    }
}
