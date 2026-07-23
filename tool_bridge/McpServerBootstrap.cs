using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Topomatic.ToolBridge
{
    /// <summary>
    /// Запускает локальный HTTP MCP-сервер и управляет его жизненным циклом.
    /// <para>
    /// Сервер запускается отдельным процессом и сразу помещается в Windows Job Object —
    /// системную группу для управления процессами.
    /// </para>
    /// <para>
    /// Для Job Object задано правило <c>KILL_ON_JOB_CLOSE</c>: когда Robur завершает
    /// работу и закрывает владеющий дескриптор, Windows автоматически завершает
    /// MCP-сервер и все созданные им дочерние процессы. Благодаря этому порт 8000
    /// освобождается даже без явного вызова команды <c>mcp_server_shutdown</c>.
    /// </para>
    /// <para>
    /// Класс не допускает запуск второго экземпляра сервера, синхронизирует запуск,
    /// остановку и обработку внезапного завершения процесса. Если сервер не удалось
    /// запустить или привязать к Job Object, он немедленно останавливается, чтобы не
    /// остаться в фоне. Команда <c>mcp_server_shutdown</c> закрывает Job Object и
    /// ожидает завершения сервера не более пяти секунд.
    /// </para>
    /// </summary>
    internal sealed class McpServerBootstrap
    {
        private const int ShutdownTimeoutMilliseconds = 5000;

        private static readonly Lazy<McpServerBootstrap> s_Instance =
            new Lazy<McpServerBootstrap>(() => new McpServerBootstrap());

        private readonly object m_SyncRoot = new object();
        private readonly ToolBridgeLogger m_Logger;

        // Доступ к обоим полям выполняется только при удержании m_SyncRoot.
        private Process m_McpServerProcess;
        private SafeJobHandle m_LifetimeJob;

        public static McpServerBootstrap Instance => s_Instance.Value;

        private McpServerBootstrap()
        {
            m_Logger = ToolBridgeLogger.Instance;
        }

        public bool ServerRunning
        {
            get
            {
                lock (m_SyncRoot)
                {
                    return IsRunning(m_McpServerProcess);
                }
            }
        }

        public void Run()
        {
            var executablePath = ResolveMcpServerExecutablePath();
            if (!File.Exists(executablePath))
            {
                m_Logger.Log("MCP server executable was not found.");
                return;
            }

            Process process = null;
            SafeJobHandle lifetimeJob = null;
            string startupError = null;

            lock (m_SyncRoot)
            {
                if (IsRunning(m_McpServerProcess))
                {
                    m_Logger.Log("MCP server process is already running.");
                    return;
                }

                DisposeProcess(m_McpServerProcess);
                m_McpServerProcess = null;
                DisposeJob(m_LifetimeJob);
                m_LifetimeJob = null;

                process = CreateProcess(executablePath);
                process.Exited += OnMcpServerProcessExited;

                try
                {
                    if (!process.Start())
                        throw new InvalidOperationException("Process.Start returned false.");

                    // Пока удерживается блокировка, обработчик Exited не сможет очистить
                    // процесс между его запуском и передачей во владение Job Object.
                    lifetimeJob = CreateLifetimeJob(process);
                    m_McpServerProcess = process;
                    m_LifetimeJob = lifetimeJob;
                    process = null;
                    lifetimeJob = null;
                }
                catch (Exception ex)
                {
                    startupError = ex.Message;
                    process.Exited -= OnMcpServerProcessExited;
                }
            }

            if (process != null)
            {
                // Не удалось полностью запустить сервер: завершаем уже созданный
                // процесс, чтобы он не оказался отделён от жизненного цикла Robur.
                StopProcess(process, null);
                m_Logger.Log("Failed to start MCP server: " + startupError);
                return;
            }

            m_Logger.Log("MCP server started.");
        }

        public void Shutdown()
        {
            Process process;
            SafeJobHandle lifetimeJob;

            lock (m_SyncRoot)
            {
                process = m_McpServerProcess;
                lifetimeJob = m_LifetimeJob;
                m_McpServerProcess = null;
                m_LifetimeJob = null;

                if (process != null)
                    process.Exited -= OnMcpServerProcessExited;
            }

            if (process == null)
                return;

            StopProcess(process, lifetimeJob);
        }

        private static Process CreateProcess(string executablePath)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
        }

        private SafeJobHandle CreateLifetimeJob(Process process)
        {
            // Дескриптор Job Object остаётся открытым, пока работает Robur. При его
            // закрытии Windows завершает процесс сервера и всё его дочернее дерево.
            var job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (job == null || job.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the MCP server lifetime job.");

            try
            {
                var limits = new NativeMethods.JobObjectExtendedLimitInformation
                {
                    BasicLimitInformation = new NativeMethods.JobObjectBasicLimitInformation
                    {
                        LimitFlags = NativeMethods.JobObjectLimit.KillOnJobClose
                    }
                };
                SetJobLimits(job, limits);

                if (!NativeMethods.AssignProcessToJobObject(job, process.Handle))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not assign MCP server to its lifetime job.");

                return job;
            }
            catch
            {
                job.Dispose();
                throw;
            }
        }

        private static void SetJobLimits(
            SafeJobHandle job,
            NativeMethods.JobObjectExtendedLimitInformation limits)
        {
            var size = Marshal.SizeOf(typeof(NativeMethods.JobObjectExtendedLimitInformation));
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(limits, buffer, false);
                if (!NativeMethods.SetInformationJobObject(
                        job,
                        NativeMethods.JobObjectInfoType.ExtendedLimitInformation,
                        buffer,
                        (uint)size))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not configure the MCP server lifetime job.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void OnMcpServerProcessExited(object sender, EventArgs e)
        {
            var process = sender as Process;
            if (process == null)
                return;

            SafeJobHandle lifetimeJob = null;
            lock (m_SyncRoot)
            {
                // Shutdown уже отсоединил процесс и самостоятельно выполнит его очистку.
                if (!ReferenceEquals(m_McpServerProcess, process))
                    return;

                lifetimeJob = m_LifetimeJob;
                m_McpServerProcess = null;
                m_LifetimeJob = null;
                process.Exited -= OnMcpServerProcessExited;
            }

            var exitCode = TryGetExitCode(process);
            DisposeJob(lifetimeJob);
            process.Dispose();
            m_Logger.Log($"MCP server exited with code {exitCode}.");
        }

        private void StopProcess(Process process, SafeJobHandle lifetimeJob)
        {
            try
            {
                // Закрытие Job Object завершает сервер и все созданные им процессы.
                DisposeJob(lifetimeJob);

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(ShutdownTimeoutMilliseconds);
                }
            }
            catch (Exception ex)
            {
                m_Logger.Log("Failed to stop MCP server process: " + ex.Message);
            }
            finally
            {
                process.Dispose();
            }
        }

        private static bool IsRunning(Process process)
        {
            if (process == null)
                return false;

            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static int TryGetExitCode(Process process)
        {
            try
            {
                return process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        private static void DisposeProcess(Process process)
        {
            if (process != null)
                process.Dispose();
        }

        private static void DisposeJob(SafeJobHandle job)
        {
            if (job != null)
                job.Dispose();
        }

        private string ResolveMcpServerExecutablePath()
        {
            var pluginDirectory = Path.GetDirectoryName(typeof(McpServerBootstrap).Assembly.Location);
            return Path.Combine(pluginDirectory, "mcp_server", "robur_mcp_server.exe");
        }

        private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeJobHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(handle);
            }
        }

        private static class NativeMethods
        {
            [Flags]
            internal enum JobObjectLimit : uint
            {
                KillOnJobClose = 0x00002000
            }

            internal enum JobObjectInfoType
            {
                ExtendedLimitInformation = 9
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern SafeJobHandle CreateJobObject(IntPtr jobAttributes, string name);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetInformationJobObject(
                SafeJobHandle job,
                JobObjectInfoType jobObjectInformationClass,
                IntPtr jobObjectInformation,
                uint jobObjectInformationLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);

            [StructLayout(LayoutKind.Sequential)]
            internal struct JobObjectBasicLimitInformation
            {
                internal long PerProcessUserTimeLimit;
                internal long PerJobUserTimeLimit;
                internal JobObjectLimit LimitFlags;
                internal UIntPtr MinimumWorkingSetSize;
                internal UIntPtr MaximumWorkingSetSize;
                internal uint ActiveProcessLimit;
                internal IntPtr Affinity;
                internal uint PriorityClass;
                internal uint SchedulingClass;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct IoCounters
            {
                internal ulong ReadOperationCount;
                internal ulong WriteOperationCount;
                internal ulong OtherOperationCount;
                internal ulong ReadTransferCount;
                internal ulong WriteTransferCount;
                internal ulong OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct JobObjectExtendedLimitInformation
            {
                internal JobObjectBasicLimitInformation BasicLimitInformation;
                internal IoCounters IoInfo;
                internal UIntPtr ProcessMemoryLimit;
                internal UIntPtr JobMemoryLimit;
                internal UIntPtr PeakProcessMemoryUsed;
                internal UIntPtr PeakJobMemoryUsed;
            }
        }
    }
}
