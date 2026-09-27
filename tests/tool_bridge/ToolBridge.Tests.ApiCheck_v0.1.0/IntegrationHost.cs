using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Topomatic.Cad.View;
using Topomatic.ToolBridge;
using Topomatic.ToolBridge.Services;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    // Adapter to current host internals. These names are not part of the API 0.1.0 baseline.
    internal static class IntegrationHost
    {
        private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        internal static Array Generate(ToolProvider provider)
        {
            var tools = Invoke(typeof(ToolProvider), provider, "GetTools", Type.EmptyTypes);
            return ToArray(TypeOf("Tool"), tools);
        }

        internal static object CreateManager(Array tools, ObjectStorage storage, ToolBridgeLogger logger, Func<CadView> view)
        {
            var type = TypeOf("ToolManager");
            var manager = Activator.CreateInstance(type, new object[] { storage, logger, view });
            Invoke(type, manager, "Initialize",
                new[] { typeof(IEnumerable<>).MakeGenericType(TypeOf("Tool")) }, tools);
            return manager;
        }

        internal static string[] GetToolNames(object manager)
        {
            return Names(Invoke(manager.GetType(), manager, "GetTools", Type.EmptyTypes));
        }

        internal static void Call(object manager, string name)
        {
            Invoke(manager.GetType(), manager, "CallTool", new[] { typeof(Dictionary<string, object>) },
                new Dictionary<string, object>
                {
                    { "tool_name", name },
                    { "arguments", new Dictionary<string, object>() }
                });
        }

        internal static string[] CreateConfigurationNames(Array tools, bool includeDomain)
        {
            var configType = TypeOf("Settings.ToolConfig");
            var saved = Array.CreateInstance(configType, includeDomain ? 1 : 0);
            if (includeDomain)
            {
                var config = Activator.CreateInstance(configType, new object[]
                {
                    "current_domain_tool", "Current", "API compatibility probe", "{\"type\":\"object\"}",
                    true, false, true
                });
                saved.SetValue(config, 0);
            }
            var configs = Invoke(TypeOf("Settings.ToolConfigFactory"), null, "Create", new[]
            {
                typeof(IEnumerable<>).MakeGenericType(TypeOf("Tool")),
                typeof(IEnumerable<>).MakeGenericType(configType)
            }, tools, saved);
            return Names(configs);
        }

        private static Type TypeOf(string name)
        {
            var type = typeof(ToolProvider).Assembly.GetType("Topomatic.ToolBridge." + name);
            Test.Require(type != null, "Адаптер интеграционных тестов: не найден тип " + name);
            return type;
        }

        private static object Invoke(Type type, object instance, string name, Type[] parameters, params object[] arguments)
        {
            var method = type.GetMethod(name, Methods, null, parameters, null);
            Test.Require(method != null, "Адаптер интеграционных тестов: не найден метод " + type.FullName + "." + name);
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static Array ToArray(Type elementType, object collection)
        {
            var items = Items(collection).ToArray();
            var result = Array.CreateInstance(elementType, items.Length);
            for (var i = 0; i < items.Length; i++)
                result.SetValue(items[i], i);
            return result;
        }

        private static string[] Names(object collection)
        {
            return Items(collection).Select(item =>
            {
                var property = item.GetType().GetProperty("Name");
                Test.Require(property != null, "Адаптер интеграционных тестов: отсутствует свойство Name.");
                return (string)property.GetValue(item);
            }).ToArray();
        }

        private static IEnumerable<object> Items(object collection)
        {
            var items = collection as IEnumerable;
            Test.Require(items != null, "Адаптер интеграционных тестов: ожидалась коллекция.");
            foreach (var item in items)
            {
                Test.Require(item != null, "Коллекция содержит пустой элемент.");
                yield return item;
            }
        }
    }
}
