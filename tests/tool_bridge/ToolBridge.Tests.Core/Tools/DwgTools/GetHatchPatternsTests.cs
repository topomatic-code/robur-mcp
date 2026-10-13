using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Topomatic.Dwg;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class GetHatchPatternsTests
    {
        [Test]
        internal static void CheckProviderSortingAndMetadata()
        {
            var f = new DwgToolsFixture();
            f.DrawingError = new InvalidOperationException("Pattern catalog must not request a drawing.");
            f.Patterns = new[] { new HatchPattern { Name = "Z", Description = "Last" }, new HatchPattern { Name = "A", Description = "First" } };
            var result = DwgToolsFixture.Result(f.Tools.GetHatchPatterns(DwgToolsFixture.Args()));
            var patterns = (JArray)result["patterns"];
            Test.Require(patterns.Count == 2 && (string)patterns[0]["name"] == "A" && (string)patterns[1]["name"] == "Z", "Patterns must be sorted by name.");
            Test.Require((string)patterns[0]["description"] == "First" && patterns[0]["lines"].Count() == 0, "Description and empty lines must be preserved.");
            Test.Require(f.PatternCalls == 1 && f.DrawingCalls == 0, "Only pattern provider must be queried.");
            f.Patterns = new HatchPattern[0];
            Test.Require(DwgToolsFixture.Result(f.Tools.GetHatchPatterns(DwgToolsFixture.Args()))["patterns"].Count() == 0, "Subsequent calls must read current provider data.");
        }

        [Test]
        internal static void CheckProviderError()
        {
            var f = new DwgToolsFixture();
            f.PatternError = new PreconditionFailedException("Catalog unavailable");
            Test.Require(ReferenceEquals(f.PatternError, Test.Throws<PreconditionFailedException>(() => f.Tools.GetHatchPatterns(DwgToolsFixture.Args()))), "Provider exception must propagate unchanged.");
        }
    }
}

