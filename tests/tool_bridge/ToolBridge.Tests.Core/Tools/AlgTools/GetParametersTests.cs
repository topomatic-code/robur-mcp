using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Alg.Parameters;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class GetParametersTests
    {
        [Test]
        internal static void CheckComputedAndTableMetadata()
        {
            var f = new AlgToolsFixture();
            var evaluations = 0;
            f.Alignment.Parameters.DefineComputedParameter<double>("TEST_COMPUTED", "Computed width", (a, station) => { evaluations++; throw new InvalidOperationException("Must not evaluate metadata"); }, null, false);
            f.Alignment.Parameters.DefineParameterTable<double>("TEST_WIDTH", "Width", BehaviorType.Interpolate, 3.5, false, false);
            f.Alignment.Parameters.DefineParameterTable<bool>("TEST_ENABLED", "Enabled", BehaviorType.Discrete, true, false, false);
            var result = AlgToolsFixture.Result(f.Tools.GetParameters(new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId }));
            var computed = (JArray)result["computedParameters"];
            var table = (JArray)result["tableParameters"];
            var item = computed.Single(x => (string)x["name"] == "TEST_COMPUTED");
            Test.Require((string)item["caption"] == "Computed width" && (string)item["type"] == "Computed", "Computed parameter metadata must be preserved.");
            item = table.Single(x => (string)x["name"] == "TEST_WIDTH");
            Test.Require((string)item["caption"] == "Width" && (string)item["type"] == "Double", "Table parameter caption and numeric type.");
            Test.Require((string)table.Single(x => (string)x["name"] == "TEST_ENABLED")["type"] == "Boolean", "Boolean table type.");
            Test.Require(!table.Any(x => (string)x["name"] == "TEST_COMPUTED") && !computed.Any(x => (string)x["name"] == "TEST_WIDTH"), "Parameter categories must not be mixed.");
            Test.Require(evaluations == 0 && f.ProviderCalls == 1, "Metadata retrieval must use DI without evaluating parameter values.");
        }

        [Test]
        internal static void CheckRemovedParametersAndEmptyTables()
        {
            var f = new AlgToolsFixture();
            foreach (var entry in f.Alignment.Parameters.GetTableParameters().ToArray()) f.Alignment.Parameters.Remove(entry.Key);
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId };
            Test.Require(((JArray)AlgToolsFixture.Result(f.Tools.GetParameters(args))["tableParameters"]).Count == 0, "No table parameters must serialize as an empty array.");
            f.Alignment.Parameters.DefineParameterTable<int>("TEST_COUNT", "Count", BehaviorType.Discrete, 2, false, false);
            Test.Require(((JArray)AlgToolsFixture.Result(f.Tools.GetParameters(args))["tableParameters"]).Any(x => (string)x["name"] == "TEST_COUNT" && (string)x["type"] == "Integer"), "New parameters must be visible.");
            f.Alignment.Parameters.Remove("TEST_COUNT");
            Test.Require(((JArray)AlgToolsFixture.Result(f.Tools.GetParameters(args))["tableParameters"]).Count == 0, "Removed parameters must not be cached.");
        }

        [Test]
        internal static void CheckArgumentsAndProviderErrors()
        {
            var f = new AlgToolsFixture();
            AlgToolsFixture.CheckInvalidPath(f.Tools.GetParameters, "algPathId");
            Test.Require(f.ProviderCalls == 0, "Invalid arguments must not reach the provider.");
            var error = new PreconditionFailedException("Road is unavailable");
            f.ProviderError = error;
            Test.Require(ReferenceEquals(error, Test.Throws<PreconditionFailedException>(() => f.Tools.GetParameters(new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId }))), "Provider errors must propagate.");
        }
    }
}
