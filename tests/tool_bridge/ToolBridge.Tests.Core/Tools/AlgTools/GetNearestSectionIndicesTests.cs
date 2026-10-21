using System.Collections.Generic;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class GetNearestSectionIndicesTests
    {
        [Test]
        internal static void CheckIntervalsAndBoundaries()
        {
            var f = new AlgToolsFixture();
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId, ["station"] = 10.0 };
            Test.Throws<BadRequestException>(() => f.Tools.GetNearestSectionIndices(args));
            var construction = f.AddConstruction();
            f.AddSection(10, construction);
            var single = AlgToolsFixture.Result(f.Tools.GetNearestSectionIndices(args));
            Test.Require((int)single["prevSectionIndex"] == 0 && (int)single["nextSectionIndex"] == 0, "A single section must supply both indices at its station.");
            f.AddSection(40, construction);
            f.AddSection(100, construction);
            var stations = new[] { 10.0, 25.0, 40.0, 70.0, 100.0, 110.0 };
            var previous = new[] { 0, 0, 1, 1, 2, 2 };
            var next = new[] { 1, 1, 2, 2, 2, 2 };
            for (var i = 0; i < stations.Length; i++)
            {
                args["station"] = stations[i];
                var result = AlgToolsFixture.Result(f.Tools.GetNearestSectionIndices(args));
                Test.Require((int)result["prevSectionIndex"] == previous[i] && (int)result["nextSectionIndex"] == next[i], "Lookup must preserve Robur interval semantics at exact stations, between stations and after the last section.");
            }
            args["station"] = 0.0;
            Test.Throws<BadRequestException>(() => f.Tools.GetNearestSectionIndices(args));
            Test.Require(f.ActiveSection == -1, "Lookup must not change the active section.");
        }

        [Test]
        internal static void CheckArgumentsAndProviderErrors()
        {
            var f = new AlgToolsFixture();
            AlgToolsFixture.CheckInvalidPath(f.Tools.GetNearestSectionIndices, "algPathId");
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId };
            Test.Throws<BadRequestException>(() => f.Tools.GetNearestSectionIndices(args));
            foreach (var station in new object[] { null, "PK 1+00", double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                args["station"] = station;
                Test.Throws<BadRequestException>(() => f.Tools.GetNearestSectionIndices(args));
            }
            Test.Require(f.ProviderCalls == 0, "Invalid arguments must not reach the provider.");
            args["station"] = 10.0;
            var error = new PreconditionFailedException("Road is unavailable");
            f.ProviderError = error;
            Test.Require(ReferenceEquals(error, Test.Throws<PreconditionFailedException>(() => f.Tools.GetNearestSectionIndices(args))), "Provider errors must propagate unchanged.");
        }
    }
}
