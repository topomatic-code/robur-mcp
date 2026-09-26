using System;
using System.Collections.Generic;
using Topomatic.ApplicationPlatform;

namespace ToolBridge.Tests.TestProviders
{
    [TestProvider]
    internal sealed class ApiCheck_v0_1_0
    {
        private static readonly string[] m_ExpectedGroups =
        {
            "ToolProvider", "ToolDefAttribute", "ObjectStorage", "ToolBridgeLogger", "JsonUtils", "DwgUtils", "SmdxUtils",
            "Integration.ToolProviderContext", "Integration.DwgStorage", "Integration.LegacyProviderNoDomain"
        };

        private bool m_ResultsLoaded;
        private string[] m_Results;
        private string m_LoadError;

        [Test]
        private void ToolProvider() => CheckGroup("ToolProvider");

        [Test]
        private void ToolDefAttribute() => CheckGroup("ToolDefAttribute");

        [Test]
        private void Integration_ToolProviderContext() => CheckGroup("Integration.ToolProviderContext");

        [Test]
        private void Integration_DwgStorage() => CheckGroup("Integration.DwgStorage");

        [Test]
        private void Integration_LegacyProviderNoDomain() => CheckGroup("Integration.LegacyProviderNoDomain");

        [Test]
        private void ObjectStorage() => CheckGroup("ObjectStorage");

        [Test]
        private void ToolBridgeLogger() => CheckGroup("ToolBridgeLogger");

        [Test]
        private void JsonUtils() => CheckGroup("JsonUtils");

        [Test]
        private void DwgUtils() => CheckGroup("DwgUtils");

        [Test]
        private void SmdxUtils() => CheckGroup("SmdxUtils");

        private void CheckGroup(string group)
        {
            // TestRunner creates one provider per block: execute the API checks once,
            // then report each group's result in its own test.
            if (!m_ResultsLoaded)
            {
                m_ResultsLoaded = true;
                try
                {
                    m_Results = ApplicationHost.Current.Plugins.Execute("tool_bridge_v0.1.0_api_check") as string[];
                }
                catch (Exception ex)
                {
                    m_LoadError = "Не удалось выполнить проверку API 0.1.0: " + ex;
                }
            }

            if (m_LoadError != null)
                Test.Fail(m_LoadError);
            else
                CheckResult(m_Results, group);
        }

        internal static void CheckResult(string[] results, string expectedGroup)
        {
            if (results == null || results.Length == 0)
            {
                Test.Fail("Плагин проверки API 0.1.0 не вернул результаты.");
                return;
            }

            var expected = new HashSet<string>(m_ExpectedGroups, StringComparer.Ordinal);
            var received = false;
            foreach (var message in results)
            {
                var success = message != null && message.StartsWith("success: ", StringComparison.Ordinal);
                var failure = message != null && message.StartsWith("fail: ", StringComparison.Ordinal);
                if (!success && !failure)
                {
                    Test.Fail("Некорректный статус проверки API 0.1.0: " + (message ?? "<null>"));
                    continue;
                }
                var body = message.Substring(success ? "success: ".Length : "fail: ".Length);
                var separator = body.IndexOf("] ", StringComparison.Ordinal);
                if (!body.StartsWith("[", StringComparison.Ordinal) || separator <= 1 ||
                    string.IsNullOrWhiteSpace(body.Substring(separator + 2)))
                {
                    Test.Fail("Некорректный формат результата API 0.1.0: " + message);
                    continue;
                }
                var group = body.Substring(1, separator - 1);
                if (!expected.Contains(group))
                {
                    Test.Fail("Неизвестная группа проверки API 0.1.0: " + group);
                    continue;
                }
                if (group != expectedGroup)
                    continue;
                if (received)
                {
                    Test.Fail("Повторная группа проверки API 0.1.0: " + group);
                    continue;
                }
                received = true;
                if (success)
                    Test.Success(body);
                else
                    Test.Fail(body);
            }
            if (!received)
                Test.Fail("Нет результата проверки API 0.1.0: " + expectedGroup);
        }
    }
}
