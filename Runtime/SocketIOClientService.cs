using EGS.SocketIO;
using EventBusCore;
using SocketIOClient;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;

namespace EGS.SocketIO
{
    public sealed class SocketIOClientService : IDisposable
    {
        private readonly SocketIOConfig _config;
        private readonly IEventBus _bus;
        private SocketIOUnity _socket;

        public bool IsReady { get; private set; }

        public SocketIOClientService(SocketIOConfig config, IEventBus bus)
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

            // 1. Conectado
            _socket.OnConnected += (sender, e) =>
            {
                Debug.Log($"[WS-IO] Conectado. Enviando Handshake: {_config.handshakeEvent}");
                _ = SendHandshakeAsync();
            };

            // 2. Escuta o ACK (Correção: Usando JsonElement ao invés de JObject)
            _socket.On(_config.handshakeAckEvent, (response) =>
            {
                try
                {
                    // Pega o primeiro argumento como JsonElement (seguro)
                    var data = response.GetValue<JsonElement>();

                    // Tenta ler a propriedade de status
                    string status = null;
                    if (data.TryGetProperty("status", out JsonElement statusProp))
                    {
                        status = statusProp.GetString();
                    }

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
                        Debug.LogWarning($"[WS-IO] Handshake recusado. Status: {status}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WS-IO] Erro ao processar ACK: {ex.Message}");
                }
            });

            _socket.OnDisconnected += (sender, e) =>
            {
                IsReady = false;
                MainThread.Post(() =>
                    _bus?.Publish(new ConnectionStatusChangedEvent(ConnectionStatus.Disconnected)));
            };

            // 3. Adaptador Genérico (CORRIGIDO AQUI)
            _socket.OnAny((eventName, response) =>
            {
                // Ignora eventos internos e o próprio ACK
                if (eventName == "connect" || eventName == "disconnect" || eventName == _config.handshakeAckEvent) return;

                try
                {
                    // Se não tiver dados (ex: apenas um ping), ignoramos ou criamos vazio
                    if (response.Count == 0) return;

                    // CORREÇÃO: Usamos JsonElement (System.Text.Json) para não conflitar com Newtonsoft
                    var element = response.GetValue<JsonElement>();

                    string jsonFinal = "";
                    string rawJson = element.GetRawText(); // Pega o texto cru do JSON

                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        // Truque de String: Injeta o "type" no final do objeto JSON existente
                        // Ex: { "a": 1 } vira { "a": 1, "type": "nomeEvento" }
                        // Removemos a última '}' e adicionamos a propriedade
                        int lastBrace = rawJson.LastIndexOf('}');
                        if (lastBrace > 0)
                        {
                            jsonFinal = rawJson.Substring(0, lastBrace) + $",\"type\":\"{eventName}\"}}";
                        }
                        else
                        {
                            // Caso de borda (JSON vazio {}), apenas recria
                            jsonFinal = $"{{\"type\":\"{eventName}\"}}";
                        }
                    }
                    else
                    {
                        // Se for Array ou Primitivo, empacota num objeto data
                        jsonFinal = $"{{\"type\":\"{eventName}\",\"data\":{rawJson}}}";
                    }

                    // Envia a string formatada para o Unity
                    MainThread.Post(() => _bus?.Publish(new IncomingWsMessageEvent(jsonFinal)));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WS-IO] Erro adapter '{eventName}': {ex.Message}");
                }
            });

            MainThread.Post(() =>
                _bus?.Publish(new ConnectionStatusChangedEvent(ConnectionStatus.Connecting)));

            await _socket.ConnectAsync();
        }

        private async Task SendHandshakeAsync()
        {
            if (_socket == null || !_socket.Connected) return;

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(_config.handshakePayload))
                {
                    // Envia o objeto parseado
                    await _socket.EmitAsync(_config.handshakeEvent, doc.RootElement);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WS-IO] Erro JSON Handshake: {ex.Message}");
            }
        }

        public async void Emit(string eventName, object data)
        {
            if (_socket != null && _socket.Connected && IsReady)
            {
                await _socket.EmitAsync(eventName, data);
            }
        }

        public void Dispose()
        {
            if (_socket != null)
            {
                try { _socket.Disconnect(); } catch { }
                try { _socket.Dispose(); } catch { }
                _socket = null;
            }
            IsReady = false;
        }
    }
}