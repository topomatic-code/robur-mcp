using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Topomatic.Cad.Foundation.Stationing;
using Topomatic.Crs.Ast;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class AlgTools : ToolProvider
    {
        [ToolDef(
            Name = "alg_get_info",
            Domain = ToolDomains.Alg,
            Description = "Возвращает информацию о трассе.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'pathId': { 'type': 'string', 'description': 'PathId трассы (из project_get_active).' },
                'usedOnlyConstructions': {
                  'type': 'boolean',
                  'default': true,
                  'description': 'Вернуть только конструкции, назначенные хотя бы одному поперечнику. false — вернуть все конструкции.'
                }
              },
              'required': ['pathId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetAlignment(Dictionary<string, object> args)
        {
            var usedOnlyConstructions = JsonUtils.GetBool(args, "usedOnlyConstructions", true) ?? true;
            var pathId = JsonUtils.RequireString(args, "pathId");
            var alignmentProvider = Container.GetSingleton<IAlignmentProvider>();
            var (algName, alg) = alignmentProvider.GetAlignment(pathId);
            var sections = alg.Corridor.Sections;
            var usedConstructionIds = new HashSet<uint>();
            for (var i = 0; i < sections.Count; i++)
            {
                usedConstructionIds.Add(sections[i].ConstructionId);
            }

            var constructions = alg.Corridor.Constructions
                .Where(kvp => !usedOnlyConstructions || usedConstructionIds.Contains(kvp.Key))
                .Select(kvp => new
                {
                    id = Convert.ToString(kvp.Key, CultureInfo.InvariantCulture),
                    name = kvp.Value.Name ?? "none",
                    userDefined = kvp.Value.UserDefined,
                    isUsed = usedConstructionIds.Contains(kvp.Key)
                }).ToArray();

            return new
            {
                result = new
                {
                    name = algName ?? "none",
                    description = alg.Description ?? "none",
                    corridor = new
                    {
                        sectionsCount = alg.Corridor.Sections.Count,
                        constructionsCount = alg.Corridor.Constructions.Count(),
                        constructions
                    }
                },
                description = "Информация о трассе.",
                status = "Информация о трассе успешно получена."
            };
        }

        [ToolDef(
            Name = "alg_get_parameters",
            Domain = ToolDomains.Alg,
            Description = "Возвращает списки вычисляемых и табличных параметров трассы.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'algPathId': { 'type': 'string', 'description': 'PathId трассы (из project_get_active).' }
              },
              'required': ['algPathId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetParameters(Dictionary<string, object> args)
        {
            var algPathId = JsonUtils.RequireString(args, "algPathId");
            var alignmentProvider = Container.GetSingleton<IAlignmentProvider>();
            var (algName, alg) = alignmentProvider.GetAlignment(algPathId);

            var computedParameters = alg.Parameters.GetComputedParameters();
            var tableParameters = alg.Parameters.GetTableParameters();

            return new
            {
                result = new
                {
                    computedParameters = computedParameters.Select(kvp =>
                        new
                        {
                            name = kvp.Key,
                            caption = kvp.Value.Caption,
                            type = kvp.Value.ParameterType.ToString()
                        }
                    ).ToArray(),
                    tableParameters = tableParameters.Select(kvp =>
                        new
                        {
                            name = kvp.Key,
                            caption = kvp.Value.Caption,
                            type = kvp.Value.ParameterType.ToString()
                        }
                    ).ToArray()
                },
                description = "Параметры трассы.",
                status = "Параметры трассы успешно получены."
            };
        }

        [ToolDef(
            Name = "alg_get_construction",
            Domain = ToolDomains.Alg,
            Description = "Возвращает конструкцию коридора трассы.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'algPathId': { 'type': 'string', 'description': 'PathId трассы (из project_get_active).' },
                'constructionId': { 'type': 'string', 'description': 'Идентификатор конструкции (из corridor.constructions инструмента alg_get_info).' },
                'expressionFormat': {
                  'type': 'string',
                  'enum': ['compact', 'ast'],
                  'default': 'compact',
                  'description': 'compact: константы как JSON-значения, формулы как {expr: текст}; неподдерживаемые формулы сохраняются как полное AST. ast: исходное дерево AST.'
                }
              },
              'required': ['algPathId', 'constructionId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetConstruction(Dictionary<string, object> args)
        {
            var expressionFormat = JsonUtils.GetString(args, "expressionFormat", "compact");
            if (expressionFormat != "compact" && expressionFormat != "ast")
                throw new BadRequestException("expressionFormat должен быть compact или ast.");

            var algPathId = JsonUtils.RequireString(args, "algPathId");
            var alignmentProvider = Container.GetSingleton<IAlignmentProvider>();
            var (algName, alg) = alignmentProvider.GetAlignment(algPathId);

            var constructionIdStr = JsonUtils.RequireString(args, "constructionId");

            if (!uint.TryParse(constructionIdStr, NumberStyles.None, CultureInfo.InvariantCulture, out var constructionId))
                throw new BadRequestException("Идентификатор конструкции должен быть целым числом от 0 до 4294967295.");

            if (!alg.Corridor.Constructions.Contains(constructionId))
                throw new BadRequestException($"В коридоре трассы не содержится конструкция с Id: {constructionId}.");

            var construction = alg.Corridor.Constructions[constructionId];
            var sections = alg.Corridor.Sections;
            var sectionRanges = new List<object>();
            for (var i = 0; i < sections.Count; i++)
            {
                if (sections[i].ConstructionId != constructionId)
                    continue;

                var startStation = sections[i].Station;
                while (i + 1 < sections.Count && sections[i + 1].ConstructionId == constructionId)
                {
                    i++;
                }

                var endStation = sections[i].Station;
                sectionRanges.Add(
                    new
                    {
                        startStation,
                        endStation,
                        startPk = alg.Stationing.ToString(startStation),
                        endPk = alg.Stationing.ToString(endStation)
                    }
                );
            }

            return new
            {
                result = new
                {
                    name = !string.IsNullOrWhiteSpace(construction.Name) ? construction.Name : "none",
                    components = ParseComponents(construction.ActConstruction, expressionFormat == "compact"),
                    sectionRanges = sectionRanges.ToArray()
                },
                description = "Конструкция коридора трассы.",
                status = "Конструкция коридора трассы успешно получена."
            };
        }

        private object ParseComponent(ActBaseComponent component, bool compactExpressions)
        {
            if (component == null)
                return "none";

            var compObject = new Dictionary<string, object>
            {
                ["name"] = component.Name ?? "none",
                ["visible"] = component.Visible
            };

            if (component is ActNode node)
                compObject["code"] = node.Code;

            if (component is ActRay ray)
                compObject["bidirectional"] = ray.Bidirectional;

            if (component is ActContour contour)
            {
                compObject["code"] = contour.Code;
                compObject["isRedLinePart"] = contour.IsRedLinePart;
                compObject["isFilling"] = contour.IsFilling;
            }

            if (component is ActVolume volume)
            {
                compObject["code"] = volume.Code;
                compObject["mode"] = volume.Mode.ToString();
                compObject["factor"] = volume.Factor;
            }

            switch (component)
            {
                case ActSimpleNode simpleNode:
                    compObject["type"] = "simple_node";
                    compObject["x"] = ParseExpression(simpleNode.X, compactExpressions);
                    compObject["y"] = ParseExpression(simpleNode.Y, compactExpressions);
                    break;
                case ActRelativeNode relativeNode:
                    compObject["type"] = "relative_node";
                    compObject["node"] = ParseExpression(relativeNode.Node, compactExpressions);
                    compObject["x"] = ParseExpression(relativeNode.X, compactExpressions);
                    compObject["y"] = ParseExpression(relativeNode.Y, compactExpressions);
                    break;
                case ActTwoRayNode twoRayNode:
                    compObject["type"] = "two_ray_node";
                    compObject["ray1"] = ParseExpression(twoRayNode.Ray1, compactExpressions);
                    compObject["ray2"] = ParseExpression(twoRayNode.Ray2, compactExpressions);
                    break;
                case ActRayContainerNode containerNode:
                    compObject["type"] = "ray_container_node";
                    compObject["ray"] = ParseExpression(containerNode.Ray, compactExpressions);
                    compObject["container"] = ParseExpression(containerNode.Container, compactExpressions);
                    compObject["index"] = containerNode.Index;
                    break;
                case ActRayContourNode contourNode:
                    compObject["type"] = "ray_contour_node";
                    compObject["ray"] = ParseExpression(contourNode.Ray, compactExpressions);
                    compObject["contour"] = ParseExpression(contourNode.Contour, compactExpressions);
                    compObject["index"] = contourNode.Index;
                    break;
                case ActSimpleRay simpleRay:
                    compObject["type"] = "simple_ray";
                    compObject["node"] = ParseExpression(simpleRay.Node, compactExpressions);
                    compObject["x"] = ParseExpression(simpleRay.X, compactExpressions);
                    compObject["y"] = ParseExpression(simpleRay.Y, compactExpressions);
                    break;
                case ActTwoNodeRay twoNodeRay:
                    compObject["type"] = "two_node_ray";
                    compObject["node1"] = ParseExpression(twoNodeRay.Node1, compactExpressions);
                    compObject["node2"] = ParseExpression(twoNodeRay.Node2, compactExpressions);
                    break;
                case ActSimpleContour simpleContour:
                    compObject["type"] = "simple_contour";
                    compObject["nodes"] = simpleContour.Nodes?.Select(item => ParseExpression(item, compactExpressions)).ToArray() ?? new object[0];
                    break;
                case ActSegmentContour segmentContour:
                    compObject["type"] = "segment_contour";
                    compObject["contour"] = ParseExpression(segmentContour.Contour, compactExpressions);
                    compObject["node1"] = ParseExpression(segmentContour.Node1, compactExpressions);
                    compObject["node2"] = ParseExpression(segmentContour.Node2, compactExpressions);
                    break;
                case ActUnionContour unionContour:
                    compObject["type"] = "union_contour";
                    compObject["contours"] = unionContour.Contours?.Select(item => ParseExpression(item, compactExpressions)).ToArray() ?? new object[0];
                    break;
                case ActSimpleVolume simpleVolume:
                    compObject["type"] = "simple_volume";
                    compObject["contour"] = ParseExpression(simpleVolume.Contour, compactExpressions);
                    break;
                case ActSegmentVolume segmentVolume:
                    compObject["type"] = "segment_volume";
                    compObject["contour"] = ParseExpression(segmentVolume.Contour, compactExpressions);
                    compObject["node1"] = ParseExpression(segmentVolume.Node1, compactExpressions);
                    compObject["node2"] = ParseExpression(segmentVolume.Node2, compactExpressions);
                    break;
                case ActSectVolume sectVolume:
                    compObject["type"] = "sect_volume";
                    compObject["contour1"] = ParseExpression(sectVolume.Contour1, compactExpressions);
                    compObject["contour2"] = ParseExpression(sectVolume.Contour2, compactExpressions);
                    compObject["firstUp"] = sectVolume.FirstUp;
                    break;
                case ActCondition condition:
                    compObject["type"] = "condition";
                    compObject["expression"] = ParseExpression(condition.Expression, compactExpressions);
                    compObject["true"] = ParseComponent(condition.True, compactExpressions);
                    compObject["false"] = ParseComponent(condition.False, compactExpressions);
                    break;
                case ActSwitch switchComponent:
                    compObject["type"] = "switch";
                    compObject["expression"] = ParseExpression(switchComponent.Expression, compactExpressions);
                    compObject["components"] = ParseComponents(switchComponent, compactExpressions);
                    break;
                case ActComponent composite:
                    compObject["type"] = "component";
                    compObject["componentType"] = composite.Type.ToString();
                    compObject["typeName"] = composite.TypeName ?? "none";
                    compObject["allowedComponentTypes"] = composite.AllowedComponentTypes ?? "none";
                    compObject["properties"] = ParseProperties(composite.Properties, compactExpressions);
                    compObject["semantic"] = ParseSemantics(composite.Semantic);
                    compObject["components"] = ParseComponents(composite, compactExpressions);
                    break;
                case ActConstruction construction:
                    compObject["type"] = "construction";
                    compObject["properties"] = ParseConstructionProperties(construction.Properties);
                    compObject["semantics"] = ParseConstructionSemantics(construction.Semantics);
                    compObject["components"] = ParseComponents(construction, compactExpressions);
                    break;
                case ActSequence sequence:
                    compObject["type"] = "sequence";
                    compObject["components"] = ParseComponents(sequence, compactExpressions);
                    break;
                case ActReport report:
                    compObject["type"] = "report";
                    compObject["message"] = report.Message ?? "none";
                    break;
                default:
                    throw new NotSupportedException($"Неизвестный тип компонента: {component.GetType().FullName}.");
            }
            return compObject;
        }

        private object[] ParseComponents(IEnumerable<ActBaseComponent> components, bool compactExpressions) =>
            components?.Select(item => ParseComponent(item, compactExpressions)).ToArray() ?? new object[0];

        private object[] ParseSemantics(SemanticList semantics)
        {
            return semantics == null ? new object[0] : Enumerable.Range(0, semantics.Count)
                .Select(i => (object)new
                {
                    code = semantics.GetCode(i),
                    name = semantics.GetName(i) ?? "none"
                }).ToArray();
        }

        private object[] ParseConstructionProperties(ActConstructionProperties properties)
        {
            return properties?.Select(p => (object)new
            {
                name = p.Name ?? "none",
                type = p.Type.ToString(),
                smdxType = p.SmdxType ?? "none",
                defaultValue = p.DefaultValue ?? "none",
                displayName = p.DisplayName ?? "none",
                category = p.Category ?? "none",
                description = p.Description ?? "none"
            }).ToArray() ?? new object[0];
        }

        private object[] ParseConstructionSemantics(ActConstructionSemantics semantics)
        {
            return semantics?.Select(s => (object)new
            {
                code = s.Code
            }).ToArray() ?? new object[0];
        }

        private object[] ParseProperties(PropertyList properties, bool compactExpressions)
        {
            return properties == null ? new object[0] : Enumerable.Range(0, properties.Count)
                .Select(i => (object)new { name = properties[i].Name ?? "none", value = ParseExpression(properties[i].Value, compactExpressions) })
                .ToArray();
        }

        private object ParseExpression(AstExpression expression, bool compactExpressions)
        {
            if (expression == null)
                return "none";

            if (compactExpressions)
            {
                if (expression is AstConstantExpression constant && IsJsonConstant(constant.Value))
                    return constant.Value;

                if (TryFormatExpression(expression, out var text))
                    return new { expr = text };

                // Keep the complete subtree if text cannot represent it faithfully.
                return ParseExpression(expression, false);
            }

            var result = new Dictionary<string, object>();
            switch (expression)
            {
                case AstAndExpression andExpression:
                    result["type"] = "and_expression";
                    result["left"] = ParseExpression(andExpression.Left, compactExpressions);
                    result["right"] = ParseExpression(andExpression.Right, compactExpressions);
                    break;
                case AstArg arg:
                    result["type"] = "arg";
                    result["name"] = arg.Name ?? "none";
                    result["expression"] = ParseExpression(arg.Expression, compactExpressions);
                    break;
                case AstBackQuoteExpression backQuoteExpression:
                    result["type"] = "back_quote_expression";
                    result["expression"] = ParseExpression(backQuoteExpression.Expression, compactExpressions);
                    break;
                case AstBinaryExpression binaryExpression:
                    result["type"] = "binary_expression";
                    result["left"] = ParseExpression(binaryExpression.Left, compactExpressions);
                    result["right"] = ParseExpression(binaryExpression.Right, compactExpressions);
                    result["operator"] = binaryExpression.Operator.ToString();
                    break;
                case AstCallExpression callExpression:
                    result["type"] = "call_expression";
                    result["target"] = ParseExpression(callExpression.Target, compactExpressions);
                    result["args"] = ParseAstItems(callExpression.Args, compactExpressions);
                    break;
                case AstConditionalExpression conditionalExpression:
                    result["type"] = "conditional_expression";
                    result["falseExpression"] = ParseExpression(conditionalExpression.FalseExpression, compactExpressions);
                    result["test"] = ParseExpression(conditionalExpression.Test, compactExpressions);
                    result["trueExpression"] = ParseExpression(conditionalExpression.TrueExpression, compactExpressions);
                    break;
                case AstConstantExpression constantExpression:
                    result["type"] = "constant_expression";
                    result["value"] = constantExpression.Value ?? "none";
                    break;
                case AstDictionaryExpression dictionaryExpression:
                    result["type"] = "dictionary_expression";
                    result["expressions"] = ParseAstItems(dictionaryExpression.Expressions, compactExpressions);
                    break;
                case AstErrorExpression errorExpression:
                    result["type"] = "error_expression";
                    break;
                case AstGeneratorExpression generatorExpression:
                    result["type"] = "generator_expression";
                    result["function"] = ParseAstObject(generatorExpression.Function, compactExpressions);
                    result["iterable"] = ParseExpression(generatorExpression.Iterable, compactExpressions);
                    break;
                case AstIndexExpression indexExpression:
                    result["type"] = "index_expression";
                    result["target"] = ParseExpression(indexExpression.Target, compactExpressions);
                    result["index"] = ParseExpression(indexExpression.Index, compactExpressions);
                    break;
                case AstLambdaExpression lambdaExpression:
                    result["type"] = "lambda_expression";
                    result["function"] = ParseAstObject(lambdaExpression.Function, compactExpressions);
                    break;
                case AstListExpression listExpression:
                    result["type"] = "list_expression";
                    result["expressions"] = ParseAstItems(listExpression.Expressions, compactExpressions);
                    break;
                case AstMemberExpression memberExpression:
                    result["type"] = "member_expression";
                    result["target"] = ParseExpression(memberExpression.Target, compactExpressions);
                    result["name"] = memberExpression.Name ?? "none";
                    break;
                case AstNameExpression nameExpression:
                    result["type"] = "name_expression";
                    result["name"] = nameExpression.Name ?? "none";
                    break;
                case AstOrExpression orExpression:
                    result["type"] = "or_expression";
                    result["left"] = ParseExpression(orExpression.Left, compactExpressions);
                    result["right"] = ParseExpression(orExpression.Right, compactExpressions);
                    break;
                case AstParenthesisExpression parenthesisExpression:
                    result["type"] = "parenthesis_expression";
                    result["expression"] = ParseExpression(parenthesisExpression.Expression, compactExpressions);
                    break;
                case AstSliceExpression sliceExpression:
                    result["type"] = "slice_expression";
                    result["sliceStart"] = ParseExpression(sliceExpression.SliceStart, compactExpressions);
                    result["sliceStop"] = ParseExpression(sliceExpression.SliceStop, compactExpressions);
                    result["sliceStep"] = ParseExpression(sliceExpression.SliceStep, compactExpressions);
                    result["stepProvided"] = sliceExpression.StepProvided;
                    break;
                case AstTupleExpression tupleExpression:
                    result["type"] = "tuple_expression";
                    result["expressions"] = ParseAstItems(tupleExpression.Expressions, compactExpressions);
                    break;
                case AstUnaryExpression unaryExpression:
                    result["type"] = "unary_expression";
                    result["expression"] = ParseExpression(unaryExpression.Expression, compactExpressions);
                    result["operator"] = unaryExpression.Operator.ToString();
                    break;
                case AstYieldExpression yieldExpression:
                    result["type"] = "yield_expression";
                    result["expression"] = ParseExpression(yieldExpression.Expression, compactExpressions);
                    break;
                default:
                    throw new NotSupportedException($"Неизвестный тип выражения: {expression.GetType().FullName}.");
            }
            return result;
        }

        private object ParseAstObject(AstObject ast, bool compactExpressions)
        {
            if (ast == null)
                return "none";

            if (ast is ActBaseComponent component)
                return ParseComponent(component, compactExpressions);

            if (ast is AstExpression expression)
                return ParseExpression(expression, compactExpressions);

            if (ast is AstStatement statement)
                return ParseStatement(statement, compactExpressions);

            if (ast is AstParameter parameter)
                return ParseParameter(parameter, compactExpressions);

            var result = new Dictionary<string, object>();
            switch (ast)
            {
                case AstDottedName dottedName:
                    result["type"] = "dotted_name";
                    result["names"] = ParseNames(dottedName.Names);
                    break;
                case AstIfStatementTest ifStatementTest:
                    result["type"] = "if_statement_test";
                    result["test"] = ParseExpression(ifStatementTest.Test, compactExpressions);
                    result["body"] = ParseAstItems(ifStatementTest.Body, compactExpressions);
                    break;
                case AstListComprehension listComprehension:
                    result["type"] = "list_comprehension";
                    result["item"] = ParseExpression(listComprehension.Item, compactExpressions);
                    result["iterators"] = ParseAstItems(listComprehension.Iterators, compactExpressions);
                    break;
                case AstListComprehensionFor listComprehensionFor:
                    result["type"] = "list_comprehension_for";
                    result["left"] = ParseExpression(listComprehensionFor.Left, compactExpressions);
                    result["list"] = ParseExpression(listComprehensionFor.List, compactExpressions);
                    break;
                case AstListComprehensionIf listComprehensionIf:
                    result["type"] = "list_comprehension_if";
                    result["test"] = ParseExpression(listComprehensionIf.Test, compactExpressions);
                    break;
                case AstTryStatementHandler tryStatementHandler:
                    result["type"] = "try_statement_handler";
                    result["test"] = ParseExpression(tryStatementHandler.Test, compactExpressions);
                    result["target"] = ParseExpression(tryStatementHandler.Target, compactExpressions);
                    result["body"] = ParseAstItems(tryStatementHandler.Body, compactExpressions);
                    break;
                default:
                    throw new NotSupportedException($"Неизвестный тип узла AST: {ast.GetType().FullName}.");
            }
            return result;
        }

        private object ParseStatement(AstStatement statement, bool compactExpressions)
        {
            if (statement == null)
                return "none";

            if (statement is ActBaseComponent component)
                return ParseComponent(component, compactExpressions);

            var result = new Dictionary<string, object>
            {
                ["documentation"] = statement.Documentation ?? "none"
            };

            switch (statement)
            {
                case AstFunctionDefinition functionDefinition:
                    result["type"] = "function_definition";
                    result["name"] = functionDefinition.Name ?? "none";
                    result["isLambda"] = functionDefinition.IsLambda;
                    result["parameters"] = ParseAstItems(functionDefinition.Parameters, compactExpressions);
                    result["body"] = ParseAstItems(functionDefinition.Body, compactExpressions);
                    result["decorators"] = ParseAstItems(functionDefinition.Decorators, compactExpressions);
                    result["isGenerator"] = functionDefinition.IsGenerator;
                    break;
                case AstClassDefinition classDefinition:
                    result["type"] = "class_definition";
                    result["name"] = classDefinition.Name ?? "none";
                    result["bases"] = ParseAstItems(classDefinition.Bases, compactExpressions);
                    result["body"] = ParseAstItems(classDefinition.Body, compactExpressions);
                    result["decorators"] = ParseAstItems(classDefinition.Decorators, compactExpressions);
                    break;
                case AstAssertStatement assertStatement:
                    result["type"] = "assert_statement";
                    result["test"] = ParseExpression(assertStatement.Test, compactExpressions);
                    result["message"] = ParseExpression(assertStatement.Message, compactExpressions);
                    break;
                case AstAssignmentStatement assignmentStatement:
                    result["type"] = "assignment_statement";
                    result["left"] = ParseAstItems(assignmentStatement.Left, compactExpressions);
                    result["right"] = ParseExpression(assignmentStatement.Right, compactExpressions);
                    break;
                case AstAugmentedAssignStatement augmentedAssignStatement:
                    result["type"] = "augmented_assign_statement";
                    result["operator"] = augmentedAssignStatement.Operator.ToString();
                    result["left"] = ParseExpression(augmentedAssignStatement.Left, compactExpressions);
                    result["right"] = ParseExpression(augmentedAssignStatement.Right, compactExpressions);
                    break;
                case AstBreakStatement breakStatement:
                    result["type"] = "break_statement";
                    break;
                case AstCommentStatement commentStatement:
                    result["type"] = "comment_statement";
                    result["comment"] = commentStatement.Comment ?? "none";
                    break;
                case AstContinueStatement continueStatement:
                    result["type"] = "continue_statement";
                    break;
                case AstDelStatement delStatement:
                    result["type"] = "del_statement";
                    result["expressions"] = ParseAstItems(delStatement.Expressions, compactExpressions);
                    break;
                case AstExecStatement execStatement:
                    result["type"] = "exec_statement";
                    result["code"] = ParseExpression(execStatement.Code, compactExpressions);
                    result["locals"] = ParseExpression(execStatement.Locals, compactExpressions);
                    result["globals"] = ParseExpression(execStatement.Globals, compactExpressions);
                    break;
                case AstExpressionStatement expressionStatement:
                    result["type"] = "expression_statement";
                    result["expression"] = ParseExpression(expressionStatement.Expression, compactExpressions);
                    break;
                case AstForStatement forStatement:
                    result["type"] = "for_statement";
                    result["left"] = ParseExpression(forStatement.Left, compactExpressions);
                    result["body"] = ParseAstItems(forStatement.Body, compactExpressions);
                    result["list"] = ParseExpression(forStatement.List, compactExpressions);
                    result["else"] = ParseAstItems(forStatement.Else, compactExpressions);
                    break;
                case AstFromImportStatement fromImportStatement:
                    result["type"] = "from_import_statement";
                    result["root"] = ParseAstObject(fromImportStatement.Root, compactExpressions);
                    result["names"] = ParseNames(fromImportStatement.Names);
                    result["asNames"] = ParseNames(fromImportStatement.AsNames);
                    break;
                case AstGlobalStatement globalStatement:
                    result["type"] = "global_statement";
                    result["names"] = ParseNames(globalStatement.Names);
                    break;
                case AstIfStatement ifStatement:
                    result["type"] = "if_statement";
                    result["tests"] = ParseAstItems(ifStatement.Tests, compactExpressions);
                    result["elseStatement"] = ParseAstItems(ifStatement.ElseStatement, compactExpressions);
                    break;
                case AstImportStatement importStatement:
                    result["type"] = "import_statement";
                    result["names"] = ParseAstItems(importStatement.Names, compactExpressions);
                    result["asNames"] = ParseNames(importStatement.AsNames);
                    break;
                case AstPrintStatement printStatement:
                    result["type"] = "print_statement";
                    result["destination"] = ParseExpression(printStatement.Destination, compactExpressions);
                    result["expressions"] = ParseAstItems(printStatement.Expressions, compactExpressions);
                    result["trailingComma"] = printStatement.TrailingComma;
                    break;
                case AstRaiseStatement raiseStatement:
                    result["type"] = "raise_statement";
                    result["valueType"] = ParseExpression(raiseStatement.Type, compactExpressions);
                    break;
                case AstReturnStatement returnStatement:
                    result["type"] = "return_statement";
                    result["expression"] = ParseExpression(returnStatement.Expression, compactExpressions);
                    break;
                case AstTryStatement tryStatement:
                    result["type"] = "try_statement";
                    result["body"] = ParseAstItems(tryStatement.Body, compactExpressions);
                    result["else"] = ParseAstItems(tryStatement.Else, compactExpressions);
                    result["finally"] = ParseAstItems(tryStatement.Finally, compactExpressions);
                    result["handlers"] = ParseAstItems(tryStatement.Handlers, compactExpressions);
                    break;
                case AstUnit unit:
                    result["type"] = "unit";
                    result["body"] = ParseAstItems(unit.Body, compactExpressions);
                    break;
                case AstWhileStatement whileStatement:
                    result["type"] = "while_statement";
                    result["test"] = ParseExpression(whileStatement.Test, compactExpressions);
                    result["body"] = ParseAstItems(whileStatement.Body, compactExpressions);
                    result["elseStatement"] = ParseAstItems(whileStatement.ElseStatement, compactExpressions);
                    break;
                case AstWithStatement withStatement:
                    result["type"] = "with_statement";
                    result["variable"] = ParseExpression(withStatement.Variable, compactExpressions);
                    result["contextManager"] = ParseExpression(withStatement.ContextManager, compactExpressions);
                    result["body"] = ParseAstItems(withStatement.Body, compactExpressions);
                    break;
                default:
                    throw new NotSupportedException($"Неизвестный тип инструкции: {statement.GetType().FullName}.");
            }
            return result;
        }

        private object ParseParameter(AstParameter parameter, bool compactExpressions)
        {
            if (parameter == null)
                return "none";

            var result = new Dictionary<string, object>
            {
                ["name"] = parameter.Name ?? "none",
                ["defaultValue"] = ParseExpression(parameter.DefaultValue, compactExpressions),
                ["isList"] = parameter.IsList,
                ["isDictionary"] = parameter.IsDictionary,
                ["kind"] = parameter.Kind.ToString()
            };

            if (parameter is AstSublistParameter sublistParameter)
            {
                result["type"] = "sublist_parameter";
                result["tuple"] = ParseExpression(sublistParameter.Tuple, compactExpressions);
            }
            else
            {
                result["type"] = "parameter";
            }

            return result;
        }

        private object[] ParseAstItems(IEnumerable<AstObject> items, bool compactExpressions) => items?.Select(item => ParseAstObject(item, compactExpressions)).ToArray() ?? new object[0];
        private string[] ParseNames(IEnumerable<string> names) => names?.Select(name => name ?? "none").ToArray() ?? new string[0];

        private static bool IsJsonConstant(object value)
        {
            return value == null || value is string || value is bool ||
                value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is decimal ||
                (value is double d && !double.IsNaN(d) && !double.IsInfinity(d)) ||
                (value is float f && !float.IsNaN(f) && !float.IsInfinity(f));
        }

        private static bool TryFormatExpression(AstExpression expression, out string text)
        {
            text = null;
            switch (expression)
            {
                case AstNameExpression name when !string.IsNullOrEmpty(name.Name):
                    text = name.Name;
                    return true;
                case AstConstantExpression constant when IsJsonConstant(constant.Value):
                    text = constant.Value == null ? "None" :
                        constant.Value is bool b ? (b ? "True" : "False") :
                        JsonConvert.SerializeObject(constant.Value);
                    return true;
                case AstParenthesisExpression parentheses:
                    return TryFormatExpression(parentheses.Expression, out text);
                case AstBinaryExpression binary:
                    var op = BinaryOperator(binary.Operator);

                    if (op == null)
                        return false;

                    return TryBinary(binary.Left, op, binary.Right, out text);
                case AstAndExpression and:
                    return TryBinary(and.Left, "and", and.Right, out text);
                case AstOrExpression or:
                    return TryBinary(or.Left, "or", or.Right, out text);
                case AstUnaryExpression unary:
                    string unaryOp;
                    switch (unary.Operator)
                    {
                        case AstOperator.Not: unaryOp = "not "; break;
                        case AstOperator.Pos: unaryOp = "+"; break;
                        case AstOperator.Negate: unaryOp = "-"; break;
                        case AstOperator.Invert: unaryOp = "~"; break;
                        default: return false;
                    }

                    if (!TryFormatOperand(unary.Expression, unary.Operator == AstOperator.Not ? 4 : 12, false, out var operand))
                        return false;

                    text = unaryOp + operand;
                    return true;
                case AstConditionalExpression conditional:
                    if (!TryFormatOperand(conditional.Test, 2, false, out var test) ||
                        !TryFormatOperand(conditional.TrueExpression, 2, false, out var yes) ||
                        !TryFormatOperand(conditional.FalseExpression, 1, false, out var no))
                    {
                        return false;
                    }

                    text = yes + " if " + test + " else " + no;
                    return true;
                case AstMemberExpression member when !string.IsNullOrEmpty(member.Name):
                    if (!TryFormatOperand(member.Target, 14, false, out var memberTarget))
                        return false;

                    // A numeric literal followed directly by a dot can be parsed as a float.
                    if (UnwrapParentheses(member.Target) is AstConstantExpression literal &&
                        literal.Value != null && !(literal.Value is string) && !(literal.Value is bool) &&
                        ExpressionPrecedence(member.Target) >= 14)
                    {
                        memberTarget = "(" + memberTarget + ")";
                    }

                    text = memberTarget + "." + member.Name;
                    return true;
                case AstIndexExpression index:
                    if (!TryFormatOperand(index.Target, 14, false, out var indexTarget))
                        return false;

                    string indexText;
                    if (index.Index is AstSliceExpression slice)
                    {
                        if (!TrySlice(slice, out indexText))
                            return false;
                    }
                    else if (!TryFormatExpression(index.Index, out indexText))
                    {
                        return false;
                    }

                    text = indexTarget + "[" + indexText + "]";
                    return true;
                case AstCallExpression call:
                    if (!TryFormatOperand(call.Target, 14, false, out var target))
                        return false;

                    var args = new List<string>();

                    if (call.Args == null)
                        return false;

                    foreach (var arg in call.Args)
                    {
                        if (!TryFormatExpression(arg.Expression, out var value))
                            return false;

                        if (string.IsNullOrEmpty(arg.Name))
                            args.Add(value);
                        else if (arg.Name == "*" || arg.Name == "**")
                            args.Add(arg.Name + value);
                        else
                            args.Add(arg.Name + "=" + value);
                    }

                    text = target + "(" + string.Join(", ", args) + ")";
                    return true;
                case AstListExpression list:
                    if (!TryItems(list.Expressions, out var listItems))
                        return false;

                    text = "[" + string.Join(", ", listItems) + "]";
                    return true;
                case AstTupleExpression tuple:
                    if (!TryItems(tuple.Expressions, out var tupleItems))
                        return false;

                    text = "(" + string.Join(", ", tupleItems) + (tupleItems.Count == 1 ? "," : "") + ")";
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryItems(IEnumerable<AstObject> items, out List<string> texts)
        {
            texts = new List<string>();

            if (items == null)
                return false;

            foreach (var item in items)
            {
                if (!(item is AstExpression expression) || !TryFormatExpression(expression, out var text))
                    return false;

                texts.Add(text);
            }
            return true;
        }

        private static bool TrySlice(AstSliceExpression slice, out string text)
        {
            text = null;
            var start = "";
            var stop = "";
            var step = "";

            if (slice.SliceStart != null && !TryFormatExpression(slice.SliceStart, out start))
                return false;

            if (slice.SliceStop != null && !TryFormatExpression(slice.SliceStop, out stop))
                return false;

            if (!slice.StepProvided && slice.SliceStep != null)
                return false;

            if (slice.SliceStep != null && !TryFormatExpression(slice.SliceStep, out step))
                return false;

            text = start + ":" + stop + (slice.StepProvided ? ":" + step : "");
            return true;
        }

        private static bool TryBinary(AstExpression left, string op, AstExpression right, out string text)
        {
            text = null;

            var precedence = BinaryPrecedence(op);
            // Comparisons must not turn into Python comparison chains. Power is right-associative
            // and accepts unary operators on its right; other binary operations associate left.
            if (!TryFormatOperand(left, precedence, op == "**" || precedence == 5, out var lhs) ||
                !TryFormatOperand(right, op == "**" ? 12 : precedence, op != "**", out var rhs))
                return false;

            text = lhs + " " + op + " " + rhs;
            return true;
        }

        private static AstExpression UnwrapParentheses(AstExpression expression)
        {
            while (expression is AstParenthesisExpression parentheses)
                expression = parentheses.Expression;

            return expression;
        }

        private static bool TryFormatOperand(AstExpression expression, int parentPrecedence, bool wrapEqual, out string text)
        {
            if (!TryFormatExpression(expression, out text))
                return false;

            var precedence = ExpressionPrecedence(expression);
            if (precedence < parentPrecedence || (wrapEqual && precedence == parentPrecedence))
                text = "(" + text + ")";

            return true;
        }

        private static int ExpressionPrecedence(AstExpression expression)
        {
            switch (UnwrapParentheses(expression))
            {
                case AstConditionalExpression _: return 1;
                case AstOrExpression _: return 2;
                case AstAndExpression _: return 3;
                case AstUnaryExpression unary: return unary.Operator == AstOperator.Not ? 4 : 12;
                case AstBinaryExpression binary: return BinaryPrecedence(BinaryOperator(binary.Operator));
                case AstCallExpression _:
                case AstIndexExpression _:
                case AstMemberExpression _: return 14;
                case AstConstantExpression constant:
                    // Negative constants have the same binding rules as unary minus, notably for **.
                    return IsJsonConstant(constant.Value) &&
                        JsonConvert.SerializeObject(constant.Value).StartsWith("-", StringComparison.Ordinal) ? 12 : 15;
                default: return 15;
            }
        }

        private static int BinaryPrecedence(string op)
        {
            switch (op)
            {
                case "or": return 2;
                case "and": return 3;
                case "<":
                case "<=":
                case ">":
                case ">=":
                case "==":
                case "!=":
                case "in":
                case "not in":
                case "is":
                case "is not": return 5;
                case "|": return 6;
                case "^": return 7;
                case "&": return 8;
                case "<<": case ">>": return 9;
                case "+": case "-": return 10;
                case "*": case "/": case "//": case "%": return 11;
                case "**": return 13;
                default: return 0;
            }
        }

        private static string BinaryOperator(AstOperator op)
        {
            switch (op)
            {
                case AstOperator.Add: return "+";
                case AstOperator.Subtract: return "-";
                case AstOperator.Multiply: return "*";
                case AstOperator.Divide: return "/";
                case AstOperator.FloorDivide: return "//";
                case AstOperator.Mod: return "%";
                case AstOperator.Power: return "**";
                case AstOperator.BitwiseAnd: return "&";
                case AstOperator.BitwiseOr: return "|";
                case AstOperator.Xor: return "^";
                case AstOperator.LeftShift: return "<<";
                case AstOperator.RightShift: return ">>";
                case AstOperator.LessThan: return "<";
                case AstOperator.LessThanOrEqual: return "<=";
                case AstOperator.GreaterThan: return ">";
                case AstOperator.GreaterThanOrEqual: return ">=";
                case AstOperator.Equal: return "==";
                case AstOperator.NotEqual: return "!=";
                case AstOperator.In: return "in";
                case AstOperator.NotIn: return "not in";
                case AstOperator.Is: return "is";
                case AstOperator.IsNot: return "is not";
                default: return null;
            }
        }
    }
}
