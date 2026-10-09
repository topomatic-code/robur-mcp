using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Topomatic.Crs.Ast;
using Topomatic.ToolBridge.Exceptions;

namespace ToolBridge.Tests.Core.Tools.AlgTools
{
    internal sealed class GetConstructionTests
    {
        [Test]
        internal static void CheckSeparatedInclusiveRanges()
        {
            var f = new AlgToolsFixture();
            var target = f.AddConstruction("Target");
            var other = f.AddConstruction("Other");
            f.AddSection(0, target);
            f.AddSection(25, target);
            f.AddSection(50, other);
            f.AddSection(75, target);
            f.AddSection(100, other);
            f.AddSection(125, target);
            f.AddSection(150, target);
            var result = AlgToolsFixture.Result(f.Tools.GetConstruction(f.ConstructionArgs(target)));
            Test.Require((string)result["name"] == "Target", "Requested construction name.");
            var ranges = (JArray)result["sectionRanges"];
            Test.Require(ranges.Count == 3, "Other assignments must split ranges, including an isolated section.");
            CheckRange(ranges[0], 0, 25, "10+00.00", "10+25.00");
            CheckRange(ranges[1], 75, 75, "10+75.00", "10+75.00");
            CheckRange(ranges[2], 125, 150, "11+25.00", "11+50.00");
            Test.Require(f.ProviderCalls == 1, "Construction must be resolved through the injected provider.");
        }

        [Test]
        internal static void CheckUnusedAndSingleSection()
        {
            var f = new AlgToolsFixture();
            var target = f.AddConstruction("Unused");
            var other = f.AddConstruction();
            var args = f.ConstructionArgs(target);
            Test.Require(((JArray)AlgToolsFixture.Result(f.Tools.GetConstruction(args))["sectionRanges"]).Count == 0, "A road without sections has no ranges.");
            f.AddSection(100, other);
            Test.Require(((JArray)AlgToolsFixture.Result(f.Tools.GetConstruction(args))["sectionRanges"]).Count == 0, "An unused construction must return an empty range array.");
            f.Alignment.Corridor.Sections[0].ConstructionId = target.Id;
            var ranges = (JArray)AlgToolsFixture.Result(f.Tools.GetConstruction(args))["sectionRanges"];
            Test.Require(ranges.Count == 1, "A changed assignment must appear on the next call.");
            CheckRange(ranges[0], 100, 100, "11+00.00", "11+00.00");
        }

        [Test]
        internal static void CheckCompactDefaultAndAstIsolation()
        {
            var f = new AlgToolsFixture();
            var expression = Binary(AstOperator.GreaterThan, Name("P2"), Constant(0));
            var target = f.AddConstruction("Formats", false, new ActSimpleNode { Name = "Axis", Code = 257, X = expression, Y = Constant("P2") });
            var args = f.ConstructionArgs(target);
            var compact = AlgToolsFixture.Result(f.Tools.GetConstruction(args));
            var node = compact["components"][0];
            Test.Require((string)node["x"]["expr"] == "P2 > 0", "Default format must be compact.");
            Test.Require(node["y"].Type == JTokenType.String && (string)node["y"] == "P2", "String constants must not become variable expressions.");
            Test.Require((int)node["code"] == 257 && (string)node["name"] == "Axis", "Node metadata must be retained.");
            args["expressionFormat"] = "ast";
            node = AlgToolsFixture.Result(f.Tools.GetConstruction(args))["components"][0];
            Test.Require((string)node["x"]["type"] == "binary_expression" && (string)node["x"]["operator"] == "GreaterThan", "AST operator and node kind.");
            Test.Require((string)node["x"]["left"]["name"] == "P2" && (int)node["x"]["right"]["value"] == 0, "AST operands.");
            Test.Require((string)node["y"]["type"] == "constant_expression", "AST must also wrap constants.");
            args["expressionFormat"] = "compact";
            Test.Require(JToken.DeepEquals(compact, AlgToolsFixture.Result(f.Tools.GetConstruction(args))), "Changing format must not mutate the model or leak across requests.");
        }

        [Test]
        internal static void CheckOperatorPrecedence()
        {
            var samples = new[]
            {
                (Binary(AstOperator.Subtract, Binary(AstOperator.Subtract, Name("A"), Name("B")), Name("C")), "A - B - C"),
                (Binary(AstOperator.Subtract, Name("A"), Binary(AstOperator.Subtract, Name("B"), Name("C"))), "A - (B - C)"),
                (Binary(AstOperator.Multiply, Binary(AstOperator.Add, Name("A"), Name("B")), Name("C")), "(A + B) * C"),
                (Binary(AstOperator.Add, Name("A"), Binary(AstOperator.Multiply, Name("B"), Name("C"))), "A + B * C"),
                (Binary(AstOperator.Power, Binary(AstOperator.Power, Constant(2), Constant(3)), Constant(2)), "(2 ** 3) ** 2"),
                (Binary(AstOperator.Power, Constant(2), Binary(AstOperator.Power, Constant(3), Constant(2))), "2 ** 3 ** 2"),
                (Binary(AstOperator.Power, Constant(-2), Constant(2)), "(-2) ** 2"),
                (Binary(AstOperator.LessThan, Constant(1), Binary(AstOperator.LessThan, Constant(2), Constant(3))), "1 < (2 < 3)"),
                (Binary(AstOperator.LessThan, Binary(AstOperator.LessThan, Constant(1), Constant(2)), Constant(3)), "(1 < 2) < 3"),
                (Binary(AstOperator.Add, Constant(1e16), Binary(AstOperator.Add, Constant(-1e16), Constant(1.0))), "1E+16 + (-1E+16 + 1.0)")
            };
            var f = new AlgToolsFixture();
            foreach (var sample in samples)
            {
                var construction = f.AddConstruction("Expression", false, new ActSimpleNode { X = sample.Item1, Y = Constant(0) });
                var expression = AlgToolsFixture.Result(f.Tools.GetConstruction(f.ConstructionArgs(construction)))["components"][0]["x"]["expr"];
                Test.Require((string)expression == sample.Item2, "Precedence/associativity: expected " + sample.Item2 + ", got " + expression);
            }
        }

        [Test]
        internal static void CheckConstantsAndCulture()
        {
            var f = new AlgToolsFixture();
            var text = "quote\" slash\\ newline\nРусский";
            var target = f.AddConstruction("Constants", false,
                new ActSimpleNode { X = Constant(null), Y = Constant(true) },
                new ActSimpleNode { X = Constant(1.25), Y = Constant(text) },
                new ActSimpleNode { X = Binary(AstOperator.Add, Constant(1.25), Constant(2.5)), Y = Binary(AstOperator.Add, Constant(text), Constant("end")) });
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                var components = AlgToolsFixture.Result(f.Tools.GetConstruction(f.ConstructionArgs(target)))["components"];
                Test.Require(components[0]["x"].Type == JTokenType.Null && (bool)components[0]["y"], "Null and boolean constants must retain JSON types.");
                Test.Require((double)components[1]["x"] == 1.25 && (string)components[1]["y"] == text, "Numeric and string constants must retain their values.");
                Test.Require((string)components[2]["x"]["expr"] == "1.25 + 2.5", "Formula numbers must use invariant culture.");
                var formula = (string)components[2]["y"]["expr"];
                Test.RequireContains(formula, "\\\"", "\\\\", "\\n");
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Test]
        internal static void CheckUnsupportedExpressionFallsBackToCompleteAst()
        {
            var f = new AlgToolsFixture();
            var target = f.AddConstruction("Fallback", false, new ActSimpleNode
            {
                X = Binary(AstOperator.TrueDivide, Name("A"), Constant(2)),
                Y = new AstErrorExpression()
            });
            var args = f.ConstructionArgs(target, "compact");
            var compact = AlgToolsFixture.Result(f.Tools.GetConstruction(args))["components"][0];
            args["expressionFormat"] = "ast";
            var ast = AlgToolsFixture.Result(f.Tools.GetConstruction(args))["components"][0];
            Test.Require(JToken.DeepEquals(compact, ast), "Fallback must preserve the whole AST, including nested operands.");
            Test.Require((string)compact["x"]["left"]["type"] == "name_expression", "Fallback children must not be compacted.");
        }

        [Test]
        internal static void CheckNestedComponentsAndSemanticProjection()
        {
            var f = new AlgToolsFixture();
            var composite = new ActComponent { Name = "Lane" };
            composite.Semantic.Add(123, "Material");
            var nested = new ActConstruction { Name = "Nested" };
            nested.Semantics.Add(new ActConstructionSemantic { Code = 456, Category = "Not exported", Volumes = "Not exported" });
            var condition = new ActCondition { Name = "Sidewalk", Expression = Name("ENABLED") };
            condition.True.Add(composite);
            condition.True.Add(new ActSimpleNode { Name = "Edge", X = Name("WIDTH"), Y = Constant(0) });
            condition.False.Add(nested);
            var volume = new ActSimpleVolume { Name = "Volume", Code = 789, Contour = Name("Contour") };
            var target = f.AddConstruction("Nested", false, condition, volume);
            foreach (var format in new[] { "compact", "ast" })
            {
                var components = AlgToolsFixture.Result(f.Tools.GetConstruction(f.ConstructionArgs(target, format)))["components"];
                var lane = components[0]["true"]["components"][0];
                Test.Require((string)components[0]["true"]["components"][1]["name"] == "Edge", "Nested component hierarchy must be preserved.");
                var semantic = (JObject)lane["semantic"][0];
                Test.Require(semantic.Properties().Count() == 2 && (int)semantic["code"] == 123 && (string)semantic["name"] == "Material", "Component semantics must contain only code/name.");
                var constructionSemantic = (JObject)components[0]["false"]["components"][0]["semantics"][0];
                Test.Require(constructionSemantic.Properties().Count() == 1 && (int)constructionSemantic["code"] == 456, "Construction semantics must contain only code.");
                Test.Require(components[1]["semantic"] == null && components[1]["semanticEx"] == null && (int)components[1]["code"] == 789, "Volume semantic details must be omitted while its code is retained.");
            }
        }

        [Test]
        internal static void CheckInvalidArguments()
        {
            var f = new AlgToolsFixture();
            AlgToolsFixture.CheckInvalidPath(f.Tools.GetConstruction, "algPathId");
            Test.Throws<BadRequestException>(() => f.Tools.GetConstruction(new Dictionary<string, object> { ["expressionFormat"] = "unknown" }));
            Test.Require(f.ProviderCalls == 0, "Malformed format/path must be rejected before resolving a road.");
            foreach (var id in new object[] { null, 1, "", "-1", "4294967296", "1.5", "abc" })
                Test.Throws<BadRequestException>(() => f.Tools.GetConstruction(new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId, ["constructionId"] = id }));
            Test.Throws<BadRequestException>(() => f.Tools.GetConstruction(new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId }));
            Test.Throws<BadRequestException>(() => f.Tools.GetConstruction(new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId, ["constructionId"] = "4294967295" }));
        }

        [Test]
        internal static void CheckProviderErrorPropagation()
        {
            var f = new AlgToolsFixture();
            var error = new PreconditionFailedException("Road is unavailable");
            f.ProviderError = error;
            Test.Require(ReferenceEquals(error, Test.Throws<PreconditionFailedException>(() => f.Tools.GetConstruction(new Dictionary<string, object> { ["algPathId"] = AlgToolsFixture.PathId, ["constructionId"] = "1" }))), "Provider errors must propagate.");
        }

        private static AstNameExpression Name(string name) => new AstNameExpression { Name = name };
        private static AstConstantExpression Constant(object value) => new AstConstantExpression(value);
        private static AstBinaryExpression Binary(AstOperator op, AstExpression left, AstExpression right) => new AstBinaryExpression(op, left, right);

        private static void CheckRange(JToken range, double start, double end, string startPk, string endPk)
        {
            Test.Require((double)range["startStation"] == start && (double)range["endStation"] == end, "Range must use the first and last assigned sections, not the next construction boundary.");
            Test.Require((string)range["startPk"] == startPk && (string)range["endPk"] == endPk, "Picket labels must respect nonzero road stationing.");
        }
    }
}
