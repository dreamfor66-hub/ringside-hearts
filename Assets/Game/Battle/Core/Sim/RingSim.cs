using System;
using System.Collections.Generic;

namespace RingsideHearts.Battle.Core
{
    /// <summary>
    /// Deterministic fixed-step simulation of one shot inside a square ring with four ropes.
    /// Each step: move, decelerate, ropes (springs), flight, resolve body contacts, record a frame.
    /// </summary>
    /// <remarks>
    /// Deceleration is dv/dt = -(friction + drag·v) (× body FrictionMul). Ropes are springs: a
    /// body pushes the rope outward up to rope.maxStretch and leaves with rope.restitution of its
    /// entry speed. Fighter types come from <see cref="Body"/> flags: pass-through, grab-and-throw,
    /// flight after a rope, fixed obstacles.
    /// </remarks>
    public sealed class RingSim
    {
        readonly float _size;
        readonly float _friction;
        readonly float _drag;
        readonly float _ropeRestitution;
        readonly float _ropeStiffness;
        readonly float _ropeMaxStretch;
        readonly float _dt;
        readonly int _maxSteps;
        readonly float _attackerRestitution;
        readonly float _knockback;
        const float FixedRestitution = 0.6f;

        struct RopeContact
        {
            public int Sign;
            public float InSpeed;
        }

        RopeContact[] _contactX;
        RopeContact[] _contactY;
        readonly HashSet<int> _passing = new HashSet<int>();

        public float Size => _size;

        public RingSim(Tuning t)
        {
            _size = t.Get("ring.size");
            _friction = t.Get("ring.friction");
            _drag = t.Get("ring.drag");
            _ropeRestitution = t.Get("rope.restitution");
            _ropeStiffness = t.Get("rope.stiffness");
            _ropeMaxStretch = t.Get("rope.maxStretch");
            _dt = t.Get("sim.dt");
            _maxSteps = Math.Max(1, (int)Math.Floor(t.Get("sim.maxSeconds") / _dt + 1e-4));
            _attackerRestitution = t.Get("hit.attackerRestitution");
            _knockback = t.Get("hit.knockback");
        }

        /// <param name="stopWhen">Optional: ends the shot early right after the step that produced a matching event.</param>
        public ShotResult Simulate(IReadOnlyList<Body> bodies, int shooter, Vec2 launchVelocity,
            Func<SimEvent, bool> stopWhen = null)
        {
            var b = new Body[bodies.Count];
            for (int i = 0; i < b.Length; i++) b[i] = bodies[i].Clone();
            b[shooter].Velocity = launchVelocity;
            _contactX = new RopeContact[b.Length];
            _contactY = new RopeContact[b.Length];
            _passing.Clear();

            var result = new ShotResult(_dt);
            result.Frames.Add(Snapshot(b));

            float time = 0f;
            for (int step = 1; step <= _maxSteps; step++)
            {
                time = step * _dt;
                int eventsBefore = result.Events.Count;

                for (int i = 0; i < b.Length; i++)
                    if (!b[i].Fixed) b[i].Position += b[i].Velocity * _dt;

                for (int i = 0; i < b.Length; i++)
                    if (!b[i].Fixed && !b[i].Airborne && !b[i].Thrown && _contactX[i].Sign == 0 && _contactY[i].Sign == 0)
                        ApplyFriction(b[i]);

                for (int i = 0; i < b.Length; i++)
                    if (!b[i].Fixed && !b[i].Thrown) ResolveRopes(b, i, time, result.Events);

                for (int i = 0; i < b.Length; i++) AdvanceFlight(b[i], i, time, result.Events);

                ResolveContacts(b, time, result.Events);

                result.Frames.Add(Snapshot(b));

                if (AllStopped(b)) break;
                if (stopWhen != null && AnyMatch(result.Events, eventsBefore, stopWhen)) break;
            }

            result.Events.Add(new SimEvent { Type = SimEventType.Stopped, Time = time, Body = shooter, Other = -1 });
            result.Duration = time;
            return result;
        }

        /// <summary>Launch speed that makes a body glide exactly <paramref name="distance"/> before stopping.</summary>
        public float SpeedToTravel(float distance)
        {
            if (distance <= 0f) return 0f;
            float lo = 0f, hi = 80f;
            for (int k = 0; k < 40; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (GlideDistance(mid) < distance) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        float GlideDistance(float v)
        {
            if (_drag <= 1e-6f) return _friction > 0f ? v * v / (2f * _friction) : float.PositiveInfinity;
            if (_friction <= 1e-6f) return v / _drag;
            return v / _drag - _friction / (_drag * _drag) * (float)Math.Log(1.0 + _drag * v / _friction);
        }

        static bool AnyMatch(List<SimEvent> events, int from, Func<SimEvent, bool> match)
        {
            for (int i = from; i < events.Count; i++)
                if (match(events[i])) return true;
            return false;
        }

        void ApplyFriction(Body body)
        {
            float speed = body.Velocity.Length;
            if (speed <= 0f) return;
            float next = Math.Max(0f, speed - (_friction + _drag * speed) * body.FrictionMul * _dt);
            body.Velocity = next > 0f ? body.Velocity * (next / speed) : Vec2.Zero;
        }

        void AdvanceFlight(Body body, int index, float time, List<SimEvent> events)
        {
            if (body.Thrown)
            {
                Vec2 left = body.ThrowTo - body.Position;
                float step = body.Velocity.Length * _dt;
                if (Vec2.Dot(left, body.Velocity) > 0f && left.Length > step) return;
                body.Position = body.ThrowTo;
                body.Velocity = Vec2.Zero;
                body.Thrown = false;
                events.Add(new SimEvent { Type = SimEventType.Slam, Time = time, Body = index, Other = -1, Position = body.Position });
                return;
            }
            if (!body.Airborne) return;
            float speed = body.Velocity.Length;
            body.FlightLeft -= speed * _dt;
            if (body.FlightLeft > 0f && speed > 0.5f) return;
            body.Airborne = false;
            body.Velocity *= body.LandingKeep;
            events.Add(new SimEvent { Type = SimEventType.Landing, Time = time, Body = index, Other = -1, Position = body.Position, Speed = speed });
        }

        void ResolveRopes(Body[] bodies, int index, float time, List<SimEvent> events)
        {
            Body body = bodies[index];
            if (EntryTime(body.Position.Y, body.Velocity.Y, body.Radius) > EntryTime(body.Position.X, body.Velocity.X, body.Radius))
            {
                ResolveAxis(body, index, false, time, events);
                ResolveAxis(body, index, true, time, events);
            }
            else
            {
                ResolveAxis(body, index, true, time, events);
                ResolveAxis(body, index, false, time, events);
            }
        }

        float EntryTime(float position, float velocity, float radius)
        {
            float hi = _size - radius;
            if (position > hi && velocity > 0f) return (position - hi) / velocity;
            if (position < radius && velocity < 0f) return (radius - position) / -velocity;
            return 0f;
        }

        void ResolveAxis(Body body, int index, bool xAxis, float time, List<SimEvent> events)
        {
            RopeContact[] contacts = xAxis ? _contactX : _contactY;
            RopeContact c = contacts[index];
            float lo = body.Radius;
            float hi = _size - body.Radius;
            float p = xAxis ? body.Position.X : body.Position.Y;
            float v = xAxis ? body.Velocity.X : body.Velocity.Y;

            if (c.Sign == 0)
            {
                int sign = p > hi && v > 0f ? 1 : p < lo && v < 0f ? -1 : 0;
                if (sign == 0) return;
                c.Sign = sign;
                c.InSpeed = Math.Abs(v);
                events.Add(new SimEvent
                {
                    Type = SimEventType.RopeBounce,
                    Time = time,
                    Body = index,
                    Other = -1,
                    Side = SideOf(xAxis, sign),
                    Speed = body.Velocity.Length,
                    NormalSpeed = c.InSpeed,
                    Position = xAxis ? new Vec2(sign > 0 ? hi : lo, body.Position.Y) : new Vec2(body.Position.X, sign > 0 ? hi : lo),
                });
            }

            float line = c.Sign > 0 ? hi : lo;
            float stretch = (p - line) * c.Sign;
            bool leaving = false;
            if (stretch > 0f)
            {
                v -= c.Sign * _ropeStiffness * stretch * _dt;
                if (stretch > _ropeMaxStretch)
                {
                    p = line + c.Sign * _ropeMaxStretch;
                    if (v * c.Sign > 0f) v = 0f;
                }
            }
            else
            {
                v = -c.Sign * c.InSpeed * _ropeRestitution;
                leaving = true;
            }

            if (xAxis)
            {
                body.Position.X = p;
                body.Velocity.X = v;
            }
            else
            {
                body.Position.Y = p;
                body.Velocity.Y = v;
            }

            if (leaving && body.RopeBoost != 1f && !body.Boosted)
            {
                body.Boosted = true;
                body.Velocity *= body.RopeBoost;
            }

            if (leaving)
            {
                events.Add(new SimEvent
                {
                    Type = SimEventType.RopeLeave,
                    Time = time,
                    Body = index,
                    Other = -1,
                    Side = SideOf(xAxis, c.Sign),
                    Speed = body.Velocity.Length,
                    Position = body.Position,
                    Velocity = body.Velocity,
                });
                c.Sign = 0;
                if (body.Flier && (!body.Flown || body.FlyEveryRope) && !body.Airborne)
                {
                    body.Airborne = true;
                    body.Flown = true;
                    body.FlightLeft = body.FlightDistance;
                    events.Add(new SimEvent { Type = SimEventType.Takeoff, Time = time, Body = index, Other = -1, Position = body.Position });
                }
            }
            contacts[index] = c;
        }

        static RopeSide SideOf(bool xAxis, int sign) =>
            xAxis ? (sign > 0 ? RopeSide.Right : RopeSide.Left) : (sign > 0 ? RopeSide.Top : RopeSide.Bottom);

        static bool Opposed(Body a, Body b) => a.Group >= 0 && b.Group >= 0 && a.Group != b.Group;

        /// <summary>
        /// Body contacts (design v0.1 6): only while approaching. The body pushing harder along the
        /// contact normal is the striker; the struck body gains striker speed × knockback × striker
        /// KnockMul along the normal, and the striker reflects keeping attackerRestitution.
        /// Fixed bodies, pass-through and grab are handled first.
        /// </summary>
        void ResolveContacts(Body[] bodies, float time, List<SimEvent> events)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                for (int j = i + 1; j < bodies.Length; j++)
                {
                    Body a = bodies[i];
                    Body c = bodies[j];
                    int key = i * 1024 + j;
                    if (!a.Solid || !c.Solid || a.Airborne || c.Airborne || a.Thrown || c.Thrown || a.Ignore == j || c.Ignore == i)
                    {
                        _passing.Remove(key);
                        continue;
                    }

                    Vec2 delta = c.Position - a.Position;
                    float dist = delta.Length;
                    float minDist = a.Radius + c.Radius;
                    if (dist >= minDist)
                    {
                        _passing.Remove(key);
                        continue;
                    }
                    if (_passing.Contains(key)) continue;
                    if (a.Fixed && c.Fixed) continue;

                    Vec2 n = dist > 1e-6f ? delta / dist : new Vec2(0f, 1f);
                    float overlap = minDist - dist;

                    if (a.Fixed || c.Fixed)
                    {
                        Body mover = a.Fixed ? c : a;
                        int moverIndex = a.Fixed ? j : i;
                        int fixedIndex = a.Fixed ? i : j;
                        Vec2 away = a.Fixed ? n : -n; // from fixed toward mover
                        mover.Position += away * overlap;
                        float into = -Vec2.Dot(mover.Velocity, away);
                        if (into <= 0f) continue;
                        events.Add(new SimEvent
                        {
                            Type = SimEventType.BodyHit, Time = time, Body = moverIndex, Other = fixedIndex,
                            Speed = mover.Velocity.Length, NormalSpeed = into, Position = mover.Position,
                        });
                        mover.Velocity = Vec2.Reflect(mover.Velocity, away) * FixedRestitution;
                        continue;
                    }

                    bool approaching = Vec2.Dot(c.Velocity - a.Velocity, n) < 0f;
                    if (!approaching)
                    {
                        a.Position -= n * (overlap * 0.5f);
                        c.Position += n * (overlap * 0.5f);
                        continue;
                    }

                    float aPush = Vec2.Dot(a.Velocity, n);
                    float cPush = -Vec2.Dot(c.Velocity, n);
                    bool aStrikes = aPush >= cPush;
                    int striker = aStrikes ? i : j;
                    int struck = aStrikes ? j : i;
                    Body s = bodies[striker];
                    Body t = bodies[struck];
                    Vec2 toStruck = aStrikes ? n : -n;
                    float speed = s.Velocity.Length;

                    events.Add(new SimEvent
                    {
                        Type = SimEventType.BodyHit,
                        Time = time,
                        Body = striker,
                        Other = struck,
                        Speed = speed,
                        NormalSpeed = Math.Abs(Vec2.Dot(s.Velocity, toStruck)),
                        Position = t.Position,
                    });

                    if (s.PassThrough && Opposed(s, t))
                    {
                        t.Velocity += toStruck * (speed * _knockback * s.PassKnock * s.KnockMul);
                        s.Velocity *= s.PassKeep;
                        _passing.Add(key);
                        continue;
                    }

                    if (s.Grab && Opposed(s, t))
                    {
                        s.Grab = false;
                        s.Velocity = Vec2.Zero;
                        events.Add(new SimEvent { Type = SimEventType.Grab, Time = time, Body = striker, Other = struck, Position = t.Position });
                        Vec2 to = s.GrabTarget - t.Position;
                        float d = to.Length;
                        t.Ignore = striker;
                        if (d > 1e-3f)
                        {
                            t.Thrown = true;
                            t.ThrowTo = s.GrabTarget;
                            t.Velocity = to / d * s.ThrowSpeed;
                        }
                        else
                        {
                            t.Velocity = Vec2.Zero;
                            events.Add(new SimEvent { Type = SimEventType.Slam, Time = time, Body = struck, Other = -1, Position = t.Position });
                        }
                        s.Position -= toStruck * overlap;
                        continue;
                    }

                    t.Velocity += toStruck * (speed * _knockback * s.KnockMul);
                    s.Velocity = Vec2.Reflect(s.Velocity, toStruck) * _attackerRestitution;
                    s.Position -= toStruck * overlap;
                }
            }
        }

        bool AllStopped(Body[] bodies)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].Fixed) continue;
                if (bodies[i].Velocity.LengthSquared > 0f) return false;
                if (bodies[i].Airborne || bodies[i].Thrown) return false;
                if (_contactX[i].Sign != 0 || _contactY[i].Sign != 0) return false;
            }
            return true;
        }

        static Vec2[] Snapshot(Body[] bodies)
        {
            var frame = new Vec2[bodies.Length];
            for (int i = 0; i < bodies.Length; i++) frame[i] = bodies[i].Position;
            return frame;
        }
    }
}
