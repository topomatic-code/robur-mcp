using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topomatic.ToolBridge.Dialogs;
using Topomatic.ToolBridge.Dialogs.Results;
using Topomatic.ToolBridge.Exceptions;
using Topomatic.ToolBridge.Settings;

namespace Topomatic.ToolBridge
{
    internal sealed class ToolBridgePipeServer : IDisposable
    {
        private readonly string m_PipeName;
        private readonly ToolManager m_ToolManager;
        private readonly ToolBridgeLogger m_Logger;
        private readonly CancellationTokenSource m_Cts;
        private readonly object m_SyncRoot;

        private Task m_AcceptLoop;
        private NamedPipeServerStream m_ActiveStream;
        private WindowsBridgeSecurityContext m_SecurityContext;
        private bool m_Started;
        private volatile bool m_Locked;
        private bool m_Disposed;

        public ToolBridgePipeServer(string pipeName, ToolManager toolManager, ToolBridgeLogger logger)
        {
            m_PipeName = pipeName;
            m_ToolManager = toolManager;
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
                stream = securityContext.CreatePipe(m_PipeName);
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
                            .Where(t => ToolSettings.GetConfig(t.Name)?.Enabled ?? false)
                            .Select(CreatePipeToolDefinition)
                            .ToArray()
                    });
                case "call_tool":
                    var toolName = "<missing>";
                    if (request.Params != null && request.Params.TryGetValue("tool_name", out var toolNameObj))
                        toolName = Convert.ToString(toolNameObj);

                    if ((!ToolSettings.GetConfig(toolName)?.Enabled) ?? true)
                        throw new BadRequestException($"Tool {toolName} отключен пользователем.");

                    m_Logger.PublicInfo($"execute -> call_tool -> {toolName}");

                    var cadView = m_ToolManager.CadView;
                    if (cadView != null)
                    {
                        cadView.Invoke((Action)(() =>
                        {
                            var cfg = ToolSettings.GetConfig(toolName);
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
                                    ToolSettings.Save();
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
    }
}
