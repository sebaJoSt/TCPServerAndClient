namespace TCPServer
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
        /// Sends a message to all available TCP servers and collects their responses
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>List of responses from available servers</returns>
        Task<List<TcpService.ServerResponse>> ClientSendMessageAsync(string message);

        /// <summary>
        /// Sends a message to a specific TCP server port and returns its response
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="targetPort">The specific port to send the message to</param>
        /// <returns>The server response if successful, null if failed or no response</returns>
        Task<TcpService.ServerResponse?> ClientSendMessageToPortAsync(string message, int targetPort);
    }
}
