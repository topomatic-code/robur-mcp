using System;
using System.Collections.Generic;
using System.Reflection;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge_v0_1_0_ApiChecker
{
    internal sealed class Module : PluginInitializator
    {
        [cmd("tool_bridge_v0.1.0_api_check")]
        private object CheckApi()
        {
            var results = new List<string>();
            Run(results, "ToolProvider", nameof(ApiChecks.CheckToolProvider));
            Run(results, "ToolDefAttribute", nameof(ApiChecks.CheckToolDefAttribute));
            Run(results, "ObjectStorage", nameof(ApiChecks.CheckObjectStorage));
            Run(results, "ToolBridgeLogger", nameof(ApiChecks.CheckLogger));
            Run(results, "JsonUtils", nameof(ApiChecks.CheckJson));
            Run(results, "DwgUtils", nameof(ApiChecks.CheckDwg));
            Run(results, "SmdxUtils", nameof(ApiChecks.CheckSmdx));
            Run(results, "Integration.ToolProviderContext", nameof(ApiChecks.CheckToolProviderContextIntegration));
            Run(results, "Integration.DwgStorage", nameof(ApiChecks.CheckDwgStorageIntegration));
            Run(results, "Integration.LegacyProviderNoDomain", nameof(ApiChecks.CheckLegacyProviderNoDomain), () => new object[] { CadView });
            return results.ToArray();
        }

        [cmd("tool_bridge_v0.1.0_register_probe")]
        private void RegisterProbe(object[] args)
        {
            if (LegacyProviderProbe.RegistrationEnabled)
                LegacyProviderProbe.Register(args);
        }

        private static void Run(List<string> results, string group, string methodName, Func<object[]> arguments = null)
        {
            // Each group is JIT-compiled inside this boundary. Missing old types/methods
            // must fail one group rather than prevent the command from returning a report.
            try
            {
                typeof(ApiChecks).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, arguments?.Invoke());
                results.Add("success: [" + group + "] Все проверки выполнены.");
            }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException && ex.InnerException != null)
                    ex = ex.InnerException;
                results.Add("fail: [" + group + "] " + ex);
            }
        }
    }
}
