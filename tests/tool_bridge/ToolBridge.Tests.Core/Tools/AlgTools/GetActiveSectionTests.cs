using System.Collections.Generic;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class GetActiveSectionTests
    {
        [Test]
        internal static void CheckCurrentSelection()
        {
            var f = new AlgToolsFixture();
            var construction = f.AddConstruction();
            f.AddSection(0, construction);
            f.AddSection(100, construction);
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId };
            foreach (var index in new[] { -1, 0, 1 })
            {
                f.ActiveSection = index;
                Test.Require((int)AlgToolsFixture.Result(f.Tools.GetActiveSection(args))["activeSection"] == index, "Get must read the current selection, including no selection.");
                Test.Require(f.ActiveSection == index, "Get must not change selection.");
            }
            Test.Require(f.ProviderCalls == 3, "Each request must resolve the road through DI.");
        }

        [Test]
        internal static void CheckInvalidArguments()
        {
            var f = new AlgToolsFixture();
            AlgToolsFixture.CheckInvalidPath(f.Tools.GetActiveSection, "algPathId");
            Test.Require(f.ProviderCalls == 0, "Invalid arguments must not reach the provider.");
        }

        [Test]
        internal static void CheckUnavailableModelAndProviderErrors()
        {
            var f = new AlgToolsFixture { SectionModelAvailable = false };
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId };
            Test.Throws<PreconditionFailedException>(() => f.Tools.GetActiveSection(args));
            var error = new PreconditionFailedException("Road is unavailable");
            f.ProviderError = error;
            Test.Require(ReferenceEquals(error, Test.Throws<PreconditionFailedException>(() => f.Tools.GetActiveSection(args))), "Get must propagate provider errors.");
        }
    }
}
