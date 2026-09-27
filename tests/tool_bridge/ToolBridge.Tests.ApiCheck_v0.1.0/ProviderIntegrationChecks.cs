using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Topomatic.ApplicationPlatform;
using Topomatic.Cad.View;
using Topomatic.ToolBridge;
using Topomatic.ToolBridge.Services;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed class ProviderIntegrationChecks
    {
        private readonly Func<CadView> m_GetCadView;

        internal ProviderIntegrationChecks(Func<CadView> getCadView)
        {
            m_GetCadView = getCadView;
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckDiscovery()
        {
            LegacyToolProvider.Discover();
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckGenerationWithoutDomain()
        {
            var provider = new LegacyToolProvider();
            var tools = IntegrationHost.Generate(provider);
            Test.Require(tools.Length == 2, "Должны быть созданы оба инструмента API 0.1.0 без Domain.");
            Test.Require(provider.InvocationCount == 0, "Генерация вызвала обработчик инструмента.");
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckConfigurationsWithoutDomain()
        {
            var tools = IntegrationHost.Generate(new LegacyToolProvider());
            RequireLegacyNames(IntegrationHost.CreateConfigurationNames(tools, false), 2);
            RequireLegacyNames(IntegrationHost.CreateConfigurationNames(tools, true), 3);
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckAvailability()
        {
            var provider = LegacyToolProvider.Discover();
            var manager = IntegrationHost.CreateManager(IntegrationHost.Generate(provider),
                new ObjectStorage(), ToolBridgeLogger.Instance, () => null);
            RequireLegacyNames(IntegrationHost.GetToolNames(manager), 2);
            Test.Require(provider.InvocationCount == 0, "Получение списка вызвало обработчик инструмента.");
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckInvocation()
        {
            var view = RequireCadView();
            var provider = LegacyToolProvider.Discover();
            var calls = new List<string>();
            provider.OnInvoke = (name, instance) => calls.Add(name);
            var manager = IntegrationHost.CreateManager(IntegrationHost.Generate(provider),
                new ObjectStorage(), ToolBridgeLogger.Instance, () => view);

            IntegrationHost.Call(manager, LegacyToolProvider.FirstName);
            IntegrationHost.Call(manager, LegacyToolProvider.SecondName);
            Test.Require(calls.SequenceEqual(new[] { LegacyToolProvider.FirstName, LegacyToolProvider.SecondName }),
                "Вызовы должны передать управление соответствующим обработчикам старого провайдера ровно по одному разу.");
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckContext()
        {
            var view = RequireCadView();
            var host = ApplicationHost.Current;
            var storage = new ObjectStorage();
            var logger = ToolBridgeLogger.Instance;
            var provider = LegacyToolProvider.Discover();
            provider.OnInvoke = (name, instance) =>
            {
                Test.Require(ReferenceEquals(instance.AppHost, host), "Обработчик не получил текущий AppHost.");
                Test.Require(ReferenceEquals(instance.CadView, view), "Обработчик не получил текущий CadView.");
                Test.Require(ReferenceEquals(instance.SessionStorage, storage), "Обработчик не получил SessionStorage менеджера.");
                Test.Require(ReferenceEquals(instance.Logger, logger), "Обработчик не получил Logger менеджера.");
            };
            var manager = IntegrationHost.CreateManager(IntegrationHost.Generate(provider), storage, logger, () => view);
            foreach (var name in new[] { LegacyToolProvider.FirstName, LegacyToolProvider.SecondName })
            {
                // Each dispatch must supply context, including after the provider clears it.
                provider.AppHost = null;
                provider.CadView = null;
                provider.SessionStorage = null;
                provider.Logger = null;
                IntegrationHost.Call(manager, name);
            }
            Test.Require(provider.InvocationCount == 2, "Проверка контекста должна выполниться в обоих обработчиках.");
        }

        private CadView RequireCadView()
        {
            var view = m_GetCadView();
            Test.Require(view != null, "Для проверки вызова старого провайдера откройте видовой экран Robur.");
            return view;
        }

        private static void RequireLegacyNames(string[] names, int expectedCount)
        {
            Test.Require(names.Length == expectedCount &&
                names.Count(name => name == LegacyToolProvider.FirstName) == 1 &&
                names.Count(name => name == LegacyToolProvider.SecondName) == 1,
                "Оба инструмента API 0.1.0 должны быть доступны по объявленным именам без повторов.");
        }
    }
}
