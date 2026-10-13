using System.Linq;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class UpdateTableTests
    {
        [Test]
        internal static void CheckUpdateAndPartialUpdate()
        {
            var f = new DwgToolsFixture();
            var entity = new DwgTable(f.Drawing.TableStyles.Standard, 2, 1, 2, 1);
            entity.Prepare(f.Drawing); entity.UnMergeAll(true);
            var guid = f.Add(entity);
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.EntityArgs(guid, "{'position':{'x':8,'y':9},'rowCount':3,'columnCount':3,'cells':[{'row':0,'column':0,'rowSpan':1,'columnSpan':2,'text':'Changed'}]}");
            args["name"] = "Renamed";
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 4;
            var result = DwgToolsFixture.Result(f.Tools.UpdateTable(args));
            Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Renamed", "Identity and updated name must be returned.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Layer and color must update.");
            Test.Require(entity.GetExtensionDictionary().GetString("name", null) == "Renamed", "Persistent name must update.");
            Test.Require(entity.RowsCount == 3 && entity.ColumnsCount == 3 && entity.Position.X == 8, "Table must grow and move.");
            Test.Require(result["cells"].Any(c => (string)c["text"] == "Changed" && (int)c["columnSpan"] == 2), "Merged cell must update.");
            f.Tools.UpdateTable(DwgToolsFixture.EntityArgs(guid, "{'rowCount':2,'columnCount':2}"));
            Test.Require(entity.RowsCount == 2 && entity.ColumnsCount == 2 && entity.Position.X == 8, "Table must shrink and preserve position.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Omitted appearance must remain unchanged.");
            Test.Require(ReferenceEquals(f.Storage.GetObject(guid), entity) && f.Drawing.ActiveSpace.Entities.Count == 1, "Update must preserve identity without creating another entity.");
            Test.Require(f.Logger.Messages.Count == 2, "Both updates must create log entries.");
        }

        [Test]
        internal static void CheckLookupAndValidation()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.UpdateTable);
            var entity = new DwgTable(f.Drawing.TableStyles.Standard, 2, 1, 2, 1);
            entity.Prepare(f.Drawing); entity.UnMergeAll(true);
            var guid = f.Add(entity);
            Test.Throws<BadRequestException>(() => f.Tools.UpdateTable(DwgToolsFixture.EntityArgs(guid, "{'rowCount':2,'columnCount':2,'cells':[{'row':0,'column':0,'rowSpan':3,'columnSpan':1,'text':'Bad'}]}")));
            var wrongType = f.Add(new DwgPoint());
            Test.Throws<PreconditionFailedException>(() => f.Tools.UpdateTable(DwgToolsFixture.EntityArgs(wrongType)));
            f.CheckDrawingError(f.Tools.UpdateTable);
        }
    }
}

