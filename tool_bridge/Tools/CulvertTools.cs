using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Culverts;
using Topomatic.Culverts.Sheets;
using Topomatic.Culverts.Specifications;
using Topomatic.FoundationClasses;
using Topomatic.Tables.Sheets;
using Topomatic.ToolBridge.Services;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class CulvertTools : ToolProvider
    {
        [ToolDef(
            Name = "clv_get_parameters",
            Description = "[ТРУБЫ] Возвращает параметры конструкции водопропускной трубы. В структуре активного проекта элемент должен являться трубой (type == culvert).",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'uri': { 'type': 'string', 'description': 'Полный глобальный uri модели водопропускной трубы (из структуры активного проекта).' }
              },
              'required': ['uri'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetParameters(Dictionary<string, object> args)
        {
            var uriStr = JsonUtils.RequireString(args, "uri");
            var uri = new URI(uriStr);
            var culvertNode = ProjectManager.Instance.GetNode(uri) ??
                throw new InvalidOperationException($"Не удалось найти водопропускную трубу в структуре проекта по указанному uri {uriStr}. Проверьте правильность переданного uri.");
            var culvertModel = culvertNode.Model ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var culvertContainer = PluginCoreOps.LockReadContainer<ICulvertContainer>(culvertModel) ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var culvert = culvertContainer.Culvert ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var sheetContext = culvert.SheetContext;
            var variables = new Dictionary<string, List<VariableInfo>>();
            var tables = new List<string>();
            for (int i = 0; i < sheetContext.VariablesCount; i++)
            {
                var variable = sheetContext.GetVariable(i);
                if (!variables.TryGetValue(variable.TableId, out List<VariableInfo> tableVariables))
                {
                    tableVariables = new List<VariableInfo>();
                    variables.Add(variable.TableId, tableVariables);
                    tables.Add(variable.TableId);
                }
                tableVariables.Add(variable);
            }
            var parameterGroups = new List<object>();
            for (int i = 0; i < tables.Count; i++)
            {
                var tableId = tables[i];
                var tableVariables = variables[tableId];
                var parameters = new List<object>();
                for (int j = 0; j < tableVariables.Count; j++)
                {
                    var variable = tableVariables[j];
                    var value = sheetContext.GetValue(variable.Value);
                    parameters.Add(new { name = variable.Name, description = variable.Description, value });
                }
                parameterGroups.Add(new { groupName = tableId, parameters = parameters.ToArray() });
            }
            return new
            {
                result = new
                {
                    culvertName = culvertNode.Name,
                    parameterGroups = parameterGroups.ToArray()
                },
                description = "Параметры водопропускной трубы.",
                status = "Данные успешно получены."
            };
        }

        [ToolDef(
            Name = "clv_get_volumes",
            Description = "[ТРУБЫ] Возвращает объемы работ по водопропускной трубе. В структуре активного проекта элемент должен являться трубой (type == culvert).",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'uri': { 'type': 'string', 'description': 'Полный глобальный uri модели водопропускной трубы (из структуры активного проекта).' }
              },
              'required': ['uri'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetVolumes(Dictionary<string, object> args)
        {
            var uriStr = JsonUtils.RequireString(args, "uri");
            var uri = new URI(uriStr);
            var culvertNode = ProjectManager.Instance.GetNode(uri) ??
                throw new InvalidOperationException($"Не удалось найти водопропускную трубу в структуре проекта по указанному uri {uriStr}. Проверьте правильность переданного uri.");
            var culvertModel = culvertNode.Model ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var culvertContainer = PluginCoreOps.LockReadContainer<ICulvertContainer>(culvertModel) ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var culvert = culvertContainer.Culvert ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var sheetContext = culvert.SheetContext;
            var tableObjects = new List<object>();
            if (sheetContext.UseVolumesTable)
            {
                var volumesDataset = sheetContext.CreateDataset(SheetTableNames.VOLUMES_TABLE);
                tableObjects.Add(
                    CreateTableObj(SheetTableNames.VOLUMES_TABLE, volumesDataset)
                );
            }
            return new
            {
                result = new
                {
                    culvertName = culvertNode.Name,
                    volumeTables = tableObjects.ToArray()
                },
                description = "Объемы работ по водопропускной трубе.",
                status = "Данные успешно получены."
            };
        }

        [ToolDef(
            Name = "clv_get_specification",
            Description = "[ТРУБЫ] Возвращает спецификацию элементов водопропускной трубы. В структуре активного проекта элемент должен являться трубой (type == culvert).",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'uri': { 'type': 'string', 'description': 'Полный глобальный uri модели водопропускной трубы (из структуры активного проекта).' }
              },
              'required': ['uri'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetSpecification(Dictionary<string, object> args)
        {
            var uriStr = JsonUtils.RequireString(args, "uri");
            var uri = new URI(uriStr);
            var culvertNode = ProjectManager.Instance.GetNode(uri) ??
                throw new InvalidOperationException($"Не удалось найти водопропускную трубу в структуре проекта по указанному uri {uriStr}. Проверьте правильность переданного uri.");
            var culvertModel = culvertNode.Model ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var culvertContainer = PluginCoreOps.LockReadContainer<ICulvertContainer>(culvertModel) ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var culvert = culvertContainer.Culvert ?? throw new InvalidOperationException("Не удалось получить модель водопропускной трубы");
            var sheetContext = culvert.SheetContext;
            var tableObjects = new List<object>();
            if (sheetContext.UseCustomSpecs)
            {
                var specDataset_1 = sheetContext.CreateDataset(SheetTableNames.SPECIFICATION_1);
                tableObjects.Add(
                    CreateTableObj(SheetTableNames.SPECIFICATION_1, specDataset_1)
                );
                var specDataset_2 = sheetContext.CreateDataset(SheetTableNames.SPECIFICATION_2);
                tableObjects.Add(
                    CreateTableObj(SheetTableNames.SPECIFICATION_2, specDataset_2)
                );
            }
            else
            {
                var specDataset = culvert.Construction.CreateSpecificationDataset(SheetTableNames.SPECIFICATION, SpecificationType.NewConstruction);
                tableObjects.Add(
                    CreateTableObj(SheetTableNames.SPECIFICATION, specDataset)
                );
                var leftDismantlingDataset = culvert.Construction.CreateSpecificationDataset(SheetTableNames.LEFT_DISMANTLING_SPEC, SpecificationType.LeftDismantle);
                tableObjects.Add(
                    CreateTableObj(SheetTableNames.LEFT_DISMANTLING_SPEC, leftDismantlingDataset)
                );
                var rightDismantlingDataset = culvert.Construction.CreateSpecificationDataset(SheetTableNames.RIGHT_DISMANTLING_SPEC, SpecificationType.RightDismantle);
                tableObjects.Add(
                    CreateTableObj(SheetTableNames.RIGHT_DISMANTLING_SPEC, rightDismantlingDataset)
                );
            }
            return new
            {
                result = new
                {
                    culvertName = culvertNode.Name,
                    specificationTables = tableObjects.ToArray()
                },
                description = "Спецификация по водопропускной трубе.",
                status = "Данные успешно получены."
            };
        }

        private static object CreateTableObj(string tableName, List<RowData> dataset)
        {
            var columns = new List<object>();
            if (dataset.Count > 0)
            {
                var firstRow = dataset[0];
                var tags = new List<string>();
                var descriptions = new List<string>();
                var tagValues = new List<List<string>>();
                foreach (var tag in firstRow.GetContextsTags())
                {
                    tags.Add(tag.m_Tag);
                    descriptions.Add(tag.m_Description);
                    tagValues.Add(new List<string>());
                }
                for (int i = 0; i < dataset.Count; i++)
                {
                    var rowData = dataset[i];
                    for (int j = 0; j < tags.Count; j++)
                    {
                        var tag = tags[j];
                        if (!rowData.TryGetValue(tag, out string value))
                            value = "-";
                        tagValues[j].Add(value);
                    }
                }
                for (int i = 0; i < tags.Count; i++)
                {
                    columns.Add(
                        new
                        {
                            columnName = tags[i],
                            description = descriptions[i],
                            values = tagValues[i].Cast<object>().ToArray()
                        }
                    );
                }
            }
            return new { tableName, columns = columns.ToArray() };
        }
    }
}
