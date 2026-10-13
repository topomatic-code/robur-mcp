using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class RemoveActiveSpaceEntityTests
    {
        [Test]
        internal static void CheckRemovalFromDrawingAndStorage()
        {
            foreach (var cache in new[] { false, true })
            {
                var f = new DwgToolsFixture();
                var line = DwgToolsFixture.Line();
                var guid = f.Add(line, "Removed", cache);
                var result = DwgToolsFixture.Result(f.Tools.RemoveActiveSpaceEntity(DwgToolsFixture.EntityArgs(guid)));
                Test.Require(f.Drawing.ActiveSpace.Entities.Count == 0 && !f.Storage.HasObject(guid), "Removal must clear drawing and storage.");
                Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Removed" && (string)result["type"] == "dwg_line", "Removed identity must be returned.");
                Test.Throws<PreconditionFailedException>(() => f.Tools.RemoveActiveSpaceEntity(DwgToolsFixture.EntityArgs(guid)));
            }
        }

        [Test]
        internal static void CheckLookupErrors()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.RemoveActiveSpaceEntity);
            f.CheckDrawingError(f.Tools.RemoveActiveSpaceEntity);
        }
    }
}

