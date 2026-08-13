using System;
using System.Reflection;

namespace Topomatic.ToolBridge.Exceptions
{
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
        /// Создаёт ошибку с кодом <see cref="ErrorCodes.PreconditionFailed"/>.
        /// </summary>
        /// <param name="message">Описание условия, которое необходимо выполнить.</param>
        /// <param name="details">Необязательные безопасные сведения о требуемом состоянии.</param>
        /// <param name="innerException">Исходное ожидаемое исключение проверки состояния.</param>
        public PreconditionFailedException(string message, object details = null, Exception innerException = null)
            : base(ErrorCodes.PreconditionFailed, message, details, innerException)
        {

        }
    }
}
