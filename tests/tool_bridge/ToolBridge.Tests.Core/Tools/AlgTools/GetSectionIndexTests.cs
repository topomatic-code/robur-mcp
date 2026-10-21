using System.Collections.Generic;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class GetSectionIndexTests
    {
        [Test]
        internal static void CheckExistingAndMissingSections()
        {
            var f = new AlgToolsFixture();
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId, ["station"] = 10.0 };
            Test.Throws<BadRequestException>(() => f.Tools.GetSectionIndex(args));
            var construction = f.AddConstruction();
            foreach (var station in new[] { 10.0, 40.0, 100.0 }) f.AddSection(station, construction);
            var stations = new[] { 10.0, 40.0, 100.0 };
            for (var i = 0; i < stations.Length; i++)
            {
                args["station"] = stations[i];
                Test.Require((int)AlgToolsFixture.Result(f.Tools.GetSectionIndex(args))["sectionIndex"] == i, "Existing sections must return zero-based indices, including both endpoints.");
            }
            foreach (var station in new[] { 0.0, 25.0, 110.0 })
            {
                args["station"] = station;
                Test.Throws<BadRequestException>(() => f.Tools.GetSectionIndex(args));
            }
            Test.Require(f.ActiveSection == -1, "Lookup must not change the active section.");
        }

        [Test]
        internal static void CheckArgumentsAndProviderErrors()
        {
            var f = new AlgToolsFixture();
            AlgToolsFixture.CheckInvalidPath(f.Tools.GetSectionIndex, "algPathId");
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId };
            Test.Throws<BadRequestException>(() => f.Tools.GetSectionIndex(args));
            foreach (var station in new object[] { null, "PK 1+00", double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                args["station"] = station;
                Test.Throws<BadRequestException>(() => f.Tools.GetSectionIndex(args));
            }
            Test.Require(f.ProviderCalls == 0, "Invalid arguments must not reach the provider.");
            args["station"] = 10.0;
            var error = new PreconditionFailedException("Road is unavailable");
            f.ProviderError = error;
            Test.Require(ReferenceEquals(error, Test.Throws<PreconditionFailedException>(() => f.Tools.GetSectionIndex(args))), "Provider errors must propagate unchanged.");
        }
    }
}
