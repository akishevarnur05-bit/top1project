using System.Collections;
using System.Collections.Generic;
using CyberDefender.Combat;
using CyberDefender.Core;
using CyberDefender.Player;
using CyberDefender.Visuals;
using UnityEngine;

namespace CyberDefender.Enemies
{
    /// <summary>
    /// ИИ вируса: подход → замах (телеграф) → удар → восстановление, плюс спецприём по типу.
    /// Ожидаемая иерархия: корень (Rigidbody2D, BoxCollider2D, Health) → "Body" (блок-заглушка, внутри "Eye")
    /// и "Sprite" (SpriteAnimator для пиксель-арта).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(BoxCollider2D))]
    public sealed class EnemyController : MonoBehaviour, IHittable
    {
        private enum State : byte { Idle, Approach, Windup, Recover, Stunned, Dashing, Dead }

        private const float ComboResetTime = 0.8f;
        private const float DashDuration = 0.25f;
        private static readonly Color StunTint = new Color(0.3f, 0.9f, 1f);
        private static readonly Color SpriteHitTint = new Color(1f, 0.45f, 0.45f);
        private static readonly Color SpriteWindupTint = new Color(1f, 0.55f, 0.55f);
        private static readonly Color GuardTint = new Color(0.4f, 0.8f, 1f);
        private static readonly Color EnrageTint = new Color(1f, 0.35f, 0.3f);

        [Header("Boss")]
        [SerializeField, Range(0f, 1f), Tooltip("При какой доле HP босс впадает в ярость")]
        private float enrageThreshold = 0.5f;
        [SerializeField] private float enrageShockwaveRadius = 4f;
        [SerializeField] private float enrageShockwaveForce = 9f;

        public static readonly List<EnemyController> Active = new List<EnemyController>();

        [SerializeField] private float knockbackDecay = 20f;
        [SerializeField] private float airKnockbackDecay = 10f;
        [SerializeField, Tooltip("Множитель отброса при 0 HP (при полном HP — ×1)")]
        private float maxKnockbackScale = 2.5f;
        [SerializeField, Tooltip("На какую глубину вирус готов спрыгнуть с края, если игрок не ниже")]
        private float safeDropDepth = 1.6f;
        [SerializeField, Tooltip("Глубина спрыгивания, когда игрок внизу (с платформы на арену, но не в пропасть)")]
        private float chaseDropDepth = 8f;
        [SerializeField] private LayerMask groundMask = 1; // Default: пол и платформы
        [SerializeField, Tooltip("Насколько игрок должен быть выше, чтобы вирус прыгнул")]
        private float jumpTriggerHeight = 1f;
        [SerializeField] private float jumpTriggerDistance = 3.5f;
        [SerializeField, Tooltip("Разница по высоте, при которой вирус ещё может ударить")]
        private float attackHeightTolerance = 1.2f;
        [SerializeField] private float deathFadeTime = 0.5f;
        [SerializeField] private float spriteDeathFadeTime = 0.9f;

        public VirusDefinition Definition { get; private set; }
        public Health Health { get; private set; }
        public bool IsBoss { get; private set; }
        /// <summary>Босс ниже порога HP: быстрее, длиннее серии, чаще спецприём.</summary>
        public bool IsEnraged { get; private set; }

        private Rigidbody2D rb;
        private BoxCollider2D bodyCollider;
        private SpriteRenderer body;
        private Transform eye;
        private SpriteAnimator spriteAnimator;
        private SpriteAnimationSet animations;
        private PlayerController target;
        private float stageHalfWidth = 8f;
        private float groundTop = -2f;
        private int airJumpsLeft;
        private DifficultyProfile profile = new DifficultyProfile();
        private int comboLeft;
        private bool teleportStrike;
        private float guardFlashTimer;

        private State state;
        private bool aiEnabled;
        private float stateTimer;
        private float stateDuration;
        private bool restartClip;
        private float attackCooldown;
        private float specialTimer;
        private float jumpTimer;
        private float knockbackX;
        private float hitFlashTimer;
        private float talkFlashTimer;
        private float lastHitTime = -10f;
        private int comboHits;
        private int facing = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        public static EnemyController FindNearest(Vector2 position)
        {
            EnemyController best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                float d = ((Vector2)Active[i].transform.position - position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Active[i];
                }
            }
            return best;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
            Health = GetComponent<Health>();

            Transform bodyTransform = transform.Find("Body");
            if (bodyTransform != null)
            {
                body = bodyTransform.GetComponent<SpriteRenderer>();
                eye = bodyTransform.Find("Eye");
            }
            Transform spriteTransform = transform.Find("Sprite");
            if (spriteTransform != null)
                spriteAnimator = spriteTransform.GetComponent<SpriteAnimator>();

            Health.Died += HandleDied;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            if (Health != null)
                Health.Died -= HandleDied;
        }

        public void Init(VirusDefinition definition, PlayerController player, float stageHalfWidth, float groundTop, bool isBoss = false)
        {
            Definition = definition;
            target = player;
            this.stageHalfWidth = stageHalfWidth;
            this.groundTop = groundTop;
            IsBoss = isBoss;
            IsEnraged = false;
            profile = GameSettings.Profile;

            Health.SetMax(definition.maxHealth * profile.EnemyHealth * (isBoss ? profile.BossHealth : 1f));
            bodyCollider.size = definition.size;
            specialTimer = SpecialCooldown * 0.5f;
            attackCooldown = 0.5f;
            state = State.Idle;

            animations = definition.animations != null && definition.animations.IsUsable && spriteAnimator != null
                ? definition.animations
                : null;
            bool useSprites = animations != null;

            if (body != null)
            {
                body.enabled = !useSprites;
                body.transform.localScale = new Vector3(definition.size.x, definition.size.y, 1f);
                if (eye != null)
                    eye.gameObject.SetActive(!useSprites);
            }
            if (spriteAnimator != null)
            {
                spriteAnimator.Renderer.enabled = useSprites;
                if (useSprites)
                {
                    spriteAnimator.SetPlacement(new Vector2(animations.offset.x, -definition.size.y * 0.5f + animations.offset.y), animations.scale);
                    spriteAnimator.Play(animations.Idle, 0f, true);
                }
            }

            if (!Active.Contains(this))
                Active.Add(this);
        }

        // ---------- Характеристики с учётом сложности и ярости босса ----------

        private float MoveSpeed => Definition.moveSpeed * profile.SpeedScale * (IsEnraged ? 1.25f : 1f);
        private float AttackCooldown => Definition.attackCooldown * profile.CooldownScale * (IsEnraged ? 0.7f : 1f);
        private float SpecialCooldown => Definition.specialCooldown * profile.CooldownScale * (IsEnraged ? 0.5f : 1f);
        private float AttackDamage => Definition.attackDamage * profile.EnemyDamage;
        private int ComboLength => profile.ComboLength + (IsBoss ? 1 : 0) + (IsEnraged ? 1 : 0);
        private float GuardChance => profile.GuardChance + (IsBoss ? 0.1f : 0f) + (IsEnraged ? 0.1f : 0f);
        private int MaxAirJumps => IsBoss ? 2 : 1;

        /// <summary>Замах с учётом сложности; не короче 0.18 с — иначе блок становится невозможным.</summary>
        private float Windup(float baseTime) => Mathf.Max(0.18f, baseTime * profile.WindupScale * (IsEnraged ? 0.8f : 1f));

        public void SetAiEnabled(bool value)
        {
            aiEnabled = value;
            if (state == State.Dead)
                return;
            if (!value)
                state = State.Idle;
            else if (state == State.Idle)
                state = State.Approach;
        }

        private void Update()
        {
            if (Definition == null || state == State.Dead)
                return;

            float dt = Time.deltaTime;
            if (hitFlashTimer > 0f) hitFlashTimer -= dt;
            if (talkFlashTimer > 0f) talkFlashTimer -= dt;
            if (guardFlashTimer > 0f) guardFlashTimer -= dt;
            attackCooldown -= dt;
            specialTimer -= dt;
            jumpTimer -= dt;

            if (aiEnabled && target != null && !target.Health.IsDead)
                Think(dt);
            else if (state != State.Stunned)
                state = State.Idle;

            if (animations != null)
                UpdateSprite();
            else
                UpdateBlock();
        }

        private void Think(float dt)
        {
            float dx = target.transform.position.x - transform.position.x;
            float dy = target.transform.position.y - transform.position.y;
            float dist = Mathf.Abs(dx);

            switch (state)
            {
                case State.Idle:
                    state = State.Approach;
                    break;

                case State.Approach:
                    // Прямо над/под игроком направление не дёргаем — иначе вирус «дрожит» на месте
                    if (dist > 0.3f)
                        facing = dx >= 0f ? 1 : -1;
                    if (TryUseSpecial(dist))
                        break;
                    if (TryJumpTowardsTarget(dy, dist))
                        break;
                    if (dist <= Definition.attackRange && Mathf.Abs(dy) <= attackHeightTolerance && attackCooldown <= 0f)
                    {
                        teleportStrike = false;
                        comboLeft = ComboLength - 1;
                        Enter(State.Windup, Windup(Definition.windup));
                    }
                    break;

                case State.Windup:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        PerformAttack();
                        // Серия: следующий удар с коротким замахом, если игрок ещё рядом
                        if (comboLeft > 0 && dist <= Definition.attackRange + 0.6f && Mathf.Abs(dy) <= attackHeightTolerance)
                        {
                            comboLeft--;
                            teleportStrike = false;
                            if (dist > 0.3f)
                                facing = dx >= 0f ? 1 : -1;
                            Enter(State.Windup, Windup(Definition.windup * 0.6f));
                        }
                        else
                        {
                            comboLeft = 0;
                            attackCooldown = AttackCooldown;
                            Enter(State.Recover, Definition.recovery * (IsEnraged ? 0.7f : 1f));
                        }
                    }
                    break;

                case State.Recover:
                case State.Stunned:
                case State.Dashing:
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                        Enter(State.Approach, 0f);
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (Definition == null || state == State.Dead)
                return;

            bool grounded = IsGrounded();
            if (grounded)
                airJumpsLeft = MaxAirJumps;

            float decay = grounded ? knockbackDecay : airKnockbackDecay;
            knockbackX = Mathf.MoveTowards(knockbackX, 0f, decay * Time.fixedDeltaTime);

            float vx = aiEnabled && state != State.Stunned && IsOffStage(grounded)
                ? Recover()
                : GuardEdge(DesiredVelocityX(), grounded);
            rb.linearVelocity = new Vector2(vx + knockbackX, rb.linearVelocity.y);
        }

        private float DesiredVelocityX()
        {
            if (state == State.Approach && aiEnabled && target != null)
            {
                float dx = target.transform.position.x - transform.position.x;
                float dy = target.transform.position.y - transform.position.y;
                // Игрок внизу — идём дальше, чтобы сойти с края платформы
                if (dy < -attackHeightTolerance)
                    return facing * MoveSpeed;
                if (Mathf.Abs(dx) > Definition.attackRange * 0.8f)
                    return Mathf.Sign(dx) * MoveSpeed;
            }
            else if (state == State.Dashing)
            {
                return facing * Definition.specialPower;
            }
            return 0f;
        }

        // ---------- Края арены ----------

        /// <summary>Отбросило за край или под арену — пора спасаться.</summary>
        private bool IsOffStage(bool grounded)
        {
            if (grounded)
                return false;
            Vector2 p = transform.position;
            return Mathf.Abs(p.x) > stageHalfWidth || p.y < groundTop - 0.3f;
        }

        /// <summary>Летим к центру арены, на спуске тратим прыжок в воздухе.</summary>
        private float Recover()
        {
            if (airJumpsLeft > 0 && rb.linearVelocity.y < 0f)
            {
                airJumpsLeft--;
                Jump();
            }
            float toCenter = -transform.position.x;
            facing = toCenter >= 0f ? 1 : -1;
            return Mathf.Abs(toCenter) > 0.2f ? facing * MoveSpeed : 0f;
        }

        /// <summary>Не шагаем в пропасть: перед краем останавливаемся или перепрыгиваем, если игрок на той стороне.</summary>
        private float GuardEdge(float vx, bool grounded)
        {
            if (!grounded || Mathf.Abs(vx) < 0.01f || target == null)
                return vx;

            int dir = vx > 0f ? 1 : -1;
            float dy = target.transform.position.y - transform.position.y;
            float depth = dy < -attackHeightTolerance ? chaseDropDepth : safeDropDepth;
            if (GroundAhead(dir, depth))
                return vx;

            if (state == State.Dashing)
            {
                Enter(State.Recover, Definition.recovery);
                return 0f;
            }

            // Игрок по ту сторону ямы — прыгаем через неё
            float dx = target.transform.position.x - transform.position.x;
            bool targetAcross = Mathf.Sign(dx) == dir && Mathf.Abs(dx) > 1f && dy > -attackHeightTolerance;
            if (targetAcross && Definition.canJump && jumpTimer <= 0f)
            {
                jumpTimer = Definition.jumpCooldown;
                Jump();
                return vx;
            }
            return 0f;
        }

        private bool GroundAhead(int dir, float depth)
        {
            Vector2 size = bodyCollider.size;
            Vector2 origin = (Vector2)transform.position + new Vector2(dir * (size.x * 0.5f + 0.3f), -size.y * 0.5f + 0.1f);
            return Physics2D.Raycast(origin, Vector2.down, depth + 0.1f, groundMask).collider != null;
        }

        /// <summary>Короткая вспышка в такт «речи» в катсцене.</summary>
        public void SpeakBlip() => talkFlashTimer = 0.07f;

        /// <summary>Вылет за зону ринг-аута — вирус уничтожен.</summary>
        public void RingOut()
        {
            if (state != State.Dead)
                Health.TakeDamage(Health.Current);
        }

        // Игрок забрался на платформу — запрыгиваем следом (снизу сквозь одностороннюю платформу)
        private bool TryJumpTowardsTarget(float dy, float dist)
        {
            if (!Definition.canJump || jumpTimer > 0f || dy < jumpTriggerHeight || dist > jumpTriggerDistance || !IsGrounded())
                return false;

            jumpTimer = Definition.jumpCooldown;
            Jump();
            return true;
        }

        private void Jump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Definition.jumpVelocity);
            GameEventBus.RaiseEnemyAction(EnemyAction.Jump, transform.position);
        }

        private bool IsGrounded()
        {
            if (rb.linearVelocity.y > 0.1f)
                return false;
            Vector2 size = bodyCollider.size;
            Vector2 feet = (Vector2)transform.position + new Vector2(0f, -size.y * 0.5f);
            return Physics2D.OverlapBox(feet, new Vector2(size.x * 0.8f, 0.1f), 0f, groundMask);
        }

        private bool TryUseSpecial(float dist)
        {
            if (specialTimer > 0f)
                return false;

            switch (Definition.special)
            {
                case VirusSpecial.TrojanDash:
                    if (dist < 2.5f)
                        return false;
                    specialTimer = SpecialCooldown;
                    Enter(State.Dashing, DashDuration);
                    return true;

                case VirusSpecial.RootkitTeleport:
                {
                    specialTimer = SpecialCooldown;
                    GameEventBus.RaiseEnemyAction(EnemyAction.Teleport, transform.position);
                    float behind = target.transform.position.x - target.FacingDirection * 1.4f;
                    float x = Mathf.Clamp(behind, -stageHalfWidth + 0.8f, stageHalfWidth - 0.8f);
                    rb.position = new Vector2(x, rb.position.y);
                    transform.position = rb.position;
                    facing = target.transform.position.x >= x ? 1 : -1;
                    // после телепорта бьёт быстрее обычного — игроку нужно развернуться и поставить блок
                    teleportStrike = true;
                    comboLeft = ComboLength - 1;
                    Enter(State.Windup, Windup(Definition.windup * 0.7f));
                    return true;
                }

                default:
                    return false;
            }
        }

        private void PerformAttack()
        {
            Vector2 delta = target.transform.position - transform.position;
            bool inFront = Mathf.Sign(delta.x) == facing || Mathf.Abs(delta.x) < 0.3f;
            if (!inFront || Mathf.Abs(delta.x) > Definition.attackRange + 0.4f || Mathf.Abs(delta.y) > 1.5f)
                return;

            var hit = new HitInfo(AttackDamage, 0f, new Vector2(facing * Definition.knockback, 0f), Definition.hitStun);
            if (target.ReceiveHit(hit) && Definition.special == VirusSpecial.RansomEncrypt)
                target.DrainEnergy(Definition.specialPower);
        }

        public bool ReceiveHit(in HitInfo hit)
        {
            if (state == State.Dead || Definition == null)
                return false;

            bool heavy = !hit.IsSkill && hit.Knockback.y > 0f;
            if (TryGuard(hit, heavy))
                return false; // заблокировано: ни урона, ни энергии игроку

            if (Time.time - lastHitTime > ComboResetTime)
                comboHits = 0;
            comboHits++;
            lastHitTime = Time.time;

            // Чем меньше HP, тем дальше улетает — добивать можно и падением.
            // На высокой сложности и у босса отброс слабее — ринг-аут нужно заработать
            float scale = PlayerController.KnockbackScale(Health, maxKnockbackScale) * profile.KnockbackTaken * (IsBoss ? 0.7f : 1f);
            knockbackX = hit.Knockback.x * scale;
            if (hit.Knockback.y > 0f)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, hit.Knockback.y * scale);

            // Защита от бесконечного комбо лёгкими ударами: вирус перестаёт сбиваться с замаха
            bool armored = !hit.IsSkill && comboHits > Definition.comboArmorHits;
            if (!armored && hit.StunDuration > 0f)
            {
                comboLeft = 0;
                Enter(State.Stunned, hit.IsSkill ? hit.StunDuration : hit.StunDuration * profile.StunTaken);
            }

            float fraction = hit.MaxHealthFraction * (IsBoss ? profile.BossSkillTaken : 1f);
            if (hit.Damage > 0f || fraction > 0f)
            {
                hitFlashTimer = 0.08f;
                CombatHitKind kind = hit.IsSkill ? CombatHitKind.Skill : heavy ? CombatHitKind.Heavy : CombatHitKind.Light;
                GameEventBus.RaiseCombatHit(kind, transform.position);
            }

            Health.TakeDamage(hit.Damage + fraction * Health.Max);

            if (IsBoss && !IsEnraged && !Health.IsDead && Health.Normalized <= enrageThreshold)
                Enrage();
            return true;
        }

        /// <summary>
        /// Блок вируса: лёгкий удар спереди, пока вирус не занят атакой. Тяжёлый удар и навыки пробивают блок —
        /// на высокой сложности приходится чередовать удары.
        /// </summary>
        private bool TryGuard(in HitInfo hit, bool heavy)
        {
            if (hit.IsSkill || heavy || GuardChance <= 0f)
                return false;
            if (state != State.Approach && state != State.Recover && state != State.Idle)
                return false;

            // Удар пришёл спереди, если толкает вирус назад — от игрока
            bool fromFront = hit.Knockback.x == 0f || Mathf.Sign(hit.Knockback.x) == -facing;
            if (!fromFront || Random.value > GuardChance)
                return false;

            knockbackX = hit.Knockback.x * 0.3f;
            guardFlashTimer = 0.15f;
            GameEventBus.RaiseCombatHit(CombatHitKind.EnemyBlocked, transform.position);
            return true;
        }

        /// <summary>Ярость босса: ударная волна отталкивает игрока, дальше бой быстрее и злее.</summary>
        private void Enrage()
        {
            IsEnraged = true;
            specialTimer = Mathf.Min(specialTimer, 1f);
            GameEventBus.RaiseEnemyAction(EnemyAction.Enrage, transform.position);

            Vector2 delta = target.transform.position - transform.position;
            if (delta.magnitude <= enrageShockwaveRadius)
            {
                float dir = delta.x >= 0f ? 1f : -1f;
                target.ReceiveHit(new HitInfo(AttackDamage * 0.5f, 0f, new Vector2(dir * enrageShockwaveForce, 0f), 0.35f));
            }
        }

        private void Enter(State next, float duration)
        {
            if (next == State.Windup)
                GameEventBus.RaiseEnemyAction(EnemyAction.Windup, transform.position);
            else if (next == State.Dashing)
                GameEventBus.RaiseEnemyAction(EnemyAction.Dash, transform.position);

            restartClip = next != state || next == State.Windup || next == State.Stunned;
            state = next;
            stateTimer = duration;
            stateDuration = duration;
        }

        // ---------- Визуал ----------

        private void UpdateSprite()
        {
            SpriteClip clip;
            float duration = 0f;

            switch (state)
            {
                case State.Windup:
                    // Кадр удара совпадает с концом замаха
                    clip = teleportStrike ? animations.Super : animations.LightAttack;
                    duration = stateDuration + 0.1f;
                    break;
                case State.Recover:
                    clip = animations.Idle;
                    break;
                case State.Stunned:
                    clip = animations.Hurt;
                    duration = stateDuration;
                    break;
                case State.Dashing:
                    clip = animations.Super;
                    duration = stateDuration;
                    break;
                case State.Approach:
                    clip = Mathf.Abs(DesiredVelocityX()) > 0.1f ? animations.Move : animations.Idle;
                    break;
                default:
                    clip = animations.Idle;
                    break;
            }

            spriteAnimator.Play(clip, duration, restartClip);
            restartClip = false;
            spriteAnimator.SetFacing(facing, animations.facesRight);

            Color c = Definition.spriteTint;
            if (IsEnraged)
                c *= Color.Lerp(Color.white, EnrageTint, 0.35f + 0.15f * Mathf.Sin(Time.time * 6f));
            if (state == State.Windup)
                c *= Color.Lerp(Color.white, SpriteWindupTint, Mathf.PingPong(Time.time * 10f, 1f));
            else if (state == State.Stunned)
                c *= Color.Lerp(Color.white, StunTint, 0.5f);
            if (guardFlashTimer > 0f)
                c = Color.Lerp(c, GuardTint, 0.7f);
            if (talkFlashTimer > 0f)
                c = Color.Lerp(c, Color.white, 0.45f);
            if (hitFlashTimer > 0f)
                c = SpriteHitTint;
            spriteAnimator.Renderer.color = c;
        }

        private void UpdateBlock()
        {
            if (body == null)
                return;

            Color c = Definition.color;
            if (IsEnraged)
                c = Color.Lerp(c, EnrageTint, 0.35f + 0.15f * Mathf.Sin(Time.time * 6f));
            if (state == State.Windup)
                c = Color.Lerp(c, Color.white, Mathf.PingPong(Time.time * 10f, 1f));
            else if (state == State.Stunned)
                c = Color.Lerp(c, StunTint, 0.6f);
            if (guardFlashTimer > 0f)
                c = Color.Lerp(c, GuardTint, 0.7f);
            if (talkFlashTimer > 0f)
                c = Color.Lerp(c, Color.white, 0.45f);
            if (hitFlashTimer > 0f)
                c = Color.white;
            body.color = c;

            if (eye != null)
            {
                Vector3 p = eye.localPosition;
                eye.localPosition = new Vector3(Mathf.Abs(p.x) * facing, p.y, p.z);
            }
        }

        private void HandleDied(Health _)
        {
            state = State.Dead;
            Active.Remove(this);
            bodyCollider.enabled = false;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            GameEventBus.RaiseEnemyAction(EnemyAction.Defeated, transform.position);
            StartCoroutine(animations != null ? SpriteDeath() : BlockDeath());
        }

        private IEnumerator SpriteDeath()
        {
            spriteAnimator.Play(animations.Death, spriteDeathFadeTime * 0.6f, true);
            SpriteRenderer sr = spriteAnimator.Renderer;
            Color baseColor = Definition.spriteTint;

            for (float t = 0f; t < spriteDeathFadeTime; t += Time.deltaTime)
            {
                // Первые 60% — анимация смерти, потом растворение
                float k = Mathf.Clamp01((t / spriteDeathFadeTime - 0.6f) / 0.4f);
                Color c = baseColor;
                c.a = 1f - k;
                sr.color = c;
                yield return null;
            }

            Destroy(gameObject);
        }

        private IEnumerator BlockDeath()
        {
            Transform bodyTransform = body != null ? body.transform : transform;
            Vector3 startScale = bodyTransform.localScale;
            SpriteRenderer eyeRenderer = eye != null ? eye.GetComponent<SpriteRenderer>() : null;

            for (float t = 0f; t < deathFadeTime; t += Time.deltaTime)
            {
                float k = 1f - t / deathFadeTime;
                if (body != null)
                {
                    Color c = Color.white;
                    c.a = k;
                    body.color = c;
                }
                if (eyeRenderer != null)
                {
                    Color e = eyeRenderer.color;
                    e.a = k;
                    eyeRenderer.color = e;
                }
                bodyTransform.localScale = new Vector3(startScale.x * (1f + (1f - k) * 0.5f), startScale.y * k, 1f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
