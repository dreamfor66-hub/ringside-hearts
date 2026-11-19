using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    /// <summary>The aimed shot's path up to where it comes off the first rope.</summary>
    public struct AimGuide
    {
        /// <summary>False when the shot stops before touching a rope.</summary>
        public bool ReachesRope;
        public RopeSide Side;
        /// <summary>Body center when it first touches the rope.</summary>
        public Vec2 Contact;
        /// <summary>Body center when it comes back off the rope.</summary>
        public Vec2 Exit;
        /// <summary>Unit direction it leaves the rope with.</summary>
        public Vec2 Reflected;
        /// <summary>Where it stops when it never reaches a rope.</summary>
        public Vec2 Stop;
        /// <summary>Simulated body centers from the origin to <see cref="Exit"/> (or <see cref="Stop"/>).</summary>
        public List<Vec2> Path;
    }

    /// <summary>
    /// Aim guide up to the first rope only (battle doc 5.1). Runs the real simulation for the shooter
    /// alone, so the guide matches the shot exactly and follows every rope setting; other bodies are
    /// left out on purpose so the guide never reveals where a shot will end up.
    /// </summary>
    public static class AimPredictor
    {
        public static AimGuide Predict(RingSim sim, Vec2 origin, float radius, Vec2 launchVelocity)
        {
            ShotResult r = sim.Simulate(new[] { new Body(origin, radius) }, 0, launchVelocity,
                e => e.Type == SimEventType.RopeLeave);

            var guide = new AimGuide { Path = new List<Vec2>(r.Frames.Count) };
            foreach (Vec2[] frame in r.Frames) guide.Path.Add(frame[0]);

            foreach (SimEvent e in r.Events)
            {
                if (e.Type == SimEventType.RopeBounce && !guide.ReachesRope)
                {
                    guide.ReachesRope = true;
                    guide.Side = e.Side;
                    guide.Contact = e.Position;
                }
                else if (e.Type == SimEventType.RopeLeave)
                {
                    guide.Exit = e.Position;
                    guide.Reflected = e.Velocity.Normalized;
                    break;
                }
            }
            if (!guide.ReachesRope) guide.Stop = guide.Path[guide.Path.Count - 1];
            return guide;
        }
    }
}
