using System.Collections.Generic;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests.DependencyInjection
{
    internal sealed class Module : PluginInitializator
    {
        [cmd("tool_bridge_tests_dependency_injection_register")]
        private void RegisterTests(object[] args)
        {
            ((List<object>)args[0]).Add(new ContainerTests());
        }
    }
}
