using CyberDefender.Combat;
using CyberDefender.Core;
using UnityEngine;

namespace CyberDefender.Player
{
    /// <summary>
    /// FSM персонажа. Не знает, откуда пришла команда (камера, микрофон, клавиатура) —
    /// получает только события GameEventBus. Команды исполняются в том же кадре, в котором пришли.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(BoxCollider2D))]
    public sealed class PlayerController : MonoBehaviour, IHittable
    {
        private enum Command : byte { None, Move, Jump, LightAttack, HeavyAttack, Block }

        private const float MaxEnergy = 100f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpVelocity = 12f;
        [SerializeField, Tooltip("Длительность шага от одиночного свайпа")]
        private float swipeStepDuration = 0.35f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.5f);
        [SerializeField] private float groundCheckRadius = 0.1f;
        [SerializeField, Tooltip("Дополнительные прыжки в воздухе")] private int maxAirJumps = 1;
        [SerializeField, Tooltip("Тяжёлый удар в воздухе подбрасывает вверх — спасение после вылета с арены. Раз за полёт")]
        private float recoveryVelocity = 10f;

        [Header("Attacks")]
        [SerializeField] private float lightAttackDuration = 0.25f;
        [SerializeField] private float lightAttackDamage = 5f;
        [SerializeField] private float heavyAttackDuration = 0.55f;
        [SerializeField] private float heavyAttackDamage = 12f;
        [SerializeField] private float heavyPushForce = 6f;
        [SerializeField, Tooltip("Сколько секунд команда, пришедшая во время анимации, ждёт исполнения")]
        private float inputBufferWindow = 0.15f;

        [Header("Defense")]
        [SerializeField, Range(0f, 1f)] private float blockDamageMultiplier = 0.2f;
        [SerializeField, Range(0f, 1f)] private float blockPushbackMultiplier = 0.4f;
        [SerializeField] private float knockbackDecay = 25f;
        [SerializeField, Tooltip("В воздухе отброс гаснет медленнее — так и вылетают за край")]
        private float airKnockbackDecay = 12f;
        [SerializeField, Tooltip("Множитель отброса при 0 HP (при полном HP — ×1)")]
        private float maxKnockbackScale = 2.5f;

        [Header("Kernel Energy")]
        [SerializeField] private float energyPerHit = 8f;
        [SerializeField] private float energyPerBlock = 5f;
        [SerializeField] private float energyGainMultiplier = 1f;

        [Header("Voice Skills")]
        [SerializeField, Range(0f, 1f)] private float purgeDamageFraction = 0.35f;
        [SerializeField] private float purgeDuration = 1.2f;
        [SerializeField] private float firewallDuration = 5f;
        [SerializeField] private float isolateStunDuration = 2.5f;
        [SerializeField] private float isolateCastDuration = 0.4f;

        [Header("Recognition")]
        [SerializeField, Range(0f, 1f)] private float minGestureConfidence = 0.7f;
        [SerializeField, Range(0f, 1f)] private float minVoiceConfidence = 0.6f;

        public PlayerState State { get; private set; } = PlayerState.Idle;
        public Health Health { get; private set; }
        public CharacterDefinition Character { get; private set; }
        public Vector2 BodySize => bodyCollider.size;
        /// <summary>Полная длительность текущего таймового состояния (атака, стан, супер-приём).</summary>
        public float StateDuration { get; private set; }
        public float KernelEnergy { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsFirewallActive => firewallTimer > 0f;
        /// <summary>Неуязвимость после восстановления из бэкапа.</summary>
        public bool IsInvulnerable => invulnerableTimer > 0f;
        public int FacingDirection { get; private set; } = 1;
        public bool ControlEnabled { get; private set; }

        private Rigidbody2D rb;
        private BoxCollider2D bodyCollider;
        private float stateTimer;
        private float firewallTimer;
        private float invulnerableTimer;
        private float knockbackX;
        private int airJumpsLeft;
        private bool recoveryUsed;
        private int moveDirection;
        private bool moveHeld;
        private bool blockHeld;
        private Command bufferedCommand;
        private float bufferedUntil;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
            Health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            GameEventBus.OnGestureDetected += HandleGesture;
            GameEventBus.OnVoiceCommandTriggered += HandleVoiceCommand;
            Health.Died += HandleDied;
        }

        private void OnDisable()
        {
            GameEventBus.OnGestureDetected -= HandleGesture;
            GameEventBus.OnVoiceCommandTriggered -= HandleVoiceCommand;
            Health.Died -= HandleDied;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (firewallTimer > 0f)
                firewallTimer -= dt;
            if (invulnerableTimer > 0f)
                invulnerableTimer -= dt;

            if (stateTimer > 0f)
            {
                stateTimer -= dt;
                if (stateTimer <= 0f)
                    OnStateTimerExpired();
            }
        }

        private void FixedUpdate()
        {
            // При прыжке сквозь платформу снизу ноги касаются её коллайдера — это ещё не «стоим»
            IsGrounded = rb.linearVelocity.y <= 0.1f
                && Physics2D.OverlapCircle((Vector2)transform.position + groundCheckOffset, groundCheckRadius, groundMask);

            if (IsGrounded)
            {
                airJumpsLeft = maxAirJumps;
                recoveryUsed = false;
            }

            float decay = IsGrounded ? knockbackDecay : airKnockbackDecay;
            knockbackX = Mathf.MoveTowards(knockbackX, 0f, decay * Time.fixedDeltaTime);
            float vx = State == PlayerState.Move ? moveDirection * moveSpeed : 0f;
            rb.linearVelocity = new Vector2(vx + knockbackX, rb.linearVelocity.y);
        }

        // ---------- Входящие события ----------

        private void HandleGesture(GestureEvent e)
        {
            if (!ControlEnabled || e.Confidence < minGestureConfidence)
                return;

            bool started = e.Phase == GesturePhase.Started;

            switch (e.Type)
            {
                case GestureType.Fist:
                    blockHeld = started;
                    if (started)
                        TryExecute(Command.Block);
                    else if (State == PlayerState.Block)
                        ChangeState(ResolveRestState());
                    break;

                case GestureType.SwipeLeft:
                case GestureType.SwipeRight:
                    if (started)
                    {
                        moveDirection = e.Type == GestureType.SwipeLeft ? -1 : 1;
                        moveHeld = true;
                        TryExecute(Command.Move);
                    }
                    else
                    {
                        moveHeld = false;
                        // шаг от свайпа уже отработал минимальную длительность — останавливаемся
                        if (State == PlayerState.Move && stateTimer <= 0f)
                            ChangeState(ResolveRestState());
                    }
                    break;

                case GestureType.OpenPalm:
                    if (started) TryExecute(Command.LightAttack);
                    break;

                case GestureType.DoublePalm:
                    if (started) TryExecute(Command.HeavyAttack);
                    break;

                case GestureType.PointUp:
                    if (started) TryExecute(Command.Jump);
                    break;
            }
        }

        private void HandleVoiceCommand(VoiceCommandEvent e)
        {
            if (!ControlEnabled || e.Confidence < minVoiceConfidence || KernelEnergy < MaxEnergy)
                return;
            if (State == PlayerState.Stun || State == PlayerState.SuperSkill)
                return;

            switch (e.Command)
            {
                case VoiceCommand.PurgeSystem:
                    ConsumeEnergy();
                    EnterTimedState(PlayerState.SuperSkill, purgeDuration);
                    GameEventBus.RaisePlayerSkillUsed(new PlayerSkillEvent(e.Command, purgeDamageFraction, purgeDuration));
                    break;

                case VoiceCommand.FirewallActivate:
                    // Щит — бафф, а не состояние: игрок продолжает драться под ним
                    ConsumeEnergy();
                    firewallTimer = firewallDuration;
                    GameEventBus.RaisePlayerSkillUsed(new PlayerSkillEvent(e.Command, 0f, firewallDuration));
                    break;

                case VoiceCommand.Isolate:
                    ConsumeEnergy();
                    EnterTimedState(PlayerState.SuperSkill, isolateCastDuration);
                    GameEventBus.RaisePlayerSkillUsed(new PlayerSkillEvent(e.Command, 0f, isolateStunDuration));
                    break;
            }
        }

        private void HandleDied(Health _)
        {
            SetControlEnabled(false);
            stateTimer = 0f;
            ChangeState(PlayerState.Stun);
        }

        // ---------- Публичный API ----------

        public bool ReceiveHit(in HitInfo hit)
        {
            if (Health.IsDead || IsFirewallActive || IsInvulnerable)
                return false;

            float damage = hit.Damage + hit.MaxHealthFraction * Health.Max;

            if (State == PlayerState.Block)
            {
                AddEnergy(energyPerBlock * energyGainMultiplier * GameSettings.Profile.EnergyGain);
                Health.TakeDamage(damage * blockDamageMultiplier);
                knockbackX = hit.Knockback.x * blockPushbackMultiplier;
                GameEventBus.RaiseCombatHit(CombatHitKind.PlayerBlocked, transform.position);
                return true;
            }

            Health.TakeDamage(damage);
            GameEventBus.RaiseCombatHit(CombatHitKind.PlayerHurt, transform.position);
            // Чем меньше HP, тем дальше улетаешь
            float scale = KnockbackScale(Health, maxKnockbackScale);
            knockbackX = hit.Knockback.x * scale;
            if (hit.Knockback.y > 0f)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, hit.Knockback.y * scale);

            if (!Health.IsDead && hit.StunDuration > 0f)
                EnterTimedState(PlayerState.Stun, hit.StunDuration);

            return true;
        }

        /// <summary>Вызывается боевой системой, когда атака игрока попала по врагу.</summary>
        public void RegisterHitLanded() => AddEnergy(energyPerHit * energyGainMultiplier * GameSettings.Profile.EnergyGain);

        /// <summary>Применяет характеристики выбранного защитника. Безопасно вызывать в любой момент (превью в меню).</summary>
        public void ApplyCharacter(CharacterDefinition character)
        {
            if (character == null)
                return;

            Character = character;
            moveSpeed = character.moveSpeed;
            jumpVelocity = character.jumpVelocity;
            lightAttackDamage = character.lightDamage;
            lightAttackDuration = character.lightDuration;
            heavyAttackDamage = character.heavyDamage;
            heavyAttackDuration = character.heavyDuration;
            heavyPushForce = character.heavyPush;
            blockDamageMultiplier = character.blockDamageMultiplier;
            energyGainMultiplier = character.energyGainMultiplier;
            firewallDuration = character.firewallDuration;
            purgeDamageFraction = character.purgeDamageFraction;

            // Размер задаём коллайдером, а не transform.scale — иначе исказятся спрайты
            bodyCollider.size = character.size;
            groundCheckOffset = new Vector2(0f, -character.size.y * 0.5f - 0.02f);
            Health.SetMax(character.maxHealth);
        }

        /// <summary>Ransomware «шифрует» часть накопленной энергии.</summary>
        public void DrainEnergy(float amount)
        {
            if (amount <= 0f || KernelEnergy <= 0f)
                return;

            bool wasFull = KernelEnergy >= MaxEnergy;
            KernelEnergy = Mathf.Max(0f, KernelEnergy - amount);
            GameEventBus.RaiseKernelEnergyChanged(KernelEnergy / MaxEnergy);
            if (wasFull)
                GameEventBus.RaiseVoiceListeningChanged(false);
        }

        public void DebugFillEnergy() => AddEnergy(MaxEnergy);

        /// <summary>Поворот к противнику, как в классических файтингах. Во время атаки не разворачиваемся.</summary>
        public void FaceTowards(float worldX)
        {
            if (State == PlayerState.Idle || State == PlayerState.Move || State == PlayerState.Block)
                FacingDirection = worldX >= transform.position.x ? 1 : -1;
        }

        public void SetControlEnabled(bool value)
        {
            ControlEnabled = value;
            if (value)
                return;

            moveHeld = false;
            blockHeld = false;
            bufferedCommand = Command.None;
            if (State == PlayerState.Move || State == PlayerState.Block)
                ChangeState(PlayerState.Idle);
        }

        public void PlaceAt(Vector2 position)
        {
            transform.position = position;
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
            knockbackX = 0f;
        }

        /// <summary>Восстановление из бэкапа после вылета или гибели: полное HP, короткая неуязвимость.</summary>
        public void Respawn(Vector2 position, float invulnerableDuration)
        {
            PlaceAt(position);
            Health.ResetHealth();
            stateTimer = 0f;
            invulnerableTimer = invulnerableDuration;
            airJumpsLeft = maxAirJumps;
            recoveryUsed = false;
            ChangeState(PlayerState.Idle);
            SetControlEnabled(true);
            GameEventBus.RaisePlayerRespawned();
        }

        public static float KnockbackScale(Health health, float maxScale)
        {
            return 1f + (1f - health.Normalized) * (maxScale - 1f);
        }

        public void ResetForNewRun(Vector2 position)
        {
            PlaceAt(position);
            Health.ResetHealth();
            stateTimer = 0f;
            firewallTimer = 0f;
            invulnerableTimer = 0f;
            FacingDirection = 1;
            ChangeState(PlayerState.Idle);

            KernelEnergy = 0f;
            GameEventBus.RaiseKernelEnergyChanged(0f);
            GameEventBus.RaiseVoiceListeningChanged(false);
        }

        // ---------- FSM ----------

        private void TryExecute(Command command)
        {
            if (CanExecute(command))
            {
                Execute(command);
                return;
            }

            // Удары и прыжок запоминаем, чтобы они сработали сразу по окончании текущего действия.
            // Блок и движение не буферизуются — они восстанавливаются из удерживаемых флагов.
            if (command == Command.LightAttack || command == Command.HeavyAttack || command == Command.Jump)
            {
                bufferedCommand = command;
                bufferedUntil = Time.time + inputBufferWindow;
            }
        }

        private bool CanExecute(Command command)
        {
            switch (State)
            {
                case PlayerState.Idle:
                case PlayerState.Move:
                    return true;
                case PlayerState.Block:
                    return command == Command.Block;
                default: // Attack, Stun, SuperSkill
                    return false;
            }
        }

        private void Execute(Command command)
        {
            switch (command)
            {
                case Command.Move:
                    FacingDirection = moveDirection;
                    EnterTimedState(PlayerState.Move, swipeStepDuration);
                    break;

                case Command.Jump:
                    if (IsGrounded)
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
                        GameEventBus.RaisePlayerJumped(false);
                    }
                    else if (airJumpsLeft > 0)
                    {
                        airJumpsLeft--;
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity * 0.9f);
                        GameEventBus.RaisePlayerJumped(true);
                    }
                    break;

                case Command.LightAttack:
                    StartAttack(AttackKind.Light, lightAttackDuration, lightAttackDamage, 0f);
                    break;

                case Command.HeavyAttack:
                    if (!IsGrounded && !recoveryUsed)
                    {
                        recoveryUsed = true;
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, recoveryVelocity));
                        GameEventBus.RaisePlayerJumped(true);
                    }
                    StartAttack(AttackKind.Heavy, heavyAttackDuration, heavyAttackDamage, heavyPushForce);
                    break;

                case Command.Block:
                    stateTimer = 0f;
                    ChangeState(PlayerState.Block);
                    break;
            }
        }

        private void StartAttack(AttackKind kind, float duration, float damage, float pushForce)
        {
            EnterTimedState(PlayerState.Attack, duration);
            GameEventBus.RaisePlayerAttack(new PlayerAttackEvent(kind, damage, pushForce, FacingDirection));
        }

        private void OnStateTimerExpired()
        {
            switch (State)
            {
                case PlayerState.Move:
                    if (!moveHeld)
                        ChangeState(ResolveRestState());
                    break;

                case PlayerState.Attack:
                case PlayerState.Stun:
                case PlayerState.SuperSkill:
                    ChangeState(ResolveRestState());
                    ConsumeBufferedCommand();
                    break;
            }
        }

        private PlayerState ResolveRestState()
        {
            if (blockHeld) return PlayerState.Block;
            if (moveHeld) return PlayerState.Move;
            return PlayerState.Idle;
        }

        private void ConsumeBufferedCommand()
        {
            if (bufferedCommand == Command.None)
                return;

            Command command = bufferedCommand;
            bufferedCommand = Command.None;

            if (Time.time <= bufferedUntil)
                TryExecute(command);
        }

        private void EnterTimedState(PlayerState state, float duration)
        {
            stateTimer = duration;
            StateDuration = duration;
            ChangeState(state);
        }

        private void ChangeState(PlayerState next)
        {
            if (next == State)
                return;

            PlayerState previous = State;
            State = next;
            GameEventBus.RaisePlayerStateChanged(previous, next);
        }

        // ---------- Kernel Energy ----------

        private void AddEnergy(float amount)
        {
            if (KernelEnergy >= MaxEnergy)
                return;

            KernelEnergy = Mathf.Min(KernelEnergy + amount, MaxEnergy);
            GameEventBus.RaiseKernelEnergyChanged(KernelEnergy / MaxEnergy);

            if (KernelEnergy >= MaxEnergy)
                GameEventBus.RaiseVoiceListeningChanged(true);
        }

        private void ConsumeEnergy()
        {
            KernelEnergy = 0f;
            GameEventBus.RaiseKernelEnergyChanged(0f);
            GameEventBus.RaiseVoiceListeningChanged(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere((Vector2)transform.position + groundCheckOffset, groundCheckRadius);
        }
#endif
    }
}
