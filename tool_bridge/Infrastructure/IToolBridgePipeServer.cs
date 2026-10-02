using System;

namespace Topomatic.ToolBridge.Infrastructure
{
    internal interface IToolBridgePipeServer : IDisposable
    {
        void Start();
    }
}
