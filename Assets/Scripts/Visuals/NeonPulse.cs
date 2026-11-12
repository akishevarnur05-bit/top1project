using UnityEngine;

namespace CyberDefender.Visuals
{
    /// <summary>Плавно «дышит» прозрачностью спрайта — неоновые края платформ и ореолы.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class NeonPulse : MonoBehaviour
    {
        [SerializeField] private float baseAlpha = 0.35f;
        [SerializeField] private float amplitude = 0.12f;
        [SerializeField] private float speed = 2f;
        [SerializeField] private float phase;

        private SpriteRenderer sr;

        public void Configure(float baseAlpha, float amplitude, float speed, float phase)
        {
            this.baseAlpha = baseAlpha;
            this.amplitude = amplitude;
            this.speed = speed;
            this.phase = phase;
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            Color c = sr.color;
            c.a = baseAlpha + amplitude * Mathf.Sin(Time.time * speed + phase);
            sr.color = c;
        }
    }
}
