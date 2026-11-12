using CyberDefender.Core;
using CyberDefender.Enemies;
using CyberDefender.Player;
using CyberDefender.Tower;
using UnityEngine;
using UnityEngine.UI;

namespace CyberDefender.UI
{
    /// <summary>HUD строится из кода: полосы HP, Kernel Energy, экраны боя, статус камеры и микрофона.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color SkillColor = new Color(1f, 0.4f, 1f);

        [SerializeField] private PlayerController player;

        private GameObject gameplayRoot;
        private RectTransform playerHpFill;
        private RectTransform enemyHpFill;
        private RectTransform energyFill;
        private Image energyFillImage;
        private GameObject enemyRoot;
        private Text playerNameText;
        private Text enemyNameText;
        private Text floorText;
        private Text centerText;
        private Text subText;
        private Text energyText;
        private Text statusText;
        private Text bannerText;
        private Text backupsText;

        private bool cameraOnline;
        private bool micOnline;
        private bool micListening;
        private string lastGesture = "—";
        private string lastVoice = "—";
        private int floorIndex;
        private int floorTotal;
        private string floorTitle = "";
        private float energy01;
        private float bannerTimer;

        private void Awake()
        {
            BuildUi();
            RefreshEnergyText();
            RefreshStatus();
        }

        private void OnEnable()
        {
            GameEventBus.OnGameFlowChanged += HandleFlow;
            GameEventBus.OnFloorStarted += HandleFloorStarted;
            GameEventBus.OnKernelEnergyChanged += HandleEnergy;
            GameEventBus.OnVoiceListeningChanged += HandleListening;
            GameEventBus.OnAiBridgeStatusChanged += HandleBridgeStatus;
            GameEventBus.OnGestureDetected += HandleGesture;
            GameEventBus.OnVoiceCommandTriggered += HandleVoice;
            GameEventBus.OnPlayerSkillUsed += HandleSkill;
            GameEventBus.OnPlayerBackupsChanged += HandleBackups;
            GameEventBus.OnRingOut += HandleRingOut;
            GameEventBus.OnEnemyAction += HandleEnemyAction;
        }

        private void OnDisable()
        {
            GameEventBus.OnGameFlowChanged -= HandleFlow;
            GameEventBus.OnFloorStarted -= HandleFloorStarted;
            GameEventBus.OnKernelEnergyChanged -= HandleEnergy;
            GameEventBus.OnVoiceListeningChanged -= HandleListening;
            GameEventBus.OnAiBridgeStatusChanged -= HandleBridgeStatus;
            GameEventBus.OnGestureDetected -= HandleGesture;
            GameEventBus.OnVoiceCommandTriggered -= HandleVoice;
            GameEventBus.OnPlayerSkillUsed -= HandleSkill;
            GameEventBus.OnPlayerBackupsChanged -= HandleBackups;
            GameEventBus.OnRingOut -= HandleRingOut;
            GameEventBus.OnEnemyAction -= HandleEnemyAction;
        }

        private void Update()
        {
            if (!gameplayRoot.activeSelf)
                return;

            if (player != null)
                UiFactory.SetFill(playerHpFill, player.Health.Normalized, false);

            EnemyController enemy = EnemyController.Active.Count > 0 ? EnemyController.Active[0] : null;
            enemyRoot.SetActive(enemy != null);
            if (enemy != null)
            {
                UiFactory.SetFill(enemyHpFill, enemy.Health.Normalized, true);
                string enemyName = enemy.Definition.displayName;
                if (enemyNameText.text != enemyName)
                    enemyNameText.text = enemyName;
            }

            UiFactory.SetFill(energyFill, energy01, false);
            energyFillImage.color = micListening
                ? Color.Lerp(UiFactory.EnergyColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f))
                : UiFactory.EnergyColor;

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.unscaledDeltaTime;
                if (bannerTimer <= 0f)
                    bannerText.text = "";
            }
        }

        // ---------- События ----------

        private void HandleFlow(GameFlowState state)
        {
            // В меню свой UI — HUD показывает только строку статуса камеры и микрофона
            // В катсцене кадр принадлежит кинорамке — полосы HP и энергии прячем
            gameplayRoot.SetActive(!TowerManager.IsMenuState(state) && state != GameFlowState.Cutscene);

            switch (state)
            {
                case GameFlowState.FloorIntro:
                    playerNameText.text = player != null && player.Character != null ? player.Character.displayName : "DEFENDER";
                    SetCenter($"ЭТАЖ {floorIndex + 1}/{floorTotal}", floorTitle, UiFactory.Cyan);
                    break;
                case GameFlowState.Playing:
                    SetCenter("", "", UiFactory.Cyan);
                    break;
                case GameFlowState.FloorCleared:
                    SetCenter("УГРОЗА УСТРАНЕНА", "Восстановление целостности…", UiFactory.EnergyColor);
                    break;
                case GameFlowState.LevelComplete:
                    SetCenter($"ЭТАЖ {floorIndex + 1} ОЧИЩЕН", "Ладонь (J) — следующий этаж      Кулак (S) — выбор уровня", UiFactory.EnergyColor);
                    break;
                case GameFlowState.GameOver:
                    SetCenter("СИСТЕМА СКОМПРОМЕТИРОВАНА", "Ладонь (J) — повторить этаж      Кулак (S) — выбор уровня", UiFactory.Danger);
                    break;
                case GameFlowState.Victory:
                    SetCenter("ЯДРО ЗАЩИЩЕНО", "Rootkit Ultimate уничтожен. Ладонь (J) — главное меню", UiFactory.EnergyColor);
                    break;
            }
        }

        private void HandleFloorStarted(int index, int total, string title)
        {
            floorIndex = index;
            floorTotal = total;
            floorTitle = title;
            floorText.text = $"ЭТАЖ {index + 1}/{total} · {title} · {GameSettings.Profile.Name}";
        }

        private void HandleEnergy(float value)
        {
            energy01 = value;
            RefreshEnergyText();
        }

        private void HandleListening(bool value)
        {
            micListening = value;
            RefreshEnergyText();
            RefreshStatus();
        }

        private void HandleBridgeStatus(InputSource source, bool online)
        {
            if (source == InputSource.Gesture) cameraOnline = online;
            else if (source == InputSource.Voice) micOnline = online;
            RefreshStatus();
        }

        private void HandleGesture(GestureEvent e)
        {
            if (e.Phase != GesturePhase.Started)
                return;
            lastGesture = e.Type.ToString();
            RefreshStatus();
        }

        private void HandleVoice(VoiceCommandEvent e)
        {
            lastVoice = e.Command.ToString();
            RefreshStatus();
        }

        private void HandleSkill(PlayerSkillEvent e)
        {
            switch (e.Skill)
            {
                case VoiceCommand.PurgeSystem: bannerText.text = "PURGE SYSTEM!"; break;
                case VoiceCommand.FirewallActivate: bannerText.text = "FIREWALL ACTIVATE!"; break;
                case VoiceCommand.Isolate: bannerText.text = "ISOLATE!"; break;
            }
            ShowBanner(SkillColor);
        }

        private void HandleBackups(int left, int total)
        {
            backupsText.text = $"БЭКАПЫ: {left}/{total}";
            backupsText.color = left <= 1 ? UiFactory.Danger : UiFactory.Cyan;
        }

        private void HandleRingOut(bool isPlayer)
        {
            bannerText.text = isPlayer ? "BACKUP LOST" : "RING OUT!";
            ShowBanner(isPlayer ? UiFactory.Danger : UiFactory.EnergyColor);
        }

        private void HandleEnemyAction(EnemyAction action, Vector2 _)
        {
            if (action != EnemyAction.Enrage)
                return;
            bannerText.text = "БОСС В ЯРОСТИ!";
            ShowBanner(UiFactory.Danger);
        }

        private void ShowBanner(Color color)
        {
            bannerText.color = color;
            bannerTimer = 1.4f;
        }

        private void SetCenter(string title, string subtitle, Color color)
        {
            centerText.text = title;
            centerText.color = color;
            subText.text = subtitle;
        }

        private void RefreshEnergyText()
        {
            energyText.text = micListening
                ? "KERNEL ENERGY 100% — СКАЖИТЕ: PURGE SYSTEM · FIREWALL ACTIVATE · ISOLATE  (клавиши 1/2/3)"
                : $"KERNEL ENERGY {Mathf.RoundToInt(energy01 * 100f)}%";
        }

        private void RefreshStatus()
        {
            string cam = cameraOnline ? "online" : "offline (клавиатура)";
            string mic = micOnline ? (micListening ? "слушает" : "online") : "offline (клавиши 1/2/3)";
            statusText.text = $"Камера: {cam}    Микрофон: {mic}\nЖест: {lastGesture}    Голос: {lastVoice}";
        }

        // ---------- Построение UI ----------

        private void BuildUi()
        {
            RectTransform root = UiFactory.CreateCanvas(transform, "HUD Canvas", 10);
            RectTransform gameplay = UiFactory.CreateStretched("Gameplay", root);
            gameplayRoot = gameplay.gameObject;

            playerNameText = UiFactory.CreateText(gameplay, "DEFENDER", 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -25f), new Vector2(600f, 40f), UiFactory.Cyan);
            playerHpFill = UiFactory.CreateBar(gameplay, new Vector2(0f, 1f), new Vector2(40f, -70f), new Vector2(640f, 32f), UiFactory.Cyan, out _);
            backupsText = UiFactory.CreateText(gameplay, "", 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -110f), new Vector2(400f, 36f), UiFactory.Cyan);

            RectTransform enemyRect = UiFactory.CreateStretched("Enemy", gameplay);
            enemyRoot = enemyRect.gameObject;
            enemyNameText = UiFactory.CreateText(enemyRect, "", 30, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-40f, -25f), new Vector2(600f, 40f), UiFactory.Danger);
            enemyHpFill = UiFactory.CreateBar(enemyRect, new Vector2(1f, 1f), new Vector2(-40f, -70f), new Vector2(640f, 32f), UiFactory.Danger, out _);

            floorText = UiFactory.CreateText(gameplay, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 40f), Color.white);
            bannerText = UiFactory.CreateText(gameplay, "", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1600f, 100f), SkillColor);
            centerText = UiFactory.CreateText(gameplay, "", 84, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1700f, 240f), UiFactory.Cyan);
            subText = UiFactory.CreateText(gameplay, "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(1700f, 60f), Color.white);

            // Снизу вверх: статус (8–60), шкала энергии (68–96), подпись (102–138), выше — пол арены
            energyText = UiFactory.CreateText(gameplay, "", 26, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 102f), new Vector2(1700f, 36f), UiFactory.EnergyColor);
            energyFill = UiFactory.CreateBar(gameplay, new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(900f, 28f), UiFactory.EnergyColor, out energyFillImage);

            statusText = UiFactory.CreateText(root, "", 20, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(30f, 8f), new Vector2(1100f, 52f), UiFactory.Muted);
        }
    }
}
