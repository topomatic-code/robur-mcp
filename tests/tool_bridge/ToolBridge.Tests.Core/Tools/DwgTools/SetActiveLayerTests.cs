using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class SetActiveLayerTests
    {
        [Test]
        internal static void CheckActivationAndRepeatedCall()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Target");
            for (var i = 0; i < 2; i++)
            {
                var result = DwgToolsFixture.Result(f.Tools.SetActiveLayer(DwgToolsFixture.Args("{'name':'Target'}")));
                Test.Require(f.Drawing.ActiveLayer.Name == "Target" && (string)result["name"] == "Target", "Requested layer must become active.");
            }
        }

        [Test]
        internal static void CheckValidation()
        {
            var f = new DwgToolsFixture();
            var original = f.Drawing.ActiveLayer;
            Test.Throws<BadRequestException>(() => f.Tools.SetActiveLayer(DwgToolsFixture.Args()));
            Test.Throws<BadRequestException>(() => f.Tools.SetActiveLayer(DwgToolsFixture.Args("{'name':' '}")));
            Test.Throws<PreconditionFailedException>(() => f.Tools.SetActiveLayer(DwgToolsFixture.Args("{'name':'Missing'}")));
            Test.Require(ReferenceEquals(original, f.Drawing.ActiveLayer), "Invalid request must preserve active layer.");
            f.CheckDrawingError(f.Tools.SetActiveLayer);
        }
    }
}

