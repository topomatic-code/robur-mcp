using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed partial class ApiChecks
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckJsonUtilsApi()
        {
            // Fixed contract from the 0.1.0 reference assembly; additions are allowed.
            ApiContract.Verify("Topomatic.ToolBridge.JsonUtils", "System.Object",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
                new[]
                {
                    "public static UnwrapJsonValue(Object) -> Object",
                    "public static GetObject(System.Collections.Generic.Dictionary`2<String,Object>,String,System.Collections.Generic.Dictionary`2<String,Object>) -> System.Collections.Generic.Dictionary`2<String,Object>",
                    "public static RequireObject(System.Collections.Generic.Dictionary`2<String,Object>,String) -> System.Collections.Generic.Dictionary`2<String,Object>",
                    "public static GetString(System.Collections.Generic.Dictionary`2<String,Object>,String,String) -> String",
                    "public static RequireString(System.Collections.Generic.Dictionary`2<String,Object>,String) -> String",
                    "public static GetInt(System.Collections.Generic.Dictionary`2<String,Object>,String,System.Nullable`1<Int32>) -> System.Nullable`1<Int32>",
                    "public static RequireInt(System.Collections.Generic.Dictionary`2<String,Object>,String) -> Int32",
                    "public static GetDouble(System.Collections.Generic.Dictionary`2<String,Object>,String,System.Nullable`1<Double>) -> System.Nullable`1<Double>",
                    "public static RequireDouble(System.Collections.Generic.Dictionary`2<String,Object>,String) -> Double",
                    "public static GetVector3D(System.Collections.Generic.Dictionary`2<String,Object>,String,System.Nullable`1<Topomatic.Cad.Foundation.Vector3D>) -> System.Nullable`1<Topomatic.Cad.Foundation.Vector3D>",
                    "public static RequireVector3D(System.Collections.Generic.Dictionary`2<String,Object>,String) -> Topomatic.Cad.Foundation.Vector3D",
                    "public static GetBool(System.Collections.Generic.Dictionary`2<String,Object>,String,System.Nullable`1<Boolean>) -> System.Nullable`1<Boolean>",
                    "public static RequireBool(System.Collections.Generic.Dictionary`2<String,Object>,String) -> Boolean",
                    "public static GetArray(System.Collections.Generic.Dictionary`2<String,Object>,String,System.Collections.Generic.Dictionary`2<String,Object>[]) -> System.Collections.Generic.Dictionary`2<String,Object>[]",
                    "public static RequireArray(System.Collections.Generic.Dictionary`2<String,Object>,String) -> System.Collections.Generic.Dictionary`2<String,Object>[]",
                    "public static GetStringArray(System.Collections.Generic.Dictionary`2<String,Object>,String,String[]) -> String[]",
                    "public static RequireStringArray(System.Collections.Generic.Dictionary`2<String,Object>,String) -> String[]",
                    "public static GetIntArray(System.Collections.Generic.Dictionary`2<String,Object>,String,Int32[]) -> Int32[]",
                    "public static RequireIntArray(System.Collections.Generic.Dictionary`2<String,Object>,String) -> Int32[]",
                }, new string[0]);
        }
    }
}
