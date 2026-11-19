using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    /// <summary>One team during a match. Same class for both sides (design 4.1).</summary>
    public sealed class TeamState
    {
        public int Index;
        public TeamSetup Setup;
        public readonly List<Fighter> Members = new List<Fighter>();
        public readonly List<Fighter> InRing = new List<Fighter>();
        /// <summary>Crowd heat 0..100 (design v0.2 4, battle doc 9).</summary>
        public float Heat;
        /// <summary>Lit tag chain slots, 0..3.</summary>
        public int Chain;
        public int Warnings;
        public bool InterventionUsed;
        public FinalePlan Plan = FinalePlan.Pinfall;
        public Fighter LastAppealer;
        public bool CancelHeatPenaltyUsed;
        /// <summary>Did this team land a hit in its action this turn (heat penalty otherwise).</summary>
        public bool HitThisTurn;
        /// <summary>Flashy play last turn: skip the next heat decay.</summary>
        public bool SkipNextDecay;
        public int ActorCursor;
        /// <summary>Second skills with triggers, with their owner (the fighter the second is attached to).</summary>
        public readonly List<KeyValuePair<Fighter, EffectBlock>> Triggers = new List<KeyValuePair<Fighter, EffectBlock>>();

        public bool IsHuman => Setup.Control == Control.Human;

        // stats for the result screen and the log
        public int Tags;
        public int Appeals;
        public int Specials;
        public int Joints;
        public int Assists;
        public int FinaleTries;
        public int Interventions;
    }

    public enum ObjectKind { Chair, Table }

    public sealed class FieldObject
    {
        public ObjectKind Kind;
        public Vec2 Position;
        public float Radius;
        public bool Broken;
        public string Name => Kind == ObjectKind.Chair ? "의자" : "테이블";
    }

    /// <summary>Things on the ring besides fighters: the referee and match objects.</summary>
    public sealed class Field
    {
        public Vec2 Referee;
        /// <summary>Turns the referee stays down (no pinfall count).</summary>
        public int RefereeDown;
        public readonly List<FieldObject> Objects = new List<FieldObject>();
    }

    public enum NoteKind { Recovered, Woke, TurnStart, TagIn, Appeal, Fell, Returned, SpecialReady, HeatDecay, Chain, RefUp, Warning }

    /// <summary>Something the match did outside a shot, for the screen to show.</summary>
    public struct MatchNote
    {
        public NoteKind Kind;
        public Fighter Fighter;
        public int Team;
        public float Amount;
    }
}
