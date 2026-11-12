using System;
using UnityEngine;

namespace CyberDefender.Visuals
{
    [Serializable]
    public sealed class SpriteClip
    {
        public string name;
        public Sprite[] frames = Array.Empty<Sprite>();
        public float fps = 10f;
        public bool loop;

        public bool IsValid => frames != null && frames.Length > 0;
    }

    /// <summary>
    /// Набор анимаций бойца. Создаётся меню CyberDefender → Import Sprite Pack и назначается
    /// в CharacterDefinition / VirusDefinition. Пустые клипы заменяются ближайшими по смыслу.
    /// </summary>
    [CreateAssetMenu(menuName = "CyberDefender/Sprite Animation Set", fileName = "Animations")]
    public sealed class SpriteAnimationSet : ScriptableObject
    {
        public SpriteClip idle = new SpriteClip { loop = true, fps = 8f };
        public SpriteClip move = new SpriteClip { loop = true, fps = 12f };
        public SpriteClip jump = new SpriteClip();
        public SpriteClip lightAttack = new SpriteClip();
        public SpriteClip heavyAttack = new SpriteClip();
        public SpriteClip block = new SpriteClip();
        public SpriteClip hurt = new SpriteClip();
        public SpriteClip super = new SpriteClip();
        public SpriteClip death = new SpriteClip();

        [Header("Placement")]
        [Tooltip("Смотрит ли персонаж на исходных кадрах вправо")]
        public bool facesRight = true;
        public float scale = 1f;
        [Tooltip("Смещение спрайта от нижней точки коллайдера (ступни), для взгляда вправо; x зеркалится при повороте")]
        public Vector2 offset;

        public SpriteClip Idle => idle;
        public SpriteClip Move => Pick(move, idle);
        public SpriteClip Jump => Pick(jump, idle);
        public SpriteClip LightAttack => Pick(lightAttack, heavyAttack, idle);
        public SpriteClip HeavyAttack => Pick(heavyAttack, lightAttack, idle);
        public SpriteClip Block => Pick(block, idle);
        public SpriteClip Hurt => Pick(hurt, idle);
        public SpriteClip Super => Pick(super, heavyAttack, lightAttack, idle);
        public SpriteClip Death => Pick(death, hurt, idle);

        public bool IsUsable => idle != null && idle.IsValid;

        private static SpriteClip Pick(params SpriteClip[] candidates)
        {
            foreach (SpriteClip clip in candidates)
            {
                if (clip != null && clip.IsValid)
                    return clip;
            }
            return null;
        }
    }
}
