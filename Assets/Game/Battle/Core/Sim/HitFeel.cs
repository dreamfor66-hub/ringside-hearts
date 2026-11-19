using System;

namespace RingsideHearts.Battle.Core
{
    /// <summary>
    /// Playback timing for impact feel. Turns real seconds into shot seconds:
    /// a body hit freezes playback (hit stop), a rope hit slows it in proportion to how hard the
    /// body went into the rope (hit slow), easing back to normal speed.
    /// </summary>
    /// <remarks>
    /// Hit stop = min(hitStop.maxSeconds, hitStop.seconds + hitStop.perSpeed × normal speed).
    /// Rope strength s = 0 at ropeSlow.minSpeed .. 1 at ropeSlow.fullSpeed; playback drops to
    /// lerp(1, ropeSlow.scale, s) and recovers linearly over ropeSlow.seconds × s (real seconds).
    /// </remarks>
    public sealed class HitFeel
    {
        readonly float _stopBase;
        readonly float _stopPerSpeed;
        readonly float _stopMax;
        readonly float _slowMinSpeed;
        readonly float _slowFullSpeed;
        readonly float _slowScale;
        readonly float _slowSeconds;

        float _stopLeft;
        float _slowStart = 1f;
        float _slowDuration;
        float _slowLeft;

        public HitFeel(Tuning t)
        {
            _stopBase = t.Get("hitStop.seconds");
            _stopPerSpeed = t.Get("hitStop.perSpeed");
            _stopMax = t.Get("hitStop.maxSeconds");
            _slowMinSpeed = t.Get("ropeSlow.minSpeed");
            _slowFullSpeed = t.Get("ropeSlow.fullSpeed");
            _slowScale = t.Get("ropeSlow.scale");
            _slowSeconds = t.Get("ropeSlow.seconds");
        }

        /// <summary>True while a hit stop is freezing playback.</summary>
        public bool Holding => _stopLeft > 0f;

        /// <summary>Current playback multiplier: 0 during hit stop, below 1 during rope slow.</summary>
        public float TimeScale => _stopLeft > 0f ? 0f : SlowScaleAt(_slowDuration - _slowLeft);

        /// <summary>0..1 strength of a rope hit at this entry speed.</summary>
        public float RopeStrength(float normalSpeed)
        {
            float span = _slowFullSpeed - _slowMinSpeed;
            if (span <= 0f) return normalSpeed >= _slowFullSpeed ? 1f : 0f;
            return Math.Max(0f, Math.Min(1f, (normalSpeed - _slowMinSpeed) / span));
        }

        /// <summary>Extra freeze for a big moment (direct hit, grab, slam).</summary>
        public void AddStop(float seconds) => _stopLeft = Math.Max(_stopLeft, seconds);

        /// <summary>Slow playback to <paramref name=scale/>, easing back over <paramref name=seconds/>.</summary>
        public void Slow(float scale, float seconds)
        {
            _slowStart = scale;
            _slowDuration = seconds;
            _slowLeft = seconds;
        }

        public void Trigger(SimEvent e)
        {
            if (e.Type == SimEventType.BodyHit)
            {
                float stop = Math.Min(_stopMax, _stopBase + _stopPerSpeed * e.NormalSpeed);
                _stopLeft = Math.Max(_stopLeft, stop);
            }
            else if (e.Type == SimEventType.RopeBounce)
            {
                float s = RopeStrength(e.NormalSpeed);
                if (s <= 0f) return;
                float start = 1f + (_slowScale - 1f) * s;
                float current = _slowLeft > 0f ? SlowScaleAt(_slowDuration - _slowLeft) : 1f;
                if (start >= current) return; // a softer hit never cuts a deeper slow short
                _slowStart = start;
                _slowDuration = _slowSeconds * s;
                _slowLeft = _slowDuration;
            }
        }

        /// <summary>Consumes real seconds and returns how many shot seconds pass (before playback speed).</summary>
        public float Advance(float realDt)
        {
            float dt = realDt;
            if (_stopLeft > 0f)
            {
                float used = Math.Min(_stopLeft, dt);
                _stopLeft -= used;
                dt -= used;
                if (dt <= 0f) return 0f;
            }

            float passed = 0f;
            if (_slowLeft > 0f)
            {
                float used = Math.Min(_slowLeft, dt);
                float e0 = _slowDuration - _slowLeft;
                passed += (SlowScaleAt(e0) + SlowScaleAt(e0 + used)) * 0.5f * used; // linear ramp: exact
                _slowLeft -= used;
                dt -= used;
            }
            return passed + dt;
        }

        float SlowScaleAt(float elapsed)
        {
            if (_slowLeft <= 0f || _slowDuration <= 0f) return 1f;
            float k = Math.Min(1f, elapsed / _slowDuration);
            return _slowStart + (1f - _slowStart) * k;
        }
    }
}
