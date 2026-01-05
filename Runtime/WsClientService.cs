using EventBusCore;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace EGS.SocketIO 
{
    public sealed class WsClientService : IDisposable
    {
        private readonly SocketIOConfig _config;
        private readonly IEventBus _bus;
        private SocketIOUnity _socket;

        public bool IsReady { get; private set; }

        public WsClientService(SocketIOConfig config, IEventBus bus)
        {
            _config = config;
            _bus = bus;
        }

        public async Task ConnectAsync()
        {
            Dispose();
            IsReady = false;

            var uri = new Uri(_config.url);
            _socket = new SocketIOUnity(uri, new SocketIOOptions
            {
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
                Reconnection = _config.autoReconnect,
                ConnectionTimeout = TimeSpan.FromSeconds(_config.connectionTimeout)
            });

            // 1. Conectou no Socket (N�vel TCP/Engine.IO)
            _socket.OnConnected += (sender, e) =>
            {
                Debug.Log($"[WS-IO] Conectado. Enviando Handshake: {_config.handshakeEvent}");
                _ = SendHandshakeAsync();
            };

            // 2. Escuta o ACK espec�fico definido no ScriptableObject
            _socket.On(_config.handshakeAckEvent, (response) =>
            {
                // Verifica se o status � 'accepted' (ou o que estiver na config)
                // O response.GetValue() pega o JSON recebido
                var data = response.GetValue<JObject>();
                var status = data["status"]?.ToString();

                if (status == _config.successStatusValue)
                {
                    IsReady = true;
                    MainThread.Post(() =>
                    {
                        Debug.Log($"[WS-IO] Handshake aceito! Status: {status}");
                        _bus?.Publish(new ConnectionStatusChangedEvent(ConnectionStatus.Ready));
                    });
                }
                else
                {
                    Debug.LogWarning($"[WS-IO] Handshake recusado ou inv�lido. Status: {status}");
                    
                }
            });

            _socket.OnDisconnected += (sender, e) =>
            {
                IsReady = false;
                MainThread.Post(() =>
                    _bus?.Publish(new ConnectionStatusChangedEvent(ConnectionStatus.Disconnected)));
            };

            _socket.OnError += (sender, e) =>
            {
                Debug.LogError($"[WS-IO] Erro interno: {e}");
            };

            // 3. Adaptador Gen�rico (Eventos SocketIO -> EventBus Unity)
            _socket.OnAny((eventName, response) =>
            {
                // Ignora eventos de controle interno e o ACK que j� tratamos
                if (eventName == "connect" || eventName == "disconnect" || eventName == _config.handshakeAckEvent) return;

                try
                {
                    var token = response.GetValue<JToken>();
                    string jsonFinal = "";

                    // Transforma em { "type": "eventName", ... } para compatibilidade
                    if (token != null && token.Type == JTokenType.Object)
                    {
                        var jObj = (JObject)token;
                        if (!jObj.ContainsKey("type")) jObj["type"] = eventName;
                        jsonFinal = jObj.ToString(Newtonsoft.Json.Formatting.None);
                    }
                    else
                    {
                        var jObj = new JObject();
                        jObj["type"] = eventName;
                        jObj["data"] = token;
                        jsonFinal = jObj.ToString(Newtonsoft.Json.Formatting.None);
                    }

                    MainThread.Post(() => _bus?.Publish(new IncomingWsMessageEvent(jsonFinal)));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WS-IO] Erro ao adaptar msg: {ex.Message}");
                }
            });

            MainThread.Post(() =>
                _bus?.Publish(new ConnectionStatusChangedEvent(ConnectionStatus.Connecting)));

            await _socket.ConnectAsync();
        }

        private async Task SendHandshakeAsync()
        {
            if (_socket == null || !_socket.Connected) return;

            // Parseia a string JSON do ScriptableObject para um objeto real
            try
            {
                var payload = JObject.Parse(_config.handshakePayload);
                await _socket.EmitAsync(_config.handshakeEvent, payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WS-IO] Erro ao enviar handshake (JSON inv�lido no Config?): {ex.Message}");
            }
        }

        // --- M�TODO NOVO PARA ENVIAR COMANDOS (BUILD, PLAY, ETC) ---
        public async void Emit(string eventName, object data)
        {
            if (_socket != null && _socket.Connected && IsReady)
            {
                await _socket.EmitAsync(eventName, data);
            }
            else
            {
                Debug.LogWarning($"[WS-IO] Tentou enviar '{eventName}' mas n�o est� pronto/conectado.");
            }
        }

        public void Dispose()
        {
            if (_socket != null)
            {
                _socket.Disconnect();
                _socket.Dispose();
                _socket = null;
            }
            IsReady = false;
        }
    }
}