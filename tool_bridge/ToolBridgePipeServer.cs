using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Pipes;
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
                        m_Logger.Log("AcceptLoop error: " + ex);
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
                        BridgeResponse response;
                        try
                        {
                            var request = JsonConvert.DeserializeObject<BridgeRequest>(line);
                            response = ProcessRequest(request);
                        }
                        catch (Exception ex)
                        {
                            response = BridgeResponse.Fail(
                                null,
                                "internal_error",
                                ex.Message,
                                ex.ToString());
                        }
                        var json = JsonConvert.SerializeObject(response);
                        writer.WriteLine(json);
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

        private BridgeResponse ProcessRequest(BridgeRequest request)
        {
            if (request == null)
                return BridgeResponse.Fail(null, "bad_request", "Request body is empty or invalid JSON.", null);
            switch ((request.Method ?? string.Empty).Trim())
            {
                case "ping":
                    m_Logger.Log("execute -> ping");
                    return BridgeResponse.OK(request.Id, new
                    {
                        protocolVersion = "1.0",
                        cadProcess = "demo-cad",
                        serverTimeUtc = DateTime.UtcNow.ToString("O")
                    });
                case "list_tools":
                    m_Logger.Log("execute -> list_tools");
                    return BridgeResponse.OK(request.Id, new
                    {
                        tools = m_ToolManager.GetTools()
                    });
                case "call_tool":
                    string toolName;
                    if (request.Params != null && request.Params.TryGetValue("tool_name", out var toolNameObj))
                        toolName = Convert.ToString(toolNameObj);
                    else
                        toolName = "unexpected tool name";
                    m_Logger.Log($"execute -> call_tool -> {toolName}");
                    return BridgeResponse.OK(request.Id, m_ToolManager.CallTool(request.Params));
                default:
                    m_Logger.Log("execute -> unexpected tool");
                    return BridgeResponse.Fail(request.Id, "unknown_method", "Unknown method: " + request.Method, null);
            }
        }
    }
}
