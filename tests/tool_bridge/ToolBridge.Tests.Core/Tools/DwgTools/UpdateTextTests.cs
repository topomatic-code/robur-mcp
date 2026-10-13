using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class UpdateTextTests
    {
        [Test]
        internal static void CheckUpdateAndPartialUpdate()
        {
            var f = new DwgToolsFixture();
            var entity = new DwgText { Content = "Original", Height = 2, Position = new Vector3D(1, 2, 0) };

            var guid = f.Add(entity);
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.EntityArgs(guid, "{'text':'Changed','position':{'x':8,'y':9},'height':4,'rotation':0.5}");
            args["name"] = "Renamed";
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 4;
            var result = DwgToolsFixture.Result(f.Tools.UpdateText(args));
            Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Renamed", "Identity and updated name must be returned.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Layer and color must update.");
            Test.Require(entity.GetExtensionDictionary().GetString("name", null) == "Renamed", "Persistent name must update.");
            Test.Require(entity.Content == "Changed" && entity.Position.X == 8 && entity.Height == 4 && entity.Rotation == 0.5, "Text properties must update.");
            f.Tools.UpdateText(DwgToolsFixture.EntityArgs(guid, "{'text':'Partial'}"));
            Test.Require(entity.Content == "Partial" && entity.Height == 4 && entity.Position.X == 8, "Text-only update must preserve placement.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Omitted appearance must remain unchanged.");
            Test.Require(ReferenceEquals(f.Storage.GetObject(guid), entity) && f.Drawing.ActiveSpace.Entities.Count == 1, "Update must preserve identity without creating another entity.");
            Test.Require(f.Logger.Messages.Count == 2, "Both updates must create log entries.");
        }

        [Test]
        internal static void CheckLookupAndValidation()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.UpdateText);
            var entity = new DwgText { Content = "Original", Height = 2, Position = new Vector3D(1, 2, 0) };

            var guid = f.Add(entity);
            Test.Throws<BadRequestException>(() => f.Tools.UpdateText(DwgToolsFixture.EntityArgs(guid, "{'height':0}")));
            var wrongType = f.Add(new DwgPoint());
            Test.Throws<PreconditionFailedException>(() => f.Tools.UpdateText(DwgToolsFixture.EntityArgs(wrongType)));
            f.CheckDrawingError(f.Tools.UpdateText);
        }
    }
}

