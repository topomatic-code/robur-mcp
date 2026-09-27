using System;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed class PluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            return new Type[] { typeof(Module) };
        }
    }
}
