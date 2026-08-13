using System;
using System.Reflection;

namespace Topomatic.ToolBridge.Exceptions
{
    /// <summary>
    /// Базовый класс ожидаемых ошибок tool bridge, которые можно безопасно вернуть MCP-клиенту.
    /// </summary>
    /// <remarks>
    /// В прикладном коде следует выбрасывать один из специализированных наследников этого класса.
    /// Неизвестные исключения не нужно оборачивать в <see cref="ToolBridgeException"/>:
    /// pipe-сервер зарегистрирует их в журнале и вернёт с кодом <see cref="ErrorCodes.InternalError"/>.
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
        protected ToolBridgeException(string code, string message, object details = null, Exception innerException = null)
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
}
