using System;

namespace EGS.SocketIO.ExampleSample
{
    [Serializable]
    public class WsBaseMessage
    {
        public string type;
    }

    [Serializable]
    public class WsStartMessage : WsBaseMessage
    {
        public string exerciseId;
    }

    [Serializable]
    public class WsWeatherMessage : WsBaseMessage
    {
        public bool rain;
    }
}
