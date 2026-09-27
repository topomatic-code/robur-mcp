using System;
using System.Collections.Generic;
using Topomatic.ApplicationPlatform;
using Topomatic.ToolBridge;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    internal sealed class LegacyToolProvider : ToolProvider
    {
        internal const string FirstName = "api_0_1_no_domain_first";
        internal const string SecondName = "api_0_1_no_domain_second";

        [ThreadStatic]
        private static List<ToolProvider> s_RegistrationTarget;

        internal Action<string, LegacyToolProvider> OnInvoke { get; set; }
        internal int InvocationCount { get; private set; }

        internal static LegacyToolProvider Discover()
        {
            Test.Require(s_RegistrationTarget == null, "Повторный вход в обнаружение тестового провайдера.");
            var providers = new List<ToolProvider>();
            s_RegistrationTarget = providers;
            try
            {
                ApplicationHost.Current.Plugins.Broadcast("tool_request", new string[0], new object[] { providers });
            }
            finally
            {
                s_RegistrationTarget = null;
            }

            LegacyToolProvider result = null;
            foreach (var provider in providers)
            {
                var legacy = provider as LegacyToolProvider;
                if (legacy == null)
                    continue;
                Test.Require(result == null, "Тестовый провайдер зарегистрирован повторно.");
                result = legacy;
            }
            Test.Require(result != null, "Broadcast tool_request не обнаружил провайдер API 0.1.0. Проверьте установку .plugin.");
            return result;
        }

        internal static void Register(object[] args)
        {
            // Register only in this test's broadcast, never in the live MCP collector.
            if (s_RegistrationTarget != null && ReferenceEquals(args[0], s_RegistrationTarget))
                s_RegistrationTarget.Add(new LegacyToolProvider());
        }

        // Domain is absent from the 0.1.0 reference used to compile these attributes.
        [ToolDef(Name = FirstName, Description = "API compatibility probe",
            InputSchema = "{\"type\":\"object\"}",
            ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true)]
        public object First(Dictionary<string, object> arguments)
        {
            InvocationCount++;
            OnInvoke?.Invoke(FirstName, this);
            return null;
        }

        [ToolDef(Name = SecondName, Description = "API compatibility probe",
            InputSchema = "{\"type\":\"object\"}",
            ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true)]
        public object Second(Dictionary<string, object> arguments)
        {
            InvocationCount++;
            OnInvoke?.Invoke(SecondName, this);
            return null;
        }
    }
}
