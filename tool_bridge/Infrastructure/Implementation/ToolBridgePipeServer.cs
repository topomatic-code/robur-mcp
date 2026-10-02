using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topomatic.ToolBridge.DependencyInjection;
using Topomatic.ToolBridge.Dialogs;
using Topomatic.ToolBridge.Dialogs.Results;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Settings;

namespace Topomatic.ToolBridge.Infrastructure.Implementation
{
    internal sealed class ToolBridgePipeServer : IToolBridgePipeServer
    {
        private const string PIPE_NAME = "robur_tool_bridge";

        private readonly IToolManager m_ToolManager;
        private readonly IToolSettings m_ToolSettings;
        private readonly IToolBridgeLogger m_Logger;
        private readonly CancellationTokenSource m_Cts;
        private readonly object m_SyncRoot;

        private Task m_AcceptLoop;
        private NamedPipeServerStream m_ActiveStream;
        private WindowsBridgeSecurityContext m_SecurityContext;
        private bool m_Started;
        private volatile bool m_Locked;
        private bool m_Disposed;

        public ToolBridgePipeServer(
            [Singleton] IToolManager toolManager,
            [Singleton] IToolSettings toolSettings,
            [Singleton] IToolBridgeLogger logger
        )
        {
            m_ToolManager = toolManager;
            m_ToolSettings = toolSettings;
            m_Logger = logger;
            m_Cts = new CancellationTokenSource();
            m_SyncRoot = new object();
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (m_Started)
                return;

            WindowsBridgeSecurityContext securityContext = null;
            NamedPipeServerStream stream = null;
            try
            {
                securityContext = WindowsBridgeSecurityContext.Acquire();
                stream = securityContext.CreatePipe(PIPE_NAME);
                m_SecurityContext = securityContext;
                SetActiveStream(stream);
                m_Started = true;
                m_AcceptLoop = Task.Run(() => AcceptSingleClient(stream, m_Cts.Token));
            }
            catch
            {
                stream?.Dispose();
                securityContext?.Dispose();
                throw;
            }
        }

        internal bool Locked => m_Locked;

        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;
            m_Cts.Cancel();
            AbortActiveStream();
            var acceptLoop = m_AcceptLoop;
            var acceptLoopCompleted = acceptLoop == null;
            try
            {
                if (acceptLoop != null)
                    acceptLoopCompleted = acceptLoop.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                acceptLoopCompleted = true;
            }
            catch (ObjectDisposedException)
            {
                acceptLoopCompleted = acceptLoop?.IsCompleted ?? true;
            }
            finally
            {
                m_AcceptLoop = null;
            }

            if (acceptLoopCompleted)
            {
                ReleaseSecurityContext();
            }
            else
            {
                // Mutex должен оставаться занятым, пока рабочая задача фактически не завершится.
                acceptLoop.ContinueWith(
                    _ => ReleaseSecurityContext(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            m_Cts.Dispose();
        }

        private void ReleaseSecurityContext()
        {
            var securityContext = Interlocked.Exchange(ref m_SecurityContext, null);
            securityContext?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
                throw new ObjectDisposedException(nameof(ToolBridgePipeServer));
        }

        private bool IsShuttingDown(CancellationToken token)
        {
            return token.IsCancellationRequested || m_Disposed;
        }

        private void SetActiveStream(NamedPipeServerStream stream)
        {
            lock (m_SyncRoot)
            {
                m_ActiveStream = stream;
            }
        }

        private void ClearActiveStream(NamedPipeServerStream stream)
        {
            lock (m_SyncRoot)
            {
                if (ReferenceEquals(m_ActiveStream, stream))
                    m_ActiveStream = null;
            }
        }

        private void AbortActiveStream()
        {
            NamedPipeServerStream stream = null;
            lock (m_SyncRoot)
            {
                stream = m_ActiveStream;
                m_ActiveStream = null;
            }
            if (stream != null)
            {
                try
                {
                    stream.Dispose();
                }
                catch { }
            }
        }

        private void AcceptSingleClient(NamedPipeServerStream stream, CancellationToken token)
        {
            try
            {
                stream.WaitForConnection();
                if (IsShuttingDown(token))
                    return;

                HandleClient(stream, token);
                if (!IsShuttingDown(token))
                {
                    m_Locked = true;
                    m_Logger.PublicWarning("Pipe client disconnected. Restart the pipe bridge to accept another client.");
                    m_Logger.SystemWarning("Pipe client disconnected. Restart the pipe bridge to accept another client.");
                }
            }
            catch (OperationCanceledException)
            {
                // штатная остановка сервера
            }
            catch (ObjectDisposedException) when (IsShuttingDown(token))
            {
                // штатная остановка сервера
            }
            catch (IOException) when (IsShuttingDown(token))
            {
                // штатная остановка сервера
            }
            catch (Exception ex)
            {
                if (!IsShuttingDown(token))
                {
                    m_Locked = true;
                    m_Logger.PublicError("Pipe server error: " + ex);
                    m_Logger.SystemError("Pipe server error: " + ex);
                }
            }
            finally
            {
                ClearActiveStream(stream);
                stream.Dispose();
            }
        }

        private void HandleClient(NamedPipeServerStream stream, CancellationToken token)
        {
            try
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                {
                    writer.AutoFlush = true;
                    var clientVerified = false;
                    while (stream.IsConnected && !token.IsCancellationRequested)
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                            return;

                        if (!clientVerified)
                        {
                            // Windows предоставляет token клиента для impersonation после чтения его сообщения.
                            if (!m_SecurityContext.IsCurrentLogonClient(stream))
                            {
                                m_Locked = true;
                                m_Logger.PublicWarning("Pipe client belongs to a different Windows logon session.");
                                m_Logger.SystemWarning("Pipe client belongs to a different Windows logon session.");
                                return;
                            }
                            clientVerified = true;
                            m_Logger.SystemInfo("Pipe client connected.");
                        }

                        var systemLoggingEnabled = m_Logger.SystemLoggingEnabled;
                        string exchangeId = null;
                        Stopwatch stopwatch = null;
                        if (systemLoggingEnabled)
                        {
                            exchangeId = Guid.NewGuid().ToString("N");
                            stopwatch = Stopwatch.StartNew();
                            m_Logger.SystemInfo(
                                $"Pipe request [exchange_id={exchangeId}]:"
                                + Environment.NewLine
                                + PreparePayloadForLog(line));
                        }

                        BridgeRequest request = null;
                        BridgeResponse response = null;
                        try
                        {
                            request = JsonConvert.DeserializeObject<BridgeRequest>(line);
                        }
                        catch (JsonException ex)
                        {
                            //m_Logger.PublicWarning("Bad request: invalid JSON. " + ex);
                            response = BridgeResponse.Fail(
                                null,
                                ErrorCodes.BadRequest,
                                "Request body contains invalid JSON.",
                                null);
                        }
                        catch (Exception ex)
                        {
                            response = CreateInternalErrorResponse(null, ex);
                        }

                        if (response == null)
                        {
                            try
                            {
                                response = ProcessRequest(request);
                            }
                            catch (ToolBridgeException ex)
                            {
                                m_Logger.SystemWarning($"Expected error [{ex.Code}]: {ex}");
                                response = BridgeResponse.Fail(
                                    request?.Id,
                                    ex.Code,
                                    ex.Message,
                                    GetSafeErrorDetails(ex));
                            }
                            catch (Exception ex)
                            {
                                response = CreateInternalErrorResponse(request?.Id, ex);
                            }
                        }

                        var json = SerializeResponse(response, request?.Id);
                        writer.WriteLine(json);
                        if (systemLoggingEnabled)
                        {
                            stopwatch.Stop();
                            m_Logger.SystemInfo(
                                $"Pipe response [exchange_id={exchangeId}] "
                                + $"[request_id={FormatLogValue(request?.Id)}] "
                                + $"[elapsed_ms={stopwatch.ElapsedMilliseconds}]:"
                                + Environment.NewLine
                                + PreparePayloadForLog(json));
                        }
                    }
                }
            }
            catch (ObjectDisposedException) when (IsShuttingDown(token))
            {
                // штатная остановка сервера
            }
            catch (IOException)
            {
                // штатное разъединение клиента
            }
        }

        private BridgeResponse CreateInternalErrorResponse(string requestId, Exception exception)
        {
            var traceId = Guid.NewGuid().ToString("N");
            //m_Logger.PublicError($"Internal error [{traceId}]: {exception}");
            return BridgeResponse.Fail(
                requestId,
                ErrorCodes.InternalError,
                "Internal server error.",
                new { trace_id = traceId });
        }

        private string SerializeResponse(BridgeResponse response, string requestId)
        {
            try
            {
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(CreateInternalErrorResponse(requestId, ex));
            }
        }

        private object GetSafeErrorDetails(ToolBridgeException exception)
        {
            if (exception.Details == null)
                return null;
            try
            {
                var serializer = JsonSerializer.CreateDefault();
                serializer.Converters.Add(new ExceptionRejectingJsonConverter());
                return JToken.FromObject(exception.Details, serializer);
            }
            catch (Exception detailsException)
            {
                m_Logger.SystemWarning($"Invalid error details omitted [{exception.Code}]: {detailsException}");
                return null;
            }
        }

        private sealed class ExceptionRejectingJsonConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType)
            {
                return typeof(Exception).IsAssignableFrom(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new JsonSerializationException("Exception objects are not allowed in error details.");
            }
        }

        private BridgeResponse ProcessRequest(BridgeRequest request)
        {
            if (request == null)
                return BridgeResponse.Fail(null, ErrorCodes.BadRequest, "Request body is empty or invalid JSON.", null);

            switch ((request.Method ?? string.Empty).Trim())
            {
                case "ping":
                    m_Logger.PublicInfo("execute -> ping");
                    return BridgeResponse.OK(request.Id, new
                    {
                        cadProcess = "demo-cad",
                        serverTimeUtc = DateTime.UtcNow.ToString("O")
                    });
                case "list_tools":
                    m_Logger.PublicInfo("execute -> list_tools");
                    return BridgeResponse.OK(request.Id, new
                    {
                        tools = m_ToolManager.GetTools()
                            .Where(t => m_ToolSettings.GetConfig(t.Name)?.Enabled ?? false)
                            .Select(CreatePipeToolDefinition)
                            .ToArray()
                    });
                case "call_tool":
                    var toolName = "<missing>";
                    if (request.Params != null && request.Params.TryGetValue("tool_name", out var toolNameObj))
                        toolName = Convert.ToString(toolNameObj);

                    if ((!m_ToolSettings.GetConfig(toolName)?.Enabled) ?? true)
                        throw new BadRequestException($"Tool {toolName} отключен пользователем.");

                    m_Logger.PublicInfo($"execute -> call_tool -> {toolName}");

                    var cadView = m_ToolManager.CadView;
                    if (cadView != null)
                    {
                        cadView.Invoke((Action)(() =>
                        {
                            var cfg = m_ToolSettings.GetConfig(toolName);
                            if (cfg == null)
                                throw new InvalidOperationException("Cannot find tool settings");
                            if ((cfg.Destructive || !cfg.ReadOnly) && cfg.ApprovalScope == ToolApprovalScope.None)
                            {
                                var approvement = ToolApprovementDlg.Execute(toolName, cfg.Description);
                                if (approvement == ToolApprovementResult.Deny)
                                {
                                    m_Logger.PublicInfo($"Выполнение tool {toolName} отклонено.");
                                    throw new PreconditionFailedException($"Пользователь отклонил выполнение tool: {toolName}.");
                                }
                                else
                                {
                                    switch (approvement)
                                    {
                                        case ToolApprovementResult.AllowOnce:
                                            cfg.ApprovalScope = ToolApprovalScope.None;
                                            m_Logger.PublicInfo($"Разрешено однократное выполнение tool {toolName}.");
                                            break;
                                        case ToolApprovementResult.AllowForSession:
                                            cfg.ApprovalScope = ToolApprovalScope.Session;
                                            m_Logger.PublicInfo($"Разрешено выполнение tool {toolName} для текущей сессии.");
                                            break;
                                        case ToolApprovementResult.AllowPermanently:
                                            cfg.ApprovalScope |= ToolApprovalScope.Permanently;
                                            m_Logger.PublicInfo($"Разрешено постоянное выполнение tool {toolName}.");
                                            break;
                                    }
                                    m_ToolSettings.Save();
                                }
                            }
                        }));
                    }
                    else
                    {
                        throw new PreconditionFailedException(
                            "Не удалось получить активный видовой экран. " +
                            "Активируйте необходимую модель в структуре проекта и перейдите на требуемый видовой экран."
                        );
                    }

                    return BridgeResponse.OK(request.Id, m_ToolManager.CallTool(request.Params));
                default:
                    m_Logger.PublicWarning("execute -> unknown method");
                    return BridgeResponse.Fail(
                        request.Id,
                        ErrorCodes.BadRequest,
                        "Unknown method: " + request.Method,
                        null);
            }
        }

        private static string FormatLogValue(string value)
        {
            return value == null ? "null" : JsonConvert.ToString(value);
        }

        private static string PreparePayloadForLog(string payload)
        {
            if (payload == null)
                return "<null>";

            try
            {
                return JToken.Parse(payload).ToString(Formatting.Indented);
            }
            catch (JsonException)
            {
                return payload;
            }
        }

        private static object CreatePipeToolDefinition(ToolDefinition tool)
        {
            var domain = string.IsNullOrWhiteSpace(tool.Domain) ? "" : $"[{tool.Domain}] ";
            return new
            {
                name = tool.Name,
                domain = tool.Domain,
                description = domain + tool.Description,
                inputSchema = tool.InputSchema,
                annotations = tool.Annotations
            };
        }

        internal sealed class WindowsBridgeSecurityContext : IDisposable
        {
            private const string MutexNamePrefix = @"Local\Topomatic.Robur.ToolBridge.";

            private readonly SecurityIdentifier m_LogonSid;
            private readonly SecurityIdentifier m_UserSid;

            private Mutex m_Mutex;

            private WindowsBridgeSecurityContext(SecurityIdentifier logonSid, SecurityIdentifier userSid, Mutex mutex)
            {
                m_LogonSid = logonSid;
                m_UserSid = userSid;
                m_Mutex = mutex;
            }

            internal static WindowsBridgeSecurityContext Acquire()
            {
                using (var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query))
                {
                    var logonSid = GetLogonSid(identity);

                    var userSid = identity.User
                        ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");

                    var security = CreateMutexSecurity(logonSid, userSid);
                    var mutexName = MutexNamePrefix + HashSid(logonSid.Value);

                    bool createdNew;
                    Mutex mutex;
                    try
                    {
                        mutex = new Mutex(false, mutexName, out createdNew, security);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new InvalidOperationException("Another tool bridge instance has reserved the session mutex.", ex);
                    }

                    if (!createdNew)
                    {
                        mutex.Dispose();
                        throw new InvalidOperationException("Another tool bridge instance is already running in this Windows logon session.");
                    }

                    return new WindowsBridgeSecurityContext(logonSid, userSid, mutex);
                }
            }

            internal NamedPipeServerStream CreatePipe(string pipeName)
            {
                if (string.IsNullOrWhiteSpace(pipeName))
                    throw new ArgumentException("Pipe name must not be empty.", nameof(pipeName));

                var pipeSecurity = CreatePipeSecurity(m_LogonSid, m_UserSid);
                var descriptor = pipeSecurity.GetSecurityDescriptorBinaryForm();
                var descriptorHandle = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
                try
                {
                    var attributes = new NativeMethods.SecurityAttributes
                    {
                        Length = Marshal.SizeOf(typeof(NativeMethods.SecurityAttributes)),
                        SecurityDescriptor = descriptorHandle.AddrOfPinnedObject(),
                        InheritHandle = false
                    };
                    var fullPipeName = @"\\.\pipe\" + pipeName;
                    var handle = NativeMethods.CreateNamedPipe(
                        fullPipeName,
                        NativeMethods.PipeAccessDuplex
                            | NativeMethods.FileFlagOverlapped
                            | NativeMethods.FileFlagFirstPipeInstance,
                        NativeMethods.PipeTypeByte
                            | NativeMethods.PipeReadModeByte
                            | NativeMethods.PipeWait
                            | NativeMethods.PipeRejectRemoteClients,
                        1,
                        4096,
                        4096,
                        0,
                        ref attributes);

                    if (handle == null || handle.IsInvalid)
                    {
                        var error = Marshal.GetLastWin32Error();
                        handle?.Dispose();
                        throw new Win32Exception(error, "Could not create the secured tool bridge pipe.");
                    }

                    try
                    {
                        return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle);
                    }
                    catch
                    {
                        handle.Dispose();
                        throw;
                    }
                }
                finally
                {
                    descriptorHandle.Free();
                }
            }

            internal bool IsCurrentLogonClient(NamedPipeServerStream stream)
            {
                if (stream == null)
                    throw new ArgumentNullException(nameof(stream));

                SecurityIdentifier clientLogonSid = null;
                stream.RunAsClient(() =>
                {
                    using (var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query))
                        clientLogonSid = GetLogonSid(identity);
                });
                return m_LogonSid.Equals(clientLogonSid);
            }

            public void Dispose()
            {
                var mutex = m_Mutex;
                m_Mutex = null;
                mutex?.Dispose();
            }

            private static MutexSecurity CreateMutexSecurity(SecurityIdentifier logonSid, SecurityIdentifier userSid)
            {
                var security = new MutexSecurity();
                security.SetAccessRuleProtection(true, false);
                security.SetOwner(userSid);
                security.AddAccessRule(
                    new MutexAccessRule(
                        logonSid,
                        MutexRights.Modify | MutexRights.Synchronize,
                        AccessControlType.Allow
                    )
                );
                return security;
            }

            private static PipeSecurity CreatePipeSecurity(SecurityIdentifier logonSid, SecurityIdentifier userSid)
            {
                var security = new PipeSecurity();
                security.SetAccessRuleProtection(true, false);
                security.SetOwner(userSid);
                security.AddAccessRule(
                    new PipeAccessRule(
                        logonSid,
                        PipeAccessRights.ReadData
                            | PipeAccessRights.WriteData
                            | PipeAccessRights.ReadAttributes
                            | PipeAccessRights.WriteAttributes
                            | PipeAccessRights.Synchronize,
                        AccessControlType.Allow
                    )
                );
                return security;
            }

            private static SecurityIdentifier GetLogonSid(WindowsIdentity identity)
            {
                if (identity == null)
                    throw new ArgumentNullException(nameof(identity));

                var requiredLength = 0;
                NativeMethods.GetTokenInformation(
                    identity.Token,
                    NativeMethods.TokenInformationClass.TokenGroups,
                    IntPtr.Zero,
                    0,
                    out requiredLength);
                var error = Marshal.GetLastWin32Error();

                if (requiredLength <= 0 || error != NativeMethods.ErrorInsufficientBuffer)
                    throw new Win32Exception(error, "Could not determine the Windows token groups buffer size.");

                var buffer = Marshal.AllocHGlobal(requiredLength);
                try
                {
                    if (!NativeMethods.GetTokenInformation(
                        identity.Token,
                        NativeMethods.TokenInformationClass.TokenGroups,
                        buffer,
                        requiredLength,
                        out requiredLength))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the Windows token groups.");
                    }

                    var groupCount = Marshal.ReadInt32(buffer);
                    var groupOffset = Marshal.OffsetOf(
                        typeof(NativeMethods.TokenGroups),
                        nameof(NativeMethods.TokenGroups.FirstGroup)).ToInt32();

                    var groupSize = Marshal.SizeOf(typeof(NativeMethods.SidAndAttributes));
                    for (var index = 0; index < groupCount; index++)
                    {
                        var groupPointer = IntPtr.Add(buffer, groupOffset + index * groupSize);
                        var group = (NativeMethods.SidAndAttributes)Marshal.PtrToStructure(
                            groupPointer,
                            typeof(NativeMethods.SidAndAttributes));

                        if ((group.Attributes & NativeMethods.SeGroupLogonId) == NativeMethods.SeGroupLogonId)
                            return new SecurityIdentifier(group.Sid);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                throw new InvalidOperationException("The Windows logon SID is unavailable.");
            }

            private static string HashSid(string sid)
            {
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sid));
                    var builder = new StringBuilder(32);
                    for (var index = 0; index < 16; index++)
                    {
                        builder.Append(hash[index].ToString("x2"));
                    }
                    return builder.ToString();
                }
            }

            private static class NativeMethods
            {
                internal const uint PipeAccessDuplex = 0x00000003;
                internal const uint FileFlagFirstPipeInstance = 0x00080000;
                internal const uint FileFlagOverlapped = 0x40000000;
                internal const uint PipeTypeByte = 0x00000000;
                internal const uint PipeReadModeByte = 0x00000000;
                internal const uint PipeWait = 0x00000000;
                internal const uint PipeRejectRemoteClients = 0x00000008;
                internal const int ErrorInsufficientBuffer = 122;
                internal const uint SeGroupLogonId = 0xC0000000;

                internal enum TokenInformationClass
                {
                    TokenGroups = 2
                }

                [StructLayout(LayoutKind.Sequential)]
                internal struct SecurityAttributes
                {
                    internal int Length;
                    internal IntPtr SecurityDescriptor;
                    [MarshalAs(UnmanagedType.Bool)]
                    internal bool InheritHandle;
                }

                [StructLayout(LayoutKind.Sequential)]
                internal struct SidAndAttributes
                {
                    internal IntPtr Sid;
                    internal uint Attributes;
                }

                [StructLayout(LayoutKind.Sequential)]
                internal struct TokenGroups
                {
                    internal uint GroupCount;
                    internal SidAndAttributes FirstGroup;
                }

                [DllImport("advapi32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool GetTokenInformation(
                    IntPtr tokenHandle,
                    TokenInformationClass tokenInformationClass,
                    IntPtr tokenInformation,
                    int tokenInformationLength,
                    out int returnLength);

                [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
                internal static extern SafePipeHandle CreateNamedPipe(
                    string name,
                    uint openMode,
                    uint pipeMode,
                    uint maxInstances,
                    uint outBufferSize,
                    uint inBufferSize,
                    uint defaultTimeout,
                    ref SecurityAttributes securityAttributes);
            }
        }
    }
}
