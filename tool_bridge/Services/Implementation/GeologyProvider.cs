using Topomatic.Glg.Model;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Exceptions;

namespace Topomatic.ToolBridge.Services.Implementation
{
    internal sealed class GeologyProvider : IGeologyProvider
    {
        private readonly IProjectManager m_ProjectManager;

        public GeologyProvider([Singleton] IProjectManager projectManager)
        {
            m_ProjectManager = projectManager;
        }

        public (string name, GeologyModel glg) GetGeology(string pathId)
        {
            if (string.IsNullOrWhiteSpace(pathId))
                throw new BadRequestException("PathId геологии не может быть пустым.");

            var glgNode = m_ProjectManager.GetNode(pathId) ??
                throw new PreconditionFailedException($"Не удалось найти геологию в структуре проекта по указанному PathId {pathId}.");

            var glgModel = glgNode.Model ??
                throw new PreconditionFailedException("Модель геологии недоступна.");

            var glg = glgModel.Model as GeologyModel ??
                throw new PreconditionFailedException("Элемент проекта не является геологией или его модель недоступна.");

            return (glgNode.Name, glg);
        }
    }
}
