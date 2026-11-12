using CyberDefender.Visuals;
using UnityEngine;

namespace CyberDefender.Player
{
    /// <summary>Играбельный защитник: характеристики, которые PlayerController применяет при выборе.</summary>
    [CreateAssetMenu(menuName = "CyberDefender/Character Definition", fileName = "Character")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public string displayName = "DEFENDER";
        public string role = "Защитник";
        [TextArea(2, 4)] public string description;
        public Color color = new Color(0.2f, 1f, 0.45f);
        public Color visorColor = new Color(0.8f, 1f, 1f);
        [Tooltip("Размер коллайдера (хитбокс тела) в юнитах")]
        public Vector2 size = new Vector2(0.9f, 1.4f);
        [Tooltip("Пиксель-арт анимации. Пусто — герой рисуется цветным блоком")]
        public SpriteAnimationSet animations;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float moveSpeed = 7f;
        public float jumpVelocity = 12f;

        [Header("Attacks")]
        public float lightDamage = 5f;
        public float lightDuration = 0.25f;
        public float heavyDamage = 12f;
        public float heavyDuration = 0.55f;
        public float heavyPush = 6f;

        [Header("Defense & Energy")]
        [Range(0f, 1f)] public float blockDamageMultiplier = 0.2f;
        [Tooltip("Множитель накопления Kernel Energy за попадания и блоки")]
        public float energyGainMultiplier = 1f;

        [Header("Voice Skills")]
        public float firewallDuration = 5f;
        [Range(0f, 1f)] public float purgeDamageFraction = 0.35f;
    }
}
