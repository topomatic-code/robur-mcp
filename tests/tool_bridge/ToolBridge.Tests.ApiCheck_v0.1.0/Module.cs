using System.Collections.Generic;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed class Module : PluginInitializator
    {
        [cmd("tool_bridge_tests_api_check_v0.1.0_register")]
        private void RegisterTests(object[] args)
        {
            ((List<object>)args[0]).Add(new ApiChecks());
            ((List<object>)args[0]).Add(new ProviderIntegrationChecks(() => CadView));
        }

        [cmd("tool_bridge_tests_api_check_v0.1.0_tools")]
        private void RegisterTools(object[] args)
        {
            LegacyToolProvider.Register(args);
        }
    }
}
