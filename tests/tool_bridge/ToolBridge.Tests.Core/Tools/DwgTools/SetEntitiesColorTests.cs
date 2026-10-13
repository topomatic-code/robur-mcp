using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class SetEntitiesColorTests
    {
        [Test]
        internal static void CheckColorModesOnBatch()
        {
            var f = new DwgToolsFixture();
            var a = DwgToolsFixture.Line(); var b = new DwgCircle { Radius = 2 };
            var ga = f.Add(a); var gb = f.Add(b);
            var args = new Dictionary<string, object> { ["guids"] = new JArray(ga.ToString(), gb.ToString()), ["colorIndex"] = 5 };
            foreach (var mode in new[] { "Indexed", "ByLayer", "ByBlock" })
            {
                args["colorMode"] = mode;
                var result = DwgToolsFixture.Result(f.Tools.SetEntitiesColor(args));
                var expected = mode == "Indexed" ? new CadColor(5) : mode == "ByLayer" ? CadColor.ByLayer : CadColor.ByBlock;
                Test.Require(a.Color == expected && b.Color == expected, "All selected entities must receive requested color.");
                Test.Require(result["entities"].Count() == 2 && result["entities"].All(e => (string)e["colorMode"] == mode), "Response must describe selected color mode.");
            }
        }

        [Test]
        internal static void CheckInvalidColorsAndBatchLookup()
        {
            var f = new DwgToolsFixture();
            var line = DwgToolsFixture.Line(); var guid = f.Add(line); var original = line.Color;
            foreach (var json in new[] { "{'colorMode':'Unknown'}", "{'colorMode':'Indexed'}", "{'colorMode':'Indexed','colorIndex':-1}" })
            {
                var args = DwgToolsFixture.Args(json); args["guids"] = new JArray(guid.ToString());
                Test.Throws<BadRequestException>(() => f.Tools.SetEntitiesColor(args));
            }
            Test.Throws<BadRequestException>(() => f.Tools.SetEntitiesColor(DwgToolsFixture.Args("{'guids':[],'colorMode':'ByLayer'}")));
            var batch = new Dictionary<string, object> { ["guids"] = new JArray(guid.ToString(), Guid.NewGuid().ToString()), ["colorMode"] = "Indexed", ["colorIndex"] = 3 };
            Test.Throws<PreconditionFailedException>(() => f.Tools.SetEntitiesColor(batch));
            Test.Require(line.Color == original, "Invalid color or missing entity must preserve color.");
            f.CheckDrawingError(f.Tools.SetEntitiesColor);
        }
    }
}

