using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests
{
    internal static class TestRunner
    {
        internal static TestingContext.Results RunProvider(object provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            var providerType = provider.GetType();

            var context = Test.GetTestingContext();
            context.BeginTests();
            TestingContext.Results results;
            try
            {
                var methods = providerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => m.IsDefined(typeof(TestAttribute), false))
                    .OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.MetadataToken).ToArray();
                if (methods.Length == 0)
                    context.Warning("В блоке нет методов с атрибутом Test.");

                foreach (var method in methods)
                {
                    // Async void cannot be observed; waiting on Task may deadlock Robur's UI.
                    if (method.ContainsGenericParameters || method.IsAbstract ||
                        method.GetParameters().Length != 0 || method.ReturnType != typeof(void) ||
                        method.IsDefined(typeof(AsyncStateMachineAttribute), false))
                    {
                        context.Skip(method.Name + ": нужен синхронный метод void без параметров и параметров типа.");
                        continue;
                    }

                    context.BeginTest(method.Name);
                    try
                    {
                        method.Invoke(method.IsStatic ? null : provider, null);
                    }
                    catch (Exception ex)
                    {
                        context.Fail(Unwrap(ex).ToString());
                    }
                    finally
                    {
                        context.EndTest();
                    }
                }
            }
            finally
            {
                results = context.EndTests();
            }
            return results;
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
                exception = exception.InnerException;
            return exception;
        }
    }
}
