using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class CreateLineTests
    {
        [Test]
        internal static void CheckCreationAndMetadata()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.Args("{'name':'Created','startPoint':{'x':1,'y':2},'endPoint':{'x':4,'y':6}}");
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 3;
            var result = DwgToolsFixture.Result(f.Tools.CreateLine(args));
            var entity = f.Created<DwgLine>(result);
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 3, "Explicit layer and color must be applied.");
            Test.Require(entity.StartPoint.X == 1 && entity.EndPoint.Y == 6 && entity.Length == 5, "Line endpoints and length must match input.");
            Test.Require(f.DrawingCalls == 1, "Creation must resolve drawing through DI.");
        }

        [Test]
        internal static void CheckInvalidArguments()
        {
            var f = new DwgToolsFixture();
            Test.Throws<BadRequestException>(() => f.Tools.CreateLine(DwgToolsFixture.Args()));
            Test.Throws<BadRequestException>(() => f.Tools.CreateLine(DwgToolsFixture.Args("{'name':'Created','startPoint':{'x':1},'endPoint':{'x':4,'y':6}}")));
            Test.Require(f.Drawing.ActiveSpace.Entities.Count == 0, "Invalid geometry must not add an entity.");
            f.CheckDrawingError(f.Tools.CreateLine);
        }
    }
}

