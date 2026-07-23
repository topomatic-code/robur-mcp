using System.Collections.Generic;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.FoundationClasses;

namespace Topomatic.ToolBridge.Services.Models
{
    internal sealed class ProjectNode
    {
        public ProjectNode()
        {
            Children = new List<ProjectNode>();
        }

        public string Name { get; set; }
        public URI Uri { get; set; }
        public string RelativePath { get; set; }
        public string Type { get; set; }
        public string TypeDescription { get; set; }
        public IProjectModel Model { get; set; }
        public ProjectNode Parent { get; set; }
        public List<ProjectNode> Children { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
