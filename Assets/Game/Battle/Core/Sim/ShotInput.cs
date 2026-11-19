namespace RingsideHearts.Battle.Core
{
    /// <summary>Turns a drag ("pull") into a launch velocity, slingshot style (design P17).</summary>
    public static class ShotInput
    {
        /// <param name="pull">Drag vector from press point to current point, y up.</param>
        /// <param name="maxPull">Drag length that gives full power.</param>
        /// <param name="maxSpeed">Launch speed at full power.</param>
        /// <param name="minPullRatio">Shorter drags (relative to maxPull) cancel the shot.</param>
        /// <returns>False when the drag is too short to fire.</returns>
        public static bool TryFromPull(Vec2 pull, float maxPull, float maxSpeed, float minPullRatio, out Vec2 velocity)
        {
            float length = pull.Length;
            if (maxPull <= 0f || length < minPullRatio * maxPull)
            {
                velocity = Vec2.Zero;
                return false;
            }
            float power = length >= maxPull ? 1f : length / maxPull;
            velocity = -pull.Normalized * (power * maxSpeed);
            return true;
        }

        /// <summary>Power in 0..1 for a drag, for UI display.</summary>
        public static float Power01(Vec2 pull, float maxPull)
        {
            if (maxPull <= 0f) return 0f;
            float p = pull.Length / maxPull;
            return p > 1f ? 1f : p;
        }
    }
}
