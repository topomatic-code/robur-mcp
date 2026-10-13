using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class SetEntitiesLayerTests
    {
        [Test]
        internal static void CheckBatchUpdate()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Target");
            var a = DwgToolsFixture.Line(); var b = new DwgCircle { Radius = 2 };
            var ga = f.Add(a); var gb = f.Add(b);
            var args = new Dictionary<string, object> { ["guids"] = new JArray(ga.ToString(), gb.ToString()), ["layerName"] = "Target" };
            var result = DwgToolsFixture.Result(f.Tools.SetEntitiesLayer(args));
            Test.Require(a.Layer.Name == "Target" && b.Layer.Name == "Target", "All selected entities must change layer.");
            Test.Require(result["entities"].Count() == 2 && result["entities"].All(e => (string)e["layerName"] == "Target"), "Response must include each changed entity.");
            Test.Require(f.Logger.Messages.Count == 1, "Batch must use a single drawing update.");
        }

        [Test]
        internal static void CheckValidationBeforeBatch()
        {
            var f = new DwgToolsFixture();
            var line = DwgToolsFixture.Line(); var guid = f.Add(line); var original = line.Layer;
            f.Drawing.Layers.Add("Target");
            Test.Throws<BadRequestException>(() => f.Tools.SetEntitiesLayer(DwgToolsFixture.Args("{'guids':[],'layerName':'Target'}")));
            var args = new Dictionary<string, object> { ["guids"] = new JArray(guid.ToString(), Guid.NewGuid().ToString()), ["layerName"] = "Target" };
            Test.Throws<PreconditionFailedException>(() => f.Tools.SetEntitiesLayer(args));
            Test.Require(ReferenceEquals(original, line.Layer), "Missing entity must be detected before mutating the batch.");
            args["guids"] = new JArray(guid.ToString());
            args["layerName"] = "Missing";
            Test.Throws<PreconditionFailedException>(() => f.Tools.SetEntitiesLayer(args));
            args["layerName"] = " ";
            Test.Throws<BadRequestException>(() => f.Tools.SetEntitiesLayer(args));
            f.CheckDrawingError(f.Tools.SetEntitiesLayer);
        }
    }
}

