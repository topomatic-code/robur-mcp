using Topomatic.Stg;

namespace Topomatic.ToolBridge.Settings
{
    internal sealed class ToolConfig
    {
        public static ToolConfig LoadFromStg(StgNode node)
        {
            var name = node.Attribute.GetString("Name");
            var domain = node.Attribute.GetString("Domain");
            var description = node.Attribute.GetString("Description");
            var inputSchema = node.Attribute.GetString("InputSchema");
            var readOnly = node.Attribute.GetBoolean("ReadOnly");
            var destructive = node.Attribute.GetBoolean("Destructive");
            var idempotent = node.Attribute.GetBoolean("Idempotent");
            var approvePermanently = node.Attribute.GetBoolean("ApprovePermanently");
            var config = new ToolConfig(name, domain, description, inputSchema, readOnly, destructive, idempotent)
            {
                Enabled = node.Attribute.GetBoolean("Enabled"),
                ApprovalScope = approvePermanently ? ToolApprovalScope.Permanently : ToolApprovalScope.None
            };
            return config;
        }

        public ToolConfig(string name, string domain, string description, string inputSchema, bool readOnly, bool destructive, bool idempotent)
        {
            Name = name;
            Domain = domain;
            Description = description;
            InputSchema = inputSchema;
            ReadOnly = readOnly;
            Destructive = destructive;
            Idempotent = idempotent;
        }

        public string Name { get; }
        public string Domain { get; }
        public string Description { get; }
        public string InputSchema { get; }
        public bool ReadOnly { get; }
        public bool Destructive { get; }
        public bool Idempotent { get; }
        public bool Enabled { get; set; }
        public ToolApprovalScope ApprovalScope { get; set; }

        public void SaveToStg(StgNode node)
        {
            node.Attribute.AddString("Name", Name);
            node.Attribute.AddString("Domain", Domain);
            node.Attribute.AddString("Description", Description);
            node.Attribute.AddString("InputSchema", InputSchema);
            node.Attribute.AddBoolean("ReadOnly", ReadOnly);
            node.Attribute.AddBoolean("Destructive", Destructive);
            node.Attribute.AddBoolean("Idempotent", Idempotent);
            node.Attribute.AddBoolean("Enabled", Enabled);
            node.Attribute.AddBoolean("ApprovePermanently", ApprovalScope == ToolApprovalScope.Permanently);
        }
    }
}
