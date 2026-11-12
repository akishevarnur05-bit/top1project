using UnityEngine;

namespace CyberDefender.Combat
{
    public readonly struct HitInfo
    {
        public readonly float Damage;
        public readonly float MaxHealthFraction; // доп. урон в долях от макс. HP цели (PURGE SYSTEM)
        public readonly Vector2 Knockback;
        public readonly float StunDuration;
        public readonly bool IsSkill;            // голосовые приёмы игнорируют защиту от комбо

        public HitInfo(float damage, float maxHealthFraction, Vector2 knockback, float stunDuration, bool isSkill = false)
        {
            Damage = damage;
            MaxHealthFraction = maxHealthFraction;
            Knockback = knockback;
            StunDuration = stunDuration;
            IsSkill = isSkill;
        }
    }

    public interface IHittable
    {
        /// <returns>true, если удар дошёл до цели (в том числе в блок)</returns>
        bool ReceiveHit(in HitInfo hit);
    }
}
