using Newtonsoft.Json.Linq;
using System;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class GetEntitiesTests
    {
        [Test]
        internal static void CheckRangeAndStableIdentifiers()
        {
            var f = new DwgToolsFixture();
            var first = DwgToolsFixture.Line();
            f.Drawing.ActiveSpace.Add(first);
            var second = DwgToolsFixture.Line();
            var secondGuid = f.Add(second, "Persistent", false);
            f.Add(DwgToolsFixture.Line(), "Outside");
            var args = DwgToolsFixture.Args("{'startIndex':0,'endIndex':1}");
            var result = DwgToolsFixture.Result(f.Tools.GetEntities(args));
            var items = (JArray)result["entitiesInfo"];
            Test.Require(items.Count == 2 && (int)result["startIndex"] == 0 && (int)result["endIndex"] == 1, "Range must include both endpoints only.");
            Test.Require((string)items[0]["name"] == "none" && (string)items[1]["name"] == "Persistent", "Missing and persistent names must be handled.");
            Test.Require((string)items[1]["guid"] == secondGuid.ToString() && ReferenceEquals(f.Storage.GetObject(secondGuid), second), "Persistent GUID must be cached.");
            var repeated = DwgToolsFixture.Result(f.Tools.GetEntities(args));
            Test.Require((string)repeated["entitiesInfo"][0]["guid"] == (string)items[0]["guid"], "Generated identifiers must be stable within session.");
        }

        [Test]
        internal static void CheckInvalidRangesAndStoredGuid()
        {
            var f = new DwgToolsFixture();
            Test.Throws<BadRequestException>(() => f.Tools.GetEntities(DwgToolsFixture.Args("{'startIndex':0,'endIndex':0}")));
            var line = DwgToolsFixture.Line(); f.Add(line);
            foreach (var json in new[] { "{}", "{'startIndex':-1,'endIndex':0}", "{'startIndex':0,'endIndex':-1}", "{'startIndex':0,'endIndex':1}" })
                Test.Throws<BadRequestException>(() => f.Tools.GetEntities(DwgToolsFixture.Args(json)));
            line.GetExtensionDictionary().SetString("guid", "invalid");
            Test.Throws<InvalidOperationException>(() => f.Tools.GetEntities(DwgToolsFixture.Args("{'startIndex':0,'endIndex':0}")));
            f.CheckDrawingError(f.Tools.GetEntities);
        }
    }
}

