using System.Collections.Generic;
using Topomatic.ApplicationPlatform.Core;

namespace Topomatic.ToolBridge.Services.Models
{
    internal sealed class ProjectNode
    {
        public ProjectNode()
        {
            Children = new List<ProjectNode>();
        }

        public string Name { get; set; }
        public string PathSegment { get; set; }
        public string PathId { get; set; }
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
