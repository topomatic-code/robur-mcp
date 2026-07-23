using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Cad.Foundation;
using Topomatic.Visualization;
using Topomatic.Visualization.Runtime;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class SolidTools : ToolProvider
    {
        [ToolDef(
            Name = "dwg_solid_create",
            Description = "[ЧЕРТЕЖ] Создает пустое твердое тело и вставляет его в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название созданного тела.' },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить созданное тело. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета созданного тела.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета созданного тела. Используется только при colorMode = Indexed.' }
              },
              'required': ['name'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateSolid(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание пустого твердого тела \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var emptyShell = new Cad.Foundation.Brep.Shell();
                var solidEntity = new DwgModel3DElement();
                var solidElement = new StaticSolidElement(name, "SmdxElement", new ImProperties(), emptyShell, new ImDocuments());
                solidEntity.Element = solidElement;
                drawing.ActiveSpace.Add(solidEntity);
                DwgUtils.ApplyEntityLayer(drawing, solidEntity, layerName);
                DwgUtils.ApplyEntityColor(solidEntity, colorMode, colorIndex);
                if (!solidEntity.HasExtensionDictionary)
                    solidEntity.CreateExtensionDictionary();
                var extDict = solidEntity.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, solidEntity);
                return new
                {
                    result = DwgUtils.CreateSolidObj(solidEntity, guidStr, name),
                    description = "Созданное твердое тело.",
                    status = "Тело успешно создано."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_solid_add_faces",
            Description = "[ЧЕРТЕЖ] Добавляет набор граней в твердое тело.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор твердого тела (из активного чертежа).' },
                'faces': {
                  'type': 'array',
                  'minItems': 1,
                  'description': 'Массив граней. Каждая грань задается массивом 3d вершин, лежащих в одной плоскости (обход строго против ЧС).',
                  'items': {
                    'type': 'object',
                    'properties': {
                      'vertices': {
                        'type': 'array',
                        'minItems': 3,
                        'items': {
                          'type': 'object',
                          'properties': {
                            'x': { 'type': 'number', 'description': 'x-координата' },
                            'y': { 'type': 'number', 'description': 'y-координата' },
                            'z': { 'type': 'number', 'description': 'z-координата' }
                          },
                          'required': ['x', 'y', 'z'],
                          'additionalProperties': false
                        }
                      }
                    },
                    'required': ['vertices'],
                    'additionalProperties': false
                  }
                }
              },
              'required': ['guid', 'faces'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object AddFaces(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var faces = JsonUtils.RequireArray(args, "faces");
            var guid = Guid.Parse(guidStr);
            var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            if (solidEntity == null)
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var solidElement = solidEntity.Element as StaticSolidElement ??
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var shell = new Cad.Foundation.Brep.Shell();
            Cad.Foundation.Brep.Tools.Copy(solidElement.GetBrep(), shell);
            AddFacesToShell(shell, faces);
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Добавление граней"));
            else
                drawing.BeginUpdate();
            try
            {
                var newSolidElement = new StaticSolidElement(
                    solidElement.Name,
                    solidElement.GetObjectType(),
                    solidElement.GetAllProperties().Clone(),
                    shell,
                    new ImDocuments()
                );
                newSolidElement.Color = solidElement.Color;
                solidEntity.Element = newSolidElement;

                // solid entity не допускает трансформаций
                solidEntity.Position = new Vector3D(0, 0, 0);
                solidEntity.Rotation = 0.0;
                solidEntity.Scale = new Vector3D(1, 1, 1);

                var name = "none";
                if (solidEntity.HasExtensionDictionary)
                {
                    var extDict = solidEntity.GetExtensionDictionary();
                    name = extDict.GetString("name", name);
                }
                return new
                {
                    result = DwgUtils.CreateSolidObj(solidEntity, guidStr, name),
                    description = "Обновленное твердое тело.",
                    status = $"Грани успешно добавлены. Количество добавленных граней: {faces.Length}."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        private static void AddFacesToShell(Cad.Foundation.Brep.Shell shell, Dictionary<string, object>[] faces)
        {
            if (faces == null || faces.Length == 0)
                throw new InvalidOperationException("Необходимо передать хотя бы одну грань.");
            for (int i = 0; i < faces.Length; i++)
            {
                var faceObject = faces[i];
                var vertices = JsonUtils.RequireArray(faceObject, "vertices");
                if (vertices.Length < 3)
                    throw new InvalidOperationException($"Грань с индексом {i} должна содержать как минимум 3 вершины.");
                var face = new List<Vector3D>(vertices.Length);
                for (int j = 0; j < vertices.Length; j++)
                {
                    var vertexObj = vertices[j];
                    var x = JsonUtils.RequireDouble(vertexObj, "x");
                    var y = JsonUtils.RequireDouble(vertexObj, "y");
                    var z = JsonUtils.RequireDouble(vertexObj, "z");
                    face.Add(new Vector3D(x, y, z));
                }
                Cad.Foundation.Brep.Tools.AddFace(shell, face);
            }
        }

        [ToolDef(
            Name = "dwg_solid_get_faces",
            Description = "[ЧЕРТЕЖ] Возвращает грани твердого тела из пространства активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор твердого тела (из активного чертежа).' }
              },
              'required': ['guid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetFaces(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var guid = Guid.Parse(guidStr);
            var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            if (solidEntity == null)
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var solidElement = solidEntity.Element as StaticSolidElement ??
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var shell = solidElement.GetBrep();
            var faces = shell.Faces
                .SelectMany(f => f.Loops.Select(l => l.GetPolygon3d()))
                .Select(polygon => new
                {
                    vertices = polygon.Select(vertex => new
                    {
                        x = vertex.X,
                        y = vertex.Y,
                        z = vertex.Z
                    }).ToArray()
                })
                .ToArray();
            return new
            {
                result = new
                {
                    guid = guidStr,
                    name = currentName ?? "none",
                    faceCount = faces.Length,
                    faces
                },
                description = "Грани твердого тела.",
                status = $"Грани твердого тела успешно получены."
            };
        }

        [ToolDef(
            Name = "dwg_solid_section",
            Description = "[ЧЕРТЕЖ] Возвращает контуры сечения твердого тела плоскостью.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор твердого тела (из активного чертежа).' },
                'position': {
                  'type': 'object',
                  'description': 'Точка, через которую проходит плоскость сечения.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' },
                    'z': { 'type': 'number', 'description': 'z-координата' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                },
                'normal': {
                  'type': 'object',
                  'description': 'Нормаль плоскости сечения.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' },
                    'z': { 'type': 'number', 'description': 'z-координата' }
                  },
                  'required': ['x', 'y', 'z'],
                  'additionalProperties': false
                }
              },
              'required': ['guid', 'position', 'normal'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object Section(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var positionObject = JsonUtils.RequireObject(args, "position");
            var normalObject = JsonUtils.RequireObject(args, "normal");
            var position = new Vector3D(
                JsonUtils.RequireDouble(positionObject, "x"),
                JsonUtils.RequireDouble(positionObject, "y"),
                JsonUtils.RequireDouble(positionObject, "z")
            );
            var normal = new Vector3D(
                JsonUtils.RequireDouble(normalObject, "x"),
                JsonUtils.RequireDouble(normalObject, "y"),
                JsonUtils.RequireDouble(normalObject, "z")
            );
            if (normal.Length <= 1e-9)
                throw new InvalidOperationException("Нормаль плоскости сечения не может быть нулевой.");
            normal.Normalize();
            var guid = Guid.Parse(guidStr);
            var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            if (solidEntity == null)
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var solidElement = solidEntity.Element as StaticSolidElement ??
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var solidShell = solidElement.GetBrep();
            var cuttingShell = CreateCuttingPlaneShell(solidShell, position, normal);
            var clipResult = Cad.Foundation.Brep.Tools.Clip(cuttingShell, solidShell, true);
            var sectionContours = clipResult.Faces.SelectMany(f => f.Loops.Select(l => l.GetPolygon3d()));
            var contours = sectionContours
                .Select(contour => new
                {
                    vertices = contour.Select(vertex => new
                    {
                        x = vertex.X,
                        y = vertex.Y,
                        z = vertex.Z
                    }).ToArray()
                })
                .ToArray();
            return new
            {
                result = new
                {
                    guid = guidStr,
                    name = currentName ?? "none",
                    contourCount = contours.Length,
                    contours
                },
                description = "Контуры сечения твердого тела плоскостью.",
                status = $"Контуры сечения успешно получены. Количество контуров: {contours.Length}."
            };
        }

        private static Cad.Foundation.Brep.Shell CreateCuttingPlaneShell(Cad.Foundation.Brep.Shell solidShell, Vector3D position, Vector3D normal)
        {
            const double MARGIN = 0.1;

            var plane = new Plane(normal, position);
            plane.Normalize();
            var bounds = Cad.Foundation.Brep.Tools.GetBounds(solidShell);
            var center = plane.Project(bounds.Center);
            var corners = bounds.GetCorners();
            var size = corners.Select(corner => Vector3D.Distance(bounds.Center, corner)).DefaultIfEmpty(1.0).Max() + MARGIN;
            if (size <= MARGIN)
                size = 1.0 + MARGIN;
            var reference = Math.Abs(Vector3D.Dot(normal, Vector3D.UnitZ)) < 0.9 ? Vector3D.UnitZ : Vector3D.UnitX;
            var u = Vector3D.Cross(normal, reference);
            if (u.Length <= 1e-9)
                u = Vector3D.UnitX;
            u.Normalize();
            var v = Vector3D.Cross(normal, u);
            v.Normalize();
            var polygon = new[]
            {
                center - u * size - v * size,
                center + u * size - v * size,
                center + u * size + v * size,
                center - u * size + v * size
            };
            return Cad.Foundation.Brep.Tools.Polygon(polygon);
        }

        [ToolDef(
            Name = "dwg_solid_remove_faces",
            Description = "[ЧЕРТЕЖ] Удаляет грани из твердого тела по индексам граней.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'guid': { 'type': 'string', 'description': 'Guid-идентификатор твердого тела (из активного чертежа).' },
                'faceIndexes': {
                  'type': 'array',
                  'minItems': 1,
                  'description': 'Массив индексов удаляемых граней. Индексы соответствуют порядку граней, возвращаемому dwg_solid_get_faces.',
                  'items': {
                    'type': 'integer',
                    'minimum': 0
                  }
                }
              },
              'required': ['guid', 'faceIndexes'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object RemoveFaces(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var guidStr = JsonUtils.RequireString(args, "guid");
            var faceIndexes = JsonUtils.RequireIntArray(args, "faceIndexes");
            if (faceIndexes.Length == 0)
                throw new InvalidOperationException("Необходимо передать хотя бы один индекс грани.");
            var guid = Guid.Parse(guidStr);
            var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
            if (solidEntity == null)
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var solidElement = solidEntity.Element as StaticSolidElement ??
                throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
            var shell = new Cad.Foundation.Brep.Shell();
            Cad.Foundation.Brep.Tools.Copy(solidElement.GetBrep(), shell);
            var shellFaces = shell.Faces.ToArray();
            var facesToRemove = new List<Cad.Foundation.Brep.Face>(faceIndexes.Length);
            var usedIndexes = new HashSet<int>();
            foreach (var faceIndex in faceIndexes)
            {
                if (faceIndex < 0 || faceIndex >= shellFaces.Length)
                    throw new InvalidOperationException($"Индекс грани {faceIndex} выходит за пределы допустимого диапазона 0..{shellFaces.Length - 1}.");
                if (usedIndexes.Add(faceIndex))
                    facesToRemove.Add(shellFaces[faceIndex]);
            }
            Cad.Foundation.Brep.Tools.RemoveFaces(shell, facesToRemove);
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Удаление граней"));
            else
                drawing.BeginUpdate();
            try
            {
                var newSolidElement = new StaticSolidElement(
                    solidElement.Name,
                    solidElement.GetObjectType(),
                    solidElement.GetAllProperties().Clone(),
                    shell,
                    new ImDocuments()
                );
                newSolidElement.Color = solidElement.Color;
                solidEntity.Element = newSolidElement;

                // solid entity не допускает трансформаций
                solidEntity.Position = new Vector3D(0, 0, 0);
                solidEntity.Rotation = 0.0;
                solidEntity.Scale = new Vector3D(1, 1, 1);

                var name = currentName ?? "none";
                if (solidEntity.HasExtensionDictionary)
                {
                    var extDict = solidEntity.GetExtensionDictionary();
                    name = extDict.GetString("name", name);
                }
                return new
                {
                    result = DwgUtils.CreateSolidObj(solidEntity, guidStr, name),
                    description = "Обновленное твердое тело.",
                    status = $"Грани успешно удалены. Количество удаленных граней: {facesToRemove.Count}."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_solid_transform",
            Description = "[ЧЕРТЕЖ] Выполняет трансформацию набора твердых тел: перенос, поворот или масштабирование. Может обновлять исходные тела или создавать трансформированные копии.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'operation': {
                  'type': 'string',
                  'description': 'Тип трансформации.',
                  'enum': ['Translate', 'Rotate', 'Scale']
                },
                'elements': {
                  'type': 'array',
                  'minItems': 1,
                  'description': 'Массив Guid-идентификаторов трансформируемых твердых тел.',
                  'items': {
                    'type': 'string'
                  }
                },
                'parameters': {
                  'type': 'object',
                  'description': 'Параметры трансформации. Для Translate: x, y, z. Для Rotate: x, y, z - углы поворота вокруг осей в радианах. Для Scale: scale.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'Смещение по X или угол поворота вокруг X.' },
                    'y': { 'type': 'number', 'description': 'Смещение по Y или угол поворота вокруг Y.' },
                    'z': { 'type': 'number', 'description': 'Смещение по Z или угол поворота вокруг Z.' },
                    'scale': { 'type': 'number', 'exclusiveMinimum': 0, 'description': 'Коэффициент масштабирования.' }
                  },
                  'additionalProperties': false
                },
                'createCopy': { 'type': 'boolean', 'description': 'Если true, создает трансформированные копии. Если false, обновляет исходные тела.' },
                'namePrefix': { 'type': 'string', 'description': 'Префикс имени для создаваемых копий. Используется только при createCopy = true.' }
              },
              'required': ['operation', 'elements', 'parameters', 'createCopy'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object Transform(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var operation = JsonUtils.RequireString(args, "operation");
            var elements = JsonUtils.RequireStringArray(args, "elements");
            var parameters = JsonUtils.RequireObject(args, "parameters");
            var createCopy = JsonUtils.RequireBool(args, "createCopy");
            var namePrefix = JsonUtils.GetString(args, "namePrefix", null);
            if (elements.Length == 0)
                throw new InvalidOperationException("Необходимо передать хотя бы один guid твердого тела.");
            var solids = new List<(string guidStr, DwgModel3DElement entity, StaticSolidElement element, string name)>(elements.Length);
            for (int i = 0; i < elements.Length; i++)
            {
                var guidStr = elements[i];
                var guid = Guid.Parse(guidStr);
                var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, guid);
                if (solidEntity == null)
                    throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
                var solidElement = solidEntity.Element as StaticSolidElement ??
                    throw new InvalidOperationException($"Не удалось найти твердое тело по указанному guid \"{guidStr}\".");
                solids.Add((guidStr, solidEntity, solidElement, currentName ?? "none"));
            }
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Трансформация твердых тел"));
            else
                drawing.BeginUpdate();
            try
            {
                var results = new List<object>(solids.Count);
                for (int i = 0; i < solids.Count; i++)
                {
                    var solidInfo = solids[i];
                    var shell = new Cad.Foundation.Brep.Shell();
                    Cad.Foundation.Brep.Tools.Copy(solidInfo.element.GetBrep(), shell);
                    shell = ApplySolidTransform(shell, operation, parameters);
                    if (createCopy)
                    {
                        var copyName = CreateSolidCopyName(solidInfo.name, namePrefix, solids.Count, i);
                        var copyGuid = Guid.NewGuid();
                        var copyGuidStr = copyGuid.ToString();
                        var copyEntity = new DwgModel3DElement();
                        var copyElement = new StaticSolidElement(
                            copyName,
                            solidInfo.element.GetObjectType(),
                            solidInfo.element.GetAllProperties().Clone(),
                            shell,
                            new ImDocuments()
                        );
                        copyEntity.Element = copyElement;

                        // solid entity не допускает трансформаций
                        copyEntity.Position = new Vector3D(0, 0, 0);
                        copyEntity.Rotation = 0.0;
                        copyEntity.Scale = new Vector3D(1, 1, 1);

                        drawing.ActiveSpace.Add(copyEntity);
                        DwgUtils.ApplyEntityLayer(drawing, copyEntity, solidInfo.entity.Layer?.Name);
                        DwgUtils.ApplyEntityColor(copyEntity, DwgUtils.GetColorMode(solidInfo.entity.Color), solidInfo.entity.Color.ColorIndex);
                        if (!copyEntity.HasExtensionDictionary)
                            copyEntity.CreateExtensionDictionary();
                        var extDict = copyEntity.GetExtensionDictionary();
                        extDict.SetString("guid", copyGuidStr);
                        extDict.SetString("name", copyName);
                        sessionStorage.AddObject(copyGuid, copyEntity);
                        results.Add(DwgUtils.CreateSolidObj(copyEntity, copyGuidStr, copyName));
                    }
                    else
                    {
                        var newSolidElement = new StaticSolidElement(
                            solidInfo.element.Name,
                            solidInfo.element.GetObjectType(),
                            solidInfo.element.GetAllProperties().Clone(),
                            shell,
                            new ImDocuments()
                        );
                        newSolidElement.Color = solidInfo.element.Color;
                        solidInfo.entity.Element = newSolidElement;

                        // solid entity не допускает трансформаций
                        solidInfo.entity.Position = new Vector3D(0, 0, 0);
                        solidInfo.entity.Rotation = 0.0;
                        solidInfo.entity.Scale = new Vector3D(1, 1, 1);

                        results.Add(DwgUtils.CreateSolidObj(solidInfo.entity, solidInfo.guidStr, solidInfo.name));
                    }
                }
                return new
                {
                    result = new
                    {
                        operation,
                        createCopy,
                        solids = results.ToArray()
                    },
                    description = createCopy ? "Созданные трансформированные копии твердых тел." : "Обновленные твердые тела.",
                    status = $"Трансформация твердых тел успешно выполнена. Количество тел: {results.Count}."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        private static Cad.Foundation.Brep.Shell ApplySolidTransform(Cad.Foundation.Brep.Shell shell, string operation, Dictionary<string, object> parameters)
        {
            if (string.Equals(operation, "Translate", StringComparison.OrdinalIgnoreCase))
            {
                var x = JsonUtils.RequireDouble(parameters, "x");
                var y = JsonUtils.RequireDouble(parameters, "y");
                var z = JsonUtils.RequireDouble(parameters, "z");
                return Cad.Foundation.Brep.Tools.Translate(x, y, z, shell);
            }
            if (string.Equals(operation, "Rotate", StringComparison.OrdinalIgnoreCase))
            {
                var x = JsonUtils.RequireDouble(parameters, "x");
                var y = JsonUtils.RequireDouble(parameters, "y");
                var z = JsonUtils.RequireDouble(parameters, "z");
                return Cad.Foundation.Brep.Tools.Rotate(x, y, z, shell);
            }
            if (string.Equals(operation, "Scale", StringComparison.OrdinalIgnoreCase))
            {
                var scale = JsonUtils.RequireDouble(parameters, "scale");
                if (scale <= 0)
                    throw new InvalidOperationException("Коэффициент масштабирования (scale) должен быть больше 0.");
                return Cad.Foundation.Brep.Tools.Scale(scale, shell);
            }
            throw new InvalidOperationException("Неизвестное значение operation. Допустимые значения: Translate, Rotate, Scale.");
        }

        private static string CreateSolidCopyName(string sourceName, string namePrefix, int totalCount, int index)
        {
            if (!string.IsNullOrWhiteSpace(namePrefix))
                return totalCount == 1 ? namePrefix : $"{namePrefix}{index + 1}";
            var baseName = string.IsNullOrWhiteSpace(sourceName) || string.Equals(sourceName, "none", StringComparison.OrdinalIgnoreCase)
                ? "solid"
                : sourceName;
            return totalCount == 1 ? $"{baseName}_copy" : $"{baseName}_copy_{index + 1}";
        }

        [ToolDef(
            Name = "dwg_solid_sweep",
            Description = "[ЧЕРТЕЖ] Создает твердое тело путем вытягивания 2d сечения вдоль 3d кривой.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название созданного тела.' },
                'section': {
                  'type': 'array',
                  'minItems': 1,
                  'description': 'Сечение, вытягиваемое вдоль кривой. Представляет собой набор 2d контуров.',
                  'items': {
                    'type': 'object',
                    'properties': {
                      'contour': {
                        'type': 'array',
                        'minItems': 3,
                        'description': '2d контур сечения.',
                        'items': {
                          'type': 'object',
                          'properties': {
                            'x': { 'type': 'number', 'description': 'x-координата' },
                            'y': { 'type': 'number', 'description': 'y-координата' }
                          },
                          'required': ['x', 'y'],
                          'additionalProperties': false
                        }
                      }
                    },
                    'required': ['contour'],
                    'additionalProperties': false
                  }
                },
                'curve': {
                  'type': 'array',
                  'minItems': 2,
                  'description': 'Кривая, состоящая из набора 3d точек и образующая траекторию вытягивания сечения.',
                  'items': {
                    'type': 'object',
                    'properties': {
                      'x': { 'type': 'number', 'description': 'x-координата' },
                      'y': { 'type': 'number', 'description': 'y-координата' },
                      'z': { 'type': 'number', 'description': 'z-координата' }
                    },
                    'required': ['x', 'y', 'z'],
                    'additionalProperties': false
                  }
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить созданное тело. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета созданного тела.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета созданного тела. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'section', 'curve'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object Sweep(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var section = JsonUtils.RequireArray(args, "section");
            var curve = JsonUtils.RequireArray(args, "curve");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            if (section.Length == 0)
                throw new InvalidOperationException("Сечение должно содержать хотя бы один контур.");
            var sectionContours = new List<(List<Vector2D> positions, int level)>(section.Length);
            for (int i = 0; i < section.Length; i++)
            {
                var sectionObject = section[i];
                var contour = JsonUtils.RequireArray(sectionObject, "contour");
                if (contour.Length < 3)
                    throw new InvalidOperationException($"Контур сечения с индексом {i} должен содержать как минимум 3 точки.");
                var contourPoints = new List<Vector2D>(contour.Length);
                for (int j = 0; j < contour.Length; j++)
                {
                    var pointObject = contour[j];
                    var x = JsonUtils.RequireDouble(pointObject, "x");
                    var y = JsonUtils.RequireDouble(pointObject, "y");
                    contourPoints.Add(new Vector2D(x, y));
                }
                if (!Cad.Foundation.Brep.Tools.IsCCW(contourPoints))
                    contourPoints.Reverse();
                sectionContours.Add((contourPoints, 0));
            }
            var contoursCount = sectionContours.Count;
            for (int i = 0; i < contoursCount; i++)
            {
                var a = sectionContours[i];
                if (a.positions.Count == 0)
                    continue;
                var aPos = a.positions[0];
                var level = 0;
                for (int j = 0; j < contoursCount; j++)
                {
                    if (i == j)
                        continue;
                    var b = sectionContours[j];
                    if (b.positions.Count == 0)
                        continue;
                    if (CadLibrary.PosInPolygon(aPos, b.positions, false))
                        level++;
                }
                sectionContours[i] = (a.positions, level);
            }
            sectionContours.Sort((a, b) => a.level.CompareTo(b.level));
            var curvePoints = new List<Vector3D>(curve.Length);
            for (int i = 0; i < curve.Length; i++)
            {
                var pointObject = curve[i];
                var x = JsonUtils.RequireDouble(pointObject, "x");
                var y = JsonUtils.RequireDouble(pointObject, "y");
                var z = JsonUtils.RequireDouble(pointObject, "z");
                curvePoints.Add(new Vector3D(x, y, z));
            }
            if (curvePoints.Count < 2)
                throw new InvalidOperationException("Кривая вытягивания должна содержать как минимум 2 точки.");
            Cad.Foundation.Brep.Shell shell = null;
            foreach (var (positions, level) in sectionContours)
            {
                var sweep = Cad.Foundation.Brep.Tools.Sweep(curvePoints, positions, 1);
                if (shell == null)
                {
                    shell = sweep;
                }
                else
                {
                    if (level % 2 == 0)
                        shell = Cad.Foundation.Brep.Tools.Union(shell, sweep);
                    else
                        shell = Cad.Foundation.Brep.Tools.Difference(shell, sweep);
                }
            }
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Операция вытягивания \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var solidEntity = new DwgModel3DElement();
                var solidElement = new StaticSolidElement(name, "SmdxElement", new ImProperties(), shell, new ImDocuments());
                solidEntity.Element = solidElement;
                drawing.ActiveSpace.Add(solidEntity);
                DwgUtils.ApplyEntityLayer(drawing, solidEntity, layerName);
                DwgUtils.ApplyEntityColor(solidEntity, colorMode, colorIndex);
                if (!solidEntity.HasExtensionDictionary)
                    solidEntity.CreateExtensionDictionary();
                var extDict = solidEntity.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, solidEntity);
                return new
                {
                    result = DwgUtils.CreateSolidObj(solidEntity, guidStr, name),
                    description = "Созданное твердое тело.",
                    status = "Тело успешно создано."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_solid_union",
            Description = "[ЧЕРТЕЖ] Выполняет операцию объединения твердых тел и вставляет результирующее тело в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {            
                'name': { 'type': 'string', 'description': 'Название результирующего тела.' },
                'elements': {
                  'type': 'array',
                  'description': 'Массив Guid-идентификаторов объединяемых твердых тел.',
                  'minItems': 2,
                  'items': {
                    'type': 'string'
                  }
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить результирующее тело. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета результирующего тела.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета результирующего тела. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'elements'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object Union(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var elements = JsonUtils.RequireStringArray(args, "elements");
            if (elements.Length < 2)
                throw new InvalidOperationException("Количество элементов 'elements' должно быть не менее 2.");
            var initialEntities = new List<DwgModel3DElement>();
            var shells = new List<Cad.Foundation.Brep.Shell>();
            foreach (var elementGuidStr in elements)
            {
                var elementGuid = Guid.Parse(elementGuidStr);
                var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, elementGuid);
                if (solidEntity == null)
                    throw new InvalidOperationException($"Не удалось найти тело по указанному guid \"{elementGuidStr}\".");
                initialEntities.Add(solidEntity);
                shells.Add(solidEntity.Element.GetBrep());
            }
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var resultShell = shells[0];
            for (int i = 1; i < shells.Count; i++)
            {
                var shell = shells[i];
                resultShell = Cad.Foundation.Brep.Tools.Union(resultShell, shell);
            }
            Cad.Foundation.Brep.Tools.SimplifyFaces(resultShell);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Объединение твердых тел \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                for (int i = 0; i < initialEntities.Count; i++)
                {
                    var entity = initialEntities[i];

                    // solid entity не допускает трансформаций
                    entity.Position = new Vector3D(0, 0, 0);
                    entity.Rotation = 0.0;
                    entity.Scale = new Vector3D(1, 1, 1);
                }
                var solidEntity = new DwgModel3DElement();
                var solidElement = new StaticSolidElement(name, "SmdxElement", new ImProperties(), resultShell, new ImDocuments());
                solidEntity.Element = solidElement;
                drawing.ActiveSpace.Add(solidEntity);
                DwgUtils.ApplyEntityLayer(drawing, solidEntity, layerName);
                DwgUtils.ApplyEntityColor(solidEntity, colorMode, colorIndex);
                if (!solidEntity.HasExtensionDictionary)
                    solidEntity.CreateExtensionDictionary();
                var extDict = solidEntity.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, solidEntity);
                return new
                {
                    result = DwgUtils.CreateSolidObj(solidEntity, guidStr, name),
                    description = "Результирующее твердое тело.",
                    status = "Операция объединения успешно выполнена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_solid_intersection",
            Description = "[ЧЕРТЕЖ] Выполняет операцию пересечения твердых тел и вставляет результирующее тело в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {            
                'name': { 'type': 'string', 'description': 'Название результирующего тела.' },
                'elements': {
                  'type': 'array',
                  'description': 'Массив Guid-идентификаторов пересекаемых твердых тел.',
                  'minItems': 2,
                  'items': {
                    'type': 'string'
                  }
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить результирующее тело. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета результирующего тела.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета результирующего тела. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'elements'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object Intersection(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var elements = JsonUtils.RequireStringArray(args, "elements");
            if (elements.Length < 2)
                throw new InvalidOperationException("Количество элементов 'elements' должно быть не менее 2.");
            var initialEntities = new List<DwgModel3DElement>();
            var shells = new List<Cad.Foundation.Brep.Shell>();
            foreach (var elementGuidStr in elements)
            {
                var elementGuid = Guid.Parse(elementGuidStr);
                var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, elementGuid);
                if (solidEntity == null)
                    throw new InvalidOperationException($"Не удалось найти тело по указанному guid \"{elementGuidStr}\".");
                initialEntities.Add(solidEntity);
                shells.Add(solidEntity.Element.GetBrep());
            }
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var resultShell = shells[0];
            for (int i = 1; i < shells.Count; i++)
            {
                var shell = shells[i];
                resultShell = Cad.Foundation.Brep.Tools.Intersection(resultShell, shell);
            }
            Cad.Foundation.Brep.Tools.SimplifyFaces(resultShell);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Пересечение твердых тел \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                for (int i = 0; i < initialEntities.Count; i++)
                {
                    var entity = initialEntities[i];

                    // solid entity не допускает трансформаций
                    entity.Position = new Vector3D(0, 0, 0);
                    entity.Rotation = 0.0;
                    entity.Scale = new Vector3D(1, 1, 1);
                }
                var solidEntity = new DwgModel3DElement();
                var solidElement = new StaticSolidElement(name, "SmdxElement", new ImProperties(), resultShell, new ImDocuments());
                solidEntity.Element = solidElement;
                drawing.ActiveSpace.Add(solidEntity);
                DwgUtils.ApplyEntityLayer(drawing, solidEntity, layerName);
                DwgUtils.ApplyEntityColor(solidEntity, colorMode, colorIndex);
                if (!solidEntity.HasExtensionDictionary)
                    solidEntity.CreateExtensionDictionary();
                var extDict = solidEntity.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, solidEntity);
                return new
                {
                    result = DwgUtils.CreateSolidObj(solidEntity, guidStr, name),
                    description = "Результирующее твердое тело.",
                    status = "Операция пересечения успешно выполнена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }

        [ToolDef(
            Name = "dwg_solid_difference",
            Description = "[ЧЕРТЕЖ] Выполняет операцию вычитания твердых тел и вставляет результирующее тело в пространство активного чертежа. Из первого переданного тела вычитаются все остальные.",
            InputSchema = @"{
              'type': 'object',
              'properties': {            
                'name': { 'type': 'string', 'description': 'Название результирующего тела.' },
                'elements': {
                  'type': 'array',
                  'description': 'Массив Guid-идентификаторов твердых тел. Из первого тела будут вычтены все остальные.',
                  'minItems': 2,
                  'items': {
                    'type': 'string'
                  }
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить результирующее тело. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета результирующего тела.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета результирующего тела. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'elements'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object Difference(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var elements = JsonUtils.RequireStringArray(args, "elements");
            if (elements.Length < 2)
                throw new InvalidOperationException("Количество элементов 'elements' должно быть не менее 2.");
            var initialEntities = new List<DwgModel3DElement>();
            var shells = new List<Cad.Foundation.Brep.Shell>();
            foreach (var elementGuidStr in elements)
            {
                var elementGuid = Guid.Parse(elementGuidStr);
                var (solidEntity, currentName) = DwgUtils.FindEntity<DwgModel3DElement>(drawing, sessionStorage, elementGuid);
                if (solidEntity == null)
                    throw new InvalidOperationException($"Не удалось найти тело по указанному guid \"{elementGuidStr}\".");
                initialEntities.Add(solidEntity);
                shells.Add(solidEntity.Element.GetBrep());
            }
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var resultShell = shells[0];
            for (int i = 1; i < shells.Count; i++)
            {
                var shell = shells[i];
                resultShell = Cad.Foundation.Brep.Tools.Difference(resultShell, shell);
            }
            Cad.Foundation.Brep.Tools.SimplifyFaces(resultShell);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Вычитание твердых тел \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                for (int i = 0; i < initialEntities.Count; i++)
                {
                    var entity = initialEntities[i];

                    // solid entity не допускает трансформаций
                    entity.Position = new Vector3D(0, 0, 0);
                    entity.Rotation = 0.0;
                    entity.Scale = new Vector3D(1, 1, 1);
                }
                var solidEntity = new DwgModel3DElement();
                var solidElement = new StaticSolidElement(name, "SmdxElement", new ImProperties(), resultShell, new ImDocuments());
                solidEntity.Element = solidElement;
                drawing.ActiveSpace.Add(solidEntity);
                DwgUtils.ApplyEntityLayer(drawing, solidEntity, layerName);
                DwgUtils.ApplyEntityColor(solidEntity, colorMode, colorIndex);
                if (!solidEntity.HasExtensionDictionary)
                    solidEntity.CreateExtensionDictionary();
                var extDict = solidEntity.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                sessionStorage.AddObject(guid, solidEntity);
                return new
                {
                    result = DwgUtils.CreateSolidObj(solidEntity, guidStr, name),
                    description = "Результирующее твердое тело.",
                    status = "Операция вычитания успешно выполнена."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }
    }
}
