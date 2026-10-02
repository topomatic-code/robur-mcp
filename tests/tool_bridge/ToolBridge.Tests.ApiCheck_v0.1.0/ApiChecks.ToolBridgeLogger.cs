using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed partial class ApiChecks
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckToolBridgeLoggerApi()
        {
            // Fixed contract from the 0.1.0 reference assembly; additions are allowed.
            ApiContract.Verify("Topomatic.ToolBridge.ToolBridgeLogger", "System.Object",
                TypeAttributes.Public | TypeAttributes.Sealed,
                new[]
                {
                    "public static get_Instance() -> Topomatic.ToolBridge.ToolBridgeLogger",
                    "public instance CreateLogString(String) -> String",
                    "public instance Log(String) -> Void",
                }, new[]
                {
                    "static Instance() -> Topomatic.ToolBridge.ToolBridgeLogger",
                });
        }
    }
}
