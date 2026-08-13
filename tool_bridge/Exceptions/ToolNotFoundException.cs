using System.Reflection;

namespace Topomatic.ToolBridge.Exceptions
{
    /// <summary>
    /// Запрошенный инструмент не зарегистрирован в tool bridge.
    /// </summary>
    /// <remarks>
    /// Обычно это исключение создаёт <c>ToolManager</c>. Провайдерам не следует использовать его
    /// для отсутствующих объектов проекта: такая ситуация относится к условию выполнения.
    /// </remarks>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class ToolNotFoundException : ToolBridgeException
    {
        /// <summary>
        /// Создаёт ошибку с кодом <see cref="ErrorCodes.ToolNotFound"/>.
        /// </summary>
        /// <param name="toolName">Имя инструмента, который не удалось найти.</param>
        public ToolNotFoundException(string toolName)
            : base(ErrorCodes.ToolNotFound, $"Tool not found: {toolName}", new { tool_name = toolName })
        {

        }
    }
}
