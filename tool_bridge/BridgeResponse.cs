using Newtonsoft.Json;
using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    internal sealed class BridgeResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }

        [JsonProperty("error")]
        public BridgeError Error { get; set; }

        public static BridgeResponse OK(string id, object result)
        {
            return new BridgeResponse { Id = id, Ok = true, Result = result };
        }

        public static BridgeResponse Fail(string id, string code, string message, object details)
        {
            return new BridgeResponse
            {
                Id = id,
                Ok = false,
                Error = new BridgeError
                {
                    Code = code,
                    Message = message,
                    Details = details
                }
            };
        }
    }
}
