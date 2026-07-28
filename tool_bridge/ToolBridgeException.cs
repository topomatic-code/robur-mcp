using System;
using System.Reflection;

namespace Topomatic.ToolBridge
{
    /// <summary>
    /// Базовый класс ожидаемых ошибок tool bridge, которые можно безопасно вернуть MCP-клиенту.
    /// </summary>
    /// <remarks>
    /// В прикладном коде следует выбрасывать один из специализированных наследников этого класса.
    /// Неизвестные исключения не нужно оборачивать в <see cref="ToolBridgeException"/>:
    /// pipe-сервер зарегистрирует их в журнале и вернёт как <c>internal_error</c>.
    /// Значение <see cref="Details"/> должно быть безопасным для передачи клиенту и сериализации в JSON.
    /// Не помещайте в него исключения, трассировку стека, внутренние пути или чувствительные данные.
    /// </remarks>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public abstract class ToolBridgeException : Exception
    {
        /// <summary>
        /// Инициализирует ожидаемую ошибку tool bridge.
        /// </summary>
        /// <param name="code">Стабильный машинно-читаемый код ошибки.</param>
        /// <param name="message">Безопасное сообщение, которое можно показать пользователю.</param>
        /// <param name="details">Необязательные безопасные данные для клиента.</param>
        /// <param name="innerException">Исходное ожидаемое исключение для записи в журнал.</param>
        protected ToolBridgeException(
            string code,
            string message,
            object details = null,
            Exception innerException = null)
            : base(message, innerException)
        {
            Code = code;
            Details = details;
        }

        /// <summary>
        /// Возвращает стабильный машинно-читаемый код ошибки.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Возвращает необязательные безопасные данные, которые будут записаны в поле <c>error.details</c>.
        /// </summary>
        /// <remarks>
        /// Значение предназначено для клиента и не должно содержать исключения или диагностические сведения.
        /// Для сохранения исходного исключения используйте параметр <c>innerException</c> конструктора.
        /// </remarks>
        public object Details { get; }
    }

    /// <summary>
    /// Ошибка запроса: отсутствуют обязательные аргументы или их значения имеют неверный формат.
    /// </summary>
    /// <remarks>
    /// Используйте это исключение для ошибок, которые клиент может исправить, изменив входные параметры.
    /// Не используйте его для ошибок текущего состояния Robur или сбоев выполнения инструмента.
    /// </remarks>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class BadRequestException : ToolBridgeException
    {
        /// <summary>
        /// Создаёт ошибку с кодом <c>bad_request</c>.
        /// </summary>
        /// <param name="message">Описание неверного параметра или структуры запроса.</param>
        /// <param name="details">Необязательные безопасные данные, например имя параметра.</param>
        /// <param name="innerException">Исходная ошибка разбора или валидации.</param>
        public BadRequestException(
            string message,
            object details = null,
            Exception innerException = null)
            : base("bad_request", message, details, innerException)
        {
        }
    }

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
        /// Создаёт ошибку с кодом <c>tool_not_found</c>.
        /// </summary>
        /// <param name="toolName">Имя инструмента, который не удалось найти.</param>
        public ToolNotFoundException(string toolName)
            : base(
                "tool_not_found",
                $"Tool not found: {toolName}",
                new { tool_name = toolName })
        {
        }
    }

    /// <summary>
    /// Инструмент был найден и запущен, но ожидаемая прикладная операция завершилась неудачей.
    /// </summary>
    /// <remarks>
    /// Используйте это исключение только для известных отказов предметной операции,
    /// например невозможности построить геометрию. Не перехватывайте все исключения через
    /// <c>catch (Exception)</c>, иначе программные дефекты будут ошибочно скрыты от диагностики.
    /// </remarks>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class ToolExecutionFailedException : ToolBridgeException
    {
        /// <summary>
        /// Создаёт ошибку с кодом <c>tool_execution_failed</c>.
        /// </summary>
        /// <param name="message">Безопасное описание причины отказа операции.</param>
        /// <param name="details">Необязательные безопасные сведения об операции.</param>
        /// <param name="innerException">Известное прикладное исключение, вызвавшее отказ.</param>
        public ToolExecutionFailedException(
            string message,
            object details = null,
            Exception innerException = null)
            : base("tool_execution_failed", message, details, innerException)
        {
        }
    }

    /// <summary>
    /// Корректный запрос нельзя выполнить из-за текущего состояния Robur или проекта.
    /// </summary>
    /// <remarks>
    /// Используйте это исключение, когда повторный вызов может стать успешным после изменения состояния:
    /// открытия модели, активации видового экрана, выбора объекта или загрузки необходимых данных.
    /// </remarks>
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class PreconditionFailedException : ToolBridgeException
    {
        /// <summary>
        /// Создаёт ошибку с кодом <c>precondition_failed</c>.
        /// </summary>
        /// <param name="message">Описание условия, которое необходимо выполнить.</param>
        /// <param name="details">Необязательные безопасные сведения о требуемом состоянии.</param>
        /// <param name="innerException">Исходное ожидаемое исключение проверки состояния.</param>
        public PreconditionFailedException(
            string message,
            object details = null,
            Exception innerException = null)
            : base("precondition_failed", message, details, innerException)
        {
        }
    }
}
