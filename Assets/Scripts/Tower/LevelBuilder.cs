using System.Collections.Generic;
using CyberDefender.Visuals;
using UnityEngine;

namespace CyberDefender.Tower
{
    /// <summary>
    /// Строит арену этажа по LevelDefinition: парящий остров, сквозные платформы, параллакс-фон.
    /// Отдаёт геометрию для игры: ширину арены, зону ринг-аута и точки появления.
    /// </summary>
    public sealed class LevelBuilder : MonoBehaviour
    {
        private struct ParallaxLayer
        {
            public Transform Transform;
            public float Factor;
        }

        [SerializeField] private Camera arenaCamera;
        [SerializeField] private Sprite fallbackSprite;
        [SerializeField] private float groundTop = -2f;
        [SerializeField, Tooltip("Максимальный зум камеры — фон должен закрывать кадр и на нём")]
        private float coverOrthoSize = 9.5f;
        [SerializeField] private float defaultCameraY = 1.5f;

        private Transform levelRoot;
        private readonly List<ParallaxLayer> layers = new List<ParallaxLayer>();
        private LevelDefinition current;

        public float GroundTop => groundTop;
        /// <summary>Половина ширины основной арены (от центра до края сплошной земли).</summary>
        public float StageHalfWidth { get; private set; } = 8f;
        /// <summary>Выход за этот прямоугольник — ринг-аут.</summary>
        public Rect BlastZone { get; private set; } = new Rect(-15f, -10f, 30f, 24f);

        public Vector2 PlayerSpawn => new Vector2(-SpawnX, groundTop + 1.2f);
        public Vector2 RespawnPoint => new Vector2(0f, groundTop + 5f);

        private float SpawnX => Mathf.Min(current != null ? current.spawnSpread : 4f, StageHalfWidth - 1f);

        public Vector2 EnemySpawn(int index, float spacing)
        {
            float x = Mathf.Min(SpawnX + index * spacing, StageHalfWidth - 1f);
            return new Vector2(x, groundTop + 1.2f);
        }

        public void Build(LevelDefinition level)
        {
            Clear();
            current = level;
            if (level == null)
                return;

            levelRoot = new GameObject($"Level: {level.name}").transform;
            if (arenaCamera != null)
                arenaCamera.backgroundColor = level.backgroundColor;

            BuildBackground(level);
            BackdropTheme theme = level.theme == BackdropTheme.Unset ? BackdropTheme.Plain : level.theme;
            levelRoot.gameObject.AddComponent<ArenaBackdrop>().Build(level, theme, arenaCamera, groundTop, defaultCameraY);
            BuildStage(level);
        }

        public void Clear()
        {
            if (levelRoot != null)
                Destroy(levelRoot.gameObject);
            levelRoot = null;
            layers.Clear();
        }

        private void LateUpdate()
        {
            if (arenaCamera == null)
                return;

            // Дальние слои почти «прилипли» к камере, ближние стоят в мире
            Vector3 cam = arenaCamera.transform.position;
            for (int i = 0; i < layers.Count; i++)
            {
                float k = 1f - layers[i].Factor;
                layers[i].Transform.position = new Vector3(cam.x * k, defaultCameraY + (cam.y - defaultCameraY) * k, 0f);
            }
        }

        // ---------- Фон ----------

        private void BuildBackground(LevelDefinition level)
        {
            if (arenaCamera == null || level.backgroundLayers == null)
                return;

            float camHeight = coverOrthoSize * 2f;
            float camWidth = camHeight * arenaCamera.aspect;

            for (int i = 0; i < level.backgroundLayers.Length; i++)
            {
                BackgroundLayer data = level.backgroundLayers[i];
                if (data == null || data.sprite == null)
                    continue;

                var go = new GameObject($"Background {i}");
                go.transform.SetParent(levelRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = data.sprite;
                sr.color = data.tint;
                sr.sortingOrder = -100 + i;

                // С запасом под параллакс: ближние слои смещаются относительно кадра сильнее
                float overscan = 1.15f + data.parallax;
                Vector2 size = data.sprite.bounds.size;
                float scale = Mathf.Max(camWidth * overscan / size.x, camHeight * overscan / size.y);
                go.transform.localScale = new Vector3(scale, scale, 1f);

                layers.Add(new ParallaxLayer { Transform = go.transform, Factor = data.parallax * 0.5f });
            }
            LateUpdate();
        }

        // ---------- Арена ----------

        private void BuildStage(LevelDefinition level)
        {
            List<string> rows = ParseRows(level.layout);
            if (rows.Count == 0)
                return;

            int width = 0;
            foreach (string row in rows)
                width = Mathf.Max(width, row.Length);
            float left = -width * 0.5f;

            float stageMin = float.MaxValue, stageMax = float.MinValue;

            for (int r = 0; r < rows.Count; r++)
            {
                int tier = rows.Count - 1 - r; // нижняя строка — основная арена
                float top = groundTop + tier * level.rowHeight;
                string row = rows[r];

                int c = 0;
                while (c < row.Length)
                {
                    char symbol = row[c];
                    if (symbol != '-' && symbol != '=')
                    {
                        c++;
                        continue;
                    }

                    // Подряд идущие одинаковые символы — одна платформа
                    int start = c;
                    while (c < row.Length && row[c] == symbol)
                        c++;
                    int length = c - start;
                    float centerX = left + start + length * 0.5f;

                    if (symbol == '=')
                    {
                        CreateSolid(level, centerX, top, length);
                        if (tier == 0)
                        {
                            stageMin = Mathf.Min(stageMin, left + start);
                            stageMax = Mathf.Max(stageMax, left + c);
                        }
                    }
                    else
                    {
                        CreatePlatform(level, centerX, top, length);
                    }
                }
            }

            StageHalfWidth = stageMax > stageMin ? Mathf.Max(Mathf.Abs(stageMin), Mathf.Abs(stageMax)) : width * 0.5f;
            float halfZone = StageHalfWidth + level.blastMarginX;
            float bottom = groundTop - level.blastDepth;
            BlastZone = new Rect(-halfZone, bottom, halfZone * 2f, level.blastDepth + level.blastHeight);
        }

        private static List<string> ParseRows(string layout)
        {
            var rows = new List<string>();
            if (string.IsNullOrWhiteSpace(layout))
                return rows;
            foreach (string raw in layout.Split('\n'))
            {
                string row = raw.TrimEnd('\r');
                if (row.Length > 0)
                    rows.Add(row);
            }
            return rows;
        }

        private void CreateSolid(LevelDefinition level, float centerX, float top, float width)
        {
            float thickness = level.solidThickness;
            GameObject go = CreateCollider("Ground", centerX, top, width, thickness);

            if (level.groundSprite != null)
            {
                CreateTiled(go.transform, level.groundSprite, width, thickness, 0f);
                return;
            }

            // Остров — микросхема: тёмный корпус, дорожки на лицевой стороне, неоновый верх, ножки снизу
            Color neon = level.groundColor;
            Color bright = Color.Lerp(neon, Color.white, 0.3f);
            float faceTop = thickness * 0.5f;
            float bottom = -thickness * 0.5f;

            CreateRect(go.transform, Square, Vector2.zero, new Vector2(width, thickness), Opaque(Color.Lerp(level.backgroundColor, neon, 0.16f)), -7);
            CreateRect(go.transform, ProceduralSprites.Gradient, Vector2.zero, new Vector2(width, thickness), WithAlpha(neon, 0.22f), -6);

            for (int i = 0; i < 2; i++)
            {
                float y = faceTop - thickness * (0.38f + i * 0.28f);
                float length = width * (i == 0 ? 0.7f : 0.45f);
                CreateRect(go.transform, Square, new Vector2(0f, y), new Vector2(length, 0.04f), WithAlpha(neon, 0.25f), -5);
                CreateRect(go.transform, Square, new Vector2(-length * 0.5f, y), new Vector2(0.14f, 0.14f), WithAlpha(neon, 0.45f), -5);
                CreateRect(go.transform, Square, new Vector2(length * 0.5f, y), new Vector2(0.14f, 0.14f), WithAlpha(neon, 0.45f), -5);
            }

            for (float x = -width * 0.5f + 0.45f; x <= width * 0.5f - 0.45f; x += 0.7f)
                CreateRect(go.transform, Square, new Vector2(x, bottom - 0.17f), new Vector2(0.14f, 0.34f), WithAlpha(neon, 0.5f), -7);
            CreateRect(go.transform, ProceduralSprites.GlowLine, new Vector2(0f, bottom), new Vector2(width, 0.8f), WithAlpha(neon, 0.1f), -8);

            CreateEdge(go.transform, faceTop, width, neon, bright, 0.12f, centerX);

            // Края острова видно издалека — с них падают
            CreateRect(go.transform, Square, new Vector2(-width * 0.5f + 0.1f, faceTop - 0.1f), new Vector2(0.2f, 0.2f), WithAlpha(Color.white, 0.9f), -2);
            CreateRect(go.transform, Square, new Vector2(width * 0.5f - 0.1f, faceTop - 0.1f), new Vector2(0.2f, 0.2f), WithAlpha(Color.white, 0.9f), -2);
        }

        /// <summary>Неоновая грань с «дышащим» ореолом.</summary>
        private void CreateEdge(Transform parent, float top, float width, Color neon, Color bright, float thickness, float phase)
        {
            CreateRect(parent, Square, new Vector2(0f, top - thickness * 0.5f), new Vector2(width, thickness), Opaque(bright), -3);
            SpriteRenderer halo = CreateRect(parent, ProceduralSprites.GlowLine, new Vector2(0f, top), new Vector2(width + 0.6f, 0.9f), WithAlpha(neon, 0.3f), -4);
            halo.gameObject.AddComponent<NeonPulse>().Configure(0.3f, 0.08f, 2f, phase * 0.7f);
        }

        private void CreatePlatform(LevelDefinition level, float centerX, float top, float width)
        {
            float thickness = level.platformThickness;
            GameObject go = CreateCollider("Platform", centerX, top, width, thickness);

            // Сквозная: запрыгиваешь снизу, стоишь сверху
            go.GetComponent<BoxCollider2D>().usedByEffector = true;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;

            if (level.platformSprite != null)
            {
                float tileHeight = level.platformSprite.bounds.size.y;
                CreateTiled(go.transform, level.platformSprite, width, tileHeight, thickness * 0.5f - tileHeight * 0.5f);
            }
            else
            {
                // Сквозная платформа — полупрозрачная «шина» с огоньками снизу
                Color neon = level.platformColor;
                Color bright = Color.Lerp(neon, Color.white, 0.3f);
                float faceTop = thickness * 0.5f;

                CreateRect(go.transform, Square, Vector2.zero, new Vector2(width, thickness), WithAlpha(Color.Lerp(level.backgroundColor, neon, 0.2f), 0.85f), -6);
                for (float x = -width * 0.5f + 0.25f; x < width * 0.5f; x += 0.5f)
                    CreateRect(go.transform, Square, new Vector2(x, -faceTop + 0.07f), new Vector2(0.08f, 0.06f), WithAlpha(neon, 0.6f), -5);
                CreateRect(go.transform, Square, new Vector2(-width * 0.5f + 0.05f, 0f), new Vector2(0.1f, thickness), Opaque(bright), -3);
                CreateRect(go.transform, Square, new Vector2(width * 0.5f - 0.05f, 0f), new Vector2(0.1f, thickness), Opaque(bright), -3);
                CreateEdge(go.transform, faceTop, width, neon, bright, 0.08f, centerX);
            }
        }

        private GameObject CreateCollider(string name, float centerX, float top, float width, float thickness)
        {
            var go = new GameObject(name);
            go.layer = 0; // Default — попадает в groundMask игрока и вирусов
            go.transform.SetParent(levelRoot, false);
            go.transform.position = new Vector3(centerX, top - thickness * 0.5f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(width, thickness);
            return go;
        }

        private Sprite Square => fallbackSprite != null ? fallbackSprite : ProceduralSprites.Square;

        private static SpriteRenderer CreateRect(Transform parent, Sprite sprite, Vector2 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static Color Opaque(Color c) => WithAlpha(c, 1f);

        private static void CreateTiled(Transform parent, Sprite sprite, float width, float height, float offsetY)
        {
            var go = new GameObject("Tiles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, offsetY, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(width, height);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Rect zone = BlastZone;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(zone.center, zone.size);
        }

        public void EditorConfigure(Camera camera, Sprite fallbackSprite)
        {
            arenaCamera = camera;
            this.fallbackSprite = fallbackSprite;
        }
#endif
    }
}
