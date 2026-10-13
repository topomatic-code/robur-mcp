using System.Collections.Generic;
using System.Linq;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class RemoveLayerTests
    {
        [Test]
        internal static void CheckRemoval()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Remove");
            var result = DwgToolsFixture.Result(f.Tools.RemoveLayer(DwgToolsFixture.Args("{'name':'Remove'}")));
            Test.Require(!f.Drawing.Layers.IsExists("Remove") && (string)result["name"] == "Remove", "Removed layer metadata must be returned.");
            Test.Throws<PreconditionFailedException>(() => f.Tools.RemoveLayer(DwgToolsFixture.Args("{'name':'Remove'}")));
        }

        [Test]
        internal static void CheckValidationAndSystemLayer()
        {
            var f = new DwgToolsFixture();
            Test.Throws<BadRequestException>(() => f.Tools.RemoveLayer(DwgToolsFixture.Args()));
            Test.Throws<BadRequestException>(() => f.Tools.RemoveLayer(DwgToolsFixture.Args("{'name':' '}")));
            var system = f.Drawing.Layers.First(l => l.IsSystem);
            Test.Throws<PreconditionFailedException>(() => f.Tools.RemoveLayer(new Dictionary<string, object> { ["name"] = system.Name }));
            Test.Require(f.Drawing.Layers.IsExists(system.Name), "System layer must survive rejected removal.");
            f.CheckDrawingError(f.Tools.RemoveLayer);
        }
    }
}

