using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed partial class ApiChecks
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckToolDefAttributeApi()
        {
            // Fixed contract from the 0.1.0 reference assembly; additions are allowed.
            ApiContract.Verify("Topomatic.ToolBridge.ToolDefAttribute", "System.Attribute",
                TypeAttributes.Public | TypeAttributes.Sealed,
                new[]
                {
                    "public instance get_Name() -> String",
                    "public instance set_Name(String) -> Void",
                    "public instance get_Description() -> String",
                    "public instance set_Description(String) -> Void",
                    "public instance get_InputSchema() -> String",
                    "public instance set_InputSchema(String) -> Void",
                    "public instance get_ReadOnlyHint() -> Boolean",
                    "public instance set_ReadOnlyHint(Boolean) -> Void",
                    "public instance get_DestructiveHint() -> Boolean",
                    "public instance set_DestructiveHint(Boolean) -> Void",
                    "public instance get_IdempotentHint() -> Boolean",
                    "public instance set_IdempotentHint(Boolean) -> Void",
                    "public instance .ctor() -> Void",
                }, new[]
                {
                    "instance Name() -> String",
                    "instance Description() -> String",
                    "instance InputSchema() -> String",
                    "instance ReadOnlyHint() -> Boolean",
                    "instance DestructiveHint() -> Boolean",
                    "instance IdempotentHint() -> Boolean",
                });
        }
    }
}
