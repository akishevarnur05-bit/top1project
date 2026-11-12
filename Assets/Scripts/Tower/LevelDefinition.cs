using System;
using UnityEngine;

namespace CyberDefender.Tower
{
    [Serializable]
    public sealed class BackgroundLayer
    {
        public Sprite sprite;
        [Tooltip("0 — дальний слой (едет вместе с камерой), 1 — ближний (стоит в мире)")]
        [Range(0f, 1f)] public float parallax = 0.1f;
        public Color tint = Color.white;
    }

    /// <summary>
    /// Арена одного этажа: схема + внешний вид + границы ринг-аута.
    /// Схема читается сверху вниз; нижняя строка — уровень основной арены (верх на y = -2).
    /// Каждый символ — 1 юнит, схема центрируется по x = 0.
    /// </summary>
    [CreateAssetMenu(menuName = "CyberDefender/Level Definition", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Tooltip("'=' — сплошная земля (в нижней строке — основная арена), '-' — сквозная платформа, '.' — пусто/пропасть")]
        [TextArea(3, 10)] public string layout = "....----......----....\n...================...";
        [Tooltip("Высота яруса. Должна быть меньше высоты прыжка самого «тяжёлого» героя (BASTION ≈ 1.85)")]
        public float rowHeight = 1.5f;
        public float platformThickness = 0.35f;
        [Tooltip("Толщина «острова» под сплошной землёй")]
        public float solidThickness = 1.6f;

        [Header("Ring Out")]
        [Tooltip("Насколько за краем арены начинается зона вылета")]
        public float blastMarginX = 7f;
        [Tooltip("Глубина под ареной, где боец считается упавшим")]
        public float blastDepth = 8f;
        public float blastHeight = 14f;

        [Header("Spawns")]
        [Tooltip("Расстояние от центра до точек появления игрока (слева) и вирусов (справа)")]
        public float spawnSpread = 4f;

        [Header("Look")]
        [Tooltip("Процедурный неоновый фон этажа. Plain — только сетка и цифровой дождь")]
        public BackdropTheme theme = BackdropTheme.Unset;
        public Color backgroundColor = new Color(0.03f, 0.05f, 0.1f);
        public Color platformColor = new Color(0.2f, 0.9f, 1f);
        public Color groundColor = new Color(0.1f, 0.8f, 0.9f);
        [Tooltip("Тайл сквозной платформы (плиткой). Пусто — неоновый блок цвета platformColor")]
        public Sprite platformSprite;
        [Tooltip("Тайл сплошной земли (плиткой). Пусто — блок цвета groundColor")]
        public Sprite groundSprite;
        [Tooltip("Слои фона от дальнего к ближнему")]
        public BackgroundLayer[] backgroundLayers = Array.Empty<BackgroundLayer>();
    }
}
