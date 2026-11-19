using System;
using System.Collections.Generic;
using RingsideHearts.Battle.Core;
using UnityEngine;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Plays a precomputed <see cref="ShotResult"/>: moves each body through a placement callback
    /// and raises its events on time. With <see cref="Feel"/> set, impacts freeze or slow playback;
    /// the impact frame itself is shown during the freeze.
    /// </summary>
    public sealed class ShotPlayer : MonoBehaviour
    {
        public event Action<SimEvent> EventReached;
        public event Action Finished;

        public bool Playing { get; private set; }
        /// <summary>Shot time currently shown (seconds since the shot began).</summary>
        public float Time => _time;
        public HitFeel Feel { get; set; }
        /// <summary>While true, the replay holds still (special cut-ins mid-shot).</summary>
        public bool Paused { get; set; }

        ShotResult _result;
        IReadOnlyList<Action<Vec2>> _place;
        float _time;
        float _speed = 1f;
        int _nextEvent;

        public void Play(ShotResult result, IReadOnlyList<Action<Vec2>> place, float speed)
        {
            _result = result;
            _place = place;
            _speed = speed > 0f ? speed : 1f;
            _time = 0f;
            _nextEvent = 0;
            Playing = true;
            Apply(0, 0f);
        }

        void Update()
        {
            if (!Playing || Paused) return;
            float real = UnityEngine.Time.deltaTime;
            float target = _time + (Feel != null ? Feel.Advance(real) : real) * _speed;

            while (_nextEvent < _result.Events.Count && _result.Events[_nextEvent].Time <= target)
            {
                SimEvent e = _result.Events[_nextEvent++];
                EventReached?.Invoke(e);
                if (Feel == null) continue;
                Feel.Trigger(e);
                if (Feel.TimeScale < 1f) target = Math.Max(_time, e.Time);
            }
            _time = target;

            float frame = _time / _result.Dt;
            int index = Mathf.FloorToInt(frame);
            int last = _result.Frames.Count - 1;
            if (index >= last)
            {
                Apply(last, 0f);
                while (_nextEvent < _result.Events.Count) EventReached?.Invoke(_result.Events[_nextEvent++]);
                _time = _result.Duration;
                Playing = false;
                Finished?.Invoke();
                return;
            }
            Apply(index, frame - index);
        }

        void Apply(int index, float t)
        {
            Vec2[] a = _result.Frames[index];
            Vec2[] b = _result.Frames[Math.Min(index + 1, _result.Frames.Count - 1)];
            for (int i = 0; i < _place.Count && i < a.Length; i++)
                _place[i]?.Invoke(a[i] + (b[i] - a[i]) * t);
        }
    }
}
