using System;
using System.Reflection;

namespace Topomatic.ToolBridge.Exceptions
{
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
        /// Создаёт ошибку с кодом <see cref="ErrorCodes.BadRequest"/>.
        /// </summary>
        /// <param name="message">Описание неверного параметра или структуры запроса.</param>
        /// <param name="details">Необязательные безопасные данные, например имя параметра.</param>
        /// <param name="innerException">Исходная ошибка разбора или валидации.</param>
        public BadRequestException(string message, object details = null, Exception innerException = null)
            : base(ErrorCodes.BadRequest, message, details, innerException)
        {

        }
    }
}
