using UnityEngine;

namespace CyberDefender.Visuals
{
    /// <summary>
    /// Спрайты, сгенерированные в коде, для неонового «цифрового» стиля. Каждый — 1×1 юнит, размер задаётся масштабом.
    /// Свечение имитируется мягкими полупрозрачными ореолами — не зависит от постобработки URP.
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite square;
        private static Sprite softDot;
        private static Sprite glowLine;
        private static Sprite gradient;
        private static Sprite ring;
        private static Sprite grid;

        /// <summary>Сплошной белый квадрат.</summary>
        public static Sprite Square => square != null ? square : (square = Create(4, FilterMode.Point, (u, v) => 1f));

        /// <summary>Мягкое круглое пятно — ореол вокруг точек и импульсов.</summary>
        public static Sprite SoftDot => softDot != null ? softDot : (softDot = Create(64, FilterMode.Bilinear, (u, v) =>
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            return (1f - d) * (1f - d);
        }));

        /// <summary>Горизонтальная полоса свечения: ярко в центре по высоте, к краям гаснет.</summary>
        public static Sprite GlowLine => glowLine != null ? glowLine : (glowLine = Create(64, FilterMode.Bilinear, (u, v) =>
        {
            float d = 1f - Mathf.Abs(v * 2f - 1f);
            return d * d;
        }));

        /// <summary>Вертикальный градиент: сверху непрозрачно, снизу прозрачно.</summary>
        public static Sprite Gradient => gradient != null ? gradient : (gradient = Create(64, FilterMode.Bilinear, (u, v) => v * v));

        /// <summary>Тонкое кольцо с мягкими краями.</summary>
        public static Sprite Ring => ring != null ? ring : (ring = Create(128, FilterMode.Bilinear, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.92f) / 0.06f);
        }));

        /// <summary>Клетка сетки: линии по левому и нижнему краю. Рисуется в режиме Tiled.</summary>
        public static Sprite Grid => grid != null ? grid : (grid = Create(32, FilterMode.Point, (u, v) => u < 0.04f || v < 0.04f ? 1f : 0f));

        private static Sprite Create(int size, FilterMode filter, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                name = "Procedural",
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            // FullRect — чтобы спрайт можно было рисовать плиткой (SpriteDrawMode.Tiled)
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }
    }
}
