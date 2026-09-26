using System;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests
{
    internal sealed class PluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            return new Type[] { typeof(TestModule) };
        }
    }
}
