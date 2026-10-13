using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class UpdateLayerTests
    {
        [Test]
        internal static void CheckRenameAndPartialUpdate()
        {
            var f = new DwgToolsFixture();
            var layer = f.Drawing.Layers.Add("Old");
            layer.Description = "Keep";
            var result = DwgToolsFixture.Result(f.Tools.UpdateLayer(DwgToolsFixture.Args("{'currentName':'Old','name':'New','colorIndex':5,'isVisible':false}")));
            Test.Require(layer.Name == "New" && !f.Drawing.Layers.IsExists("Old") && layer.Description == "Keep", "Rename must preserve omitted description.");
            Test.Require(layer.Color.ColorIndex == 5 && !layer.Visible && (string)result["name"] == "New", "Appearance and response must update.");
            f.Tools.UpdateLayer(DwgToolsFixture.Args("{'currentName':'New','description':'Changed'}"));
            Test.Require(layer.Description == "Changed" && layer.Color.ColorIndex == 5 && !layer.Visible, "Partial update must preserve appearance.");
        }

        [Test]
        internal static void CheckValidation()
        {
            var f = new DwgToolsFixture();
            f.Drawing.Layers.Add("Old"); f.Drawing.Layers.Add("Taken");
            foreach (var json in new[] { "{}", "{'currentName':' '}", "{'currentName':'Old','name':' '}", "{'currentName':'Old','colorIndex':-1}" })
                Test.Throws<BadRequestException>(() => f.Tools.UpdateLayer(DwgToolsFixture.Args(json)));
            foreach (var json in new[] { "{'currentName':'Missing'}", "{'currentName':'Old','name':'Taken'}" })
                Test.Throws<PreconditionFailedException>(() => f.Tools.UpdateLayer(DwgToolsFixture.Args(json)));
            Test.Require(f.Drawing.Layers.IsExists("Old") && f.Logger.Messages.Count == 0, "Rejected update must preserve layers.");
            f.CheckDrawingError(f.Tools.UpdateLayer);
        }
    }
}

