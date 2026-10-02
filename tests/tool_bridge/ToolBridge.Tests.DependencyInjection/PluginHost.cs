using System;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests.DependencyInjection
{
    internal sealed class PluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            return new Type[] { typeof(Module) };
        }
    }
}
