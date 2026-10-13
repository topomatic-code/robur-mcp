using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class UpdateCircleTests
    {
        [Test]
        internal static void CheckUpdateAndPartialUpdate()
        {
            var f = new DwgToolsFixture();
            var entity = new DwgCircle { Center = new Vector3D(1, 2, 0), Radius = 2 };

            var guid = f.Add(entity);
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.EntityArgs(guid, "{'center':{'x':8,'y':9},'radius':4}");
            args["name"] = "Renamed";
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 4;
            var result = DwgToolsFixture.Result(f.Tools.UpdateCircle(args));
            Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Renamed", "Identity and updated name must be returned.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Layer and color must update.");
            Test.Require(entity.GetExtensionDictionary().GetString("name", null) == "Renamed", "Persistent name must update.");
            Test.Require(entity.Center.X == 8 && entity.Center.Y == 9 && entity.Radius == 4, "Circle geometry must update.");
            f.Tools.UpdateCircle(DwgToolsFixture.EntityArgs(guid, "{'radius':6}"));
            Test.Require(entity.Center.X == 8 && entity.Radius == 6, "Radius update must preserve center.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Omitted appearance must remain unchanged.");
            Test.Require(ReferenceEquals(f.Storage.GetObject(guid), entity) && f.Drawing.ActiveSpace.Entities.Count == 1, "Update must preserve identity without creating another entity.");
            Test.Require(f.Logger.Messages.Count == 2, "Both updates must create log entries.");
        }

        [Test]
        internal static void CheckLookupAndValidation()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.UpdateCircle);
            var entity = new DwgCircle { Center = new Vector3D(1, 2, 0), Radius = 2 };

            var guid = f.Add(entity);
            Test.Throws<BadRequestException>(() => f.Tools.UpdateCircle(DwgToolsFixture.EntityArgs(guid, "{'radius':0}")));
            var wrongType = f.Add(new DwgPoint());
            Test.Throws<PreconditionFailedException>(() => f.Tools.UpdateCircle(DwgToolsFixture.EntityArgs(wrongType)));
            f.CheckDrawingError(f.Tools.UpdateCircle);
        }
    }
}

