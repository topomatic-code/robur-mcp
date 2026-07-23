using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Topomatic.ApplicationPlatform;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.FoundationClasses;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Services
{
    internal sealed class ProjectManager
    {
        private static ProjectManager m_Instance;

        public static ProjectManager Instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = new ProjectManager();
                return m_Instance;
            }
        }

        private ProjectManager()
        {

        }

        public ProjectNode GetProjectTree()
        {
            var appHost = ApplicationHost.Current;
            var project = appHost.ActiveProject as ModelProject;
            var projectModel = project.Model;
            if (project == null || projectModel == null)
                throw new InvalidOperationException("Не удалось получить активный проект.");
            var projectName = appHost.Plugins.Execute("getname", new object[] { projectModel }) as string;
            if (string.IsNullOrEmpty(projectName))
                throw new InvalidOperationException("Не удалось определить имя проекта");
            var root = new ProjectNode()
            {
                Name = projectName,
                Uri = projectModel.Uri,
                RelativePath = "",
                Type = projectModel.ModelType,
                TypeDescription = "Проект",
                Model = projectModel
            };
            foreach (var child in projectModel.GetChilds())
            {
                var absUri = child.Uri.AsAbsoluteUri;
                var index = absUri.IndexOf(root.Name);
                if (index < 0)
                {
                    Debug.Fail("Unexpected project item Uri");
                    continue;
                }
                var path = absUri.Substring(index + root.Name.Length).TrimStart('/');
                if (path.Length == 0)
                {
                    Debug.Fail("Unexpected project item Uri");
                    continue;
                }
                var pathFragments = path.Split('/');
                var node = root;
                foreach (var fragment in pathFragments)
                {
                    var childNode = node.Children.FirstOrDefault(n => n.Name == fragment);
                    if (childNode == null)
                    {
                        childNode = new ProjectNode() { Name = fragment, Parent = node };
                        node.Children.Add(childNode);
                    }
                    node = childNode;
                }
                node.Uri = child.Uri;
                node.RelativePath = path;
                node.Type = child.ModelType;
                switch (node.Type)
                {
                    case "folder":
                        node.TypeDescription = "(folder) Папка";
                        break;
                    case "dtm":
                        node.TypeDescription = "(model) Поверхность";
                        break;
                    case "road":
                        node.TypeDescription = "(model) Трасса автомобильной дороги";
                        break;
                    case "survey":
                        node.TypeDescription = "(model) Изыскательская (геологическая) трасса";
                        break;
                    case "global_glg":
                        node.TypeDescription = "(model) Геология";
                        break;
                    case "culvert":
                        node.TypeDescription = "(model) Водопропускная труба";
                        break;
                    case "application/dwg":
                        node.TypeDescription = "(model) Чертеж в формате dwg";
                        break;
                    case "application/culvert-dwl":
                        node.TypeDescription = "(model) Динамический чертеж водопропускной трубы";
                        break;
                    default:
                        node.TypeDescription = "none";
                        break;
                }
                node.Model = child;
            }
            return root;
        }

        public ProjectNode GetNode(URI nodeUri)
        {
            var root = GetProjectTree();
            var stack = new Stack<ProjectNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node.Uri.Equals(nodeUri))
                    return node;
                node.Children.ForEach(child => stack.Push(child));
            }
            return null;
        }

        public bool ContainsNode(URI nodeUri) => GetNode(nodeUri) != null;

        public List<ProjectNode> FindNodes(Predicate<ProjectNode> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException("predicate");
            var result = new List<ProjectNode>();
            var root = GetProjectTree();
            var stack = new Stack<ProjectNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (predicate(node))
                    result.Add(node);
                node.Children.ForEach(child => stack.Push(child));
            }
            return result;
        }

        public ProjectNode RemoveNode(URI nodeUri)
        {
            var node = GetNode(nodeUri);
            if (node != null)
            {
                var model = node.Model ?? throw new InvalidOperationException("У элемента проекта отсутствует модель.");
                var project = model.Project ?? throw new InvalidOperationException("Не удалось получить проект, содержащий данную модель.");
                var projectModel = project.Model ?? throw new InvalidOperationException("Не удалось получить проект, содержащий данную модель.");
                project.BeginUpdate();
                try
                {
                    projectModel.Remove(model, false);
                }
                finally
                {
                    project.EndUpdate();
                }
            }
            return node;
        }

        public ProjectNode CreateFolder(URI parentUri, string folderName)
        {
            var parentNode = GetNode(parentUri) ?? throw new InvalidOperationException("Не удалось получить родительский элемент.");
            var parentModel = parentNode.Model ?? throw new InvalidOperationException("У родительского элемента проекта отсутствует модель.");
            var project = parentModel.Project ?? throw new InvalidOperationException("Не удалось получить проект.");
            project.BeginUpdate();
            try
            {
                var folderModel = PluginCoreOps.CreateFolder(parentModel, folderName);
                var folderNode = GetNode(folderModel.Uri);
                return folderNode;
            }
            finally
            {
                project.EndUpdate();
            }
        }
    }
}
