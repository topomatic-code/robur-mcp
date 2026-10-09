using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using Topomatic.Alg;
using Topomatic.Alg.Crs;
using Topomatic.Alg.Road;
using Topomatic.Alg.Style;
using Topomatic.Crs.Ast;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services;
using ToolsUnderTest = Topomatic.ToolBridge.Tools.AlgTools;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class AlgToolsFixture : IAlignmentProvider
    {
        internal const string PathId = ":Models/Test.roadx";
        internal readonly Alignment Alignment = new TestAlignment();
        internal readonly ToolsUnderTest Tools;
        internal string Name = "Test road";
        internal int ProviderCalls;
        internal Exception ProviderError;

        internal AlgToolsFixture()
        {
            Alignment.Description = "Road description";
            Alignment.Stationing.MakeDefault(10, 0, 10000);
            var container = new Container();
            container.RegisterSingleton<IAlignmentProvider>(() => this);
            Tools = new ToolsUnderTest { Container = container };
        }

        public (string name, Alignment alg) GetAlignment(string pathId)
        {
            ProviderCalls++;
            Test.Require(pathId == PathId, "Tool must pass the supplied road PathId to the provider.");
            if (ProviderError != null) throw ProviderError;
            return (Name, Alignment);
        }

        internal Construction AddConstruction(string name = "Construction", bool userDefined = false, params ActBaseComponent[] components)
        {
            var ast = new ActConstruction();
            foreach (var component in components) ast.Add(component);
            return Alignment.Corridor.Constructions.Add(name, userDefined, ast);
        }

        internal void AddSection(double station, Construction construction)
        {
            var sections = Alignment.Corridor.Sections;
            sections[sections.Add(station)].ConstructionId = construction.Id;
        }

        internal Dictionary<string, object> ConstructionArgs(Construction construction, string format = null)
        {
            var args = new Dictionary<string, object>
            {
                ["algPathId"] = PathId,
                ["constructionId"] = construction.Id.ToString(CultureInfo.InvariantCulture)
            };
            if (format != null) args["expressionFormat"] = format;
            return args;
        }

        internal static JObject Result(object response)
        {
            var json = JObject.FromObject(response);
            Test.Require(json["result"] is JObject, "Tool must return a result object.");
            return (JObject)json["result"];
        }

        internal static void CheckInvalidPath(Func<Dictionary<string, object>, object> call, string key)
        {
            Test.Throws<BadRequestException>(() => call(new Dictionary<string, object>()));
            Test.Throws<BadRequestException>(() => call(new Dictionary<string, object> { [key] = 42 }));
        }

        // Road data are real Robur models. Rendering styles are not used by AlgTools and
        // require global font/style services; omit only their initialization in this fixture.
        private sealed class TestAlignment : RoadAlignment
        {
            internal TestAlignment() : base(null) { }
            protected override AlignmentStyle CreateStyle() => null;
        }
    }
}
