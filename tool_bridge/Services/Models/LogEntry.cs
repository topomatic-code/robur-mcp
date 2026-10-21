using Topomatic.FoundationClasses.Diagnostics;

namespace Topomatic.ToolBridge.Services.Models
{
    internal sealed class LogEntry
    {
        public string Source { get; set; }
        public TaskLevel Level { get; set; }
        public string Message { get; set; }

        public override string ToString() => $"[{Source}] [{Level}]: {Message}";
    }
}
