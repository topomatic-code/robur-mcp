using System.Collections.Generic;
using System.Linq;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class ProjectTools : ToolProvider
    {
        [ToolDef(
            Name = "project_get_active",
            Domain = ToolDomains.Project,
            Description = "Возвращает данные и структуру активного проекта Robur.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true
        )]
        public object GetActiveProject(Dictionary<string, object> args)
        {
            var projectManager = Container.GetSingleton<IProjectManager>();
            var projectRoot = projectManager.GetProjectTree() ??
                throw new PreconditionFailedException("Не удалось получить активный проект.");

            return new
            {
                result = new
                {
                    projectName = projectRoot.Name,
                    projectTree = CreateProjectElement(projectRoot)
                },
                description = "Активный проект Robur.",
                status = "Активный проект успешно получен."
            };
        }

        private static object CreateProjectElement(ProjectNode projectNode)
        {
            return new
            {
                name = projectNode.Name ?? "none",
                pathId = projectNode.PathId ?? "none",
                type = projectNode.Type ?? "none",
                typeDescription = projectNode.TypeDescription ?? "none",
                children = projectNode.Children.Select(n => CreateProjectElement(n)).ToArray()
            };
        }

        [ToolDef(
            Name = "project_delete_item",
            Domain = ToolDomains.Project,
            Description = "Удаляет элемент из проекта (а также все вложенные элементы).",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'pathId': { 'type': 'string', 'description': 'PathId элемента (из project_get_active).' }
              },
              'required': ['pathId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object DeleteProjectItem(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "pathId");
            if (string.IsNullOrWhiteSpace(pathId))
                throw new BadRequestException("PathId элемента проекта не может быть пустым.");

            var projectManager = Container.GetSingleton<IProjectManager>();
            if (projectManager.GetNode(pathId) == null)
                throw new PreconditionFailedException($"Не удалось найти элемент проекта по указанному PathId {pathId}.");

            var deletedNode = projectManager.RemoveNode(pathId) ??
                throw new ToolExecutionFailedException("Не удалось удалить элемент проекта.");

            return new
            {
                result = CreateProjectElement(deletedNode),
                description = "Удаленный элемент.",
                status = "Элемент проекта успешно удален."
            };
        }

        [ToolDef(
            Name = "project_move_rename_item",
            Domain = ToolDomains.Project,
            Description = "Перемещает или переименовывает элемент активного проекта.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'pathId': { 'type': 'string', 'description': 'PathId элемента (из project_get_active).' },
                'newName': {
                  'type': 'string',
                  'description': 'Новое имя (путь) элемента относительно его текущего расположения. Имена файлов необходимо передавать с расширением. Примеры: Name.ext — переименовать в текущей папке; ../Name.ext — переместить на уровень выше; Inner Folder/Name.ext — переместить во вложенную папку Inner Folder.'
                }
              },
              'required': ['pathId', 'newName'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object MoveRenameProjectItem(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "pathId");
            if (string.IsNullOrWhiteSpace(pathId))
                throw new BadRequestException("PathId элемента проекта не может быть пустым.");

            var newName = JsonUtils.RequireString(args, "newName");
            if (string.IsNullOrWhiteSpace(newName))
                throw new BadRequestException("Новое имя элемента проекта не может быть пустым.");

            var projectManager = Container.GetSingleton<IProjectManager>();
            if (projectManager.GetNode(pathId) == null)
                throw new PreconditionFailedException($"Не удалось найти элемент проекта по указанному PathId {pathId}.");

            var movedNode = projectManager.MoveRenameNode(pathId, newName) ??
                throw new ToolExecutionFailedException("Не удалось переместить или переименовать элемент проекта.");

            return new
            {
                result = CreateProjectElement(movedNode),
                description = "Перемещенный или переименованный элемент.",
                status = "Элемент проекта успешно перемещен или переименован."
            };
        }

        [ToolDef(
            Name = "project_reorder_item",
            Domain = ToolDomains.Project,
            Description = "Перемещает элемент в структуре активного проекта на одну позицию вверх или вниз среди элементов текущего уровня.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'pathId': { 'type': 'string', 'description': 'PathId элемента (из project_get_active).' },
                'direction': {
                  'type': 'string',
                  'enum': ['up', 'down'],
                  'description': 'Направление перемещения: up — на одну позицию вверх, down — на одну позицию вниз.'
                }
              },
              'required': ['pathId', 'direction'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object ReorderProjectItem(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "pathId");
            if (string.IsNullOrWhiteSpace(pathId))
                throw new BadRequestException("PathId элемента проекта не может быть пустым.");

            var direction = JsonUtils.RequireString(args, "direction");
            if (direction != "up" && direction != "down")
                throw new BadRequestException("Направление перемещения должно иметь значение up или down.");

            var projectManager = Container.GetSingleton<IProjectManager>();
            if (projectManager.GetNode(pathId) == null)
                throw new PreconditionFailedException($"Не удалось найти элемент проекта по указанному PathId {pathId}.");

            var reorderedNode = projectManager.ReorderNode(pathId, direction == "up") ??
                throw new ToolExecutionFailedException("Не удалось изменить порядок элемента проекта.");

            return new
            {
                result = CreateProjectElement(reorderedNode),
                description = "Элемент с измененным положением в структуре проекта.",
                status = direction == "up"
                    ? "Элемент проекта успешно перемещен на одну позицию вверх."
                    : "Элемент проекта успешно перемещен на одну позицию вниз."
            };
        }

        [ToolDef(
            Name = "project_create_folder",
            Domain = ToolDomains.Project,
            Description = "Создает папку в структуре проекта.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'parentPathId': { 'type': 'string', 'description': 'PathId родительского элемента (из project_get_active).' },
                'folderName': { 'type': 'string', 'description': 'Название папки.' }
              },
              'required': ['parentPathId', 'folderName'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateFolder(Dictionary<string, object> args)
        {
            var parentPathId = JsonUtils.RequireString(args, "parentPathId");

            var folderName = JsonUtils.RequireString(args, "folderName");
            if (string.IsNullOrWhiteSpace(folderName))
                throw new BadRequestException("Название папки не может быть пустым.");

            var projectManager = Container.GetSingleton<IProjectManager>();
            if (projectManager.GetNode(parentPathId) == null)
                throw new PreconditionFailedException($"Не удалось найти родительский элемент проекта по указанному PathId {parentPathId}.");

            var folderNode = projectManager.CreateFolder(parentPathId, folderName) ??
                throw new ToolExecutionFailedException("Не удалось создать папку.");

            return new
            {
                result = CreateProjectElement(folderNode),
                description = "Созданная папка.",
                status = "Папка успешно создана."
            };
        }

        [ToolDef(
            Name = "project_activate_model",
            Domain = ToolDomains.Project,
            Description = "Активирует модель проекта, открывая ее при необходимости.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'pathId': { 'type': 'string', 'description': 'PathId модели (из project_get_active).' }
              },
              'required': ['pathId'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object ActivateModel(Dictionary<string, object> args)
        {
            var pathId = JsonUtils.RequireString(args, "pathId");

            if (string.IsNullOrWhiteSpace(pathId))
                throw new BadRequestException("PathId элемента проекта не может быть пустым.");

            var projectManager = Container.GetSingleton<IProjectManager>();
            var node = projectManager.ActivateModel(pathId) ??
                throw new PreconditionFailedException($"Не удалось найти элемент проекта по указанному PathId {pathId}.");

            return new
            {
                result = CreateProjectElement(node),
                description = "Активированный элемент.",
                status = "Элемент проекта успешно активирован."
            };
        }

        [ToolDef(
            Name = "project_get_active_windows",
            Domain = ToolDomains.Project,
            Description = "Возвращает список окон (вкладок) активной модели.",
            InputSchema = @"{
              'type': 'object',
              'properties': {},
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object GetActiveWindows(Dictionary<string, object> args)
        {
            var projectManager = Container.GetSingleton<IProjectManager>();
            var windows = projectManager.GetActiveWindows();

            return new
            {
                result = new
                {
                    windowCount = windows.Count,
                    windows = windows.Select(w => CreateWindowObj(w)).ToArray()
                },
                description = "Окна (вкладки) активной модели.",
                status = "Окна (вкладки) активной модели успешно получены."
            };
        }

        [ToolDef(
            Name = "close_window",
            Domain = ToolDomains.Project,
            Description = "Закрывает окно (вкладку) активной модели по UID. Закрывать можно только динамические окна (Dynamic = true).",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'uid': { 'type': 'string', 'description': 'UID окна (вкладки) из списка, возвращаемого project_get_active_windows.' }
              },
              'required': ['uid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CloseWindow(Dictionary<string, object> args)
        {
            var uid = JsonUtils.RequireString(args, "uid");

            if (string.IsNullOrWhiteSpace(uid))
                throw new BadRequestException("UID окна (вкладки) не может быть пустым.");

            var projectManager = Container.GetSingleton<IProjectManager>();

            var closedWindow = projectManager.CloseWindow(uid) ??
                throw new BadRequestException($"Активная модель не содержит окно (вкладку) с переданным UID: {uid}.");

            return new
            {
                result = CreateWindowObj(closedWindow),
                description = "Закрытое окно (вкладка).",
                status = "Окно (вкладка) успешно закрыто."
            };
        }

        [ToolDef(
            Name = "activate_window",
            Domain = ToolDomains.Project,
            Description = "Активирует окно (вкладку) активной модели по UID.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'uid': { 'type': 'string', 'description': 'UID окна (вкладки) из списка, возвращаемого project_get_active_windows.' }
              },
              'required': ['uid'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true
        )]
        public object ActivateWindow(Dictionary<string, object> args)
        {
            var uid = JsonUtils.RequireString(args, "uid");

            if (string.IsNullOrWhiteSpace(uid))
                throw new BadRequestException("UID окна (вкладки) не может быть пустым.");

            var projectManager = Container.GetSingleton<IProjectManager>();

            var activatedWindow = projectManager.ActivateWindow(uid) ??
                throw new BadRequestException($"Активная модель не содержит окно (вкладку) с переданным UID: {uid}.");

            return new
            {
                result = CreateWindowObj(activatedWindow),
                description = "Активированное окно (вкладка).",
                status = "Окно (вкладка) успешно активировано."
            };
        }

        [ToolDef(
            Name = "project_add_quick_drawing",
            Domain = ToolDomains.Project,
            Description = "Создает новое окно (вкладку) с пустым быстрым чертежом в активном проекте.",
            InputSchema = @"{
              'type': 'object',
              'properties': {
                'windowName': { 'type': 'string', 'description': 'Название нового окна (вкладки) с быстрым чертежом. Имена длиннее 20 символов сокращаются до первых 17 символов с многоточием. Итоговое имя должно быть уникальным среди окон активного проекта.' }
              },
              'required': ['windowName'],
              'additionalProperties': false
            }",
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = false
        )]
        public object AddQuickDrawing(Dictionary<string, object> args)
        {
            var windowName = JsonUtils.RequireString(args, "windowName");

            if (string.IsNullOrWhiteSpace(windowName))
                throw new BadRequestException("Название окна (вкладки) не может быть пустым.");

            var projectManager = Container.GetSingleton<IProjectManager>();
            var window = projectManager.AddQuickDrawing(windowName) ??
                throw new PreconditionFailedException("Не удалось создать быстрый чертеж.");

            return new
            {
                result = CreateWindowObj(window),
                description = "Новое окно (вкладка) с быстрым чертежом.",
                status = "Новое окно (вкладка) с быстрым чертежом успешно создано."
            };
        }

        public static object CreateWindowObj(WindowInfo windowInfo)
        {
            return new
            {
                windowInfo.Name,
                windowInfo.UID,
                windowInfo.Dynamic,
                windowInfo.HasCadView,
                windowInfo.HasDrawing,
                windowInfo.IsQuickDwg
            };
        }
    }
}
