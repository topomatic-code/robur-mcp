using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;
using Topomatic.Dwg.Layer;
using Topomatic.Landscaping;
using Topomatic.Tables;
using Topomatic.ToolBridge.Services;
using Topomatic.Visualization;
using Topomatic.Visualization.Constructions;
using Topomatic.Visualization.Runtime;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public static class DwgUtils
    {
        public static Drawing GetDrawing(CadView cadView)
        {
            if (cadView == null)
                return null;
            var layer = DrawingLayer.GetDrawingLayer(cadView);
            if (layer == null)
                return null;
            return layer.Drawing;
        }

        public static (T entity, string name) FindEntity<T>(Drawing drawing, ObjectStorage sessionStorage, Guid guid) where T : DwgEntity
        {
            var guidStr = guid.ToString();
            T entity = null;
            string name = null;
            if (sessionStorage.HasObject(guid))
            {
                entity = (T)sessionStorage.GetObject(guid);
                if (entity.Drawing != drawing)
                    throw new InvalidOperationException($"Элемент (сущность) с guid \"{guidStr}\" не находится в активном чертеже.");
                if (entity.HasExtensionDictionary)
                {
                    var extDict = entity.GetExtensionDictionary();
                    name = extDict.GetString("name", null);
                }
            }
            else
            {
                foreach (var dwgEntity in drawing.ActiveSpace.Entities)
                {
                    if (dwgEntity is T && dwgEntity.HasExtensionDictionary)
                    {
                        var extDict = dwgEntity.GetExtensionDictionary();
                        if (string.Equals(guidStr, extDict.GetString("guid", null)))
                        {
                            entity = (T)dwgEntity;
                            name = extDict.GetString("name", null);
                            sessionStorage.AddObject(guid, entity);
                            break;
                        }
                    }
                }
            }
            return (entity, name);
        }

        public static (string, string) GetEntityType(DwgEntity entity)
        {
            var type = "none";
            var typeDescription = "none";
            if (entity is DwgPolyline)
            {
                type = "dwg_polyline";
                typeDescription = "2d полилиния";
            }
            else if (entity is DwgTable)
            {
                type = "dwg_table";
                typeDescription = "Таблица";
            }
            else if (entity is DwgMText)
            {
                type = "dwg_mtext";
                typeDescription = "Многострочный текст";
            }
            else if (entity is DwgText)
            {
                type = "dwg_text";
                typeDescription = "Текст";
            }
            else if (entity is DwgCircle)
            {
                type = "dwg_circle";
                typeDescription = "Окружность";
            }
            else if (entity is DwgLine)
            {
                type = "dwg_line";
                typeDescription = "Линия";
            }
            else if (entity is DwgHatch)
            {
                type = "dwg_hatch";
                typeDescription = "Штриховка";
            }
            else if (entity is DwgInsert)
            {
                type = "dwg_block_insert";
                typeDescription = "Вставка блока";
            }
            else if (entity is DwgSmdxPointLandscaping)
            {
                type = "landscp_point_plant";
                typeDescription = "Точечный элемент посадки";
            }
            else if (entity is DwgModel3DElement solidEntity && solidEntity.Element is StaticSolidElement)
            {
                type = "dwg_solid";
                typeDescription = "Твердое тело";
            }
            else if (entity is DwgModel3DElement tlcEntity && tlcEntity.Element is ConstructedModel3dElement)
            {
                type = "tlc_model";
                typeDescription = "Tlc-модель";
            }
            return (type, typeDescription);
        }

        public static object CreateEntityObj(DwgEntity entity, string guid, string name)
        {
            object result;
            if (entity is DwgPolyline polyline)
            {
                result = CreatePolylineObj(polyline, guid, name);
            }
            else if (entity is DwgTable table)
            {
                result = CreateTableObj(table, guid, name);
            }
            else if (entity is DwgMText mText)
            {
                result = CreateMTextObj(mText, guid, name);
            }
            else if (entity is DwgText text)
            {
                result = CreateTextObj(text, guid, name);
            }
            else if (entity is DwgCircle circle)
            {
                result = CreateCircleObj(circle, guid, name);
            }
            else if (entity is DwgLine line)
            {
                result = CreateLineObj(line, guid, name);
            }
            else if (entity is DwgHatch hatch)
            {
                result = CreateHatchObj(hatch, guid, name);
            }
            else if (entity is DwgInsert insert)
            {
                result = CreateBlockInsertObj(insert, guid, name);
            }
            else if (entity is DwgSmdxPointLandscaping pointPlant)
            {
                string libUid;
                if (pointPlant.HasExtensionDictionary)
                    libUid = pointPlant.GetExtensionDictionary().GetString("libUid", "none");
                else
                    libUid = "none";
                result = CreatePointPlantObj(pointPlant, libUid, guid, name);
            }
            else if (entity is DwgModel3DElement solidEntity && solidEntity.Element is StaticSolidElement)
            {
                result = CreateSolidObj(solidEntity, guid, name);
            }
            else if (entity is DwgModel3DElement tlcEntity && tlcEntity.Element is ConstructedModel3dElement)
            {
                result = CreateTlcObj(tlcEntity, guid, name);
            }
            else
            {
                var (type, typeDescription) = GetEntityType(entity);
                result = new
                {
                    guid,
                    name,
                    layerName = entity.Layer?.Name ?? "none",
                    colorMode = GetColorMode(entity.Color),
                    colorIndex = entity.Color.ColorIndex,
                    bounds = new
                    {
                        left = entity.Bounds.Left,
                        right = entity.Bounds.Right,
                        top = entity.Bounds.Top,
                        bottom = entity.Bounds.Bottom
                    },
                    type,
                    typeDescription,
                    message = $"Функция генерации подробной информации для элементов типа \"{type}\" еще не реализована."
                };
            }
            return result;
        }

        public static object CreateEntityInfoObj(DwgEntity entity, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(entity);
            return new
            {
                guid,
                name,
                layerName = entity.Layer?.Name ?? "none",
                colorMode = GetColorMode(entity.Color),
                colorIndex = entity.Color.ColorIndex,
                bounds = new
                {
                    left = entity.Bounds.Left,
                    right = entity.Bounds.Right,
                    top = entity.Bounds.Top,
                    bottom = entity.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreatePolylineObj(DwgPolyline polyline, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(polyline);
            return new
            {
                guid,
                name,
                layerName = polyline.Layer?.Name ?? "none",
                colorMode = GetColorMode(polyline.Color),
                colorIndex = polyline.Color.ColorIndex,
                bounds = new
                {
                    left = polyline.Bounds.Left,
                    right = polyline.Bounds.Right,
                    top = polyline.Bounds.Top,
                    bottom = polyline.Bounds.Bottom
                },
                type,
                typeDescription,
                points = polyline.Select(p => new { x = p.Vertex.X, y = p.Vertex.Y }).ToArray(),
                closed = polyline.Closed
            };
        }

        public static object CreateTableObj(DwgTable table, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(table);
            var cells = new List<object>();
            var sheetEditor = (ISheetEditorModel)table;
            for (int i = 0; i < sheetEditor.RowsCount; i++)
            {
                for (int j = 0; j < sheetEditor.ColumnsCount; j++)
                {
                    var cellEditor = sheetEditor[i, j];
                    if (cellEditor.MergedX1 == j && cellEditor.MergedY1 == i)
                    {
                        cells.Add
                        (
                            new
                            {
                                row = cellEditor.MergedY1,
                                column = cellEditor.MergedX1,
                                rowSpan = cellEditor.MergedY2 - cellEditor.MergedY1 + 1,
                                columnSpan = cellEditor.MergedX2 - cellEditor.MergedX1 + 1,
                                text = cellEditor.SourceText
                            }
                        );
                    }
                }
            }
            return new
            {
                guid,
                name,
                layerName = table.Layer?.Name ?? "none",
                colorMode = GetColorMode(table.Color),
                colorIndex = table.Color.ColorIndex,
                position = new { x = table.Position.X, y = table.Position.Y },
                rowCount = table.RowsCount,
                columnCount = table.ColumnsCount,
                cells = cells.ToArray(),
                bounds = new
                {
                    left = table.Bounds.Left,
                    right = table.Bounds.Right,
                    top = table.Bounds.Top,
                    bottom = table.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateMTextObj(DwgMText mText, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(mText);
            return new
            {
                guid,
                name,
                layerName = mText.Layer?.Name ?? "none",
                colorMode = GetColorMode(mText.Color),
                colorIndex = mText.Color.ColorIndex,
                text = mText.Content,
                position = new { x = mText.Position.X, y = mText.Position.Y },
                height = mText.Height,
                rotation = mText.Rotation,
                attachmentPoint = mText.AttachmentPoint.ToString(),
                bounds = new
                {
                    left = mText.Bounds.Left,
                    right = mText.Bounds.Right,
                    top = mText.Bounds.Top,
                    bottom = mText.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateTextObj(DwgText text, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(text);
            return new
            {
                guid,
                name,
                layerName = text.Layer?.Name ?? "none",
                colorMode = GetColorMode(text.Color),
                colorIndex = text.Color.ColorIndex,
                text = text.Content,
                position = new { x = text.Position.X, y = text.Position.Y },
                height = text.Height,
                rotation = text.Rotation,
                justify = text.Justify.ToString(),
                textAlignmentPoint = new { x = text.TextAlignmentPoint.X, y = text.TextAlignmentPoint.Y },
                bounds = new
                {
                    left = text.Bounds.Left,
                    right = text.Bounds.Right,
                    top = text.Bounds.Top,
                    bottom = text.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateCircleObj(DwgCircle circle, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(circle);
            return new
            {
                guid,
                name,
                layerName = circle.Layer?.Name ?? "none",
                colorMode = GetColorMode(circle.Color),
                colorIndex = circle.Color.ColorIndex,
                center = new { x = circle.Center.X, y = circle.Center.Y },
                radius = circle.Radius,
                bounds = new
                {
                    left = circle.Bounds.Left,
                    right = circle.Bounds.Right,
                    top = circle.Bounds.Top,
                    bottom = circle.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateLineObj(DwgLine line, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(line);
            return new
            {
                guid,
                name,
                layerName = line.Layer?.Name ?? "none",
                colorMode = GetColorMode(line.Color),
                colorIndex = line.Color.ColorIndex,
                startPoint = new { x = line.StartPoint.X, y = line.StartPoint.Y },
                endPoint = new { x = line.EndPoint.X, y = line.EndPoint.Y },
                length = line.Length,
                bounds = new
                {
                    left = line.Bounds.Left,
                    right = line.Bounds.Right,
                    top = line.Bounds.Top,
                    bottom = line.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateHatchObj(DwgHatch hatch, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(hatch);
            var contours = hatch.BoundaryPath.Select(path => new
            {
                points = path.Contur.Select(p => new { x = p.X, y = p.Y }).ToArray()
            }).ToArray();
            return new
            {
                guid,
                name,
                layerName = hatch.Layer?.Name ?? "none",
                colorMode = GetColorMode(hatch.Color),
                colorIndex = hatch.Color.ColorIndex,
                contours,
                patternName = hatch.PatternName,
                patternScale = hatch.PatternScale,
                patternAngle = hatch.PatternAngle,
                patternType = hatch.PatternType.ToString(),
                hatchStyle = hatch.HatchStyle.ToString(),
                isSolid = hatch.IsSolid,
                area = hatch.Area,
                bounds = new
                {
                    left = hatch.Bounds.Left,
                    right = hatch.Bounds.Right,
                    top = hatch.Bounds.Top,
                    bottom = hatch.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateBlockInsertObj(DwgInsert insert, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(insert);
            return new
            {
                guid,
                name,
                blockName = insert.Block?.Name ?? "none",
                layerName = insert.Layer?.Name ?? "none",
                colorMode = GetColorMode(insert.Color),
                colorIndex = insert.Color.ColorIndex,
                position = new { x = insert.Position.X, y = insert.Position.Y, z = insert.Position.Z },
                rotation = insert.Rotation,
                scale = new { x = insert.Scale.X, y = insert.Scale.Y, z = insert.Scale.Z },
                bounds = new
                {
                    left = insert.Bounds.Left,
                    right = insert.Bounds.Right,
                    top = insert.Bounds.Top,
                    bottom = insert.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreatePointPlantObj(DwgSmdxPointLandscaping pointPlant, string libUid, string guid, string name)
        {
            var (type, typeDescription) = GetEntityType(pointPlant);
            return new
            {
                guid,
                name,
                layerName = pointPlant.Layer?.Name ?? "none",
                colorMode = GetColorMode(pointPlant.Color),
                colorIndex = pointPlant.Color.ColorIndex,
                position = new { x = pointPlant.Position.X, y = pointPlant.Position.Y },
                plantElement = new
                {
                    libUid,
                    name = pointPlant.PlantElement.Name
                },
                bounds = new
                {
                    left = pointPlant.Bounds.Left,
                    right = pointPlant.Bounds.Right,
                    top = pointPlant.Bounds.Top,
                    bottom = pointPlant.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateSolidObj(DwgModel3DElement solidEntity, string guid, string name)
        {
            var solidElement = solidEntity.Element as StaticSolidElement ??
                throw new ArgumentException("Element is not solid", nameof(solidEntity));
            var (type, typeDescription) = GetEntityType(solidEntity);
            var shell = solidElement.GetBrep();
            return new
            {
                guid,
                name,
                layerName = solidEntity.Layer?.Name ?? "none",
                colorMode = GetColorMode(solidEntity.Color),
                colorIndex = solidEntity.Color.ColorIndex,
                vertexCount = Convert.ToString(shell?.Vertices?.Count()) ?? "none",
                edgeCount = Convert.ToString(shell?.Edges?.Count()) ?? "none",
                faceCount = Convert.ToString(shell?.Faces?.Count()) ?? "none",
                bounds = new
                {
                    left = solidEntity.Bounds.Left,
                    right = solidEntity.Bounds.Right,
                    top = solidEntity.Bounds.Top,
                    bottom = solidEntity.Bounds.Bottom
                },
                type,
                typeDescription
            };
        }

        public static object CreateTlcObj(DwgModel3DElement tlcEntity, string guid, string name)
        {
            var tlcModel = tlcEntity.Element as ConstructedModel3dElement ??
                throw new ArgumentException("Element is not tlc model", nameof(tlcEntity));
            var (type, typeDescription) = GetEntityType(tlcEntity);
            var meshBounds = BoundingBox3D.Empty;
            var geometryModel = tlcModel.GetModel();
            if (geometryModel != null)
                meshBounds = geometryModel.GetBounds();
            return new
            {
                guid,
                name,
                position = new { x = tlcEntity.Position.X, y = tlcEntity.Position.Y, z = tlcEntity.Position.Z },
                scale = new { x = tlcEntity.Scale.X, y = tlcEntity.Scale.Y, z = tlcEntity.Scale.Z },
                normal = new { x = tlcEntity.Normal.X, y = tlcEntity.Normal.Y, z = tlcEntity.Normal.Z },
                angle = tlcEntity.Angle,
                layerName = tlcEntity.Layer?.Name ?? "none",
                colorMode = GetColorMode(tlcEntity.Color),
                colorIndex = tlcEntity.Color.ColorIndex,
                bounds = new
                {
                    left = tlcEntity.Bounds.Left,
                    right = tlcEntity.Bounds.Right,
                    top = tlcEntity.Bounds.Top,
                    bottom = tlcEntity.Bounds.Bottom
                },
                meshBounds = CreateBounds3DObj(meshBounds),
                type,
                typeDescription
            };
        }

        public static object CreateBounds3DObj(BoundingBox3D bounds)
        {
            return new
            {
                left = bounds.Left,
                right = bounds.Right,
                top = bounds.Top,
                bottom = bounds.Bottom,
                near = bounds.Near,
                far = bounds.Far
            };
        }

        public static void ApplyEntityLayer(Drawing drawing, DwgEntity entity, string layerName)
        {
            if (layerName == null)
                return;
            if (string.IsNullOrWhiteSpace(layerName))
                throw new InvalidOperationException("Имя слоя сущности (layerName) не может быть пустым.");
            if (!drawing.Layers.IsExists(layerName))
                throw new InvalidOperationException($"Слой с именем {layerName} не содержится в активном чертеже.");
            entity.Layer = drawing.Layers[layerName] ?? throw new InvalidOperationException($"Не удалось получить слой с именем {layerName}.");
        }

        public static string GetColorMode(CadColor color)
        {
            if (color == CadColor.ByLayer)
                return "ByLayer";
            if (color == CadColor.ByBlock)
                return "ByBlock";
            return "Indexed";
        }

        public static void ApplyEntityColor(DwgEntity entity, string colorMode, int? colorIndex)
        {
            if (colorMode == null)
                return;
            if (string.Equals(colorMode, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                entity.Color = CadColor.ByLayer;
            }
            else if (string.Equals(colorMode, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                entity.Color = CadColor.ByBlock;
            }
            else if (string.Equals(colorMode, "Indexed", StringComparison.OrdinalIgnoreCase))
            {
                if (colorIndex == null)
                    throw new InvalidOperationException("Для режима цвета Indexed необходимо передать colorIndex.");
                if (colorIndex.Value < 0)
                    throw new InvalidOperationException("Индекс цвета сущности (colorIndex) не может быть отрицательным.");
                entity.Color = new CadColor(colorIndex.Value);
            }
            else
            {
                throw new InvalidOperationException("Неизвестное значение colorMode. Допустимые значения: Indexed, ByLayer, ByBlock.");
            }
            if (entity is DwgModel3DElement solidEntity && solidEntity.Element is StaticSolidElement solidElement)
            {
                solidEntity.BeginChange();
                try
                {
                    solidElement.Color = solidEntity.Color.Win32Color;
                }
                finally
                {
                    solidEntity.EndChange();
                }
            }
        }

        public static AcPatternType ParsePatternType(string value)
        {
            if (Enum.TryParse(value, true, out AcPatternType patternType))
                return patternType;
            throw new InvalidOperationException(
                $"Неизвестное значение patternType \"{value}\". Допустимые значения: {string.Join(", ", Enum.GetNames(typeof(AcPatternType)))}.");
        }

        public static AcHatchStyle ParseHatchStyle(string value)
        {
            if (Enum.TryParse(value, true, out AcHatchStyle hatchStyle))
                return hatchStyle;
            throw new InvalidOperationException(
                $"Неизвестное значение hatchStyle \"{value}\". Допустимые значения: {string.Join(", ", Enum.GetNames(typeof(AcHatchStyle)))}.");
        }

        public static TextAlignment ParseTextAlignment(string value)
        {
            if (Enum.TryParse(value, true, out TextAlignment alignment))
                return alignment;
            throw new InvalidOperationException(
                $"Неизвестное значение justify \"{value}\". Допустимые значения: {string.Join(", ", Enum.GetNames(typeof(TextAlignment)))}.");
        }

        public static AttachmentPoint ParseAttachmentPoint(string value)
        {
            if (Enum.TryParse(value, true, out AttachmentPoint attachmentPoint))
                return attachmentPoint;
            throw new InvalidOperationException(
                $"Неизвестное значение attachmentPoint \"{value}\". Допустимые значения: {string.Join(", ", Enum.GetNames(typeof(AttachmentPoint)))}.");
        }
    }
}
