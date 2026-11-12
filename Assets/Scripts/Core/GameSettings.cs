using System;
using UnityEngine;

namespace CyberDefender.Core
{
    public enum Difficulty : byte { Easy, Normal, Hard, Nightmare }

    /// <summary>Множители боя для уровня сложности. 1 — исходный баланс из ассетов вирусов.</summary>
    public sealed class DifficultyProfile
    {
        public string Name;
        public string Description;
        public float EnemyHealth = 1f;
        public float EnemyDamage = 1f;
        public float WindupScale = 1f;    // меньше — короче замах, меньше времени на блок
        public float CooldownScale = 1f;  // пауза между атаками и спецприёмами
        public float SpeedScale = 1f;
        public float KnockbackTaken = 1f; // как далеко отлетают вирусы от ударов игрока
        public float StunTaken = 1f;      // как долго вирус оглушён ударом игрока
        public float GuardChance;         // шанс заблокировать лёгкий удар спереди
        public int ComboLength = 1;       // ударов в серии
        public float EnergyGain = 1f;     // скорость накопления Kernel Energy игроком
        public int Backups = 3;
        public float BossHealth = 1f;     // дополнительно к EnemyHealth
        public float BossSkillTaken = 1f; // доля урона голосовых навыков по боссу
    }

    /// <summary>Настройки игрока (сложность, громкость). Хранятся в PlayerPrefs.</summary>
    public static class GameSettings
    {
        private const string DifficultyKey = "CyberDefender.Difficulty";
        private const string MusicKey = "CyberDefender.MusicVolume";
        private const string SfxKey = "CyberDefender.SfxVolume";
        private const string BridgeKey = "CyberDefender.AutoStartBridge";

        private static readonly DifficultyProfile[] Profiles =
        {
            new DifficultyProfile
            {
                Name = "ЛЁГКИЙ",
                Description = "Прежний баланс: долгие замахи, вирусы не блокируют и бьют по одному разу.",
            },
            new DifficultyProfile
            {
                Name = "НОРМАЛЬНЫЙ",
                Description = "Вирусы крепче и бьют сериями по 2, иногда блокируют лёгкие удары. Босс заметно сильнее.",
                EnemyHealth = 1.3f, EnemyDamage = 1.3f, WindupScale = 0.85f, CooldownScale = 0.8f, SpeedScale = 1.1f,
                KnockbackTaken = 0.85f, StunTaken = 0.85f, GuardChance = 0.15f, ComboLength = 2, EnergyGain = 0.9f,
                Backups = 3, BossHealth = 1.4f, BossSkillTaken = 0.55f,
            },
            new DifficultyProfile
            {
                Name = "СЛОЖНЫЙ",
                Description = "Быстрые замахи, частые блоки, всего 2 бэкапа. Энергия копится медленнее.",
                EnemyHealth = 1.6f, EnemyDamage = 1.6f, WindupScale = 0.72f, CooldownScale = 0.65f, SpeedScale = 1.2f,
                KnockbackTaken = 0.72f, StunTaken = 0.7f, GuardChance = 0.25f, ComboLength = 2, EnergyGain = 0.8f,
                Backups = 2, BossHealth = 1.7f, BossSkillTaken = 0.45f,
            },
            new DifficultyProfile
            {
                Name = "КОШМАР",
                Description = "Серии по 3 удара, почти нет времени на реакцию, 1 бэкап. Только для хакеров.",
                EnemyHealth = 2f, EnemyDamage = 2f, WindupScale = 0.6f, CooldownScale = 0.5f, SpeedScale = 1.3f,
                KnockbackTaken = 0.6f, StunTaken = 0.55f, GuardChance = 0.35f, ComboLength = 3, EnergyGain = 0.7f,
                Backups = 1, BossHealth = 2f, BossSkillTaken = 0.35f,
            },
        };

        public static Difficulty Difficulty { get; private set; } = Difficulty.Normal;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        /// <summary>Игра сама запускает Python-мост камеры и микрофона.</summary>
        public static bool AutoStartBridge { get; private set; } = true;

        public static DifficultyProfile Profile => Profiles[(int)Difficulty];
        public static int DifficultyCount => Profiles.Length;
        public static DifficultyProfile GetProfile(Difficulty difficulty) => Profiles[(int)difficulty];

        public static event Action Changed;

        // PlayerPrefs нельзя читать в статическом конструкторе — загружаем перед первой сценой
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Changed = null;
            Difficulty = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal), 0, Profiles.Length - 1);
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
            AutoStartBridge = PlayerPrefs.GetInt(BridgeKey, 1) == 1;
        }

        public static void SetAutoStartBridge(bool value)
        {
            AutoStartBridge = value;
            PlayerPrefs.SetInt(BridgeKey, value ? 1 : 0);
            Save();
        }

        public static void SetDifficulty(Difficulty value)
        {
            Difficulty = (Difficulty)Mathf.Clamp((int)value, 0, Profiles.Length - 1);
            PlayerPrefs.SetInt(DifficultyKey, (int)Difficulty);
            Save();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            Save();
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            Save();
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
