using Topomatic.Alg;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Exceptions;

namespace Topomatic.ToolBridge.Services.Implementation
{
    internal sealed class AlignmentProvider : IAlignmentProvider
    {
        private readonly IProjectManager m_ProjectManager;

        public AlignmentProvider([Singleton] IProjectManager projectManager)
        {
            m_ProjectManager = projectManager;
        }

        public (string name, Alignment alg) GetAlignment(string pathId)
        {
            if (string.IsNullOrWhiteSpace(pathId))
                throw new BadRequestException("PathId трассы не может быть пустым.");

            var algNode = m_ProjectManager.GetNode(pathId) ??
                throw new PreconditionFailedException($"Не удалось найти трассу в структуре проекта по указанному PathId {pathId}.");

            var algModel = algNode.Model ??
                throw new PreconditionFailedException("Модель трассы недоступна.");

            var algContainer = PluginCoreOps.LockReadContainer<IAlignmentContainer>(algModel) ??
                throw new PreconditionFailedException("Элемент проекта не является трассой или его модель недоступна.");

            var alg = algContainer.Alignment ??
                throw new PreconditionFailedException("Данные трассы недоступны.");

            return (algNode.Name, alg);
        }
    }
}
