using static TCPServerAndClient.TcpService;

namespace TCPServerAndClient
{
    public interface ITcpService
    {
        /// <summary>
        /// Gets the port number on which the server is listening
        /// </summary>
        int ServerPort { get; }

        /// <summary>
        /// Gets the last message received by the server
        /// </summary>
        string ServerReceivedMessage { get; }

        /// <summary>
        /// Gets the last response message sent by the server
        /// </summary>
        string ServerResponseMessage { get; }

        /// <summary>
        /// Gets the list of responses from all server instances
        /// </summary>
        List<TcpService.ServerResponse> ServerResponses { get; }

        /// <summary>
        /// Event that is triggered when a message is received and requires processing
        /// </summary>
        event Func<string, Task<string>>? MessageReceived;

        /// <summary>
        /// Starts the TCP server on an available port within the configured range
        /// </summary>
        /// <returns>True if server started successfully, false otherwise</returns>
        bool StartServer();

        /// <summary>
        /// Runs the server and begins accepting client connections
        /// </summary>
        /// <param name="token">Cancellation token to stop the server</param>
        Task RunServerAsync(CancellationToken token);

        /// <summary>
        /// Sends a message to multiple TCP servers and collects their responses.
        /// </summary>
        /// <param name="message">The message to send to the servers.</param>
        /// <param name="portList">Optional list of specific ports to target. If null, uses the default port range.</param>
        /// <param name="connectionTimeout">Timeout for establishing connection. Default is 100ms.</param>
        /// <param name="responseTimeout">Timeout for waiting for server response. Default is 8000ms.</param>
        /// <returns>A list of ServerResponse objects containing successful responses.</returns>
        Task<List<ServerResponse>> ClientSendMessageAsync(
            string message,
            IEnumerable<int>? portList = null,
            TimeSpan? connectionTimeout = null,
            TimeSpan? responseTimeout = null);

        /// <summary>
        /// Sends a message to a specific TCP server port and collects the response.
        /// </summary>
        /// <param name="message">The message to send to the server.</param>
        /// <param name="targetPort">The specific port to target.</param>
        /// <param name="connectionTimeout">Timeout for establishing connection. Default is 100ms.</param>
        /// <param name="responseTimeout">Timeout for waiting for server response. Default is 8000ms.</param>
        /// <returns>A ServerResponse object if successful, null if failed.</returns>
        Task<ServerResponse?> ClientSendMessageToPortAsync(
            string message,
            int targetPort,
            TimeSpan? connectionTimeout = null,
            TimeSpan? responseTimeout = null);
    }
}
