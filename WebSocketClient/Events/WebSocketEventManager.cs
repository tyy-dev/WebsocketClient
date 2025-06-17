namespace WebSocketClient.Events {

    /// <summary>
    /// Manages registering to specific WebSocket events and invoking/dispatching events to matching handlers
    /// </summary>
    public class WebSocketEventManager {
        /// <summary>
        /// Internal collection of registered <see cref="WebSocketEvent"/> instances, 
        /// used to map event IDs to event types.
        /// </summary>
        private readonly List<WebSocketEvent> registeredEvents = [];

        /// <summary>
        /// Event triggered whenever any <see cref="WebSocketEvent"/> occurs.
        /// Event is invoked regardless of specific type, however the handler is only invoked if the specific type is of GenericEvent or matches WebSocketEvent
        /// </summary>
        public event EventHandler<WebSocketEvent>? eventHandler;

        /// <summary>
        /// Registers a handler to be invoked for all incoming WebSocket events,
        /// wrapping the event into a generic event type <see cref="WebSocketGenericEvent"/>.
        /// </summary>
        /// <param name="handler">An action to perform on every incoming generic event.</param>
        public void RegisterOn(Action<WebSocketGenericEvent> handler) {
            this.registeredEvents.Add(new WebSocketGenericEvent());
            this.eventHandler += (_, ev) => {
                string eventId = ev.eventId;

                // If ev is already a generic event with a meaningful innerEventEventId, use that instead
                if (ev is WebSocketGenericEvent evt)
                    eventId = evt.innerEventEventId;

                WebSocketGenericEvent genericEvent = new() {
                    innerEventEventId = eventId,
                    rawData = ev.rawData
                };

                handler(genericEvent);
            };
        }

        /// <summary>
        /// Registers a handler for events of type T.
        /// If the event type is not yet registered, creates and stores an instance.
        /// The handler will be invoked when an event of type T is raised.
        /// </summary>
        /// <typeparam name="T">The WebSocketEvent-derived type to register the handler for.</typeparam>
        /// <param name="handler">The action to invoke when the event occurs.</param>
        public void RegisterOn<T>(Action<T> handler) where T : WebSocketEvent, new() {
            T eventInstance = new();
            string eventId = eventInstance.eventId;

            if (!this.registeredEvents.Any(e => e.eventId == eventId))
                this.registeredEvents.Add(eventInstance);

            this.eventHandler += (_, ev) => {
                if (ev is T typedEvent)
                    handler(typedEvent);
            };
        }

        /// <summary>
        /// Attempts to retrieve a registered event instance by its event ID.
        /// </summary>
        /// <param name="eventId">The string identifier of the event.</param>
        /// <param name="eventInstance">Outputs the found event instance or null.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        public bool TryGetEventInstance(string eventId, out WebSocketEvent? eventInstance) {
            eventInstance = this.registeredEvents.FirstOrDefault(e => e.eventId == eventId);
            return eventInstance != null;
        }

        /// <summary>
        /// Invokes all registered handlers with the provided websocket event instance.
        /// </summary>
        /// <param name="websocketEvent">The event instance to invoke handlers for.</param>
        public void Invoke(WebSocketEvent websocketEvent) => this.eventHandler?.Invoke(this, websocketEvent);
    }
}
