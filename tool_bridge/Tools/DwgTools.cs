using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class DwgTools : ToolProvider
    {
        [ToolDef(
            Name = "dwg_get_active_drawing_info",
            Description = "[ЧЕРТЕЖ] Возвращает информацию об активном чертеже Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetActiveDrawingInfo(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var activeSpaceBounds = drawing.ActiveSpace.Bounds;
            return new
            {
                result = new
                {
                    activeSpace = new
                    {
                        entitiesCount = drawing.ActiveSpace.Entities.Count,
                        bounds = new
                        {
                            left = activeSpaceBounds.Left,
                            right = activeSpaceBounds.Right,
                            top = activeSpaceBounds.Top,
                            bottom = activeSpaceBounds.Bottom
                        }
                    },
                    layers = drawing.Layers.Select(l => CreateLayerObj(l)).ToArray(),
                    activeLayerName = drawing.ActiveLayer?.Name ?? "none"
                },
                description = "Информация об активном чертеже.",
                status = "Информация об активном чертеже успешно получена."
            };
        }

        [ToolDef(
            Name = "dwg_add_layer",
            Description = "[ЧЕРТЕЖ] Добавляет новый слой в активный чертеж Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Имя слоя.' },
                'description': { 'type': 'string', 'description': 'Описание слоя.' },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета слоя (используется идексация цветов Autocad).' },
                'isVisible': { 'type': 'boolean', 'description': 'Видим ли слой.' }
              },
              'required': ['name'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            IdempotentHint = false,
            DestructiveHint = true
        )]
        public object AddLayer(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var name = JsonUtils.RequireString(args, "name");
            var description = JsonUtils.GetString(args, "description", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var isVisible = JsonUtils.GetBool(args, "isVisible", null);
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Имя слоя не может быть пустым.");
            if (drawing.Layers.IsExists(name))
                throw new InvalidOperationException($"Слой с именем {name} уже содержится в активном чертеже.");
            if (colorIndex != null && colorIndex.Value < 0)
                throw new InvalidOperationException("Индекс цвета слоя не может быть отрицательным.");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание слоя \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var layer = drawing.Layers.Add(name) ?? throw new InvalidOperationException("Не удалось создать слой.");
                if (description != null)
                    layer.Description = description;
                if (colorIndex != null)
                    layer.Color = new CadColor(colorIndex.Value);
                if (isVisible != null)
                    layer.Visible = isVisible.Value;
                return new
                {
                    result = CreateLayerObj(layer),
                    description = "Созданный слой активного чертежа.",
                    status = "Слой успешно создан."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_layer",
            Description = "[ЧЕРТЕЖ] Обновляет слой в активном чертеже Robur. Обновляет только переданные свойства, оставляя остальные без изменений.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'currentName': { 'type': 'string', 'description': 'Текущее имя слоя.' },
                'name': { 'type': 'string', 'description': 'Новое имя слоя.' },
                'description': { 'type': 'string', 'description': 'Описание слоя.' },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета слоя (используется идексация цветов Autocad).' },
                'isVisible': { 'type': 'boolean', 'description': 'Видим ли слой.' }
              },
              'required': ['currentName'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            IdempotentHint = true,
            DestructiveHint = true
        )]
        public object UpdateLayer(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var currentName = JsonUtils.RequireString(args, "currentName");
            var name = JsonUtils.GetString(args, "name", null);
            var description = JsonUtils.GetString(args, "description", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var isVisible = JsonUtils.GetBool(args, "isVisible", null);
            if (string.IsNullOrWhiteSpace(currentName))
                throw new InvalidOperationException("Текущее имя слоя не может быть пустым.");
            if (!drawing.Layers.IsExists(currentName))
                throw new InvalidOperationException($"Слой с именем {currentName} не содержится в активном чертеже.");
            if (name != null && string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Новое имя слоя не может быть пустым.");
            if (colorIndex != null && colorIndex.Value < 0)
                throw new InvalidOperationException("Индекс цвета слоя не может быть отрицательным.");
            if (name != null && !string.Equals(currentName, name) && drawing.Layers.IsExists(name))
                throw new InvalidOperationException($"Слой с именем {name} уже содержится в активном чертеже.");
            var layer = drawing.Layers[currentName] ?? throw new InvalidOperationException($"Не удалось получить слой с именем {currentName}.");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление слоя \"{currentName}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (name != null)
                    layer.Name = name;
                if (description != null)
                    layer.Description = description;
                if (colorIndex != null)
                    layer.Color = new CadColor(colorIndex.Value);
                if (isVisible != null)
                    layer.Visible = isVisible.Value;
                return new
                {
                    result = CreateLayerObj(layer),
                    description = "Обновленный слой активного чертежа.",
                    status = "Слой успешно обновлен."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_remove_layer",
            Description = "[ЧЕРТЕЖ] Удаляет слой из активного чертежа Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Имя слоя.' }
              },
              'required': ['name'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            IdempotentHint = false,
            DestructiveHint = true
        )]
        public object RemoveLayer(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var name = JsonUtils.RequireString(args, "name");
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Имя слоя не может быть пустым.");
            if (!drawing.Layers.IsExists(name))
                throw new InvalidOperationException($"Слой с именем {name} не содержится в активном чертеже.");
            var layer = drawing.Layers[name] ?? throw new InvalidOperationException($"Не удалось получить слой с именем {name}.");
            if (layer.IsSystem)
                throw new InvalidOperationException($"Системный слой {name} нельзя удалить.");
            var result = CreateLayerObj(layer);
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Удаление слоя \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!drawing.Layers.Remove(name))
                    throw new InvalidOperationException($"Не удалось удалить слой с именем {name}.");
                return new
                {
                    result,
                    description = "Удаленный слой активного чертежа.",
                    status = "Слой успешно удален."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_set_active_layer",
            Description = "[ЧЕРТЕЖ] Устанавливает активный слой в активном чертеже Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Имя слоя.' }
              },
              'required': ['name'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            IdempotentHint = true,
            DestructiveHint = true
        )]
        public object SetActiveLayer(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var name = JsonUtils.RequireString(args, "name");
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Имя слоя не может быть пустым.");
            if (!drawing.Layers.IsExists(name))
                throw new InvalidOperationException($"Слой с именем {name} не содержится в активном чертеже.");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Установка активного слоя \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var layer = drawing.Layers.ActivateLayer(name) ?? throw new InvalidOperationException($"Не удалось активировать слой с именем {name}.");
                return new
                {
                    result = CreateLayerObj(layer),
                    description = "Активный слой чертежа.",
                    status = "Активный слой успешно установлен."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        private static object CreateLayerObj(DwgLayer layer) => new
        {
            name = layer.Name,
            description = layer.Description,
            colorIndex = layer.Color.ColorIndex,
            isVisible = layer.Visible
        };

        [ToolDef(
            Name = "dwg_set_entities_layer",
            Description = "[ЧЕРТЕЖ] Устанавливает слой для набора элементов (сущностей) из пространства активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guids': {
                  'type': 'array',
                  'description': 'Guid-идентификаторы элементов (сущностей) из активного чертежа.',
                  'minItems': 1,
                  'items': {
                    'type': 'string',
                    'description': 'Guid-идентификатор элемента (сущности).'
                  }
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, который нужно назначить сущностям.' }
              },
              'required': ['guids', 'layerName'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object SetEntitiesLayer(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidArray = JsonUtils.RequireStringArray(args, "guids");
            var layerName = JsonUtils.RequireString(args, "layerName");
            if (guidArray.Length == 0)
                throw new InvalidOperationException("Необходимо передать хотя бы один guid сущности.");
            var entities = new List<(string guid, DwgEntity entity, string name, string type, string typeDescription)>(guidArray.Length);
            for (int i = 0; i < guidArray.Length; i++)
            {
                var guidStr = guidArray[i];
                var guid = Guid.Parse(guidStr);
                var (entity, name) = DwgUtils.FindEntity<DwgEntity>(drawing, sessionStorage, guid);
                if (entity == null)
                    throw new InvalidOperationException($"Не удалось найти элемент по указанному guid \"{guidStr}\".");
                var (type, typeDescription) = DwgUtils.GetEntityType(entity);
                entities.Add((guidStr, entity, name ?? "none", type, typeDescription));
            }
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Установка слоя \"{layerName}\" для {entities.Count} сущностей"));
            else
                drawing.BeginUpdate();
            try
            {
                foreach (var entityInfo in entities)
                {
                    DwgUtils.ApplyEntityLayer(drawing, entityInfo.entity, layerName);
                }
                return new
                {
                    result = new
                    {
                        layerName,
                        entities = entities.Select(entityInfo => new
                        {
                            entityInfo.guid,
                            entityInfo.name,
                            entityInfo.type,
                            entityInfo.typeDescription,
                            layerName = entityInfo.entity.Layer?.Name ?? "none"
                        }).ToArray()
                    },
                    description = "Сущности, для которых был установлен слой.",
                    status = "Слой сущностей успешно обновлен."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_set_entities_color",
            Description = "[ЧЕРТЕЖ] Устанавливает цвет для набора элементов (сущностей) из пространства активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guids': {
                  'type': 'array',
                  'description': 'Guid-идентификаторы элементов (сущностей) из активного чертежа.',
                  'minItems': 1,
                  'items': {
                    'type': 'string',
                    'description': 'Guid-идентификатор элемента (сущности).'
                  }
                },
                'colorMode': {
                  'type': 'string',
                  'description': 'Режим цвета сущностей.',
                  'enum': ['Indexed', 'ByLayer', 'ByBlock']
                },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета сущностей. Используется только при colorMode = Indexed.' }
              },
              'required': ['guids', 'colorMode'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object SetEntitiesColor(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidArray = JsonUtils.RequireStringArray(args, "guids");
            var colorMode = JsonUtils.RequireString(args, "colorMode");
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            if (guidArray.Length == 0)
                throw new InvalidOperationException("Необходимо передать хотя бы один guid сущности.");
            var entities = new List<(string guid, DwgEntity entity, string name, string type, string typeDescription)>(guidArray.Length);
            for (int i = 0; i < guidArray.Length; i++)
            {
                var guidStr = guidArray[i];
                var guid = Guid.Parse(guidStr);
                var (entity, name) = DwgUtils.FindEntity<DwgEntity>(drawing, sessionStorage, guid);
                if (entity == null)
                    throw new InvalidOperationException($"Не удалось найти элемент по указанному guid \"{guidStr}\".");
                var (type, typeDescription) = DwgUtils.GetEntityType(entity);
                entities.Add((guidStr, entity, name ?? "none", type, typeDescription));
            }
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Установка цвета {colorMode} для {entities.Count} сущностей"));
            else
                drawing.BeginUpdate();
            try
            {
                foreach (var entityInfo in entities)
                {
                    DwgUtils.ApplyEntityColor(entityInfo.entity, colorMode, colorIndex);
                }
                return new
                {
                    result = new
                    {
                        colorMode,
                        colorIndex,
                        entities = entities.Select(entityInfo => new
                        {
                            entityInfo.guid,
                            entityInfo.name,
                            entityInfo.type,
                            entityInfo.typeDescription,
                            colorMode = DwgUtils.GetColorMode(entityInfo.entity.Color),
                            colorIndex = entityInfo.entity.Color.ColorIndex
                        }).ToArray()
                    },
                    description = "Сущности, для которых был установлен цвет.",
                    status = "Цвет сущностей успешно обновлен."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_get_entities_info",
            Description = "[ЧЕРТЕЖ] Возвращает информацию о диапазоне элементов (сущностей) из пространства активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'startIndex' : { 'type': 'integer', 'description': 'Начальный индекс запрашиваемого диапазона элементов (отсчет индексов начинается с 0).' },
                'endIndex': { 'type': 'integer', 'description': 'Конечный индекс запрашиваемого диапазона элементов (отсчет индексов начинается с 0).' }
              },
              'required': ['startIndex', 'endIndex'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetEntities(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var startIndex = JsonUtils.RequireInt(args, "startIndex");
            var endIndex = JsonUtils.RequireInt(args, "endIndex");
            var drawingEntities = drawing.ActiveSpace.Entities;
            if (startIndex < 0 || startIndex >= drawingEntities.Count)
                throw new InvalidOperationException("Значение startIndex не попадает в допустимый диапазон.");
            if (endIndex < startIndex || endIndex >= drawingEntities.Count)
                throw new InvalidOperationException("Значение endIndex не попадает в допустимый диапазон.");
            var entities = new List<object>();
            for (int i = startIndex; i <= endIndex; i++)
            {
                var entity = drawingEntities[i];
                string guidStr = null;
                var name = "none";
                if (entity.HasExtensionDictionary)
                {
                    var extDict = entity.GetExtensionDictionary();
                    guidStr = extDict.GetString("guid", null);
                    name = extDict.GetString("name", "none");
                }
                if (guidStr == null)
                {
                    if (sessionStorage.HasObject(entity))
                        guidStr = sessionStorage.GetGuid(entity).ToString();
                    else
                        guidStr = sessionStorage.AddObject(entity).ToString();
                }
                else
                {
                    var guid = Guid.Parse(guidStr);
                    if (!sessionStorage.HasObject(guid))
                        sessionStorage.AddObject(guid, entity);
                }
                entities.Add(DwgUtils.CreateEntityInfoObj(entity, guidStr, name));
            }
            return new
            {
                result = new
                {
                    startIndex,
                    endIndex,
                    entitiesInfo = entities.ToArray()
                },
                description = $"Информация о диапазоне элементов (сущностей) чертежа с {startIndex} по {endIndex} индексы.",
                status = "Информация о диапазоне элементов (сущностей) чертежа успешно получена."
            };
        }

        [ToolDef(
            Name = "dwg_get_active_space_entity",
            Description = "[ЧЕРТЕЖ] Возвращает данные и структуру элемента (сущности) из пространства активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор элемента (сущности) (из активного чертежа).' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetActiveSpaceEntity(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            DwgEntity resultEntity = null;
            var name = "none";
            if (sessionStorage.HasObject(guid))
            {
                resultEntity = (DwgEntity)sessionStorage.GetObject(guid);
                if (resultEntity.Drawing != drawing)
                    throw new InvalidOperationException($"Элемент (сущность) с guid \"{guidStr}\" не находится в активном чертеже.");
                if (resultEntity.HasExtensionDictionary)
                {
                    var extDict = resultEntity.GetExtensionDictionary();
                    name = extDict.GetString("name", "none");
                }
            }
            else
            {
                foreach (var dwgEntity in drawing.ActiveSpace.Entities)
                {
                    if (dwgEntity.HasExtensionDictionary)
                    {
                        var extDict = dwgEntity.GetExtensionDictionary();
                        if (string.Equals(guidStr, extDict.GetString("guid", null)))
                        {
                            sessionStorage.AddObject(guid, dwgEntity);
                            resultEntity = dwgEntity;
                            name = extDict.GetString("name", "none");
                            break;
                        }
                    }
                }
            }
            if (resultEntity == null)
                throw new InvalidOperationException($"Не удалось найти элемент (сущность) по указанному guid \"{guidStr}\".");
            return new
            {
                result = DwgUtils.CreateEntityObj(resultEntity, guidStr, name),
                description = "Элемент (сущность) чертежа.",
                status = "Элемент (сущность) чертежа успешно получен."
            };
        }

        [ToolDef(
            Name = "dwg_create_polyline",
            Description = "[ЧЕРТЕЖ] Создает 2d полилинию и добавляет ее в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название полилинии.' },
                'points': {
                  'type': 'array',
                  'description': 'Массив 2d точек полилинии.',
                  'minItems': 2,
                  'items': {
                    'type': 'object',
                    'properties': {
                      'x': { 'type': 'number', 'description': 'x-координата' },
                      'y': { 'type': 'number', 'description': 'y-координата' }
                    },
                    'required': ['x', 'y'],
                    'additionalProperties': false
                  }
                },
                'closed': { 'type': 'boolean', 'description': 'Закрыт ли контур (если true, то последняя точка соединяется с первой).' },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить полилинию. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета полилинии.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета полилинии. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'points', 'closed'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreatePolyline(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var pointArray = JsonUtils.RequireArray(args, "points");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var points = new Vector2D[pointArray.Length];
            for (int i = 0; i < pointArray.Length; i++)
            {
                var pointObj = pointArray[i];
                var x = JsonUtils.RequireDouble(pointObj, "x");
                var y = JsonUtils.RequireDouble(pointObj, "y");
                points[i] = new Vector2D(x, y);
            }
            var closed = JsonUtils.RequireBool(args, "closed");
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание полилинии \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var polyline = drawing.ActiveSpace.AddPolyline(points);
                DwgUtils.ApplyEntityLayer(drawing, polyline, layerName);
                DwgUtils.ApplyEntityColor(polyline, colorMode, colorIndex);
                polyline.Closed = closed;
                if (!polyline.HasExtensionDictionary)
                    polyline.CreateExtensionDictionary();
                var extDict = polyline.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, polyline);
                return new
                {
                    result = DwgUtils.CreatePolylineObj(polyline, guidStr, name),
                    description = "Созданная полилиния.",
                    status = "Полилиния успешно создана."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_polyline",
            Description = "[ЧЕРТЕЖ] Обновляет 2d полилинию в пространстве активного чертежа. Обновляет только переданные свойства, оставляя остальные без изменений.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор 2d полилинии (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название полилинии.' },
                'points': {
                  'type': 'array',
                  'description': 'Массив 2d точек полилинии.',
                  'minItems': 2,
                  'items': {
                    'type': 'object',
                    'properties': {
                      'x': { 'type': 'number', 'description': 'x-координата' },
                      'y': { 'type': 'number', 'description': 'y-координата' }
                    },
                    'required': ['x', 'y'],
                    'additionalProperties': false
                  }
                },
                'closed': { 'type': 'boolean', 'description': 'Закрыт ли контур (если true, то последняя точка соединяется с первой).' },
                'layerName': { 'type': 'string', 'description': 'Имя слоя полилинии. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета полилинии.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета полилинии. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdatePolyline(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var pointArray = JsonUtils.GetArray(args, "points", null);
            var closed = JsonUtils.GetBool(args, "closed", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var (polyline, currentName) = DwgUtils.FindEntity<DwgPolyline>(drawing, sessionStorage, guid);
            if (polyline == null)
                throw new InvalidOperationException($"Не удалось найти полилинию по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление полилинии \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!polyline.HasExtensionDictionary)
                    polyline.CreateExtensionDictionary();
                var extDict = polyline.GetExtensionDictionary();
                if (name != null)
                {
                    if (name != currentName)
                        extDict.SetString("name", name);
                }
                else
                {
                    if (currentName == null)
                        extDict.SetString("name", "none");
                }
                if (pointArray != null)
                {
                    polyline.Clear();
                    for (int i = 0; i < pointArray.Length; i++)
                    {
                        var pointObj = pointArray[i];
                        var x = JsonUtils.RequireDouble(pointObj, "x");
                        var y = JsonUtils.RequireDouble(pointObj, "y");
                        polyline.Add(new BugleVector2D(new Vector2D(x, y)));
                    }
                }
                if (closed != null)
                    polyline.Closed = closed.Value;
                DwgUtils.ApplyEntityLayer(drawing, polyline, layerName);
                DwgUtils.ApplyEntityColor(polyline, colorMode, colorIndex);
                return new
                {
                    result = DwgUtils.CreatePolylineObj(polyline, guidStr, name),
                    description = "Обновленная полилиния.",
                    status = "Полилиния успешно обновлена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_create_table",
            Description = "[ЧЕРТЕЖ] Создает таблицу и добавляет ее в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название таблицы.' },
                'position': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Точка вставки таблицы (левая верхняя точка таблицы).'
                },
                'rowCount': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество строк в таблице.' },
                'columnCount': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество столбцов в таблице.' },
                'cells': {
                  'type': 'array',
                  'description': 'Массив ячеек таблицы.',
                  'items': {
                    'type': 'object',
                    'properties': {
                      'row': { 'type': 'integer', 'minimum' : 0, 'description': 'Индекс первой (верхней) строки ячейки (начало индексации с 0).' },
                      'column': { 'type': 'integer', 'minimum' : 0, 'description': 'Индекс первого (левого) столбца ячейки (начало индексации с 0).' },
                      'rowSpan': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество строк, занимаемых ячейкой.' },
                      'columnSpan': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество столбцов, занимаемых ячейкой.' },
                      'text': { 'type': 'string', 'description': 'Текст ячейки. Является мультитекстом. Управляющие последовательности: \\P - перенос строки (новый абзцац).' }
                    },
                    'required': ['row', 'column', 'rowSpan', 'columnSpan', 'text'],
                    'additionalProperties': false
                  }
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить таблицу. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета таблицы.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета таблицы. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'position', 'rowCount', 'columnCount', 'cells'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateTable(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var position = JsonUtils.RequireObject(args, "position");
            var rowCount = JsonUtils.RequireInt(args, "rowCount");
            var columnCount = JsonUtils.RequireInt(args, "columnCount");
            var cells = JsonUtils.RequireArray(args, "cells");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание таблицы \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var table = new DwgTable(drawing.TableStyles.Standard, rowCount, 1, columnCount, 1);
                table.Prepare(drawing);
                table.UnMergeAll(true);
                foreach (var cell in cells)
                {
                    var startRow = JsonUtils.RequireInt(cell, "row");
                    var startColumn = JsonUtils.RequireInt(cell, "column");
                    var rowSpan = JsonUtils.RequireInt(cell, "rowSpan");
                    var columnSpan = JsonUtils.RequireInt(cell, "columnSpan");
                    var text = JsonUtils.RequireString(cell, "text");
                    var endColumn = startColumn + columnSpan - 1;
                    var endRow = startRow + rowSpan - 1;
                    if (endColumn >= columnCount)
                        throw new InvalidOperationException($"Индекс конечного столбца ячейки вышел за допустимый диапазон. Убедитесь в правильности значения columnSpan = {columnSpan}, для ячейки (row = {startRow}, column = {startColumn}).");
                    if (endRow >= rowCount)
                        throw new InvalidOperationException($"Индекс конечной строки ячейки вышел за допустимый диапазон. Убедитесь в правильности значения rowSpan = {rowSpan}, для ячейки (row = {startRow}, column = {startColumn}).");
                    if (rowSpan > 1 || columnSpan > 1)
                        table.MergeCells(startColumn, startRow, endColumn, endRow);
                    table[startRow, startColumn].SourceText = text;
                }
                var x = JsonUtils.RequireDouble(position, "x");
                var y = JsonUtils.RequireDouble(position, "y");
                table.Position = new Vector3D(x, y, 0);
                drawing.ActiveSpace.Add(table);
                DwgUtils.ApplyEntityLayer(drawing, table, layerName);
                DwgUtils.ApplyEntityColor(table, colorMode, colorIndex);
                if (!table.HasExtensionDictionary)
                    table.CreateExtensionDictionary();
                var extDict = table.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, table);
                return new
                {
                    result = DwgUtils.CreateTableObj(table, guidStr, name),
                    description = "Созданная таблица.",
                    status = "Таблица успешно создана."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_table",
            Description = "[ЧЕРТЕЖ] Обновляет таблицу в пространстве активного чертежа. Обновляет только переданные свойства, оставляя остальные без изменений.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор таблицы (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название таблицы.' },
                'position': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Точка вставки таблицы (левая верхняя точка таблицы).'
                },
                'rowCount': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество строк в таблице.' },
                'columnCount': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество столбцов в таблице.' },
                'cells': {
                  'type': 'array',
                  'description': 'Массив ячеек таблицы.',
                  'items': {
                    'type': 'object',
                    'properties': {
                      'row': { 'type': 'integer', 'minimum' : 0, 'description': 'Индекс первой (верхней) строки ячейки (начало индексации с 0).' },
                      'column': { 'type': 'integer', 'minimum' : 0, 'description': 'Индекс первого (левого) столбца ячейки (начало индексации с 0).' },
                      'rowSpan': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество строк, занимаемых ячейкой.' },
                      'columnSpan': { 'type': 'integer', 'minimum' : 1, 'description': 'Количество столбцов, занимаемых ячейкой.' },
                      'text': { 'type': 'string', 'description': 'Текст ячейки. Является мультитекстом. Управляющие последовательности: \\P - перенос строки (новый абзцац).' }
                    },
                    'required': ['row', 'column', 'rowSpan', 'columnSpan', 'text'],
                    'additionalProperties': false
                  }
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя таблицы. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета таблицы.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета таблицы. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdateTable(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var position = JsonUtils.GetObject(args, "position", null);
            var rowCount = JsonUtils.GetInt(args, "rowCount", null);
            var columnCount = JsonUtils.GetInt(args, "columnCount", null);
            var cells = JsonUtils.GetArray(args, "cells", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var (table, currentName) = DwgUtils.FindEntity<DwgTable>(drawing, sessionStorage, guid);
            if (table == null)
                throw new InvalidOperationException($"Не удалось найти таблицу по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление таблицы \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!table.HasExtensionDictionary)
                    table.CreateExtensionDictionary();
                var extDict = table.GetExtensionDictionary();
                if (name != null)
                {
                    if (name != currentName)
                        extDict.SetString("name", name);
                }
                else
                {
                    if (currentName == null)
                        extDict.SetString("name", "none");
                }
                if (position != null)
                {
                    var x = JsonUtils.RequireDouble(position, "x");
                    var y = JsonUtils.RequireDouble(position, "y");
                    table.Position = new Vector3D(x, y, 0);
                }
                if (rowCount != null)
                {
                    var currentLastRowIndex = table.RowsCount - 1;
                    var newLastRowIndex = rowCount - 1;
                    if (currentLastRowIndex > newLastRowIndex)
                    {
                        for (int i = currentLastRowIndex; i > newLastRowIndex; i--)
                        {
                            table.RemoveRowAt(i);
                        }
                    }
                    else
                    {
                        for (int i = currentLastRowIndex; i < newLastRowIndex; i++)
                        {
                            table.AddRow();
                        }
                    }
                }
                if (columnCount != null)
                {
                    var currentLastColumnIndex = table.ColumnsCount - 1;
                    var newLastColumnIndex = columnCount - 1;
                    if (currentLastColumnIndex > newLastColumnIndex)
                    {
                        for (int i = currentLastColumnIndex; i > newLastColumnIndex; i--)
                        {
                            table.RemoveColumnAt(i);
                        }
                    }
                    else
                    {
                        for (int i = currentLastColumnIndex; i < newLastColumnIndex; i++)
                        {
                            table.AddColumn();
                        }
                    }
                }
                if (cells != null)
                {
                    table.UnMergeAll(false);
                    foreach (var cell in cells)
                    {
                        var startRow = JsonUtils.RequireInt(cell, "row");
                        var startColumn = JsonUtils.RequireInt(cell, "column");
                        var rowSpan = JsonUtils.RequireInt(cell, "rowSpan");
                        var columnSpan = JsonUtils.RequireInt(cell, "columnSpan");
                        var text = JsonUtils.RequireString(cell, "text");
                        var endColumn = startColumn + columnSpan - 1;
                        var endRow = startRow + rowSpan - 1;
                        if (endColumn >= columnCount)
                            throw new InvalidOperationException($"Индекс конечного столбца ячейки вышел за допустимый диапазон. Убедитесь в правильности значения columnSpan = {columnSpan}, для ячейки (row = {startRow}, column = {startColumn}).");
                        if (endRow >= rowCount)
                            throw new InvalidOperationException($"Индекс конечной строки ячейки вышел за допустимый диапазон. Убедитесь в правильности значения rowSpan = {rowSpan}, для ячейки (row = {startRow}, column = {startColumn}).");
                        if (rowSpan > 1 || columnSpan > 1)
                            table.MergeCells(startColumn, startRow, endColumn, endRow);
                        table[startRow, startColumn].SourceText = text;
                    }
                }
                DwgUtils.ApplyEntityLayer(drawing, table, layerName);
                DwgUtils.ApplyEntityColor(table, colorMode, colorIndex);
                return new
                {
                    result = DwgUtils.CreateTableObj(table, guidStr, name),
                    description = "Обновленная таблица.",
                    status = "Таблица успешно обновлена."
                };

            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_create_mtext",
            Description = "[ЧЕРТЕЖ] Создает многострочный текст (мультитекст, type = dwg_mtext) и добавляет его в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название текста.' },
                'text': { 'type': 'string', 'description': 'Текстовое содержимое. Управляющие последовательности: \\P - перенос строки (новый абзцац).' },
                'position': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата точки вставки.' },
                    'y': { 'type': 'number', 'description': 'y-координата точки вставки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Позиция текста.'
                },
                'height': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Высота текста.' },
                'rotation': { 'type': 'number', 'description': 'Угол поворота текста (в радианах).' },
                'attachmentPoint': {
                  'type': 'string',
                  'description': 'Точка привязки текста.',
                  'enum': ['TopLeft', 'TopCenter', 'TopRight', 'MiddleLeft', 'MiddleCenter', 'MiddleRight', 'BottomLeft', 'BottomCenter', 'BottomRight']
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить мультитекст. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета мультитекста.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета мультитекста. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'text', 'position', 'height'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateMText(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var text = JsonUtils.RequireString(args, "text");
            var position = JsonUtils.RequireObject(args, "position");
            var x = JsonUtils.RequireDouble(position, "x");
            var y = JsonUtils.RequireDouble(position, "y");
            var height = JsonUtils.RequireDouble(args, "height");
            if (height <= 0)
                throw new InvalidOperationException("Высота текста (height) должна быть больше 0.");
            var rotation = JsonUtils.GetDouble(args, "rotation", 0).Value;
            var attachmentPointString = JsonUtils.GetString(args, "attachmentPoint", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание многострочного текста \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var mText = new DwgMText
                {
                    Content = text,
                    Position = new Vector3D(x, y, 0),
                    Height = height,
                    Rotation = rotation
                };
                if (attachmentPointString != null)
                    mText.AttachmentPoint = DwgUtils.ParseAttachmentPoint(attachmentPointString);
                drawing.ActiveSpace.Add(mText);
                DwgUtils.ApplyEntityLayer(drawing, mText, layerName);
                DwgUtils.ApplyEntityColor(mText, colorMode, colorIndex);
                if (!mText.HasExtensionDictionary)
                    mText.CreateExtensionDictionary();
                var extDict = mText.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, mText);
                return new
                {
                    result = DwgUtils.CreateMTextObj(mText, guidStr, name),
                    description = "Созданный многострочный текст.",
                    status = "Многострочный текст успешно создан."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_mtext",
            Description = "[ЧЕРТЕЖ] Обновляет многострочный текст (мультитекст, type = dwg_mtext) в пространстве активного чертежа. Обновляет только переданные свойства.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор текста (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название текста.' },
                'text': { 'type': 'string', 'description': 'Текстовое содержимое. Управляющие последовательности: \\P - перенос строки (новый абзцац).' },
                'position': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата точки вставки.' },
                    'y': { 'type': 'number', 'description': 'y-координата точки вставки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Позиция текста.'
                },
                'height': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Высота текста.' },
                'rotation': { 'type': 'number', 'description': 'Угол поворота текста (в радианах).' },
                'attachmentPoint': {
                  'type': 'string',
                  'description': 'Точка привязки текста.',
                  'enum': ['TopLeft', 'TopCenter', 'TopRight', 'MiddleLeft', 'MiddleCenter', 'MiddleRight', 'BottomLeft', 'BottomCenter', 'BottomRight']
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя мультитекста. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета мультитекста.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета мультитекста. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdateMText(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var text = JsonUtils.GetString(args, "text", null);
            var position = JsonUtils.GetObject(args, "position", null);
            var height = JsonUtils.GetDouble(args, "height", null);
            var rotation = JsonUtils.GetDouble(args, "rotation", null);
            var attachmentPointString = JsonUtils.GetString(args, "attachmentPoint", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var (mText, currentName) = DwgUtils.FindEntity<DwgMText>(drawing, sessionStorage, guid);
            if (mText == null)
                throw new InvalidOperationException($"Не удалось найти многострочный текст по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление многострочного текста \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!mText.HasExtensionDictionary)
                    mText.CreateExtensionDictionary();
                var extDict = mText.GetExtensionDictionary();
                if (name != null)
                {
                    if (name != currentName)
                        extDict.SetString("name", name);
                }
                else
                {
                    if (currentName == null)
                        extDict.SetString("name", "none");
                }
                if (text != null)
                    mText.Content = text;
                if (position != null)
                {
                    var x = JsonUtils.RequireDouble(position, "x");
                    var y = JsonUtils.RequireDouble(position, "y");
                    mText.Position = new Vector3D(x, y, 0);
                }
                if (height != null)
                {
                    if (height.Value <= 0)
                        throw new InvalidOperationException("Высота текста (height) должна быть больше 0.");
                    mText.Height = height.Value;
                }
                if (rotation != null)
                    mText.Rotation = rotation.Value;
                if (attachmentPointString != null)
                    mText.AttachmentPoint = DwgUtils.ParseAttachmentPoint(attachmentPointString);
                DwgUtils.ApplyEntityLayer(drawing, mText, layerName);
                DwgUtils.ApplyEntityColor(mText, colorMode, colorIndex);
                return new
                {
                    result = DwgUtils.CreateMTextObj(mText, guidStr, name ?? currentName ?? "none"),
                    description = "Обновленный многострочный текст.",
                    status = "Многострочный текст успешно обновлен."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_create_text",
            Description = "[ЧЕРТЕЖ] Создает текст (type = dwg_text) и добавляет его в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название текста.' },
                'text': { 'type': 'string', 'description': 'Текстовое содержимое DwgText.' },
                'position': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата точки вставки.' },
                    'y': { 'type': 'number', 'description': 'y-координата точки вставки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Позиция текста.'
                },
                'height': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Высота текста.' },
                'rotation': { 'type': 'number', 'description': 'Угол поворота текста (в радианах).' },
                'justify': {
                  'type': 'string',
                  'description': 'Выравнивание текста.',
                  'enum': ['Left', 'Center', 'Right', 'Aligned', 'Middle', 'Fit', 'TopLeft', 'TopCenter', 'TopRight', 'MiddleLeft', 'MiddleCenter', 'MiddleRight', 'BottomLeft', 'BottomCenter', 'BottomRight']
                },
                'textAlignmentPoint': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата точки выравнивания текста.' },
                    'y': { 'type': 'number', 'description': 'y-координата точки выравнивания текста.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Точка выравнивания (используется в режимах выравнивания, отличных от Left).'
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить текст. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета текста.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета текста. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'text', 'position', 'height'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateText(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var text = JsonUtils.RequireString(args, "text");
            var position = JsonUtils.RequireObject(args, "position");
            var x = JsonUtils.RequireDouble(position, "x");
            var y = JsonUtils.RequireDouble(position, "y");
            var height = JsonUtils.RequireDouble(args, "height");
            if (height <= 0)
                throw new InvalidOperationException("Высота текста (height) должна быть больше 0.");
            var rotation = JsonUtils.GetDouble(args, "rotation", 0) ?? 0;
            var justifyString = JsonUtils.GetString(args, "justify", null);
            var textAlignmentPoint = JsonUtils.GetObject(args, "textAlignmentPoint", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание текста \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var dwgText = new DwgText
                {
                    Content = text,
                    Position = new Vector3D(x, y, 0),
                    Height = height,
                    Rotation = rotation
                };
                if (justifyString != null)
                    dwgText.Justify = DwgUtils.ParseTextAlignment(justifyString);
                if (textAlignmentPoint != null)
                {
                    var alignmentX = JsonUtils.RequireDouble(textAlignmentPoint, "x");
                    var alignmentY = JsonUtils.RequireDouble(textAlignmentPoint, "y");
                    dwgText.TextAlignmentPoint = new Vector3D(alignmentX, alignmentY, 0);
                }
                drawing.ActiveSpace.Add(dwgText);
                DwgUtils.ApplyEntityLayer(drawing, dwgText, layerName);
                DwgUtils.ApplyEntityColor(dwgText, colorMode, colorIndex);
                if (!dwgText.HasExtensionDictionary)
                    dwgText.CreateExtensionDictionary();
                var extDict = dwgText.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, dwgText);
                return new
                {
                    result = DwgUtils.CreateTextObj(dwgText, guidStr, name),
                    description = "Созданный текст.",
                    status = "Текст успешно создан."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_text",
            Description = "[ЧЕРТЕЖ] Обновляет текст (type = dwg_text) в пространстве активного чертежа. Обновляет только переданные свойства.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор текста (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название текста.' },
                'text': { 'type': 'string', 'description': 'Текстовое содержимое DwgText.' },
                'position': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата точки вставки.' },
                    'y': { 'type': 'number', 'description': 'y-координата точки вставки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Позиция текста.'
                },
                'height': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Высота текста.' },
                'rotation': { 'type': 'number', 'description': 'Угол поворота текста (в радианах).' },
                'justify': {
                  'type': 'string',
                  'description': 'Выравнивание текста.',
                  'enum': ['Left', 'Center', 'Right', 'Aligned', 'Middle', 'Fit', 'TopLeft', 'TopCenter', 'TopRight', 'MiddleLeft', 'MiddleCenter', 'MiddleRight', 'BottomLeft', 'BottomCenter', 'BottomRight']
                },
                'textAlignmentPoint': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата точки выравнивания текста.' },
                    'y': { 'type': 'number', 'description': 'y-координата точки выравнивания текста.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Точка выравнивания (используется в режимах выравнивания, отличных от Left).'
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя текста. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета текста.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета текста. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdateText(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var text = JsonUtils.GetString(args, "text", null);
            var position = JsonUtils.GetObject(args, "position", null);
            var height = JsonUtils.GetDouble(args, "height", null);
            var rotation = JsonUtils.GetDouble(args, "rotation", null);
            var justifyString = JsonUtils.GetString(args, "justify", null);
            var textAlignmentPoint = JsonUtils.GetObject(args, "textAlignmentPoint", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var (dwgText, currentName) = DwgUtils.FindEntity<DwgText>(drawing, sessionStorage, guid);
            if (dwgText == null)
                throw new InvalidOperationException($"Не удалось найти текст по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление текста \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!dwgText.HasExtensionDictionary)
                    dwgText.CreateExtensionDictionary();
                var extDict = dwgText.GetExtensionDictionary();
                if (name != null)
                {
                    if (name != currentName)
                        extDict.SetString("name", name);
                }
                else
                {
                    if (currentName == null)
                        extDict.SetString("name", "none");
                }
                if (text != null)
                    dwgText.Content = text;
                if (position != null)
                {
                    var x = JsonUtils.RequireDouble(position, "x");
                    var y = JsonUtils.RequireDouble(position, "y");
                    dwgText.Position = new Vector3D(x, y, 0);
                }
                if (height != null)
                {
                    if (height.Value <= 0)
                        throw new InvalidOperationException("Высота текста (height) должна быть больше 0.");
                    dwgText.Height = height.Value;
                }
                if (rotation != null)
                    dwgText.Rotation = rotation.Value;
                if (justifyString != null)
                    dwgText.Justify = DwgUtils.ParseTextAlignment(justifyString);
                if (textAlignmentPoint != null)
                {
                    var alignmentX = JsonUtils.RequireDouble(textAlignmentPoint, "x");
                    var alignmentY = JsonUtils.RequireDouble(textAlignmentPoint, "y");
                    dwgText.TextAlignmentPoint = new Vector3D(alignmentX, alignmentY, 0);
                }
                DwgUtils.ApplyEntityLayer(drawing, dwgText, layerName);
                DwgUtils.ApplyEntityColor(dwgText, colorMode, colorIndex);
                return new
                {
                    result = DwgUtils.CreateTextObj(dwgText, guidStr, name ?? currentName ?? "none"),
                    description = "Обновленный текст.",
                    status = "Текст успешно обновлен."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_create_circle",
            Description = "[ЧЕРТЕЖ] Создает окружность и добавляет ее в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название окружности.' },
                'center': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата центра.' },
                    'y': { 'type': 'number', 'description': 'y-координата центра.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Координаты центра окружности.'
                },
                'radius': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Радиус окружности.' },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить окружность. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета окружности.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета окружности. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'center', 'radius'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateCircle(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var center = JsonUtils.RequireObject(args, "center");
            var centerX = JsonUtils.RequireDouble(center, "x");
            var centerY = JsonUtils.RequireDouble(center, "y");
            var radius = JsonUtils.RequireDouble(args, "radius");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            if (radius <= 0)
                throw new InvalidOperationException("Радиус окружности (radius) должен быть больше 0.");
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание окружности \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var circle = new DwgCircle
                {
                    Center = new Vector3D(centerX, centerY, 0),
                    Radius = radius
                };
                drawing.ActiveSpace.Add(circle);
                DwgUtils.ApplyEntityLayer(drawing, circle, layerName);
                DwgUtils.ApplyEntityColor(circle, colorMode, colorIndex);
                if (!circle.HasExtensionDictionary)
                    circle.CreateExtensionDictionary();
                var extDict = circle.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, circle);
                return new
                {
                    result = DwgUtils.CreateCircleObj(circle, guidStr, name),
                    description = "Созданная окружность.",
                    status = "Окружность успешно создана."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_circle",
            Description = "[ЧЕРТЕЖ] Обновляет окружность в пространстве активного чертежа. Обновляет только переданные свойства.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор окружности (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название окружности.' },
                'center': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата центра.' },
                    'y': { 'type': 'number', 'description': 'y-координата центра.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Координаты центра окружности.'
                },
                'radius': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Радиус окружности.' },
                'layerName': { 'type': 'string', 'description': 'Имя слоя окружности. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета окружности.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета окружности. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdateCircle(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var center = JsonUtils.GetObject(args, "center", null);
            var radius = JsonUtils.GetDouble(args, "radius", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var (circle, currentName) = DwgUtils.FindEntity<DwgCircle>(drawing, sessionStorage, guid);
            if (circle == null)
                throw new InvalidOperationException($"Не удалось найти окружность по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление окружности \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!circle.HasExtensionDictionary)
                    circle.CreateExtensionDictionary();
                var extDict = circle.GetExtensionDictionary();
                if (name != null)
                {
                    if (name != currentName)
                        extDict.SetString("name", name);
                }
                else
                {
                    if (currentName == null)
                        extDict.SetString("name", "none");
                }
                if (center != null)
                {
                    var centerX = JsonUtils.RequireDouble(center, "x");
                    var centerY = JsonUtils.RequireDouble(center, "y");
                    circle.Center = new Vector3D(centerX, centerY, 0);
                }
                if (radius != null)
                {
                    if (radius.Value <= 0)
                        throw new InvalidOperationException("Радиус окружности (radius) должен быть больше 0.");
                    circle.Radius = radius.Value;
                }
                DwgUtils.ApplyEntityLayer(drawing, circle, layerName);
                DwgUtils.ApplyEntityColor(circle, colorMode, colorIndex);
                return new
                {
                    result = DwgUtils.CreateCircleObj(circle, guidStr, name ?? currentName ?? "none"),
                    description = "Обновленная окружность.",
                    status = "Окружность успешно обновлена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_create_line",
            Description = "[ЧЕРТЕЖ] Создает линию и добавляет ее в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название линии.' },
                'startPoint': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата начальной точки.' },
                    'y': { 'type': 'number', 'description': 'y-координата начальной точки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Начальная точка линии.'
                },
                'endPoint': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата конечной точки.' },
                    'y': { 'type': 'number', 'description': 'y-координата конечной точки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Конечная точка линии.'
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить линию. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета линии.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета линии. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'startPoint', 'endPoint'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateLine(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var startPoint = JsonUtils.RequireObject(args, "startPoint");
            var startX = JsonUtils.RequireDouble(startPoint, "x");
            var startY = JsonUtils.RequireDouble(startPoint, "y");
            var endPoint = JsonUtils.RequireObject(args, "endPoint");
            var endX = JsonUtils.RequireDouble(endPoint, "x");
            var endY = JsonUtils.RequireDouble(endPoint, "y");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание линии \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var line = new DwgLine
                {
                    StartPoint = new Vector3D(startX, startY, 0),
                    EndPoint = new Vector3D(endX, endY, 0)
                };
                drawing.ActiveSpace.Add(line);
                DwgUtils.ApplyEntityLayer(drawing, line, layerName);
                DwgUtils.ApplyEntityColor(line, colorMode, colorIndex);
                if (!line.HasExtensionDictionary)
                    line.CreateExtensionDictionary();
                var extDict = line.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, line);
                return new
                {
                    result = DwgUtils.CreateLineObj(line, guidStr, name),
                    description = "Созданная линия.",
                    status = "Линия успешно создана."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_line",
            Description = "[ЧЕРТЕЖ] Обновляет линию в пространстве активного чертежа. Обновляет только переданные свойства.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор линии (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название линии.' },
                'startPoint': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата начальной точки.' },
                    'y': { 'type': 'number', 'description': 'y-координата начальной точки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Начальная точка линии.'
                },
                'endPoint': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата конечной точки.' },
                    'y': { 'type': 'number', 'description': 'y-координата конечной точки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Конечная точка линии.'
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя линии. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета линии.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета линии. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdateLine(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var startPoint = JsonUtils.GetObject(args, "startPoint", null);
            var endPoint = JsonUtils.GetObject(args, "endPoint", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var (line, currentName) = DwgUtils.FindEntity<DwgLine>(drawing, sessionStorage, guid);
            if (line == null)
                throw new InvalidOperationException($"Не удалось найти линию по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление линии \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!line.HasExtensionDictionary)
                    line.CreateExtensionDictionary();
                var extDict = line.GetExtensionDictionary();
                if (name != null)
                {
                    if (name != currentName)
                        extDict.SetString("name", name);
                }
                else
                {
                    if (currentName == null)
                        extDict.SetString("name", "none");
                }
                if (startPoint != null)
                {
                    var startX = JsonUtils.RequireDouble(startPoint, "x");
                    var startY = JsonUtils.RequireDouble(startPoint, "y");
                    line.StartPoint = new Vector3D(startX, startY, 0);
                }
                if (endPoint != null)
                {
                    var endX = JsonUtils.RequireDouble(endPoint, "x");
                    var endY = JsonUtils.RequireDouble(endPoint, "y");
                    line.EndPoint = new Vector3D(endX, endY, 0);
                }
                DwgUtils.ApplyEntityLayer(drawing, line, layerName);
                DwgUtils.ApplyEntityColor(line, colorMode, colorIndex);
                return new
                {
                    result = DwgUtils.CreateLineObj(line, guidStr, name ?? currentName ?? "none"),
                    description = "Обновленная линия.",
                    status = "Линия успешно обновлена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_create_hatch",
            Description = "[ЧЕРТЕЖ] Создает штриховку и добавляет ее в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название штриховки.' },
                'contours': {
                  'type': 'array',
                  'minItems': 1,
                  'description': 'Массив контуров штриховки. Каждый контур задается массивом 2d точек.',
                  'items': {
                    'type': 'object',
                    'properties': {
                      'points': {
                        'type': 'array',
                        'minItems': 3,
                        'items': {
                          'type': 'object',
                          'properties': {
                            'x': { 'type': 'number', 'description': 'x-координата точки контура.' },
                            'y': { 'type': 'number', 'description': 'y-координата точки контура.' }
                          },
                          'required': ['x', 'y'],
                          'additionalProperties': false
                        }
                      }
                    },
                    'required': ['points'],
                    'additionalProperties': false
                  }
                },
                'patternName': { 'type': 'string', 'description': 'Имя шаблона штриховки. По умолчанию SOLID.' },
                'patternScale': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Масштаб шаблона штриховки.' },
                'patternAngle': { 'type': 'number', 'description': 'Угол шаблона штриховки (в радианах).' },
                'patternType': {
                  'type': 'string',
                  'description': 'Тип шаблона штриховки.',
                  'enum': ['UserDefined', 'PreDefined']
                },
                'hatchStyle': {
                  'type': 'string',
                  'description': 'Стиль штриховки.',
                  'enum': ['Normal', 'Outer', 'Ignore']
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить штриховку. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета штриховки.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета штриховки. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'contours'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateHatch(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var contours = JsonUtils.RequireArray(args, "contours");
            var patternName = JsonUtils.GetString(args, "patternName", "SOLID");
            var patternScale = JsonUtils.GetDouble(args, "patternScale", null);
            var patternAngle = JsonUtils.GetDouble(args, "patternAngle", null);
            var patternTypeString = JsonUtils.GetString(args, "patternType", null);
            var hatchStyleString = JsonUtils.GetString(args, "hatchStyle", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание штриховки \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var hatch = new DwgHatch();
                SetHatchContours(hatch, contours);
                hatch.PatternName = string.IsNullOrWhiteSpace(patternName) ? "SOLID" : patternName;
                if (patternScale != null)
                {
                    if (patternScale.Value <= 0)
                        throw new InvalidOperationException("Масштаб штриховки (patternScale) должен быть больше 0.");
                    hatch.PatternScale = patternScale.Value;
                }
                if (patternAngle != null)
                    hatch.PatternAngle = patternAngle.Value;
                if (patternTypeString != null)
                    hatch.PatternType = DwgUtils.ParsePatternType(patternTypeString);
                if (hatchStyleString != null)
                    hatch.HatchStyle = DwgUtils.ParseHatchStyle(hatchStyleString);
                drawing.ActiveSpace.Add(hatch);
                DwgUtils.ApplyEntityLayer(drawing, hatch, layerName);
                DwgUtils.ApplyEntityColor(hatch, colorMode, colorIndex);
                if (!hatch.HasExtensionDictionary)
                    hatch.CreateExtensionDictionary();
                var extDict = hatch.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, hatch);
                return new
                {
                    result = DwgUtils.CreateHatchObj(hatch, guidStr, name),
                    description = "Созданная штриховка.",
                    status = "Штриховка успешно создана."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_update_hatch",
            Description = "[ЧЕРТЕЖ] Обновляет штриховку в пространстве активного чертежа. Обновляет только переданные свойства.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор штриховки (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название штриховки.' },
                'contours': {
                  'type': 'array',
                  'minItems': 1,
                  'description': 'Массив контуров штриховки. Каждый контур задается массивом 2d точек.',
                  'items': {
                    'type': 'object',
                    'properties': {
                      'points': {
                        'type': 'array',
                        'minItems': 3,
                        'items': {
                          'type': 'object',
                          'properties': {
                            'x': { 'type': 'number', 'description': 'x-координата точки контура.' },
                            'y': { 'type': 'number', 'description': 'y-координата точки контура.' }
                          },
                          'required': ['x', 'y'],
                          'additionalProperties': false
                        }
                      }
                    },
                    'required': ['points'],
                    'additionalProperties': false
                  }
                },
                'patternName': { 'type': 'string', 'description': 'Имя шаблона штриховки.' },
                'patternScale': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Масштаб шаблона штриховки.' },
                'patternAngle': { 'type': 'number', 'description': 'Угол шаблона штриховки (в радианах).' },
                'patternType': {
                  'type': 'string',
                  'description': 'Тип шаблона штриховки.',
                  'enum': ['UserDefined', 'PreDefined']
                },
                'hatchStyle': {
                  'type': 'string',
                  'description': 'Стиль штриховки.',
                  'enum': ['Normal', 'Outer', 'Ignore']
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя штриховки. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета штриховки.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета штриховки. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdateHatch(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var contours = JsonUtils.GetArray(args, "contours", null);
            var patternName = JsonUtils.GetString(args, "patternName", null);
            var patternScale = JsonUtils.GetDouble(args, "patternScale", null);
            var patternAngle = JsonUtils.GetDouble(args, "patternAngle", null);
            var patternTypeString = JsonUtils.GetString(args, "patternType", null);
            var hatchStyleString = JsonUtils.GetString(args, "hatchStyle", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var (hatch, currentName) = DwgUtils.FindEntity<DwgHatch>(drawing, sessionStorage, guid);
            if (hatch == null)
                throw new InvalidOperationException($"Не удалось найти штриховку по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление штриховки \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!hatch.HasExtensionDictionary)
                    hatch.CreateExtensionDictionary();
                var extDict = hatch.GetExtensionDictionary();
                if (name != null)
                {
                    if (name != currentName)
                        extDict.SetString("name", name);
                }
                else
                {
                    if (currentName == null)
                        extDict.SetString("name", "none");
                }
                if (contours != null)
                    SetHatchContours(hatch, contours);
                if (patternName != null)
                {
                    if (string.IsNullOrWhiteSpace(patternName))
                        throw new InvalidOperationException("Имя шаблона штриховки (patternName) не может быть пустым.");
                    hatch.PatternName = patternName;
                }
                if (patternScale != null)
                {
                    if (patternScale.Value <= 0)
                        throw new InvalidOperationException("Масштаб штриховки (patternScale) должен быть больше 0.");
                    hatch.PatternScale = patternScale.Value;
                }
                if (patternAngle != null)
                    hatch.PatternAngle = patternAngle.Value;
                if (patternTypeString != null)
                    hatch.PatternType = DwgUtils.ParsePatternType(patternTypeString);
                if (hatchStyleString != null)
                    hatch.HatchStyle = DwgUtils.ParseHatchStyle(hatchStyleString);
                DwgUtils.ApplyEntityLayer(drawing, hatch, layerName);
                DwgUtils.ApplyEntityColor(hatch, colorMode, colorIndex);
                return new
                {
                    result = DwgUtils.CreateHatchObj(hatch, guidStr, name ?? currentName ?? "none"),
                    description = "Обновленная штриховка.",
                    status = "Штриховка успешно обновлена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        private static void SetHatchContours(DwgHatch hatch, Dictionary<string, object>[] contours)
        {
            if (contours == null || contours.Length == 0)
                throw new InvalidOperationException("Необходимо передать хотя бы один контур штриховки.");
            hatch.BoundaryPath.Clear();
            for (int i = 0; i < contours.Length; i++)
            {
                var contourObject = contours[i];
                var points = JsonUtils.RequireArray(contourObject, "points");
                if (points.Length < 3)
                    throw new InvalidOperationException($"Контур с индексом {i} должен содержать как минимум 3 точки.");
                var contourPoints = new List<Vector2D>(points.Length + 1);
                for (int j = 0; j < points.Length; j++)
                {
                    var pointObject = points[j];
                    var x = JsonUtils.RequireDouble(pointObject, "x");
                    var y = JsonUtils.RequireDouble(pointObject, "y");
                    contourPoints.Add(new Vector2D(x, y));
                }
                var firstPoint = contourPoints[0];
                var lastPoint = contourPoints[contourPoints.Count - 1];
                var isClosed = firstPoint.Equals(lastPoint);
                if (!isClosed)
                    contourPoints.Add(firstPoint);
                var path = new PolylineBoundaryPath
                {
                    IsPolyline = true,
                    LoopType = AcLoopType.Polyline
                };
                for (int j = 0; j < contourPoints.Count; j++)
                {
                    path.Contur.Add(contourPoints[j]);
                }
                hatch.BoundaryPath.Add(path);
            }
        }

        [ToolDef(
            Name = "dwg_get_hatch_patterns",
            Description = "[ЧЕРТЕЖ] Возвращает список доступных паттернов штриховок.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        public object GetHatchPatterns(Dictionary<string, object> args)
        {
            var patternManager = HatchPatternManager.Current ?? throw new InvalidOperationException("Не удалось получить доступ к менеджеру штриховок.");
            var patterns = patternManager.GetDefinedPatterns() ?? Enumerable.Empty<HatchPattern>();
            var patternObjects = patterns
                .Select(pattern => new
                {
                    name = pattern.Name,
                    description = pattern.Description,
                    lines = pattern.Select(line => new
                    {
                        angle = line.Angle,
                        start = new { x = line.StartX, y = line.StartY },
                        delta = new { x = line.DeltaX, y = line.DeltaY },
                        linetypePattern = new
                        {
                            count = line.LinetypePattern?.Count ?? 0,
                            totalLength = line.LinetypePattern?.TotalPatternLength ?? 0
                        }
                    }).ToArray()
                })
                .OrderBy(p => p.name)
                .ToArray();
            return new
            {
                result = new
                {
                    patterns = patternObjects
                },
                description = "Список доступных паттернов штриховок.",
                status = "Паттерны штриховок успешно получены."
            };
        }

        [ToolDef(
            Name = "dwg_remove_active_space_entity",
            Description = "[ЧЕРТЕЖ] Удаляет элемент из пространства активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор элемента чертежа (из активного чертежа).' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object RemoveActiveSpaceEntity(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            DwgEntity entity = null;
            var name = "none";
            if (sessionStorage.HasObject(guid))
            {
                entity = (DwgEntity)sessionStorage.GetObject(guid);
                if (entity.Drawing != drawing)
                    throw new InvalidOperationException($"Элемент с guid \"{guidStr}\" не находится в активном чертеже.");
                sessionStorage.RemoveObject(guid);
                if (entity.HasExtensionDictionary)
                {
                    var exitDict = entity.GetExtensionDictionary();
                    name = exitDict.GetString("name", "none");
                }
            }
            else
            {
                foreach (var e in drawing.ActiveSpace.Entities)
                {
                    if (e.HasExtensionDictionary)
                    {
                        var extDict = e.GetExtensionDictionary();
                        if (string.Equals(guidStr, extDict.GetString("guid", null)))
                        {
                            entity = e;
                            name = extDict.GetString("name", "none");
                            break;
                        }
                    }
                }
            }
            if (entity == null)
                throw new InvalidOperationException($"Не удалось найти элемент по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Удаление элемента \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                drawing.ActiveSpace.Entities.Remove(entity);
                var (type, typeDescription) = DwgUtils.GetEntityType(entity);
                return new
                {
                    result = new
                    {
                        guid = guidStr,
                        name,
                        layerName = entity.Layer?.Name ?? "none",
                        colorMode = DwgUtils.GetColorMode(entity.Color),
                        colorIndex = entity.Color.ColorIndex,
                        type,
                        typeDescription
                    },
                    description = "Удаленный элемент.",
                    status = "Элемент успешно удален из активного чертежа."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }
    }
}
