using System.Collections.Generic;
using ToolBridge.Tests.Core.DependencyInjection;
using ToolBridge.Tests.Core.Tools.AlgTools;
using ToolBridge.Tests.Core.Tools.DwgTools;
using Topomatic.ApplicationPlatform.Plugins;

namespace ToolBridge.Tests.Core
{
    internal sealed class Module : PluginInitializator
    {
        [cmd("tool_bridge_tests_core_register")]
        private void RegisterTests(object[] args)
        {
            ((List<object>)args[0]).Add(new ContainerTests());
            ((List<object>)args[0]).Add(new GetAlignmentTests());
            ((List<object>)args[0]).Add(new GetParametersTests());
            ((List<object>)args[0]).Add(new GetConstructionTests());
            ((List<object>)args[0]).Add(new CreatePolylineTests());
            ((List<object>)args[0]).Add(new CreateCircleTests());
            ((List<object>)args[0]).Add(new CreateLineTests());
            ((List<object>)args[0]).Add(new CreateTextTests());
            ((List<object>)args[0]).Add(new CreateMTextTests());
            ((List<object>)args[0]).Add(new CreateTableTests());
            ((List<object>)args[0]).Add(new CreateHatchTests());
            ((List<object>)args[0]).Add(new UpdatePolylineTests());
            ((List<object>)args[0]).Add(new UpdateCircleTests());
            ((List<object>)args[0]).Add(new UpdateLineTests());
            ((List<object>)args[0]).Add(new UpdateTextTests());
            ((List<object>)args[0]).Add(new UpdateMTextTests());
            ((List<object>)args[0]).Add(new UpdateTableTests());
            ((List<object>)args[0]).Add(new UpdateHatchTests());
            ((List<object>)args[0]).Add(new GetActiveDrawingInfoTests());
            ((List<object>)args[0]).Add(new AddLayerTests());
            ((List<object>)args[0]).Add(new UpdateLayerTests());
            ((List<object>)args[0]).Add(new RemoveLayerTests());
            ((List<object>)args[0]).Add(new SetActiveLayerTests());
            ((List<object>)args[0]).Add(new GetEntitiesTests());
            ((List<object>)args[0]).Add(new GetActiveSpaceEntityTests());
            ((List<object>)args[0]).Add(new RemoveActiveSpaceEntityTests());
            ((List<object>)args[0]).Add(new SetEntitiesLayerTests());
            ((List<object>)args[0]).Add(new SetEntitiesColorTests());
            ((List<object>)args[0]).Add(new GetHatchPatternsTests());
        }
    }
}
