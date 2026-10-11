using System.Collections.Generic;

namespace Casino.Core.Player
{
    public enum DealerMoment
    {
        Greeting, PlaceBet, BigBet, AllIn, PlayerBlackjack, PlayerWin, PlayerBigWin, PlayerLose, PlayerBust, Push,
        DealerBust, DealerBlackjack, Insurance, Split, Double, Shuffle, LowChips, OutOfChips, Exhausted, Idle,
        BeginnerLuck, BeginnerLuckOver,
    }

    public enum StaffRole { Host, Dealer, Cashier, Server }

    public static class DealerLines
    {
        static readonly Dictionary<DealerMoment, string[]> Lines = new Dictionary<DealerMoment, string[]>
        {
            [DealerMoment.Greeting] = new[] { "Welcome to my table! Ready to play?", "Good to see you. Take a seat!", "Hi there! Cards are fresh and so am I.", "Welcome! Let's make tonight a good one." },
            [DealerMoment.PlaceBet] = new[] { "Place your bet whenever you're ready.", "Chips down, and let's see some cards.", "What are we playing for?" },
            [DealerMoment.BigBet] = new[] { "Ooh, a big bet! I like your style.", "Now that's a bet. Let's do this!", "Feeling bold tonight, aren't we?" },
            [DealerMoment.AllIn] = new[] { "All in?! My heart is racing!", "Everything on one hand. Fearless!" },
            [DealerMoment.PlayerBlackjack] = new[] { "Blackjack! Incredible!", "Twenty-one, just like that!", "Blackjack! The cards love you tonight." },
            [DealerMoment.PlayerWin] = new[] { "Nicely played!", "Winner! Well done.", "You're on a roll!", "That one's yours." },
            [DealerMoment.PlayerBigWin] = new[] { "What a win! Everyone's watching this table!", "Huge! You're on fire tonight!" },
            [DealerMoment.PlayerLose] = new[] { "So close! The next one's yours.", "The house got lucky that time.", "Shake it off, new hand coming up." },
            [DealerMoment.PlayerBust] = new[] { "Ouch, one card too many.", "Bust! It happens to the best of us." },
            [DealerMoment.Push] = new[] { "A tie. Nobody loses!", "Push. Your chips are safe." },
            [DealerMoment.DealerBust] = new[] { "I busted! Lucky you.", "Too many for me. You win!" },
            [DealerMoment.DealerBlackjack] = new[] { "Blackjack for the house, sorry!", "Dealer blackjack. Rough luck." },
            [DealerMoment.Insurance] = new[] { "I'm showing an ace. Insurance?" },
            [DealerMoment.Split] = new[] { "Splitting! Two chances to win." },
            [DealerMoment.Double] = new[] { "Doubling down. Brave move!", "Double it! One card coming up." },
            [DealerMoment.Shuffle] = new[] { "Fresh shoe, shuffling up!" },
            [DealerMoment.LowChips] = new[] { "Running low? The cashier can top you up.", "Chips getting thin. Want to grab more?" },
            [DealerMoment.OutOfChips] = new[] { "Out of chips! Visit the cashier or grab free chips.", "Your stack is empty. The cashier is right behind you." },
            [DealerMoment.Exhausted] = new[] { "You look exhausted! Grab a bite at the bar.", "Running on empty? The bar has you covered." },
            [DealerMoment.Idle] = new[] { "Take your time.", "Whenever you're ready!", "The night is young." },
            [DealerMoment.BeginnerLuck] = new[] { "Beginner's luck is on your side!", "The deck is in your favor tonight!" },
            [DealerMoment.BeginnerLuckOver] = new[] { "Beginner's luck is over. From here it's a fair game!" },
        };

        static readonly Dictionary<StaffRole, string[]> StaffGreetings = new Dictionary<StaffRole, string[]>
        {
            [StaffRole.Host] = new[] { "Welcome to the casino! Your seat is waiting.", "Good evening! The tables are hot tonight.", "Welcome back! Feeling lucky?", "Tap a table to start playing!" },
            [StaffRole.Cashier] = new[] { "Need more chips? You came to the right window.", "Bigger stacks, bigger bonuses!" },
            [StaffRole.Server] = new[] { "Hungry? Pick anything from the menu.", "A bite to eat keeps your energy up!" },
            [StaffRole.Dealer] = new[] { "Welcome to my table!" },
        };

        // Warm, playful small talk. Deliberately never about buying or betting more.
        static readonly Dictionary<StaffRole, string[]> CharmLines = new Dictionary<StaffRole, string[]>
        {
            [StaffRole.Host] = new[] { "There you are! The place feels brighter already.", "Make yourself at home. Tonight is all about you.", "I love that smile. Lucky night?", "Relax, you're in good hands here." },
            [StaffRole.Cashier] = new[] { "Hey you! I was hoping you'd stop by my window.", "Always nice to see a friendly face.", "Look who it is! Having fun out there?" },
            [StaffRole.Server] = new[] { "Hi there, gorgeous!", "I like your style tonight.", "Hey, nice to see you again!", "Having a good night? You deserve one." },
            [StaffRole.Dealer] = new[] { "Careful, I might start rooting for you.", "My favourite player is back!", "Sit with me, I'll deal you something nice.", "You make this table a lot more fun." },
        };

        // Said when the player leaves a table (Leave always works).
        static readonly string[] Persuasion =
        {
            "Leaving already? The night is young!",
            "Come on, just one more hand. I have a good feeling about this one!",
            "You can't leave me now, we're just getting started!",
            "Last chance! The next hand could be the big one...",
            "Okay, okay. I'll keep your seat warm. Promise you'll be back?",
        };

        public static string Pick(DealerMoment moment, IRandom rng)
        {
            var options = Lines[moment];
            return options[rng.Next(options.Length)];
        }

        public static string Greeting(StaffRole role, IRandom rng)
        {
            var options = StaffGreetings[role];
            return options[rng.Next(options.Length)];
        }

        public static string Charm(StaffRole role, IRandom rng)
        {
            var options = CharmLines[role];
            return options[rng.Next(options.Length)];
        }

        /// <summary>
        /// Playful teasing about the daily streak, in the spirit of Duolingo's owl. It's about showing up: never about
        /// betting more, buying anything, or staying when the player wants to go.
        /// </summary>
        public static string StreakTease(int streak, bool doneToday, int roundsLeft, IRandom rng)
        {
            string[] lines;
            if (streak == 0)
                lines = new[]
                {
                    $"No streak yet? Play {roundsLeft} rounds today and I'll start counting.",
                    $"Fresh start! {roundsLeft} rounds and your flame is lit.",
                    "I keep a little list of who shows up every day. You're not on it... yet.",
                };
            else if (!doneToday)
                lines = new[]
                {
                    $"Day {streak + 1} is waiting for you. Just {roundsLeft} rounds to go!",
                    $"{streak} days in a row... you're not going to stop now, are you?",
                    $"Your flame looks a little cold today. {roundsLeft} rounds should warm it up.",
                    $"I've been counting: {streak} days. Let's make it {streak + 1}.",
                    $"Don't think I didn't notice. {roundsLeft} rounds left to keep your streak!",
                };
            else
                lines = new[]
                {
                    $"{streak} days in a row? I'm starting to think you like it here.",
                    "Your streak is safe for today. Now you're just showing off.",
                    $"Day {streak}, done. Same time tomorrow?",
                    $"{streak} days! At this point I should save you a seat.",
                };
            return lines[rng.Next(lines.Length)];
        }

        /// <summary>What the dealer says the moment today's goal is met.</summary>
        public static string StreakExtended(int streak, IRandom rng)
        {
            switch (streak)
            {
                case 1: return "There it is: day one of your streak! Come back tomorrow and we'll make it two.";
                case 7: return "A whole week in a row! I'm genuinely impressed.";
                case 30: return "Thirty days! You practically work here now.";
                case 100: return "One hundred days. I'm telling everyone about you.";
            }
            string[] lines =
            {
                $"That's {streak} days in a row! Look at you.",
                $"Streak saved: day {streak}! I knew you'd make it.",
                $"Day {streak}! Bet you can't make it {streak + 1}.",
                $"{streak} days... okay, show-off.",
            };
            return lines[rng.Next(lines.Length)];
        }

        public static int PersuasionLevels => Persuasion.Length;

        public static string PersuasionLine(int attempt) =>
            Persuasion[attempt < 0 ? 0 : attempt >= Persuasion.Length ? Persuasion.Length - 1 : attempt];
    }

    /// <summary>Counts how often the player tried to leave recently, so the dealer's pleas escalate.</summary>
    public sealed class PersuasionTracker
    {
        const int HandsToForget = 12;
        int handsSinceAttempt;

        public int Attempts { get; private set; }

        public void OnLeaveAttempt()
        {
            Attempts++;
            handsSinceAttempt = 0;
        }

        public void OnHandPlayed()
        {
            if (Attempts > 0 && ++handsSinceAttempt >= HandsToForget) Reset();
        }

        public void Reset()
        {
            Attempts = 0;
            handsSinceAttempt = 0;
        }
    }
}
