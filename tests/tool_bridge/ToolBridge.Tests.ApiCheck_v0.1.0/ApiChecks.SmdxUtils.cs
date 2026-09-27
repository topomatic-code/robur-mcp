using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed partial class ApiChecks
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckSmdxUtilsApi()
        {
            // Fixed contract from the 0.1.0 reference assembly; additions are allowed.
            ApiContract.Verify(typeof(global::Topomatic.ToolBridge.SmdxUtils),
                "Topomatic.ToolBridge.SmdxUtils", "System.Object",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            new[]
            {
                "public static CreateImElementObj(Topomatic.Visualization.ImElement) -> Object",
                    "public static CreatePropsArray(Topomatic.Visualization.ImProperties) -> Object",
                }, new string[0]);
        }
    }
}
