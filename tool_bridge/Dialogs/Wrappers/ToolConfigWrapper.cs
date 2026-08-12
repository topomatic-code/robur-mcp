using System.ComponentModel;
using Topomatic.ComponentModel;
using Topomatic.ToolBridge.Dialogs.Converters;
using Topomatic.ToolBridge.Settings;

namespace Topomatic.ToolBridge.Dialogs.Wrappers
{
    internal sealed class ToolConfigWrapper
    {
        private const string CATEGORY_PARAMETERS = "Параметры инструмента";
        private const string CATEGORY_FLAGS = "Флаги инструмента";
        private const string CATEGORY_SETTINGS = "Настройки инструмента";

        private readonly ToolConfig m_ToolConfig;

        public ToolConfigWrapper(ToolConfig toolConfig)
        {
            m_ToolConfig = toolConfig;
            ApprovalScope = toolConfig.ApprovalScope;
            Enabled = toolConfig.Enabled;
        }

        [Browsable(false)]
        public bool Enabled { get; set; }

        [Browsable(false)]
        public string InputSchema => m_ToolConfig.InputSchema;

        [Category(CATEGORY_PARAMETERS)]
        [DisplayName("Категория")]
        public string Domain => m_ToolConfig.Domain;

        [Category(CATEGORY_PARAMETERS)]
        [DisplayName("Название")]
        public string Name => m_ToolConfig.Name;

        [Category(CATEGORY_PARAMETERS)]
        [DisplayName("Описание")]
        public string Description => m_ToolConfig.Description;

        [Category(CATEGORY_FLAGS)]
        [DisplayName("Только для чтения")]
        public bool ReadOnly => m_ToolConfig.ReadOnly;

        [Category(CATEGORY_FLAGS)]
        [DisplayName("Изменяет состояние (данные)")]
        public bool Destructive => m_ToolConfig.Destructive;

        [Category(CATEGORY_FLAGS)]
        [DisplayName("Идемпотентный")]
        public bool Idempotent => m_ToolConfig.Idempotent;

        [Category(CATEGORY_SETTINGS)]
        [DisplayName("Режим подтверждения выполнения")]
        [PropertyTypeConverter(typeof(ToolApprovalScopeConverter))]
        public ToolApprovalScope ApprovalScope { get; set; }

        public void SaveChanges()
        {
            m_ToolConfig.Enabled = Enabled;
            m_ToolConfig.ApprovalScope = ApprovalScope;
        }
    }
}
