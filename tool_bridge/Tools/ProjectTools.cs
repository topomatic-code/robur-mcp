using System;
using System.Collections.Generic;
using System.Linq;
using Topomatic.FoundationClasses;
using Topomatic.ToolBridge.Services;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Tools
{
    internal sealed class ProjectTools : ToolProvider
    {
        [ToolDef(
            Name = "project_get_active",
            Description = "[ПРОЕКТ] Возвращает данные и структуру активного проекта Robur.",
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
            var projectRoot = projectManager.GetProjectTree();
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
            Description = "[ПРОЕКТ] Удаляет элемент из проекта (а также все вложенные элементы).",
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
            var uri = new URI(uriStr);
            var deletedNode = ProjectManager.Instance.RemoveNode(uri) ??
                throw new InvalidOperationException($"Не удалось найти элемент проекта по указанному uri {uriStr}. Проверьте правильность переданного uri.");
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
            Description = "[ПРОЕКТ] Создает папку в структуре проекта.",
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
            var parentUri = new URI(parentUriStr);
            var folderName = JsonUtils.RequireString(args, "folderName");
            var folderNode = ProjectManager.Instance.CreateFolder(parentUri, folderName) ?? throw new InvalidOperationException("Не удалось создать папку.");
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
