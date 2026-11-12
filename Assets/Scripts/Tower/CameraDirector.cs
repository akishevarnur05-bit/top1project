using CyberDefender.Core;
using CyberDefender.Enemies;
using UnityEngine;

namespace CyberDefender.Tower
{
    /// <summary>
    /// Камера платформенного файтинга: держит в кадре игрока и всех вирусов, отдаляется,
    /// когда бойцы разлетаются. В меню возвращается в статичную позицию.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraDirector : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private LevelBuilder levelBuilder;
        [SerializeField] private Vector2 menuPosition = new Vector2(0f, 1.5f);
        [SerializeField] private float minSize = 5.5f;
        [SerializeField] private float maxSize = 9f;
        [SerializeField, Tooltip("Поля вокруг бойцов, юниты")] private Vector2 padding = new Vector2(3f, 2.5f);
        [SerializeField, Tooltip("Камера чуть выше центра бойцов — видно платформы")] private float verticalBias = 1.2f;
        [SerializeField] private float smoothing = 4f;

        private Camera cam;
        private bool follow;
        private bool hasFocus;
        private Vector2 focusPoint;
        private float focusSize;

        /// <summary>Крупный план (катсцены): камера держит точку, пока не вызван ClearFocus.</summary>
        public void Focus(Vector2 point, float orthoSize)
        {
            hasFocus = true;
            focusPoint = point;
            focusSize = orthoSize;
        }

        public void ClearFocus() => hasFocus = false;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            GameEventBus.OnGameFlowChanged += HandleFlow;
        }

        private void OnDisable()
        {
            GameEventBus.OnGameFlowChanged -= HandleFlow;
        }

        private void HandleFlow(GameFlowState state)
        {
            follow = !TowerManager.IsMenuState(state);
        }

        private void LateUpdate()
        {
            Vector2 targetPos = menuPosition;
            float targetSize = minSize;

            if (hasFocus)
            {
                targetPos = focusPoint;
                targetSize = focusSize;
            }
            else if (follow && player != null)
            {
                // Падающих в пропасть не догоняем — кадр не уходит ниже арены
                float floor = levelBuilder != null ? levelBuilder.GroundTop - 2f : -4f;
                float limitX = levelBuilder != null ? levelBuilder.StageHalfWidth + 4f : 12f;

                Vector2 min = Clamp(player.position, floor, limitX);
                Vector2 max = min;
                foreach (EnemyController enemy in EnemyController.Active)
                {
                    Vector2 p = Clamp(enemy.transform.position, floor, limitX);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                Vector2 extent = (max - min) * 0.5f + padding;
                targetPos = (min + max) * 0.5f + new Vector2(0f, verticalBias);
                targetSize = Mathf.Clamp(Mathf.Max(extent.y, extent.x / cam.aspect), minSize, maxSize);
            }

            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            Vector3 pos = transform.position;
            transform.position = new Vector3(Mathf.Lerp(pos.x, targetPos.x, t), Mathf.Lerp(pos.y, targetPos.y, t), pos.z);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);
        }

        private static Vector2 Clamp(Vector2 p, float floor, float limitX)
        {
            return new Vector2(Mathf.Clamp(p.x, -limitX, limitX), Mathf.Max(p.y, floor));
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform player, LevelBuilder levelBuilder)
        {
            this.player = player;
            this.levelBuilder = levelBuilder;
        }
#endif
    }
}
