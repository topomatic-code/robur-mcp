using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Topomatic.ToolBridge;

namespace ToolBridge_v0_1_0_ApiChecker
{
    // Keep registration dormant outside the integration test, including normal startup.
    internal static class LegacyProviderProbe
    {
        private static bool m_RegistrationEnabled;

        internal static bool RegistrationEnabled
        {
            get => m_RegistrationEnabled;
            set => m_RegistrationEnabled = value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Register(object[] args)
        {
            ((List<ToolProvider>)args[0]).Add(new Provider());
        }

        internal sealed class Provider : ToolProvider
        {
            // Two absent domains exercise comparison even when there are no saved tools.
            // These attributes and the base class compile against ToolBridge 0.1.0 only.
            [ToolDef(Name = "api_0_1_no_domain_echo", Description = "Legacy echo probe",
                InputSchema = "{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"required\":[\"value\"]}",
                ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true)]
            public object Echo(Dictionary<string, object> arguments)
            {
                if (AppHost == null || CadView == null || SessionStorage == null || Logger == null)
                    throw new InvalidOperationException("Legacy provider did not receive its runtime context.");
                return new Dictionary<string, object> { { "value", arguments["value"] } };
            }

            [ToolDef(Name = "api_0_1_no_domain_second", Description = "Second legacy probe",
                InputSchema = "{\"type\":\"object\"}", ReadOnlyHint = true,
                DestructiveHint = false, IdempotentHint = true)]
            public object Second(Dictionary<string, object> arguments) => arguments;
        }
    }
}
