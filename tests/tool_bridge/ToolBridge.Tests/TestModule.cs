using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Topomatic.ApplicationPlatform;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests
{
    internal sealed class TestModule : PluginInitializator
    {
        private static int m_Running;

        [cmd("run_tests")]
        private void RunTests()
        {
            if (Interlocked.Exchange(ref m_Running, 1) != 0)
                return;
            try
            {
                using (var console = new TestConsole())
                {
                    try
                    {
                        RunProviders(console);
                    }
                    catch (Exception ex)
                    {
                        console.Write(ConsoleColor.Red, "✗ Ошибка запуска тестов: " + ex);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref m_Running, 0);
            }
        }

        private static void RunProviders(TestConsole console)
        {
            var providers = new List<object>();
            ApplicationHost.Current.Plugins.Broadcast("test_request", new string[0], new object[] { providers });
            if (providers.Count == 0)
                console.Write(ConsoleColor.Yellow, "! Тестовые провайдеры не найдены.");
            var registeredTypes = new HashSet<Type>();
            foreach (var provider in providers.OrderBy(p => p?.GetType().FullName, StringComparer.Ordinal))
            {
                try
                {
                    if (provider == null)
                        throw new InvalidOperationException("Зарегистрирован пустой тестовый провайдер.");
                    var type = provider.GetType();
                    if (!registeredTypes.Add(type))
                        throw new InvalidOperationException("Повторная регистрация тестового провайдера: " + type.FullName);
                    console.Write(ConsoleColor.Gray, "=== " + type.FullName + " ===");
                    console.WriteResults(TestRunner.RunProvider(provider));
                }
                catch (Exception ex)
                {
                    console.Write(ConsoleColor.Red, "✗ Ошибка тестового провайдера: " + ex);
                }
            }
        }
    }
}
