using UnityEngine;

namespace CyberDefender.Audio
{
    public enum Sfx : byte
    {
        SwingLight, SwingHeavy, HitLight, HitHeavy, HitSkill, PlayerHurt, Block,
        Jump, AirJump, EnemyWindup, EnemyDash, EnemyTeleport, EnemyJump, EnemyDefeated,
        RingOut, BackupLost, Respawn, EnergyFull, Purge, Firewall, Isolate,
        UiMove, UiConfirm, UiBack, FloorStart, FloorCleared, GameOver, Victory,
        BootLine, TitleReveal, BossEnrage
    }

    /// <summary>Рецепты всех звуковых эффектов. Каждый звук синтезируется кодом один раз при запуске.</summary>
    public static class SfxLibrary
    {
        public static int Count => (int)Sfx.BossEnrage + 1;

        /// <summary>Громкость звука относительно остальных — баланс микса в одном месте.</summary>
        public static float Volume(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.SwingLight: return 0.35f;
                case Sfx.SwingHeavy: return 0.45f;
                case Sfx.EnemyJump: return 0.3f;
                case Sfx.Jump:
                case Sfx.AirJump: return 0.4f;
                case Sfx.EnemyWindup: return 0.45f;
                case Sfx.UiMove: return 0.4f;
                case Sfx.BootLine: return 0.25f;
                case Sfx.UiConfirm:
                case Sfx.UiBack: return 0.5f;
                case Sfx.EnemyDefeated: return 0.7f;
                default: return 0.8f;
            }
        }

        /// <summary>Случайный разброс высоты: частые звуки не звучат как одна и та же запись.</summary>
        public static float PitchJitter(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.SwingLight:
                case Sfx.SwingHeavy:
                case Sfx.HitLight:
                case Sfx.HitHeavy:
                case Sfx.Block:
                case Sfx.EnemyJump:
                case Sfx.PlayerHurt:
                    return 0.08f;
                default:
                    return 0f;
            }
        }

        public static AudioClip Build(Sfx sfx)
        {
            SynthBuffer b;
            switch (sfx)
            {
                case Sfx.SwingLight:
                    b = new SynthBuffer(0.12f);
                    b.Noise(0f, 0.1f, 0.6f, 6000f, 1500f, decay: 20f, attack: 0.01f);
                    b.Tone(0f, 0.06f, 900f, 500f, Wave.Sine, 0.1f);
                    break;

                case Sfx.SwingHeavy:
                    b = new SynthBuffer(0.26f);
                    b.Noise(0f, 0.24f, 0.7f, 3000f, 400f, decay: 9f, attack: 0.03f);
                    b.Tone(0f, 0.22f, 220f, 80f, Wave.Triangle, 0.3f, decay: 8f);
                    break;

                case Sfx.HitLight:
                    b = new SynthBuffer(0.15f);
                    b.Tone(0f, 0.12f, 260f, 70f, Wave.Square, 0.5f, decay: 25f);
                    b.Noise(0f, 0.05f, 0.7f, 8000f, 2000f, decay: 50f);
                    b.Crush(12);
                    break;

                case Sfx.HitHeavy:
                    b = new SynthBuffer(0.36f);
                    b.Tone(0f, 0.3f, 180f, 40f, Wave.Square, 0.55f, decay: 10f);
                    b.Tone(0f, 0.3f, 90f, 35f, Wave.Sine, 0.7f, decay: 8f);
                    b.Noise(0f, 0.2f, 0.8f, 5000f, 400f, decay: 14f);
                    b.Crush(10);
                    break;

                case Sfx.HitSkill:
                    b = new SynthBuffer(0.36f);
                    b.Tone(0f, 0.3f, 160f, 40f, Wave.Square, 0.5f, decay: 10f);
                    b.Tone(0f, 0.16f, 1800f, 300f, Wave.Saw, 0.25f, decay: 14f);
                    b.Noise(0f, 0.2f, 0.7f, 7000f, 600f, decay: 14f);
                    b.Crush(8);
                    break;

                case Sfx.PlayerHurt:
                    b = new SynthBuffer(0.3f);
                    b.Tone(0f, 0.26f, 420f, 110f, Wave.Square, 0.45f, decay: 6f, detune: 0.03f);
                    b.Tone(0f, 0.26f, 430f, 105f, Wave.Saw, 0.2f, decay: 6f);
                    b.Noise(0f, 0.08f, 0.5f, 3000f, 3000f, decay: 30f);
                    b.Crush(6);
                    break;

                case Sfx.Block:
                    b = new SynthBuffer(0.3f);
                    b.Tone(0f, 0.28f, 1250f, 1250f, Wave.Sine, 0.35f, decay: 14f);
                    b.Tone(0f, 0.28f, 1870f, 1870f, Wave.Sine, 0.25f, decay: 18f);
                    b.Tone(0f, 0.2f, 3100f, 3100f, Wave.Sine, 0.12f, decay: 25f);
                    b.Noise(0f, 0.025f, 0.6f, 6000f, 6000f, highpass: true);
                    break;

                case Sfx.Jump:
                    b = new SynthBuffer(0.14f);
                    b.Tone(0f, 0.12f, 280f, 680f, Wave.Square, 0.3f, decay: 10f);
                    b.Crush(16);
                    break;

                case Sfx.AirJump:
                    b = new SynthBuffer(0.17f);
                    b.Tone(0f, 0.14f, 480f, 1100f, Wave.Pulse, 0.28f, decay: 9f);
                    b.Noise(0f, 0.14f, 0.2f, 2000f, 7000f, attack: 0.02f);
                    break;

                case Sfx.EnemyWindup:
                    // Телеграф удара на слух: «пи-пи» снизу вверх — пора ставить блок
                    b = new SynthBuffer(0.17f);
                    b.Tone(0f, 0.07f, 880f, 880f, Wave.Square, 0.22f);
                    b.Tone(0.085f, 0.075f, 1320f, 1320f, Wave.Square, 0.22f);
                    b.Crush(8);
                    break;

                case Sfx.EnemyDash:
                    b = new SynthBuffer(0.26f);
                    b.Noise(0f, 0.24f, 0.6f, 800f, 6000f, decay: 6f, attack: 0.02f);
                    b.Tone(0f, 0.2f, 150f, 400f, Wave.Saw, 0.15f);
                    break;

                case Sfx.EnemyTeleport:
                    b = new SynthBuffer(0.32f);
                    for (int i = 0; i < 6; i++)
                        b.Tone(i * 0.04f, 0.05f, SynthBuffer.Note(12 - i * 3), SynthBuffer.Note(12 - i * 3), Wave.Square, 0.25f);
                    b.Noise(0f, 0.25f, 0.25f, 9000f, 2000f, highpass: true);
                    b.Crush(6);
                    break;

                case Sfx.EnemyJump:
                    b = new SynthBuffer(0.12f);
                    b.Tone(0f, 0.1f, 180f, 380f, Wave.Triangle, 0.35f, decay: 12f);
                    break;

                case Sfx.EnemyDefeated:
                    b = new SynthBuffer(0.62f);
                    b.Tone(0f, 0.5f, 600f, 60f, Wave.Square, 0.4f, decay: 4f);
                    b.Tone(0.05f, 0.45f, 300f, 30f, Wave.Saw, 0.3f, decay: 5f);
                    b.Noise(0f, 0.55f, 0.6f, 6000f, 200f, decay: 5f);
                    b.Crush(5);
                    break;

                case Sfx.RingOut:
                    // Протяжное «пиу-у-у» в пропасть и взрыв за краем
                    b = new SynthBuffer(1f);
                    b.Tone(0f, 0.45f, 1600f, 200f, Wave.Sine, 0.35f, vibrato: 0.02f);
                    b.Tone(0.4f, 0.58f, 120f, 35f, Wave.Sine, 0.8f, decay: 5f);
                    b.Noise(0.4f, 0.58f, 0.8f, 4000f, 150f, decay: 5f);
                    b.Crush(10);
                    break;

                case Sfx.BackupLost:
                    b = new SynthBuffer(1f);
                    b.Noise(0f, 0.08f, 0.5f, 5000f, 5000f, decay: 20f);
                    Melody(b, 0f, 0.18f, new[] { 3f, -1f, -5f }, Wave.Square, 0.3f, 0.2f, 3f, 0.02f);
                    b.Tone(0.54f, 0.45f, SynthBuffer.Note(-9), SynthBuffer.Note(-10), Wave.Square, 0.3f, decay: 3f, detune: 0.02f);
                    b.Crush(6);
                    break;

                case Sfx.Respawn:
                    b = new SynthBuffer(0.55f);
                    Melody(b, 0f, 0.06f, new[] { 0f, 4f, 7f, 12f, 16f, 19f }, Wave.Pulse, 0.22f, 0.15f, 10f);
                    b.Tone(0f, 0.4f, 400f, 1600f, Wave.Sine, 0.12f);
                    break;

                case Sfx.EnergyFull:
                    b = new SynthBuffer(0.75f);
                    Melody(b, 0f, 0.09f, new[] { 7f, 12f, 19f, 24f }, Wave.Square, 0.18f, 0.35f, 6f);
                    Melody(b, 0f, 0.09f, new[] { -5f, 0f, 7f, 12f }, Wave.Sine, 0.25f, 0.4f, 5f);
                    b.Noise(0.27f, 0.45f, 0.12f, 9000f, 9000f, decay: 6f, highpass: true);
                    break;

                case Sfx.Purge:
                    // 0.35 с зарядки совпадают с замахом PURGE, затем удар по площади
                    b = new SynthBuffer(1.3f);
                    b.Tone(0f, 0.36f, 200f, 1200f, Wave.Saw, 0.25f, attack: 0.3f);
                    b.Noise(0f, 0.36f, 0.3f, 500f, 6000f, attack: 0.3f);
                    b.Tone(0.35f, 0.9f, 90f, 30f, Wave.Sine, 0.9f, decay: 3.5f);
                    b.Tone(0.35f, 0.5f, 400f, 60f, Wave.Square, 0.35f, decay: 6f);
                    b.Noise(0.35f, 0.9f, 0.9f, 7000f, 200f, decay: 4f);
                    b.Crush(12);
                    break;

                case Sfx.Firewall:
                    b = new SynthBuffer(0.95f);
                    float[] shield = { 3f, 7f, 10f, 15f };
                    for (int i = 0; i < shield.Length; i++)
                    {
                        float f = SynthBuffer.Note(shield[i]);
                        b.Tone(i * 0.03f, 0.85f, f, f, Wave.Triangle, 0.2f, decay: 2f, attack: 0.05f, release: 0.2f, vibrato: 0.01f);
                    }
                    b.Tone(0f, 0.3f, 300f, 1200f, Wave.Sine, 0.2f);
                    break;

                case Sfx.Isolate:
                    b = new SynthBuffer(0.5f);
                    b.Tone(0f, 0.45f, 2400f, 200f, Wave.Saw, 0.3f, decay: 4f, vibrato: 0.08f);
                    b.Tone(0f, 0.45f, 2500f, 210f, Wave.Square, 0.15f, decay: 4f);
                    b.Noise(0f, 0.1f, 0.3f, 6000f, 6000f, highpass: true);
                    b.Crush(8);
                    break;

                case Sfx.UiMove:
                    b = new SynthBuffer(0.05f);
                    b.Tone(0f, 0.04f, 1320f, 1320f, Wave.Square, 0.25f, decay: 40f);
                    break;

                case Sfx.UiConfirm:
                    b = new SynthBuffer(0.22f);
                    Melody(b, 0f, 0.07f, new[] { 3f, 10f }, Wave.Square, 0.25f, 0.13f, 10f);
                    break;

                case Sfx.UiBack:
                    b = new SynthBuffer(0.22f);
                    Melody(b, 0f, 0.07f, new[] { 10f, 3f }, Wave.Square, 0.25f, 0.13f, 10f);
                    break;

                case Sfx.FloorStart:
                    b = new SynthBuffer(0.9f);
                    Melody(b, 0f, 0.1f, new[] { -12f, -9f, -5f }, Wave.Pulse, 0.25f, 0.12f, 8f);
                    b.Tone(0.3f, 0.55f, SynthBuffer.Note(0), SynthBuffer.Note(0), Wave.Pulse, 0.25f, decay: 3f, release: 0.15f, vibrato: 0.01f);
                    b.Tone(0f, 0.85f, SynthBuffer.Note(-24), SynthBuffer.Note(-24), Wave.Saw, 0.25f, decay: 2.5f, release: 0.2f);
                    break;

                case Sfx.FloorCleared:
                    b = new SynthBuffer(1.15f);
                    Melody(b, 0f, 0.09f, new[] { 0f, 4f, 7f }, Wave.Square, 0.22f, 0.12f, 8f);
                    Chord(b, 0.27f, 0.85f, new[] { 12f, 16f, 19f }, Wave.Square, 0.14f, 2.5f);
                    Chord(b, 0.27f, 0.85f, new[] { 0f }, Wave.Triangle, 0.35f, 2.5f);
                    break;

                case Sfx.GameOver:
                    b = new SynthBuffer(1.9f);
                    Melody(b, 0f, 0.3f, new[] { 0f, -2f, -5f }, Wave.Square, 0.25f, 0.28f, 2f, 0.02f);
                    b.Tone(0.9f, 0.95f, SynthBuffer.Note(-9), SynthBuffer.Note(-9), Wave.Square, 0.25f, decay: 1.5f, release: 0.3f, vibrato: 0.015f, detune: 0.02f);
                    b.Tone(0f, 1.85f, SynthBuffer.Note(-33), SynthBuffer.Note(-34), Wave.Saw, 0.25f, decay: 1f, release: 0.4f);
                    b.Crush(10);
                    break;

                case Sfx.Victory:
                    b = new SynthBuffer(2.3f);
                    Melody(b, 0f, 0.1f, new[] { -5f, 0f, 4f, 7f }, Wave.Square, 0.22f, 0.12f, 6f);
                    b.Tone(0.45f, 0.55f, SynthBuffer.Note(12), SynthBuffer.Note(12), Wave.Square, 0.22f, decay: 2f, vibrato: 0.01f);
                    Chord(b, 1.05f, 1.2f, new[] { 7f, 12f, 16f, 19f }, Wave.Square, 0.12f, 1.5f, 0.012f);
                    b.Tone(0f, 0.45f, SynthBuffer.Note(-24), SynthBuffer.Note(-24), Wave.Triangle, 0.4f, decay: 3f);
                    b.Tone(0.45f, 0.55f, SynthBuffer.Note(-17), SynthBuffer.Note(-17), Wave.Triangle, 0.4f, decay: 3f);
                    b.Tone(1.05f, 1.2f, SynthBuffer.Note(-12), SynthBuffer.Note(-12), Wave.Triangle, 0.4f, decay: 1.5f, release: 0.3f);
                    break;

                case Sfx.BootLine:
                    // Щелчок старого терминала на каждую строку загрузки
                    b = new SynthBuffer(0.06f);
                    b.Tone(0f, 0.03f, 2000f, 2000f, Wave.Square, 0.2f, decay: 60f);
                    b.Noise(0f, 0.02f, 0.3f, 8000f, 8000f, highpass: true);
                    break;

                case Sfx.TitleReveal:
                    // Глитч-удар и восходящий «запуск» под появление логотипа
                    b = new SynthBuffer(1.6f);
                    b.Noise(0f, 0.25f, 0.8f, 9000f, 300f, decay: 8f);
                    b.Tone(0f, 0.9f, 70f, 35f, Wave.Sine, 0.9f, decay: 3f);
                    b.Tone(0f, 0.12f, 1800f, 200f, Wave.Square, 0.25f, decay: 20f);
                    Melody(b, 0.15f, 0.08f, new[] { -12f, -5f, 0f, 7f, 12f }, Wave.Pulse, 0.18f, 0.2f, 6f);
                    Chord(b, 0.55f, 1f, new[] { 0f, 3f, 7f, 12f }, Wave.Square, 0.08f, 1.8f, 0.01f);
                    b.Crush(14);
                    break;

                case Sfx.BossEnrage:
                    // Сирена тревоги поверх низкого рыка и ударной волны
                    b = new SynthBuffer(1.4f);
                    b.Tone(0f, 1.3f, 70f, 45f, Wave.Saw, 0.5f, decay: 1.5f, attack: 0.05f, vibrato: 0.05f, detune: 0.03f);
                    b.Noise(0f, 0.6f, 0.8f, 5000f, 200f, decay: 5f);
                    for (int i = 0; i < 3; i++)
                        b.Tone(0.1f + i * 0.4f, 0.38f, 700f, 1100f, Wave.Square, 0.18f, release: 0.05f);
                    b.Crush(8);
                    break;

                default:
                    b = new SynthBuffer(0.05f);
                    break;
            }

            b.Finish(0.9f);
            return b.ToClip(sfx.ToString());
        }

        private static void Melody(SynthBuffer b, float start, float spacing, float[] notes, Wave wave,
            float volume, float noteLength, float decay, float detune = 0f)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                float f = SynthBuffer.Note(notes[i]);
                b.Tone(start + i * spacing, noteLength, f, f, wave, volume, decay: decay, detune: detune);
            }
        }

        private static void Chord(SynthBuffer b, float start, float length, float[] notes, Wave wave,
            float volume, float decay, float vibrato = 0f)
        {
            for (int i = 0; i < notes.Length; i++)
            {
                float f = SynthBuffer.Note(notes[i]);
                b.Tone(start, length, f, f, wave, volume, decay: decay, release: 0.25f, vibrato: vibrato);
            }
        }
    }
}
