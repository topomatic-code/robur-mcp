using System.Collections.Generic;
using ToolBridge.Tests.Core.DependencyInjection;
using ToolBridge.Tests.Core.Tools.AlgTools;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests.Core
{
    internal sealed class Module : PluginInitializator
    {
        [cmd("tool_bridge_tests_core_register")]
        private void RegisterTests(object[] args)
        {
            ((List<object>)args[0]).Add(new ContainerTests());
            ((List<object>)args[0]).Add(new GetAlignmentTests());
            ((List<object>)args[0]).Add(new GetParametersTests());
            ((List<object>)args[0]).Add(new GetConstructionTests());
        }
    }
}
