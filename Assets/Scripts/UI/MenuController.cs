using System.Collections;
using System.Collections.Generic;
using System.Text;
using CyberDefender.Core;
using CyberDefender.Enemies;
using CyberDefender.Player;
using CyberDefender.Tower;
using UnityEngine;
using UnityEngine.UI;

namespace CyberDefender.UI
{
    /// <summary>
    /// Заставка загрузки, главное меню, управление, выбор этажа и выбор защитника.
    /// Управляется теми же жестами, что и бой: свайп (A/D, ←/→) — выбор, ладонь (J, Enter) — подтвердить,
    /// кулак (S) — назад. Мышь тоже работает.
    /// </summary>
    public sealed class MenuController : MonoBehaviour
    {
        private sealed class Card
        {
            public RectTransform Root;
            public Image Frame;
            public Text Status;
        }

        private static readonly string[] MainItems = { "ИГРАТЬ", "НАСТРОЙКИ", "УПРАВЛЕНИЕ", "ВЫХОД" };
        private static readonly string[] SettingNames = { "СЛОЖНОСТЬ", "МУЗЫКА", "ЗВУКИ", "КАМЕРА И МИКРОФОН" };
        private static readonly Color[] DifficultyColors =
        {
            new Color(0.3f, 1f, 0.6f), new Color(0.2f, 0.9f, 1f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.25f, 0.3f),
        };
        private static readonly Color FrameIdle = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color CardBody = new Color(0.03f, 0.06f, 0.1f, 0.92f);
        private static readonly Color GlitchRed = new Color(1f, 0.15f, 0.35f, 0.75f);
        private static readonly Color GlitchCyan = new Color(0.1f, 0.95f, 1f, 0.75f);

        // Строки «BIOS» на заставке: текст и цвет
        private static readonly (string text, string color)[] BootLines =
        {
            ("KERNEL DEFENSE BIOS  v6.6.7", "#33E6FF"),
            ("(C) CyberDefender Labs. Все процессы защищены.", "#8899AA"),
            ("", "#FFFFFF"),
            ("Проверка памяти ............ 65536K OK", "#FFFFFF"),
            ("Жестовый интерфейс ......... OK", "#FFFFFF"),
            ("Голосовой модуль ........... ОЖИДАНИЕ", "#FFFFFF"),
            ("Сканирование ядра .......... 5 секторов", "#FFFFFF"),
            ("!! ОБНАРУЖЕНО ЗАРАЖЕНИЕ: Trojan / Ransomware / Rootkit", "#FF4060"),
            ("Запуск протокола защиты...", "#4DFF99"),
        };

        [SerializeField] private TowerManager tower;
        [SerializeField] private PlayerController player;
        [SerializeField] private CharacterDefinition[] characters;
        [SerializeField] private float inputLockAfterScreenChange = 0.25f;
        [SerializeField, Tooltip("Символов в секунду на заставке загрузки")] private float bootTypeSpeed = 90f;

        private Image dim;
        private GameObject bootScreen;
        private Text bootText;
        private GameObject mainScreen;
        private GameObject controlsScreen;
        private GameObject settingsScreen;
        private readonly List<Card> settingCards = new List<Card>();
        private Text difficultyDescription;
        private Text levelDifficultyText;
        private int settingIndex;
        private GameObject levelScreen;
        private GameObject selectScreen;
        private readonly List<Card> mainCards = new List<Card>();
        private readonly List<Card> levelCards = new List<Card>();
        private readonly List<Card> characterCards = new List<Card>();
        private int mainIndex;
        private int levelIndex;
        private int characterIndex;
        private float acceptInputAt;

        // Титульный экран
        private CanvasGroup titleGroup;
        private RectTransform logo;
        private RectTransform logoRed;
        private RectTransform logoCyan;
        private Text tagline;
        private Text progressText;
        private RectTransform scanBar;
        private GameObject scanlines;
        private Text floorLabel;
        private bool bootStarted;
        private bool booting;
        private Coroutine bootRoutine;
        private float revealTime = -10f;
        private float glitchCooldown = 2f;
        private float glitchBurst;

        private const string TaglineText = "> обнаружено заражение ядра ОС. Запустите защитника";
        private static readonly Vector2 LogoPosition = new Vector2(0f, 220f);

        private void Awake()
        {
            BuildUi();
            HandleFlow(GameFlowState.MainMenu);
        }

        private void OnEnable()
        {
            GameEventBus.OnGestureDetected += HandleGesture;
            GameEventBus.OnGameFlowChanged += HandleFlow;
        }

        private void OnDisable()
        {
            GameEventBus.OnGestureDetected -= HandleGesture;
            GameEventBus.OnGameFlowChanged -= HandleFlow;
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            // Лёгкая «пульсация» выбранной карточки
            float pulse = 1.04f + 0.015f * Mathf.Sin(now * 5f);
            if (mainScreen.activeSelf)
            {
                AnimateSelection(mainCards, mainIndex, pulse);
                AnimateTitle(now);
            }
            if (levelScreen.activeSelf)
                AnimateSelection(levelCards, levelIndex, pulse);
            if (settingsScreen.activeSelf)
                AnimateSelection(settingCards, settingIndex, 1f + (pulse - 1f) * 0.5f);
            if (selectScreen.activeSelf)
                AnimateSelection(characterCards, characterIndex, pulse);

            // Полоса развёртки медленно ползёт сверху вниз по всем экранам меню
            float y = Mathf.Repeat(now * 0.18f, 1.2f);
            scanBar.anchorMin = new Vector2(0f, 1.1f - y);
            scanBar.anchorMax = new Vector2(1f, 1.1f - y);
        }

        // ---------- Заставка и титульный экран ----------

        private IEnumerator BootSequence()
        {
            booting = true;
            bootScreen.SetActive(true);
            titleGroup.alpha = 0f;

            var done = new StringBuilder();
            float budget = 0f;

            foreach ((string text, string color) in BootLines)
            {
                for (int shown = 0; shown <= text.Length;)
                {
                    bootText.text = $"{done}<color={color}>{text.Substring(0, shown)}</color>_";
                    yield return null;
                    budget += Time.unscaledDeltaTime * bootTypeSpeed;
                    int step = Mathf.FloorToInt(budget);
                    budget -= step;
                    if (shown == text.Length)
                        break;
                    shown = Mathf.Min(text.Length, shown + step);
                }

                done.Append($"<color={color}>{text}</color>\n");
                GameEventBus.RaiseUiFeedback(UiFeedback.BootLine);
                yield return new WaitForSecondsRealtime(text.Length == 0 ? 0.05f : 0.14f);
            }

            bootText.text = done.ToString();
            yield return new WaitForSecondsRealtime(0.5f);
            FinishBoot();
        }

        private void FinishBoot()
        {
            if (!booting)
                return;
            booting = false;
            if (bootRoutine != null)
                StopCoroutine(bootRoutine);

            bootScreen.SetActive(false);
            revealTime = Time.unscaledTime;
            glitchBurst = 0.6f; // логотип «вспыхивает» с сильным глитчем
            acceptInputAt = Time.unscaledTime + 0.5f;
            GameEventBus.RaiseUiFeedback(UiFeedback.TitleReveal);
        }

        private void AnimateTitle(float now)
        {
            float dt = Time.unscaledDeltaTime;
            float sinceReveal = now - revealTime;

            // Появление: проявление и «наезд» логотипа
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sinceReveal / 0.5f));
            titleGroup.alpha = booting ? 0f : k;
            float scale = Mathf.Lerp(1.25f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sinceReveal / 0.4f)));
            logo.localScale = new Vector3(scale, scale, 1f);

            // Глитч: редкие короткие всплески, между ними — лёгкий хроматический сдвиг
            glitchCooldown -= dt;
            if (glitchCooldown <= 0f)
            {
                glitchBurst = Random.Range(0.08f, 0.22f);
                glitchCooldown = Random.Range(1.8f, 4.5f);
            }

            float strength = 0.15f;
            if (glitchBurst > 0f)
            {
                glitchBurst -= dt;
                strength = 1f;
            }

            Vector2 jitter = strength >= 1f ? new Vector2(Random.Range(-1f, 1f) * 8f, 0f) : Vector2.zero;
            logo.anchoredPosition = LogoPosition + jitter;
            logoRed.anchoredPosition = new Vector2(-4f + Random.Range(-1f, 1f) * 16f * strength, Random.Range(-1f, 1f) * 5f * strength);
            logoCyan.anchoredPosition = new Vector2(4f + Random.Range(-1f, 1f) * 16f * strength, Random.Range(-1f, 1f) * 5f * strength);

            bool cursor = Mathf.Repeat(now, 1f) < 0.55f;
            tagline.text = cursor ? TaglineText + "_" : TaglineText + " ";
        }

        // ---------- Навигация ----------

        private void HandleFlow(GameFlowState state)
        {
            bool menu = TowerManager.IsMenuState(state);
            mainScreen.SetActive(state == GameFlowState.MainMenu);
            controlsScreen.SetActive(state == GameFlowState.Controls);
            settingsScreen.SetActive(state == GameFlowState.Settings);
            levelScreen.SetActive(state == GameFlowState.LevelSelect);
            selectScreen.SetActive(state == GameFlowState.CharacterSelect);

            dim.enabled = menu;
            scanlines.SetActive(menu);
            // Чем важнее увидеть арену за меню, тем слабее затемнение
            switch (state)
            {
                case GameFlowState.CharacterSelect: dim.color = new Color(0f, 0.02f, 0.05f, 0.35f); break;
                case GameFlowState.LevelSelect: dim.color = new Color(0f, 0.02f, 0.05f, 0.5f); break;
                case GameFlowState.MainMenu: dim.color = new Color(0f, 0.01f, 0.03f, 0.72f); break;
                default: dim.color = new Color(0f, 0.01f, 0.03f, 0.85f); break;
            }

            acceptInputAt = Mathf.Max(acceptInputAt, Time.unscaledTime + inputLockAfterScreenChange);

            if (state == GameFlowState.MainMenu)
            {
                // Заставка загрузки — один раз за запуск игры
                if (!bootStarted)
                {
                    bootStarted = true;
                    bootRoutine = StartCoroutine(BootSequence());
                }
                RefreshProgress();
            }
            else if (state == GameFlowState.LevelSelect)
            {
                levelIndex = tower.SelectedFloor;
                RefreshLevelStatus();
                DifficultyProfile profile = GameSettings.Profile;
                levelDifficultyText.text = $"Сложность: {profile.Name}   ·   бэкапов: {profile.Backups}   ·   меняется в настройках";
                levelDifficultyText.color = DifficultyColors[(int)GameSettings.Difficulty];
            }
            else if (state == GameFlowState.Settings)
            {
                RefreshSettings();
            }
            else if (state == GameFlowState.CharacterSelect)
            {
                PreviewCharacter();
                RefreshFloorLabel();
            }

            RefreshSelection();
        }

        private void HandleGesture(GestureEvent e)
        {
            if (e.Phase != GesturePhase.Started)
                return;

            if (booting)
            {
                FinishBoot(); // любой жест пропускает заставку
                return;
            }

            if (Time.unscaledTime < acceptInputAt)
                return;

            switch (tower.FlowState)
            {
                case GameFlowState.MainMenu:
                    if (e.Type == GestureType.SwipeLeft) StepMain(-1);
                    else if (e.Type == GestureType.SwipeRight) StepMain(1);
                    else if (e.Type == GestureType.OpenPalm) ConfirmMain();
                    break;

                case GameFlowState.Controls:
                    if (e.Type == GestureType.Fist || e.Type == GestureType.OpenPalm)
                        Back(GameFlowState.MainMenu);
                    break;

                case GameFlowState.Settings:
                    if (e.Type == GestureType.SwipeLeft) ChangeSetting(-1);
                    else if (e.Type == GestureType.SwipeRight) ChangeSetting(1);
                    else if (e.Type == GestureType.OpenPalm || e.Type == GestureType.PointUp) StepSetting(1);
                    else if (e.Type == GestureType.Fist) Back(GameFlowState.MainMenu);
                    break;

                case GameFlowState.LevelSelect:
                    if (e.Type == GestureType.SwipeLeft) StepLevel(-1);
                    else if (e.Type == GestureType.SwipeRight) StepLevel(1);
                    else if (e.Type == GestureType.OpenPalm) ConfirmLevel();
                    else if (e.Type == GestureType.Fist) Back(GameFlowState.MainMenu);
                    break;

                case GameFlowState.CharacterSelect:
                    if (e.Type == GestureType.SwipeLeft) StepCharacter(-1);
                    else if (e.Type == GestureType.SwipeRight) StepCharacter(1);
                    else if (e.Type == GestureType.OpenPalm) ConfirmCharacter();
                    else if (e.Type == GestureType.Fist) Back(GameFlowState.LevelSelect);
                    break;
            }
        }

        private void Back(GameFlowState target)
        {
            GameEventBus.RaiseUiFeedback(UiFeedback.Back);
            tower.ShowMenu(target);
        }

        private void StepMain(int delta)
        {
            mainIndex = Wrap(mainIndex + delta, MainItems.Length);
            GameEventBus.RaiseUiFeedback(UiFeedback.Move);
            RefreshSelection();
        }

        private void StepLevel(int delta)
        {
            if (tower.FloorCount == 0)
                return;
            levelIndex = Wrap(levelIndex + delta, tower.FloorCount);
            GameEventBus.RaiseUiFeedback(UiFeedback.Move);
            tower.SelectFloor(levelIndex);
            RefreshSelection();
        }

        private void StepCharacter(int delta)
        {
            if (characters.Length == 0)
                return;
            characterIndex = Wrap(characterIndex + delta, characters.Length);
            GameEventBus.RaiseUiFeedback(UiFeedback.Move);
            PreviewCharacter();
            RefreshSelection();
        }

        private void ConfirmMain()
        {
            GameEventBus.RaiseUiFeedback(UiFeedback.Confirm);
            switch (mainIndex)
            {
                case 0:
                    tower.ShowMenu(GameFlowState.LevelSelect);
                    break;
                case 1:
                    tower.ShowMenu(GameFlowState.Settings);
                    break;
                case 2:
                    tower.ShowMenu(GameFlowState.Controls);
                    break;
                case 3:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        private void StepSetting(int delta)
        {
            settingIndex = Wrap(settingIndex + delta, SettingNames.Length);
            GameEventBus.RaiseUiFeedback(UiFeedback.Move);
            RefreshSelection();
        }

        private void ChangeSetting(int delta)
        {
            switch (settingIndex)
            {
                case 0:
                    GameSettings.SetDifficulty((Difficulty)Wrap((int)GameSettings.Difficulty + delta, GameSettings.DifficultyCount));
                    break;
                case 1:
                    GameSettings.SetMusicVolume(StepVolume(GameSettings.MusicVolume, delta));
                    break;
                case 2:
                    GameSettings.SetSfxVolume(StepVolume(GameSettings.SfxVolume, delta));
                    break;
                case 3:
                    GameSettings.SetAutoStartBridge(!GameSettings.AutoStartBridge);
                    break;
            }
            // Щелчок после изменения — сразу слышно новую громкость звуков
            GameEventBus.RaiseUiFeedback(UiFeedback.Move);
            RefreshSettings();
        }

        private static float StepVolume(float value, int delta) => Wrap(Mathf.RoundToInt(value * 10f) + delta, 11) / 10f;

        private void ClickSetting(int index)
        {
            if (Time.unscaledTime < acceptInputAt)
                return;
            if (index == settingIndex)
            {
                ChangeSetting(1);
                return;
            }
            settingIndex = index;
            GameEventBus.RaiseUiFeedback(UiFeedback.Move);
            RefreshSelection();
        }

        private void RefreshSettings()
        {
            Difficulty difficulty = GameSettings.Difficulty;
            DifficultyProfile p = GameSettings.Profile;
            string[] values =
            {
                p.Name,
                $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%",
                $"{Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}%",
                GameSettings.AutoStartBridge ? "АВТОЗАПУСК" : "ВРУЧНУЮ",
            };
            for (int i = 0; i < settingCards.Count; i++)
            {
                settingCards[i].Status.text = $"<   {values[i]}   >";
                settingCards[i].Status.color = i == 0 ? DifficultyColors[(int)difficulty] : Color.white;
            }

            difficultyDescription.text =
                $"{p.Description}\n\n" +
                $"HP вирусов ×{p.EnemyHealth:0.#}   ·   урон ×{p.EnemyDamage:0.#}   ·   замах ×{p.WindupScale:0.##}   ·   " +
                $"серия ударов: {p.ComboLength}   ·   блок вируса: {Mathf.RoundToInt(p.GuardChance * 100f)}%   ·   бэкапов: {p.Backups}";
            difficultyDescription.color = Color.Lerp(Color.white, DifficultyColors[(int)difficulty], 0.35f);
        }

        private void ConfirmLevel()
        {
            if (tower.FloorCount == 0)
                return;
            GameEventBus.RaiseUiFeedback(UiFeedback.Confirm);
            tower.SelectFloor(levelIndex);
            tower.ShowMenu(GameFlowState.CharacterSelect);
        }

        private void ConfirmCharacter()
        {
            if (characters.Length == 0)
                return;
            GameEventBus.RaiseUiFeedback(UiFeedback.Confirm);
            tower.StartRun(characters[characterIndex]);
        }

        private void PreviewCharacter()
        {
            if (characters.Length > 0)
                player.ApplyCharacter(characters[characterIndex]);
        }

        // Клик мышью: первый клик выбирает, повторный — подтверждает
        private void ClickMain(int index)
        {
            if (booting || Time.unscaledTime < acceptInputAt)
                return;
            mainIndex = index;
            ConfirmMain();
        }

        private void ClickLevel(int index)
        {
            if (Time.unscaledTime < acceptInputAt)
                return;
            if (index == levelIndex)
            {
                ConfirmLevel();
                return;
            }
            StepLevel(index - levelIndex);
        }

        private void ClickCharacter(int index)
        {
            if (Time.unscaledTime < acceptInputAt)
                return;
            if (index == characterIndex)
            {
                ConfirmCharacter();
                return;
            }
            StepCharacter(index - characterIndex);
        }

        private static int Wrap(int value, int count) => (value % count + count) % count;

        private void RefreshSelection()
        {
            for (int i = 0; i < mainCards.Count; i++)
                SetCardSelected(mainCards[i], i == mainIndex, UiFactory.Cyan);

            for (int i = 0; i < levelCards.Count; i++)
                SetCardSelected(levelCards[i], i == levelIndex, FloorColor(i));

            for (int i = 0; i < settingCards.Count; i++)
                SetCardSelected(settingCards[i], i == settingIndex, UiFactory.Cyan);

            for (int i = 0; i < characterCards.Count; i++)
                SetCardSelected(characterCards[i], i == characterIndex, characters[i].color);
        }

        private void RefreshProgress()
        {
            int total = tower.FloorCount;
            int cleared = tower.ClearedFloorCount;
            var bar = new StringBuilder();
            for (int i = 0; i < total; i++)
                bar.Append(TowerManager.IsFloorCleared(i) ? "■" : "□");
            progressText.text = $"ОЧИЩЕНО ЭТАЖЕЙ  {bar}  {cleared}/{total}";
        }

        private void RefreshLevelStatus()
        {
            for (int i = 0; i < levelCards.Count; i++)
            {
                bool cleared = TowerManager.IsFloorCleared(i);
                levelCards[i].Status.text = cleared ? "ОЧИЩЕН" : "ЗАРАЖЁН";
                levelCards[i].Status.color = cleared ? UiFactory.EnergyColor : UiFactory.Danger;
            }
        }

        private void RefreshFloorLabel()
        {
            if (tower.FloorCount == 0)
                return;
            int index = tower.SelectedFloor;
            floorLabel.text = $"ЭТАЖ {index + 1}/{tower.FloorCount} · {tower.GetFloor(index).title}";
            floorLabel.color = FloorColor(index);
        }

        private Color FloorColor(int index)
        {
            TowerManager.FloorConfig floor = tower.GetFloor(index);
            return floor.level != null ? floor.level.platformColor : UiFactory.Cyan;
        }

        private static void SetCardSelected(Card card, bool selected, Color accent)
        {
            card.Frame.color = selected ? accent : FrameIdle;
            if (!selected)
                card.Root.localScale = Vector3.one;
        }

        private static void AnimateSelection(List<Card> cards, int index, float scale)
        {
            if (index >= 0 && index < cards.Count)
                cards[index].Root.localScale = new Vector3(scale, scale, 1f);
        }

        // ---------- Построение UI ----------

        private void BuildUi()
        {
            RectTransform root = UiFactory.CreateCanvas(transform, "Menu Canvas", 20);

            RectTransform dimRect = UiFactory.CreateStretched("Dim", root);
            dim = UiFactory.AddImage(dimRect, Color.black);

            BuildMainScreen(root);
            BuildControlsScreen(root);
            BuildSettingsScreen(root);
            BuildLevelScreen(root);
            BuildSelectScreen(root);
            BuildScanlines(root);
            BuildBootScreen(root);
        }

        private void BuildMainScreen(RectTransform root)
        {
            RectTransform screen = UiFactory.CreateStretched("MainMenu", root);
            mainScreen = screen.gameObject;
            titleGroup = screen.gameObject.AddComponent<CanvasGroup>();

            UiFactory.CreateText(screen, "// KERNEL DEFENSE SYSTEM · ПРОТОКОЛ 6.6", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(1800f, 36f), UiFactory.Muted);

            // Логотип: две цветные копии за основным текстом дают хроматический глитч
            logo = UiFactory.CreateRect("Logo", screen, new Vector2(0.5f, 0.5f), LogoPosition, new Vector2(1800f, 180f));
            logo.pivot = new Vector2(0.5f, 0.5f);
            logo.anchoredPosition = LogoPosition;
            logoRed = LogoLayer(logo, GlitchRed);
            logoCyan = LogoLayer(logo, GlitchCyan);
            LogoLayer(logo, Color.white);

            RectTransform lineLeft = UiFactory.CreateRect("Line", screen, new Vector2(0.5f, 0.5f), new Vector2(-760f, 118f), new Vector2(380f, 3f));
            UiFactory.AddImage(lineLeft, UiFactory.EnergyColor);
            RectTransform lineRight = UiFactory.CreateRect("Line", screen, new Vector2(0.5f, 0.5f), new Vector2(380f, 118f), new Vector2(380f, 3f));
            UiFactory.AddImage(lineRight, UiFactory.EnergyColor);
            UiFactory.CreateText(screen, "K E R N E L   C L A S H", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(760f, 60f), UiFactory.EnergyColor);

            tagline = UiFactory.CreateText(screen, TaglineText, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(1600f, 40f), UiFactory.Muted);

            for (int i = 0; i < MainItems.Length; i++)
            {
                int index = i;
                Card card = CreateCard(screen, new Vector2((i - (MainItems.Length - 1) * 0.5f) * 420f, -110f), new Vector2(380f, 110f), () => ClickMain(index), out RectTransform body);
                UiFactory.CreateText(body, $"0{i + 1}", 20, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(60f, 26f), UiFactory.Muted);
                UiFactory.CreateText(body, MainItems[i], 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 90f), Color.white);
                mainCards.Add(card);
            }

            progressText = UiFactory.CreateText(screen, "", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(1200f, 40f), UiFactory.EnergyColor);
            UiFactory.CreateText(screen, "v0.9 · жесты + голос", 20, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 14f), new Vector2(500f, 30f), UiFactory.Muted);

            CreateHint(screen, "Свайп ← → (A / D) — выбор      Ладонь (J / Enter) — подтвердить");
        }

        private static RectTransform LogoLayer(RectTransform logo, Color color)
        {
            RectTransform layer = UiFactory.CreateStretched("Layer", logo);
            Text text = UiFactory.CreateText(layer, "CYBERDEFENDER", 150, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800f, 180f), color);
            text.fontStyle = FontStyle.Bold;
            if (color != Color.white)
                Destroy(text.GetComponent<Outline>()); // у цветных копий обводка лишняя
            return layer;
        }

        private void BuildControlsScreen(RectTransform root)
        {
            RectTransform screen = UiFactory.CreateStretched("Controls", root);
            controlsScreen = screen.gameObject;

            UiFactory.CreateText(screen, "УПРАВЛЕНИЕ", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 370f), new Vector2(1600f, 90f), UiFactory.Cyan);

            const string gestures = "ЖЕСТ (камера)\n\nСвайп ладонью ← →\nУказательный палец вверх\nОткрытая ладонь\nДве ладони вперёд\nКулак (держать)";
            const string keys = "КЛАВИША\n\nA / D\nW\nJ\nK\nS";
            const string actions = "ДЕЙСТВИЕ\n\nДвижение\nПрыжок (дважды — в воздухе)\nЛёгкий удар\nТяжёлый удар / толчок\nБлок";
            UiFactory.CreateText(screen, gestures, 30, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(-430f, 150f), new Vector2(560f, 300f), Color.white);
            UiFactory.CreateText(screen, keys, 30, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(40f, 150f), new Vector2(240f, 300f), UiFactory.Cyan);
            UiFactory.CreateText(screen, actions, 30, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(420f, 150f), new Vector2(460f, 300f), UiFactory.EnergyColor);

            const string voice =
                "ГОЛОС — когда Kernel Energy = 100% (копится за попадания и блоки)\n\n" +
                "«PURGE SYSTEM!»  (1) — супер-атака по всем вирусам рядом\n" +
                "«FIREWALL ACTIVATE!»  (2) — неуязвимость\n" +
                "«ISOLATE!»  (3) — стан ближайшего вируса на 2.5 с\n\n" +
                "Вирус мигает белым и пищит перед ударом — успейте сжать кулак.   M — музыка вкл/выкл";
            UiFactory.CreateText(screen, voice, 28, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1600f, 300f), new Color(1f, 0.6f, 1f));

            CreateHint(screen, "Ладонь (J) или кулак (S) — назад");
        }

        private void BuildSettingsScreen(RectTransform root)
        {
            RectTransform screen = UiFactory.CreateStretched("Settings", root);
            settingsScreen = screen.gameObject;

            UiFactory.CreateText(screen, "НАСТРОЙКИ", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 400f), new Vector2(1600f, 90f), UiFactory.Cyan);

            for (int i = 0; i < SettingNames.Length; i++)
            {
                int index = i;
                Card card = CreateCard(screen, new Vector2(0f, 260f - i * 105f), new Vector2(1100f, 88f), () => ClickSetting(index), out RectTransform body);
                UiFactory.CreateText(body, SettingNames[i], 38, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(480f, 60f), Color.white);
                card.Status = UiFactory.CreateText(body, "", 38, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(560f, 60f), Color.white);
                settingCards.Add(card);
            }

            difficultyDescription = UiFactory.CreateText(screen, "", 28, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1500f, 200f), Color.white);
            UiFactory.CreateText(screen, "Сложность действует со следующего боя. Пройденные этажи не сбрасываются.", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), new Vector2(1500f, 30f), UiFactory.Muted);

            CreateHint(screen, "Свайп ← → (A / D) — изменить      Ладонь (J) / палец вверх (W) — следующий пункт      Кулак (S) — назад");
        }

        private void BuildLevelScreen(RectTransform root)
        {
            RectTransform screen = UiFactory.CreateStretched("LevelSelect", root);
            levelScreen = screen.gameObject;

            UiFactory.CreateText(screen, "БАШНЯ ЗАРАЖЕНИЯ", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(1600f, 80f), UiFactory.Cyan);
            levelDifficultyText = UiFactory.CreateText(screen, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 370f), new Vector2(1600f, 36f), UiFactory.Muted);
            UiFactory.CreateText(screen, "Арена позади меню меняется вместе с выбором этажа", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(1600f, 30f), UiFactory.Muted);

            int count = tower != null ? tower.FloorCount : 0;
            float spacing = Mathf.Min(360f, 1800f / Mathf.Max(1, count));
            float width = spacing - 30f;

            for (int i = 0; i < count; i++)
            {
                int index = i;
                TowerManager.FloorConfig floor = tower.GetFloor(i);
                Color accent = FloorColor(i);
                bool boss = i == count - 1;

                float x = (i - (count - 1) * 0.5f) * spacing;
                Card card = CreateCard(screen, new Vector2(x, 20f), new Vector2(width, 440f), () => ClickLevel(index), out RectTransform body);

                RectTransform band = UiFactory.CreateRect("Band", body, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width - 10f, 10f));
                UiFactory.AddImage(band, accent);

                float inner = width - 40f;
                UiFactory.CreateText(body, $"ЭТАЖ {i + 1}", 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -26f), new Vector2(inner, 34f), UiFactory.Muted);
                if (boss)
                    UiFactory.CreateText(body, "БОСС", 26, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-20f, -26f), new Vector2(120f, 34f), UiFactory.Danger);
                UiFactory.CreateText(body, floor.title, 34, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -66f), new Vector2(inner, 84f), accent);

                UiFactory.CreateText(body, "УГРОЗЫ", 20, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -160f), new Vector2(inner, 28f), UiFactory.Muted);
                UiFactory.CreateText(body, DescribeViruses(floor.viruses), 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -188f), new Vector2(inner, 90f), Color.white);

                UiFactory.CreateText(body, "СЛОЖНОСТЬ", 20, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -285f), new Vector2(inner, 28f), UiFactory.Muted);
                UiFactory.CreateText(body, Pips(i + 1, count), 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -313f), new Vector2(inner, 40f), accent);

                Text status = UiFactory.CreateText(body, "", 28, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(20f, 18f), new Vector2(inner, 36f), UiFactory.Danger);
                card.Status = status;
                levelCards.Add(card);
            }

            CreateHint(screen, "Свайп ← → (A / D) — этаж      Ладонь (J) — выбрать героя      Кулак (S) — назад");
        }

        private static string DescribeViruses(VirusDefinition[] viruses)
        {
            if (viruses == null || viruses.Length == 0)
                return "—";

            // Одинаковые вирусы сворачиваем: «Trojan-Horse ×2»
            var counts = new List<(string name, int count)>();
            foreach (VirusDefinition v in viruses)
            {
                if (v == null)
                    continue;
                int found = counts.FindIndex(c => c.name == v.displayName);
                if (found >= 0)
                    counts[found] = (v.displayName, counts[found].count + 1);
                else
                    counts.Add((v.displayName, 1));
            }

            var sb = new StringBuilder();
            foreach ((string name, int count) in counts)
                sb.AppendLine(count > 1 ? $"{name} ×{count}" : name);
            return sb.ToString().TrimEnd();
        }

        private static string Pips(int value, int total)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < total; i++)
                sb.Append(i < value ? "■" : "□");
            return sb.ToString();
        }

        private void BuildSelectScreen(RectTransform root)
        {
            RectTransform screen = UiFactory.CreateStretched("CharacterSelect", root);
            selectScreen = screen.gameObject;

            UiFactory.CreateText(screen, "ВЫБЕРИТЕ ЗАЩИТНИКА", 64, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 445f), new Vector2(1600f, 80f), UiFactory.Cyan);
            floorLabel = UiFactory.CreateText(screen, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 390f), new Vector2(1600f, 40f), Color.white);

            float maxHp = 1f, maxSpeed = 1f, maxPower = 1f;
            foreach (CharacterDefinition c in characters)
            {
                maxHp = Mathf.Max(maxHp, c.maxHealth);
                maxSpeed = Mathf.Max(maxSpeed, c.moveSpeed);
                maxPower = Mathf.Max(maxPower, c.heavyDamage);
            }

            for (int i = 0; i < characters.Length; i++)
            {
                int index = i;
                CharacterDefinition c = characters[i];
                float x = (i - (characters.Length - 1) * 0.5f) * 580f;
                Card card = CreateCard(screen, new Vector2(x, 140f), new Vector2(530f, 400f), () => ClickCharacter(index), out RectTransform body);

                RectTransform swatch = UiFactory.CreateRect("Swatch", body, new Vector2(0f, 1f), new Vector2(24f, -26f), new Vector2(44f, 64f));
                UiFactory.AddImage(swatch, c.color);
                UiFactory.CreateText(body, c.displayName, 44, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(86f, -20f), new Vector2(420f, 52f), Color.white);
                UiFactory.CreateText(body, c.role, 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(86f, -66f), new Vector2(420f, 34f), c.color);

                AddStat(body, "ЗДОРОВЬЕ", c.maxHealth / maxHp, -120f, c.color);
                AddStat(body, "СКОРОСТЬ", c.moveSpeed / maxSpeed, -158f, c.color);
                AddStat(body, "СИЛА", c.heavyDamage / maxPower, -196f, c.color);
                AddStat(body, "ЗАЩИТА", 1f - c.blockDamageMultiplier, -234f, c.color);

                UiFactory.CreateText(body, c.description, 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(24f, -278f), new Vector2(470f, 110f), UiFactory.Muted);
                characterCards.Add(card);
            }

            CreateHint(screen, "Свайп ← → (A / D) — выбор      Ладонь (J) — в бой      Кулак (S) — к выбору этажа");
        }

        private void BuildScanlines(RectTransform root)
        {
            // Тонкие строки развёртки поверх меню — «экран старого терминала»
            var texture = new Texture2D(1, 4, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };
            var clear = new Color(0f, 0f, 0f, 0f);
            texture.SetPixels(new[] { new Color(0f, 0f, 0f, 0.22f), clear, clear, clear });
            texture.Apply();

            RectTransform lines = UiFactory.CreateStretched("Scanlines", root);
            var raw = lines.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.uvRect = new Rect(0f, 0f, 1f, 1080f / 4f);
            raw.raycastTarget = false;

            // Светлая полоса развёртки; позицию двигает Update
            scanBar = UiFactory.CreateRect("ScanBar", lines, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 140f));
            scanBar.anchorMin = new Vector2(0f, 1f);
            scanBar.anchorMax = new Vector2(1f, 1f);
            scanBar.pivot = new Vector2(0.5f, 0.5f);
            scanBar.sizeDelta = new Vector2(0f, 140f);
            UiFactory.AddImage(scanBar, new Color(0.2f, 0.9f, 1f, 0.035f));

            // Меню показывает полосы только на своих экранах
            lines.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            scanlines = lines.gameObject;
        }

        private void BuildBootScreen(RectTransform root)
        {
            RectTransform screen = UiFactory.CreateStretched("Boot", root);
            bootScreen = screen.gameObject;
            UiFactory.AddImage(screen, new Color(0.005f, 0.01f, 0.02f, 1f), raycastTarget: true);

            bootText = UiFactory.CreateText(screen, "", 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(120f, -120f), new Vector2(1700f, 800f), Color.white);
            bootText.supportRichText = true;
            UiFactory.CreateText(screen, "Любой жест, клавиша или клик — пропустить", 22, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(800f, 30f), UiFactory.Muted);
            var skip = screen.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(FinishBoot);
            bootScreen.SetActive(false);
        }

        private static void AddStat(RectTransform body, string label, float value, float y, Color color)
        {
            UiFactory.CreateText(body, label, 20, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(24f, y), new Vector2(160f, 26f), Color.white);
            RectTransform fill = UiFactory.CreateBar(body, new Vector2(0f, 1f), new Vector2(190f, y), new Vector2(310f, 24f), color, out _);
            UiFactory.SetFill(fill, value, false);
        }

        private static Card CreateCard(RectTransform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick, out RectTransform body)
        {
            RectTransform root = UiFactory.CreateRect("Card", parent, new Vector2(0.5f, 0.5f), position, size);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = position;

            Image frame = UiFactory.AddImage(root, FrameIdle, raycastTarget: true);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            body = UiFactory.CreateStretched("Body", root, 5f);
            UiFactory.AddImage(body, CardBody);

            return new Card { Root = root, Frame = frame };
        }

        private static void CreateHint(RectTransform screen, string text)
        {
            // Между полом арены и строкой статуса HUD
            UiFactory.CreateText(screen, text, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(1800f, 40f), UiFactory.Muted);
        }

#if UNITY_EDITOR
        public void EditorConfigure(TowerManager tower, PlayerController player, CharacterDefinition[] characters)
        {
            this.tower = tower;
            this.player = player;
            this.characters = characters;
        }
#endif
    }
}
