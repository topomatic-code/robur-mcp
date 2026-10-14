using System.Collections.Generic;
using System.Linq;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class GeologyTools : ToolProvider
    {
        [ToolDef(
            Name = "glg_get_info",
            Domain = ToolDomains.Geology,
            Description = "Возвращает информацию о геологической модели.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'pathId': { 'type': 'string', 'description': 'PathId геологической модели (из project_get_active).' }
              },
              'required': ['pathId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetGeology(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "pathId");
            var glgProvider = Container.GetSingleton<IGeologyProvider>();
            var (glgName, glgModel) = glgProvider.GetGeology(pathId);

            var boreholes = new List<object>();
            foreach (var borehole in glgModel.BoreholeTable)
            {
                boreholes.Add(
                    new
                    {
                        id = borehole.Id,
                        number = borehole.Number
                    }
                );
            }

            return new
            {
                result = new
                {
                    name = glgName ?? "none",
                    boreholeCount = boreholes.Count,
                    boreholes
                },
                description = "Информация о геологической модели.",
                status = "Информация о геологической модели успешно получена."
            };
        }

        [ToolDef(
            Name = "glg_get_borehole",
            Domain = ToolDomains.Geology,
            Description = "Возвращает информацию о выработке.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'glgPathId': { 'type': 'string', 'description': 'PathId геологической модели (из project_get_active).' },
                'boreholeId': { 'type': 'string', 'description': 'GUID выработки (id из glg_get_info).' }
              },
              'required': ['glgPathId', 'boreholeId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetBorehole(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "glgPathId");
            var boreholeIdStr = JsonUtils.RequireString(args, "boreholeId");
            var boreholeId = DwgUtils.ParseGuid(boreholeIdStr);

            var glgProvider = Container.GetSingleton<IGeologyProvider>();
            var (_, glgModel) = glgProvider.GetGeology(pathId);
            var borehole = glgModel.BoreholeTable.FirstOrDefault(b => b.Id.Equals(boreholeId)) ??
                throw new BadRequestException($"Не удалось найти выработку с id: {boreholeIdStr} в геологической модели: {pathId}.");

            return new
            {
                result = new
                {
                    id = borehole.Id,
                    number = borehole.Number,
                    type = borehole.BoreholeType.ToString(),
                    marked = borehole.Marked,
                    boringDate = borehole.BoringDate,
                    elevation = borehole.Z,
                    archiveNumber = borehole.ArchiveNumber,
                    seasonFrostDepth = borehole.SeasonFrostDepth,
                    seasonThawingDepth = borehole.SeasonThawingDepth,
                    depth = borehole.Depth,
                    coordinates = new
                    {
                        easting = borehole.X,
                        northing = borehole.Y
                    },
                    description = borehole.Description,
                    drillingEquipmentType = borehole.DrillingEquipmentType,
                    constructionType = borehole.ConstructionType,
                    bulkCalculate = borehole.BulkCalculate,
                    isFictive = borehole.IsFictive
                },
                description = "Информация о выработке.",
                status = "Информация о выработке успешно получена."
            };
        }

        [ToolDef(
            Name = "glg_get_borehole_grounds",
            Domain = ToolDomains.Geology,
            Description = "Возвращает список грунтов выработки.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'glgPathId': { 'type': 'string', 'description': 'PathId геологической модели (из project_get_active).' },
                'boreholeId': { 'type': 'string', 'description': 'GUID выработки (id из glg_get_info).' }
              },
              'required': ['glgPathId', 'boreholeId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetBoreholeGrounds(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "glgPathId");
            var boreholeIdStr = JsonUtils.RequireString(args, "boreholeId");
            var boreholeId = DwgUtils.ParseGuid(boreholeIdStr);

            var glgProvider = Container.GetSingleton<IGeologyProvider>();
            var (_, glgModel) = glgProvider.GetGeology(pathId);
            var borehole = glgModel.BoreholeTable.FirstOrDefault(b => b.Id.Equals(boreholeId)) ??
                throw new BadRequestException($"Не удалось найти выработку с id: {boreholeIdStr} в геологической модели: {pathId}.");

            var grounds = borehole.Select((layer, index) =>
                new
                {
                    rowNumber = index + 1,
                    id = layer.Ground.Id,
                    cipher = layer.Ground.Cipher,
                    name = layer.Ground.Name,
                    beddingGroup = layer.Ground.BeddingGroup,
                    depth = layer.Depth,
                    power = layer.Power,
                    description = layer.Description,
                    excavationCategory = layer.ExcavationCategory,
                    genesis = layer.Genesis
                }
            ).ToArray();

            return new
            {
                result = new
                {
                    id = borehole.Id,
                    number = borehole.Number,
                    groundCount = grounds.Length,
                    grounds
                },
                description = "Грунты выработки.",
                status = "Список грунтов выработки успешно получен."
            };
        }

        [ToolDef(
            Name = "glg_get_invalid_bedding_groups",
            Domain = ToolDomains.Geology,
            Description = "Проверяет порядок групп напластований грунтов во всех выработках геологической модели.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'glgPathId': { 'type': 'string', 'description': 'PathId геологической модели (из project_get_active).' }
              },
              'required': ['glgPathId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetInvalidBeddingGroups(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "glgPathId");
            var glgProvider = Container.GetSingleton<IGeologyProvider>();
            var (_, glgModel) = glgProvider.GetGeology(pathId);
            var boreholes = new List<object>();

            foreach (var borehole in glgModel.BoreholeTable)
            {
                var invalidGrounds = new List<object>();
                int? maxBeddingGroup = null;
                var maxBeddingGroupRow = 0;

                for (var i = 0; i < borehole.Count; i++)
                {
                    var ground = borehole[i].Ground;
                    if (ground == null)
                        continue;

                    var beddingGroup = ground.BeddingGroup;
                    if (maxBeddingGroup.HasValue && beddingGroup < maxBeddingGroup.Value)
                    {
                        invalidGrounds.Add(
                            new
                            {
                                rowNumber = i + 1,
                                id = ground.Id,
                                cipher = ground.Cipher,
                                name = ground.Name,
                                beddingGroup,
                                precedingMaxBeddingGroup = maxBeddingGroup.Value,
                                precedingMaxBeddingGroupRow = maxBeddingGroupRow
                            }
                        );
                    }
                    else if (!maxBeddingGroup.HasValue || beddingGroup > maxBeddingGroup.Value)
                    {
                        maxBeddingGroup = beddingGroup;
                        maxBeddingGroupRow = i + 1;
                    }
                }

                if (invalidGrounds.Count > 0)
                {
                    boreholes.Add(
                        new
                        {
                            id = borehole.Id,
                            number = borehole.Number,
                            invalidGrounds
                        }
                    );
                }
            }

            return new
            {
                result = new
                {
                    invalidBoreholeCount = boreholes.Count,
                    boreholes
                },
                description = boreholes.Count == 0
                    ? "Все выработки корректны: группы напластований грунтов идут в неубывающем порядке."
                    : "Выработки с нарушением порядка групп напластований.",
                status = "Проверка групп напластований успешно выполнена."
            };
        }
    }
}
