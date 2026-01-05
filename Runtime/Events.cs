using EventBusCore;
using UnityEngine;

namespace EGS.SocketIO 
{
    public enum ConnectionStatus { Connecting, Ready, Disconnected }
    
    public sealed class ConnectionStatusChangedEvent : IEvent
    {
        public readonly ConnectionStatus NewStatus;
        public ConnectionStatusChangedEvent(ConnectionStatus status) => NewStatus = status;
    }
    
    public sealed class IncomingWsMessageEvent : IEvent
    {
        public readonly string RawJson;
        public IncomingWsMessageEvent(string rawJson) => RawJson = rawJson;
    }
}