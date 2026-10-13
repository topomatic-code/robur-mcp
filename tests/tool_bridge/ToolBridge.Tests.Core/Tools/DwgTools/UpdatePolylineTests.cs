using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class UpdatePolylineTests
    {
        [Test]
        internal static void CheckUpdateAndPartialUpdate()
        {
            var f = new DwgToolsFixture();
            var entity = new DwgPolyline();
            entity.Add(new BugleVector2D(new Vector2D(1, 2))); entity.Add(new BugleVector2D(new Vector2D(4, 6)));
            var guid = f.Add(entity);
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.EntityArgs(guid, "{'points':[{'x':8,'y':9},{'x':10,'y':11},{'x':12,'y':13}],'closed':true}");
            args["name"] = "Renamed";
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 4;
            var result = DwgToolsFixture.Result(f.Tools.UpdatePolyline(args));
            Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Renamed", "Identity and updated name must be returned.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Layer and color must update.");
            Test.Require(entity.GetExtensionDictionary().GetString("name", null) == "Renamed", "Persistent name must update.");
            Test.Require(entity.Count == 3 && entity.Closed && entity[0].Vertex.X == 8, "Vertices must be replaced and closure updated.");
            f.Tools.UpdatePolyline(DwgToolsFixture.EntityArgs(guid, "{'closed':false}"));
            Test.Require(entity.Count == 3 && !entity.Closed, "Partial update must preserve vertices.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Omitted appearance must remain unchanged.");
            Test.Require(ReferenceEquals(f.Storage.GetObject(guid), entity) && f.Drawing.ActiveSpace.Entities.Count == 1, "Update must preserve identity without creating another entity.");
            Test.Require(f.Logger.Messages.Count == 2, "Both updates must create log entries.");
        }

        [Test]
        internal static void CheckLookupAndValidation()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.UpdatePolyline);
            var entity = new DwgPolyline();
            entity.Add(new BugleVector2D(new Vector2D(1, 2))); entity.Add(new BugleVector2D(new Vector2D(4, 6)));
            var guid = f.Add(entity);
            Test.Throws<BadRequestException>(() => f.Tools.UpdatePolyline(DwgToolsFixture.EntityArgs(guid, "{'points':[{'x':1}]}")));
            var wrongType = f.Add(new DwgPoint());
            Test.Throws<PreconditionFailedException>(() => f.Tools.UpdatePolyline(DwgToolsFixture.EntityArgs(wrongType)));
            f.CheckDrawingError(f.Tools.UpdatePolyline);
        }
    }
}

