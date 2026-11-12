using CyberDefender.Visuals;
using UnityEngine;

namespace CyberDefender.Enemies
{
    public enum VirusSpecial : byte
    {
        None,
        TrojanDash,      // рывок к игроку издалека, specialPower = скорость рывка
        RansomEncrypt,   // удар «шифрует» энергию игрока, specialPower = сколько энергии снять
        RootkitTeleport  // телепорт за спину игрока, specialPower не используется
    }

    [CreateAssetMenu(menuName = "CyberDefender/Virus Definition", fileName = "Virus")]
    public sealed class VirusDefinition : ScriptableObject
    {
        public string displayName = "Virus";
        public Color color = Color.red;
        [Tooltip("Размер коллайдера (хитбокс тела) в юнитах")]
        public Vector2 size = new Vector2(1f, 1.3f);
        [Tooltip("Пиксель-арт анимации. Пусто — вирус рисуется цветным блоком")]
        public SpriteAnimationSet animations;
        [Tooltip("Цвет, на который умножается спрайт (белый — без изменений)")]
        public Color spriteTint = Color.white;

        [Header("Stats")]
        public float maxHealth = 60f;
        public float moveSpeed = 3f;

        [Header("Platforms")]
        [Tooltip("Запрыгивает на платформы, если игрок выше")]
        public bool canJump = true;
        public float jumpVelocity = 12f;
        public float jumpCooldown = 1.2f;

        [Header("Attack")]
        public float attackRange = 1.3f;
        public float attackDamage = 8f;
        [Tooltip("Замах — окно, в которое игрок видит вспышку и успевает поставить блок")]
        public float windup = 0.45f;
        public float recovery = 0.4f;
        public float attackCooldown = 1.2f;
        public float knockback = 4f;
        [Tooltip("Стан игрока при попадании")]
        public float hitStun = 0.3f;
        [Tooltip("После стольких ударов подряд вирус перестаёт сбиваться с замаха")]
        public int comboArmorHits = 3;

        [Header("Special")]
        public VirusSpecial special;
        public float specialCooldown = 4f;
        public float specialPower = 10f;

        [Header("Voice (катсцены)")]
        [Tooltip("Высота «пиликанья»: 1 — средний голос, меньше — ниже и зловещее")]
        public float voicePitch = 1f;
        [Tooltip("Хрип/помехи в голосе: 0 — чистый писк, 1 — сплошной шум")]
        [Range(0f, 1f)] public float voiceGrit = 0.15f;
    }
}
