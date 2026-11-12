using System.Collections.Generic;
using CyberDefender.Visuals;
using UnityEngine;

namespace CyberDefender.Tower
{
    /// <summary>Мотив процедурного фона этажа.</summary>
    public enum BackdropTheme
    {
        Unset,       // старые ассеты — Build Game Scene подставит мотив по этажу
        BootSector,  // шины данных с бегущими импульсами, сканирующий луч
        MemoryHeap,  // мигающие ячейки памяти
        FileSystem,  // башни секторов диска
        DriverStack, // дорожки микросхемы и чипы
        KernelCore,  // пульсирующее ядро и ударные волны
        Plain,       // только сетка и цифровой дождь
    }

    /// <summary>
    /// Неоновый «цифровой» фон, целиком из кода: дальняя сетка со свечением горизонта, мотив этажа на среднем плане,
    /// цифровой дождь на ближнем. Слои двигаются с параллаксом относительно камеры.
    /// </summary>
    public sealed class ArenaBackdrop : MonoBehaviour
    {
        private struct Layer
        {
            public Transform Transform;
            public float Factor; // 0 — едет вместе с камерой, 1 — стоит в мире
        }

        private struct Drop
        {
            public Transform Transform;
            public float Speed;
        }

        private struct Pulse
        {
            public Transform Transform;
            public Vector2[] Path;
            public float[] Distances;
            public float Position;
            public float Speed;
        }

        private struct Cell
        {
            public SpriteRenderer Renderer;
            public float BaseAlpha;
            public float Glow;
        }

        private struct Wave
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float Age;
        }

        private const float RainTop = 16f;
        private const float RainBottom = -14f;
        private const float RainHalfWidth = 32f;
        private const float WaveLifetime = 2.2f;
        private const float WaveInterval = 2.4f;

        private readonly List<Layer> layers = new List<Layer>();
        private readonly List<Drop> drops = new List<Drop>();
        private readonly List<Pulse> pulses = new List<Pulse>();
        private readonly List<Cell> cells = new List<Cell>();
        private readonly List<Wave> waves = new List<Wave>();
        private readonly List<Transform> orbits = new List<Transform>();

        private Camera cam;
        private float baseY;
        private Color accent;
        private Color accent2;
        private System.Random rng;

        private float cellRate;
        private float cellAccumulator;
        private float cellPeak = 0.45f;
        private Transform scanBar;
        private float scanBottom;
        private float scanTop;
        private Transform core;
        private float waveTimer;

        /// <param name="groundTop">Верх основного острова в мире.</param>
        /// <param name="baseY">Высота камеры по умолчанию — относительно неё раскладываются слои.</param>
        public void Build(LevelDefinition level, BackdropTheme theme, Camera camera, float groundTop, float baseY)
        {
            cam = camera;
            this.baseY = baseY;
            accent = level.platformColor;
            accent2 = level.groundColor;
            rng = new System.Random(17 + (int)theme * 7919);

            // Локальные координаты слоёв: (0, 0) ≈ центр кадра камеры по умолчанию
            float gy = groundTop - baseY;

            Transform far = CreateLayer("Far", 0.04f);
            BuildFar(far, gy);

            Transform mid = CreateLayer("Mid", 0.25f);
            switch (theme)
            {
                case BackdropTheme.BootSector: BuildBootSector(mid, gy); break;
                case BackdropTheme.MemoryHeap: BuildMemoryHeap(mid, gy); break;
                case BackdropTheme.FileSystem: BuildFileSystem(mid, gy); break;
                case BackdropTheme.DriverStack: BuildDriverStack(mid, gy); break;
                case BackdropTheme.KernelCore: BuildKernelCore(mid, gy); break;
            }

            Transform near = CreateLayer("Near", 0.45f);
            BuildRain(near, theme == BackdropTheme.KernelCore ? 44 : 32);

            LateUpdate();
        }

        // ---------- Слои ----------

        private Transform CreateLayer(string name, float factor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            layers.Add(new Layer { Transform = go.transform, Factor = factor });
            return go.transform;
        }

        private void LateUpdate()
        {
            if (cam == null)
                return;

            Vector3 c = cam.transform.position;
            for (int i = 0; i < layers.Count; i++)
            {
                float k = 1f - layers[i].Factor;
                layers[i].Transform.position = new Vector3(c.x * k, baseY + (c.y - baseY) * k, 0f);
            }
        }

        private void BuildFar(Transform parent, float gy)
        {
            // Свечение «горизонта» снизу и тёмный верх
            SpriteRenderer horizon = CreateSprite(parent, ProceduralSprites.Gradient, new Vector2(0f, gy - 2f), new Vector2(80f, 16f), WithAlpha(accent2, 0.2f), -95);
            horizon.flipY = true;
            CreateSprite(parent, ProceduralSprites.Gradient, new Vector2(0f, 14f), new Vector2(80f, 14f), new Color(0f, 0f, 0f, 0.35f), -94);

            // Сетка: клетка 2.5 юнита
            const float cell = 2.5f;
            SpriteRenderer grid = CreateSprite(parent, ProceduralSprites.Grid, Vector2.zero, new Vector2(cell, cell), WithAlpha(accent, 0.06f), -93);
            grid.drawMode = SpriteDrawMode.Tiled;
            grid.size = new Vector2(80f / cell, 48f / cell);
        }

        private void BuildRain(Transform parent, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var drop = new GameObject("Drop").transform;
                drop.SetParent(parent, false);
                drop.localPosition = new Vector3(Range(-RainHalfWidth, RainHalfWidth), Range(RainBottom, RainTop), 0f);

                float length = Range(0.6f, 2f);
                float alpha = Range(0.1f, 0.3f);
                CreateSprite(drop, ProceduralSprites.Square, new Vector2(0f, length * 0.5f), new Vector2(0.05f, length), WithAlpha(accent, alpha), -55);
                CreateSprite(drop, ProceduralSprites.Square, Vector2.zero, new Vector2(0.1f, 0.1f), WithAlpha(Color.Lerp(accent, Color.white, 0.5f), alpha + 0.3f), -54);

                drops.Add(new Drop { Transform = drop, Speed = Range(2.5f, 8f) });
            }
        }

        // ---------- Мотивы этажей ----------

        /// <summary>Загрузочный сектор: горизонтальные шины данных, перемычки, бегущие импульсы, сканирующий луч.</summary>
        private void BuildBootSector(Transform parent, float gy)
        {
            const int lines = 6;
            float[] ys = new float[lines];
            for (int i = 0; i < lines; i++)
            {
                ys[i] = gy - 3f + i * 2.6f;
                CreateTrace(parent, new Vector2(-34f, ys[i]), new Vector2(34f, ys[i]), 0.05f, 0.14f);
                int pulseCount = 2;
                for (int p = 0; p < pulseCount; p++)
                {
                    bool leftToRight = rng.NextDouble() < 0.5;
                    var a = new Vector2(leftToRight ? -34f : 34f, ys[i]);
                    var b = new Vector2(leftToRight ? 34f : -34f, ys[i]);
                    AddPulse(parent, new[] { a, b }, Range(4f, 9f), Range(0f, 68f));
                }
            }

            // Перемычки между соседними шинами
            for (int i = 0; i < 12; i++)
            {
                int row = rng.Next(lines - 1);
                float x = Mathf.Round(Range(-30f, 30f));
                CreateTrace(parent, new Vector2(x, ys[row]), new Vector2(x, ys[row + 1]), 0.04f, 0.1f);
                CreateNode(parent, new Vector2(x, ys[row]), 0.2f);
                CreateNode(parent, new Vector2(x, ys[row + 1]), 0.2f);
            }

            scanBottom = gy - 6f;
            scanTop = gy + 18f;
            scanBar = CreateSprite(parent, ProceduralSprites.GlowLine, new Vector2(0f, scanBottom), new Vector2(80f, 1.4f), WithAlpha(accent, 0.09f), -70).transform;
        }

        /// <summary>Куча памяти: сетка ячеек, занятые блоки светятся ровно, свободные вспыхивают и гаснут.</summary>
        private void BuildMemoryHeap(Transform parent, float gy)
        {
            const int cols = 34;
            const int rows = 14;
            const float spacing = 1.6f;
            const float size = 1.2f;
            float left = -(cols - 1) * spacing * 0.5f;
            float bottom = gy - 5f;

            for (int r = 0; r < rows; r++)
            {
                // «Выделенные» блоки — непрерывные отрезки в строке
                int allocStart = rng.Next(cols);
                int allocLength = rng.Next(3, 9);
                for (int c = 0; c < cols; c++)
                {
                    bool allocated = c >= allocStart && c < allocStart + allocLength;
                    float baseAlpha = allocated ? 0.13f : 0.04f;
                    var pos = new Vector2(left + c * spacing, bottom + r * spacing);
                    SpriteRenderer sr = CreateSprite(parent, ProceduralSprites.Square, pos, new Vector2(size, size), WithAlpha(allocated ? accent2 : accent, baseAlpha), -80);
                    cells.Add(new Cell { Renderer = sr, BaseAlpha = baseAlpha });
                }
            }
            cellRate = 30f;
            cellPeak = 0.4f;
        }

        /// <summary>Файловая система: башни из секторов диска, по индексной шине бегут пакеты.</summary>
        private void BuildFileSystem(Transform parent, float gy)
        {
            const float spacing = 3.4f;
            const float width = 2.6f;
            const float blockHeight = 0.9f;
            const float gap = 0.18f;
            float bottom = gy - 5f;

            for (float x = -32f; x <= 32f; x += spacing)
            {
                int height = rng.Next(2, 10);
                for (int b = 0; b < height; b++)
                {
                    var pos = new Vector2(x, bottom + b * (blockHeight + gap) + blockHeight * 0.5f);
                    SpriteRenderer sr = CreateSprite(parent, ProceduralSprites.Square, pos, new Vector2(width, blockHeight), WithAlpha(accent, 0.06f), -80);
                    cells.Add(new Cell { Renderer = sr, BaseAlpha = 0.06f });
                }
                // Верхний сектор подсвечен — силуэт «папки»
                float top = bottom + height * (blockHeight + gap);
                CreateSprite(parent, ProceduralSprites.Square, new Vector2(x - width * 0.2f, top + 0.08f), new Vector2(width * 0.6f, 0.16f), WithAlpha(accent, 0.28f), -79);
            }
            cellRate = 7f;
            cellPeak = 0.3f;

            float busY = bottom - 0.6f;
            CreateTrace(parent, new Vector2(-34f, busY), new Vector2(34f, busY), 0.06f, 0.2f);
            for (int p = 0; p < 4; p++)
                AddPulse(parent, new[] { new Vector2(-34f, busY), new Vector2(34f, busY) }, Range(5f, 10f), Range(0f, 68f));
        }

        /// <summary>Стек драйверов: дорожки печатной платы с импульсами и силуэты чипов.</summary>
        private void BuildDriverStack(Transform parent, float gy)
        {
            for (int c = 0; c < 6; c++)
            {
                var center = new Vector2(Range(-28f, 28f), Range(gy - 2f, gy + 13f));
                CreateChip(parent, center, new Vector2(Range(2.5f, 4.5f), Range(1.6f, 2.8f)));
            }

            for (int t = 0; t < 16; t++)
            {
                var points = new List<Vector2> { new Vector2(Mathf.Round(Range(-32f, 32f)), Mathf.Round(Range(gy - 4f, gy + 16f))) };
                bool horizontal = rng.NextDouble() < 0.5;
                int segments = rng.Next(3, 6);
                for (int s = 0; s < segments; s++)
                {
                    Vector2 last = points[points.Count - 1];
                    float length = Mathf.Round(Range(2f, 7f)) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    points.Add(horizontal ? last + new Vector2(length, 0f) : last + new Vector2(0f, length));
                    horizontal = !horizontal;
                }

                for (int i = 0; i < points.Count - 1; i++)
                {
                    CreateTrace(parent, points[i], points[i + 1], 0.06f, 0.13f);
                    CreateNode(parent, points[i + 1], 0.16f);
                }
                CreateSprite(parent, ProceduralSprites.Ring, points[0], new Vector2(0.45f, 0.45f), WithAlpha(accent, 0.4f), -78);
                AddPulse(parent, points.ToArray(), Range(3f, 6f), Range(0f, 20f));
            }
        }

        /// <summary>Ядро: пульсирующий центр, кольца, вращающиеся орбиты и ударные волны.</summary>
        private void BuildKernelCore(Transform parent, float gy)
        {
            var center = new Vector2(0f, gy + 5.5f);

            for (int s = 0; s < 16; s++)
            {
                float angle = s * Mathf.PI * 2f / 16f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                CreateLine(parent, center + dir * 3f, center + dir * 30f, 0.05f, WithAlpha(accent, 0.05f), -85);
            }

            float[] radii = { 3f, 5.2f, 8f };
            for (int i = 0; i < radii.Length; i++)
            {
                SpriteRenderer ring = CreateSprite(parent, ProceduralSprites.Ring, center, Vector2.one * radii[i] * 2f, WithAlpha(accent, 0.18f - i * 0.04f), -84);
                ring.gameObject.AddComponent<NeonPulse>().Configure(0.16f - i * 0.04f, 0.05f, 1.5f, i * 1.3f);
            }

            core = CreateSprite(parent, ProceduralSprites.SoftDot, center, new Vector2(6f, 6f), WithAlpha(accent2, 0.45f), -83).transform;
            CreateSprite(parent, ProceduralSprites.SoftDot, center, new Vector2(2.2f, 2.2f), WithAlpha(Color.Lerp(accent, Color.white, 0.6f), 0.6f), -82);

            // Две орбиты точек, вращаются навстречу друг другу
            for (int o = 0; o < 2; o++)
            {
                var orbit = new GameObject("Orbit").transform;
                orbit.SetParent(parent, false);
                orbit.localPosition = center;
                float radius = o == 0 ? 4.1f : 6.6f;
                int dots = o == 0 ? 10 : 16;
                for (int d = 0; d < dots; d++)
                {
                    float angle = d * Mathf.PI * 2f / dots;
                    var pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    CreateSprite(orbit, ProceduralSprites.Square, pos, new Vector2(0.18f, 0.18f), WithAlpha(accent, 0.5f), -81);
                    CreateSprite(orbit, ProceduralSprites.SoftDot, pos, new Vector2(0.8f, 0.8f), WithAlpha(accent, 0.2f), -82);
                }
                orbits.Add(orbit);
            }

            for (int w = 0; w < 2; w++)
            {
                SpriteRenderer sr = CreateSprite(parent, ProceduralSprites.Ring, center, Vector2.one, WithAlpha(accent2, 0f), -86);
                waves.Add(new Wave { Transform = sr.transform, Renderer = sr, Age = WaveLifetime });
            }
        }

        // ---------- Анимация ----------

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < drops.Count; i++)
            {
                Drop d = drops[i];
                Vector3 p = d.Transform.localPosition;
                p.y -= d.Speed * dt;
                if (p.y < RainBottom)
                {
                    p.y = RainTop;
                    p.x = Range(-RainHalfWidth, RainHalfWidth);
                }
                d.Transform.localPosition = p;
            }

            for (int i = 0; i < pulses.Count; i++)
            {
                Pulse pulse = pulses[i];
                float total = pulse.Distances[pulse.Distances.Length - 1];
                pulse.Position = Mathf.Repeat(pulse.Position + pulse.Speed * dt, total);
                pulse.Transform.localPosition = Evaluate(pulse);
                pulses[i] = pulse;
            }

            UpdateCells(dt);

            if (scanBar != null)
            {
                float y = Mathf.Lerp(scanBottom, scanTop, Mathf.Repeat(Time.time / 7f, 1f));
                scanBar.localPosition = new Vector3(0f, y, 0f);
            }

            if (core != null)
            {
                float s = 6f + Mathf.Sin(Time.time * 2.4f) * 0.8f;
                core.localScale = new Vector3(s, s, 1f);
                for (int i = 0; i < orbits.Count; i++)
                    orbits[i].localRotation = Quaternion.Euler(0f, 0f, Time.time * (i == 0 ? 18f : -11f));
                UpdateWaves(dt);
            }
        }

        private void UpdateCells(float dt)
        {
            if (cells.Count == 0)
                return;

            cellAccumulator += cellRate * dt;
            while (cellAccumulator >= 1f)
            {
                cellAccumulator -= 1f;
                int index = rng.Next(cells.Count);
                Cell lit = cells[index];
                lit.Glow = 1f;
                cells[index] = lit;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Cell cell = cells[i];
                if (cell.Glow <= 0f)
                    continue;
                cell.Glow = Mathf.Max(0f, cell.Glow - dt * 1.4f);
                Color c = cell.Renderer.color;
                c.a = cell.BaseAlpha + cell.Glow * cellPeak;
                cell.Renderer.color = c;
                cells[i] = cell;
            }
        }

        private void UpdateWaves(float dt)
        {
            waveTimer -= dt;
            for (int i = 0; i < waves.Count; i++)
            {
                Wave w = waves[i];
                if (waveTimer <= 0f && w.Age >= WaveLifetime)
                {
                    w.Age = 0f;
                    waveTimer = WaveInterval;
                }

                if (w.Age < WaveLifetime)
                {
                    w.Age += dt;
                    float k = Mathf.Clamp01(w.Age / WaveLifetime);
                    float size = Mathf.Lerp(2f, 34f, k);
                    w.Transform.localScale = new Vector3(size, size, 1f);
                    Color c = w.Renderer.color;
                    c.a = 0.3f * (1f - k);
                    w.Renderer.color = c;
                }
                waves[i] = w;
            }
        }

        private static Vector2 Evaluate(in Pulse pulse)
        {
            for (int i = 1; i < pulse.Distances.Length; i++)
            {
                if (pulse.Position <= pulse.Distances[i])
                {
                    float segment = pulse.Distances[i] - pulse.Distances[i - 1];
                    float t = segment > 0f ? (pulse.Position - pulse.Distances[i - 1]) / segment : 0f;
                    return Vector2.Lerp(pulse.Path[i - 1], pulse.Path[i], t);
                }
            }
            return pulse.Path[pulse.Path.Length - 1];
        }

        // ---------- Примитивы ----------

        private void AddPulse(Transform parent, Vector2[] path, float speed, float startPosition)
        {
            var go = new GameObject("Pulse").transform;
            go.SetParent(parent, false);
            CreateSprite(go, ProceduralSprites.SoftDot, Vector2.zero, new Vector2(0.9f, 0.9f), WithAlpha(accent, 0.55f), -76);
            CreateSprite(go, ProceduralSprites.Square, Vector2.zero, new Vector2(0.16f, 0.16f), WithAlpha(Color.Lerp(accent, Color.white, 0.6f), 0.9f), -75);

            var distances = new float[path.Length];
            for (int i = 1; i < path.Length; i++)
                distances[i] = distances[i - 1] + Vector2.Distance(path[i - 1], path[i]);

            var pulse = new Pulse { Transform = go, Path = path, Distances = distances, Speed = speed };
            pulse.Position = Mathf.Repeat(startPosition, Mathf.Max(distances[distances.Length - 1], 0.01f));
            go.localPosition = Evaluate(pulse);
            pulses.Add(pulse);
        }

        /// <summary>Дорожка: тонкая линия и мягкое свечение вокруг неё.</summary>
        private void CreateTrace(Transform parent, Vector2 a, Vector2 b, float thickness, float alpha)
        {
            CreateLine(parent, a, b, thickness, WithAlpha(accent, alpha), -78);
            CreateLine(parent, a, b, thickness * 8f, WithAlpha(accent, alpha * 0.35f), -79, ProceduralSprites.GlowLine);
        }

        private void CreateNode(Transform parent, Vector2 position, float size)
        {
            CreateSprite(parent, ProceduralSprites.Square, position, new Vector2(size, size), WithAlpha(accent, 0.35f), -77);
        }

        private void CreateChip(Transform parent, Vector2 center, Vector2 size)
        {
            CreateSprite(parent, ProceduralSprites.Square, center, size, WithAlpha(Color.Lerp(Color.black, accent2, 0.25f), 0.6f), -82);
            CreateSprite(parent, ProceduralSprites.Square, center + new Vector2(0f, size.y * 0.5f), new Vector2(size.x, 0.06f), WithAlpha(accent, 0.3f), -81);
            CreateSprite(parent, ProceduralSprites.Square, center - new Vector2(0f, size.y * 0.5f), new Vector2(size.x, 0.06f), WithAlpha(accent, 0.2f), -81);
            for (float x = -size.x * 0.5f + 0.35f; x < size.x * 0.5f - 0.2f; x += 0.5f)
            {
                CreateSprite(parent, ProceduralSprites.Square, center + new Vector2(x, size.y * 0.5f + 0.18f), new Vector2(0.12f, 0.3f), WithAlpha(accent, 0.22f), -82);
                CreateSprite(parent, ProceduralSprites.Square, center - new Vector2(-x, size.y * 0.5f + 0.18f), new Vector2(0.12f, 0.3f), WithAlpha(accent, 0.22f), -82);
            }
            CreateSprite(parent, ProceduralSprites.SoftDot, center, size * 1.8f, WithAlpha(accent, 0.05f), -83);
        }

        private static SpriteRenderer CreateLine(Transform parent, Vector2 a, Vector2 b, float thickness, Color color, int order, Sprite sprite = null)
        {
            Vector2 delta = b - a;
            SpriteRenderer sr = CreateSprite(parent, sprite != null ? sprite : ProceduralSprites.Square, (a + b) * 0.5f,
                new Vector2(delta.magnitude + thickness, thickness), color, order);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return sr;
        }

        private static SpriteRenderer CreateSprite(Transform parent, Sprite sprite, Vector2 localPosition, Vector2 size, Color color, int order)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
