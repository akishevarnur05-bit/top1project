using CyberDefender.Combat;
using CyberDefender.Core;
using CyberDefender.Visuals;
using UnityEngine;

namespace CyberDefender.Player
{
    /// <summary>
    /// Внешний вид игрока. Если у героя назначен SpriteAnimationSet — пиксель-арт анимации по состоянию FSM,
    /// иначе программерский блок с визором. Щит Firewall рисуется в обоих режимах.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerVisuals : MonoBehaviour
    {
        [Header("Block Fallback")]
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer visor;

        [Header("Pixel Art")]
        [SerializeField] private SpriteAnimator spriteAnimator;

        [Header("Effects")]
        [SerializeField] private SpriteRenderer shield;

        [Header("Block Colors")]
        [SerializeField] private Color idleColor = new Color(0.2f, 1f, 0.45f);
        [SerializeField] private Color attackColor = new Color(1f, 0.9f, 0.2f);
        [SerializeField] private Color blockColor = new Color(0.6f, 0.8f, 1f);
        [SerializeField] private Color stunColor = new Color(0.45f, 0.45f, 0.45f);
        [SerializeField] private Color superColor = new Color(1f, 0.3f, 1f);

        [Header("Sprite Tints")]
        [SerializeField] private Color spriteHitTint = new Color(1f, 0.45f, 0.45f);
        [SerializeField] private Color spriteBlockTint = new Color(0.65f, 0.85f, 1f);
        [SerializeField] private Color spriteSuperTint = new Color(1f, 0.7f, 1f);

        private PlayerController controller;
        private CharacterDefinition appliedCharacter;
        private SpriteAnimationSet animations;
        private AttackKind lastAttack;
        private bool restartAttackClip;
        private float flashTimer;
        private float visorOffsetX;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (visor != null)
                visorOffsetX = Mathf.Abs(visor.transform.localPosition.x);
        }

        private void OnEnable()
        {
            GameEventBus.OnPlayerAttack += HandleAttack;
        }

        private void OnDisable()
        {
            GameEventBus.OnPlayerAttack -= HandleAttack;
        }

        private void Start()
        {
            controller.Health.Damaged += HandleDamaged;
            Configure();
        }

        private void OnDestroy()
        {
            if (controller != null && controller.Health != null)
                controller.Health.Damaged -= HandleDamaged;
        }

        private void HandleDamaged(Health _, float amount)
        {
            if (amount > 0f)
                flashTimer = 0.1f;
        }

        private void HandleAttack(PlayerAttackEvent e)
        {
            lastAttack = e.Kind;
            restartAttackClip = true; // два одинаковых удара подряд — клип с первого кадра
        }

        private void LateUpdate()
        {
            if (controller.Character != appliedCharacter)
                Configure();

            if (flashTimer > 0f)
                flashTimer -= Time.deltaTime;

            if (animations != null)
                UpdateSprite();
            else
                UpdateBlock();

            UpdateShield();
        }

        /// <summary>Перестраивает визуал под выбранного героя: размер блока, позиция и масштаб спрайта.</summary>
        private void Configure()
        {
            appliedCharacter = controller.Character;
            animations = appliedCharacter != null && appliedCharacter.animations != null && appliedCharacter.animations.IsUsable
                ? appliedCharacter.animations
                : null;

            Vector2 size = controller.BodySize;
            bool useSprites = animations != null;

            if (body != null)
            {
                body.enabled = !useSprites;
                body.transform.localScale = new Vector3(size.x, size.y, 1f);
            }
            if (visor != null)
                visor.enabled = !useSprites;

            if (spriteAnimator != null)
            {
                spriteAnimator.Renderer.enabled = useSprites;
                if (useSprites)
                {
                    // Пивот кадров — снизу по центру, ставим «ступни» на низ коллайдера
                    spriteAnimator.SetPlacement(new Vector2(animations.offset.x, -size.y * 0.5f + animations.offset.y), animations.scale);
                    spriteAnimator.Play(animations.Idle, 0f, true);
                }
            }
        }

        private void UpdateSprite()
        {
            SpriteClip clip;
            float duration = 0f;
            bool restart = false;
            PlayerState state = controller.State;

            if (controller.Health.IsDead)
            {
                clip = animations.Death;
            }
            else
            {
                switch (state)
                {
                    case PlayerState.Attack:
                        clip = lastAttack == AttackKind.Light ? animations.LightAttack : animations.HeavyAttack;
                        duration = controller.StateDuration;
                        restart = restartAttackClip;
                        break;
                    case PlayerState.SuperSkill:
                        clip = animations.Super;
                        duration = controller.StateDuration;
                        break;
                    case PlayerState.Block:
                        clip = animations.Block;
                        break;
                    case PlayerState.Stun:
                        clip = animations.Hurt;
                        duration = controller.StateDuration;
                        break;
                    case PlayerState.Move:
                        clip = controller.IsGrounded ? animations.Move : animations.Jump;
                        break;
                    default:
                        clip = controller.IsGrounded ? animations.Idle : animations.Jump;
                        break;
                }
            }

            restartAttackClip = false;
            spriteAnimator.Play(clip, duration, restart);
            spriteAnimator.SetFacing(controller.FacingDirection, animations.facesRight);

            Color tint = Color.white;
            if (flashTimer > 0f) tint = spriteHitTint;
            else if (state == PlayerState.Block) tint = spriteBlockTint;
            else if (state == PlayerState.SuperSkill) tint = spriteSuperTint;
            tint.a = InvulnerableAlpha();
            spriteAnimator.Renderer.color = tint;
        }

        private void UpdateBlock()
        {
            if (body != null)
            {
                Color c = flashTimer > 0f ? Color.white : BlockColorFor(controller.State);
                c.a = InvulnerableAlpha();
                body.color = c;
            }

            if (visor != null)
            {
                if (controller.Character != null)
                    visor.color = controller.Character.visorColor;
                Vector3 p = visor.transform.localPosition;
                visor.transform.localPosition = new Vector3(visorOffsetX * controller.FacingDirection, p.y, p.z);
            }
        }

        private void UpdateShield()
        {
            if (shield == null)
                return;

            bool active = controller.IsFirewallActive;
            shield.enabled = active;
            if (active)
            {
                Color c = shield.color;
                c.a = 0.25f + 0.15f * Mathf.Sin(Time.time * 10f);
                shield.color = c;
            }
        }

        /// <summary>Мигание после восстановления из бэкапа.</summary>
        private float InvulnerableAlpha()
        {
            return controller.IsInvulnerable && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f ? 0.35f : 1f;
        }

        private Color BlockColorFor(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Attack: return attackColor;
                case PlayerState.Block: return blockColor;
                case PlayerState.Stun: return stunColor;
                case PlayerState.SuperSkill: return superColor;
                default: return controller.Character != null ? controller.Character.color : idleColor;
            }
        }
    }
}
