using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    /// <summary>Full outcome of one shot: body positions at every step and the events in order.</summary>
    public sealed class ShotResult
    {
        public ShotResult(float dt)
        {
            Dt = dt;
        }

        public float Dt { get; }
        /// <summary>Frames[0] holds the start positions; each later frame is one <see cref="Dt"/> later.</summary>
        public List<Vec2[]> Frames { get; } = new List<Vec2[]>();
        public List<SimEvent> Events { get; } = new List<SimEvent>();
        public float Duration { get; internal set; }

        public Vec2 FinalPosition(int body) => Frames[Frames.Count - 1][body];
    }
}
