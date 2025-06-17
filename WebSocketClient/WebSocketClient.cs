using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Transport;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using WebSocketClient.Events;

namespace WebSocketClient {
    /// <summary>
    /// Represents a WebSocket client that supports both standard WebSocket connections and Socket.IO protocol connections. 
    /// Usage:
    /// - Instantiate with the WebSocket URL.
    /// - Optionally provide a WebSocketMessageHandler instance to process incoming events.
    /// - Call ConnectAsync() to establish the connection.
    /// - Use Emit() to emit data / Socket.IO Events
    /// - Use CloseAsync() to close the connection gracefully.
    /// Note:
    /// - If connecting to a Socket.IO server, set isSocketIO to true when creating the client.
    /// - The message handler is invoked for incoming raw messages or connection events.
    /// 
    /// </summary>
    public class WebSocketClient(string url, WebSocketMessageHandler? messageHandler = null, bool isSocketIO = false, SocketIOOptions? socketIOOptions = null) {
        /// <summary>
        /// The underlying ClientWebSocket instance used for standard WebSocket connections.
        /// </summary>
        private readonly ClientWebSocket client = new();

        /// <summary>
        /// The Uri of the WebSocket
        /// </summary>
        private readonly Uri uri = new(url);

        /// <summary>
        /// The Socket.IO client instance, initialized if <paramref name="isSocketIO"/> is true.
        /// </summary>
        private SocketIOClient.SocketIO? socketIOClient = null;

        /// <summary>
        /// Gets or sets the <see cref="WebSocketMessageHandler"/> responsible for processing incoming messages and events.
        /// </summary>
        public WebSocketMessageHandler? messageHandler { get; set; } = messageHandler;

        /// <summary>
        /// Asynchronously connects to the configured WebSocket or Socket.IO server.
        /// Establishes the connection, sets up message/event handlers, and begins receiving messages if applicable.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous connect operation.</returns>
        public async Task ConnectAsync() {
            #region Socket.IO
            if ((this.uri.AbsoluteUri.Contains("socket.io") || this.uri.AbsoluteUri.Contains("engine.io")) && !isSocketIO)
                Console.WriteLine(@"Warning: The URL suggests a Socket.IO server, but 'isSocketIO' is set to false.
                                         You might want to enable Socket.IO mode to handle this connection properly.");

            if (isSocketIO) {
                SocketIOOptions socketIoOptions = socketIOOptions ?? new SocketIOOptions() {
                    EIO = SocketIO.Core.EngineIO.V4,
                };
                this.socketIOClient = new(this.uri, options: socketIOOptions);
                this.socketIOClient.OnError += (sender, e) => Console.WriteLine(e);
                this.socketIOClient.OnAny((eventName, args) => {
                    string rawArgsJson = args.ToString();
                    JArray argsArray = JArray.Parse(rawArgsJson);

                    // Insert the eventName at the beginning
                    argsArray.Insert(0, eventName);

                    string rawData = argsArray.ToString(Newtonsoft.Json.Formatting.None);
                    this.messageHandler?.ProcessRawMessage(rawData);
                });

                this.socketIOClient.OnConnected += (_, _) => this.messageHandler?.HandleEvent(new WebSocketEventConnected());
                this.socketIOClient.OnDisconnected += async (_, reason) => await this.CloseAsync(null, reason);
                await this.socketIOClient.ConnectAsync(cancellationToken: CancellationToken.None);
                return;
            }
            #endregion
            #region Client Websocket
            await this.client.ConnectAsync(this.uri, CancellationToken.None);
            if (this.client.State == WebSocketState.Open)
                this.messageHandler?.HandleEvent(new WebSocketEventConnected());
            _ = this.ReceiveClientLoop();
            #endregion
        }

        /// <summary>
        /// Sets the Socket.IO transport protocol (e.g., Polling, WebSocket).
        /// Must be called before <see cref="ConnectAsync"/>. Optionally triggers a reconnect if already connected.
        /// </summary>
        /// <param name="transport">The <see cref="TransportProtocol"/> to use for the connection.</param>
        /// <param name="reconnect">
        /// If true and the client is currently connected, the connection will be closed and re-established using the new transport.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this method is called when the client is not operating in Socket.IO mode.
        /// </exception>
        public async Task SetTransport(TransportProtocol transport, bool reconnect = false) {
            if (!isSocketIO)
                throw new InvalidOperationException("Transport mode can only be set when using Socket.IO. Ensure 'isSocketIO' is true during initialization.");

            socketIOOptions ??= new() {
                    EIO = SocketIO.Core.EngineIO.V4,
                };

            socketIOOptions.Transport = transport;

            if (reconnect) {
                if (socketIOClient?.Connected == true) await this.CloseAsync(WebSocketCloseStatus.NormalClosure, "Switching transport");
                await this.ConnectAsync();
            }
        }

        /// <summary>
        /// Switches the transport protocol dynamically by setting it and reconnecting immediately.
        /// This is a convenience wrapper around <see cref="SetTransport"/> with <c>reconnect: true</c>.
        /// </summary>
        /// <param name="transport">The desired <see cref="TransportProtocol"/> to switch to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not in Socket.IO mode. This method is only applicable to Socket.IO connections.
        /// </exception>
        public async Task SwitchTransportAsync(TransportProtocol transport) {
            if (!isSocketIO)
                throw new InvalidOperationException("Transport switching is only supported for Socket.IO connections.");

            await this.SetTransport(transport, reconnect: true);
        }

        /// <summary>
        /// Gets the currently active <see cref="TransportProtocol"/> used by the underlying Socket.IO connection.
        /// </summary>
        /// <returns>
        /// The <see cref="TransportProtocol"/> currently in use (e.g., <c>Polling</c> or <c>WebSocket</c>),
        /// or <c>null</c> if the client is not in Socket.IO mode or the Socket.IO client has not been initialized.
        /// </returns>
        public TransportProtocol? GetActiveTransport() => this.socketIOClient?.Options.Transport;

        /// <summary>
        /// Closes the WebSocket or Socket.IO connection gracefully.
        /// Sends close frame with optional status and description, and triggers close event handlers.
        /// </summary>
        /// <param name="closeStatus">Optional WebSocket close status code. Defaults to NormalClosure.</param>
        /// <param name="closeDescription">Optional textual description for closing. Defaults to "io client disconnect" for Socket.IO</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous close operation.</returns>
        public async Task CloseAsync(WebSocketCloseStatus? closeStatus = null, string? closeDescription = null) {
            WebSocketEventCloseConnection closeEvent = new() {
                status = closeStatus ?? WebSocketCloseStatus.NormalClosure,
                description = closeDescription
            };

            if (this.socketIOClient?.Connected == true) {
                closeEvent.description ??= "io client disconnect";

                await this.socketIOClient.DisconnectAsync();

                this.messageHandler?.HandleEvent(closeEvent);
            }
            else {
                if (this.client.State == WebSocketState.Open)
                    await this.client.CloseAsync(closeEvent.status, closeDescription, CancellationToken.None);

                this.messageHandler?.HandleEvent(closeEvent);
            }
        }

        /// <summary>
        /// Internal loop method to receive messages from a standard WebSocket connection. (NOT Socket.IO)
        /// Continuously reads messages until the connection is closed or an error occurs.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous receive loop.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is in Socket.IO mode. This method is only applicable to standard WebSocket connections.
        /// </exception>
        public async Task ReceiveClientLoop() {
            if (isSocketIO)
                throw new InvalidOperationException("ReceiveClientLoop is only applicable for standard WebSocket connections. It cannot be used when 'isSocketIO' is true.");

            try {
                while (this.client.State == WebSocketState.Open) {
                    await using MemoryStream ms = new();
                    WebSocketReceiveResult result;
                    do {
                        ArraySegment<byte> messageBuffer = WebSocket.CreateClientBuffer(1024, 16);
                        result = await this.client.ReceiveAsync(messageBuffer, CancellationToken.None);
                        ms.Write(messageBuffer.Array!, messageBuffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Text) {
                        string rawData = Encoding.UTF8.GetString(ms.ToArray());
                        this.messageHandler?.ProcessRawMessage(rawData);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close) {
                        await this.CloseAsync(this.client.CloseStatus, this.client.CloseStatusDescription);
                        break;
                    }
                }
            }
            catch (Exception e) {
                Console.WriteLine($"WebSocket received error: {e.Message}");
            }
        }

         /// <summary>
         /// Emits a WebSocket event to the server by sending its event ID followed by
         /// the event data extracted from properties decorated with <see cref="EventDataIndexAttribute"/>.
         /// </summary>
         /// <param name="webSocketEvent">The WebSocket event instance to emit.</param>
         /// <returns>A <see cref="Task"/> representing the asynchronous send operation.</returns>
         /// <exception cref="ArgumentNullException">Thrown if <paramref name="webSocketEvent"/> is null.</exception>
        public async Task Emit(WebSocketEvent webSocketEvent) {
            ArgumentNullException.ThrowIfNull(webSocketEvent);

            object?[] emitArgs = [webSocketEvent.eventId, .. webSocketEvent.GetEmitData()];

            await this.Emit(emitArgs);
        }

        /// <summary>
        /// Emuts data or an Socket.IO event to the server.
        /// The first argument is treated as the event name, and subsequent arguments as event data.
        /// <see cref="WebSocketMessageHandler.ParseRawData(string)"/> for more information regarding structure.
        /// </summary>
        /// <param name="data">One or more objects representing the event name followed by its arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous send operation.</returns>
        /// <exception cref="ArgumentException">Thrown if no event name is provided.</exception>
        public async Task Emit(params object?[] data) {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Emit requires at least an event name.");

            if (this.socketIOClient == null) {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
                await this.client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else {
                string eventName = data[0]?.ToString() ?? throw new ArgumentException("Event name cannot be null.");
                object?[] args = [.. data.Skip(1)];
                if (args.Length == 0)
                    await this.socketIOClient.EmitAsync(eventName);
                else if (args.Length == 1)
                    await this.socketIOClient.EmitAsync(eventName, args[0]);
                else
                    await this.socketIOClient.EmitAsync(eventName, args);
            }
        }
    }
}
