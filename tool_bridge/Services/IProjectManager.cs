using System;
using System.Collections.Generic;
using Topomatic.ToolBridge.Services.Models;

namespace Topomatic.ToolBridge.Services
{
    internal interface IProjectManager
    {
        ProjectNode GetProjectTree();
        ProjectNode GetNode(string pathId);
        bool ContainsNode(string pathId);
        List<ProjectNode> FindNodes(Predicate<ProjectNode> predicate);
        ProjectNode RemoveNode(string pathId);
        ProjectNode MoveRenameNode(string pathId, string newName);
        ProjectNode ReorderNode(string pathId, bool upDirection);
        ProjectNode CreateFolder(string parentPathId, string folderName);
        ProjectNode ActivateModel(string pathId);
        List<WindowInfo> GetActiveWindows();
        WindowInfo CloseWindow(string uid);
        WindowInfo ActivateWindow(string uid);
        WindowInfo AddQuickDrawing(string windowName);
    }
}
