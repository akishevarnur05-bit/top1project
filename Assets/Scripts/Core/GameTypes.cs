namespace CyberDefender.Core
{
    public enum GestureType : byte
    {
        None,
        Fist,        // BLOCK / SHIELD
        OpenPalm,    // LIGHT ATTACK
        PointUp,     // JUMP
        SwipeLeft,   // MOVE LEFT
        SwipeRight,  // MOVE RIGHT
        DoublePalm   // HEAVY ATTACK / PUSH
    }

    // Started — жест появился в кадре, Ended — исчез.
    // Для мгновенных жестов (свайп, удар) провайдер может прислать Started и Ended подряд.
    public enum GesturePhase : byte { Started, Ended }

    public enum VoiceCommand : byte
    {
        None,
        PurgeSystem,      // супер-комбо, 35% урона
        FirewallActivate, // неуязвимость 5 с
        Isolate           // стан противника 2.5 с
    }

    public enum InputSource : byte { Gesture, Voice, Simulated }

    public enum PlayerState : byte { Idle, Move, Attack, Block, Stun, SuperSkill }

    public enum AttackKind : byte { Light, Heavy }

    public enum GameFlowState : byte
    {
        MainMenu, Controls, CharacterSelect, FloorIntro, Playing, FloorCleared, GameOver, Victory, Cutscene,
        LevelSelect,  // выбор этажа перед выбором героя
        LevelComplete, // этаж очищен: дальше или к выбору уровня
        Settings
    }

    // События для звука и эффектов: кого и чем ударили, что делает вирус, отклик меню
    public enum CombatHitKind : byte { Light, Heavy, Skill, PlayerHurt, PlayerBlocked, EnemyBlocked }

    public enum EnemyAction : byte { Windup, Dash, Teleport, Jump, Defeated, Enrage }

    public enum UiFeedback : byte { Move, Confirm, Back, BootLine, TitleReveal }

    public readonly struct GestureEvent
    {
        public readonly GestureType Type;
        public readonly GesturePhase Phase;
        public readonly float Confidence;
        public readonly InputSource Source;
        public readonly double Timestamp; // Time.realtimeSinceStartupAsDouble — для замера input latency

        public GestureEvent(GestureType type, GesturePhase phase, float confidence, InputSource source, double timestamp)
        {
            Type = type;
            Phase = phase;
            Confidence = confidence;
            Source = source;
            Timestamp = timestamp;
        }
    }

    public readonly struct VoiceCommandEvent
    {
        public readonly VoiceCommand Command;
        public readonly float Confidence;
        public readonly InputSource Source;
        public readonly double Timestamp;

        public VoiceCommandEvent(VoiceCommand command, float confidence, InputSource source, double timestamp)
        {
            Command = command;
            Confidence = confidence;
            Source = source;
            Timestamp = timestamp;
        }
    }

    public readonly struct PlayerAttackEvent
    {
        public readonly AttackKind Kind;
        public readonly float Damage;
        public readonly float PushForce;
        public readonly int Direction; // -1 влево, 1 вправо

        public PlayerAttackEvent(AttackKind kind, float damage, float pushForce, int direction)
        {
            Kind = kind;
            Damage = damage;
            PushForce = pushForce;
            Direction = direction;
        }
    }

    public readonly struct PlayerSkillEvent
    {
        public readonly VoiceCommand Skill;
        public readonly float Value;    // PurgeSystem: доля от макс. HP противника
        public readonly float Duration; // Firewall: время щита, Isolate: время стана

        public PlayerSkillEvent(VoiceCommand skill, float value, float duration)
        {
            Skill = skill;
            Value = value;
            Duration = duration;
        }
    }
}
