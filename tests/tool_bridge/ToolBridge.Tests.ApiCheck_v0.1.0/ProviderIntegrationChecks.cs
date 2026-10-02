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
            ProbeToolProvider.Discover();
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckGenerationWithoutDomain()
        {
            var provider = new ProbeToolProvider();
            var tools = IntegrationHost.Generate(provider);
            Test.Require(tools.Length == 2, "Должны быть созданы оба инструмента API 0.1.0 без Domain.");
            Test.Require(provider.InvocationCount == 0, "Генерация вызвала обработчик инструмента.");
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckConfigurationsWithoutDomain()
        {
            var tools = IntegrationHost.Generate(new ProbeToolProvider());
            RequireToolNames(IntegrationHost.CreateConfigurationNames(tools, false));
            RequireToolNames(IntegrationHost.CreateConfigurationNames(tools, true), IntegrationHost.DomainToolName);
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckAvailability()
        {
            var provider = ProbeToolProvider.Discover();
            var manager = IntegrationHost.CreateManager(IntegrationHost.Generate(provider),
                new ObjectStorage(), ToolBridgeLogger.Instance, () => null);
            RequireToolNames(IntegrationHost.GetToolNames(manager));
            Test.Require(provider.InvocationCount == 0, "Получение списка вызвало обработчик инструмента.");
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckInvocation()
        {
            var view = RequireCadView();
            var provider = ProbeToolProvider.Discover();
            var calls = new List<string>();
            provider.OnInvoke = (name, instance) => calls.Add(name);
            var manager = IntegrationHost.CreateManager(IntegrationHost.Generate(provider),
                new ObjectStorage(), ToolBridgeLogger.Instance, () => view);

            IntegrationHost.Call(manager, ProbeToolProvider.FirstName);
            IntegrationHost.Call(manager, ProbeToolProvider.SecondName);
            Test.Require(calls.SequenceEqual(new[] { ProbeToolProvider.FirstName, ProbeToolProvider.SecondName }),
                "Вызовы должны передать управление соответствующим обработчикам тестового провайдера ровно по одному разу.");
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void CheckContext()
        {
            var view = RequireCadView();
            var host = ApplicationHost.Current;
            var storage = new ObjectStorage();
            var logger = ToolBridgeLogger.Instance;
            var provider = ProbeToolProvider.Discover();
            provider.OnInvoke = (name, instance) =>
            {
                Test.Require(ReferenceEquals(instance.AppHost, host), "Обработчик не получил текущий AppHost.");
                Test.Require(ReferenceEquals(instance.CadView, view), "Обработчик не получил текущий CadView.");
                Test.Require(ReferenceEquals(instance.SessionStorage, storage), "Обработчик не получил SessionStorage менеджера.");
                Test.Require(ReferenceEquals(instance.Logger, logger), "Обработчик не получил Logger менеджера.");
            };
            var manager = IntegrationHost.CreateManager(IntegrationHost.Generate(provider), storage, logger, () => view);
            foreach (var name in new[] { ProbeToolProvider.FirstName, ProbeToolProvider.SecondName })
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
            Test.Require(view != null, "Для проверки вызова тестового провайдера откройте видовой экран Robur.");
            return view;
        }

        private static void RequireToolNames(string[] names, params string[] additionalNames)
        {
            var expected = new[] { ProbeToolProvider.FirstName, ProbeToolProvider.SecondName }.Concat(additionalNames);
            Test.Require(names.OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal),
                "Список должен содержать все ожидаемые инструменты без пропусков, повторов и лишних элементов.");
        }
    }
}
