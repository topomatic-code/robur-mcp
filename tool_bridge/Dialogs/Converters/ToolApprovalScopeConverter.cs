using Topomatic.ComponentModel;
using Topomatic.ToolBridge.Settings;

namespace Topomatic.ToolBridge.Dialogs.Converters
{
    internal sealed class ToolApprovalScopeConverter : BaseEnumConverter
    {
        public ToolApprovalScopeConverter()
        {
            Dictionary[ToolApprovalScope.None] = "Запрашивать каждый раз";
            Dictionary[ToolApprovalScope.Session] = "Разрешить для текущей сессии";
            Dictionary[ToolApprovalScope.Permanently] = "Разрешить навсегда";
        }
    }
}
