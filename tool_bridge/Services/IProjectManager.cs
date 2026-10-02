using System;
using System.Collections.Generic;
using Topomatic.FoundationClasses;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Services
{
    internal interface IProjectManager
    {
        ProjectNode GetProjectTree();
        ProjectNode GetNode(URI nodeUri);
        bool ContainsNode(URI nodeUri);
        List<ProjectNode> FindNodes(Predicate<ProjectNode> predicate);
        ProjectNode RemoveNode(URI nodeUri);
        ProjectNode MoveRenameNode(URI nodeUri, string newName);
        ProjectNode ReorderNode(URI nodeUri, bool upDirection);
        ProjectNode CreateFolder(URI parentUri, string folderName);
        ProjectNode ActivateModel(URI nodeUri);
        List<WindowInfo> GetActiveWindows();
        WindowInfo CloseWindow(string uid);
        WindowInfo ActivateWindow(string uid);
        WindowInfo AddQuickDrawing(string windowName);
    }
}
