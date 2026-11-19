namespace RingsideHearts.Battle.Core
{
    /// <summary>
    /// A round body inside the ring, in ring coordinates (0..ring.size, y up). Fighter types are
    /// expressed as per-body behavior flags so the simulation stays generic.
    /// </summary>
    public sealed class Body
    {
        public Vec2 Position;
        public Vec2 Velocity;
        public float Radius;
        public bool Solid;

        /// <summary>Team of a fighter body; -1 for neutral bodies (referee, objects).</summary>
        public int Group = -1;
        /// <summary>Never moves; movers bounce off it (tables, chairs).</summary>
        public bool Fixed;
        /// <summary>Multiplies the knockback this body gives when it is the striker.</summary>
        public float KnockMul = 1f;
        /// <summary>Multiplies floor friction and drag for this body.</summary>
        public float FrictionMul = 1f;

        /// <summary>Stampeder: passes through opposing fighters instead of bouncing.</summary>
        public bool PassThrough;
        public float PassKeep = 0.9f;
        public float PassKnock = 0.6f;

        /// <summary>Grappler: on first contact with an opposing fighter, stops and throws it to <see cref="GrabTarget"/>.</summary>
        public bool Grab;
        public Vec2 GrabTarget;
        /// <summary>Speed the grabbed fighter is carried at to <see cref="GrabTarget"/> before the slam.</summary>
        public float ThrowSpeed = 16f;

        /// <summary>Rope-timed specials: velocity × this when first leaving a rope.</summary>
        public float RopeBoost = 1f;

        /// <summary>Highflier: after leaving a rope it flies <see cref="FlightDistance"/> without touching bodies, then lands.</summary>
        public bool Flier;
        public bool FlyEveryRope;
        public float FlightDistance = 3.5f;
        public float LandingKeep = 0.4f;

        /// <summary>Body index this body passes through for the rest of the shot (a thrown fighter and its thrower).</summary>
        internal int Ignore = -1;
        internal bool Thrown;
        internal Vec2 ThrowTo;
        internal bool Boosted;
        internal bool Airborne;
        internal bool Flown;
        internal float FlightLeft;

        public Body(Vec2 position, float radius, bool solid = true)
        {
            Position = position;
            Radius = radius;
            Solid = solid;
            Velocity = Vec2.Zero;
        }

        public Body Clone()
        {
            var b = (Body)MemberwiseClone();
            b.Airborne = false;
            b.Flown = false;
            b.FlightLeft = 0f;
            b.Ignore = -1;
            b.Thrown = false;
            b.Boosted = false;
            return b;
        }
    }
}
