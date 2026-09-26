using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Topomatic.ToolBridge;

namespace ToolBridge_v0_1_0_ApiChecker
{
    internal static partial class ApiChecks
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckJson()
        {
            var nested = new Dictionary<string, object> { { "x", 1.0 }, { "y", 2.0 }, { "z", 3.0 } };
            var source = new Dictionary<string, object>
            {
                { "object", nested }, { "jsonObject", JObject.Parse("{\"x\":1}") },
                { "text", new JValue("hello") }, { "int", new JValue(7) }, { "number", 2.5 },
                { "bool", "true" }, { "vector", nested },
                { "objects", JArray.Parse("[{\"key\":\"value\"}]") },
                { "strings", JArray.Parse("[\"a\",\"b\"]") }, { "ints", JArray.Parse("[1,2,3]") },
                { "null", null }, { "wrong", new object() }
            };
            Require(Equals(JsonUtils.UnwrapJsonValue(new JValue(7)), 7L) ||
                Equals(JsonUtils.UnwrapJsonValue(new JValue(7)), 7), "UnwrapJsonValue token");
            Require(ReferenceEquals(JsonUtils.UnwrapJsonValue(nested), nested) && JsonUtils.UnwrapJsonValue(null) == null,
                "UnwrapJsonValue non-token/null");
            Require(ReferenceEquals(JsonUtils.GetObject(source, "object", null), nested) &&
                ReferenceEquals(JsonUtils.RequireObject(source, "object"), nested), "JSON object");
            Require(Convert.ToInt32(JsonUtils.RequireObject(source, "jsonObject")["x"]) == 1, "JObject conversion");
            Require(ReferenceEquals(JsonUtils.GetObject(source, "missing", nested), nested), "object default");
            Require(JsonUtils.GetString(source, "text", null) == "hello" && JsonUtils.RequireString(source, "text") == "hello" &&
                JsonUtils.GetString(source, "null", "fallback") == "fallback", "string and default");
            Require(JsonUtils.GetInt(source, "int", null) == 7 && JsonUtils.RequireInt(source, "int") == 7 &&
                JsonUtils.GetInt(source, "missing", 42) == 42, "integer and default");
            Require(JsonUtils.GetDouble(source, "number", null) == 2.5 && JsonUtils.RequireDouble(source, "number") == 2.5 &&
                JsonUtils.GetDouble(source, "missing", 42) == 42, "double and default");
            var vector = JsonUtils.RequireVector3D(source, "vector");
            var optionalVector = JsonUtils.GetVector3D(source, "vector", null);
            Require(vector.X == 1 && vector.Y == 2 && vector.Z == 3 && optionalVector.HasValue &&
                optionalVector.Value.Equals(vector) && JsonUtils.GetVector3D(source, "missing", vector).Value.Equals(vector),
                "vector and default");
            Require(JsonUtils.GetBool(source, "bool", null) == true && JsonUtils.RequireBool(source, "bool") &&
                JsonUtils.GetBool(source, "missing", false) == false, "boolean and default");
            var objectArray = JsonUtils.RequireArray(source, "objects");
            Require(objectArray.Length == 1 && Equals(objectArray[0]["key"], "value") &&
                JsonUtils.GetArray(source, "objects", null).Length == 1 &&
                ReferenceEquals(JsonUtils.GetArray(source, "missing", objectArray), objectArray), "object arrays");
            var strings = new[] { "a", "b" };
            Require(JsonUtils.RequireStringArray(source, "strings").SequenceEqual(strings) &&
                JsonUtils.GetStringArray(source, "strings", null).SequenceEqual(strings) &&
                ReferenceEquals(JsonUtils.GetStringArray(source, "missing", strings), strings), "string arrays");
            var ints = new[] { 1, 2, 3 };
            Require(JsonUtils.RequireIntArray(source, "ints").SequenceEqual(ints) &&
                JsonUtils.GetIntArray(source, "ints", null).SequenceEqual(ints) &&
                ReferenceEquals(JsonUtils.GetIntArray(source, "missing", ints), ints), "integer arrays");

            foreach (var numeric in new object[] { 7, 7L, 7.0, 7.0f, 7m, "7" })
            {
                source["numeric"] = numeric;
                Require(JsonUtils.RequireInt(source, "numeric") == 7 && JsonUtils.RequireDouble(source, "numeric") == 7,
                    "numeric conversion " + numeric.GetType().Name);
            }
            source["fraction"] = 1.9;
            Require(JsonUtils.RequireInt(source, "fraction") == 1, "0.1 fractional-to-integer truncation");
            source["bool"] = false;
            Require(!JsonUtils.RequireBool(source, "bool"), "native boolean");

            var required = new Action<string>[]
            {
                key => JsonUtils.RequireObject(source, key), key => JsonUtils.RequireString(source, key),
                key => JsonUtils.RequireInt(source, key), key => JsonUtils.RequireDouble(source, key),
                key => JsonUtils.RequireVector3D(source, key), key => JsonUtils.RequireBool(source, key),
                key => JsonUtils.RequireArray(source, key), key => JsonUtils.RequireStringArray(source, key),
                key => JsonUtils.RequireIntArray(source, key)
            };
            foreach (var require in required)
            {
                Expect<ArgumentException>(() => require("missing"), "missing required JSON value");
                Expect<ArgumentException>(() => require("wrong"), "invalid JSON value");
            }
            Expect<ArgumentNullException>(() => JsonUtils.GetString(null, "x", null), "null JSON source");
            source["objects"] = JArray.Parse("[1]");
            source["strings"] = JArray.Parse("[1]");
            source["ints"] = JArray.Parse("[\"bad\"]");
            Expect<ArgumentException>(() => JsonUtils.RequireArray(source, "objects"), "invalid object array item");
            Expect<ArgumentException>(() => JsonUtils.RequireStringArray(source, "strings"), "invalid string array item");
            Expect<ArgumentException>(() => JsonUtils.RequireIntArray(source, "ints"), "invalid integer array item");
        }
    }
}
