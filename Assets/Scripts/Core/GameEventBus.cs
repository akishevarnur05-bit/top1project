using System;
using UnityEngine;

namespace CyberDefender.Core
{
    /// <summary>
    /// Глобальная шина событий. Все Raise* вызывать только из главного потока Unity:
    /// провайдеры с фоновыми потоками (UDP от OpenCV / Vosk) складывают результаты
    /// в очередь и публикуют их из Update.
    /// </summary>
    public static class GameEventBus
    {
        // Ввод от ИИ-провайдеров
        public static event Action<GestureEvent> OnGestureDetected;
        public static event Action<VoiceCommandEvent> OnVoiceCommandTriggered;
        public static event Action<InputSource, bool> OnAiBridgeStatusChanged; // (источник, online)

        // Состояние игрока
        public static event Action<float> OnKernelEnergyChanged;      // 0..1
        public static event Action<bool> OnVoiceListeningChanged;     // true — шкала полна, включить микрофон
        public static event Action<PlayerState, PlayerState> OnPlayerStateChanged; // (было, стало)
        public static event Action<PlayerAttackEvent> OnPlayerAttack;
        public static event Action<PlayerSkillEvent> OnPlayerSkillUsed;
        public static event Action<int, int> OnPlayerBackupsChanged; // (осталось, всего)
        public static event Action<bool> OnRingOut;                  // true — вылетел игрок, false — вирус
        public static event Action<bool> OnPlayerJumped;             // true — прыжок в воздухе
        public static event Action OnPlayerRespawned;

        // Бой
        public static event Action<CombatHitKind, Vector2> OnCombatHit; // (что за удар, где)
        public static event Action<EnemyAction, Vector2> OnEnemyAction;

        // Игровой цикл
        public static event Action<GameFlowState> OnGameFlowChanged;
        public static event Action<int, int, string> OnFloorStarted; // (индекс этажа, всего этажей, название)
        public static event Action<UiFeedback> OnUiFeedback;

        public static void RaiseGesture(in GestureEvent e) => OnGestureDetected?.Invoke(e);
        public static void RaiseVoiceCommand(in VoiceCommandEvent e) => OnVoiceCommandTriggered?.Invoke(e);
        public static void RaiseAiBridgeStatusChanged(InputSource source, bool online) => OnAiBridgeStatusChanged?.Invoke(source, online);
        public static void RaiseKernelEnergyChanged(float normalized) => OnKernelEnergyChanged?.Invoke(normalized);
        public static void RaiseVoiceListeningChanged(bool listening) => OnVoiceListeningChanged?.Invoke(listening);
        public static void RaisePlayerStateChanged(PlayerState from, PlayerState to) => OnPlayerStateChanged?.Invoke(from, to);
        public static void RaisePlayerAttack(in PlayerAttackEvent e) => OnPlayerAttack?.Invoke(e);
        public static void RaisePlayerSkillUsed(in PlayerSkillEvent e) => OnPlayerSkillUsed?.Invoke(e);
        public static void RaisePlayerBackupsChanged(int left, int total) => OnPlayerBackupsChanged?.Invoke(left, total);
        public static void RaiseRingOut(bool player) => OnRingOut?.Invoke(player);
        public static void RaisePlayerJumped(bool airborne) => OnPlayerJumped?.Invoke(airborne);
        public static void RaisePlayerRespawned() => OnPlayerRespawned?.Invoke();
        public static void RaiseCombatHit(CombatHitKind kind, Vector2 position) => OnCombatHit?.Invoke(kind, position);
        public static void RaiseEnemyAction(EnemyAction action, Vector2 position) => OnEnemyAction?.Invoke(action, position);
        public static void RaiseGameFlowChanged(GameFlowState state) => OnGameFlowChanged?.Invoke(state);
        public static void RaiseFloorStarted(int index, int total, string title) => OnFloorStarted?.Invoke(index, total, title);
        public static void RaiseUiFeedback(UiFeedback feedback) => OnUiFeedback?.Invoke(feedback);

        // Сброс подписок при входе в Play Mode с отключённым Domain Reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnGestureDetected = null;
            OnVoiceCommandTriggered = null;
            OnAiBridgeStatusChanged = null;
            OnKernelEnergyChanged = null;
            OnVoiceListeningChanged = null;
            OnPlayerStateChanged = null;
            OnPlayerAttack = null;
            OnPlayerSkillUsed = null;
            OnPlayerBackupsChanged = null;
            OnRingOut = null;
            OnPlayerJumped = null;
            OnPlayerRespawned = null;
            OnCombatHit = null;
            OnEnemyAction = null;
            OnGameFlowChanged = null;
            OnFloorStarted = null;
            OnUiFeedback = null;
        }
    }
}
