using System.Collections.Generic;
using UnityEngine;

namespace CyberDefender.Audio
{
    /// <summary>
    /// «Голос» вирусов в катсценах: короткие синтезированные писки на каждую букву субтитров.
    /// Звук генерируется кодом — квадратная волна с падающим тоном, хрип добавляет шум и бит-крашинг.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoiceBeeper : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float BlipDuration = 0.075f;
        private const float BaseFrequency = 520f;

        [SerializeField, Range(0f, 1f)] private float volume = 0.25f;
        [SerializeField, Tooltip("Случайный разброс высоты между писками — речь звучит «живее»")]
        private float pitchJitter = 0.14f;

        private readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
        private readonly System.Random rng = new System.Random(5);
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        public void Blip(float pitch, float grit)
        {
            // Клипы кэшируются по 11 уровням хрипа, высота меняется через AudioSource.pitch
            int key = Mathf.RoundToInt(Mathf.Clamp01(grit) * 10f);
            if (!clips.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                clip = BuildClip(key / 10f);
                clips[key] = clip;
            }

            float jitter = 1f + ((float)rng.NextDouble() * 2f - 1f) * pitchJitter;
            source.pitch = Mathf.Max(0.1f, pitch * jitter);
            source.PlayOneShot(clip, volume);
        }

        private AudioClip BuildClip(float grit)
        {
            int length = Mathf.CeilToInt(SampleRate * BlipDuration);
            var data = new float[length];
            var noise = new System.Random(11);
            int levels = Mathf.RoundToInt(Mathf.Lerp(64f, 5f, grit));
            double phase = 0.0;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float k = t / BlipDuration;

                // Тон слегка падает к концу писка — получается «пиу», а не ровный гудок
                float frequency = BaseFrequency * (1f - 0.3f * k);
                phase += 2.0 * Mathf.PI * frequency / SampleRate;
                float sine = Mathf.Sin((float)phase);
                float tone = Mathf.Lerp(Mathf.Sign(sine), sine, 0.35f);

                float n = (float)noise.NextDouble() * 2f - 1f;
                float sample = Mathf.Lerp(tone, tone * 0.5f + n * 0.5f, grit * 0.8f);
                sample = Mathf.Round(sample * levels) / levels;

                float attack = Mathf.Clamp01(t / 0.004f);
                float release = Mathf.Clamp01((BlipDuration - t) / 0.01f);
                data[i] = sample * attack * release * Mathf.Exp(-t * 22f) * 0.8f;
            }

            AudioClip clip = AudioClip.Create($"Blip_{grit:0.0}", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
