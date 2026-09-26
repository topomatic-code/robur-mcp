using System;

namespace ToolBridge.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class TestAttribute : Attribute
    {

    }
}
