using System;

namespace Casino.Core.Player
{
    /// <summary>
    /// The character's energy bar. It drains while the player is in the casino, slowly recovers while the game is
    /// closed, and can be topped up instantly with food and drinks.
    /// </summary>
    public sealed class EnergyModel
    {
        public EnergyModel(float value, double minutesToEmpty, double hoursToFullWhileAway)
        {
            Value = Clamp01(value);
            DrainPerSecond = minutesToEmpty > 0 ? 1.0 / (minutesToEmpty * 60.0) : 0;
            RegenPerSecond = hoursToFullWhileAway > 0 ? 1.0 / (hoursToFullWhileAway * 3600.0) : 0;
        }

        public float Value { get; private set; }
        public double DrainPerSecond { get; }
        public double RegenPerSecond { get; }
        public bool IsEmpty => Value <= 0f;
        public bool IsLow => Value < 0.2f;

        public event Action<float> Changed;

        public void Drain(double seconds) => Set(Value - (float)(seconds * DrainPerSecond));

        public void Regenerate(double seconds)
        {
            if (seconds > 0) Set(Value + (float)(seconds * RegenPerSecond));
        }

        public void Add(float amount) => Set(Value + amount);

        public double SecondsUntilEmpty => DrainPerSecond > 0 ? Value / DrainPerSecond : double.PositiveInfinity;
        public double SecondsUntilFullWhileAway => RegenPerSecond > 0 ? (1 - Value) / RegenPerSecond : double.PositiveInfinity;

        void Set(float v)
        {
            v = Clamp01(v);
            if (Math.Abs(v - Value) < 1e-7f) return;
            Value = v;
            Changed?.Invoke(v);
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
