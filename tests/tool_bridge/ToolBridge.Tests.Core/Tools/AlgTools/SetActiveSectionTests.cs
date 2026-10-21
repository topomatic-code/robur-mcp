using System.Collections.Generic;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class SetActiveSectionTests
    {
        [Test]
        internal static void CheckSelectionAndRepeatedSelection()
        {
            var f = new AlgToolsFixture();
            var construction = f.AddConstruction();
            f.AddSection(0, construction);
            f.AddSection(100, construction);
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId };
            foreach (var index in new[] { 1, 1, 0 })
            {
                var setArgs = new Dictionary<string, object>(args) { ["activeSection"] = index };
                Test.Require((int)AlgToolsFixture.Result(f.Tools.SetActiveSection(setArgs))["activeSection"] == index, "Set must return the selected index.");
                Test.Require(f.ActiveSection == index, "Selection must be delegated to the provider.");
            }
        }

        [Test]
        internal static void CheckInvalidArgumentsAndRange()
        {
            var f = new AlgToolsFixture();
            AlgToolsFixture.CheckInvalidPath(f.Tools.SetActiveSection, "algPathId");
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId };
            Test.Throws<BadRequestException>(() => f.Tools.SetActiveSection(args));
            foreach (var value in new object[] { 0.5, "invalid", null })
            {
                args["activeSection"] = value;
                Test.Throws<BadRequestException>(() => f.Tools.SetActiveSection(args));
            }
            Test.Require(f.ProviderCalls == 0, "Invalid arguments must not reach the provider.");
            f.AddSection(0, f.AddConstruction());
            foreach (var index in new[] { -1, 1 })
            {
                args["activeSection"] = index;
                Test.Throws<BadRequestException>(() => f.Tools.SetActiveSection(args));
                Test.Require(f.ActiveSection == -1, "Rejected indices must not change selection.");
            }
        }

        [Test]
        internal static void CheckUnavailableModelAndProviderErrors()
        {
            var f = new AlgToolsFixture { SectionModelAvailable = false };
            var args = new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId, ["activeSection"] = 0 };
            Test.Throws<PreconditionFailedException>(() => f.Tools.SetActiveSection(args));
            var error = new PreconditionFailedException("Road is unavailable");
            f.ProviderError = error;
            Test.Require(ReferenceEquals(error, Test.Throws<PreconditionFailedException>(() => f.Tools.SetActiveSection(args))), "Set must propagate provider errors.");
        }
    }
}
