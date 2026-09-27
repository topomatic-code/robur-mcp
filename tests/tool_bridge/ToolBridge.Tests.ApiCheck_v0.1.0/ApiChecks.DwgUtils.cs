using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed partial class ApiChecks
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckDwgUtilsApi()
        {
            // Fixed contract from the 0.1.0 reference assembly; additions are allowed.
            ApiContract.Verify(typeof(global::Topomatic.ToolBridge.DwgUtils),
                "Topomatic.ToolBridge.DwgUtils", "System.Object",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            new[]
            {
                "public static GetDrawing(Topomatic.Cad.View.CadView) -> Topomatic.Dwg.Drawing",
                    "public static FindEntity<!!0>(Topomatic.Dwg.Drawing,Topomatic.ToolBridge.Services.ObjectStorage,System.Guid) -> System.ValueTuple`2<!!0,String> where !!0:None[Topomatic.Dwg.Entities.DwgEntity]",
                    "public static GetEntityType(Topomatic.Dwg.Entities.DwgEntity) -> System.ValueTuple`2<String,String>",
                    "public static CreateEntityObj(Topomatic.Dwg.Entities.DwgEntity,String,String) -> Object",
                    "public static CreateEntityInfoObj(Topomatic.Dwg.Entities.DwgEntity,String,String) -> Object",
                    "public static CreatePolylineObj(Topomatic.Dwg.Entities.DwgPolyline,String,String) -> Object",
                    "public static CreateTableObj(Topomatic.Dwg.Entities.DwgTable,String,String) -> Object",
                    "public static CreateMTextObj(Topomatic.Dwg.Entities.DwgMText,String,String) -> Object",
                    "public static CreateTextObj(Topomatic.Dwg.Entities.DwgText,String,String) -> Object",
                    "public static CreateCircleObj(Topomatic.Dwg.Entities.DwgCircle,String,String) -> Object",
                    "public static CreateLineObj(Topomatic.Dwg.Entities.DwgLine,String,String) -> Object",
                    "public static CreateHatchObj(Topomatic.Dwg.Entities.DwgHatch,String,String) -> Object",
                    "public static CreateBlockInsertObj(Topomatic.Dwg.Entities.DwgInsert,String,String) -> Object",
                    "public static CreatePointPlantObj(Topomatic.Landscaping.DwgSmdxPointLandscaping,String,String,String) -> Object",
                    "public static CreateSolidObj(Topomatic.Visualization.Runtime.DwgModel3DElement,String,String) -> Object",
                    "public static CreateTlcObj(Topomatic.Visualization.Runtime.DwgModel3DElement,String,String) -> Object",
                    "public static CreateBounds3DObj(Topomatic.Cad.Foundation.BoundingBox3D) -> Object",
                    "public static ApplyEntityLayer(Topomatic.Dwg.Drawing,Topomatic.Dwg.Entities.DwgEntity,String) -> Void",
                    "public static GetColorMode(Topomatic.Cad.Foundation.CadColor) -> String",
                    "public static ApplyEntityColor(Topomatic.Dwg.Entities.DwgEntity,String,System.Nullable`1<Int32>) -> Void",
                    "public static ParsePatternType(String) -> Topomatic.Dwg.Entities.AcPatternType",
                    "public static ParseHatchStyle(String) -> Topomatic.Dwg.Entities.AcHatchStyle",
                    "public static ParseTextAlignment(String) -> Topomatic.Dwg.TextAlignment",
                    "public static ParseAttachmentPoint(String) -> Topomatic.Dwg.AttachmentPoint",
                }, new string[0]);
        }
    }
}
