using Newtonsoft.Json.Linq;
using System;
using System.Reflection;

namespace Topomatic.ToolBridge
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ToolDefAttribute : Attribute
    {
        /// <summary>
        /// Название tool.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Описание tool.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Json-схема входных параметров tool (JSON Schema 2020-12).
        /// </summary>
        public string InputSchema { get; set; }

        /// <summary>
        /// Вызов tool ничего не изменяет, только читает данные.
        /// </summary>
        public bool ReadOnlyHint { get; set; }

        /// <summary>
        /// Вызов tool изменяет или удаляет данные, потенциально необратимо.
        /// </summary>
        public bool DestructiveHint { get; set; }

        /// <summary>
        /// Повторный вызов tool даёт тот же результат и не меняет состояние после первого раза.
        /// </summary>
        public bool IdempotentHint { get; set; }

        internal ToolDefinition GetDefinition()
        {
            return new ToolDefinition(
                Name,
                Description,
                JObject.Parse(InputSchema),
                new ToolAnnotations(ReadOnlyHint, DestructiveHint, IdempotentHint)
            );
        }
    }
}
