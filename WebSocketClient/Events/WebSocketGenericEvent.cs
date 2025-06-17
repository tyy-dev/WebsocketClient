namespace WebSocketClient.Events {
    /// <summary>
    /// Represents a generic WebSocket event wrapper.
    /// Used to handle all events in a non-type-specific manner.
    /// When wrapping another event, <see cref="innerEvent"/> holds the original event instance;
    /// otherwise, it is null.
    /// </summary>
    public class WebSocketGenericEvent : WebSocketEvent {
        public string? innerEventEventId;

        /// <summary>
        /// Identifier for the generic event type.
        /// </summary>
        public override string eventId => "GenericEvent";
    }
}
