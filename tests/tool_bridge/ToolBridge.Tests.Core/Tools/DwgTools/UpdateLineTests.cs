using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class UpdateLineTests
    {
        [Test]
        internal static void CheckUpdateAndPartialUpdate()
        {
            var f = new DwgToolsFixture();
            var entity = DwgToolsFixture.Line();

            var guid = f.Add(entity);
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.EntityArgs(guid, "{'startPoint':{'x':8,'y':9},'endPoint':{'x':11,'y':13}}");
            args["name"] = "Renamed";
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 4;
            var result = DwgToolsFixture.Result(f.Tools.UpdateLine(args));
            Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Renamed", "Identity and updated name must be returned.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Layer and color must update.");
            Test.Require(entity.GetExtensionDictionary().GetString("name", null) == "Renamed", "Persistent name must update.");
            Test.Require(entity.StartPoint.X == 8 && entity.EndPoint.Y == 13 && entity.Length == 5, "Both endpoints must update.");
            f.Tools.UpdateLine(DwgToolsFixture.EntityArgs(guid, "{'endPoint':{'x':12,'y':14}}"));
            Test.Require(entity.StartPoint.X == 8 && entity.EndPoint.X == 12, "End-only update must preserve start.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Omitted appearance must remain unchanged.");
            Test.Require(ReferenceEquals(f.Storage.GetObject(guid), entity) && f.Drawing.ActiveSpace.Entities.Count == 1, "Update must preserve identity without creating another entity.");
            Test.Require(f.Logger.Messages.Count == 2, "Both updates must create log entries.");
        }

        [Test]
        internal static void CheckLookupAndValidation()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.UpdateLine);
            var entity = DwgToolsFixture.Line();

            var guid = f.Add(entity);
            Test.Throws<BadRequestException>(() => f.Tools.UpdateLine(DwgToolsFixture.EntityArgs(guid, "{'startPoint':{'x':1}}")));
            var wrongType = f.Add(new DwgPoint());
            Test.Throws<PreconditionFailedException>(() => f.Tools.UpdateLine(DwgToolsFixture.EntityArgs(wrongType)));
            f.CheckDrawingError(f.Tools.UpdateLine);
        }
    }
}

