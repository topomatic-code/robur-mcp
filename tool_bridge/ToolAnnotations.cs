using Newtonsoft.Json;
using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    internal sealed class ToolAnnotations
    {
        public ToolAnnotations(bool readOnlyHint, bool destructiveHint, bool idempotentHint)
        {
            ReadOnlyHint = readOnlyHint;
            DestructiveHint = destructiveHint;
            IdempotentHint = idempotentHint;
        }

        [JsonProperty("readOnlyHint")]
        public bool ReadOnlyHint { get; }

        [JsonProperty("destructiveHint")]
        public bool DestructiveHint { get; }

        [JsonProperty("idempotentHint")]
        public bool IdempotentHint { get; }
    }
}
