using System.Linq;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class CreateTableTests
    {
        [Test]
        internal static void CheckCreationAndMetadata()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.Args("{'name':'Created','position':{'x':3,'y':4},'rowCount':2,'columnCount':2,'cells':[{'row':0,'column':0,'rowSpan':1,'columnSpan':2,'text':'Header'}]}");
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 3;
            var result = DwgToolsFixture.Result(f.Tools.CreateTable(args));
            var entity = f.Created<DwgTable>(result);
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 3, "Explicit layer and color must be applied.");
            Test.Require(entity.RowsCount == 2 && entity.ColumnsCount == 2 && entity.Position.X == 3, "Table dimensions and position must match input.");
            Test.Require(result["cells"].Any(c => (string)c["text"] == "Header" && (int)c["columnSpan"] == 2), "Merged cell text and span must be returned.");
            Test.Require(f.DrawingCalls == 1, "Creation must resolve drawing through DI.");
        }

        [Test]
        internal static void CheckInvalidArguments()
        {
            var f = new DwgToolsFixture();
            Test.Throws<BadRequestException>(() => f.Tools.CreateTable(DwgToolsFixture.Args()));
            Test.Throws<BadRequestException>(() => f.Tools.CreateTable(DwgToolsFixture.Args("{'name':'Created','position':{'x':0,'y':0},'rowCount':2,'columnCount':2,'cells':[{'row':0,'column':0,'rowSpan':1,'columnSpan':3,'text':'Bad'}]}")));
            Test.Require(f.Drawing.ActiveSpace.Entities.Count == 0, "Invalid geometry must not add an entity.");
            f.CheckDrawingError(f.Tools.CreateTable);
        }
    }
}

