using System;

namespace RingsideHearts.Battle.Core
{
    public enum FinalePlan { Pinfall, Submission, Ringout }

    public enum ShotKind { Normal, Special, Joint }

    /// <summary>
    /// A fighter in a match. Same class for both teams (design 4.1). Stamina knocks a fighter
    /// down and comes back; Body is lasting damage and sets how hard a finale is to resist.
    /// </summary>
    public sealed class Fighter
    {
        public CharacterData Data;
        public CharacterData Second;
        public int Team;
        public int Slot;

        public float MaxStamina;
        public float Stamina;
        public float MaxBody;
        public float Body;
        public float Power;
        public float Guts;

        public bool IsDown;
        public int Downs;
        /// <summary>Downed by a special or joint special: the next finale on this fighter is easier.</summary>
        public bool DownedBySpecial;

        public int SpecialTurns;
        public int SpecialCount;

        public Vec2 Position;
        public bool InRing;
        /// <summary>Stopped in a corner: the next shot's first hit is a dive.</summary>
        public bool OnTurnbuckle;
        /// <summary>Turns before this fighter may tag back in.</summary>
        public int TagLock;
        /// <summary>Turns this fighter stays knocked off the apron (no tag, no assist).</summary>
        public int Fallen;
        public bool FirstShotDone;

        // second-skill modifiers (EffectBlock "always" effects)
        public float AppealHeatMul = 1f;
        public float DownRecoveryMul = 1f;
        public float FrictionMul = 1f;
        public float FinalePowerMul = 1f;
        public float LariatMul = 1f;
        /// <summary>Multiplies how fast an opponent caught in this fighter's submission crawls to the rope.</summary>
        public float CrawlMul = 1f;
        public float FirstShotPowerMul = 1f;
        public float InterventionMul = 1f;
        public float InterventionTimeAdd;

        public float StaminaRatio => MaxStamina > 0f ? Math.Max(0f, Stamina) / MaxStamina : 0f;
        public float BodyRatio => MaxBody > 0f ? Math.Max(0f, Body) / MaxBody : 0f;
        public bool SpecialReady => SpecialCount <= 0;
        public bool OnApron => !InRing;
        public bool CanTagIn => OnApron && !IsDown && Fallen == 0 && TagLock == 0;
        public string Short => Data.Short;

        /// <summary>The second adds 20% of its own stamina, power and guts (design v0.2 5).</summary>
        public Fighter(CharacterData data, CharacterData second, TeamSetup setup, int team, int slot, float secondShare)
        {
            Data = data;
            Second = second;
            Team = team;
            Slot = slot;
            float st = data.Stamina + (second != null ? second.Stamina * secondShare : 0f);
            MaxStamina = st * setup.StaminaMul;
            Stamina = MaxStamina;
            MaxBody = st * setup.BodyMul;
            Body = MaxBody;
            Power = (data.Power + (second != null ? second.Power * secondShare : 0f)) * setup.PowerMul;
            Guts = data.Guts + (second != null ? second.Guts * secondShare : 0f);
            SpecialTurns = Math.Max(1, data.SpecialTurns);
            SpecialCount = SpecialTurns;
        }
    }
}
