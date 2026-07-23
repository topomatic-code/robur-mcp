using System;
using System.Reflection;
using Topomatic.ApplicationPlatform.Plugins;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    internal sealed class ToolBridgePluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            return new Type[] { typeof(ToolBridgeModule) };
        }
    }
}
