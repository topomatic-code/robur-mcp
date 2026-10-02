using System;

namespace Topomatic.ToolBridge.Services
{
    public interface IObjectStorage
    {
        Guid AddObject(object obj);
        void AddObject(Guid guid, object obj);
        object GetObject(Guid guid);
        Guid GetGuid(object obj);
        bool HasObject(Guid guid);
        bool HasObject(object obj);
        bool RemoveObject(Guid guid);
        bool RemoveObject(object obj);
        void Clear();
    }
}
