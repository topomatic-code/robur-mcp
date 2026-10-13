using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class CreateMTextTests
    {
        [Test]
        internal static void CheckCreationAndMetadata()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.Args("{'name':'Created','text':'Label','position':{'x':3,'y':4},'height':2,'rotation':0.5}");
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 3;
            var result = DwgToolsFixture.Result(f.Tools.CreateMText(args));
            var entity = f.Created<DwgMText>(result);
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 3, "Explicit layer and color must be applied.");
            Test.Require(entity.Content == "Label" && entity.Height == 2 && entity.Position.Y == 4 && entity.Rotation == 0.5, "MText and placement must match input.");
            Test.Require(f.DrawingCalls == 1, "Creation must resolve drawing through DI.");
        }

        [Test]
        internal static void CheckInvalidArguments()
        {
            var f = new DwgToolsFixture();
            Test.Throws<BadRequestException>(() => f.Tools.CreateMText(DwgToolsFixture.Args()));
            Test.Throws<BadRequestException>(() => f.Tools.CreateMText(DwgToolsFixture.Args("{'name':'Created','text':'Label','position':{'x':3,'y':4},'height':-1}")));
            Test.Require(f.Drawing.ActiveSpace.Entities.Count == 0, "Invalid geometry must not add an entity.");
            f.CheckDrawingError(f.Tools.CreateMText);
        }
    }
}

