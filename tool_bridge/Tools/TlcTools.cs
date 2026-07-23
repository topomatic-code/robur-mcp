using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Cad.Foundation;
using Topomatic.Visualization;
using Topomatic.Visualization.Constructions;
using Topomatic.Visualization.Runtime;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class TlcTools : ToolProvider
    {
        [ToolDef(
            Name = "tlc_model_create",
            Description = "[ЧЕРТЕЖ, TLC] Создает Tlc-модель по скрипту и вставляет ее в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название созданной Tlc-модели.' },
                'scriptPath': { 'type': 'string', 'description': 'Полный путь к файлу Tlc-скрипта, из которого нужно создать модель.' },
                'position': {
                  'type': 'object',
                  'description': 'Точка вставки Tlc-модели.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' },
                    'z': { 'type': 'number', 'description': 'z-координата' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'scale': {
                  'type': 'object',
                  'description': 'Масштаб Tlc-модели по осям. По умолчанию { x: 1, y: 1, z: 1 }.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'Масштаб по оси x' },
                    'y': { 'type': 'number', 'description': 'Масштаб по оси y' },
                    'z': { 'type': 'number', 'description': 'Масштаб по оси z' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'normal': {
                  'type': 'object',
                  'description': 'Нормаль плоскости вставки Tlc-модели. По умолчанию { x: 0, y: 0, z: 1 }.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-компонента нормали' },
                    'y': { 'type': 'number', 'description': 'y-компонента нормали' },
                    'z': { 'type': 'number', 'description': 'z-компонента нормали' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'angle': { 'type': 'number', 'description': 'Угол поворота Tlc-модели вокруг нормали (в радианах). По умолчанию 0.' },
                'parameters': {
                  'type': 'object',
                  'description': 'Значения параметров Tlc-модели. Можно передать только изменяемые параметры из схемы, возвращаемой tlc_get_parameter_schema.',
                  'additionalProperties': true
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить Tlc-модель. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета Tlc-модели.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета Tlc-модели. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'scriptPath', 'position'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateTlcModel(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var scriptPath = JsonUtils.RequireString(args, "scriptPath");
            var position = JsonUtils.RequireVector3D(args, "position");
            var scale = JsonUtils.GetVector3D(args, "scale", Vector3D.One).Value;
            var normal = JsonUtils.GetVector3D(args, "normal", Vector3D.UnitZ).Value;
            var angle = JsonUtils.GetDouble(args, "angle", 0.0).Value;
            if (normal.Length <= 1e-9)
                throw new InvalidOperationException("Нормаль Tlc-модели не может быть нулевой.");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Вставка tlc-модели \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var doc = new ConstructionDocument();
                doc.LoadFromFile(scriptPath);
                var tlcModel = doc.CreateModel() as ConstructedModel3dElement ??
                    throw new InvalidOperationException("Скрипт не создал Tlc-модель ожидаемого типа.");
                try
                {
                    tlcModel.BeginUpdate();
                    tlcModel.EndUpdate();
                    SetParameters(tlcModel, args);
                    tlcModel.GetModel();
                }
                catch (Exception ex)
                {
                    return new
                    {
                        result = ex.Message,
                        description = "Текст ошибки.",
                        status = "Возникла ошибка при выполнении скрипта."
                    };
                }
                var tlcEntity = new DwgModel3DElement()
                {
                    Position = position,
                    Scale = scale,
                    Normal = Vector3D.Normalize(normal),
                    Angle = angle,
                    Element = tlcModel
                };
                tlcEntity.Prepare(drawing);
                tlcEntity.Regen(EventArgs.Empty);
                drawing.ActiveSpace.Add(tlcEntity);
                DwgUtils.ApplyEntityLayer(drawing, tlcEntity, layerName);
                DwgUtils.ApplyEntityColor(tlcEntity, colorMode, colorIndex);
                if (!tlcEntity.HasExtensionDictionary)
                    tlcEntity.CreateExtensionDictionary();
                var extDict = tlcEntity.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, tlcEntity);
                return new
                {
                    result = DwgUtils.CreateTlcObj(tlcEntity, guidStr, name),
                    description = "Созданная Tlc-модель.",
                    status = "Tlc-модель успешно создана."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "tlc_model_update",
            Description = "[ЧЕРТЕЖ, TLC] Обновляет Tlc-модель в пространстве активного чертежа. Обновляет только переданные свойства, оставляя остальные без изменений.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор Tlc-модели (из активного чертежа).' },
                'name': { 'type': 'string', 'description': 'Название Tlc-модели.' },
                'scriptPath': { 'type': 'string', 'description': 'Полный путь к файлу Tlc-скрипта, из которого нужно обновить модель.' },
                'position': {
                  'type': 'object',
                  'description': 'Точка вставки Tlc-модели.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' },
                    'z': { 'type': 'number', 'description': 'z-координата' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'scale': {
                  'type': 'object',
                  'description': 'Масштаб Tlc-модели по осям.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'Масштаб по оси x' },
                    'y': { 'type': 'number', 'description': 'Масштаб по оси y' },
                    'z': { 'type': 'number', 'description': 'Масштаб по оси z' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'normal': {
                  'type': 'object',
                  'description': 'Нормаль плоскости вставки Tlc-модели.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-компонента нормали' },
                    'y': { 'type': 'number', 'description': 'y-компонента нормали' },
                    'z': { 'type': 'number', 'description': 'z-компонента нормали' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'angle': { 'type': 'number', 'description': 'Угол поворота Tlc-модели вокруг нормали (в радианах).' },
                'parameters': {
                  'type': 'object',
                  'description': 'Значения параметров Tlc-модели. Можно передать только изменяемые параметры из схемы, возвращаемой tlc_get_parameter_schema.',
                  'additionalProperties': true
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя Tlc-модели. Если не задано, слой не изменяется.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета Tlc-модели.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета Tlc-модели. Используется только при colorMode = Indexed.' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = true
        )]
        public object UpdateTlcModel(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var scriptPath = JsonUtils.GetString(args, "scriptPath", null);
            var position = JsonUtils.GetVector3D(args, "position", null);
            var scale = JsonUtils.GetVector3D(args, "scale", null);
            var normal = JsonUtils.GetVector3D(args, "normal", null);
            var angle = JsonUtils.GetDouble(args, "angle", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            if (normal != null && normal.Value.Length <= 1e-9)
                throw new InvalidOperationException("Нормаль Tlc-модели не может быть нулевой.");
            var (tlcEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            if (tlcEntity == null)
                throw new InvalidOperationException($"Не удалось найти Tlc-модель по указанному guid \"{guidStr}\".");
            var tlcModel = tlcEntity.Element as ConstructedModel3dElement ??
                throw new InvalidOperationException($"Не удалось найти Tlc-модель по указанному guid \"{guidStr}\".");
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Обновление Tlc-модели \"{name ?? currentName ?? "none"}\""));
            else
                drawing.BeginUpdate();
            try
            {
                if (!tlcEntity.HasExtensionDictionary)
                    tlcEntity.CreateExtensionDictionary();
                var extDict = tlcEntity.GetExtensionDictionary();
                var resultName = name ?? currentName ?? "none";
                extDict.SetString("name", resultName);
                if (position != null)
                    tlcEntity.Position = position.Value;
                if (scale != null)
                    tlcEntity.Scale = scale.Value;
                if (normal != null)
                    tlcEntity.Normal = Vector3D.Normalize(normal.Value);
                if (angle != null)
                    tlcEntity.Angle = angle.Value;
                DwgUtils.ApplyEntityLayer(drawing, tlcEntity, layerName);
                DwgUtils.ApplyEntityColor(tlcEntity, colorMode, colorIndex);
                try
                {
                    tlcEntity.BeginChange();
                    try
                    {
                        if (scriptPath != null)
                        {
                            var doc = new ConstructionDocument();
                            doc.LoadFromFile(scriptPath);
                            var curProps = tlcModel.GetAllProperties();
                            tlcModel = doc.CreateModel() as ConstructedModel3dElement ??
                                throw new InvalidOperationException("Скрипт не создал Tlc-модель ожидаемого типа.");
                            tlcModel.BeginUpdate();
                            tlcModel.EndUpdate();
                            tlcModel.BeginUpdate();
                            try
                            {
                                tlcModel.ApplayOverridedProperties(curProps);
                            }
                            finally
                            {
                                tlcModel.EndUpdate();
                            }
                            tlcEntity.Element = tlcModel;
                        }
                        SetParameters(tlcModel, args);
                        tlcModel.GetModel();
                    }
                    finally
                    {
                        tlcEntity.EndChange();
                        tlcEntity.Regen(EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    return new
                    {
                        result = ex.Message,
                        description = "Текст ошибки.",
                        status = "Возникла ошибка при обновлении Tlc-модели."
                    };
                }
                return new
                {
                    result = DwgUtils.CreateTlcObj(tlcEntity, guidStr, resultName),
                    description = "Обновленная Tlc-модель.",
                    status = "Tlc-модель успешно обновлена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "tlc_script_execute",
            Description = "[TLC] Выполняет Tlc-скрипт для проверки ошибок построения модели.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'scriptPath': { 'type': 'string', 'description': 'Полный путь к файлу Tlc-скрипта, который нужно выполнить и проверить.' },
                'parameters': {
                  'type': 'object',
                  'description': 'Значения параметров Tlc-модели. Можно передать только изменяемые параметры из схемы, возвращаемой tlc_get_parameter_schema.',
                  'additionalProperties': true
                }
              },
              'required': ['scriptPath'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object ExecuteTlcScript(Dictionary<string, object> args)
        {
            var scriptPath = JsonUtils.RequireString(args, "scriptPath");
            var doc = new ConstructionDocument();
            doc.LoadFromFile(scriptPath);
            var tlcModel = doc.CreateModel() as ConstructedModel3dElement ??
                throw new InvalidOperationException("Скрипт не создал Tlc-модель ожидаемого типа.");
            var meshBounds = BoundingBox3D.Empty;
            try
            {
                tlcModel.BeginUpdate();
                tlcModel.EndUpdate();
                SetParameters(tlcModel, args);
                var geometryModel = tlcModel.GetModel();
                if (geometryModel != null)
                    meshBounds = geometryModel.GetBounds();
                return new
                {
                    result = new
                    {
                        name = tlcModel.Name,
                        properties = SmdxUtils.CreatePropsArray(tlcModel.GetAllProperties()),
                        meshBounds = DwgUtils.CreateBounds3DObj(meshBounds)
                    },
                    description = "Результат выполнения Tlc-скрипта.",
                    status = "Скрипт успешно выполнен без ошибок."
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    result = ex.Message,
                    description = "Текст ошибки.",
                    status = "Возникла ошибка при выполнении скрипта."
                };
            }
        }

        [ToolDef(
            Name = "tlc_model_get_script",
            Description = "[ЧЕРТЕЖ, TLC] Возвращает Tlc-скрипт из Tlc-модели, вставленной в активный чертеж.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор Tlc-модели (из активного чертежа).' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetTlcModelScript(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var (tlcEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            if (tlcEntity == null)
                throw new InvalidOperationException($"Не удалось найти Tlc-модель по указанному guid \"{guidStr}\".");
            var tlcModel = tlcEntity.Element as ConstructedModel3dElement ??
                throw new InvalidOperationException($"Не удалось найти Tlc-модель по указанному guid \"{guidStr}\".");
            var document = tlcModel.Document ??
                throw new InvalidOperationException($"У Tlc-модели с guid \"{guidStr}\" отсутствует документ со скриптом.");
            return new
            {
                result = new
                {
                    guid = guidStr,
                    name = currentName ?? tlcModel.Name ?? "none",
                    scriptPath = document.Name,
                    modules = document.Modules,
                    script = document.Script
                },
                description = "Текст Tlc-скрипта из модели.",
                status = "Текст Tlc-скрипта успешно получен."
            };
        }

        [ToolDef(
            Name = "tlc_get_parameter_schema",
            Description = "[ЧЕРТЕЖ, TLC] Возвращает схему параметров Tlc-модели из Tlc-скрипта или из модели, вставленной в активный чертеж.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'source': {
                  'type': 'string',
                  'description': 'Источник получения схемы параметров: Script - из файла Tlc-скрипта, Model - из Tlc-модели в активном чертеже.',
                  'enum': ['Script', 'Model']
                },
                'scriptPath': { 'type': 'string', 'description': 'Полный путь к файлу Tlc-скрипта. Требуется при source = Script.' },
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор Tlc-модели из активного чертежа. Требуется при source = Model.' }
              },
              'required': ['source'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetTlcParameterSchema(Dictionary<string, object> args)
        {
            var source = JsonUtils.RequireString(args, "source");
            ImProperties properties;
            if (string.Equals(source, "Script", StringComparison.OrdinalIgnoreCase))
            {
                var scriptPath = JsonUtils.RequireString(args, "scriptPath");
                var doc = new ConstructionDocument();
                doc.LoadFromFile(scriptPath);
                var tlcModel = doc.CreateModel() as ConstructedModel3dElement ??
                    throw new InvalidOperationException("Скрипт не создал Tlc-модель ожидаемого типа.");
                try
                {
                    tlcModel.BeginUpdate();
                    tlcModel.EndUpdate();
                    properties = tlcModel.GetAllProperties();
                }
                catch (Exception ex)
                {
                    return new
                    {
                        result = ex.Message,
                        description = "Текст ошибки.",
                        status = "Возникла ошибка при выполнении скрипта."
                    };
                }
            }
            else if (string.Equals(source, "Model", StringComparison.OrdinalIgnoreCase))
            {
                var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
                var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
                var guidStr = JsonUtils.RequireString(args, "guid");
                var guid = Guid.Parse(guidStr);
                var (tlcEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
                if (tlcEntity == null)
                    throw new InvalidOperationException($"Не удалось найти Tlc-модель по указанному guid \"{guidStr}\".");
                var tlcModel = tlcEntity.Element as ConstructedModel3dElement ??
                    throw new InvalidOperationException($"Не удалось найти Tlc-модель по указанному guid \"{guidStr}\".");
                properties = tlcModel.GetAllProperties();
            }
            else
            {
                throw new InvalidOperationException("Неизвестное значение source. Допустимые значения: Script, Model.");
            }
            return new
            {
                result = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = GeneratePropertySchema(properties),
                    ["additionalProperties"] = false
                },
                description = "Схема параметров Tlc-модели.",
                status = "Схема параметров успешно получена."
            };
        }

        private object GeneratePropertySchema(ImProperties properties)
        {
            var schemaProperties = new Dictionary<string, object>();
            foreach (var property in properties)
            {
                if (string.IsNullOrWhiteSpace(property.Tag))
                    continue;
                var schema = new Dictionary<string, object>();
                switch (property.Info)
                {
                    case FloatPropertyInfo floatProp:
                        if (floatProp.Layout == ImPropertyLayout.Single)
                        {
                            schema["type"] = "number";
                            if (floatProp.Low != null)
                                schema["minimum"] = floatProp.Low.Value;
                            if (floatProp.High != null)
                                schema["maximum"] = floatProp.High.Value;
                            if (property.Value != null)
                                schema["default"] = Convert.ToDouble(property.Value);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    case IntegerPropertyInfo intProp:
                        if (intProp.Layout == ImPropertyLayout.Single)
                        {
                            schema["type"] = "integer";
                            if (intProp.Low != null)
                                schema["minimum"] = intProp.Low.Value;
                            if (intProp.High != null)
                                schema["maximum"] = intProp.High.Value;
                            if (property.Value != null)
                                schema["default"] = Convert.ToInt32(property.Value);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    case BooleanPropertyInfo boolBrop:
                        if (boolBrop.Layout == ImPropertyLayout.Single)
                        {
                            schema["type"] = "boolean";
                            if (property.Value != null)
                                schema["default"] = Convert.ToBoolean(property.Value);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    case StringPropertyInfo stringProp:
                        if (stringProp.Layout == ImPropertyLayout.Single)
                        {
                            schema["type"] = "string";
                            if (property.Value != null)
                                schema["default"] = Convert.ToString(property.Value);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    case EnumerationPropertyInfo enumProp:
                        if (enumProp.Layout == ImPropertyLayout.Single)
                        {
                            schema["type"] = "string";
                            schema["enum"] = enumProp.Values.Keys.ToArray();
                            if (property.Value != null)
                                schema["default"] = Convert.ToString(property.Value);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    case ReferencePropertyInfo _:
                        throw new NotImplementedException();
                    case TypedPropertyInfo typedProp:
                        var typeDescriptor = typedProp.Type;
                        var typeProperties = typeDescriptor.GetAllProperties();
                        if (typedProp.Layout == ImPropertyLayout.Single)
                        {
                            schema["type"] = "object";
                            schema["properties"] = GeneratePropertySchema(typeProperties);
                            schema["additionalProperties"] = false;
                        }
                        else if (typedProp.Layout == ImPropertyLayout.List)
                        {
                            schema["type"] = "array";
                            schema["items"] = new Dictionary<string, object>()
                            {
                                ["type"] = "object",
                                ["properties"] = GeneratePropertySchema(typeProperties),
                                ["additionalProperties"] = false
                            };
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    case null:
                        if (property.Value is double d)
                        {
                            schema["type"] = "number";
                            schema["default"] = d;
                        }
                        else if (property.Value is int i)
                        {
                            schema["type"] = "integer";
                            schema["default"] = i;
                        }
                        else if (property.Value is bool b)
                        {
                            schema["type"] = "boolean";
                            schema["default"] = b;
                        }
                        else if (property.Value is string s)
                        {
                            schema["type"] = "string";
                            schema["default"] = s;
                        }
                        else
                        {
                            throw new NotSupportedException("Unexpected property value type.");
                        }
                        break;
                    default:
                        throw new NotSupportedException("Unexpected property type.");
                }
                schemaProperties[property.Tag] = schema;
            }
            return schemaProperties;
        }

        private void SetParameters(ConstructedModel3dElement tlcModel, Dictionary<string, object> args)
        {
            var parameters = JsonUtils.GetObject(args, "parameters", null);
            if (parameters != null)
            {
                var properties = tlcModel.GetAllProperties();
                var updatedProps = GenerateProperties(properties, parameters);
                if (updatedProps.Count > 0)
                {
                    tlcModel.BeginUpdate();
                    try
                    {
                        tlcModel.ApplayOverridedProperties(updatedProps);
                    }
                    finally
                    {
                        tlcModel.EndUpdate();
                    }
                }
            }
        }

        private ImProperties GenerateProperties(ImProperties source, Dictionary<string, object> parameters)
        {
            var result = new ImProperties();
            foreach (var property in source)
            {
                if (string.IsNullOrWhiteSpace(property.Tag) || !parameters.ContainsKey(property.Tag))
                    continue;
                var updatedProp = property.Clone();
                switch (property.Info)
                {
                    case FloatPropertyInfo _:
                        updatedProp.Value = JsonUtils.RequireDouble(parameters, property.Tag);
                        result.Add(updatedProp);
                        break;
                    case IntegerPropertyInfo _:
                        updatedProp.Value = JsonUtils.RequireInt(parameters, property.Tag);
                        result.Add(updatedProp);
                        break;
                    case BooleanPropertyInfo _:
                        updatedProp.Value = JsonUtils.RequireBool(parameters, property.Tag);
                        result.Add(updatedProp);
                        break;
                    case StringPropertyInfo _:
                        updatedProp.Value = JsonUtils.RequireString(parameters, property.Tag);
                        result.Add(updatedProp);
                        break;
                    case EnumerationPropertyInfo _:
                        updatedProp.Value = JsonUtils.RequireString(parameters, property.Tag);
                        result.Add(updatedProp);
                        break;
                    case ReferencePropertyInfo _:
                        break;
                    case TypedPropertyInfo typedProp:
                        if (typedProp.Layout == ImPropertyLayout.Single)
                        {
                            var value = ImAggregates.Create(typedProp.Type);
                            value.FillProperties();
                            var overridedProperties = GenerateProperties(value.Properties, JsonUtils.RequireObject(parameters, property.Tag));
                            value.ApplayOverridedProperties(overridedProperties);
                            updatedProp.Value = value;
                            result.Add(updatedProp);
                        }
                        else if (typedProp.Layout == ImPropertyLayout.List)
                        {
                            var items = JsonUtils.RequireArray(parameters, property.Tag);
                            var values = new object[items.Length];
                            for (int i = 0; i < items.Length; i++)
                            {
                                var value = ImAggregates.Create(typedProp.Type);
                                value.FillProperties();
                                var overridedProperties = GenerateProperties(value.Properties, items[i]);
                                value.ApplayOverridedProperties(overridedProperties);
                                values[i] = value;
                            }
                            updatedProp.Value = values;
                            result.Add(updatedProp);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    case null:
                        if (property.Value is double)
                        {
                            updatedProp.Value = JsonUtils.RequireDouble(parameters, property.Tag);
                            result.Add(updatedProp);
                        }
                        else if (property.Value is int)
                        {
                            updatedProp.Value = JsonUtils.RequireInt(parameters, property.Tag);
                            result.Add(updatedProp);
                        }
                        else if (property.Value is bool)
                        {
                            updatedProp.Value = JsonUtils.RequireBool(parameters, property.Tag);
                            result.Add(updatedProp);
                        }
                        else if (property.Value is string)
                        {
                            updatedProp.Value = JsonUtils.RequireString(parameters, property.Tag);
                            result.Add(updatedProp);
                        }
                        else
                        {
                            throw new NotSupportedException("Unexpected property value type.");
                        }
                        break;
                    default:
                        throw new NotSupportedException("Unexpected property type.");
                }
            }
            return result;
        }
    }
}
