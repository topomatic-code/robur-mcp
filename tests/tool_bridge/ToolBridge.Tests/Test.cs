namespace ToolBridge.Tests
{
    internal static class Test
    {
        private static readonly TestingContext m_TestingContext = new TestingContext();

        public static void Assert(bool condition) =>
            Assert(condition, "Условие выполнено.", "Условие не выполнено.");

        public static void Assert(bool condition, string successMessage, string failureMessage)
        {
            if (condition)
                Success(successMessage);
            else
                Fail(failureMessage);
        }

        public static void Success(string message) => m_TestingContext.Success(message);

        public static void Fail(string message) => m_TestingContext.Fail(message);

        public static void Warning(string message) => m_TestingContext.Warning(message);

        internal static TestingContext GetTestingContext() => m_TestingContext;
    }
}
