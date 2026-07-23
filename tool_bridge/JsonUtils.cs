using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Topomatic.Cad.Foundation;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public static class JsonUtils
    {
        public static object UnwrapJsonValue(object value) => value is JValue token ? token.Value : value;

        public static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key, Dictionary<string, object> def) => GetObject(source, key, false, def);
        public static Dictionary<string, object> RequireObject(Dictionary<string, object> source, string key) => GetObject(source, key, true, null);
        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key, bool require, Dictionary<string, object> def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required") : def;
            if (raw is JObject obj)
                return obj.ToObject<Dictionary<string, object>>() ?? throw new ArgumentException($"{key} must be an object");
            return raw as Dictionary<string, object> ?? throw new ArgumentException($"{key} must be an object");
        }

        public static string GetString(Dictionary<string, object> source, string key, string def) => GetString(source, key, false, def);
        public static string RequireString(Dictionary<string, object> source, string key) => GetString(source, key, true, null);
        private static string GetString(Dictionary<string, object> source, string key, bool require, string def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required") : def;
            var value = UnwrapJsonValue(raw);
            if (value is string str)
                return str;
            throw new ArgumentException($"{key} must be a string");
        }

        public static int? GetInt(Dictionary<string, object> source, string key, int? def) => GetInt(source, key, false, def);
        public static int RequireInt(Dictionary<string, object> source, string key) => GetInt(source, key, true, null).Value;
        private static int? GetInt(Dictionary<string, object> source, string key, bool require, int? def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required", nameof(key)) : def;
            var value = UnwrapJsonValue(raw);
            switch (value)
            {
                case int i:
                    return i;
                case long l:
                    return checked((int)l);
                case double d:
                    return (int)d;
                case float f:
                    return (int)f;
                case decimal m:
                    return (int)m;
                case string s:
                    if (int.TryParse(s, out var parsed))
                        return parsed;
                    break;
            }
            throw new ArgumentException($"{key} must be an integer", nameof(key));
        }

        public static double? GetDouble(Dictionary<string, object> source, string key, double? def) => GetDouble(source, key, false, def);
        public static double RequireDouble(Dictionary<string, object> source, string key) => GetDouble(source, key, true, null).Value;
        private static double? GetDouble(Dictionary<string, object> source, string key, bool require, double? def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required", nameof(key)) : def;
            var value = UnwrapJsonValue(raw);
            switch (value)
            {
                case double d:
                    return d;
                case float f:
                    return f;
                case long l:
                    return l;
                case int i:
                    return i;
                case decimal m:
                    return (double)m;
                case string s:
                    if (double.TryParse(s, out var parsed))
                        return parsed;
                    break;
            }
            throw new ArgumentException($"{key} must be a number", nameof(key));
        }

        public static Vector3D? GetVector3D(Dictionary<string, object> source, string key, Vector3D? def) => GetVector3D(source, key, false, def);
        public static Vector3D RequireVector3D(Dictionary<string, object> source, string key) => GetVector3D(source, key, true, null).Value;
        private static Vector3D? GetVector3D(Dictionary<string, object> source, string key, bool require, Vector3D? def)
        {
            var vector = GetObject(source, key, null);
            if (vector == null)
                return require ? throw new ArgumentException($"{key} is required", nameof(key)) : def;
            var x = RequireDouble(vector, "x");
            var y = RequireDouble(vector, "y");
            var z = RequireDouble(vector, "z");
            return new Vector3D(x, y, z);
        }

        public static bool? GetBool(Dictionary<string, object> source, string key, bool? def) => GetBool(source, key, false, def);
        public static bool RequireBool(Dictionary<string, object> source, string key) => GetBool(source, key, true, null).Value;
        private static bool? GetBool(Dictionary<string, object> source, string key, bool require, bool? def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required", nameof(key)) : def;
            var value = UnwrapJsonValue(raw);
            if (value is bool b)
                return b;
            if (value is string s && bool.TryParse(s, out var parsed))
                return parsed;
            throw new ArgumentException($"{key} must be a boolean", nameof(key));
        }

        public static Dictionary<string, object>[] GetArray(Dictionary<string, object> source, string key, Dictionary<string, object>[] def) => GetArray(source, key, false, def);
        public static Dictionary<string, object>[] RequireArray(Dictionary<string, object> source, string key) => GetArray(source, key, true, null);
        private static Dictionary<string, object>[] GetArray(Dictionary<string, object> source, string key, bool require, Dictionary<string, object>[] def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required") : def;
            var array = raw as JArray ?? throw new ArgumentException($"{key} must be an array");
            var result = new Dictionary<string, object>[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                var token = array[i];
                if (!(token is JObject obj))
                    throw new ArgumentException($"{key} must contain only objects");
                var dict = obj.ToObject<Dictionary<string, object>>() ?? throw new ArgumentException($"{key} contains an invalid object");
                result[i] = dict;
            }
            return result;
        }

        public static string[] GetStringArray(Dictionary<string, object> source, string key, string[] def) => GetStringArray(source, key, false, def);
        public static string[] RequireStringArray(Dictionary<string, object> source, string key) => GetStringArray(source, key, true, null);
        private static string[] GetStringArray(Dictionary<string, object> source, string key, bool require, string[] def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required") : def;
            var array = raw as JArray ?? throw new ArgumentException($"{key} must be an array");
            var result = new string[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                var value = UnwrapJsonValue(array[i]);
                if (value is string str)
                    result[i] = str;
                else
                    throw new ArgumentException($"{key} must contain only strings");
            }
            return result;
        }

        public static int[] GetIntArray(Dictionary<string, object> source, string key, int[] def) => GetIntArray(source, key, false, def);
        public static int[] RequireIntArray(Dictionary<string, object> source, string key) => GetIntArray(source, key, true, null);
        private static int[] GetIntArray(Dictionary<string, object> source, string key, bool require, int[] def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new ArgumentException($"{key} is required") : def;
            var array = raw as JArray ?? throw new ArgumentException($"{key} must be an array");
            var result = new int[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                var value = UnwrapJsonValue(array[i]);
                switch (value)
                {
                    case int intValue:
                        result[i] = intValue;
                        break;
                    case long longValue:
                        result[i] = checked((int)longValue);
                        break;
                    case double doubleValue:
                        result[i] = (int)doubleValue;
                        break;
                    case float floatValue:
                        result[i] = (int)floatValue;
                        break;
                    case decimal decimalValue:
                        result[i] = (int)decimalValue;
                        break;
                    case string stringValue:
                        if (int.TryParse(stringValue, out var parsed))
                        {
                            result[i] = parsed;
                            break;
                        }
                        throw new ArgumentException($"{key} must contain only integers");
                    default:
                        throw new ArgumentException($"{key} must contain only integers");
                }
            }
            return result;
        }
    }
}
