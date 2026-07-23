using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    internal sealed class BridgeRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public Dictionary<string, object> Params { get; set; }
    }
}
