using System.Collections.Generic;
using System.Linq;
using Topomatic.FoundationClasses;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services;
using Topomatic.ToolBridge.Services.Models;
using Topomatic.ToolBridge.Utils;

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
            var projectManager = ProjectManager.Instance;
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
                name = projectNode.Name,
                uri = projectNode.Uri.AsAbsoluteUri,
                relativePath = projectNode.RelativePath,
                type = projectNode.Type,
                typeDescription = projectNode.TypeDescription,
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
                'uri': { 'type': 'string', 'description': 'Полный глобальный uri элемента проекта (из структуры активного проекта).' }
              },
              'required': ['uri'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object DeleteProjectItem(Dictionary<string, object> args)
        {
            var uriStr = JsonUtils.RequireString(args, "uri");
            if (string.IsNullOrWhiteSpace(uriStr))
                throw new BadRequestException("URI элемента проекта не может быть пустым.");
            var uri = new URI(uriStr);
            var projectManager = ProjectManager.Instance;
            if (projectManager.GetNode(uri) == null)
                throw new PreconditionFailedException($"Не удалось найти элемент проекта по указанному uri {uriStr}.");
            var deletedNode = projectManager.RemoveNode(uri) ??
                throw new ToolExecutionFailedException("Не удалось удалить элемент проекта.");
            return new
            {
                result = new
                {
                    name = deletedNode.Name,
                    uri = deletedNode.Uri.AsAbsoluteUri,
                    relativePath = deletedNode.RelativePath,
                    type = deletedNode.Type,
                    typeDescription = deletedNode.TypeDescription,
                    children = deletedNode.Children.Select(n => CreateProjectElement(n)).ToArray()
                },
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
                'uri': { 'type': 'string', 'description': 'Полный глобальный uri элемента проекта (из структуры активного проекта).' },
                'newName': {
                  'type': 'string',
                  'description': 'Новое имя (путь) элемента относительно его текущего расположения. Имена файлов необходимо передавать с расширением. Примеры: Name.ext — переименовать в текущей папке; ../Name.ext — переместить на уровень выше; Inner Folder/Name.ext — переместить во вложенную папку Inner Folder.'
                }
              },
              'required': ['uri', 'newName'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object MoveRenameProjectItem(Dictionary<string, object> args)
        {
            var uriStr = JsonUtils.RequireString(args, "uri");
            if (string.IsNullOrWhiteSpace(uriStr))
                throw new BadRequestException("URI элемента проекта не может быть пустым.");
            var newName = JsonUtils.RequireString(args, "newName");
            if (string.IsNullOrWhiteSpace(newName))
                throw new BadRequestException("Новое имя элемента проекта не может быть пустым.");
            var uri = new URI(uriStr);
            var projectManager = ProjectManager.Instance;
            if (projectManager.GetNode(uri) == null)
                throw new PreconditionFailedException($"Не удалось найти элемент проекта по указанному uri {uriStr}.");
            var movedNode = projectManager.MoveRenameNode(uri, newName) ??
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
                'uri': { 'type': 'string', 'description': 'Полный глобальный uri элемента проекта (из структуры активного проекта).' },
                'direction': {
                  'type': 'string',
                  'enum': ['up', 'down'],
                  'description': 'Направление перемещения: up — на одну позицию вверх, down — на одну позицию вниз.'
                }
              },
              'required': ['uri', 'direction'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object ReorderProjectItem(Dictionary<string, object> args)
        {
            var uriStr = JsonUtils.RequireString(args, "uri");
            if (string.IsNullOrWhiteSpace(uriStr))
                throw new BadRequestException("URI элемента проекта не может быть пустым.");
            var direction = JsonUtils.RequireString(args, "direction");
            if (direction != "up" && direction != "down")
                throw new BadRequestException("Направление перемещения должно иметь значение up или down.");

            var uri = new URI(uriStr);
            var projectManager = ProjectManager.Instance;
            if (projectManager.GetNode(uri) == null)
                throw new PreconditionFailedException($"Не удалось найти элемент проекта по указанному uri {uriStr}.");
            var reorderedNode = projectManager.ReorderNode(uri, direction == "up") ??
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
                'parentUri': { 'type': 'string', 'description': 'Полный глобальный uri родительского элемента в проекте (из структуры активного проекта).' },
                'folderName': { 'type': 'string', 'description': 'Название папки.' }
              },
              'required': ['parentUri', 'folderName'],
              'additionalProperties': false
            }",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false
        )]
        public object CreateFolder(Dictionary<string, object> args)
        {
            var parentUriStr = JsonUtils.RequireString(args, "parentUri");
            if (string.IsNullOrWhiteSpace(parentUriStr))
                throw new BadRequestException("URI родительского элемента не может быть пустым.");
            var parentUri = new URI(parentUriStr);
            var folderName = JsonUtils.RequireString(args, "folderName");
            if (string.IsNullOrWhiteSpace(folderName))
                throw new BadRequestException("Название папки не может быть пустым.");
            if (ProjectManager.Instance.GetNode(parentUri) == null)
                throw new PreconditionFailedException($"Не удалось найти родительский элемент проекта по указанному uri {parentUriStr}.");
            var folderNode = ProjectManager.Instance.CreateFolder(parentUri, folderName) ??
                throw new ToolExecutionFailedException("Не удалось создать папку.");
            return new
            {
                result = new
                {
                    name = folderNode.Name,
                    uri = folderNode.Uri.AsAbsoluteUri,
                    relativePath = folderNode.RelativePath,
                    type = folderNode.Type,
                    typeDescription = folderNode.TypeDescription,
                    children = folderNode.Children.Select(n => CreateProjectElement(n)).ToArray()
                },
                description = "Созданная папка.",
                status = "Папка успешно создана."
            };
        }
    }
}
