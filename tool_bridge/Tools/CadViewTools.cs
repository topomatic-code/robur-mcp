using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;
using Topomatic.Cad.View.Hints;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class CadViewTools : ToolProvider
    {
        [ToolDef(
            Name = "cad_view_get_point",
            Description = "[ВИДОВОЙ ЭКРАН] Запрашивает у пользователя ввод точки при помощи курсора на текущем видовом экране (CadView). Возвращает точку в координатах пространства текущего вида активной модели.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'message': { 'type': 'string', 'description': 'Сообщение, показываемое пользователю при вводе точки.' }
              },
              'required': ['message'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetPoint(Dictionary<string, object> args)
        {
            var cadView = CadView ?? throw new InvalidOperationException("Не удалось найти активный видовой экран.");
            var message = JsonUtils.RequireString(args, "message");
            if (CadCursors.GetPoint(cadView, out var point, message))
            {
                return new
                {
                    result = new
                    {
                        x = point.X,
                        y = point.Y
                    },
                    description = "Точка на текущем видовом экране.",
                    status = "Точка успешно получена."
                };
            }
            else
            {
                throw new InvalidOperationException("Пользователь отменил ввод точки.");
            }
        }

        [ToolDef(
            Name = "cad_view_get_polygon",
            Description = "[ВИДОВОЙ ЭКРАН] Запрашивает у пользователя ввод контура полилинии при помощи курсора на текущем видовом экране (CadView). Возвращает контур полилинии в координатах пространства текущего вида активной модели.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'message': { 'type': 'string', 'description': 'Сообщение, показываемое пользователю при вводе контура полилинии.' }
              },
              'required': ['message'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetPolygon(Dictionary<string, object> args)
        {
            const string CLOSE_CONTOUR = "Замкнуть контур";
            const string END_INPUT = "Завершить ввод";

            var cadView = CadView ?? throw new InvalidOperationException("Не удалось найти активный видовой экран.");
            var message = JsonUtils.RequireString(args, "message");
            var positions = new List<Vector2D>();

            void drawContour(CadPen pen, Vector3D point)
            {
                if (positions.Count > 1)
                    pen.DrawArray(positions, ArrayMode.Polyline);
                if (positions.Count > 0)
                    pen.DrawLine(positions[positions.Count - 1], point.Pos);
            }

            cadView.DynamicDraw += drawContour;
            try
            {
                var closed = false;
                while (true)
                {
                    string[] options;
                    if (positions.Count < 2)
                        options = new string[0];
                    else if (positions.Count == 2)
                        options = new[] { END_INPUT };
                    else
                        options = new[] { CLOSE_CONTOUR, END_INPUT };
                    var result = CadCursors.GetPoint(cadView, out var pos, message, options);
                    var exit = false;
                    switch (result)
                    {
                        case GetPointResult.Accept:
                            positions.Add(pos.Pos);
                            break;
                        case GetPointResult.UserCmd:
                            var userSelection = cadView.LastUserCmd;
                            if (userSelection == CLOSE_CONTOUR)
                            {
                                closed = true;
                                exit = true;
                            }
                            else if (userSelection == END_INPUT)
                            {
                                exit = true;
                            }
                            else
                            {
                                throw new InvalidOperationException("Не удалось запросить контур.");
                            }
                            break;
                        default:
                            if (positions.Count > 0)
                                positions.RemoveAt(positions.Count - 1);
                            else
                                throw new InvalidOperationException("Пользователь отменил ввод контура.");
                            break;
                    }
                    if (exit)
                        break;
                }
                return new
                {
                    result = new
                    {
                        positions = positions.Select(pos => new { x = pos.X, y = pos.Y }),
                        closed
                    },
                    description = "Контур полилинии на текущем видовом экране.",
                    status = "Контур полилинии успешно получен."
                };
            }
            finally
            {
                cadView.DynamicDraw -= drawContour;
            }
        }

        [ToolDef(
            Name = "cad_view_zoom",
            Description = "[ВИДОВОЙ ЭКРАН] Масштабирует и переносит видовой экран так, чтобы показать переданную область.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'min': {
                  'type': 'object',
                  'description': 'Точка, содержащая минимальные координаты области.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false
                },
                'max': {
                  'type': 'object',
                  'description': 'Точка, содержащая максимальные координаты области.',
                  'properties': {
                    'x': { 'type': 'number', 'description': 'x-координата' },
                    'y': { 'type': 'number', 'description': 'y-координата' }
                  },
                  'required': ['x', 'y'],
                  'additionalProperties': false
                }
              },
              'required': ['min', 'max'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object Zoom(Dictionary<string, object> args)
        {
            var cadView = CadView ?? throw new InvalidOperationException("Не удалось найти активный видовой экран.");
            var minObj = JsonUtils.RequireObject(args, "min");
            var min = new Vector2D(
                JsonUtils.RequireDouble(minObj, "x"),
                JsonUtils.RequireDouble(minObj, "y")
            );
            var maxObj = JsonUtils.RequireObject(args, "max");
            var max = new Vector2D(
                JsonUtils.RequireDouble(maxObj, "x"),
                JsonUtils.RequireDouble(maxObj, "y")
            );
            var bounds = new BoundingBox2D(min, max);
            cadView.ZoomBound(bounds, true);
            return new { status = "Параметры видового экрана успешно применены." };
        }
    }
}
