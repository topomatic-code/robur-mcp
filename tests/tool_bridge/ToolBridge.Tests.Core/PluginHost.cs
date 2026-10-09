using System;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests.Core
{
    internal sealed class PluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            return new Type[] { typeof(Module) };
        }
    }
}
