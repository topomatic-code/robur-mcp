using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ToolBridge.Tests
{
    // A separate Windows console ensures that closing it never terminates Robur.
    // Windows PowerShell is built into Windows; no third-party dependencies are used.
    internal sealed class TestConsole : IDisposable
    {
        private readonly NamedPipeServerStream m_Pipe;
        private readonly BlockingCollection<string> m_Messages = new BlockingCollection<string>();
        private readonly object m_SyncRoot = new object();
        private readonly Task m_OutputTask;
        private bool m_Closed;

        internal Task Completion => m_OutputTask;

        public TestConsole()
        {
            var pipeName = "ToolBridge.Tests." + Guid.NewGuid().ToString("N");
            m_Pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                var script = @"
$Host.UI.RawUI.WindowTitle = 'Tool Bridge Tests'
$client = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'PIPE_NAME', [System.IO.Pipes.PipeDirection]::In)
$completed = $false
try {
    $client.Connect(15000)
    $reader = [System.IO.StreamReader]::new($client, [System.Text.Encoding]::UTF8)
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line -eq 'complete') { $completed = $true; break }
            $parts = $line.Split([char]9, 2)
            $message = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($parts[1]))
            Write-Host $message -ForegroundColor ([ConsoleColor][int]$parts[0])
        }
    } finally { $reader.Dispose() }
} catch { Write-Host ('! ' + $_.Exception.Message) -ForegroundColor Yellow }
finally { $client.Dispose() }
if (-not $completed) { Write-Host '! Вывод результатов прерван; отчёт может быть неполным.' -ForegroundColor Yellow }
Write-Host 'Нажмите Enter, чтобы закрыть окно.'
[void](Read-Host)
".Replace("PIPE_NAME", pipeName);
                var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    @"WindowsPowerShell\v1.0\powershell.exe");
                var start = new ProcessStartInfo(executable, "-NoLogo -NoProfile -EncodedCommand " +
                    Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                using (var process = Process.Start(start)) { }
                m_OutputTask = Task.Run(() => ProcessOutput(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5)));
                Write(ConsoleColor.Gray, "Запуск тестов Tool Bridge...");
            }
            catch
            {
                m_Pipe.Dispose();
                m_Messages.Dispose();
                throw;
            }
        }

        // Allows checking the transport with a local pipe without opening a window.
        internal TestConsole(NamedPipeServerStream pipe, TimeSpan connectionTimeout, TimeSpan writeTimeout)
        {
            m_Pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
            m_OutputTask = Task.Run(() => ProcessOutput(connectionTimeout, writeTimeout));
        }

        public void WriteResults(TestingContext.Results results) => WriteResults(results, Write);

        // The output delegate allows verifying formatting without opening a window.
        internal static void WriteResults(TestingContext.Results results, Action<ConsoleColor, string> output)
        {
            foreach (var message in results.Messages)
            {
                switch (message.Kind)
                {
                    case TestingContext.MessageKind.Success:
                        output(ConsoleColor.Green, "✓ " + message.Text);
                        break;
                    case TestingContext.MessageKind.Failure:
                        output(ConsoleColor.Red, "✗ " + message.Text);
                        break;
                    case TestingContext.MessageKind.Warning:
                        output(ConsoleColor.Yellow, "! " + message.Text);
                        break;
                }
            }
            output(ConsoleColor.Green, $"✓ Пройдено: {results.Passed}");
            output(ConsoleColor.Red, $"✗ Не пройдено: {results.Failed}");
            if (results.Skipped != 0)
                output(ConsoleColor.Yellow, $"! Не выполнено: {results.Skipped}");
            output(ConsoleColor.Gray, string.Empty);
        }

        public void Write(ConsoleColor color, string text)
        {
            lock (m_SyncRoot)
            {
                if (m_Closed)
                    return;
                m_Messages.Add(((int)color).ToString() + "\t" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
            }
        }

        public void Dispose()
        {
            // Finish delivery in the background; never wait for the viewer on Robur's thread.
            lock (m_SyncRoot)
            {
                if (m_Closed)
                    return;
                m_Closed = true;
                m_Messages.CompleteAdding();
            }
        }

        private async Task ProcessOutput(TimeSpan connectionTimeout, TimeSpan writeTimeout)
        {
            try
            {
                if (!m_Pipe.IsConnected)
                    await WithTimeout(m_Pipe.WaitForConnectionAsync(), connectionTimeout).ConfigureAwait(false);

                foreach (var message in m_Messages.GetConsumingEnumerable())
                    await WriteLine(message, writeTimeout).ConfigureAwait(false);

                await WriteLine("complete", writeTimeout).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Closing the viewer must not interrupt test execution.
            }
            catch (TimeoutException)
            {
                // Stop sending when the viewer cannot connect or consume output.
            }
            catch (ObjectDisposedException)
            {
                // The pipe can be closed while an asynchronous operation is pending.
            }
            finally
            {
                m_Pipe.Dispose();
                lock (m_SyncRoot)
                {
                    m_Closed = true;
                    m_Messages.Dispose();
                }
            }
        }

        private Task WriteLine(string message, TimeSpan timeout)
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\n");
            return WithTimeout(m_Pipe.WriteAsync(bytes, 0, bytes.Length), timeout);
        }

        private async Task WithTimeout(Task operation, TimeSpan timeout)
        {
            using (var timerCancellation = new CancellationTokenSource())
            {
                var timer = Task.Delay(timeout, timerCancellation.Token);
                if (await Task.WhenAny(operation, timer).ConfigureAwait(false) != operation)
                {
                    // Observe cancellation of pending I/O without waiting on the producer.
                    var observation = operation.ContinueWith(t => { var error = t.Exception; },
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted |
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    m_Pipe.Dispose();
                    throw new TimeoutException("Console output timed out.");
                }
                timerCancellation.Cancel();
                await operation.ConfigureAwait(false);
            }
        }
    }
}

