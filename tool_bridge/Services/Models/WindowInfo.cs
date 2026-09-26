using Topomatic.ApplicationPlatform;

namespace Topomatic.ToolBridge.Services.Models
{
    internal sealed class WindowInfo
    {
        public string Name { get; set; }
        public string UID { get; set; }
        public bool Dynamic { get; set; }
        public IDocumentWindow Window { get; set; }
        public bool HasCadView { get; set; }
        public bool HasDrawing { get; set; }
        public bool IsQuickDwg { get; set; }
    }
}
