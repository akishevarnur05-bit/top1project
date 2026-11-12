using CyberDefender.Combat;
using CyberDefender.Core;
using CyberDefender.Enemies;
using UnityEngine;

namespace CyberDefender.Player
{
    /// <summary>
    /// Превращает события атак и навыков игрока в реальные попадания: хитбоксы с задержкой
    /// на замах (startup), PURGE по площади, ISOLATE по ближайшему врагу.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [Header("Light Attack")]
        [SerializeField] private float lightStartup = 0.07f;
        [SerializeField] private Vector2 lightHitbox = new Vector2(1.2f, 0.9f);
        [SerializeField] private float lightKnockback = 2.5f;
        [SerializeField] private float lightStun = 0.22f;

        [Header("Heavy Attack")]
        [SerializeField] private float heavyStartup = 0.18f;
        [SerializeField] private Vector2 heavyHitbox = new Vector2(1.6f, 1.1f);
        [SerializeField] private float heavyKnockbackUp = 3f;
        [SerializeField] private float heavyStun = 0.45f;

        [Header("PURGE SYSTEM")]
        [SerializeField] private float purgeStartup = 0.35f;
        [SerializeField] private float purgeRadius = 7f;
        [SerializeField] private float purgeKnockback = 9f;
        [SerializeField] private float purgeStun = 0.8f;

        private PlayerController controller;
        private readonly Collider2D[] overlapResults = new Collider2D[8];
        private ContactFilter2D contactFilter;

        private bool attackPending;
        private float attackResolveTime;
        private PlayerAttackEvent pendingAttack;

        private bool purgePending;
        private float purgeResolveTime;
        private float purgeFraction;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            contactFilter = ContactFilter2D.noFilter;
        }

        private void OnEnable()
        {
            GameEventBus.OnPlayerAttack += HandleAttack;
            GameEventBus.OnPlayerSkillUsed += HandleSkill;
        }

        private void OnDisable()
        {
            GameEventBus.OnPlayerAttack -= HandleAttack;
            GameEventBus.OnPlayerSkillUsed -= HandleSkill;
        }

        private void Update()
        {
            EnemyController nearest = EnemyController.FindNearest(transform.position);
            if (nearest != null)
                controller.FaceTowards(nearest.transform.position.x);

            if (attackPending && Time.time >= attackResolveTime)
            {
                attackPending = false;
                // Если игрока сбили во время замаха — удар отменяется
                if (controller.State == PlayerState.Attack)
                    ResolveAttack(pendingAttack);
            }

            if (purgePending && Time.time >= purgeResolveTime)
            {
                purgePending = false;
                ResolvePurge();
            }
        }

        private void HandleAttack(PlayerAttackEvent e)
        {
            pendingAttack = e;
            attackPending = true;
            attackResolveTime = Time.time + (e.Kind == AttackKind.Light ? lightStartup : heavyStartup);
        }

        private void HandleSkill(PlayerSkillEvent e)
        {
            switch (e.Skill)
            {
                case VoiceCommand.PurgeSystem:
                    purgePending = true;
                    purgeResolveTime = Time.time + purgeStartup;
                    purgeFraction = e.Value;
                    break;

                case VoiceCommand.Isolate:
                    EnemyController target = EnemyController.FindNearest(transform.position);
                    if (target != null)
                        target.ReceiveHit(new HitInfo(0f, 0f, Vector2.zero, e.Duration, isSkill: true));
                    break;
            }
        }

        private void ResolveAttack(in PlayerAttackEvent e)
        {
            bool heavy = e.Kind == AttackKind.Heavy;
            Vector2 size = heavy ? heavyHitbox : lightHitbox;
            Vector2 center = HitboxCenter(size, e.Direction);

            int count = Physics2D.OverlapBox(center, size, 0f, contactFilter, overlapResults);
            var hit = new HitInfo(
                e.Damage,
                0f,
                new Vector2(e.Direction * (heavy ? e.PushForce : lightKnockback), heavy ? heavyKnockbackUp : 0f),
                heavy ? heavyStun : lightStun);

            bool landed = false;
            for (int i = 0; i < count; i++)
            {
                Collider2D col = overlapResults[i];
                if (col.gameObject == gameObject)
                    continue;
                if (col.TryGetComponent(out IHittable target))
                    landed |= target.ReceiveHit(hit);
            }

            if (landed)
                controller.RegisterHitLanded();
        }

        private void ResolvePurge()
        {
            Vector2 origin = transform.position;
            var enemies = EnemyController.Active;

            // Идём с конца: убитый враг удаляет себя из списка
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = enemies[i];
                float dx = enemy.transform.position.x - origin.x;
                if (Mathf.Abs(dx) > purgeRadius)
                    continue;

                float dir = dx >= 0f ? 1f : -1f;
                enemy.ReceiveHit(new HitInfo(0f, purgeFraction, new Vector2(dir * purgeKnockback, 4f), purgeStun, isSkill: true));
            }
        }

        private Vector2 HitboxCenter(Vector2 size, int direction)
        {
            float halfWidth = controller != null ? controller.BodySize.x * 0.5f : 0.45f;
            return (Vector2)transform.position + new Vector2(direction * (halfWidth + size.x * 0.5f), 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            int dir = Application.isPlaying && controller != null ? controller.FacingDirection : 1;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(HitboxCenter(lightHitbox, dir), lightHitbox);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireCube(HitboxCenter(heavyHitbox, dir), heavyHitbox);
        }
#endif
    }
}
