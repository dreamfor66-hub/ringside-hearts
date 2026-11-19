namespace RingsideHearts.Battle.Core
{
    public enum RopeSide { Left, Right, Bottom, Top }

    public enum SimEventType { RopeBounce, RopeLeave, BodyHit, Stopped, Landing, Takeoff, Grab, Slam }

    /// <summary>Something that happened during a simulated shot.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        /// <summary>Seconds since the shot started.</summary>
        public float Time;
        /// <summary>The body that bounced, or the one that struck in a BodyHit.</summary>
        public int Body;
        /// <summary>The body that was struck in a BodyHit; -1 otherwise.</summary>
        public int Other;
        public RopeSide Side;
        /// <summary>Speed of <see cref="Body"/> just before the event.</summary>
        public float Speed;
        /// <summary>For RopeBounce: speed component into the rope just before the bounce.</summary>
        public float NormalSpeed;
        /// <summary>
        /// RopeBounce: contact point of the body's center. RopeLeave: where the body comes back off
        /// the rope. BodyHit: position of the struck body.
        /// </summary>
        public Vec2 Position;
        /// <summary>RopeLeave: velocity the body leaves the rope with.</summary>
        public Vec2 Velocity;
    }
}
