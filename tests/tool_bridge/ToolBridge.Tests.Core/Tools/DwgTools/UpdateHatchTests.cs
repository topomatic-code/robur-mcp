using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class UpdateHatchTests
    {
        [Test]
        internal static void CheckUpdateAndPartialUpdate()
        {
            var f = new DwgToolsFixture();
            var entity = new DwgHatch();

            var guid = f.Add(entity);
            f.Drawing.Layers.Add("Target");
            var args = DwgToolsFixture.EntityArgs(guid, "{'contours':[{'points':[{'x':0,'y':0},{'x':4,'y':0},{'x':4,'y':3}]}],'patternScale':2,'patternAngle':0.5}");
            args["name"] = "Renamed";
            args["layerName"] = "Target";
            args["colorMode"] = "Indexed";
            args["colorIndex"] = 4;
            var result = DwgToolsFixture.Result(f.Tools.UpdateHatch(args));
            Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Renamed", "Identity and updated name must be returned.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Layer and color must update.");
            Test.Require(entity.GetExtensionDictionary().GetString("name", null) == "Renamed", "Persistent name must update.");
            Test.Require(entity.BoundaryPath.Count == 1 && entity.PatternScale == 2 && entity.PatternAngle == 0.5, "Hatch contour and pattern settings must update.");
            f.Tools.UpdateHatch(DwgToolsFixture.EntityArgs(guid, "{'patternScale':3}"));
            Test.Require(entity.BoundaryPath.Count == 1 && entity.PatternScale == 3 && entity.PatternAngle == 0.5, "Scale update must preserve contour and angle.");
            Test.Require(entity.Layer.Name == "Target" && entity.Color.ColorIndex == 4, "Omitted appearance must remain unchanged.");
            Test.Require(ReferenceEquals(f.Storage.GetObject(guid), entity) && f.Drawing.ActiveSpace.Entities.Count == 1, "Update must preserve identity without creating another entity.");
            Test.Require(f.Logger.Messages.Count == 2, "Both updates must create log entries.");
        }

        [Test]
        internal static void CheckLookupAndValidation()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.UpdateHatch);
            var entity = new DwgHatch();

            var guid = f.Add(entity);
            Test.Throws<BadRequestException>(() => f.Tools.UpdateHatch(DwgToolsFixture.EntityArgs(guid, "{'patternScale':0}")));
            var wrongType = f.Add(new DwgPoint());
            Test.Throws<PreconditionFailedException>(() => f.Tools.UpdateHatch(DwgToolsFixture.EntityArgs(wrongType)));
            f.CheckDrawingError(f.Tools.UpdateHatch);
        }
    }
}

