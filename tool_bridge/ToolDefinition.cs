using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    internal sealed class ToolDefinition
    {
        public ToolDefinition(string name, string domain, string description, JObject inputSchema, ToolAnnotations annotations, bool defaultEnabled)
        {
            Name = name;
            Domain = domain;
            Description = description;
            InputSchema = inputSchema;
            Annotations = annotations;
            DefaultEnabled = defaultEnabled;
        }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("domain")]
        public string Domain { get; }

        [JsonProperty("description")]
        public string Description { get; }

        [JsonProperty("inputSchema")]
        public JObject InputSchema { get; }

        [JsonProperty("annotations")]
        public ToolAnnotations Annotations { get; }

        [JsonIgnore]
        public bool DefaultEnabled { get; }
    }
}
