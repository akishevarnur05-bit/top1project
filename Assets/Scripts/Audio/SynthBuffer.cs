using UnityEngine;

namespace CyberDefender.Audio
{
    public enum Wave : byte { Sine, Square, Pulse, Triangle, Saw }

    /// <summary>
    /// Простейший чиптюн-синтезатор: тоны и шум складываются в буфер, из которого делается AudioClip.
    /// В режиме loop хвосты нот, вылезающие за конец, переносятся в начало — петля звучит бесшовно.
    /// </summary>
    public sealed class SynthBuffer
    {
        public const int SampleRate = 44100;

        public readonly float[] Data;
        private readonly bool loop;
        private readonly System.Random rng;

        public SynthBuffer(float seconds, bool loop = false, int seed = 1)
        {
            Data = new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];
            this.loop = loop;
            rng = new System.Random(seed);
        }

        /// <summary>Частота ноты: 0 — до второй октавы (C5), шаг — полутон.</summary>
        public static float Note(float semitonesFromC5) => 523.25f * Mathf.Pow(2f, semitonesFromC5 / 12f);

        /// <summary>Тон с экспоненциальным скольжением частоты fromHz → toHz и затуханием exp(-decay·t).</summary>
        public void Tone(float start, float duration, float fromHz, float toHz, Wave wave, float volume,
            float decay = 0f, float attack = 0.003f, float release = 0.01f, float vibrato = 0f, float detune = 0f)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.CeilToInt(duration * SampleRate);
            if (count <= 0)
                return;

            // Скольжение и затухание считаем умножением на шаг — без Pow/Exp на каждый сэмпл
            double frequency = fromHz;
            double sweep = System.Math.Pow(toHz / fromHz, 1.0 / count);
            float decayStep = Mathf.Exp(-decay / SampleRate);
            float decayEnv = 1f;
            double phase = 0.0, phase2 = 0.0;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                double f = vibrato > 0f ? frequency * (1.0 + vibrato * System.Math.Sin(t * 37.7)) : frequency;
                phase += f / SampleRate;
                float s = Osc(wave, Frac(phase));
                if (detune > 0f)
                {
                    phase2 += f * (1.0 + detune) / SampleRate;
                    s = (s + Osc(wave, Frac(phase2))) * 0.5f;
                }

                float env = Mathf.Min(Mathf.Clamp01(t / Mathf.Max(attack, 1e-4f)), Mathf.Clamp01((duration - t) / Mathf.Max(release, 1e-4f)));
                Add(first + i, s * env * decayEnv * volume);

                frequency *= sweep;
                decayEnv *= decayStep;
            }
        }

        /// <summary>Шум через однополюсный фильтр со скользящим срезом. highpass — «шипение» тарелок.</summary>
        public void Noise(float start, float duration, float volume, float fromCutoff, float toCutoff,
            float decay = 0f, float attack = 0.001f, float release = 0.01f, bool highpass = false)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.CeilToInt(duration * SampleRate);
            if (count <= 0)
                return;

            double cutoff = fromCutoff;
            double sweep = System.Math.Pow(toCutoff / fromCutoff, 1.0 / count);
            float decayStep = Mathf.Exp(-decay / SampleRate);
            float decayEnv = 1f;
            float low = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float a = 1f - Mathf.Exp((float)(-2.0 * System.Math.PI * cutoff / SampleRate));
                float x = (float)rng.NextDouble() * 2f - 1f;
                low += a * (x - low);
                float s = highpass ? x - low : low;

                float env = Mathf.Min(Mathf.Clamp01(t / Mathf.Max(attack, 1e-4f)), Mathf.Clamp01((duration - t) / Mathf.Max(release, 1e-4f)));
                Add(first + i, s * env * decayEnv * volume);

                cutoff *= sweep;
                decayEnv *= decayStep;
            }
        }

        /// <summary>Бит-крашинг: грубое квантование амплитуды — «цифровая» грязь.</summary>
        public void Crush(int levels)
        {
            for (int i = 0; i < Data.Length; i++)
                Data[i] = Mathf.Round(Data[i] * levels) / levels;
        }

        /// <summary>Мягкое ограничение и нормализация к peak.</summary>
        public void Finish(float peak)
        {
            float max = 1e-4f;
            for (int i = 0; i < Data.Length; i++)
            {
                float x = Data[i];
                Data[i] = x / (1f + Mathf.Abs(x) * 0.5f);
                max = Mathf.Max(max, Mathf.Abs(Data[i]));
            }

            float gain = peak / max;
            for (int i = 0; i < Data.Length; i++)
                Data[i] *= gain;
        }

        public AudioClip ToClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, Data.Length, 1, SampleRate, false);
            clip.SetData(Data, 0);
            return clip;
        }

        private void Add(int index, float value)
        {
            if (loop)
                index %= Data.Length;
            else if (index >= Data.Length)
                return;
            Data[index] += value;
        }

        private static float Frac(double x) => (float)(x - System.Math.Floor(x));

        private static float Osc(Wave wave, float p)
        {
            switch (wave)
            {
                case Wave.Sine: return Mathf.Sin(p * 2f * Mathf.PI);
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Pulse: return p < 0.25f ? 1f : -1f;
                case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                default: return 2f * p - 1f; // Saw
            }
        }
    }
}
