using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ToolBridge.Tests
{
    internal static class TestRunner
    {
        internal static TestingContext.Results RunProvider(Type providerType)
        {
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

                object provider = null;
                Exception constructorError = null;
                bool constructorAttempted = false;
                foreach (var method in methods)
                {
                    // Async void cannot be observed; waiting on Task may deadlock Robur's UI.
                    if (providerType.ContainsGenericParameters || method.ContainsGenericParameters ||
                        method.IsAbstract || method.GetParameters().Length != 0 ||
                        method.ReturnType != typeof(void) || method.IsDefined(typeof(AsyncStateMachineAttribute), false))
                    {
                        context.Skip(method.Name + ": нужен синхронный метод void без параметров и параметров типа.");
                        continue;
                    }

                    context.BeginTest(method.Name);
                    try
                    {
                        if (!method.IsStatic && !constructorAttempted)
                        {
                            constructorAttempted = true;
                            try { provider = Activator.CreateInstance(providerType, true); }
                            catch (Exception ex) { constructorError = Unwrap(ex); }
                        }
                        if (!method.IsStatic && constructorError != null)
                            context.Fail("Не удалось создать провайдер: " + constructorError);
                        else
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
