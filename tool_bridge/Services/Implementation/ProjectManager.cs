using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Topomatic.Acax.Export;
using Topomatic.Acax.Import.Dxf;
using Topomatic.ApplicationPlatform;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.View;
using Topomatic.Dwg;
using Topomatic.Dwg.Layer;
using Topomatic.FoundationClasses;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Services.Implementation
{
    internal sealed class ProjectManager : IProjectManager
    {
        private const string QUICK_DWG_PREFIX = "quick_dwg";

        public ProjectManager()
        {

        }

        public ProjectNode GetProjectTree()
        {
            var appHost = ApplicationHost.Current;
            var project = appHost.ActiveProject as ModelProject;
            var projectModel = project.Model;

            if (project == null || projectModel == null)
                throw new InvalidOperationException("Не удалось получить активный проект.");

            var root = new ProjectNode()
            {
                Name = GetName(projectModel),
                PathSegment = "",
                PathId = PluginCoreOps.FindModelPathId(projectModel),
                Type = projectModel.ModelType,
                TypeDescription = "Проект",
                Model = projectModel
            };

            foreach (var child in projectModel.GetChilds())
            {
                var pathId = PluginCoreOps.FindModelPathId(child);
                var pathFragments = pathId.TrimStart(':').Split('/');
                var node = root;

                foreach (var fragment in pathFragments)
                {
                    var childNode = node.Children.FirstOrDefault(n => n.PathSegment == fragment);
                    if (childNode == null)
                    {
                        childNode = new ProjectNode() { PathSegment = fragment, Parent = node };
                        node.Children.Add(childNode);
                    }
                    node = childNode;
                }

                node.Name = GetName(child);
                node.PathId = pathId;
                node.Type = child.ModelType;

                switch (node.Type)
                {
                    case ProjectNodeTypes.FOLDER:
                        node.TypeDescription = "(folder) Папка";
                        break;
                    case ProjectNodeTypes.DTM:
                        node.TypeDescription = "(model) Поверхность";
                        break;
                    case ProjectNodeTypes.ROAD:
                        node.TypeDescription = "(model) Трасса автомобильной дороги";
                        break;
                    case ProjectNodeTypes.SURVEY:
                        node.TypeDescription = "(model) Изыскательская (геологическая) трасса";
                        break;
                    case ProjectNodeTypes.GLOBAL_GLG:
                        node.TypeDescription = "(model) Геология";
                        break;
                    case ProjectNodeTypes.CULVERT:
                        node.TypeDescription = "(model) Водопропускная труба";
                        break;
                    case ProjectNodeTypes.APPLICATION_DWG:
                        node.TypeDescription = "(model) Чертеж в формате dwg";
                        break;
                    case ProjectNodeTypes.APPLICATION_CULVERT_DWL:
                        node.TypeDescription = "(model) Динамический чертеж водопропускной трубы";
                        break;
                    default:
                        node.TypeDescription = "none";
                        break;
                }

                node.Model = child;
            }

            // Фильтрация от поврежденных элементов проекта
            var stack = new Stack<ProjectNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var child in node.Children.ToList())
                {
                    if (child.Model == null || child.Model.Uri == null || string.IsNullOrWhiteSpace(child.Type))
                        node.Children.Remove(child);
                    else
                        stack.Push(child);
                }
            }

            return root;
        }

        private static string GetName(IProjectModel model) =>
            ApplicationHost.Current.Plugins.Execute("getname", new object[] { model }) as string;

        public ProjectNode GetNode(string pathId)
        {
            var root = GetProjectTree();
            var stack = new Stack<ProjectNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();

                if (string.Equals(node.PathId, pathId))
                    return node;

                node.Children.ForEach(child => stack.Push(child));
            }
            return null;
        }

        public bool ContainsNode(string pathId) => GetNode(pathId) != null;

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

        public ProjectNode RemoveNode(string pathId)
        {
            var node = GetNode(pathId);
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

        public ProjectNode MoveRenameNode(string pathId, string newName)
        {
            var node = GetNode(pathId);
            if (string.IsNullOrWhiteSpace(newName) || node == null)
                return null;

            if (node.Type != ProjectNodeTypes.FOLDER)
            {
                var ext = Path.GetExtension(node.PathSegment);
                if (string.IsNullOrWhiteSpace(ext))
                    throw new PreconditionFailedException("Не удалось определить расширение существующего файла модели.");

                var newExt = Path.GetExtension(newName);
                if (string.IsNullOrWhiteSpace(newExt))
                    throw new BadRequestException($"Неверное новое имя файла {newName}. Имена файлов следует передавать с расширением!");

                if (ext != newExt)
                    throw new BadRequestException($"Изменение расширения файла {node.PathId} недопустимо.");
            }

            try
            {
                ApplicationHost.Current.Plugins.Execute("mvitem", new object[] { node.PathId, newName });

            }
            catch (MessageException e)
            {
                throw new PreconditionFailedException(e.Message, innerException: e);
            }

            return FindNodes(n =>
                string.Equals(n.Model.Uri.AsAbsoluteUri, new URI(node.Model.Uri.DirectoryUri, newName).AsAbsoluteUri)
            ).SingleOrDefault();
        }

        public ProjectNode ReorderNode(string pathId, bool upDirection)
        {
            var node = GetNode(pathId);
            if (node == null)
                return null;

            try
            {
                if (upDirection)
                    ApplicationHost.Current.Plugins.Execute("coreitem_up", new object[] { node.PathId });
                else
                    ApplicationHost.Current.Plugins.Execute("coreitem_down", new object[] { node.PathId });
            }
            catch (MessageException e)
            {
                throw new PreconditionFailedException(e.Message, innerException: e);
            }

            return node;
        }

        public ProjectNode CreateFolder(string parentPathId, string folderName)
        {
            var parentNode = GetNode(parentPathId) ?? throw new InvalidOperationException("Не удалось получить родительский элемент.");
            var parentModel = parentNode.Model ?? throw new InvalidOperationException("У родительского элемента проекта отсутствует модель.");
            var project = parentModel.Project ?? throw new InvalidOperationException("Не удалось получить проект.");
            project.BeginUpdate();
            try
            {
                var folderModel = PluginCoreOps.CreateFolder(parentModel, folderName);
                var folderNode = GetNode(PluginCoreOps.FindModelPathId(folderModel));
                return folderNode;
            }
            finally
            {
                project.EndUpdate();
            }
        }

        public ProjectNode ActivateModel(string pathId)
        {
            var node = GetNode(pathId);

            if (node != null)
            {
                var availableModels = ApplicationHost.Current.GetAvailableModelTypes().Except(new[] { ProjectNodeTypes.PROJECT });

                if (!availableModels.Any(modelType => string.Equals(modelType, node.Type)))
                    throw new BadRequestException($"Неподдерживаемый тип модели {node.Type ?? "none"}.");

                if (!(bool)ApplicationHost.Current.Plugins.Execute("activate", new object[] { node.Model }))
                    ApplicationHost.Current.Plugins.Execute("open", new object[] { node.Model });

                if (!(bool)ApplicationHost.Current.Plugins.Execute("opened", new object[] { node.Model }))
                    throw new ToolExecutionFailedException($"Не удалось активировать элемент проекта {node.Name}.");
            }

            return node;
        }

        public List<WindowInfo> GetActiveWindows()
        {
            var activeWindows = new List<WindowInfo>();

            foreach (var window in ApplicationHost.Current.ActiveProject.GetWindows())
            {
                CadView cadView = null;

                if (window is IFramableDocumentWindow framable)
                    cadView = framable.CadView;

                activeWindows.Add(
                    new WindowInfo()
                    {
                        Name = window.Text,
                        UID = window.UID,
                        Dynamic = window.CloseButton,
                        Window = window,
                        HasCadView = cadView != null,
                        HasDrawing = cadView != null && DrawingLayer.GetDrawingLayer(cadView)?.Drawing != null,
                        IsQuickDwg = window.UID.StartsWith(QUICK_DWG_PREFIX)
                    }
                );
            }

            return activeWindows;
        }

        public WindowInfo CloseWindow(string uid)
        {
            var activeWindows = GetActiveWindows();

            foreach (var windowInfo in activeWindows)
            {
                if (string.Equals(windowInfo.UID, uid))
                {
                    if (!windowInfo.Dynamic)
                    {
                        throw new BadRequestException(
                            $"Окно {windowInfo.Name} с UID: {windowInfo.UID} не является динамическим. " +
                            "Закрывать можно только динамические окна."
                        );
                    }
                    windowInfo.Window.Close();
                    return windowInfo;
                }
            }

            return null;
        }

        public WindowInfo ActivateWindow(string uid)
        {
            var activeWindows = GetActiveWindows();

            foreach (var windowInfo in activeWindows)
            {
                if (string.Equals(windowInfo.UID, uid))
                {
                    windowInfo.Window.Activate();
                    return windowInfo;
                }
            }

            return null;
        }

        public WindowInfo AddQuickDrawing(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
                throw new BadRequestException($"Передано некорректное имя окна (вкладки): {windowName}.");

            if (windowName.Length > 20)
                windowName = windowName.Substring(0, 17) + "...";

            var activeProject = ApplicationHost.Current.ActiveProject;

            if (activeProject == null)
                return null;

            var windows = activeProject.GetWindows();
            if (windows.Any(w => string.Equals(w.Text, windowName)))
                throw new PreconditionFailedException($"Проект уже содержит вкладку (окно) с именем {windowName}.");

            var drawing = new Drawing();

            var guid = Guid.NewGuid();
            var uid = $"{QUICK_DWG_PREFIX}_{Guid.NewGuid()}";
            var window = activeProject.AddDocumentWindow(uid);
            window.Text = windowName;
            window.CloseButton = true;
            window.DisplayTabs = true;

            var frame = window.AddCadViewFrame(Consts.ModelFrame, "Модель");
            var cadView = frame.CadView;
            cadView.ShowScreenRotationSetting = true;
            cadView.ShowScreenScaleRatio = true;
            cadView.ShowDriverSetting = true;
            cadView.ShowUCSSetting = true;
            cadView.MultiSelect = true;
            cadView.DraftingSettings.DrawGrid = true;

            cadView.ContextMenu = new ContextMenu(
                new[]
                {
                    new MenuItem("Загрузить чертеж", (s, e) => {

                        AcaxImporter.ImportWithDialog(drawing, out var fileName);
                        cadView.Unlock();
                        cadView.Invalidate();
                        cadView.SolveLimits();

                    }),
                    new MenuItem("Сохранить чертеж", (s, e) => {

                        var providers = DrawingExportProvider.GetProviders().Values.ToArray();
                        var filters = providers.Select(p => $"{p.DisplayName} (*{p.Extention})|*{p.Extention}").ToArray();
                        using (var saveDlg = new SaveFileDialog())
                        {
                            saveDlg.FileName = windowName.TrimEnd('.');

                            if (windowName.EndsWith("."))
                                saveDlg.FileName += "_";

                            saveDlg.Filter = string.Join("|", filters);

                            if (saveDlg.ShowDialog() != DialogResult.OK)
                                return;

                            var fileName = saveDlg.FileName;
                            var provider = providers[saveDlg.FilterIndex - 1];
                            provider.SaveToFile(fileName, drawing);
                        }

                    })
                }
            );

            cadView.DynamicDraw += (pen, mousePos) =>
            {
                const int TEXT_MARGIN = 12;

                var font = cadView.Font;
                var text = FormattableString.Invariant($"X: {mousePos.X:0.00}, Y: {mousePos.Y:0.00}");
                var textSize = TextRenderer.MeasureText(text, font);
                var x = TEXT_MARGIN;
                var y = cadView.Size.Height - textSize.Height - TEXT_MARGIN;

                var graphics = pen.Graphics;
                graphics.BeginGraphics();
                try
                {
                    graphics.Color = Color.Yellow;
                    graphics.DrawString(text, font, x, y);
                }
                finally
                {
                    graphics.EndGraphics();
                }
            };

            var drawingLayer = new DrawingLayer() { Drawing = drawing };
            cadView.AddLayer(drawingLayer);

            return new WindowInfo()
            {
                Name = window.Text,
                UID = window.UID,
                Dynamic = window.CloseButton,
                Window = window,
                HasCadView = true,
                HasDrawing = true,
                IsQuickDwg = true
            };
        }
    }
}
