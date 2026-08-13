using System;
using System.Reflection;

namespace Topomatic.ToolBridge.Exceptions
{
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
        /// Создаёт ошибку с кодом <see cref="ErrorCodes.ToolExecutionFailed"/>.
        /// </summary>
        /// <param name="message">Безопасное описание причины отказа операции.</param>
        /// <param name="details">Необязательные безопасные сведения об операции.</param>
        /// <param name="innerException">Известное прикладное исключение, вызвавшее отказ.</param>
        public ToolExecutionFailedException(string message, object details = null, Exception innerException = null)
            : base(ErrorCodes.ToolExecutionFailed, message, details, innerException)
        {

        }
    }
}
