namespace Topomatic.ToolBridge.DependencyInjection
{
    public interface IContainer
    {
        T CreateInstance<T>() where T : class;
        T GetSingleton<T>() where T : class;
    }
}
