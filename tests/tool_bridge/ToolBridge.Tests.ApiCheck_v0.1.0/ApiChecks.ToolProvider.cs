using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed partial class ApiChecks
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckToolProviderApi()
        {
            // Fixed contract from the 0.1.0 reference assembly; additions are allowed.
            ApiContract.Verify("Topomatic.ToolBridge.ToolProvider", "System.Object",
                TypeAttributes.Public | TypeAttributes.Abstract,
                new[]
                {
                    "public instance get_AppHost() -> Topomatic.ApplicationPlatform.IApplicationHost",
                    "public instance set_AppHost(Topomatic.ApplicationPlatform.IApplicationHost) -> Void",
                    "public instance get_CadView() -> Topomatic.Cad.View.CadView",
                    "public instance set_CadView(Topomatic.Cad.View.CadView) -> Void",
                    "public instance get_SessionStorage() -> Topomatic.ToolBridge.Services.ObjectStorage",
                    "public instance set_SessionStorage(Topomatic.ToolBridge.Services.ObjectStorage) -> Void",
                    "public instance get_Logger() -> Topomatic.ToolBridge.ToolBridgeLogger",
                    "public instance set_Logger(Topomatic.ToolBridge.ToolBridgeLogger) -> Void",
                    "protected instance .ctor() -> Void",
                }, new[]
                {
                    "instance AppHost() -> Topomatic.ApplicationPlatform.IApplicationHost",
                    "instance CadView() -> Topomatic.Cad.View.CadView",
                    "instance SessionStorage() -> Topomatic.ToolBridge.Services.ObjectStorage",
                    "instance Logger() -> Topomatic.ToolBridge.ToolBridgeLogger",
                });
        }
    }
}
