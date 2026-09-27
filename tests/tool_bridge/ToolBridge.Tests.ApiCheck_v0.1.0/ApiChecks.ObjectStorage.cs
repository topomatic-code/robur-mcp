using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed partial class ApiChecks
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckObjectStorageApi()
        {
            // Fixed contract from the 0.1.0 reference assembly; additions are allowed.
            ApiContract.Verify(typeof(global::Topomatic.ToolBridge.Services.ObjectStorage),
                "Topomatic.ToolBridge.Services.ObjectStorage", "System.Object",
                TypeAttributes.Public | TypeAttributes.Sealed,
            new[]
            {
                "public instance .ctor() -> Void",
                    "public instance AddObject(Object) -> System.Guid",
                    "public instance AddObject(System.Guid,Object) -> Void",
                    "public instance GetObject(System.Guid) -> Object",
                    "public instance GetGuid(Object) -> System.Guid",
                    "public instance HasObject(System.Guid) -> Boolean",
                    "public instance HasObject(Object) -> Boolean",
                    "public instance RemoveObject(System.Guid) -> Boolean",
                    "public instance RemoveObject(Object) -> Boolean",
                    "public instance Clear() -> Void",
                }, new string[0]);
        }
    }
}
