using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class CreatePolylineTests
    {
        [Test]
        internal static void CheckCreationAndMetadata()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.Args("{'name':'Created','points':[{'x':1,'y':2},{'x':4,'y':6}],'closed':true}");
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 3;
            var result = DwgToolsFixture.Result(f.Tools.CreatePolyline(args));
            var entity = f.Created<DwgPolyline>(result);
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 3, "Explicit layer and color must be applied.");
            Test.Require(entity.Count == 2 && entity.Closed, "Polyline vertices and closure must be saved.");
            Test.Require((double)result["points"][1]["x"] == 4, "Response must contain vertices.");
            Test.Require(f.DrawingCalls == 1, "Creation must resolve drawing through DI.");
        }

        [Test]
        internal static void CheckInvalidArguments()
        {
            var f = new DwgToolsFixture();
            Test.Throws<BadRequestException>(() => f.Tools.CreatePolyline(DwgToolsFixture.Args()));
            Test.Throws<BadRequestException>(() => f.Tools.CreatePolyline(DwgToolsFixture.Args("{'name':'Created','points':[{'x':1}],'closed':false}")));
            Test.Require(f.Drawing.ActiveSpace.Entities.Count == 0, "Invalid geometry must not add an entity.");
            f.CheckDrawingError(f.Tools.CreatePolyline);
        }
    }
}

