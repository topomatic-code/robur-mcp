using System.Linq;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class GetActiveDrawingInfoTests
    {
        [Test]
        internal static void CheckDrawingMetadata()
        {
            var f = new DwgToolsFixture();
            var layer = f.Drawing.Layers.Add("Visible");
            layer.Description = "Description";
            f.Drawing.Layers.ActivateLayer("Visible");
            f.Add(DwgToolsFixture.Line());
            var result = DwgToolsFixture.Result(f.Tools.GetActiveDrawingInfo(DwgToolsFixture.Args()));
            Test.Require((int)result["activeSpace"]["entitiesCount"] == 1, "Entity count must match drawing.");
            Test.Require((string)result["activeLayerName"] == "Visible", "Active layer must be returned.");
            Test.Require(result["layers"].Any(l => (string)l["name"] == "Visible" && (string)l["description"] == "Description"), "Layer metadata must be returned.");
            Test.Require((double)result["activeSpace"]["bounds"]["left"] == 1 && (double)result["activeSpace"]["bounds"]["right"] == 4, "Bounds must describe real geometry.");
            Test.Require(f.DrawingCalls == 1 && f.Logger.Messages.Count == 0, "Read must use provider without starting an edit.");
        }

        [Test]
        internal static void CheckEmptyDrawingAndProviderError()
        {
            var f = new DwgToolsFixture();
            var result = DwgToolsFixture.Result(f.Tools.GetActiveDrawingInfo(DwgToolsFixture.Args()));
            Test.Require((int)result["activeSpace"]["entitiesCount"] == 0, "Empty drawing must return zero entities.");
            f.CheckDrawingError(f.Tools.GetActiveDrawingInfo);
        }
    }
}

