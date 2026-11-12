using System.Collections;
using CyberDefender.Audio;
using CyberDefender.Core;
using CyberDefender.Enemies;
using CyberDefender.UI;
using UnityEngine;

namespace CyberDefender.Tower
{
    /// <summary>
    /// Катсцена перед боем: камера наезжает на вируса, он «пиликает» и говорит реплики субтитрами.
    /// Ладонь (J) — допечатать / следующая реплика, кулак (S) — пропустить всю сцену.
    /// Если игрок ничего не жмёт, реплики листаются сами.
    /// </summary>
    public sealed class IntroCutscene : MonoBehaviour
    {
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private CutsceneOverlay overlay;
        [SerializeField] private VoiceBeeper beeper;

        [Header("Timing")]
        [SerializeField] private float charactersPerSecond = 30f;
        [SerializeField, Tooltip("Писк на каждую N-ю букву")] private int blipEvery = 2;
        [SerializeField] private float focusSize = 3.2f;
        [SerializeField] private float zoomInTime = 0.9f;
        [SerializeField, Tooltip("Сколько реплика висит после того, как допечатана")] private float holdTime = 1.1f;
        [SerializeField] private float holdPerCharacter = 0.035f;
        [SerializeField] private float outroTime = 0.6f;

        private bool advanceRequested;
        private bool skipRequested;
        private bool listening;

        public IEnumerator Play(EnemyController speaker, string[] lines)
        {
            if (speaker == null || lines == null || lines.Length == 0)
                yield break;

            Begin(speaker);
            yield return Wait(zoomInTime);

            for (int i = 0; i < lines.Length && !skipRequested; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                yield return TypeLine(speaker, lines[i]);
                if (skipRequested)
                    break;

                advanceRequested = false;
                yield return Wait(holdTime + lines[i].Length * holdPerCharacter);
                advanceRequested = false;
            }

            Finish();
            yield return new WaitForSeconds(outroTime);
        }

        /// <summary>Аварийная уборка, если катсцену прервали снаружи (StopAllCoroutines).</summary>
        public void Abort()
        {
            if (listening)
                Finish();
        }

        private void Begin(EnemyController speaker)
        {
            advanceRequested = false;
            skipRequested = false;
            listening = true;
            GameEventBus.OnGestureDetected += HandleGesture;

            overlay.SetSpeaker(speaker.Definition.displayName, Color.Lerp(speaker.Definition.color, Color.white, 0.25f));
            overlay.SetSubtitle("", 0);
            overlay.Show(true);
            // Вирус чуть выше центра кадра — снизу его не закрывают субтитры
            cameraDirector.Focus((Vector2)speaker.transform.position + new Vector2(0f, -0.3f), focusSize);
        }

        private void Finish()
        {
            listening = false;
            GameEventBus.OnGestureDetected -= HandleGesture;
            overlay.Show(false);
            overlay.SetSubtitle("", 0);
            cameraDirector.ClearFocus();
        }

        private IEnumerator TypeLine(EnemyController speaker, string line)
        {
            VirusDefinition voice = speaker.Definition;
            float budget = 0f;
            int visible = 0;
            int letters = 0;
            advanceRequested = false;

            while (visible < line.Length)
            {
                if (skipRequested)
                    yield break;
                if (advanceRequested)
                {
                    advanceRequested = false;
                    break;
                }

                budget += Time.deltaTime * charactersPerSecond;
                while (budget >= 1f && visible < line.Length)
                {
                    budget -= 1f;
                    char c = line[visible++];
                    if (char.IsLetterOrDigit(c) && letters++ % Mathf.Max(1, blipEvery) == 0)
                    {
                        if (beeper != null)
                            beeper.Blip(voice.voicePitch, voice.voiceGrit);
                        speaker.SpeakBlip();
                    }

                    // Паузы на знаках препинания — «интонация»
                    if (c == '.' || c == '!' || c == '?') budget -= 6f;
                    else if (c == ',' || c == '—') budget -= 3f;
                }

                overlay.SetSubtitle(line, visible);
                yield return null;
            }

            overlay.SetSubtitle(line, line.Length);
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !advanceRequested && !skipRequested; t += Time.deltaTime)
                yield return null;
        }

        private void HandleGesture(GestureEvent e)
        {
            if (e.Phase != GesturePhase.Started)
                return;
            if (e.Type == GestureType.OpenPalm)
                advanceRequested = true;
            else if (e.Type == GestureType.Fist)
                skipRequested = true;
        }

        private void OnDisable()
        {
            Abort();
        }

#if UNITY_EDITOR
        public void EditorConfigure(CameraDirector cameraDirector, CutsceneOverlay overlay, VoiceBeeper beeper)
        {
            this.cameraDirector = cameraDirector;
            this.overlay = overlay;
            this.beeper = beeper;
        }
#endif
    }
}
