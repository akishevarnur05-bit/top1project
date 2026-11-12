using System;
using System.Collections;
using CyberDefender.Combat;
using CyberDefender.Core;
using CyberDefender.Enemies;
using CyberDefender.Player;
using CyberDefender.Visuals;
using UnityEngine;

namespace CyberDefender.Tower
{
    /// <summary>
    /// Владелец игрового цикла: меню → выбор этажа → выбор героя → бой → следующий этаж / выбор уровня.
    /// Экраны меню рисует MenuController, но переключает их только TowerManager.
    /// После поражения: ладонь (J) — повтор этажа тем же героем, кулак (S) — к выбору уровня.
    /// Пройденные этажи запоминаются в PlayerPrefs.
    /// </summary>
    public sealed class TowerManager : MonoBehaviour
    {
        [Serializable]
        public sealed class FloorConfig
        {
            public string title;
            public LevelDefinition level;
            public VirusDefinition[] viruses;
            [Tooltip("Реплики первого вируса в катсцене перед боем. Пусто — без катсцены")]
            [TextArea(1, 3)] public string[] introLines = Array.Empty<string>();

            public FloorConfig() { }

            public FloorConfig(string title, LevelDefinition level, params VirusDefinition[] viruses)
            {
                this.title = title;
                this.level = level;
                this.viruses = viruses;
            }
        }

        [SerializeField] private FloorConfig[] floors = Array.Empty<FloorConfig>();
        [SerializeField] private PlayerController player;
        [SerializeField] private LevelBuilder levelBuilder;
        [SerializeField] private IntroCutscene introCutscene;
        [SerializeField] private Sprite enemySprite;

        [Header("Arena")]
        [SerializeField] private Vector2 menuPreviewPosition = new Vector2(0f, -1f);
        [SerializeField] private float enemySpacing = 1.8f;

        // Сколько раз игрок может восстановиться после вылета или гибели — задаёт уровень сложности
        private int startingBackups = 3;

        [Header("Backups")]
        [SerializeField] private float respawnDelay = 0.8f;
        [SerializeField] private float respawnInvulnerability = 2f;

        [Header("Flow")]
        [SerializeField] private float floorIntroDuration = 1.5f;
        [SerializeField] private float floorClearedDuration = 2f;
        [SerializeField] private float restartLockDuration = 1.2f;

        private const string ClearedKeyPrefix = "CyberDefender.FloorCleared.";

        public GameFlowState FlowState { get; private set; }
        public int CurrentFloor { get; private set; }
        public int FloorCount => floors.Length;
        /// <summary>Этаж, выбранный на экране выбора уровня; с него начинается бой.</summary>
        public int SelectedFloor { get; private set; }

        public FloorConfig GetFloor(int index) => floors[index];

        public static bool IsFloorCleared(int index) => PlayerPrefs.GetInt(ClearedKeyPrefix + index, 0) == 1;

        public int ClearedFloorCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < floors.Length; i++)
                    if (IsFloorCleared(i))
                        count++;
                return count;
            }
        }

        public CharacterDefinition LastCharacter { get; private set; }
        public int BackupsLeft { get; private set; }

        private float acceptInputAt;
        private bool respawning;

        private void Start()
        {
            GameEventBus.OnGestureDetected += HandleGesture;
            player.Health.Died += HandlePlayerDied;
            ShowMenu(GameFlowState.MainMenu);
        }

        private void OnDestroy()
        {
            GameEventBus.OnGestureDetected -= HandleGesture;
            if (player != null && player.Health != null)
                player.Health.Died -= HandlePlayerDied;
        }

        private void Update()
        {
            if (FlowState != GameFlowState.Playing)
                return;

            Rect zone = levelBuilder.BlastZone;

            if (!respawning && !zone.Contains(player.transform.position))
            {
                GameEventBus.RaiseRingOut(true);
                LoseBackup();
            }

            for (int i = EnemyController.Active.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = EnemyController.Active[i];
                if (zone.Contains(enemy.transform.position))
                    continue;
                GameEventBus.RaiseRingOut(false);
                enemy.RingOut(); // смерть уберёт его из Active и, если он последний, закроет этаж
            }
        }

        // Навигацию по меню обрабатывает MenuController; здесь — только экраны после боя
        private void HandleGesture(GestureEvent e)
        {
            if (e.Phase != GesturePhase.Started || Time.time < acceptInputAt)
                return;

            if (FlowState == GameFlowState.GameOver)
            {
                if (e.Type == GestureType.OpenPalm)
                {
                    GameEventBus.RaiseUiFeedback(UiFeedback.Confirm);
                    StartRun(LastCharacter);
                }
                else if (e.Type == GestureType.Fist)
                {
                    GameEventBus.RaiseUiFeedback(UiFeedback.Back);
                    ShowMenu(GameFlowState.LevelSelect);
                }
            }
            else if (FlowState == GameFlowState.LevelComplete)
            {
                if (e.Type == GestureType.OpenPalm)
                {
                    GameEventBus.RaiseUiFeedback(UiFeedback.Confirm);
                    SelectedFloor = Mathf.Min(CurrentFloor + 1, floors.Length - 1);
                    StartRun(LastCharacter);
                }
                else if (e.Type == GestureType.Fist)
                {
                    GameEventBus.RaiseUiFeedback(UiFeedback.Back);
                    SelectedFloor = Mathf.Min(CurrentFloor + 1, floors.Length - 1);
                    ShowMenu(GameFlowState.LevelSelect);
                }
            }
            else if (FlowState == GameFlowState.Victory && e.Type == GestureType.OpenPalm)
            {
                GameEventBus.RaiseUiFeedback(UiFeedback.Confirm);
                ShowMenu(GameFlowState.MainMenu);
            }
        }

        public static bool IsMenuState(GameFlowState state) =>
            state == GameFlowState.MainMenu || state == GameFlowState.Controls || state == GameFlowState.Settings ||
            state == GameFlowState.LevelSelect || state == GameFlowState.CharacterSelect;

        public void ShowMenu(GameFlowState state)
        {
            if (!IsMenuState(state))
                return;

            StopAllCoroutines();
            AbortCutscene();
            respawning = false;
            ClearEnemies();
            player.SetControlEnabled(false);
            // Фоном меню служит выбранный этаж — на выборе героя видно, где придётся драться
            levelBuilder.Build(floors.Length > 0 ? floors[SelectedFloor].level : null);
            player.ResetForNewRun(state == GameFlowState.CharacterSelect ? menuPreviewPosition : levelBuilder.PlayerSpawn);
            SetFlow(state);
        }

        /// <summary>Выбор этажа на экране выбора уровня: арена за меню сразу перестраивается.</summary>
        public void SelectFloor(int index)
        {
            if (floors.Length == 0)
                return;
            int clamped = Mathf.Clamp(index, 0, floors.Length - 1);
            if (clamped == SelectedFloor)
                return;

            SelectedFloor = clamped;
            if (IsMenuState(FlowState))
            {
                levelBuilder.Build(floors[SelectedFloor].level);
                player.PlaceAt(FlowState == GameFlowState.CharacterSelect ? menuPreviewPosition : levelBuilder.PlayerSpawn);
            }
        }

        /// <summary>Бой на выбранном этаже (SelectedFloor) с полным запасом бэкапов.</summary>
        public void StartRun(CharacterDefinition character)
        {
            if (floors.Length == 0)
            {
                Debug.LogError("[TowerManager] Не настроено ни одного этажа.");
                return;
            }

            if (character != null)
            {
                LastCharacter = character;
                player.ApplyCharacter(character);
            }

            StopAllCoroutines();
            AbortCutscene();
            respawning = false;
            player.ResetForNewRun(levelBuilder.PlayerSpawn);
            startingBackups = Mathf.Max(1, GameSettings.Profile.Backups);
            BackupsLeft = startingBackups;
            GameEventBus.RaisePlayerBackupsChanged(BackupsLeft, startingBackups);
            StartCoroutine(RunFloor(Mathf.Clamp(SelectedFloor, 0, floors.Length - 1)));
        }

        private IEnumerator RunFloor(int index)
        {
            CurrentFloor = index;
            FloorConfig floor = floors[index];

            ClearEnemies();
            player.SetControlEnabled(false);
            levelBuilder.Build(floor.level);
            player.PlaceAt(levelBuilder.PlayerSpawn);

            for (int i = 0; i < floor.viruses.Length; i++)
                SpawnEnemy(floor.viruses[i], levelBuilder.EnemySpawn(i, enemySpacing), isBoss: index == floors.Length - 1);

            GameEventBus.RaiseFloorStarted(index, floors.Length, floor.title);
            SetFlow(GameFlowState.FloorIntro);

            yield return new WaitForSeconds(floorIntroDuration);

            if (introCutscene != null && floor.introLines != null && floor.introLines.Length > 0 && EnemyController.Active.Count > 0)
            {
                SetFlow(GameFlowState.Cutscene);
                yield return introCutscene.Play(EnemyController.Active[0], floor.introLines);
            }

            SetFlow(GameFlowState.Playing);
            player.SetControlEnabled(true);
            for (int i = 0; i < EnemyController.Active.Count; i++)
                EnemyController.Active[i].SetAiEnabled(true);
        }

        private IEnumerator FloorCleared()
        {
            SetFlow(GameFlowState.FloorCleared);
            player.SetControlEnabled(false);
            PlayerPrefs.SetInt(ClearedKeyPrefix + CurrentFloor, 1);
            PlayerPrefs.Save();

            yield return new WaitForSeconds(floorClearedDuration);

            acceptInputAt = Time.time + restartLockDuration;
            SetFlow(CurrentFloor + 1 < floors.Length ? GameFlowState.LevelComplete : GameFlowState.Victory);
        }

        private void HandleEnemyDied(Health _)
        {
            // EnemyController подписан раньше и уже убрал себя из Active
            if (FlowState == GameFlowState.Playing && EnemyController.Active.Count == 0)
                StartCoroutine(FloorCleared());
        }

        private void HandlePlayerDied(Health _)
        {
            if (FlowState == GameFlowState.Playing && !respawning)
                LoseBackup();
        }

        /// <summary>Вылет или гибель: тратим бэкап и восстанавливаемся над ареной, либо проигрыш.</summary>
        private void LoseBackup()
        {
            BackupsLeft--;
            GameEventBus.RaisePlayerBackupsChanged(Mathf.Max(BackupsLeft, 0), startingBackups);
            player.SetControlEnabled(false);

            if (BackupsLeft > 0)
            {
                StartCoroutine(RespawnPlayer());
                return;
            }

            StopAllCoroutines();
            for (int i = 0; i < EnemyController.Active.Count; i++)
                EnemyController.Active[i].SetAiEnabled(false);

            acceptInputAt = Time.time + restartLockDuration;
            SetFlow(GameFlowState.GameOver);
        }

        private IEnumerator RespawnPlayer()
        {
            respawning = true;
            yield return new WaitForSeconds(respawnDelay);
            respawning = false;

            // Последний вирус мог погибнуть, пока игрок падал, — всё равно восстанавливаем к следующему этажу
            if (FlowState != GameFlowState.Playing && FlowState != GameFlowState.FloorCleared)
                yield break;
            player.Respawn(levelBuilder.RespawnPoint, respawnInvulnerability);
            if (FlowState != GameFlowState.Playing)
                player.SetControlEnabled(false);
        }

        private void SpawnEnemy(VirusDefinition definition, Vector2 position, bool isBoss)
        {
            var go = new GameObject(definition.displayName);
            go.layer = player.gameObject.layer; // тот же слой, что у игрока — проверка земли не считает врагов полом
            go.transform.position = position;

            // Блок-заглушка: используется, пока у вируса нет пиксель-арт анимаций
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = enemySprite;
            body.color = definition.color;
            body.sortingOrder = 1;

            var eye = new GameObject("Eye");
            eye.transform.SetParent(bodyGo.transform, false);
            eye.transform.localPosition = new Vector3(0.25f, 0.25f, 0f);
            eye.transform.localScale = new Vector3(0.35f, 0.12f, 1f);
            var eyeRenderer = eye.AddComponent<SpriteRenderer>();
            eyeRenderer.sprite = enemySprite;
            eyeRenderer.color = new Color(1f, 0.1f, 0.1f);
            eyeRenderer.sortingOrder = 2;

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            spriteGo.AddComponent<SpriteRenderer>().sortingOrder = 1;
            spriteGo.AddComponent<SpriteAnimator>();

            go.AddComponent<BoxCollider2D>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<Health>();

            var enemy = go.AddComponent<EnemyController>();
            enemy.Init(definition, player, levelBuilder.StageHalfWidth, levelBuilder.GroundTop, isBoss);
            enemy.Health.Died += HandleEnemyDied;
        }

        private static void ClearEnemies()
        {
            for (int i = EnemyController.Active.Count - 1; i >= 0; i--)
                Destroy(EnemyController.Active[i].gameObject);
            EnemyController.Active.Clear();
        }

        private void AbortCutscene()
        {
            if (introCutscene != null)
                introCutscene.Abort();
        }

        private void SetFlow(GameFlowState state)
        {
            FlowState = state;
            GameEventBus.RaiseGameFlowChanged(state);
        }

#if UNITY_EDITOR
        public void EditorConfigure(PlayerController player, LevelBuilder levelBuilder, IntroCutscene introCutscene, Sprite enemySprite, FloorConfig[] floors)
        {
            this.player = player;
            this.levelBuilder = levelBuilder;
            this.introCutscene = introCutscene;
            this.enemySprite = enemySprite;
            this.floors = floors;
        }
#endif
    }
}
