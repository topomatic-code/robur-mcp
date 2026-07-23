using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    internal sealed class ToolDefinition
    {
        public ToolDefinition(string name, string description, JObject inputSchema, ToolAnnotations annotations)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Annotations = annotations;
        }

        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("description")]
        public string Description { get; }

        [JsonProperty("inputSchema")]
        public JObject InputSchema { get; }

        [JsonProperty("annotations")]
        public ToolAnnotations Annotations { get; }
    }
}
