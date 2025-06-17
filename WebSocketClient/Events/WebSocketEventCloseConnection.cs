using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketClient.Events {
    public class WebSocketEventCloseConnection : WebSocketEvent {
        public override string eventId => "closeConnection";

        public WebSocketCloseStatus status { get; set; }
        public string? description { get; set; }
    }

}
