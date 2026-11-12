using System;
using CyberDefender.Core;
using CyberDefender.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberDefender.InputProviders
{
    /// <summary>
    /// Отладочная замена камеры и микрофона: клавиши публикуют те же события в GameEventBus,
    /// что и GestureInputProvider / VoiceInputProvider.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class InputSimulatedProvider : MonoBehaviour
    {
        [Serializable]
        private struct GestureBinding
        {
            public Key key;
            public GestureType gesture;
            public GestureBinding(Key key, GestureType gesture) { this.key = key; this.gesture = gesture; }
        }

        [Serializable]
        private struct VoiceBinding
        {
            public Key key;
            public VoiceCommand command;
            public VoiceBinding(Key key, VoiceCommand command) { this.key = key; this.command = command; }
        }

        [SerializeField] private bool editorOnly = false;

        [SerializeField] private GestureBinding[] gestureBindings =
        {
            new GestureBinding(Key.A, GestureType.SwipeLeft),
            new GestureBinding(Key.D, GestureType.SwipeRight),
            new GestureBinding(Key.W, GestureType.PointUp),
            new GestureBinding(Key.J, GestureType.OpenPalm),
            new GestureBinding(Key.K, GestureType.DoublePalm),
            new GestureBinding(Key.S, GestureType.Fist),
            new GestureBinding(Key.LeftArrow, GestureType.SwipeLeft),
            new GestureBinding(Key.RightArrow, GestureType.SwipeRight),
            new GestureBinding(Key.Enter, GestureType.OpenPalm),
        };

        [SerializeField] private VoiceBinding[] voiceBindings =
        {
            new VoiceBinding(Key.Digit1, VoiceCommand.PurgeSystem),
            new VoiceBinding(Key.Digit2, VoiceCommand.FirewallActivate),
            new VoiceBinding(Key.Digit3, VoiceCommand.Isolate),
        };

        [Header("Debug")]
        [SerializeField] private PlayerController debugPlayer;
        [SerializeField] private Key fillEnergyKey = Key.F1;

        private void Awake()
        {
            if (editorOnly && !Application.isEditor)
                enabled = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            double now = Time.realtimeSinceStartupAsDouble;

            for (int i = 0; i < gestureBindings.Length; i++)
            {
                var key = keyboard[gestureBindings[i].key];
                if (key.wasPressedThisFrame)
                    GameEventBus.RaiseGesture(new GestureEvent(gestureBindings[i].gesture, GesturePhase.Started, 1f, InputSource.Simulated, now));
                else if (key.wasReleasedThisFrame)
                    GameEventBus.RaiseGesture(new GestureEvent(gestureBindings[i].gesture, GesturePhase.Ended, 1f, InputSource.Simulated, now));
            }

            for (int i = 0; i < voiceBindings.Length; i++)
            {
                if (keyboard[voiceBindings[i].key].wasPressedThisFrame)
                    GameEventBus.RaiseVoiceCommand(new VoiceCommandEvent(voiceBindings[i].command, 1f, InputSource.Simulated, now));
            }

            if (debugPlayer != null && Application.isEditor && keyboard[fillEnergyKey].wasPressedThisFrame)
                debugPlayer.DebugFillEnergy();
        }
    }
}
