using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Topomatic.ApplicationPlatform;
using Topomatic.Cad.Foundation.Brep;
using Topomatic.ToolBridge;
using Topomatic.ToolBridge.Services;
using Topomatic.Visualization;

namespace ToolBridge_v0_1_0_ApiChecker
{
    internal static partial class ApiChecks
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckToolDefAttribute()
        {
            var attribute = new ToolDefAttribute
            {
                Name = "compatibility_probe",
                Description = "API 0.1.0 probe",
                InputSchema = "{\"type\":\"object\"}",
                ReadOnlyHint = true,
                DestructiveHint = false,
                IdempotentHint = true
            };
            Require(attribute.Name == "compatibility_probe" && attribute.Description == "API 0.1.0 probe" &&
                attribute.InputSchema == "{\"type\":\"object\"}" && attribute.ReadOnlyHint &&
                !attribute.DestructiveHint && attribute.IdempotentHint, "ToolDefAttribute properties");
            attribute.ReadOnlyHint = false;
            attribute.DestructiveHint = true;
            attribute.IdempotentHint = false;
            Require(!attribute.ReadOnlyHint && attribute.DestructiveHint && !attribute.IdempotentHint,
                "ToolDefAttribute boolean setters");

            // This fixture does not inherit ToolProvider: attribute compatibility
            // must not depend on loading or constructing a provider.
            var method = typeof(AttributeTarget).GetMethod(nameof(AttributeTarget.Echo));
            var definition = method.GetCustomAttribute<ToolDefAttribute>();
            Require(definition != null && definition.Name == "api_0_1_probe" && definition.ReadOnlyHint &&
                definition.InputSchema == "{\"type\":\"object\"}" && definition.IdempotentHint && !definition.DestructiveHint,
                "ToolDefAttribute on a method without Domain");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckToolProvider()
        {
            var provider = new ProbeProvider();
            // Exercise the old accessors without calling other ToolBridge mechanisms.
            provider.AppHost = null;
            provider.CadView = null;
            provider.SessionStorage = null;
            provider.Logger = null;
            Require(provider.AppHost == null && provider.CadView == null &&
                provider.SessionStorage == null && provider.Logger == null, "ToolProvider context accessors");
            var method = typeof(ProbeProvider).GetMethod(nameof(ProbeProvider.Echo));
            var arguments = new Dictionary<string, object> { { "value", 17 } };
            var handler = (Func<Dictionary<string, object>, object>)method.CreateDelegate(
                typeof(Func<Dictionary<string, object>, object>), provider);
            Require(ReferenceEquals(handler(arguments), arguments), "Provider method signature and invocation");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckToolProviderContextIntegration()
        {
            var provider = new ProbeProvider();
            var storage = new ObjectStorage();
            var logger = ToolBridgeLogger.Instance;
            provider.AppHost = ApplicationHost.Current;
            provider.CadView = null;
            provider.SessionStorage = storage;
            provider.Logger = logger;
            Require(ReferenceEquals(provider.AppHost, ApplicationHost.Current) && provider.CadView == null &&
                ReferenceEquals(provider.SessionStorage, storage) && ReferenceEquals(provider.Logger, logger),
                "ToolProvider context properties");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckObjectStorage()
        {
            var storage = new ObjectStorage();
            var first = new object();
            var second = new object();
            var firstId = storage.AddObject(first);
            var secondId = Guid.NewGuid();
            storage.AddObject(secondId, second);
            Require(firstId != Guid.Empty && storage.GetGuid(first) == firstId && storage.GetGuid(second) == secondId,
                "ObjectStorage GUID allocation and lookup");
            Require(ReferenceEquals(storage.GetObject(firstId), first) && ReferenceEquals(storage.GetObject(secondId), second),
                "ObjectStorage object lookup");
            Require(storage.HasObject(firstId) && storage.HasObject(first) && !storage.HasObject(Guid.NewGuid()) &&
                !storage.HasObject(new object()) && !storage.HasObject((object)null), "ObjectStorage membership");
            Expect<InvalidOperationException>(() => storage.AddObject(first), "duplicate object");
            Expect<InvalidOperationException>(() => storage.AddObject(firstId, new object()), "duplicate GUID");
            Expect<InvalidOperationException>(() => storage.AddObject(Guid.NewGuid(), second), "duplicate object with explicit GUID");
            Expect<ArgumentNullException>(() => storage.AddObject(null), "null object");
            Expect<ArgumentNullException>(() => storage.AddObject(Guid.NewGuid(), null), "null object with GUID");
            Expect<ArgumentNullException>(() => storage.GetGuid(null), "null GUID lookup");
            Expect<InvalidOperationException>(() => storage.GetGuid(new object()), "unknown object lookup");
            Require(storage.RemoveObject(firstId) && !storage.RemoveObject(firstId) && !storage.HasObject(first), "remove by GUID");
            Require(storage.RemoveObject(second) && !storage.RemoveObject(second) && !storage.HasObject(secondId), "remove by object");
            Require(!storage.RemoveObject((object)null), "remove null");
            Expect<InvalidOperationException>(() => storage.GetObject(firstId), "lookup after removal");
            storage.AddObject(firstId, first);
            storage.Clear();
            Require(!storage.HasObject(firstId) && !storage.HasObject(first), "Clear removes both indexes");
            storage.AddObject(firstId, first);
            Require(ReferenceEquals(storage.GetObject(firstId), first), "reuse after Clear");
            storage.Clear();
            GC.KeepAlive(first);
            GC.KeepAlive(second);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckLogger()
        {
            var logger = ToolBridgeLogger.Instance;
            Require(logger != null && ReferenceEquals(logger, ToolBridgeLogger.Instance), "Logger singleton");
            var message = "ToolBridge 0.1.0 API compatibility probe";
            var formatted = logger.CreateLogString(message);
            Require(formatted != null && formatted.Contains(message) && formatted.Contains("Robur tool bridge"), "Log formatting");
            logger.Log(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckSmdx()
        {
            var properties = new ImProperties
            {
                new ImProperty("code", "Group|Subgroup|Code", "ABC"),
                new ImProperty("plain", "Plain", "value")
            };
            var values = (object[])SmdxUtils.CreatePropsArray(properties);
            Require(values.Length == 2, "SMDX property count");
            Require((string)Property(values[0], "name") == "Code" && (string)Property(values[0], "group") == "Group|Subgroup" &&
                (string)Property(values[0], "tag") == "code" && (string)Property(values[0], "value") == "ABC" &&
                (string)Property(values[0], "units") == "", "SMDX grouped property");
            Require((string)Property(values[1], "group") == "", "SMDX ungrouped property");
            Require(((object[])SmdxUtils.CreatePropsArray(new ImProperties())).Length == 0, "empty SMDX properties");
            var element = CreateSolid("SMDX probe", properties);
            var serialized = SmdxUtils.CreateImElementObj(element);
            Require((string)Property(serialized, "name") == element.Name, "SMDX element name");
            Require(Property(serialized, "properties") is object[], "SMDX element properties");
            var bounds = Property(serialized, "bounds");
            foreach (var name in new[] { "left", "right", "top", "bottom", "near", "far" })
                Require(Property(bounds, name) is double, "SMDX bounds: " + name);
        }

        private static StaticSolidElement CreateSolid(string name, ImProperties properties) =>
            new StaticSolidElement(name, "SmdxElement", properties, new Shell(), new ImDocuments());

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Проверка не пройдена: " + message);
        }

        private static void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Ожидалось " + typeof(T).Name + ": " + message);
        }

        private static object Property(object instance, string name)
        {
            Require(instance != null, "null result for " + name);
            var property = instance.GetType().GetProperty(name);
            Require(property != null, "missing result property " + name);
            return property.GetValue(instance);
        }

        private static void Number(object instance, string name, double expected) =>
            Require(Math.Abs(Convert.ToDouble(Property(instance, name)) - expected) < 1e-8, name);

        // Keep the derived old-API type out of Module so a type-load error stays inside its group.
        private sealed class ProbeProvider : ToolProvider
        {
            public object Echo(Dictionary<string, object> arguments) => arguments;
        }

        private sealed class AttributeTarget
        {
            [ToolDef(Name = "api_0_1_probe", Description = "Read-only compatibility probe",
                InputSchema = "{\"type\":\"object\"}", ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true)]
            public object Echo(Dictionary<string, object> arguments) => arguments;
        }
    }
}
