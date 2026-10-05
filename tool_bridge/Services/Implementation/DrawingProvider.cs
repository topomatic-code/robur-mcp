using Topomatic.Dwg;
using Topomatic.Dwg.Layer;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Infrastructure;

namespace Topomatic.ToolBridge.Services.Implementation
{
    internal class DrawingProvider : IDrawingProvider
    {
        private readonly ICadViewProvider m_CadViewProvider;

        public DrawingProvider([Singleton] ICadViewProvider cadViewProvider)
        {
            m_CadViewProvider = cadViewProvider;
        }

        public Drawing GetActiveDrawing(bool require)
        {
            var cadView = m_CadViewProvider.CadView;
            var layer = cadView == null ? null : DrawingLayer.GetDrawingLayer(cadView);
            var drawing = layer?.Drawing;

            if (require && drawing == null)
                throw new PreconditionFailedException("Не удалось получить активный чертеж.");

            return drawing;
        }
    }
}
