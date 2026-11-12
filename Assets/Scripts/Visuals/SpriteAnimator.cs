using UnityEngine;

namespace CyberDefender.Visuals
{
    /// <summary>
    /// Лёгкий покадровый проигрыватель без Animator Controller: состояние бойца
    /// напрямую выбирает клип, а длительность атак подгоняется под игровые тайминги.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private SpriteClip current;
        private float time;
        private float fps;
        private Vector2 offset;

        public SpriteRenderer Renderer => spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>Положение и масштаб относительно бойца; offset.x задаётся для взгляда вправо и зеркалится при повороте.</summary>
        public void SetPlacement(Vector2 offset, float scale)
        {
            this.offset = offset;
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        }

        /// <param name="duration">Если &gt; 0 — клип растягивается ровно на это время (для атак).</param>
        /// <param name="restart">Начать заново, даже если этот клип уже играет (повторный удар).</param>
        public void Play(SpriteClip clip, float duration = 0f, bool restart = false)
        {
            if (clip == null || !clip.IsValid)
                return;
            if (clip == current && !restart)
                return;

            current = clip;
            time = 0f;
            fps = duration > 0f ? clip.frames.Length / duration : Mathf.Max(1f, clip.fps);
            spriteRenderer.sprite = clip.frames[0];
        }

        public void SetFacing(int direction, bool facesRight)
        {
            bool flipped = facesRight ? direction < 0 : direction > 0;
            spriteRenderer.flipX = flipped;
            // Персонаж в кадре часто стоит не по центру — зеркалим смещение вместе со спрайтом
            transform.localPosition = new Vector3(flipped ? -offset.x : offset.x, offset.y, 0f);
        }

        private void Update()
        {
            if (current == null)
                return;

            time += Time.deltaTime;
            int length = current.frames.Length;
            int frame = (int)(time * fps);
            frame = current.loop ? frame % length : Mathf.Min(frame, length - 1);
            spriteRenderer.sprite = current.frames[frame];
        }
    }
}
