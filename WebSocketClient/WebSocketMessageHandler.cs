using Newtonsoft.Json;
using WebSocketClient.Events;

namespace WebSocketClient {
    /// <summary>
    /// Handles incoming webSocket data by parsing into strongly-typed events, subscribing to specific events and invoking them.
    /// </summary>
    public class WebSocketMessageHandler {
        private readonly WebSocketEventManager eventManager = new();

        /// <summary>
        /// Registers an event handler for all incoming websocket events.
        /// This handler will be invoked for every event regardless of its specific type.
        /// </summary>
        /// <param name="handler">Action to perform when any event occurs.</param>
        public void On(Action<WebSocketGenericEvent> handler) => this.eventManager.RegisterOn(handler);

        /// <summary>
        /// Registers an event handler for a specific WebSocketEvent type.
        /// </summary>
        /// <typeparam name="T">The type of WebSocketEvent to listen for.</typeparam>
        /// <param name="handler">Action to perform when the event occurs.</param>
        public void On<T>(Action<T> handler) where T : WebSocketEvent, new() => this.eventManager.RegisterOn(handler);

        /// <summary>
        /// Handles a strongly typed WebSocketEvent by invoking registered event handlers.
        /// </summary>
        /// <param name="websocketEvent">The event to handle.</param>
        internal void HandleEvent(WebSocketEvent websocketEvent) => this.eventManager?.Invoke(websocketEvent);

        /// <summary>
        /// Parses raw data received into a WebSocketEvent instance.
        /// /// Assumes the raw data is a JSON array where:
        /// - The first element represents the event identifier (event ID),
        /// - Subsequent elements represent the event data payload.
        /// Populates the event data properties decorated with EventDataIndexAttribute.
        /// </summary>
        /// <param name="rawData">The raw string from the websocket.</param>
        /// <returns>A populated WebSocketEvent instance or null if parsing fails.</returns>
        internal WebSocketEvent? ParseRawData(string rawData) {
            try {
                object[]? eventData = JsonConvert.DeserializeObject<object[]>(rawData);
                if (eventData == null || eventData.Length < 1)
                    return null;

                string? eventId = eventData.ElementAtOrDefault(0)?.ToString();
                if (eventId == null)
                    return null;

                if (!this.eventManager.TryGetEventInstance(eventId, out WebSocketEvent? websocketEvent))
                    websocketEvent = new WebSocketGenericEvent() {
                        innerEventEventId = eventId
                    };

                websocketEvent!.rawData = eventData.Skip(1);
                websocketEvent!.PopulateAttributedProperties();
                return websocketEvent;
            }
            catch (Exception e) {
                Console.WriteLine($"Error handling raw data `{rawData}`: {e.Message}");
            }
            return null;
        }


        /// <summary>
        /// Processes a raw JSON message string by parsing it into a WebSocketEvent and then handling the event if valid.
        /// </summary>
        /// <param name="rawData">The raw JSON message string received from the WebSocket.</param>
        /// <returns>True if a valid WebSocketEvent was handled; otherwise, false.</returns>
        public void ProcessRawMessage(string rawData) {
            WebSocketEvent? websocketEvent = this.ParseRawData(rawData);
            if (websocketEvent != null)
                this.HandleEvent(websocketEvent);
        }
    }
}
