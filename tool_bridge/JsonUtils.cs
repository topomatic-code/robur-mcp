using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Topomatic.Cad.Foundation;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public static class JsonUtils
    {
        public static object UnwrapJsonValue(object value) => value is JValue token ? token.Value : value;

        private static bool TryConvertInt(object value, out int result)
        {
            switch (value)
            {
                case int intValue:
                    result = intValue;
                    return true;
                case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                    result = (int)longValue;
                    return true;
                case double doubleValue
                    when !double.IsNaN(doubleValue)
                    && !double.IsInfinity(doubleValue)
                    && doubleValue >= int.MinValue
                    && doubleValue <= int.MaxValue
                    && Math.Truncate(doubleValue) == doubleValue:
                    result = (int)doubleValue;
                    return true;
                case float floatValue
                    when !float.IsNaN(floatValue)
                    && !float.IsInfinity(floatValue)
                    && floatValue >= int.MinValue
                    && floatValue <= int.MaxValue
                    && Math.Truncate(floatValue) == floatValue:
                    result = (int)floatValue;
                    return true;
                case decimal decimalValue
                    when decimalValue >= int.MinValue
                    && decimalValue <= int.MaxValue
                    && decimal.Truncate(decimalValue) == decimalValue:
                    result = (int)decimalValue;
                    return true;
                case string stringValue:
                    return int.TryParse(
                        stringValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out result);
                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryConvertDouble(object value, out double result)
        {
            switch (value)
            {
                case double doubleValue:
                    result = doubleValue;
                    break;
                case float floatValue:
                    result = floatValue;
                    break;
                case long longValue:
                    result = longValue;
                    break;
                case int intValue:
                    result = intValue;
                    break;
                case decimal decimalValue:
                    result = (double)decimalValue;
                    break;
                case string stringValue:
                    if (!double.TryParse(
                        stringValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out result))
                    {
                        return false;
                    }
                    break;
                default:
                    result = default;
                    return false;
            }
            return !double.IsNaN(result) && !double.IsInfinity(result);
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key, Dictionary<string, object> def) => GetObject(source, key, false, def);
        public static Dictionary<string, object> RequireObject(Dictionary<string, object> source, string key) => GetObject(source, key, true, null);
        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key, bool require, Dictionary<string, object> def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new BadRequestException($"{key} is required") : def;
            if (raw is JObject obj)
                return obj.ToObject<Dictionary<string, object>>() ?? throw new BadRequestException($"{key} must be an object");
            return raw as Dictionary<string, object> ?? throw new BadRequestException($"{key} must be an object");
        }

        public static string GetString(Dictionary<string, object> source, string key, string def) => GetString(source, key, false, def);
        public static string RequireString(Dictionary<string, object> source, string key) => GetString(source, key, true, null);
        private static string GetString(Dictionary<string, object> source, string key, bool require, string def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new BadRequestException($"{key} is required") : def;
            var value = UnwrapJsonValue(raw);
            if (value is string str)
                return str;
            throw new BadRequestException($"{key} must be a string");
        }

        public static int? GetInt(Dictionary<string, object> source, string key, int? def) => GetInt(source, key, false, def);
        public static int RequireInt(Dictionary<string, object> source, string key) => GetInt(source, key, true, null).Value;
        private static int? GetInt(Dictionary<string, object> source, string key, bool require, int? def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new BadRequestException($"{key} is required") : def;
            var value = UnwrapJsonValue(raw);
            if (TryConvertInt(value, out var result))
                return result;
            throw new BadRequestException($"{key} must be an integer");
        }

        public static double? GetDouble(Dictionary<string, object> source, string key, double? def) => GetDouble(source, key, false, def);
        public static double RequireDouble(Dictionary<string, object> source, string key) => GetDouble(source, key, true, null).Value;
        private static double? GetDouble(Dictionary<string, object> source, string key, bool require, double? def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new BadRequestException($"{key} is required") : def;
            var value = UnwrapJsonValue(raw);
            if (TryConvertDouble(value, out var result))
                return result;
            throw new BadRequestException($"{key} must be a number");
        }

        public static Vector3D? GetVector3D(Dictionary<string, object> source, string key, Vector3D? def) => GetVector3D(source, key, false, def);
        public static Vector3D RequireVector3D(Dictionary<string, object> source, string key) => GetVector3D(source, key, true, null).Value;
        private static Vector3D? GetVector3D(Dictionary<string, object> source, string key, bool require, Vector3D? def)
        {
            var vector = GetObject(source, key, null);
            if (vector == null)
                return require ? throw new BadRequestException($"{key} is required") : def;
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
                return require ? throw new BadRequestException($"{key} is required") : def;
            var value = UnwrapJsonValue(raw);
            if (value is bool b)
                return b;
            if (value is string s && bool.TryParse(s, out var parsed))
                return parsed;
            throw new BadRequestException($"{key} must be a boolean");
        }

        public static Dictionary<string, object>[] GetArray(Dictionary<string, object> source, string key, Dictionary<string, object>[] def) => GetArray(source, key, false, def);
        public static Dictionary<string, object>[] RequireArray(Dictionary<string, object> source, string key) => GetArray(source, key, true, null);
        private static Dictionary<string, object>[] GetArray(Dictionary<string, object> source, string key, bool require, Dictionary<string, object>[] def)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.TryGetValue(key, out var raw) || raw == null)
                return require ? throw new BadRequestException($"{key} is required") : def;
            var array = raw as JArray ?? throw new BadRequestException($"{key} must be an array");
            var result = new Dictionary<string, object>[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                var token = array[i];
                if (!(token is JObject obj))
                    throw new BadRequestException($"{key} must contain only objects");
                var dict = obj.ToObject<Dictionary<string, object>>() ?? throw new BadRequestException($"{key} contains an invalid object");
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
                return require ? throw new BadRequestException($"{key} is required") : def;
            var array = raw as JArray ?? throw new BadRequestException($"{key} must be an array");
            var result = new string[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                var value = UnwrapJsonValue(array[i]);
                if (value is string str)
                    result[i] = str;
                else
                    throw new BadRequestException($"{key} must contain only strings");
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
                return require ? throw new BadRequestException($"{key} is required") : def;
            var array = raw as JArray ?? throw new BadRequestException($"{key} must be an array");
            var result = new int[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                var value = UnwrapJsonValue(array[i]);
                if (!TryConvertInt(value, out var intValue))
                    throw new BadRequestException($"{key} must contain only integers");
                result[i] = intValue;
            }
            return result;
        }
    }
}
