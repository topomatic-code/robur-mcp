using System.Collections.Generic;
using System.Linq;
using Topomatic.FoundationClasses;
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
