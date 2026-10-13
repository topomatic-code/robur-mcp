using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Infrastructure;
using Topomatic.ToolBridge.Services;
using ToolsUnderTest = Topomatic.ToolBridge.Tools.DwgTools;

namespace ToolBridge.Tests.Core.Tools.DwgTools
{
    internal sealed class DwgToolsFixture : IDrawingProvider, IHatchPatternProvider
    {
        private Drawing m_Drawing;
        internal Drawing Drawing
        {
            get
            {
                if (m_Drawing == null)
                {
                    m_Drawing = new Drawing();
                    // Font rendering is outside these tests. Deterministic metrics
                    // let real text/table models compute bounds without font files.
                    m_Drawing.ActiveStyle.Font = new TestFont();
                }
                return m_Drawing;
            }
        }
        internal readonly IObjectStorage Storage;
        internal readonly ToolsUnderTest Tools;
        internal readonly StubLogger Logger = new StubLogger();
        internal int DrawingCalls;
        internal int PatternCalls;
        internal Exception DrawingError;
        internal Exception PatternError;
        internal IEnumerable<HatchPattern> Patterns = new HatchPattern[0];

        internal DwgToolsFixture()
        {
            var container = new Container();
            container.RegisterSingleton<IDrawingProvider>(() => this);
            container.RegisterSingleton<IHatchPatternProvider>(() => this);
            container.RegisterSingleton<IObjectStorage, ObjectStorage>();
            Storage = container.GetSingleton<IObjectStorage>();
            container.RegisterSingleton<IToolBridgeLogger>(() => Logger);
            Tools = new ToolsUnderTest { Container = container };
        }

        public Drawing GetActiveDrawing(bool require)
        {
            DrawingCalls++;
            Test.Require(require, "Tool must require an active drawing.");
            if (DrawingError != null) throw DrawingError;
            return Drawing;
        }

        public IEnumerable<HatchPattern> GetDefinedPatterns()
        {
            PatternCalls++;
            if (PatternError != null) throw PatternError;
            return Patterns;
        }

        internal static Dictionary<string, object> Args(string json = "{}") =>
            JObject.Parse(json).ToObject<Dictionary<string, object>>();

        internal static JObject Result(object response)
        {
            var json = JObject.FromObject(response);
            Test.Require(json["result"] is JObject, "Response must contain a result object.");
            Test.Require(!string.IsNullOrWhiteSpace((string)json["status"]), "Response must contain status.");
            return (JObject)json["result"];
        }

        internal Guid Add(DwgEntity entity, string name = "Original", bool cache = true)
        {
            Drawing.ActiveSpace.Add(entity);
            var guid = Guid.NewGuid();
            entity.CreateExtensionDictionary();
            entity.GetExtensionDictionary().SetString("guid", guid.ToString());
            entity.GetExtensionDictionary().SetString("name", name);
            if (cache) Storage.AddObject(guid, entity);
            return guid;
        }

        internal static DwgLine Line() => new DwgLine
        {
            StartPoint = new Vector3D(1, 2, 0),
            EndPoint = new Vector3D(4, 6, 0)
        };

        internal static Dictionary<string, object> EntityArgs(Guid guid, string json = "{}")
        {
            var args = Args(json);
            args["guid"] = guid.ToString();
            return args;
        }

        internal T Created<T>(JObject result) where T : DwgEntity
        {
            var guid = Guid.Parse((string)result["guid"]);
            var entity = Storage.GetObject(guid) as T;
            Test.Require(entity != null && entity.Drawing == Drawing, "Created entity must be stored and belong to the drawing.");
            Test.Require(Drawing.ActiveSpace.Entities.Count == 1, "Exactly one entity must be added.");
            Test.Require(entity.GetExtensionDictionary().GetString("guid", null) == guid.ToString(), "Persistent GUID must match storage.");
            Test.Require(entity.GetExtensionDictionary().GetString("name", null) == "Created", "Persistent name must be saved.");
            Test.Require((string)result["name"] == "Created" && Logger.Messages.Count == 1, "Response name and update log must be present.");
            return entity;
        }

        internal void CheckLookupErrors(Func<Dictionary<string, object>, object> call)
        {
            Test.Throws<BadRequestException>(() => call(Args()));
            Test.Throws<BadRequestException>(() => call(Args("{'guid':'invalid'}")));
            Test.Throws<PreconditionFailedException>(() => call(EntityArgs(Guid.NewGuid())));
            var other = new Drawing();
            var entity = Line();
            other.ActiveSpace.Add(entity);
            var foreignGuid = Storage.AddObject(entity);
            Test.Throws<PreconditionFailedException>(() => call(EntityArgs(foreignGuid)));
        }

        internal void CheckDrawingError(Func<Dictionary<string, object>, object> call)
        {
            DrawingError = new PreconditionFailedException("Drawing unavailable");
            Test.Require(ReferenceEquals(DrawingError, Test.Throws<PreconditionFailedException>(() => call(Args()))),
                "Drawing provider errors must propagate.");
        }

        private sealed class TestFont : CadFont
        {
            public override string Name => "Test";
            public override string FileName => "Test";
            public override string FilePath => "Test";
            protected override System.Drawing.SizeF GetTextSize(string text, double height, double ratio, double pressure, bool trimLastSpace) =>
                new System.Drawing.SizeF((float)((text?.Length ?? 0) * height * ratio), (float)height);
            protected override void DoDrawString(string text, CadPen pen, Vector3D position, double height,
                double ratio, double pressure, double angle, double oblique, double width, TextJustify justify) =>
                throw new InvalidOperationException("Tests must not render text.");
        }

        internal sealed class StubLogger : IToolBridgeLogger
        {
            internal readonly List<string> Messages = new List<string>();
            public bool SystemLoggingEnabled => false;
            public string CreateLogString(string message) { Messages.Add(message); return message; }
            public void PublicInfo(string message) { }
            public void PublicWarning(string message) { }
            public void PublicError(string message) { }
            public void SystemInfo(string message) { }
            public void SystemWarning(string message, Exception exception = null) { }
            public void SystemError(string message, Exception exception = null) { }
            public void EnableSystemLogging(string path) { }
        }
    }
}

