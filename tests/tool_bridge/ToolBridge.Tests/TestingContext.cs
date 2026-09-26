using System;
using System.Collections.Generic;

namespace ToolBridge.Tests
{
    internal sealed class TestingContext
    {
        private readonly List<Message> m_Messages = new List<Message>();
        private int m_Passed;
        private int m_Failed;
        private int m_Skipped;
        private bool m_BlockActive;
        private bool m_TestActive;
        private bool m_TestFailed;
        private string m_TestName;
        private int m_FirstMessage;

        public void BeginTests()
        {
            if (m_BlockActive)
                throw new InvalidOperationException("EndTests must be called before starting another block.");
            Reset();
            m_BlockActive = true;
        }

        public Results EndTests()
        {
            if (!m_BlockActive)
                throw new InvalidOperationException("BeginTests must be called first.");
            if (m_TestActive)
                throw new InvalidOperationException("EndTest must be called before ending the block.");
            try
            {
                // Return an independent snapshot before resetting the block.
                return new Results(new List<Message>(m_Messages).AsReadOnly(), m_Passed, m_Failed, m_Skipped);
            }
            finally
            {
                Reset();
            }
        }

        public void Success(string message) => AddMessage(MessageKind.Success, message);

        public void Fail(string message)
        {
            AddMessage(MessageKind.Failure, message);
            m_TestFailed = true;
        }

        public void Warning(string message) => AddMessage(MessageKind.Warning, message);

        internal void BeginTest(string name)
        {
            if (!m_BlockActive || m_TestActive)
                throw new InvalidOperationException("A test requires an active block with no running test.");
            m_TestActive = true;
            m_TestFailed = false;
            m_TestName = name;
            m_FirstMessage = m_Messages.Count;
        }

        internal void EndTest()
        {
            if (!m_TestActive)
                throw new InvalidOperationException("BeginTest must be called first.");
            if (m_TestFailed)
                m_Failed++;
            else
                m_Passed++;
            m_Messages.Insert(m_FirstMessage,
                new Message(m_TestFailed ? MessageKind.Failure : MessageKind.Success, m_TestName));
            m_TestActive = false;
            m_TestFailed = false;
            m_TestName = null;
            m_FirstMessage = 0;
        }

        internal void Skip(string message)
        {
            if (!m_BlockActive || m_TestActive)
                throw new InvalidOperationException("A test can only be skipped between tests in an active block.");
            Warning(message);
            m_Skipped++;
        }

        private void AddMessage(MessageKind kind, string text)
        {
            if (!m_BlockActive)
                throw new InvalidOperationException("Test messages must be written inside a test block.");
            if (kind != MessageKind.Warning && !m_TestActive)
                throw new InvalidOperationException("Test results must be written inside a test method.");
            m_Messages.Add(new Message(kind, text));
        }

        private void Reset()
        {
            m_Messages.Clear();
            m_Passed = m_Failed = m_Skipped = 0;
            m_BlockActive = m_TestActive = m_TestFailed = false;
            m_TestName = null;
            m_FirstMessage = 0;
        }

        internal enum MessageKind
        {
            Success,
            Failure,
            Warning
        }

        internal sealed class Message
        {
            public Message(MessageKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }

            public MessageKind Kind { get; }
            public string Text { get; }
        }

        internal sealed class Results
        {
            public Results(IReadOnlyList<Message> messages, int passed, int failed, int skipped)
            {
                Messages = messages;
                Passed = passed;
                Failed = failed;
                Skipped = skipped;
            }

            public IReadOnlyList<Message> Messages { get; }
            public int Passed { get; }
            public int Failed { get; }
            public int Skipped { get; }
        }
    }
}
