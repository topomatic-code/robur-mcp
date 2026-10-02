using System;

namespace ToolBridge.Tests
{
    /// <summary>Assertions and messages for synchronous tests running in Robur.</summary>
    public static class Test
    {
        private static readonly TestingContext m_TestingContext = new TestingContext();

        public static void Assert(bool condition) =>
            Assert(condition, "Условие выполнено.", "Условие не выполнено.");

        /// <summary>Records only a failure and continues the current test.</summary>
        public static void Assert(bool condition, string failureMessage)
        {
            if (!condition)
                Fail(failureMessage);
        }

        public static void Assert(bool condition, string successMessage, string failureMessage)
        {
            if (condition)
                Success(successMessage);
            else
                Fail(failureMessage);
        }

        /// <summary>Stops the current test when the condition is false.</summary>
        public static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Проверка не пройдена: " + message);
        }

        /// <summary>
        /// Returns the expected exception (including derived types) without recording a message.
        /// Stops the current test if no exception is thrown; unexpected exceptions propagate unchanged.
        /// The action must be synchronous.
        /// </summary>
        public static T Throws<T>(Action action) where T : Exception
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            try { action(); }
            catch (T exception) { return exception; }
            throw new InvalidOperationException("Expected exception: " + typeof(T).FullName);
        }

        /// <summary>Stops the current test at the first missing fragment, using ordinal comparison.</summary>
        public static void RequireContains(string message, params string[] fragments)
        {
            foreach (var fragment in fragments)
                Require(message != null && message.IndexOf(fragment, StringComparison.Ordinal) >= 0,
                    "Diagnostic must contain: " + fragment + ". Actual: " + message);
        }

        public static void Success(string message) => m_TestingContext.Success(message);
        public static void Fail(string message) => m_TestingContext.Fail(message);
        public static void Warning(string message) => m_TestingContext.Warning(message);

        internal static TestingContext GetTestingContext() => m_TestingContext;
    }
}
