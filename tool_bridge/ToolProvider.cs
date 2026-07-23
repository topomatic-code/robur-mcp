using System;
using System.Collections.Generic;
using System.Reflection;
using Topomatic.ApplicationPlatform;
using Topomatic.Cad.View;
using Topomatic.ToolBridge.Services;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public abstract class ToolProvider
    {
        public IApplicationHost AppHost { get; set; }
        public CadView CadView { get; set; }
        public ObjectStorage SessionStorage { get; set; }
        public ToolBridgeLogger Logger { get; set; }

        internal List<Tool> GetTools()
        {
            var tools = new List<Tool>();
            var type = GetType();
            var methods = type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );
            foreach (var method in methods)
            {
                var toolDef = method.GetCustomAttribute<ToolDefAttribute>();
                var parameters = method.GetParameters();
                if (toolDef == null || parameters == null || parameters.Length != 1 || parameters[0].ParameterType != typeof(Dictionary<string, object>))
                    continue;
                tools.Add
                (
                    new Tool()
                    {
                        Provider = this,
                        Definition = toolDef.GetDefinition(),
                        Func = (Func<Dictionary<string, object>, object>)method.CreateDelegate(typeof(Func<Dictionary<string, object>, object>), this)
                    }
                );
            }
            return tools;
        }
    }
}
