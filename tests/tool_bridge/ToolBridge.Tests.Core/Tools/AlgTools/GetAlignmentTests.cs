using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class GetAlignmentTests
    {
        [Test]
        internal static void CheckUsedConstructionsByDefault()
        {
            var f = new AlgToolsFixture();
            var used = f.AddConstruction("Same name", true);
            var unused = f.AddConstruction("Same name");
            f.AddSection(0, used);
            f.AddSection(100, used);
            foreach (var explicitFilter in new[] { false, true })
            {
                var args = new Dictionary<string, object> { ["pathId"] = AlgToolsFixture.PathId };
                if (explicitFilter) args["usedOnlyConstructions"] = true;
                var result = AlgToolsFixture.Result(f.Tools.GetAlignment(args));
                Test.Require((string)result["name"] == f.Name && (string)result["description"] == "Road description", "Road metadata must be returned.");
                var corridor = result["corridor"];
                var items = (JArray)corridor["constructions"];
                Test.Require(items.Count == 1, "Repeated assignments must not duplicate constructions; unused ones must be excluded.");
                Test.Require((string)items[0]["id"] == used.Id.ToString(CultureInfo.InvariantCulture), "Filter must use IDs, not names.");
                Test.Require((bool)items[0]["isUsed"] && (bool)items[0]["userDefined"], "Construction flags must be preserved.");
                Test.Require((int)corridor["sectionsCount"] == 2, "All sections must be counted.");
                Test.Require((int)corridor["constructionsCount"] == f.Alignment.Corridor.Constructions.Count(), "Count must include unused constructions.");
            }
            Test.Require(f.ProviderCalls == 2, "Both requests must resolve the road through DI.");
        }

        [Test]
        internal static void CheckFullListAndUpdatedAssignments()
        {
            var f = new AlgToolsFixture();
            var first = f.AddConstruction("First", true);
            var second = f.AddConstruction("Second", false);
            f.AddSection(0, second);
            var args = new Dictionary<string, object> { ["pathId"] = AlgToolsFixture.PathId, ["usedOnlyConstructions"] = false };
            var items = (JArray)AlgToolsFixture.Result(f.Tools.GetAlignment(args))["corridor"]["constructions"];
            Test.Require(items.Count == f.Alignment.Corridor.Constructions.Count(), "False must include every construction.");
            var a = items.Single(x => (string)x["id"] == first.Id.ToString());
            var b = items.Single(x => (string)x["id"] == second.Id.ToString());
            Test.Require(!(bool)a["isUsed"] && (bool)a["userDefined"], "User-defined does not imply used.");
            Test.Require((bool)b["isUsed"] && !(bool)b["userDefined"], "A standard construction can be used.");
            f.Alignment.Corridor.Sections[0].ConstructionId = first.Id;
            args["usedOnlyConstructions"] = true;
            items = (JArray)AlgToolsFixture.Result(f.Tools.GetAlignment(args))["corridor"]["constructions"];
            Test.Require(items.Count == 1 && (string)items[0]["id"] == first.Id.ToString(), "Assignments must be read afresh on the next call.");
        }

        [Test]
        internal static void CheckNoAssignedSections()
        {
            var f = new AlgToolsFixture();
            f.AddConstruction();
            var result = AlgToolsFixture.Result(f.Tools.GetAlignment(new Dictionary<string, object> { ["pathId"] = AlgToolsFixture.PathId }));
            Test.Require(((JArray)result["corridor"]["constructions"]).Count == 0, "A road with no sections must return an empty filtered list.");
            Test.Require((int)result["corridor"]["sectionsCount"] == 0, "Empty road section count.");
        }

        [Test]
        internal static void CheckArgumentsAndProviderErrors()
        {
            var f = new AlgToolsFixture();
            AlgToolsFixture.CheckInvalidPath(f.Tools.GetAlignment, "pathId");
            Test.Throws<BadRequestException>(() => f.Tools.GetAlignment(new Dictionary<string, object> { ["pathId"] = AlgToolsFixture.PathId, ["usedOnlyConstructions"] = "invalid" }));
            Test.Require(f.ProviderCalls == 0, "Invalid arguments must not reach the provider.");
            var error = new PreconditionFailedException("Road is unavailable");
            f.ProviderError = error;
            Test.Require(ReferenceEquals(error, Test.Throws<PreconditionFailedException>(() => f.Tools.GetAlignment(new Dictionary<string, object> { ["pathId"] = AlgToolsFixture.PathId }))), "Provider errors must propagate.");
        }
    }
}
