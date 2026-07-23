using System;
using System.Collections.Generic;
using Topomatic.Cad.Foundation;
using Topomatic.Landscaping;
using Topomatic.Visualization;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class LandscapingTools : ToolProvider
    {
        [ToolDef(
            Name = "landscp_get_lib_plant_elements",
            Description = "[ЧЕРТЕЖ, ОЗЕЛЕНЕНИЕ] Возвращает элементы растений для посадки из библиотеки Bim-элементов.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetLibraryPlantElements(Dictionary<string, object> args)
        {
            var plantUids = TypedObjectCollections.Current.FindUids(DwgSmdxLandscaping.PLANT_SMDX, null);
            var plantElements = new List<object>();
            foreach (var libUid in plantUids)
            {
                if (TypedObjectCollections.Current.FindObject(libUid) is ImElement plantElement)
                {
                    plantElements.Add(
                        new
                        {
                            libUid,
                            name = plantElement.Name
                        }
                    );
                }
            }
            return new
            {
                result = plantElements.ToArray(),
                description = "Элементы растений для посадки из библиотеки Bim-элементов.",
                status = "Элементы растений для посадки из библиотеки Bim-элементов успешно получены."
            };
        }

        [ToolDef(
            Name = "landscp_get_plant_element_info",
            Description = "[ЧЕРТЕЖ, ОЗЕЛЕНЕНИЕ] Возвращает подробную информацию об элементе растения для посадки из библиотеки Bim-элементов.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'libUid': { 'type': 'string', 'description': 'Uid-идентификатор Bim-элемента растения (из библиотеки Bim-элементов).' }
              },
              'required': ['libUid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetPlantElementInfo(Dictionary<string, object> args)
        {
            var libUid = JsonUtils.RequireString(args, "libUid");
            var plantElement = TypedObjectCollections.Current.FindObject(libUid) as ImElement ??
                throw new InvalidOperationException("Не удалось найти элемент посадки в библиотеке.");
            return new
            {
                libUid,
                result = SmdxUtils.CreateImElementObj(plantElement),
                description = "Информация об элементе растения для посадки.",
                status = "Информация успешно получена."
            };
        }

        [ToolDef(
            Name = "landscp_create_point_plant",
            Description = "[ЧЕРТЕЖ, ОЗЕЛЕНЕНИЕ] Создает точечный элемент посадки и добавляет его в пространство активного чертежа.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'name': { 'type': 'string', 'description': 'Название создаваемой вставки элемента посадки.' },
                'libUid': { 'type': 'string', 'description': 'Uid-идентификатор Bim-элемента растения (из библиотеки Bim-элементов).' },
                'position': {
                  'type': 'object',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата положения.' },
                    'y': { 'type': 'number', 'description': 'y-координата положения.' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false,
                  'description': 'Положение вставки элемента посадки.'
                },
                'layerName': { 'type': 'string', 'description': 'Имя слоя, на который нужно поместить точечный элемент посадки. Если не задано, используется активный слой.' },
                'colorMode': { 'type': 'string', 'description': 'Режим цвета полилинии.', 'enum': ['Indexed', 'ByLayer', 'ByBlock'] },
                'colorIndex': { 'type': 'integer', 'description': 'Индекс цвета полилинии. Используется только при colorMode = Indexed.' }
              },
              'required': ['name', 'libUid', 'position'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreatePointPlant(Dictionary<string, object> args)
        {
            var drawing = DwgUtils.GetDrawing(CadView) ?? throw new InvalidOperationException("Не удалось получить активный чертеж.");
            var sessionStorage = SessionStorage ?? throw new InvalidOperationException("Cannot get session storage.");
            var name = JsonUtils.RequireString(args, "name");
            var libUid = JsonUtils.RequireString(args, "libUid");
            var plantElement = TypedObjectCollections.Current.FindObject(libUid) as ImElement ??
                throw new InvalidOperationException("Не удалось найти элемент посадки в библиотеке.");
            var position = JsonUtils.RequireObject(args, "position");
            var x = JsonUtils.RequireDouble(position, "x");
            var y = JsonUtils.RequireDouble(position, "y");
            var layerName = JsonUtils.GetString(args, "layerName", null);
            var colorMode = JsonUtils.GetString(args, "colorMode", null);
            var colorIndex = JsonUtils.GetInt(args, "colorIndex", null);
            var guid = Guid.NewGuid();
            var guidStr = guid.ToString();
            var logger = Logger;
            if (logger != null)
                drawing.BeginUpdate(logger.CreateLogString($"Создание точечной посадки \"{name}\""));
            else
                drawing.BeginUpdate();
            try
            {
                var pointPlant = new DwgSmdxPointLandscaping();
                pointPlant.Prepare(drawing);
                pointPlant.PlantElement = plantElement.Clone();
                pointPlant.Position = new Vector3D(x, y, 0);
                drawing.ActiveSpace.Add(pointPlant);
                DwgUtils.ApplyEntityLayer(drawing, pointPlant, layerName);
                DwgUtils.ApplyEntityColor(pointPlant, colorMode, colorIndex);
                if (!pointPlant.HasExtensionDictionary)
                    pointPlant.CreateExtensionDictionary();
                var extDict = pointPlant.GetExtensionDictionary();
                extDict.SetString("guid", guidStr);
                extDict.SetString("name", name);
                extDict.SetString("libUid", libUid);
                sessionStorage.AddObject(guid, pointPlant);
                return new
                {
                    result = DwgUtils.CreatePointPlantObj(pointPlant, libUid, guidStr, name),
                    description = "Созданная вставка точечного элемента посадки.",
                    status = "Точечный элемент посадки успешно создан."
                };
            }
            finally
            {
                drawing.EndUpdate();
            }
        }
    }
}
