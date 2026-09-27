using System;

namespace ToolBridge.Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class TestAttribute : Attribute
    {

    }
}
