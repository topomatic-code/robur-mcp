using System;

namespace Topomatic.ToolBridge.DependencyInjection
{
    /// <summary>
    /// Указывает, что параметр конструктора разрешается из регистрации синглтона.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class SingletonAttribute : Attribute
    {
    }
}
