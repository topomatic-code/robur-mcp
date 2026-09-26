using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Topomatic.ApplicationPlatform;
using Topomatic.Cad.View;
using Topomatic.ToolBridge;

namespace ToolBridge_v0_1_0_ApiChecker
{
    internal static partial class ApiChecks
    {
        // Reflection here is a host adapter to internal 0.2 infrastructure, not a
        // replacement for the old binary API calls made by LegacyProviderProbe.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckLegacyProviderNoDomain(CadView cadView)
        {
            var stage = "подготовка окружения";
            IList tools = null;
            IList configs = null;
            object[] savedTools = null;
            object[] savedConfigs = null;
            IDisposable server = null;
            try
            {
                var assembly = typeof(ToolProvider).Assembly;
                var bootstrapType = assembly.GetType("Topomatic.ToolBridge.ToolBridgeBootstrap", true);
                var bootstrap = bootstrapType.GetProperty("Instance").GetValue(null);
                Require(!(bool)Property(bootstrap, "ServerRunning"),
                    "Перед интеграционной проверкой остановите Tool Bridge/MCP-сервер. Общие коллекции временно изменяются.");

                var collectorType = assembly.GetType("Topomatic.ToolBridge.ToolCollector", true);
                var settingsType = assembly.GetType("Topomatic.ToolBridge.Settings.ToolSettings", true);
                // Initialize production statics BEFORE adding malformed legacy metadata.
                // A failure of Load later cannot poison the ToolSettings type initializer.
                collectorType.GetProperty("Tools").GetValue(null);
                settingsType.GetProperty("ToolConfigs").GetValue(null);
                tools = GetIntegrationList(collectorType, "m_Tools");
                configs = GetIntegrationList(settingsType, "m_ToolConfigs");
                savedTools = tools.Cast<object>().ToArray();
                savedConfigs = configs.Cast<object>().ToArray();

                stage = "обнаружение старого провайдера через tool_request";
                var providers = new List<ToolProvider>();
                LegacyProviderProbe.RegistrationEnabled = true;
                try
                {
                    ApplicationHost.Current.Plugins.Broadcast("tool_request", new string[0], new object[] { providers });
                }
                finally
                {
                    LegacyProviderProbe.RegistrationEnabled = false;
                }
                var legacy = providers.OfType<LegacyProviderProbe.Provider>().ToArray();
                Require(legacy.Length == 1, "tool_request должен обнаружить ровно один старый провайдер; проверьте обновление .plugin");

                stage = "регистрация методов без Domain";
                var getTools = typeof(ToolProvider).GetMethod("GetTools", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(getTools != null, "Host adapter: ToolProvider.GetTools not found");
                var definitions = ((IEnumerable)getTools.Invoke(legacy[0], null)).Cast<object>().ToArray();
                Require(definitions.Length == 2, "Должны быть зарегистрированы оба старых метода");
                tools.Clear();
                foreach (var tool in definitions)
                {
                    tools.Add(tool);
                }

                stage = "загрузка и сортировка настроек инструментов без Domain";
                settingsType.GetMethod("Load").Invoke(null, null);

                stage = "получение списка инструментов через list_tools";
                var managerType = assembly.GetType("Topomatic.ToolBridge.ToolManager", true);
                var storage = assembly.GetType("Topomatic.ToolBridge.Services.ObjectStorage", true)
                    .GetProperty("Instance").GetValue(null);
                var logger = ToolBridgeLogger.Instance;
                var manager = Activator.CreateInstance(managerType, new object[] { storage, logger, (Func<CadView>)(() => cadView) });
                managerType.GetMethod("Initialize").Invoke(manager, null);
                var serverType = assembly.GetType("Topomatic.ToolBridge.ToolBridgePipeServer", true);
                // Never start a pipe or a background thread; use the real request dispatcher on the UI thread.
                server = (IDisposable)Activator.CreateInstance(serverType, new object[] { "api_0_1_integration_unused", manager, logger });
                var listed = SendIntegrationRequest(assembly, server, "list_tools", new Dictionary<string, object>());
                var listedTools = ((JArray)listed["tools"]).OfType<JObject>().ToArray();
                Require(listedTools.Length == 2, "list_tools должен вернуть оба старых инструмента");
                foreach (var name in new[] { "api_0_1_no_domain_echo", "api_0_1_no_domain_second" })
                {
                    var tool = listedTools.SingleOrDefault(t => (string)t["name"] == name);
                    Require(tool != null && tool["inputSchema"] is JObject && (bool)tool["annotations"]["readOnlyHint"],
                        "Список должен сохранять имя, схему и readOnlyHint старого инструмента: " + name);
                }

                stage = "вызов старого обработчика через call_tool";
                Require(cadView != null, "Откройте видовой экран Robur для вызова инструмента");
                var value = Guid.NewGuid().ToString("N");
                var result = SendIntegrationRequest(assembly, server, "call_tool", new Dictionary<string, object>
                {
                    { "tool_name", "api_0_1_no_domain_echo" },
                    { "arguments", new Dictionary<string, object> { { "value", value } } }
                });
                Require((string)result["value"] == value, "Должен выполниться старый обработчик с переданным аргументом");
            }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException && ex.InnerException != null)
                    ex = ex.InnerException;
                throw new InvalidOperationException("Интеграция старого провайдера, этап: " + stage, ex);
            }
            finally
            {
                LegacyProviderProbe.RegistrationEnabled = false;
                // Restore original object instances, order and unsaved settings even after a failed sort.
                if (savedTools != null) RestoreIntegrationList(tools, savedTools);
                if (savedConfigs != null) RestoreIntegrationList(configs, savedConfigs);
                server?.Dispose();
            }
        }

        private static IList GetIntegrationList(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Require(field != null, "Host adapter: missing " + type.FullName + "." + fieldName);
            return (IList)field.GetValue(null);
        }

        private static void RestoreIntegrationList(IList list, object[] saved)
        {
            list.Clear();
            foreach (var item in saved) list.Add(item);
        }

        private static JToken SendIntegrationRequest(Assembly assembly, object server, string method, Dictionary<string, object> parameters)
        {
            var requestType = assembly.GetType("Topomatic.ToolBridge.BridgeRequest", true);
            var id = Guid.NewGuid().ToString("N");
            var request = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new { id, method, @params = parameters }), requestType);
            var process = server.GetType().GetMethod("ProcessRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(process != null, "Host adapter: ProcessRequest not found");
            var response = JObject.Parse(JsonConvert.SerializeObject(process.Invoke(server, new[] { request })));
            Require((string)response["id"] == id && (bool)response["ok"], method + ": " + response);
            return response["result"];
        }
    }
}
