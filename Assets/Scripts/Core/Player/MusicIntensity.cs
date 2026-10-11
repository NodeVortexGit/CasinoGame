using System;

namespace Casino.Core.Player
{
    /// <summary>
    /// Turns betting behaviour into a 0..1 music intensity. Bigger bets - in absolute chips, relative to the table,
    /// and relative to the player's bankroll - push it up; it cools down slowly when the player bets small or idles.
    /// </summary>
    public sealed class MusicIntensity
    {
        const float AbsoluteScale = 5.7f;     // log10 of 500,000: the biggest table bet maxes this term out
        const float CoolPerSecond = 0.006f;   // ~2 minutes to cool from frantic back to calm
        const float SpikeDecayPerSecond = 0.04f;
        const float Slew = 0.18f;             // how fast the music itself follows (per second)

        float heat;
        float spike;

        public float Baseline { get; set; } = 0.1f;
        public float Target => Clamp01(Math.Max(Baseline, heat) + spike);
        public float Current { get; private set; }

        public void OnBetPlaced(long bet, long tableMin, long tableMax, long balanceBeforeBet)
        {
            if (bet <= 0) return;
            float absolute = Clamp01((float)Math.Log10(bet) / AbsoluteScale);
            float relative = tableMax > tableMin && tableMin > 0
                ? Clamp01((float)(Math.Log((double)bet / tableMin) / Math.Log((double)tableMax / tableMin)))
                : 0f;
            float risk = balanceBeforeBet > 0 ? Clamp01((float)bet / balanceBeforeBet) : 1f;
            float level = Clamp01(0.12f + 0.38f * absolute + 0.2f * relative + 0.4f * risk);
            // Rises fast, falls back gradually when the player bets smaller again.
            heat = level > heat ? heat + (level - heat) * 0.75f : heat + (level - heat) * 0.3f;
        }

        public void OnRoundResult(long net, bool blackjack)
        {
            if (blackjack) spike = Math.Min(0.3f, spike + 0.15f);
            else if (net > 0) spike = Math.Min(0.3f, spike + 0.08f);
        }

        public void Update(float deltaTime)
        {
            heat = Math.Max(0f, heat - CoolPerSecond * deltaTime);
            spike = Math.Max(0f, spike - SpikeDecayPerSecond * deltaTime);
            float target = Target;
            float step = Slew * deltaTime;
            Current = Math.Abs(target - Current) <= step ? target : Current + Math.Sign(target - Current) * step;
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
