using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TCPServerAndClient
{
    public class TcpService : ITcpService
    {
        private const int PortStart = 55500;
        private const int PortEnd = 55800;

        private const int BufferSize = 1024;

        private readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromMilliseconds(8000);

        private Socket? listenSocket = null;
        public int ServerPort { get; private set; }

        public string ServerReceivedMessage { get; set; } = string.Empty;
        public string ServerResponseMessage { get; set; } = string.Empty;

        public class ServerResponse(int port, string message)
        {
            public int Port { get; set; } = port;
            public string Message { get; set; } = message;
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

        public List<ServerResponse> ServerResponses { get; private set; } = [];

        // Define a delegate for received messages with a response
        public event Func<string, Task<string>>? MessageReceived;

        public bool StartServer()
        {
            bool isConnected = false;

            for (int port = PortStart; port <= PortEnd; port++)
            {
                listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    ServerPort = port;
                    isConnected = true;
                    Debug.WriteLine($"Server listening on port {port}");
                    isConnected = true;
                    break;
                }
                catch (SocketException)
                {
                    Debug.WriteLine($"Failed to bind to port {port}, trying next port...");
                    listenSocket.Close();
                }
            }

            if (!isConnected)
            {
                Debug.WriteLine("Failed to bind to any port in the specified range.");
            }

            return isConnected;
        }

        public async Task RunServerAsync(CancellationToken token)
        {
            try
            {
                if (listenSocket != null)
                {
                    listenSocket.Listen(120);

                    // Accept incoming connections
                    while (!token.IsCancellationRequested)
                    {
                        var socket = await listenSocket.AcceptAsync(token);
                        _ = Task.Run(() => ProcessClient(socket), token);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Server encountered an error: {ex.Message}");
            }
            finally
            {
                listenSocket?.Close();
                Debug.WriteLine("Server shutting down.");
            }
        }

        private async Task ProcessClient(Socket socket)
        {
            Debug.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            var stream = new NetworkStream(socket);
            var reader = PipeReader.Create(stream);

            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                    {
                        var message = Encoding.UTF8.GetString(line.ToArray());
                        string response = await ProcessMessageAsync(message);

                        if (!string.IsNullOrEmpty(response))
                        {
                            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                            await stream.WriteAsync(responseBytes);
                        }
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                await reader.CompleteAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{socket.RemoteEndPoint}] Error: {ex.Message}");
            }
            finally
            {
                socket.Close();
                Debug.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
            }
        }

        private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            var position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        private async Task<string> ProcessMessageAsync(string message)
        {
            Debug.WriteLine($"Received: {message}");

            ServerReceivedMessage = message;

            if (MessageReceived != null)
            {
                try
                {
                    return await MessageReceived.Invoke(message);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing message: {ex.Message}");
                    return "Error processing message";
                }
            }

            Debug.WriteLine($"Echoing: {message}");
            ServerResponseMessage = message;
            return message;
        }

        public async Task<List<ServerResponse>> ClientSendMessageAsync(
               string message,
               IEnumerable<int>? portList = null,
               TimeSpan? connectionTimeout = null,
               TimeSpan? responseTimeout = null)
        {
            var actualConnectionTimeout = connectionTimeout ?? DefaultConnectionTimeout;
            var actualResponseTimeout = responseTimeout ?? DefaultResponseTimeout;

            Debug.WriteLine(portList != null
                ? $"Try to send message '{message}' to specified ports: {string.Join(", ", portList)}"
                : $"Try to send message '{message}' to Ports {PortStart} - {PortEnd} ({PortEnd - PortStart + 1})");

            if (string.IsNullOrWhiteSpace(message))
            {
                return [];
            }

            ServerResponses.Clear();

            var portsToTry = (portList ?? Enumerable.Range(PortStart, PortEnd - PortStart + 1))
                .Where(p => p != ServerPort)
                .ToList();

            var connectionTasks = portsToTry.Select(async port =>
            {
                using var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    var connectTask = clientSocket.ConnectAsync(IPAddress.Loopback, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(actualConnectionTimeout)) != connectTask)
                    {
                        return null;
                    }

                    await connectTask;

                    if (!clientSocket.Connected)
                    {
                        return null;
                    }

                    clientSocket.NoDelay = true;
                    clientSocket.SendBufferSize = BufferSize;
                    clientSocket.ReceiveBufferSize = BufferSize;

                    using var stream = new NetworkStream(clientSocket);

                    byte[] messageBytes = Encoding.UTF8.GetBytes(message + "\n");
                    await stream.WriteAsync(messageBytes);

                    var buffer = new byte[BufferSize];
                    using var cts = new CancellationTokenSource(actualResponseTimeout);
                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, cts.Token);
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\n');

                        return new ServerResponse(port, response);
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
                {
                    return null;
                }
                finally
                {
                    if (clientSocket.Connected)
                    {
                        clientSocket.Shutdown(SocketShutdown.Both);
                    }
                }
            }).ToList();

            var results = await Task.WhenAll(connectionTasks);
            var validResponses = results.Where(r => r != null).ToList();

            ServerResponses.AddRange(validResponses!);

            Debug.WriteLine($"Received {validResponses.Count} responses from {results.Length} connection attempts");
            foreach (var response in validResponses)
            {
                Debug.WriteLine($"Port {response!.Port}: {response.Message}");
            }

            return ServerResponses;
        }

        public async Task<ServerResponse?> ClientSendMessageToPortAsync(
            string message,
            int targetPort,
            TimeSpan? connectionTimeout = null,
            TimeSpan? responseTimeout = null)
        {
            var actualConnectionTimeout = connectionTimeout ?? DefaultConnectionTimeout;
            var actualResponseTimeout = responseTimeout ?? DefaultResponseTimeout;

            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            Debug.WriteLine($"Trying to send message '{message}' to Port {targetPort}");

            using var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                var connectTask = clientSocket.ConnectAsync(IPAddress.Loopback, targetPort);
                if (await Task.WhenAny(connectTask, Task.Delay(actualConnectionTimeout)) != connectTask)
                {
                    Debug.WriteLine($"Connection timeout to port {targetPort}");
                    return null;
                }

                await connectTask;

                if (!clientSocket.Connected)
                {
                    Debug.WriteLine($"Failed to connect to port {targetPort}");
                    return null;
                }

                clientSocket.NoDelay = true;
                clientSocket.SendBufferSize = BufferSize;
                clientSocket.ReceiveBufferSize = BufferSize;

                using var stream = new NetworkStream(clientSocket);

                byte[] messageBytes = Encoding.UTF8.GetBytes(message + "\n");
                await stream.WriteAsync(messageBytes);

                var buffer = new byte[BufferSize];
                using var cts = new CancellationTokenSource(actualResponseTimeout);
                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, cts.Token);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\n');

                    var serverResponse = new ServerResponse(targetPort, response);
                    Debug.WriteLine($"Received response from port {targetPort}: {response}");
                    return serverResponse;
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"Response timeout from port {targetPort}");
                    return null;
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                Debug.WriteLine($"Error connecting to port {targetPort}: {ex.Message}");
                return null;
            }
            finally
            {
                if (clientSocket.Connected)
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                }
            }
        }
    }
}
