using UnityEngine;

namespace EGS.SocketIO 
{
    [CreateAssetMenu(fileName = "NewSocketConfig", menuName = "EGS/SocketIO Config")]
    public class SocketIOConfig : ScriptableObject
    {
        [Header("Connection Settings")]
        public string url = "http://127.0.0.1:3000";
        public float reconnectDelay = 5f;
        public float connectionTimeout = 10f;
        public bool autoReconnect = false; // Deixe false pois seu Supervisor gerencia isso

        [Header("Handshake Settings")]
        [Tooltip("Nome do evento enviado ao conectar")]
        public string handshakeEvent = "handshake";

        [Tooltip("JSON que ser� enviado no handshake")]
        [TextArea(3, 10)]
        public string handshakePayload = "{\"type\":\"manager\", \"version\":\"1.0.0\"}";

        [Header("Handshake ACK Settings")]
        [Tooltip("Nome do evento que o servidor devolve para confirmar")]
        public string handshakeAckEvent = "handshake_ack";

        [Tooltip("Se o ACK retornar um JSON com status, qual valor define sucesso? Ex: 'accepted'")]
        public string successStatusValue = "accepted";
    }
}