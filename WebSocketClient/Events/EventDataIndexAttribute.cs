namespace WebSocketClient.Events {
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class EventDataIndexAttribute(int index, bool deserializeJson = false) : Attribute {
        /// <summary>
        /// The index position of the data element in the event's data array.
        /// </summary>
        public int index {
            get; set;
        } = index;

        /// <summary>
        /// A boolean indicating whether the data at the specified index should be deserialized from JSON. (or serialized if emitted)
        /// </summary>
        public bool deserializeJson {
            get; set;
        } = deserializeJson;
    }
}
