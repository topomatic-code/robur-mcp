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
            if (m_AcceptLoop != null && !m_AcceptLoop.IsCompleted)
                return;
            m_AcceptLoop = Task.Run(() => AcceptLoop(m_Cts.Token));
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;
            m_Cts.Cancel();
            AbortActiveStream();
            try
            {
                m_AcceptLoop?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                m_AcceptLoop = null;
            }
            m_Cts.Dispose();
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

        private void AcceptLoop(CancellationToken token)
        {
            while (!IsShuttingDown(token))
            {
                NamedPipeServerStream stream = null;
                try
                {
                    stream = new NamedPipeServerStream(
                        m_PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous
                    );
                    SetActiveStream(stream);
                    stream.WaitForConnection();
                    if (IsShuttingDown(token))
                        return;
                    HandleClient(stream, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException) when (IsShuttingDown(token))
                {
                    return;
                }
                catch (IOException) when (IsShuttingDown(token))
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (!IsShuttingDown(token))
                        m_Logger.PublicError("AcceptLoop error: " + ex);
                    Thread.Sleep(500);
                }
                finally
                {
                    ClearActiveStream(stream);
                    stream?.Dispose();
                }
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
                    while (stream.IsConnected && !token.IsCancellationRequested)
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                            return;

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
                            m_Logger.PublicWarning("Bad request: invalid JSON. " + ex);
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
                                m_Logger.PublicWarning($"Expected error [{ex.Code}]: {ex}");
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
            m_Logger.PublicError($"Internal error [{traceId}]: {exception}");
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
                m_Logger.PublicWarning($"Invalid error details omitted [{exception.Code}]: {detailsException}");
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
                        protocolVersion = "1.0",
                        cadProcess = "demo-cad",
                        serverTimeUtc = DateTime.UtcNow.ToString("O")
                    });
                case "list_tools":
                    m_Logger.PublicInfo("execute -> list_tools");
                    return BridgeResponse.OK(request.Id, new
                    {
                        tools = m_ToolManager.GetTools()
                            .Select(CreatePipeToolDefinition)
                            .ToArray()
                    });
                case "call_tool":
                    var toolName = "<missing>";
                    if (request.Params != null && request.Params.TryGetValue("tool_name", out var toolNameObj))
                        toolName = Convert.ToString(toolNameObj);
                    m_Logger.PublicInfo($"execute -> call_tool -> {toolName}");
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
