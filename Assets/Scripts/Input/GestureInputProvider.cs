using System;
using System.Globalization;
using System.Net.Sockets;
using CyberDefender.Core;
using UnityEngine;

namespace CyberDefender.InputProviders
{
    /// <summary>
    /// Получает жесты от Tools/ai_bridge/gesture_server.py (OpenCV + MediaPipe Hands, 21 landmark)
    /// и публикует их в GameEventBus. Протокол: "G &lt;GestureType&gt; start|end &lt;confidence&gt;", "HB" — пульс.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GestureInputProvider : MonoBehaviour
    {
        private const int GestureCount = (int)GestureType.DoublePalm + 1;

        [SerializeField] private int listenPort = 5065;
        [SerializeField] private float connectionTimeout = 2f;

        private UdpLineReceiver receiver;
        private double lastMessageTime = double.NegativeInfinity;
        private bool connected;
        private readonly bool[] held = new bool[GestureCount];

        private void OnEnable()
        {
            try
            {
                receiver = new UdpLineReceiver(listenPort);
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[GestureInput] Порт {listenPort} занят: {ex.Message}");
                enabled = false;
            }
        }

        private void OnDisable()
        {
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
        }

        private void HandleLine(string line, double now)
        {
            string[] parts = line.Split(' ');
            if (parts.Length < 4 || parts[0] != "G")
                return;
            if (!Enum.TryParse(parts[1], out GestureType type) || type == GestureType.None)
                return;
            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float confidence))
                confidence = 1f;

            bool started = parts[2] == "start";
            held[(int)type] = started;
            GameEventBus.RaiseGesture(new GestureEvent(type, started ? GesturePhase.Started : GesturePhase.Ended, confidence, InputSource.Gesture, now));
        }

        private void SetConnected(bool value)
        {
            if (value == connected)
                return;

            connected = value;
            if (!value)
                ReleaseHeldGestures();
            GameEventBus.RaiseAiBridgeStatusChanged(InputSource.Gesture, value);
        }

        // Связь пропала — отпускаем удерживаемые жесты, чтобы персонаж не застрял в блоке
        private void ReleaseHeldGestures()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < held.Length; i++)
            {
                if (!held[i])
                    continue;
                held[i] = false;
                GameEventBus.RaiseGesture(new GestureEvent((GestureType)i, GesturePhase.Ended, 1f, InputSource.Gesture, now));
            }
        }
    }
}
