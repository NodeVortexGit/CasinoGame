using System;
using System.Collections.Generic;
using Casino.Art;
using Casino.UI;
using UnityEngine;

namespace Casino.World
{
    /// <summary>Something solid on the floor plan: a circle or an axis-aligned box (x/z).</summary>
    public struct Obstacle
    {
        public bool Circle;
        public Vector2 Center, Min, Max;
        public float Radius;

        public static Obstacle Round(float x, float z, float radius) =>
            new Obstacle { Circle = true, Center = new Vector2(x, z), Radius = radius };

        public static Obstacle Rect(float minX, float minZ, float maxX, float maxZ) =>
            new Obstacle { Min = new Vector2(minX, minZ), Max = new Vector2(maxX, maxZ) };
    }

    /// <summary>A place the player can use: a table, the cashier, the bar, a member of staff.</summary>
    public sealed class Spot
    {
        /// <summary>What the action button says, and the line under it, in the player's language.</summary>
        public Func<string> Title, Subtitle;
        public Icon Icon = Icon.Play;
        public ButtonStyle Style = ButtonStyle.Gold;
        public Vector3 Focus;
        public float TapRadius = 1.2f, UseDistance = 3f;
        public Vector2 Approach;
        public float ApproachYaw;
        public Action Use;
        public StaffFigure Staff;
        /// <summary>Index of the staff member reserved for this spot while the world is being built (-1 = none).</summary>
        public int StaffIndex = -1;
        /// <summary>Moving spots (the waitress) follow their staff member.</summary>
        public bool FollowsStaff;
    }

    /// <summary>
    /// First-person walking on the casino floor: stick / keys to move, drag to look, tap to walk somewhere. Collisions
    /// are resolved in 2D against circles and boxes (no physics engine needed).
    /// </summary>
    public sealed class PlayerRig
    {
        public const float Radius = 0.33f, EyeHeight = 1.62f, WalkSpeed = 2.4f;

        readonly List<Obstacle> obstacles;
        readonly Vector2 roomMin, roomMax;
        Vector2? target;
        float? targetYaw;
        Action arrive;
        float stuck, lastDistance, walked;

        public Vector2 Position;
        public float Yaw, Pitch;
        public float Speed { get; private set; }
        public float StepPhase => walked;
        public bool AutoWalking => target.HasValue || targetYaw.HasValue;

        public PlayerRig(List<Obstacle> obstacles, Vector2 roomMin, Vector2 roomMax)
        {
            this.obstacles = obstacles;
            this.roomMin = roomMin;
            this.roomMax = roomMax;
        }

        public Vector3 Eye => new Vector3(Position.x, EyeHeight, Position.y);
        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);

        public void Look(float yawDelta, float pitchDelta)
        {
            if (Mathf.Abs(yawDelta) + Mathf.Abs(pitchDelta) > 0.01f) targetYaw = null;
            Yaw = Mathf.Repeat(Yaw + yawDelta, 360f);
            Pitch = Mathf.Clamp(Pitch + pitchDelta, -35f, 30f);
        }

        /// <summary>Walks to a point by itself, optionally turns to a heading, then calls <paramref name="arrived"/>.</summary>
        public void WalkTo(Vector2 point, float? faceYaw, Action arrived)
        {
            target = point;
            targetYaw = faceYaw;
            arrive = arrived;
            stuck = 0f;
            lastDistance = float.MaxValue;
        }

        public void Cancel()
        {
            target = null;
            targetYaw = null;
            arrive = null;
        }

        /// <summary><paramref name="stick"/>: x = sideways, y = forward, each -1..1. Manual input cancels auto-walking.</summary>
        public void Tick(Vector2 stick, float dt)
        {
            Vector2 move = Vector2.zero;
            if (stick.sqrMagnitude > 0.01f)
            {
                Cancel();
                float yawRad = Yaw * Mathf.Deg2Rad;
                var fwd = new Vector2(Mathf.Sin(yawRad), Mathf.Cos(yawRad));
                var right = new Vector2(fwd.y, -fwd.x);
                move = (fwd * stick.y + right * stick.x) * WalkSpeed;
                if (stick.magnitude > 1f) move /= stick.magnitude;
            }
            else if (target.HasValue)
            {
                Vector2 to = target.Value - Position;
                float dist = to.magnitude;
                if (dist < 0.08f)
                {
                    target = null;
                }
                else
                {
                    float desired = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
                    Yaw = Mathf.MoveTowardsAngle(Yaw, desired, 240f * dt);
                    Pitch = Mathf.MoveTowards(Pitch, 0f, 40f * dt);
                    float speed = WalkSpeed * Mathf.Clamp01(dist * 1.5f + 0.3f);
                    move = to / dist * speed;
                    // Give up when blocked (walking into furniture) instead of pushing forever.
                    if (dist > lastDistance - 0.002f) stuck += dt;
                    else stuck = 0f;
                    lastDistance = dist;
                    if (stuck > 0.6f) Cancel();
                }
            }
            else if (targetYaw.HasValue)
            {
                Yaw = Mathf.MoveTowardsAngle(Yaw, targetYaw.Value, 200f * dt);
                Pitch = Mathf.MoveTowards(Pitch, 0f, 40f * dt);
                if (Mathf.Abs(Mathf.DeltaAngle(Yaw, targetYaw.Value)) < 0.5f) targetYaw = null;
            }

            if (!AutoWalking && arrive != null)
            {
                var done = arrive;
                arrive = null;
                done();
            }

            Vector2 before = Position;
            Position += move * dt;
            Resolve();
            Speed = (Position - before).magnitude / Mathf.Max(dt, 1e-4f);
            walked += Speed * dt;
        }

        public void Teleport(Vector2 position, float yaw)
        {
            Cancel();
            Position = position;
            Yaw = yaw;
            Pitch = 0f;
            Resolve();
        }

        void Resolve()
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var o in obstacles)
                {
                    if (o.Circle)
                    {
                        Vector2 d = Position - o.Center;
                        float min = o.Radius + Radius;
                        float len = d.magnitude;
                        if (len < min) Position = o.Center + (len > 1e-4f ? d / len : Vector2.up) * min;
                    }
                    else
                    {
                        var q = new Vector2(Mathf.Clamp(Position.x, o.Min.x, o.Max.x), Mathf.Clamp(Position.y, o.Min.y, o.Max.y));
                        Vector2 d = Position - q;
                        float len = d.magnitude;
                        if (len >= Radius) continue;
                        if (len > 1e-4f)
                        {
                            Position = q + d / len * Radius;
                        }
                        else
                        {
                            // Inside the box: leave through the nearest side.
                            float left = Position.x - o.Min.x, right = o.Max.x - Position.x;
                            float down = Position.y - o.Min.y, up = o.Max.y - Position.y;
                            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
                            if (m == left) Position.x = o.Min.x - Radius;
                            else if (m == right) Position.x = o.Max.x + Radius;
                            else if (m == down) Position.y = o.Min.y - Radius;
                            else Position.y = o.Max.y + Radius;
                        }
                    }
                }
                Position.x = Mathf.Clamp(Position.x, roomMin.x + Radius, roomMax.x - Radius);
                Position.y = Mathf.Clamp(Position.y, roomMin.y + Radius, roomMax.y - Radius);
            }
        }
    }
}
