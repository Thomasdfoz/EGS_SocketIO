using EventBusCore.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EGS.SocketIO.ExampleSample
{
    public sealed class WebSocketClientBehaviour : MonoBehaviour
    {
        [Header("Configuração Geral")]
        [SerializeField] private SocketIOConfig config; // Arraste o ScriptableObject aqui!

        private SocketIOClientService _client;
        private CancellationTokenSource _lifecycleCts;
        private Coroutine _supervisorCo;

        public ConnectionStatus ConnectionStatus { get; private set; }
        private bool _shouldReconnect;

        private void OnDestroy()
        {
            StopLoop();
        }

        public void SendSocketCommand(string eventName, object data)
        {
            if (_client != null && ConnectionStatus == ConnectionStatus.Ready)
            {
                _client.Emit(eventName, data);
            }
            else
            {
                Debug.LogWarning("[WS] Não é possível enviar comando: Socket não está pronto.");
            }
        }

        public void StartLoop()
        {
            if (config == null)
            {
                Debug.LogError("[WS] Configuração (ScriptableObject) não atribuída!");
                return;
            }

            if (_supervisorCo != null) return;

            ConnectionStatus = ConnectionStatus.Disconnected;
            _lifecycleCts = new CancellationTokenSource();

            EventBusProvider.Bus?.Subscribe<ConnectionStatusChangedEvent>(OnConn);
            EventBusProvider.Bus?.Subscribe<IncomingWsMessageEvent>(OnMsg);

            _supervisorCo = StartCoroutine(Supervisor());
        }

        public void StopLoop()
        {
            if (_supervisorCo != null) { StopCoroutine(_supervisorCo); _supervisorCo = null; }

            EventBusProvider.Bus?.Unsubscribe<ConnectionStatusChangedEvent>(OnConn);
            EventBusProvider.Bus?.Unsubscribe<IncomingWsMessageEvent>(OnMsg);

            try { _lifecycleCts?.Cancel(); } catch { }
            try { _lifecycleCts?.Dispose(); } catch { }
            _lifecycleCts = null;

            _client?.Dispose();
            _client = null;
            _shouldReconnect = false;
        }

        private System.Collections.IEnumerator Supervisor()
        {
            EventBusProvider.Bus?.Publish(new ConnectionStatusChangedEvent(ConnectionStatus.Connecting));

            while (_lifecycleCts != null && !_lifecycleCts.IsCancellationRequested)
            {
                // 1) TRY CONNECT
                var connectTask = ConnectOnce();

                // Espera a task
                while (!connectTask.IsCompleted) yield return null;

                if (_lifecycleCts == null || _lifecycleCts.IsCancellationRequested) yield break;

                if (connectTask.IsFaulted)
                {
                    // Usa o tempo definido no ScriptableObject
                    Debug.LogWarning($"[WS] Falha. Retentando em {config.reconnectDelay}s...");
                    yield return new WaitForSeconds(config.reconnectDelay);
                    continue;
                }

                // 2) CONNECTED
                // O Service só manda o evento 'Ready' depois do Handshake ACK.
                // Aqui apenas esperamos cair a conexão.
                _shouldReconnect = false;
                while (!_shouldReconnect && _lifecycleCts != null && !_lifecycleCts.IsCancellationRequested)
                {
                    yield return null;
                }

                if (_lifecycleCts == null || _lifecycleCts.IsCancellationRequested) yield break;

                // 3) RETRY
                yield return new WaitForSeconds(config.reconnectDelay);
            }
        }

        private async Task ConnectOnce()
        {
            try
            {
                _client?.Dispose();
                _client = new SocketIOClientService(config, EventBusProvider.Bus);
                await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void OnConn(ConnectionStatusChangedEvent e)
        {
            ConnectionStatus = e.NewStatus;

            if (e.NewStatus == ConnectionStatus.Ready)
            {
                // Só entra aqui se recebeu o 'handshake_ack' com sucesso
                Debug.Log("<color=green>[WS]</color> SISTEMA PRONTO (Handshake OK).");
                _shouldReconnect = false;
            }
            else if (e.NewStatus == ConnectionStatus.Disconnected)
            {
                _shouldReconnect = true;
            }
        }

        private void OnMsg(IncomingWsMessageEvent e)
        {
            // Mantendo a compatibilidade com seu código legado de processamento
            if (string.IsNullOrWhiteSpace(e.RawJson)) return;

            try
            {
                WsBaseMessage baseMsg = JsonUtility.FromJson<WsBaseMessage>(e.RawJson);
                if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type)) return;

                switch (baseMsg.type)
                {
                    // Seus eventos antigos continuam funcionando aqui
                    case "start":
                        WsStartMessage start = JsonUtility.FromJson<WsStartMessage>(e.RawJson);
                        EventBusProvider.Bus?.Publish(new StartExerciseEvent(start.exerciseId));
                        break;

                    case "weather":
                        WsWeatherMessage weather = JsonUtility.FromJson<WsWeatherMessage>(e.RawJson);
                        EventBusProvider.Bus?.Publish(new WeatherChangedEvent(weather.rain));
                        break;
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[WS] Parse: {ex.Message}"); }
        }
    }
}