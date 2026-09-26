using System;
using System.Linq;
using System.Threading;
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
                        var providers = typeof(TestModule).Assembly.GetTypes()
                            .Where(t => t.IsClass && t.IsDefined(typeof(TestProviderAttribute), false))
                            .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
                        if (providers.Length == 0)
                            console.Write(ConsoleColor.Yellow, "! Классы с атрибутом TestProvider не найдены.");
                        foreach (var provider in providers)
                        {
                            console.Write(ConsoleColor.Gray, "=== " + provider.FullName + " ===");
                            console.WriteResults(TestRunner.RunProvider(provider));
                        }
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
    }
}

