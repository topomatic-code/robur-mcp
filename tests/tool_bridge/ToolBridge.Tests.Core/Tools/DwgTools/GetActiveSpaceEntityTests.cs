namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class GetActiveSpaceEntityTests
    {
        [Test]
        internal static void CheckStorageAndDrawingLookup()
        {
            foreach (var cache in new[] { false, true })
            {
                var f = new DwgToolsFixture();
                var line = DwgToolsFixture.Line();
                var guid = f.Add(line, "Line", cache);
                var result = DwgToolsFixture.Result(f.Tools.GetActiveSpaceEntity(DwgToolsFixture.EntityArgs(guid)));
                Test.Require((string)result["guid"] == guid.ToString() && (string)result["name"] == "Line" && (string)result["type"] == "dwg_line", "Entity identity and type must be returned.");
                Test.Require((double)result["length"] == 5 && (double)result["startPoint"]["x"] == 1, "Entity geometry must be returned.");
                Test.Require(ReferenceEquals(f.Storage.GetObject(guid), line), "Drawing lookup must populate storage.");
                Test.Require(f.Logger.Messages.Count == 0, "Reading must not start an edit.");
            }
        }

        [Test]
        internal static void CheckLookupErrors()
        {
            var f = new DwgToolsFixture();
            f.CheckLookupErrors(f.Tools.GetActiveSpaceEntity);
            f.CheckDrawingError(f.Tools.GetActiveSpaceEntity);
        }
    }
}

