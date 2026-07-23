using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class BlockTools : ToolProvider
    {
        [ToolDef(
            Name = "dwg_block_create",
            Description = "[ЧЕРТЕЖ] Создает блок в таблице блоков активного чертежа Robur из элементов чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Имя создаваемого блока.' },
                'entities': {
                  'type': 'array',
                  'minItems': 1,
                  'description': 'Массив guid-идентификаторов элементов чертежа, которые нужно скопировать в создаваемый блок.',
                  'items': { 'type': 'string' }
                }
              },
              'required': ['name', 'entities'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateBlock(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var entities = JsonUtils.RequireStringArray(args, "entities");
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Имя блока не может быть пустым.");
            if (entities.Length == 0)
                throw new InvalidOperationException("Необходимо передать хотя бы один guid элемента чертежа.");
            if (drawing.Blocks.IsExists(name))
                throw new InvalidOperationException($"Блок с именем {name} уже содержится в таблице блоков чертежа.");
            var sourceEntities = new List<DwgEntity>(entities.Length);
            foreach (var entityGuidStr in entities)
            {
                if (!Guid.TryParse(entityGuidStr, out var entityGuid))
                    throw new InvalidOperationException($"Некорректный guid элемента чертежа \"{entityGuidStr}\".");
                var (entity, currentName) = DwgUtils.FindEntity<DwgEntity>(drawing, sessionStorage, entityGuid);
                if (entity == null)
                    throw new InvalidOperationException($"Не удалось найти элемент по указанному guid \"{entityGuidStr}\".");
                sourceEntities.Add(entity);
            }
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание блока чертежа \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var block = drawing.Blocks.Add(name) ?? throw new InvalidOperationException("Не удалось создать блок.");
                var refContext = new ReferencesContext(drawing);
                block.Entities.CopyFrom(sourceEntities, e => e.Layer = null, refContext);
                return new
                {
                    result = CreateBlockObj(block),
                    description = "Созданный блок.",
                    status = "Блок успешно создан в таблице блоков."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_block_insert",
            Description = "[ЧЕРТЕЖ] Вставляет блок в пространство активного чертежа Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название вставки блока.' },
                'blockName': { 'type': 'string', 'description': 'Имя блока из таблицы блоков активного чертежа.' },
                'position': {
                  'type': 'object',
                  'description': 'Точка вставки блока.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата точки вставки.' },
                    'y': { 'type': 'number', 'description': 'y-координата точки вставки.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false
                },
                'rotation': { 'type': 'number', 'description': 'Угол поворота вставки блока в радианах. Если не задан, используется 0.' },
                'scale': {
                  'type': 'object',
                  'description': 'Масштаб вставки блока по осям. Если не задан, используется 1 по всем осям.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'Масштаб по оси X.' },
                    'y': { 'type': 'number', 'description': 'Масштаб по оси Y.' },
                    'z': { 'type': 'number', 'description': 'Масштаб по оси Z.' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить вставку блока. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета вставки блока.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета вставки блока. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'blockName', 'position'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object InsertBlock(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var blockName = JsonUtils.RequireString(args, "blockName");
            var position = JsonUtils.RequireObject(args, "position");
            var positionX = JsonUtils.RequireDouble(position, "x");
            var positionY = JsonUtils.RequireDouble(position, "y");
            var rotation = JsonUtils.GetDouble(args, "rotation", 0);
            var scale = JsonUtils.GetObject(args, "scale", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Название вставки блока не может быть пустым.");
            if (string.IsNullOrWhiteSpace(blockName))
                throw new InvalidOperationException("Имя блока не может быть пустым.");
            if (!drawing.Blocks.IsExists(blockName))
                throw new InvalidOperationException($"Блок с именем {blockName} не содержится в таблице блоков чертежа.");
            var block = drawing.Blocks[blockName] ?? throw new InvalidOperationException($"Не удалось получить блок с именем {blockName}.");
            var scaleVector = new Vector3D(1, 1, 1);
            if (scale != null)
            {
                var scaleX = JsonUtils.RequireDouble(scale, "x");
                var scaleY = JsonUtils.RequireDouble(scale, "y");
                var scaleZ = JsonUtils.RequireDouble(scale, "z");
                if (scaleX == 0 || scaleY == 0 || scaleZ == 0)
                    throw new InvalidOperationException("Масштаб вставки блока не может быть равен 0.");
                scaleVector = new Vector3D(scaleX, scaleY, scaleZ);
            }
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Вставка блока \"{blockName}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var insert = new DwgInsert
                {
                    Block = block,
                    Position = new Vector3D(positionX, positionY, 0),
                    Rotation = rotation.Value,
                    Scale = scaleVector
                };
                drawing.ActiveSpace.Add(insert);
                DwgUtils.ApplyEntityLayer(drawing, insert, layerName);
                DwgUtils.ApplyEntityColor(insert, colorMode, colorIndex);
                if (!insert.HasExtensionDictionary)
                    insert.CreateExtensionDictionary();
                var extDict = insert.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, insert);
                return new
                {
                    result = DwgUtils.CreateBlockInsertObj(insert, guidStr, name),
                    description = "Созданная вставка блока.",
                    status = "Блок успешно вставлен в пространство активного чертежа."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_block_explode",
            Description = "[ЧЕРТЕЖ] Взрывает вставку блока: удаляет вставку и добавляет в пространство активного чертежа копии сущностей блока с учетом положения, поворота и масштаба вставки.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор вставки блока (из активного чертежа).' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object ExplodeBlock(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            if (!Guid.TryParse(guidStr, out var guid))
                throw new InvalidOperationException($"Некорректный guid вставки блока \"{guidStr}\".");
            var (insert, currentName) = DwgUtils.FindEntity<DwgInsert>(drawing, sessionStorage, guid);
            if (insert == null)
                throw new InvalidOperationException($"Не удалось найти вставку блока по указанному guid \"{guidStr}\".");
            var block = insert.Block ?? throw new InvalidOperationException($"Вставка блока с guid \"{guidStr}\" не ссылается на блок.");
            var removedInsertInfo = DwgUtils.CreateEntityInfoObj(insert, guidStr, currentName ?? "none");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Взрыв вставки блока \"{block.Name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var copiedEntities = new List<DwgEntity>();
                var refContext = new ReferencesContext(drawing);
                drawing.ActiveSpace.Entities.CopyFrom(block.Entities, copiedEntities.Add, refContext);
                for (int i = copiedEntities.Count - 1; i >= 0; i--)
                {
                    var entity = copiedEntities[i];
                    try
                    {
                        var origin = new Vector2D(0.0, 0.0);
                        entity.ScaleEntity(origin, insert.XScaleFactor, insert.YScaleFactor);
                        entity.Rotate(origin, insert.Rotation);
                        entity.Move(insert.Position.X, insert.Position.Y, insert.Position.Z);
                        // Возможно лучше использовать Transform?
                        //entity.Transform(insert.Matrix);
                    }
                    catch
                    {
                        // При преобразовании сущностей чертежа возможны исключения.
                        drawing.ActiveSpace.Entities.Remove(entity);
                        copiedEntities.RemoveAt(i);
                    }
                }
                if (!drawing.ActiveSpace.Entities.Remove(insert))
                    throw new InvalidOperationException($"Не удалось удалить вставку блока с guid \"{guidStr}\".");
                sessionStorage.RemoveObject(guid);
                var insertedEntitiesInfo = copiedEntities.Select(entity =>
                {
                    var entityGuid = Guid.NewGuid();
                    var entityGuidStr = entityGuid.ToString();
                    if (!entity.HasExtensionDictionary)
                        entity.CreateExtensionDictionary();
                    var extDict = entity.GetExtensionDictionary();
                    var entityName = extDict.GetString("name", "none");
                    extDict.SetString("guid", entityGuidStr);
                    extDict.SetString("name", entityName);
                    sessionStorage.AddObject(entityGuid, entity);
                    return DwgUtils.CreateEntityInfoObj(entity, entityGuidStr, entityName);
                }).ToArray();
                return new
                {
                    result = new
                    {
                        removedInsertInfo,
                        insertedEntityCount = insertedEntitiesInfo.Length,
                        insertedEntitiesInfo
                    },
                    description = "Результат взрыва вставки блока.",
                    status = "Вставка блока успешно взорвана."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_block_remove",
            Description = "[ЧЕРТЕЖ] Удаляет блок из таблицы блоков активного чертежа Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Имя удаляемого блока.' }
              },
              'required': ['name'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object RemoveBlock(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var name = JsonUtils.RequireString(args, "name");
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Имя блока не может быть пустым.");
            if (!drawing.Blocks.IsExists(name))
                throw new InvalidOperationException($"Блок с именем {name} не содержится в таблице блоков чертежа.");
            var block = drawing.Blocks[name] ?? throw new InvalidOperationException($"Не удалось получить блок с именем {name}.");
            var result = CreateBlockObj(block);
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Удаление блока чертежа \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!drawing.Blocks.Remove(name))
                    throw new InvalidOperationException($"Не удалось удалить блок с именем {name}.");
                return new
                {
                    result,
                    description = "Удаленный блок.",
                    status = "Блок успешно удален из таблицы блоков."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_block_list",
            Description = "[ЧЕРТЕЖ] Возвращает список блоков из таблицы блоков активного чертежа Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetBlocks(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var blocks = drawing.Blocks.Select(CreateBlockObj).ToArray();
            return new
            {
                result = new
                {
                    blockCount = blocks.Length,
                    blocks
                },
                description = "Блоки активного чертежа.",
                status = "Список блоков успешно получен."
            };
        }

        private static object CreateBlockObj(DwgBlock block)
        {
            return new
            {
                block.Name,
                entityCount = block.Entities.Count,
                bounds = new
                {
                    left = block.Bounds.Left,
                    right = block.Bounds.Right,
                    top = block.Bounds.Top,
                    bottom = block.Bounds.Bottom
                }
            };
        }

    }
}
