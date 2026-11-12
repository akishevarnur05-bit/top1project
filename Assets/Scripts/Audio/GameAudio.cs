using System.Collections;
using System.Collections.Generic;
using CyberDefender.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberDefender.Audio
{
    /// <summary>
    /// Весь звук игры: слушает GameEventBus и играет синтезированные эффекты и музыку.
    /// Ни одного аудиофайла — клипы генерируют SfxLibrary и MusicComposer при запуске.
    /// M — выключить / включить музыку.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const float MinRepeatInterval = 0.04f;

        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
        [SerializeField, Tooltip("Сколько эффектов может звучать одновременно")] private int voices = 12;
        [SerializeField] private float musicFadeTime = 1.2f;
        [SerializeField, Range(0f, 1f), Tooltip("Громкость музыки во время катсцены")] private float cutsceneDuck = 0.35f;
        [SerializeField, Range(0f, 1f)] private float floorClearedDuck = 0.45f;

        private AudioClip[] clips;
        private float[] lastPlayed;
        private AudioSource[] sfxSources;
        private int nextVoice;

        private readonly AudioSource[] music = new AudioSource[2];
        private readonly float[] musicLevel = new float[2];
        private readonly float[] musicTarget = new float[2];
        private readonly Dictionary<string, AudioClip> tracks = new Dictionary<string, AudioClip>();
        private int activeMusic;
        private float duck = 1f;
        private float duckTarget = 1f;
        private bool musicMuted;

        private int floorIndex;
        private int floorTotal = 1;
        private int lastBackups = int.MaxValue;

        private void Awake()
        {
            sfxSources = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < sfxSources.Length; i++)
                sfxSources[i] = CreateSource("Sfx", false);
            for (int i = 0; i < music.Length; i++)
                music[i] = CreateSource("Music", true);

            clips = new AudioClip[SfxLibrary.Count];
            lastPlayed = new float[SfxLibrary.Count];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = SfxLibrary.Build((Sfx)i);
                lastPlayed[i] = -1f;
            }

            Track(MusicComposer.Menu);
        }

        private void Start()
        {
            // Треки этажей сочиняем заранее, по одному за кадр, — к началу забега они уже готовы
            StartCoroutine(PrecomposeTracks());
        }

        private void OnEnable()
        {
            GameEventBus.OnGameFlowChanged += HandleFlow;
            GameEventBus.OnFloorStarted += HandleFloorStarted;
            GameEventBus.OnPlayerAttack += HandleAttack;
            GameEventBus.OnPlayerSkillUsed += HandleSkill;
            GameEventBus.OnPlayerJumped += HandleJump;
            GameEventBus.OnPlayerRespawned += HandleRespawn;
            GameEventBus.OnPlayerBackupsChanged += HandleBackups;
            GameEventBus.OnRingOut += HandleRingOut;
            GameEventBus.OnCombatHit += HandleHit;
            GameEventBus.OnEnemyAction += HandleEnemyAction;
            GameEventBus.OnVoiceListeningChanged += HandleListening;
            GameEventBus.OnUiFeedback += HandleUi;
        }

        private void OnDisable()
        {
            GameEventBus.OnGameFlowChanged -= HandleFlow;
            GameEventBus.OnFloorStarted -= HandleFloorStarted;
            GameEventBus.OnPlayerAttack -= HandleAttack;
            GameEventBus.OnPlayerSkillUsed -= HandleSkill;
            GameEventBus.OnPlayerJumped -= HandleJump;
            GameEventBus.OnPlayerRespawned -= HandleRespawn;
            GameEventBus.OnPlayerBackupsChanged -= HandleBackups;
            GameEventBus.OnRingOut -= HandleRingOut;
            GameEventBus.OnCombatHit -= HandleHit;
            GameEventBus.OnEnemyAction -= HandleEnemyAction;
            GameEventBus.OnVoiceListeningChanged -= HandleListening;
            GameEventBus.OnUiFeedback -= HandleUi;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
                musicMuted = !musicMuted;

            float dt = Time.unscaledDeltaTime;
            float speed = dt / Mathf.Max(0.01f, musicFadeTime);
            duck = Mathf.MoveTowards(duck, duckTarget, speed);

            for (int i = 0; i < music.Length; i++)
            {
                musicLevel[i] = Mathf.MoveTowards(musicLevel[i], musicTarget[i], speed);
                music[i].volume = musicLevel[i] * duck * musicVolume * GameSettings.MusicVolume * (musicMuted ? 0f : 1f);
                if (musicLevel[i] <= 0f && musicTarget[i] <= 0f && music[i].isPlaying)
                    music[i].Stop();
            }
        }

        // ---------- Эффекты ----------

        public void Play(Sfx sfx, float pitch = 1f)
        {
            int id = (int)sfx;
            // Несколько одинаковых событий в одном кадре (PURGE по толпе) — один звук
            if (Time.unscaledTime - lastPlayed[id] < MinRepeatInterval)
                return;
            lastPlayed[id] = Time.unscaledTime;

            AudioSource source = sfxSources[nextVoice];
            nextVoice = (nextVoice + 1) % sfxSources.Length;

            float jitter = SfxLibrary.PitchJitter(sfx);
            source.Stop();
            source.clip = clips[id];
            source.pitch = pitch * (1f + Random.Range(-jitter, jitter));
            source.volume = SfxLibrary.Volume(sfx) * sfxVolume * GameSettings.SfxVolume;
            source.Play();
        }

        private void HandleAttack(PlayerAttackEvent e) =>
            Play(e.Kind == AttackKind.Heavy ? Sfx.SwingHeavy : Sfx.SwingLight);

        private void HandleSkill(PlayerSkillEvent e)
        {
            switch (e.Skill)
            {
                case VoiceCommand.PurgeSystem: Play(Sfx.Purge); break;
                case VoiceCommand.FirewallActivate: Play(Sfx.Firewall); break;
                case VoiceCommand.Isolate: Play(Sfx.Isolate); break;
            }
        }

        private void HandleJump(bool airborne) => Play(airborne ? Sfx.AirJump : Sfx.Jump);

        private void HandleRespawn() => Play(Sfx.Respawn);

        private void HandleBackups(int left, int total)
        {
            // Новый забег присылает полный запас — это не потеря
            if (left < lastBackups)
                Play(Sfx.BackupLost);
            lastBackups = left;
        }

        private void HandleRingOut(bool isPlayer)
        {
            // Вылет игрока озвучивает потеря бэкапа
            if (!isPlayer)
                Play(Sfx.RingOut);
        }

        private void HandleHit(CombatHitKind kind, Vector2 _)
        {
            switch (kind)
            {
                case CombatHitKind.Light: Play(Sfx.HitLight); break;
                case CombatHitKind.Heavy: Play(Sfx.HitHeavy); break;
                case CombatHitKind.Skill: Play(Sfx.HitSkill); break;
                case CombatHitKind.PlayerHurt: Play(Sfx.PlayerHurt); break;
                case CombatHitKind.PlayerBlocked: Play(Sfx.Block); break;
                case CombatHitKind.EnemyBlocked: Play(Sfx.Block, 0.75f); break;
            }
        }

        private void HandleEnemyAction(EnemyAction action, Vector2 _)
        {
            switch (action)
            {
                case EnemyAction.Windup: Play(Sfx.EnemyWindup); break;
                case EnemyAction.Dash: Play(Sfx.EnemyDash); break;
                case EnemyAction.Teleport: Play(Sfx.EnemyTeleport); break;
                case EnemyAction.Jump: Play(Sfx.EnemyJump); break;
                case EnemyAction.Defeated: Play(Sfx.EnemyDefeated); break;
                case EnemyAction.Enrage: Play(Sfx.BossEnrage); break;
            }
        }

        private void HandleListening(bool listening)
        {
            if (listening)
                Play(Sfx.EnergyFull);
        }

        private void HandleUi(UiFeedback feedback)
        {
            switch (feedback)
            {
                case UiFeedback.Move: Play(Sfx.UiMove); break;
                case UiFeedback.Confirm: Play(Sfx.UiConfirm); break;
                case UiFeedback.Back: Play(Sfx.UiBack); break;
                case UiFeedback.BootLine: Play(Sfx.BootLine); break;
                case UiFeedback.TitleReveal: Play(Sfx.TitleReveal); break;
            }
        }

        // ---------- Музыка ----------

        private void HandleFloorStarted(int index, int total, string _)
        {
            floorIndex = index;
            floorTotal = Mathf.Max(1, total);
        }

        private void HandleFlow(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.MainMenu:
                case GameFlowState.Controls:
                case GameFlowState.Settings:
                case GameFlowState.LevelSelect:
                case GameFlowState.CharacterSelect:
                    duckTarget = 1f;
                    PlayMusic(Track(MusicComposer.Menu));
                    break;

                case GameFlowState.FloorIntro:
                    duckTarget = 1f;
                    PlayMusic(Track(MusicComposer.ForFloor(floorIndex, floorIndex == floorTotal - 1)));
                    Play(Sfx.FloorStart);
                    break;

                case GameFlowState.Cutscene:
                    duckTarget = cutsceneDuck;
                    break;

                case GameFlowState.Playing:
                    duckTarget = 1f;
                    break;

                case GameFlowState.FloorCleared:
                    duckTarget = floorClearedDuck;
                    Play(Sfx.FloorCleared);
                    break;

                case GameFlowState.GameOver:
                    StopMusic();
                    Play(Sfx.GameOver);
                    break;

                case GameFlowState.Victory:
                    StopMusic();
                    Play(Sfx.Victory);
                    break;
            }
        }

        private void PlayMusic(AudioClip clip)
        {
            AudioSource current = music[activeMusic];
            if (current.clip == clip && current.isPlaying && musicTarget[activeMusic] > 0f)
                return;

            // Кроссфейд: новый трек на свободном источнике, старый затихает
            musicTarget[activeMusic] = 0f;
            activeMusic = 1 - activeMusic;
            AudioSource next = music[activeMusic];
            next.clip = clip;
            next.Play();
            musicLevel[activeMusic] = 0f;
            musicTarget[activeMusic] = 1f;
        }

        private void StopMusic()
        {
            for (int i = 0; i < music.Length; i++)
                musicTarget[i] = 0f;
        }

        private AudioClip Track(in MusicStyle style)
        {
            if (!tracks.TryGetValue(style.Name, out AudioClip clip) || clip == null)
            {
                clip = MusicComposer.Compose(style);
                tracks[style.Name] = clip;
            }
            return clip;
        }

        private IEnumerator PrecomposeTracks()
        {
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                Track(MusicComposer.ForFloor(i, false));
            }
            yield return null;
            Track(MusicComposer.Boss);
        }

        private AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }
    }
}
