using System.Linq;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class CreateHatchTests
    {
        [Test]
        internal static void CheckCreationAndMetadata()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.Args("{'name':'Created','contours':[{'points':[{'x':0,'y':0},{'x':4,'y':0},{'x':4,'y':3}]}]}");
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 3;
            var result = DwgToolsFixture.Result(f.Tools.CreateHatch(args));
            var entity = f.Created<DwgHatch>(result);
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 3, "Explicit layer and color must be applied.");
            Test.Require(entity.IsSolid && entity.BoundaryPath.Count == 1, "Default hatch must be solid with one contour.");
            Test.Require(result["contours"][0]["points"].Count() == 4, "Open contour must be closed automatically.");
            Test.Require(f.DrawingCalls == 1, "Creation must resolve drawing through DI.");
        }

        [Test]
        internal static void CheckInvalidArguments()
        {
            var f = new DwgToolsFixture();
            Test.Throws<BadRequestException>(() => f.Tools.CreateHatch(DwgToolsFixture.Args()));
            Test.Throws<BadRequestException>(() => f.Tools.CreateHatch(DwgToolsFixture.Args("{'name':'Created','contours':[{'points':[{'x':0,'y':0},{'x':1,'y':1}]}]}")));
            Test.Require(f.Drawing.ActiveSpace.Entities.Count == 0, "Invalid geometry must not add an entity.");
            f.CheckDrawingError(f.Tools.CreateHatch);
        }
    }
}

