using System;

namespace Casino.Core.Physics
{
    /// <summary>
    /// A tiny physics engine for hanging strands (hair locks, ribbons): a chain of point masses whose root is pinned to
    /// a moving anchor. Each node is pulled back towards its rest shape, smoothed against its neighbours and damped.
    /// When the anchor accelerates (a character starts walking or turns), the chain lags behind and swings back.
    /// Offsets are 2D (sideways, up) relative to the rest shape, in the same units as the anchor acceleration.
    /// </summary>
    public sealed class SpringChain
    {
        const float SubStep = 1f / 120f;
        const int MaxSubSteps = 12;

        readonly float[] x, y, vx, vy;
        float accumulator;

        public SpringChain(int nodes)
        {
            if (nodes < 2) throw new ArgumentOutOfRangeException(nameof(nodes), "A chain needs a root and at least one node.");
            x = new float[nodes];
            y = new float[nodes];
            vx = new float[nodes];
            vy = new float[nodes];
        }

        /// <summary>Pull towards the rest shape (1/s²). Nodes near the root are proportionally stiffer.</summary>
        public float Stiffness { get; set; } = 70f;

        /// <summary>Velocity damping (1/s).</summary>
        public float Damping { get; set; } = 5f;

        /// <summary>How strongly neighbouring nodes pull each other into a smooth curve (1/s²).</summary>
        public float Bending { get; set; } = 120f;

        /// <summary>Largest offset any node may reach, so a hard jolt can never fling the hair away.</summary>
        public float MaxOffset { get; set; } = 0.25f;

        public int Count => x.Length;
        public float OffsetX(int i) => x[i];
        public float OffsetY(int i) => y[i];

        /// <summary>Advances the simulation. <paramref name="anchorAx"/>/<paramref name="anchorAy"/> is the anchor's
        /// acceleration; the strand feels the opposite (inertial) force. <paramref name="forceX"/>/<paramref name="forceY"/>
        /// is any extra force per unit mass (a breeze, an idle sway).</summary>
        public void Step(float dt, float anchorAx, float anchorAy, float forceX = 0f, float forceY = 0f)
        {
            if (!(dt > 0f)) return;
            accumulator = Math.Min(accumulator + dt, SubStep * MaxSubSteps);
            while (accumulator >= SubStep)
            {
                accumulator -= SubStep;
                Integrate(SubStep, forceX - anchorAx, forceY - anchorAy);
            }
        }

        void Integrate(float h, float fx, float fy)
        {
            int n = x.Length;
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float k = Stiffness * (1.6f - t);              // stiffer at the root, looser at the tip
                float load = 0.4f + 0.6f * t;                  // the tip swings more than the root
                int next = i + 1 < n ? i + 1 : i;
                float bx = Bending * (x[i - 1] + x[next] - 2f * x[i]);
                float by = Bending * (y[i - 1] + y[next] - 2f * y[i]);
                float ax = fx * load - k * x[i] + bx - Damping * vx[i];
                float ay = fy * load - k * y[i] + by - Damping * vy[i];
                vx[i] += ax * h;
                vy[i] += ay * h;
            }
            for (int i = 1; i < n; i++)
            {
                x[i] = Clamp(x[i] + vx[i] * h);
                y[i] = Clamp(y[i] + vy[i] * h);
            }
        }

        float Clamp(float v) => v > MaxOffset ? MaxOffset : v < -MaxOffset ? -MaxOffset : v;

        /// <summary>Kicks every node sideways (e.g. a hair flip when a character turns to face you).</summary>
        public void Impulse(float velocityX, float velocityY)
        {
            for (int i = 1; i < x.Length; i++)
            {
                float t = i / (float)(x.Length - 1);
                vx[i] += velocityX * t;
                vy[i] += velocityY * t;
            }
        }

        public void Reset()
        {
            Array.Clear(x, 0, x.Length);
            Array.Clear(y, 0, y.Length);
            Array.Clear(vx, 0, vx.Length);
            Array.Clear(vy, 0, vy.Length);
            accumulator = 0f;
        }
    }
}
