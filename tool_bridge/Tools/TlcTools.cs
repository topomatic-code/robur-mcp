using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Topomatic.Cad.Foundation;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Utils;
using Topomatic.Visualization;
using Topomatic.Visualization.Constructions;
using Topomatic.Visualization.Runtime;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class TlcTools : ToolProvider
    {
        [ToolDef(
            Name = "tlc_model_create",
            Domain = ToolDomains.Tlc,
            Description = "Создает Tlc-модель по скрипту и вставляет ее в пространство активного чертежа.",
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
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета Tlc-модели. Используется только при colorMode = Indexed.' },
                'includeConsoleOutput': { 'type': 'boolean', 'description': 'Включать ли консольный вывод Tlc-скрипта в поле consoleOutput результата. По умолчанию false.', 'default': false }
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
            var drawing = DwgUtils.RequireDrawing(CadView);
            var sessionStorage = DwgUtils.RequireSessionStorage(SessionStorage);
            var name = JsonUtils.RequireString(args, "name");
            var scriptPath = JsonUtils.RequireString(args, "scriptPath");
            var position = JsonUtils.RequireVector3D(args, "position");
            var scale = JsonUtils.GetVector3D(args, "scale", Vector3D.One).Value;
            var normal = JsonUtils.GetVector3D(args, "normal", Vector3D.UnitZ).Value;
            var angle = JsonUtils.GetDouble(args, "angle", 0.0).Value;
            if (normal.Length <= 1e-9)
                throw new BadRequestException("Нормаль Tlc-модели не может быть нулевой.");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var includeConsoleOutput = JsonUtils.GetBool(args, "includeConsoleOutput", false).Value;
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Вставка Tlc-модели \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var tlcModel = LoadTlcModel(scriptPath);
                string consoleOutput = null;
                try
                {
                    var firstOutput = string.Empty;
                    using (var console = new ConsoleReader())
                    {
                        tlcModel.BeginUpdate();
                        tlcModel.EndUpdate();
                        firstOutput = console.Content;
                    }
                    var secondOutput = string.Empty;
                    using (var console = new ConsoleReader())
                    {
                        SetParameters(tlcModel, args);
                        secondOutput = console.Content;
                    }

                    consoleOutput = string.IsNullOrWhiteSpace(secondOutput) ? firstOutput : secondOutput;

                    tlcModel.GetModel();
                }
                catch (Exception ex)
                {
                    return CreateTlcDiagnosticResponse(ex);
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
                    result = DwgUtils.CreateTlcObj(
                        tlcEntity,
                        guidStr,
                        name,
                        includeConsoleOutput ? consoleOutput : null),
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
            Domain = ToolDomains.Tlc,
            Description = "Обновляет Tlc-модель в пространстве активного чертежа. Обновляет только переданные свойства, оставляя остальные без изменений.",
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
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета Tlc-модели. Используется только при colorMode = Indexed.' },
                'includeConsoleOutput': { 'type': 'boolean', 'description': 'Включать ли консольный вывод Tlc-скрипта в поле consoleOutput результата. По умолчанию false.', 'default': false }
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
            var drawing = DwgUtils.RequireDrawing(CadView);
            var sessionStorage = DwgUtils.RequireSessionStorage(SessionStorage);
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = DwgUtils.ParseGuid(guidStr);
            var name = JsonUtils.GetString(args, "name", null);
            var scriptPath = JsonUtils.GetString(args, "scriptPath", null);
            var position = JsonUtils.GetVector3D(args, "position", null);
            var scale = JsonUtils.GetVector3D(args, "scale", null);
            var normal = JsonUtils.GetVector3D(args, "normal", null);
            var angle = JsonUtils.GetDouble(args, "angle", null);
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var includeConsoleOutput = JsonUtils.GetBool(args, "includeConsoleOutput", false).Value;
            if (normal != null && normal.Value.Length <= 1e-9)
                throw new BadRequestException("Нормаль Tlc-модели не может быть нулевой.");
            var (tlcEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            var tlcModel = DwgUtils.RequireTlcElement(tlcEntity, guidStr);
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
                string consoleOutput = null;
                try
                {
                    tlcEntity.BeginChange();
                    try
                    {
                        var firstOutput = string.Empty;
                        var secondOutput = string.Empty;
                        if (scriptPath != null)
                        {
                            var curProps = tlcModel.GetAllProperties();
                            tlcModel = LoadTlcModel(scriptPath);

                            using (var console = new ConsoleReader())
                            {
                                tlcModel.BeginUpdate();
                                tlcModel.EndUpdate();
                                firstOutput = console.Content;
                            }

                            using (var console = new ConsoleReader())
                            {
                                tlcModel.BeginUpdate();
                                try
                                {
                                    tlcModel.ApplayOverridedProperties(curProps);
                                }
                                finally
                                {
                                    tlcModel.EndUpdate();
                                }
                                secondOutput = console.Content;
                            }

                            tlcEntity.Element = tlcModel;
                        }

                        var thirdOutput = string.Empty;
                        using (var console = new ConsoleReader())
                        {
                            SetParameters(tlcModel, args);
                            thirdOutput = console.Content;
                        }

                        if (!string.IsNullOrWhiteSpace(thirdOutput))
                            consoleOutput = thirdOutput;
                        else if (!string.IsNullOrWhiteSpace(secondOutput))
                            consoleOutput = secondOutput;
                        else
                            consoleOutput = firstOutput;

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
                    return CreateTlcDiagnosticResponse(ex);
                }
                return new
                {
                    result = DwgUtils.CreateTlcObj(
                        tlcEntity,
                        guidStr,
                        resultName,
                        includeConsoleOutput ? consoleOutput : null),
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
            Domain = ToolDomains.Tlc,
            Description = "Выполняет Tlc-скрипт для проверки ошибок построения модели.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'scriptPath': { 'type': 'string', 'description': 'Полный путь к файлу Tlc-скрипта, который нужно выполнить и проверить.' },
                'parameters': {
                  'type': 'object',
                  'description': 'Значения параметров Tlc-модели. Можно передать только изменяемые параметры из схемы, возвращаемой tlc_get_parameter_schema.',
                  'additionalProperties': true
                },
                'includeConsoleOutput': { 'type': 'boolean', 'description': 'Включать ли консольный вывод Tlc-скрипта в поле consoleOutput результата. По умолчанию false.', 'default': false }
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
            var includeConsoleOutput = JsonUtils.GetBool(args, "includeConsoleOutput", false).Value;
            var tlcModel = LoadTlcModel(scriptPath);
            var meshBounds = BoundingBox3D.Empty;
            try
            {
                var firstOutput = string.Empty;
                using (var console = new ConsoleReader())
                {
                    tlcModel.BeginUpdate();
                    tlcModel.EndUpdate();
                    firstOutput = console.Content;
                }
                var secondOutput = string.Empty;
                using (var console = new ConsoleReader())
                {
                    SetParameters(tlcModel, args);
                    secondOutput = console.Content;
                }

                var consoleOutput = string.IsNullOrWhiteSpace(secondOutput) ? firstOutput : secondOutput;
                var geometryModel = tlcModel.GetModel();
                if (geometryModel != null)
                    meshBounds = geometryModel.GetBounds();
                var result = new Dictionary<string, object>
                {
                    ["name"] = tlcModel.Name,
                    ["properties"] = SmdxUtils.CreatePropsArray(tlcModel.GetAllProperties()),
                    ["meshBounds"] = DwgUtils.CreateBounds3DObj(meshBounds)
                };
                if (includeConsoleOutput)
                    result["consoleOutput"] = consoleOutput;
                return new
                {
                    result,
                    description = "Результат выполнения Tlc-скрипта.",
                    status = "Скрипт успешно выполнен без ошибок."
                };
            }
            catch (Exception ex)
            {
                return CreateTlcDiagnosticResponse(ex);
            }
        }

        [ToolDef(
            Name = "tlc_model_get_script",
            Domain = ToolDomains.Tlc,
            Description = "Возвращает Tlc-скрипт из Tlc-модели, вставленной в активный чертеж.",
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
            var drawing = DwgUtils.RequireDrawing(CadView);
            var sessionStorage = DwgUtils.RequireSessionStorage(SessionStorage);
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = DwgUtils.ParseGuid(guidStr);
            var (tlcEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            var tlcModel = DwgUtils.RequireTlcElement(tlcEntity, guidStr);
            var document = tlcModel.Document ??
                throw new PreconditionFailedException($"У Tlc-модели с guid \"{guidStr}\" отсутствует документ со скриптом.");
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
            Domain = ToolDomains.Tlc,
            Description = "Возвращает схему параметров Tlc-модели из Tlc-скрипта или из модели, вставленной в активный чертеж.",
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
                var tlcModel = LoadTlcModel(scriptPath);
                try
                {
                    tlcModel.BeginUpdate();
                    tlcModel.EndUpdate();
                    properties = tlcModel.GetAllProperties();
                }
                catch (Exception ex)
                {
                    return CreateTlcDiagnosticResponse(ex);
                }
            }
            else if (string.Equals(source, "Model", StringComparison.OrdinalIgnoreCase))
            {
                var drawing = DwgUtils.RequireDrawing(CadView);
                var sessionStorage = DwgUtils.RequireSessionStorage(SessionStorage);
                var guidStr = JsonUtils.RequireString(args, "guid");
                var guid = DwgUtils.ParseGuid(guidStr);
                var (tlcEntity, _) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
                var tlcModel = DwgUtils.RequireTlcElement(tlcEntity, guidStr);
                properties = tlcModel.GetAllProperties();
            }
            else
            {
                throw new BadRequestException("Неизвестное значение source. Допустимые значения: Script, Model.");
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

        private static ConstructedModel3dElement LoadTlcModel(string scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
                throw new BadRequestException("Путь к Tlc-скрипту не может быть пустым.");
            if (!File.Exists(scriptPath))
                throw new PreconditionFailedException("Файл Tlc-скрипта не найден.");

            var document = new ConstructionDocument();
            document.LoadFromFile(scriptPath);
            return document.CreateModel() as ConstructedModel3dElement ??
                throw new InvalidOperationException("Скрипт не создал Tlc-модель ожидаемого типа.");
        }

        private static object GeneratePropertySchema(ImProperties properties)
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
                            throw CreateUnsupportedPropertyException(property.Tag);
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
                            throw CreateUnsupportedPropertyException(property.Tag);
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
                            throw CreateUnsupportedPropertyException(property.Tag);
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
                            throw CreateUnsupportedPropertyException(property.Tag);
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
                            throw CreateUnsupportedPropertyException(property.Tag);
                        }
                        break;
                    case ReferencePropertyInfo _:
                        throw CreateUnsupportedPropertyException(property.Tag);
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
                            throw CreateUnsupportedPropertyException(property.Tag);
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
                            throw CreateUnsupportedPropertyException(property.Tag);
                        }
                        break;
                    default:
                        throw CreateUnsupportedPropertyException(property.Tag);
                }
                schemaProperties[property.Tag] = schema;
            }
            return schemaProperties;
        }

        private static void SetParameters(ConstructedModel3dElement tlcModel, Dictionary<string, object> args)
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

        private static ImProperties GenerateProperties(ImProperties source, Dictionary<string, object> parameters)
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
                            throw CreateUnsupportedPropertyException(property.Tag);
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
                            throw CreateUnsupportedPropertyException(property.Tag);
                        }
                        break;
                    default:
                        throw CreateUnsupportedPropertyException(property.Tag);
                }
            }
            return result;
        }

        private static object CreateTlcDiagnosticResponse(Exception exception)
        {
            return new
            {
                result = exception.Message,
                description = "Текст ошибки.",
                status = "Возникла ошибка при выполнении скрипта."
            };
        }

        private static NotSupportedException CreateUnsupportedPropertyException(string propertyName)
        {
            return new NotSupportedException($"Параметр Tlc-модели \"{propertyName}\" имеет неподдерживаемый тип или структуру.");
        }
    }
}
