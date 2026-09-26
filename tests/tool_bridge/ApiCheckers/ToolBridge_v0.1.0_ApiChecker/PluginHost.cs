using System;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge_v0_1_0_ApiChecker
{
    internal sealed class PluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            return new Type[] { typeof(Module) };
        }
    }
}
