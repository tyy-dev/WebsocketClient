using System.Reflection;
using Newtonsoft.Json.Linq;

namespace WebSocketClient.Events {
    /// <summary>
    /// Base class representing a generic WebSocket event.
    /// Derived event types must specify an eventId and may decorate properties to populate with EventDataIndexAttribute
    /// to map raw data elements to strongly typed properties.
    /// </summary>
    public abstract class WebSocketEvent {
        /// <summary>
        /// Unique string identifier for the event type, 
        /// also the first "element" of the true raw data from the websocket,["Message", "text here"],
        /// event id would be message, text here would be index 0 of rawData.
        /// </summary>
        public abstract string eventId {
            get;
        }
        /// <summary>
        /// Raw event data elements parsed from incoming WebSocket message, not including the eventId.
        /// </summary>
        public IEnumerable<object> rawData = [];

        /// <summary>
        /// Maps elements from rawData to properties marked with EventDataIndexAttribute.
        /// Performs type checking and sets property values accordingly.
        /// </summary>
        internal void PopulateAttributedProperties() {
            IEnumerable<(PropertyInfo prop, EventDataIndexAttribute attr)> attributedProps = this.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.GetCustomAttribute<EventDataIndexAttribute>() is not null)
                .Select(prop => (prop, attr: prop.GetCustomAttribute<EventDataIndexAttribute>()!));

            foreach ((PropertyInfo prop, EventDataIndexAttribute attr) in attributedProps) {
                int dataIndex = attr!.index;
                if (dataIndex < 0 || dataIndex > this.rawData.Count())
                    continue;

                try {
                    object? data = this.rawData.ElementAt(dataIndex);

                    if (attr.deserializeJson && data is Newtonsoft.Json.Linq.JToken jToken) {
                        object? converted = jToken.ToObject(prop.PropertyType);
                        if (converted != null)
                            prop.SetValue(this, converted);
                        continue;
                    }

                    if (prop.PropertyType.IsAssignableFrom(data.GetType())) // Mostly for sanity
                        prop.SetValue(this, data);
                }
                catch (Exception e) {
                    Console.WriteLine($"Error with {this.eventId}[{dataIndex}]: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Extracts the values of properties decorated with EventDataIndexAttribute ordered by index,
        /// to be used as data elements when emitting this event.
        /// </summary>
        public IEnumerable<object?> GetEmitData() => this.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(prop => (prop, attr: prop.GetCustomAttribute<EventDataIndexAttribute>()))
                .Where(x => x.attr != null)
                .OrderBy(x => x.attr!.index)
                .Select(x => {
                    object? value = x.prop.GetValue(this);
                    if (x.attr!.deserializeJson && value != null)
                        return JToken.FromObject(value);
                    return value;
                });
    }
}
