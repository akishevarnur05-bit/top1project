using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CyberDefender.Core;
using UnityEngine;

namespace CyberDefender.InputProviders
{
    /// <summary>
    /// Связь с Tools/ai_bridge/voice_server.py (Vosk). Когда Kernel Energy = 100%, отправляет "LISTEN 1",
    /// и Python начинает распознавать речь. Протокол входящих: "V &lt;VoiceCommand&gt; &lt;confidence&gt;", "HB" — пульс.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class VoiceInputProvider : MonoBehaviour
    {
        private static readonly byte[] ListenOn = Encoding.ASCII.GetBytes("LISTEN 1");
        private static readonly byte[] ListenOff = Encoding.ASCII.GetBytes("LISTEN 0");

        [SerializeField] private int listenPort = 5066;
        [SerializeField] private int controlPort = 5067;
        [SerializeField] private float connectionTimeout = 3f;
        [SerializeField, Tooltip("Как часто повторять LISTEN, если Python-процесс перезапустили")]
        private float stateResendInterval = 1f;

        private UdpLineReceiver receiver;
        private UdpClient sender;
        private IPEndPoint controlEndPoint;
        private double lastMessageTime = double.NegativeInfinity;
        private float nextResendTime;
        private bool connected;
        private bool listening;

        private void OnEnable()
        {
            try
            {
                receiver = new UdpLineReceiver(listenPort);
                sender = new UdpClient();
                controlEndPoint = new IPEndPoint(IPAddress.Loopback, controlPort);
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[VoiceInput] Порт {listenPort} занят: {ex.Message}");
                enabled = false;
                return;
            }

            GameEventBus.OnVoiceListeningChanged += HandleListeningChanged;
        }

        private void OnDisable()
        {
            GameEventBus.OnVoiceListeningChanged -= HandleListeningChanged;
            if (sender != null)
            {
                listening = false;
                SendListenState();
                sender.Close();
                sender = null;
            }
            receiver?.Dispose();
            receiver = null;
            SetConnected(false);
        }

        private void Update()
        {
            if (receiver == null)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            while (receiver.TryDequeue(out string line))
            {
                lastMessageTime = now;
                HandleLine(line, now);
            }

            SetConnected(now - lastMessageTime < connectionTimeout);

            if (Time.unscaledTime >= nextResendTime)
            {
                nextResendTime = Time.unscaledTime + stateResendInterval;
                SendListenState();
            }
        }

        private void HandleListeningChanged(bool value)
        {
            listening = value;
            SendListenState();
        }

        private void HandleLine(string line, double now)
        {
            if (!listening)
                return;

            string[] parts = line.Split(' ');
            if (parts.Length < 3 || parts[0] != "V")
                return;
            if (!Enum.TryParse(parts[1], out VoiceCommand command) || command == VoiceCommand.None)
                return;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float confidence))
                confidence = 1f;

            GameEventBus.RaiseVoiceCommand(new VoiceCommandEvent(command, confidence, InputSource.Voice, now));
        }

        private void SendListenState()
        {
            byte[] payload = listening ? ListenOn : ListenOff;
            try
            {
                sender.Send(payload, payload.Length, controlEndPoint);
            }
            catch (SocketException)
            {
                // Python-моста нет — игра работает с клавиатуры
            }
        }

        private void SetConnected(bool value)
        {
            if (value == connected)
                return;
            connected = value;
            GameEventBus.RaiseAiBridgeStatusChanged(InputSource.Voice, value);
        }
    }
}
