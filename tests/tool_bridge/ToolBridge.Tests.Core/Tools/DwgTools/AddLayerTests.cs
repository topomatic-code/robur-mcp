using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class AddLayerTests
    {
        [Test]
        internal static void CheckCreationAndDefaults()
        {
            var f = new DwgToolsFixture();
            var result = DwgToolsFixture.Result(f.Tools.AddLayer(DwgToolsFixture.Args("{'name':'New','description':'Description','colorIndex':3,'isVisible':false}")));
            var layer = f.Drawing.Layers["New"];
            Test.Require(layer != null && layer.Description == "Description" && layer.Color.ColorIndex == 3 && !layer.Visible, "Layer properties must be saved.");
            Test.Require((string)result["name"] == "New" && !(bool)result["isVisible"], "Response must describe created layer.");
            f.Tools.AddLayer(DwgToolsFixture.Args("{'name':'Default'}"));
            Test.Require(f.Drawing.Layers.IsExists("Default"), "Optional properties may be omitted.");
            Test.Require(f.Logger.Messages.Count == 2, "Each creation must log an update.");
        }

        [Test]
        internal static void CheckInvalidAndDuplicateNames()
        {
            var f = new DwgToolsFixture();
            foreach (var json in new[] { "{}", "{'name':' '}", "{'name':'Bad','colorIndex':-1}" })
                Test.Throws<BadRequestException>(() => f.Tools.AddLayer(DwgToolsFixture.Args(json)));
            f.Drawing.Layers.Add("Existing");
            Test.Throws<PreconditionFailedException>(() => f.Tools.AddLayer(DwgToolsFixture.Args("{'name':'Existing'}")));
            Test.Require(!f.Drawing.Layers.IsExists("Bad") && f.Logger.Messages.Count == 0, "Validation must precede edits.");
            f.CheckDrawingError(f.Tools.AddLayer);
        }
    }
}

