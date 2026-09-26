using System;

namespace ToolBridge.Tests
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class TestProviderAttribute : Attribute
    {

    }
}
