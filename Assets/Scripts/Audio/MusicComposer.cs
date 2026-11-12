using UnityEngine;

namespace CyberDefender.Audio
{
    /// <summary>Параметры чиптюн-петли: темп, тональность, гармония и «накал» (0 — меню, 1 — бой, 2 — босс).</summary>
    public readonly struct MusicStyle
    {
        public readonly string Name;
        public readonly float Bpm;
        public readonly int Root;          // полутоны от C5
        public readonly int[] Progression; // ступени натурального минора, по 2 такта на аккорд
        public readonly int Intensity;
        public readonly int Seed;

        public MusicStyle(string name, float bpm, int root, int[] progression, int intensity, int seed)
        {
            Name = name;
            Bpm = bpm;
            Root = root;
            Progression = progression;
            Intensity = intensity;
            Seed = seed;
        }
    }

    /// <summary>
    /// Сочиняет фоновую музыку кодом: бас, арпеджио по аккордам, барабаны и мелодия во второй половине петли.
    /// Каждый этаж звучит в своей тональности и темпе, финальный этаж — быстрее и плотнее.
    /// </summary>
    public static class MusicComposer
    {
        private const int StepsPerBar = 16;
        private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };

        public static readonly MusicStyle Menu = new MusicStyle("Music_Menu", 92f, 0, new[] { 0, 5, 2, 6 }, 0, 7);

        private static readonly MusicStyle[] Floors =
        {
            new MusicStyle("Music_Floor1", 116f, -3, new[] { 0, 5, 2, 6 }, 1, 11),
            new MusicStyle("Music_Floor2", 122f, -1, new[] { 0, 3, 5, 4 }, 1, 23),
            new MusicStyle("Music_Floor3", 126f, 2, new[] { 0, 6, 5, 6 }, 1, 37),
            new MusicStyle("Music_Floor4", 132f, 4, new[] { 0, 5, 3, 4 }, 1, 41),
        };

        public static readonly MusicStyle Boss = new MusicStyle("Music_Boss", 142f, -2, new[] { 0, 0, 5, 6 }, 2, 53);

        public static MusicStyle ForFloor(int index, bool isBoss) =>
            isBoss ? Boss : Floors[Mathf.Abs(index) % Floors.Length];

        public static AudioClip Compose(in MusicStyle s)
        {
            float step = 60f / s.Bpm / 4f;
            int bars = s.Progression.Length * 2;
            var b = new SynthBuffer(bars * StepsPerBar * step, loop: true, seed: s.Seed);
            var rng = new System.Random(s.Seed);

            int[] arpUp = { 0, 1, 2, 3 };
            int[] arpWave = { 0, 2, 1, 3, 2, 1 };

            for (int bar = 0; bar < bars; bar++)
            {
                int degree = s.Progression[bar / 2];
                float barStart = bar * StepsPerBar * step;
                // Аккорд из трёх ступеней через одну + октава основного тона
                int[] chord = { ScaleNote(degree), ScaleNote(degree + 2), ScaleNote(degree + 4), ScaleNote(degree) + 12 };
                bool secondHalf = bar >= bars / 2;

                Bass(b, s, barStart, step, chord[0]);
                Arpeggio(b, s, barStart, step, chord, bar % 2 == 0 ? arpUp : arpWave, rng);
                Drums(b, s, barStart, step, bar);

                if (s.Intensity == 0)
                    Pad(b, s, barStart, step * StepsPerBar, chord);
                else if (secondHalf)
                    Lead(b, s, barStart, step, chord, rng);
            }

            b.Finish(0.85f);
            return b.ToClip(s.Name);
        }

        private static int ScaleNote(int degree)
        {
            int octave = Mathf.FloorToInt(degree / 7f);
            return octave * 12 + Minor[degree - octave * 7];
        }

        private static void Bass(SynthBuffer b, in MusicStyle s, float barStart, float step, int chordRoot)
        {
            float f = SynthBuffer.Note(s.Root + chordRoot - 24);

            if (s.Intensity == 0)
            {
                b.Tone(barStart, step * StepsPerBar, f, f, Wave.Triangle, 0.35f, decay: 0.6f, attack: 0.05f, release: 0.2f);
                return;
            }

            // Восьмые с «прыжком» на октаву — у босса через каждую ноту
            for (int i = 0; i < StepsPerBar; i += 2)
            {
                bool octave = s.Intensity >= 2 ? (i / 2) % 2 == 1 : i == 6 || i == 14;
                float note = octave ? f * 2f : f;
                float t = barStart + i * step;
                b.Tone(t, step * 1.8f, note, note, Wave.Square, 0.14f, decay: 6f);
                b.Tone(t, step * 1.8f, note, note, Wave.Triangle, 0.3f, decay: 5f);
            }
        }

        private static void Arpeggio(SynthBuffer b, in MusicStyle s, float barStart, float step, int[] chord, int[] pattern, System.Random rng)
        {
            int stride = s.Intensity == 0 ? 2 : 1;
            int n = 0;
            for (int i = 0; i < StepsPerBar; i += stride, n++)
            {
                if (s.Intensity == 1 && rng.NextDouble() < 0.1)
                    continue;

                float f = SynthBuffer.Note(s.Root + chord[pattern[n % pattern.Length]]);
                float t = barStart + i * step;
                if (s.Intensity == 0)
                    b.Tone(t, step * 3f, f, f, Wave.Triangle, 0.1f, decay: 4f, release: 0.05f);
                else
                    b.Tone(t, step * 0.9f, f, f, Wave.Pulse, 0.08f, decay: 10f, detune: s.Intensity >= 2 ? 0.008f : 0f);
            }
        }

        private static void Pad(SynthBuffer b, in MusicStyle s, float barStart, float length, int[] chord)
        {
            for (int i = 0; i < 3; i++)
            {
                float f = SynthBuffer.Note(s.Root + chord[i] - 12);
                b.Tone(barStart, length, f, f, Wave.Triangle, 0.07f, decay: 0.4f, attack: 0.35f, release: 0.3f, vibrato: 0.004f, detune: 0.004f);
            }
        }

        private static void Lead(SynthBuffer b, in MusicStyle s, float barStart, float step, int[] chord, System.Random rng)
        {
            // Мелодия четвертями по звукам аккорда — всегда в гармонии, но каждый раз своя
            for (int i = 0; i < StepsPerBar; i += 4)
            {
                if (rng.NextDouble() < 0.25)
                    continue;
                float f = SynthBuffer.Note(s.Root + chord[rng.Next(3)] + 12);
                b.Tone(barStart + i * step, step * 3.2f, f, f, Wave.Square, 0.055f, decay: 3f, release: 0.04f, vibrato: 0.008f);
            }
        }

        private static void Drums(SynthBuffer b, in MusicStyle s, float barStart, float step, int bar)
        {
            if (s.Intensity == 0)
            {
                Kick(b, barStart, 0.3f);
                Kick(b, barStart + 8 * step, 0.25f);
                Hat(b, barStart + 4 * step, 0.04f);
                Hat(b, barStart + 12 * step, 0.04f);
                return;
            }

            for (int i = 0; i < StepsPerBar; i++)
            {
                float t = barStart + i * step;
                if (i % 4 == 0 || (s.Intensity >= 2 && i == 14 && bar % 2 == 1))
                    Kick(b, t, 0.55f);
                if (i == 4 || i == 12)
                    Snare(b, t);
                if (s.Intensity >= 2)
                    Hat(b, t, i % 2 == 0 ? 0.05f : 0.09f);
                else if (i % 4 == 2)
                    Hat(b, t, 0.09f);
            }
        }

        private static void Kick(SynthBuffer b, float t, float volume)
        {
            b.Tone(t, 0.16f, 150f, 45f, Wave.Sine, volume, decay: 14f, attack: 0.001f);
            b.Noise(t, 0.01f, volume * 0.35f, 4000f, 4000f);
        }

        private static void Snare(SynthBuffer b, float t)
        {
            b.Noise(t, 0.14f, 0.3f, 6000f, 2500f, decay: 20f);
            b.Tone(t, 0.08f, 220f, 160f, Wave.Triangle, 0.2f, decay: 25f);
        }

        private static void Hat(SynthBuffer b, float t, float volume)
        {
            b.Noise(t, 0.04f, volume, 7000f, 7000f, decay: 60f, highpass: true);
        }
    }
}
