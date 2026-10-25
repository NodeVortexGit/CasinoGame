using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Casino.Core;
using Casino.Core.Baccarat;
using Casino.Core.Blackjack;
using Casino.Core.Dice;
using Casino.Core.Economy;
using Casino.Core.Physics;
using Casino.Core.Player;
using Casino.Core.Poker;
using Casino.Core.Region;
using Casino.Core.Roulette;
using Casino.Core.Slots;
using Casino.Core.VideoPoker;

namespace Casino.Tests
{
    static class Program
    {
        static int passed, failed;

        static int Main()
        {
            Run(nameof(HandTotals), HandTotals);
            Run(nameof(ShoeComposition), ShoeComposition);
            Run(nameof(NaturalPaysThreeToTwo), NaturalPaysThreeToTwo);
            Run(nameof(DealerBlackjackWithInsurance), DealerBlackjackWithInsurance);
            Run(nameof(InsuranceLostWhenNoDealerBlackjack), InsuranceLostWhenNoDealerBlackjack);
            Run(nameof(BothBlackjackPushes), BothBlackjackPushes);
            Run(nameof(BustLosesEvenIfDealerBusts), BustLosesEvenIfDealerBusts);
            Run(nameof(DoubleDownWin), DoubleDownWin);
            Run(nameof(SplitPlaysBothHands), SplitPlaysBothHands);
            Run(nameof(SplitAcesGetOneCardEach), SplitAcesGetOneCardEach);
            Run(nameof(PushReturnsStake), PushReturnsStake);
            Run(nameof(CannotStartWithoutChips), CannotStartWithoutChips);
            Run(nameof(FairGameHasNormalHouseEdge), FairGameHasNormalHouseEdge);
            Run(nameof(FullBeginnerLuckAlmostAlwaysWins), FullBeginnerLuckAlmostAlwaysWins);
            Run(nameof(BeginnerLuckCurve), BeginnerLuckCurve);
            Run(nameof(LuckNeverMakesThingsWorse), LuckNeverMakesThingsWorse);
            Run(nameof(LuckPacksQueueStrongestFirstAndRespectTheCap), LuckPacksQueueStrongestFirstAndRespectTheCap);
            Run(nameof(LuckPackWinRatesMatchSimulation), LuckPackWinRatesMatchSimulation);
            Run(nameof(LuckPacksPricedLikeChips), LuckPacksPricedLikeChips);
            Run(nameof(RegionalPrices), RegionalPrices);
            Run(nameof(SkuRoundTrip), SkuRoundTrip);
            Run(nameof(OfflineCountryLookup), OfflineCountryLookup);
            Run(nameof(RegionPlanNeverUsesIpWithGps), RegionPlanNeverUsesIpWithGps);
            Run(nameof(AgeGateBoundaries), AgeGateBoundaries);
            Run(nameof(EnergyDrainAndRegen), EnergyDrainAndRegen);
            Run(nameof(MusicFollowsBets), MusicFollowsBets);
            Run(nameof(PersuasionEscalatesAndForgets), PersuasionEscalatesAndForgets);
            Run(nameof(ChipFormatting), ChipFormatting);
            Run(nameof(NpcStyleDefaults), NpcStyleDefaults);
            Run(nameof(HairChainSwingsAndSettles), HairChainSwingsAndSettles);
            Run(nameof(HairChainSurvivesHugeSteps), HairChainSurvivesHugeSteps);
            Run(nameof(SlotLinePays), SlotLinePays);
            Run(nameof(SlotPaybackIsExactAndFair), SlotPaybackIsExactAndFair);
            Run(nameof(SlotSimulationMatchesExactPayback), SlotSimulationMatchesExactPayback);
            Run(nameof(RouletteEveryBetHasTheSameEdge), RouletteEveryBetHasTheSameEdge);
            Run(nameof(RouletteWheelAndColours), RouletteWheelAndColours);
            Run(nameof(StreakGrowsOnlyOnGoalDays), StreakGrowsOnlyOnGoalDays);
            Run(nameof(StreakFreezesCoverMissedDays), StreakFreezesCoverMissedDays);
            Run(nameof(StreakEarnsFreezesAndMilestones), StreakEarnsFreezesAndMilestones);
            Run(nameof(LevelCurve), LevelCurve);
            Run(nameof(DailyWheelOddsAndStreakBonus), DailyWheelOddsAndStreakBonus);
            Run(nameof(MissionsPerDayAndProgress), MissionsPerDayAndProgress);
            Run(nameof(PokerHandRanking), PokerHandRanking);
            Run(nameof(HoldemPayouts), HoldemPayouts);
            Run(nameof(HoldemHouseEdge), HoldemHouseEdge);
            Run(nameof(BaccaratDrawingRules), BaccaratDrawingRules);
            Run(nameof(BaccaratExactEdges), BaccaratExactEdges);
            Run(nameof(VideoPokerHands), VideoPokerHands);
            Run(nameof(VideoPokerPayback), VideoPokerPayback);
            Run(nameof(SicBoExactEdges), SicBoExactEdges);
            Run(nameof(CrapsRules), CrapsRules);
            Run(nameof(CrapsHouseEdges), CrapsHouseEdges);
            Console.WriteLine();
            Console.WriteLine($"{passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }

        static void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine("FAIL " + name + ": " + e.Message);
            }
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"{what}: expected {expected}, got {actual}");
        }

        // ---------- helpers ----------

        sealed class ScriptedDraw : IDrawStrategy
        {
            readonly Queue<Card> cards;
            public ScriptedDraw(params string[] cs) => cards = new Queue<Card>(cs.Select(ParseCard));
            public Card Draw(Shoe shoe, DrawTarget target, BlackjackRound round, int handIndex) =>
                cards.Count > 0 ? cards.Dequeue() : throw new Exception("script ran out of cards");
        }

        static Card ParseCard(string s)
        {
            string r = s.Substring(0, s.Length - 1);
            Rank rank = r switch { "A" => Rank.Ace, "J" => Rank.Jack, "Q" => Rank.Queen, "K" => Rank.King, _ => (Rank)int.Parse(r) };
            Suit suit = "SHDC".IndexOf(s[s.Length - 1]) switch { 0 => Suit.Spades, 1 => Suit.Hearts, 2 => Suit.Diamonds, _ => Suit.Clubs };
            return new Card(rank, suit);
        }

        static Hand MakeHand(params string[] cs)
        {
            var h = new Hand();
            foreach (var c in cs) h.Add(ParseCard(c));
            return h;
        }

        /// <summary>Deal order: player, dealer up, player, dealer hole, then hits.</summary>
        static (BlackjackRound round, Wallet wallet) Scripted(long chips, params string[] cards)
        {
            var wallet = new Wallet(chips);
            var round = new BlackjackRound(new BlackjackRules(), new Shoe(6, 0.75, new Pcg32(1)), new ScriptedDraw(cards), wallet);
            return (round, wallet);
        }

        // ---------- blackjack rules ----------

        static void HandTotals()
        {
            Equal(21, MakeHand("AS", "KH").Total, "A+K");
            Assert(MakeHand("AS", "KH").IsTwentyOne2Cards, "A+K is two-card 21");
            Equal(17, MakeHand("AS", "6H").Total, "soft 17");
            Assert(MakeHand("AS", "6H").IsSoft, "A6 soft");
            Equal(17, MakeHand("AS", "6H", "KD").Total, "A6K hard 17");
            Assert(!MakeHand("AS", "6H", "KD").IsSoft, "A6K hard");
            Equal(12, MakeHand("AS", "AH").Total, "AA");
            Equal(21, MakeHand("AS", "AH", "9D").Total, "AA9");
            Assert(MakeHand("KS", "QH", "2D").IsBust, "22 bust");
        }

        static void ShoeComposition()
        {
            var shoe = new Shoe(6, 0.75, new Pcg32(7));
            Equal(312, shoe.TotalCards, "cards");
            Equal(96, shoe.CountRemaining(10), "ten-values");
            Equal(24, shoe.CountRemaining(1), "aces");
            var c = shoe.DrawValue(1);
            Assert(c.IsAce, "drew ace");
            Equal(23, shoe.CountRemaining(1), "aces after");
            Equal(311, shoe.Remaining, "remaining");
            int drawn = 1;
            while (!shoe.NeedsShuffle) { shoe.Draw(); drawn++; }
            Equal(234, drawn, "cut card at 75%");
        }

        static void NaturalPaysThreeToTwo()
        {
            var (round, wallet) = Scripted(1000, "AS", "9H", "KD", "7C");
            Assert(round.Start(100), "start");
            Equal(RoundPhase.Complete, round.Phase, "phase");
            Equal(HandOutcome.Blackjack, round.Hands[0].Outcome, "outcome");
            Equal(1150L, wallet.Balance, "balance");
            Equal(150L, round.Net, "net");
        }

        static void DealerBlackjackWithInsurance()
        {
            var (round, wallet) = Scripted(1000, "10S", "AH", "9D", "KC");
            round.Start(100);
            Equal(RoundPhase.Insurance, round.Phase, "insurance offered");
            round.ResolveInsurance(true);
            Equal(RoundPhase.Complete, round.Phase, "complete");
            Equal(HandOutcome.Lose, round.Hands[0].Outcome, "hand lost");
            Equal(1000L, wallet.Balance, "insurance covers the loss");
        }

        static void InsuranceLostWhenNoDealerBlackjack()
        {
            // Player 19 vs dealer A5 (no blackjack). Dealer: soft 16 + K = hard 16, + 9 = bust.
            var (round, wallet) = Scripted(1000, "10S", "AH", "9D", "5C", "KC", "9H");
            round.Start(100);
            round.ResolveInsurance(true);
            Equal(RoundPhase.PlayerTurn, round.Phase, "player turn");
            Equal(850L, wallet.Balance, "bet + insurance taken");
            round.Stand();
            Equal(HandOutcome.Win, round.Hands[0].Outcome, "win");
            Equal(1050L, wallet.Balance, "insurance lost, hand won");
        }

        static void BothBlackjackPushes()
        {
            var (round, wallet) = Scripted(1000, "AS", "KH", "QD", "AC");
            round.Start(100);
            Equal(RoundPhase.Complete, round.Phase, "dealer peeks with ten up");
            Equal(HandOutcome.Push, round.Hands[0].Outcome, "push");
            Equal(1000L, wallet.Balance, "stake back");
        }

        static void BustLosesEvenIfDealerBusts()
        {
            var (round, wallet) = Scripted(1000, "10S", "6H", "6D", "10C", "KD");
            round.Start(100);
            round.Hit();
            Equal(HandOutcome.Bust, round.Hands[0].Outcome, "bust");
            Equal(RoundPhase.Complete, round.Phase, "dealer does not draw when everyone busted");
            Equal(900L, wallet.Balance, "lost");
        }

        static void DoubleDownWin()
        {
            var (round, wallet) = Scripted(1000, "6S", "6H", "5D", "10C", "KD", "9S");
            round.Start(100);
            Assert(round.CanDouble, "can double on 11");
            round.Double();
            Equal(21, round.Hands[0].Hand.Total, "player 21");
            Equal(25, round.Dealer.Total, "dealer bust");
            Equal(HandOutcome.Win, round.Hands[0].Outcome, "win");
            Equal(1200L, wallet.Balance, "doubled win");
        }

        static void SplitPlaysBothHands()
        {
            // P: 8 8 vs dealer 6 + 10. Split: hand1 gets 3 (11) -> double gets 10 (21). hand2 gets 10 (18) stand. Dealer draws 9 -> 25 bust.
            var (round, wallet) = Scripted(1000, "8S", "6H", "8D", "10C", "3S", "10H", "10D", "9C");
            round.Start(100);
            Assert(round.CanSplit, "can split 8s");
            round.Split();
            Equal(2, round.Hands.Count, "two hands");
            Equal(11, round.Hands[0].Hand.Total, "first hand 8+3");
            round.Double();
            Equal(1, round.ActiveIndex, "moved to second hand");
            Equal(18, round.Hands[1].Hand.Total, "second hand 8+10");
            round.Stand();
            Equal(RoundPhase.Complete, round.Phase, "complete");
            Equal(HandOutcome.Win, round.Hands[0].Outcome, "h1 win");
            Equal(HandOutcome.Win, round.Hands[1].Outcome, "h2 win");
            Equal(1000L - 300 + 400 + 200, wallet.Balance, "paid both");
        }

        static void SplitAcesGetOneCardEach()
        {
            var (round, wallet) = Scripted(1000, "AS", "9H", "AD", "8C", "KS", "5H");
            round.Start(100);
            round.Split();
            Equal(RoundPhase.Complete, round.Phase, "split aces finish automatically");
            Equal(2, round.Hands[0].Hand.Count, "one card on first ace");
            Equal(2, round.Hands[1].Hand.Count, "one card on second ace");
            Equal(HandOutcome.Win, round.Hands[0].Outcome, "21 (not blackjack) beats 17");
            Equal(200L, round.Hands[0].Payout, "split 21 pays 1:1");
            Equal(HandOutcome.Lose, round.Hands[1].Outcome, "16 loses to 17");
            Equal(1000L, wallet.Balance, "net zero");
        }

        static void PushReturnsStake()
        {
            var (round, wallet) = Scripted(1000, "10S", "10H", "8D", "8C");
            round.Start(250);
            round.Stand();
            Equal(HandOutcome.Push, round.Hands[0].Outcome, "push");
            Equal(1000L, wallet.Balance, "stake back");
        }

        static void CannotStartWithoutChips()
        {
            var (round, wallet) = Scripted(50, "10S", "10H", "8D", "8C");
            Assert(!round.Start(100), "should refuse");
            Equal(50L, wallet.Balance, "untouched");
            Equal(RoundPhase.Idle, round.Phase, "idle");
        }

        // ---------- simulations ----------

        static (double winRate, double edge, double pushRate) Simulate(int rounds, IDrawStrategy draw, ulong seed, Func<BlackjackRound, PlayerAction> policy = null)
        {
            var wallet = new Wallet(long.MaxValue / 4);
            var round = new BlackjackRound(new BlackjackRules(), new Shoe(6, 0.75, new Pcg32(seed)), draw, wallet);
            long wagered = 0, net = 0;
            int wins = 0, pushes = 0, handsTotal = 0;
            for (int i = 0; i < rounds; i++)
            {
                round.Start(100);
                if (round.Phase == RoundPhase.Insurance) round.ResolveInsurance(false);
                while (round.Phase == RoundPhase.PlayerTurn)
                {
                    var h = round.ActiveHand;
                    var action = policy != null ? policy(round) : BasicStrategy.Decide(h.Hand, round.DealerUpcard, round.CanDouble, round.CanSplit);
                    switch (action)
                    {
                        case PlayerAction.Split when round.CanSplit: round.Split(); break;
                        case PlayerAction.Double when round.CanDouble: round.Double(); break;
                        case PlayerAction.Hit when round.CanHit: round.Hit(); break;
                        case PlayerAction.Split:
                        case PlayerAction.Double: if (round.CanHit) round.Hit(); else round.Stand(); break;
                        default: round.Stand(); break;
                    }
                }
                wagered += 100;
                net += round.Net;
                foreach (var h in round.Hands)
                {
                    handsTotal++;
                    if (h.Outcome == HandOutcome.Win || h.Outcome == HandOutcome.Blackjack) wins++;
                    else if (h.Outcome == HandOutcome.Push) pushes++;
                }
            }
            return (wins / (double)handsTotal, net / (double)wagered, pushes / (double)handsTotal);
        }

        static void FairGameHasNormalHouseEdge()
        {
            var (winRate, edge, pushRate) = Simulate(400_000, FairDraw.Instance, 12345);
            Console.WriteLine($"     fair: win {winRate:P2}, push {pushRate:P2}, player return {edge:P3} per initial bet");
            Assert(winRate > 0.41 && winRate < 0.45, "fair win rate should be ~43%");
            Assert(edge > -0.012 && edge < 0.004, "house edge should be ~0.5%");
        }

        static void FullBeginnerLuckAlmostAlwaysWins()
        {
            var luck = new BeginnerLuckDraw(() => 1.0, new Pcg32(99));
            var (winRate, edge, _) = Simulate(20_000, luck, 777);
            Console.WriteLine($"     luck 100%, basic strategy: win {winRate:P2}, return {edge:P1}");
            Assert(winRate > 0.97, "full luck should win nearly every hand");

            // Even a clueless player who always stands should win.
            var (standWin, _, _) = Simulate(20_000, new BeginnerLuckDraw(() => 1.0, new Pcg32(5)), 778, _ => PlayerAction.Stand);
            Console.WriteLine($"     luck 100%, always stand:    win {standWin:P2}");
            Assert(standWin > 0.97, "always-stand under full luck should still win");

            var (halfWin, _, _) = Simulate(50_000, new BeginnerLuckDraw(() => 0.5, new Pcg32(6)), 779);
            Console.WriteLine($"     luck  50%, basic strategy: win {halfWin:P2}");
            Assert(halfWin > 0.55 && halfWin < 0.97, "half luck should sit between fair and full");
        }

        static void BeginnerLuckCurve()
        {
            var bl = new BeginnerLuck(10, 30, 0);
            Equal(1.0, bl.Luck, "start");
            bl.HandsPlayed = 9;
            Equal(1.0, bl.Luck, "still full at hand 10");
            bl.HandsPlayed = 10;
            Assert(bl.Luck < 1 && bl.Luck > 0.9, "fading");
            bl.HandsPlayed = 39;
            Assert(bl.Luck > 0 && bl.Luck < 0.05, "almost gone");
            bl.HandsPlayed = 40;
            Equal(0.0, bl.Luck, "over");
            Assert(!bl.IsActive, "inactive");
            Equal(0, bl.HandsRemaining, "none left");
        }

        static void LuckPacksQueueStrongestFirstAndRespectTheCap()
        {
            var save = new SaveData();
            var boost = new LuckBoost(save, 1_000);
            Assert(!boost.IsActive && boost.LuckFor(100) == 0, "nothing bought");
            boost.Add(25, 20);
            boost.Add(50, 30);
            Equal(75, boost.HandsLeft, "both packs");
            Equal(30, boost.Percent, "strongest first");
            Equal(0.3, boost.LuckFor(1_000), "at the cap");
            Equal(0.0, boost.LuckFor(1_001), "over the cap is fair");
            for (int i = 0; i < 50; i++) boost.UseHand();
            Equal(20, boost.Percent, "then the weaker pack");
            boost.Add(10, 20);
            Equal(35, boost.HandsLeft, "same level merges");
            for (int i = 0; i < 35; i++) boost.UseHand();
            Assert(!boost.IsActive && boost.LuckFor(100) == 0, "all used up");
            Equal(0, save.luckPackHands.Count, "save cleaned");
        }

        static void LuckPackWinRatesMatchSimulation()
        {
            // The win rates and edges shown to players must be what the draw really does.
            foreach (int pct in new[] { 0, 20, 30 })
            {
                var (win, edge, _) = Simulate(60_000, new BeginnerLuckDraw(() => pct / 100.0, new Pcg32((ulong)(40 + pct))), (ulong)(70 + pct));
                Assert(Math.Abs(win - LuckBoost.WinRate(pct)) < 0.02, $"win rate at {pct}%: simulated {win:P1}, shown {LuckBoost.WinRate(pct):P1}");
                Assert(Math.Abs(edge - LuckBoost.Edge(pct)) < 0.04, $"edge at {pct}%: simulated {edge:P1}, table {LuckBoost.Edge(pct):P1}");
            }
        }

        static void LuckPacksPricedLikeChips()
        {
            // Expected chips from a pack played at the bet cap, per dollar, close to what chips cost: not a scam, and
            // not so much better that luck replaces chip packs.
            var smallest = ProductCatalog.OfKind(ProductKind.Chips)[0];
            double chipsPerDollar = smallest.Chips / (double)smallest.BaseUsd;
            var packs = ProductCatalog.OfKind(ProductKind.Luck);
            Assert(packs.Count >= 1, "luck packs exist");
            foreach (var p in packs)
            {
                double expected = p.LuckHands * LuckBoost.Edge(p.LuckPercent) * LuckBoost.DefaultMaxBet;
                double ratio = expected / (double)p.BaseUsd / chipsPerDollar;
                Assert(ratio > 0.8 && ratio < 1.5, $"{p.Id}: {expected:0} expected chips for ${p.BaseUsd} is {ratio:0.00}x the chip price");
            }
        }

        static void LuckNeverMakesThingsWorse()
        {
            var (fairWin, fairEdge, _) = Simulate(100_000, FairDraw.Instance, 31);
            var (luckWin, luckEdge, _) = Simulate(100_000, new BeginnerLuckDraw(() => 0.1, new Pcg32(32)), 31);
            Assert(luckWin >= fairWin - 0.003 && luckEdge >= fairEdge - 0.01, "a little luck must not reduce the player's odds");
            var (zeroWin, zeroEdge, _) = Simulate(100_000, new BeginnerLuckDraw(() => 0.0, new Pcg32(33)), 31);
            Equal(fairWin, zeroWin, "luck 0 is identical to fair dealing (win rate)");
            Equal(fairEdge, zeroEdge, "luck 0 is identical to fair dealing (return)");
        }

        // ---------- economy / region ----------

        static void RegionalPrices()
        {
            Equal(1f, RegionalPricing.Multiplier("US"), "US");
            Equal(1f, RegionalPricing.Multiplier("ZZ"), "unknown country pays base");
            Equal(1, RegionalPricing.Tier("CH"), "CH tier");
            Equal(3, RegionalPricing.Tier("BG"), "BG tier");
            Equal(5, RegionalPricing.Tier("IN"), "IN tier");
            Equal(4.99m, RegionalPricing.PriceUsd(4.99m, 1f), "base price kept");
            Equal(2.99m, RegionalPricing.PriceUsd(4.99m, 0.6f), "charm price");
            Equal(0.19m, RegionalPricing.PriceUsd(0.49m, 0.2f), "floor");
            var pack = ProductCatalog.Find("chips_30k");
            Assert(RegionalPricing.PriceUsd(pack, "IN", PricingMode.StoreNative) < RegionalPricing.PriceUsd(pack, "BG", PricingMode.StoreNative), "India cheaper than Bulgaria");
            Assert(RegionalPricing.PriceUsd(pack, "BG", PricingMode.StoreNative) < RegionalPricing.PriceUsd(pack, "DE", PricingMode.StoreNative), "Bulgaria cheaper than Germany");
            Assert(RegionalPricing.AllCountries.Count() > 200, "table loaded");
            Equal(0, ProductCatalog.BonusPercent(ProductCatalog.Find("chips_5k")), "no bonus on smallest");
            Assert(ProductCatalog.BonusPercent(ProductCatalog.Find("chips_1500k")) >= 150, "big pack bonus");
            foreach (var food in ProductCatalog.OfKind(ProductKind.Food).Zip(ProductCatalog.OfKind(ProductKind.Food).Skip(1)))
                Assert(food.Second.BaseUsd > food.First.BaseUsd && food.Second.Energy > food.First.Energy, "food: more filling costs more");
        }

        static void SkuRoundTrip()
        {
            var p = ProductCatalog.Find("food_burger");
            Equal("food_burger", RegionalPricing.StoreSku(p, "IN", PricingMode.StoreNative), "native sku");
            Equal("food_burger_t5", RegionalPricing.StoreSku(p, "IN", PricingMode.LocationTiers), "tier sku");
            Equal("food_burger", RegionalPricing.ProductIdFromSku("food_burger_t5"), "strip tier");
            Equal("chips_5k", RegionalPricing.ProductIdFromSku("chips_5k"), "no tier");
        }

        static void OfflineCountryLookup()
        {
            var lookup = CountryLookup.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "countries.bytes")));
            Assert(lookup.CountryCount > 230, "countries loaded");
            var cases = new (double lat, double lon, string code)[]
            {
                (42.6977, 23.3219, "BG"), (43.2141, 27.9147, "BG"), (40.7128, -74.0060, "US"), (52.52, 13.405, "DE"),
                (19.076, 72.8777, "IN"), (6.5244, 3.3792, "NG"), (-23.5505, -46.6333, "BR"), (-33.8688, 151.2093, "AU"),
                (35.6762, 139.6503, "JP"), (46.2044, 6.1432, "CH"), (1.3521, 103.8198, "SG"), (51.1279, 1.3134, "GB"),
                (50.9513, 1.8587, "FR"), (55.7558, 37.6173, "RU"), (-1.2921, 36.8219, "KE"), (64.1466, -21.9426, "IS"),
                (49.6116, 6.1319, "LU"), (25.2048, 55.2708, "AE"), (19.4326, -99.1332, "MX"), (37.5665, 126.978, "KR"),
            };
            foreach (var (lat, lon, code) in cases)
                Equal(code, lookup.Find(lat, lon), $"({lat},{lon})");
            Equal<string>(null, lookup.Find(30, -40), "mid-Atlantic");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) lookup.Find(42.6977, 23.3219);
            Console.WriteLine($"     lookup: {sw.Elapsed.TotalMilliseconds / 200:F3} ms per query");
        }

        static void RegionPlanNeverUsesIpWithGps()
        {
            var phone = RegionPolicy.Plan(new DeviceCapabilities { HasLocationHardware = true, HasTelephony = true });
            Assert(phone.SequenceEqual(new[] { RegionSource.Gps, RegionSource.CellNetwork }), "phone: GPS then cell, never IP");
            var tablet = RegionPolicy.Plan(new DeviceCapabilities { HasLocationHardware = true });
            Assert(!tablet.Contains(RegionSource.IpLookup), "GPS tablet never falls back to IP");
            var desktop = RegionPolicy.Plan(new DeviceCapabilities { SteamRunning = true });
            Assert(desktop.SequenceEqual(new[] { RegionSource.Steam, RegionSource.IpLookup, RegionSource.DeviceLocale }), "desktop: steam, ip, locale");
            Assert(RegionPolicy.IsPhysical(RegionSource.Gps) && !RegionPolicy.IsPhysical(RegionSource.IpLookup), "physical sources");
        }

        // ---------- player ----------

        static void AgeGateBoundaries()
        {
            var today = new DateTime(2026, 10, 10);
            Assert(AgeGate.TryGetBirthDate(2008, 10, 10, today, out var exactly18, out _), "valid");
            Assert(AgeGate.IsOldEnough(exactly18, today), "18 today is allowed");
            Assert(AgeGate.TryGetBirthDate(2008, 10, 11, today, out var dayShort, out _), "valid");
            Assert(!AgeGate.IsOldEnough(dayShort, today), "one day short is refused");
            Assert(!AgeGate.TryGetBirthDate(2001, 2, 29, today, out _, out _), "no Feb 29 in 2001");
            Assert(AgeGate.TryGetBirthDate(2000, 2, 29, today, out _, out _), "leap day 2000");
            Assert(!AgeGate.TryGetBirthDate(2030, 1, 1, today, out _, out _), "future");
            Assert(!AgeGate.TryGetBirthDate(1890, 1, 1, today, out _, out _), "too old to be real");
            Assert(!AgeGate.TryGetBirthDate(1990, 13, 1, today, out _, out _), "bad month");
        }

        static void HairChainSwingsAndSettles()
        {
            var chain = new SpringChain(5);
            for (int i = 0; i < 120; i++) chain.Step(1f / 60f, 0f, 0f);
            Equal(0f, chain.OffsetX(4), "rests without forces");
            // The anchor accelerates to the right for 0.3 s: the strand lags to the left, the tip most.
            for (int i = 0; i < 18; i++) chain.Step(1f / 60f, 3f, 0f);
            Assert(chain.OffsetX(4) < -0.01f, $"tip lags behind ({chain.OffsetX(4):F3})");
            Assert(chain.OffsetX(4) < chain.OffsetX(1), "tip lags more than the root");
            Equal(0f, chain.OffsetX(0), "root stays pinned");
            for (int i = 0; i < 240; i++) chain.Step(1f / 60f, 0f, 0f);
            Assert(Math.Abs(chain.OffsetX(4)) < 0.002f, $"settles back ({chain.OffsetX(4):F4})");
        }

        static void HairChainSurvivesHugeSteps()
        {
            var chain = new SpringChain(6) { MaxOffset = 0.2f };
            for (int i = 0; i < 50; i++) chain.Step(5f, 400f, -300f);
            for (int n = 0; n < chain.Count; n++)
                Assert(!float.IsNaN(chain.OffsetX(n)) && Math.Abs(chain.OffsetX(n)) <= 0.2f && Math.Abs(chain.OffsetY(n)) <= 0.2f, "bounded");
        }

        static void SlotLinePays()
        {
            SlotSymbol C = SlotSymbol.Cherry, W = SlotSymbol.Wild, S = SlotSymbol.Seven, L = SlotSymbol.Lemon, T = SlotSymbol.Scatter;
            Equal(500, SlotMachine.LinePay(new[] { S, S, S, S, S }, out _, out _), "five sevens");
            Equal(500, SlotMachine.LinePay(new[] { S, W, S, W, S }, out _, out int n), "wilds substitute");
            Equal(5, n, "wild run length");
            Equal(2500, SlotMachine.LinePay(new[] { W, W, W, W, W }, out _, out _), "five aces");
            Equal(500, SlotMachine.LinePay(new[] { W, W, W, W, L }, out var sym, out _), "four wilds beat five lemons");
            Equal(SlotSymbol.Wild, sym, "paid as wilds");
            Equal(0, SlotMachine.LinePay(new[] { C, C, L, L, L }, out _, out _), "two in a row don't pay");
            Equal(0, SlotMachine.LinePay(new[] { L, L, S, S, S }, out _, out _), "must start on the left reel");
            Equal(0, SlotMachine.LinePay(new[] { T, T, T, L, L }, out _, out _), "stars don't pay on lines");
            // A full evaluation: payouts are line bets (bet / 10) times the multiplier.
            var r = SlotMachine.Evaluate(new int[5], 100);
            long sum = 0;
            foreach (var w in r.Lines) sum += w.Payout;
            Equal(r.TotalPayout, sum + r.ScatterPayout, "total adds up");
        }

        static void SlotPaybackIsExactAndFair()
        {
            double rtp = SlotMachine.ExactRtp();
            Console.WriteLine($"     slot payback {rtp:P2}");
            Assert(rtp > 0.955 && rtp < 0.962, $"payback {rtp:P2} is in the published 95.5-96.2% band");
        }

        static void SlotSimulationMatchesExactPayback()
        {
            var rng = new Pcg32(99);
            long bet = 100, paid = 0, wagered = 0;
            int wins = 0;
            const int spins = 400_000;
            for (int i = 0; i < spins; i++)
            {
                var r = SlotMachine.Spin(rng, bet);
                wagered += bet;
                paid += r.TotalPayout;
                if (r.TotalPayout > 0) wins++;
            }
            double sim = paid / (double)wagered;
            Console.WriteLine($"     simulated {sim:P2} over {spins:N0} spins, a win on {wins / (double)spins:P1} of spins");
            Assert(Math.Abs(sim - SlotMachine.ExactRtp()) < 0.02, "simulation agrees with the exact payback");
            // A real machine pays on about a quarter to a third of spins; more than that feels like winning while losing.
            double hit = wins / (double)spins;
            Assert(hit > 0.25 && hit < 0.35, $"a win on {hit:P1} of spins, not most of them");
        }

        static void RouletteEveryBetHasTheSameEdge()
        {
            foreach (RouletteBetKind kind in Enum.GetValues(typeof(RouletteBetKind)))
            {
                var bet = new RouletteBet(kind, 37, kind == RouletteBetKind.Straight ? 17 : 0);
                long back = 0;
                for (int n = 0; n < RouletteRules.Pockets; n++) back += RouletteRules.Return(bet, n);
                // Staking 37 on each of the 37 numbers returns 36 x 37: the 2.7% house edge, no more.
                Equal(36L * 37, back, kind + " returns 36/37");
            }
        }

        static void RouletteWheelAndColours()
        {
            Equal(37, RouletteRules.WheelOrder.Length, "pockets");
            Equal(37, new HashSet<int>(RouletteRules.WheelOrder).Count, "each number once");
            Assert(!RouletteRules.IsRed(0) && !RouletteRules.IsBlack(0), "zero is green");
            int reds = 0;
            for (int n = 1; n <= 36; n++) if (RouletteRules.IsRed(n)) reds++;
            Equal(18, reds, "18 reds");
            for (int i = 1; i < 37; i++)
                Assert(RouletteRules.IsRed(RouletteRules.WheelOrder[i]) != RouletteRules.IsRed(RouletteRules.WheelOrder[i % 36 + 1]), "colours alternate");
        }

        static void StreakGrowsOnlyOnGoalDays()
        {
            var save = new SaveData();
            var streak = new Streak(save, 10);
            const int day = 739000;
            for (int i = 0; i < 9; i++) Equal(StreakChange.None, streak.OnRound(day), "below the goal");
            Equal(StreakChange.Extended, streak.OnRound(day), "10th round meets the goal");
            Equal(StreakChange.None, streak.OnRound(day), "once a day");
            Equal(1, streak.Count, "one day");
            Assert(streak.DoneToday(day), "done today");
            for (int i = 0; i < 10; i++) streak.OnRound(day + 1);
            Equal(2, streak.Count, "two days");
            Equal(0, streak.RoundsToday(day + 2), "rounds reset each day");
            // Skipping a day without a freeze ends it.
            Equal(StreakChange.Lost, streak.Settle(day + 3, out int lost), "lost");
            Equal(2, lost, "lost length");
            Equal(0, streak.Count, "back to zero");
            Equal(2, streak.Best, "best kept");
        }

        static void StreakFreezesCoverMissedDays()
        {
            var save = new SaveData { streakCount = 5, streakBest = 5, streakLastDay = 100, streakFreezes = 2 };
            var streak = new Streak(save, 10);
            Equal(StreakChange.None, streak.Settle(101, out _), "next day is fine");
            Equal(StreakChange.Frozen, streak.Settle(103, out int frozen), "two missed days frozen");
            Equal(2, frozen, "frozen days");
            Equal(0, streak.Freezes, "freezes used");
            Equal(5, streak.Count, "streak kept");
            Equal(DayMark.Frozen, streak.Mark(101), "day 101 shows frozen");
            Equal(DayMark.Frozen, streak.Mark(102), "day 102 shows frozen");
            for (int i = 0; i < 10; i++) streak.OnRound(103);
            Equal(6, streak.Count, "continues");
            Equal(DayMark.Met, streak.Mark(103), "met mark");
            // Three missed days with one freeze: lost.
            save.streakFreezes = 1;
            Equal(StreakChange.Lost, streak.Settle(107, out _), "not enough freezes");
        }

        static void StreakEarnsFreezesAndMilestones()
        {
            var save = new SaveData();
            var streak = new Streak(save, 1);
            for (int d = 1; d <= 7; d++) streak.OnRound(d);
            Equal(7, streak.Count, "a week");
            Equal(1, streak.Freezes, "a freeze for 7 days");
            for (int d = 8; d <= 21; d++) streak.OnRound(d);
            Equal(Streak.MaxFreezes, streak.Freezes, "capped");
            Assert(!streak.AddFreeze(), "no more than the cap");
            Assert(Streak.IsMilestone(7) && Streak.IsMilestone(30) && !Streak.IsMilestone(8), "milestones");
            Assert(Streak.MilestoneReward(30) > Streak.MilestoneReward(7), "bigger milestones pay more");
            Equal(21, save.streakMetDays.Count, "calendar remembers three weeks");
        }

        static void LevelCurve()
        {
            Equal(0L, Levels.XpToReach(1), "level 1 is free");
            Equal(100L, Levels.XpToReach(2), "level 2 at 100");
            Equal(240L, Levels.XpToReach(3), "level 3 at 240");
            for (int l = 1; l < 60; l++)
            {
                Assert(Levels.XpToReach(l + 1) > Levels.XpToReach(l), "rising");
                Equal(l, Levels.LevelFor(Levels.XpToReach(l)), "inverse at " + l);
                Equal(l, Levels.LevelFor(Levels.XpToReach(l + 1) - 1), "just below the next at " + l);
            }
            var win = new RoundReport { Game = GameKind.Slots, Wagered = 10, Returned = 50 };
            var loss = new RoundReport { Game = GameKind.Slots, Wagered = 50_000, Returned = 0 };
            Assert(Levels.XpFor(win) > Levels.XpFor(loss), "wins earn a little more");
            Equal(Levels.XpPerRound, Levels.XpFor(loss), "a huge bet earns no extra XP");
            // Tables unlock in order.
            int last = 0;
            foreach (var t in TableCatalog.Blackjack)
            {
                Assert(t.Level >= last, "unlock order");
                last = t.Level;
            }
            Equal(1, TableCatalog.Blackjack[0].Level, "first table open from the start");
        }

        static void DailyWheelOddsAndStreakBonus()
        {
            int sum = 0;
            foreach (int w in DailyWheel.Weights) sum += w;
            Equal(100, sum, "weights are percentages");
            Equal(DailyWheel.Prizes.Length, DailyWheel.Weights.Length, "a weight per prize");
            var rng = new Pcg32(99);
            var counts = new int[DailyWheel.Prizes.Length];
            const int spins = 200_000;
            for (int i = 0; i < spins; i++) counts[DailyWheel.Spin(rng)]++;
            for (int i = 0; i < counts.Length; i++)
                Assert(Math.Abs(counts[i] * 100.0 / spins - DailyWheel.Weights[i]) < 0.4, "shown odds are the real odds, segment " + i);
            Equal(DailyWheel.Prizes[0], DailyWheel.Prize(0, 0), "no streak, no bonus");
            Equal(DailyWheel.Prizes[0] * 2, DailyWheel.Prize(0, 30), "capped at double");
            Assert(DailyWheel.Ready(5, 6) && !DailyWheel.Ready(6, 6), "once a day");
        }

        static void MissionsPerDayAndProgress()
        {
            for (int day = 739000; day < 739060; day++)
            {
                var ids = Missions.ForDay(day);
                Equal(Missions.PerDay, ids.Length, "three a day");
                var games = new HashSet<GameKind?> { Missions.Pool[ids[0]].Game, Missions.Pool[ids[1]].Game, Missions.Pool[ids[2]].Game };
                Equal(3, games.Count, "three different games");
                Assert(Missions.ForDay(day)[1] == ids[1], "same missions all day");
            }
            var seen = new HashSet<int>();
            for (int day = 0; day < 200; day++) foreach (int i in Missions.ForDay(day)) seen.Add(i);
            Equal(Missions.Pool.Length, seen.Count, "every mission comes up");
            var hands = Array.Find(Missions.Pool, m => m.Id == "bj_hands");
            var chips = Array.Find(Missions.Pool, m => m.Id == "bj_chips");
            var big = Array.Find(Missions.Pool, m => m.Id == "sl_big");
            var bjWin = new RoundReport { Game = GameKind.Blackjack, Wagered = 100, Returned = 250, Blackjack = true };
            Equal(1, Missions.Progress(hands, bjWin), "a hand");
            Equal(150, Missions.Progress(chips, bjWin), "net chips won");
            Equal(0, Missions.Progress(big, bjWin), "slots mission ignores blackjack");
            Equal(1, Missions.Progress(big, new RoundReport { Game = GameKind.Slots, Wagered = 10, Returned = 50 }), "5x is a big win");
            Equal(0, Missions.Progress(big, new RoundReport { Game = GameKind.Slots, Wagered = 10, Returned = 40 }), "4x is not");
        }

        /// <summary>Cards from text: "AS KH 10D 2C" (rank then suit letter).</summary>
        static Card[] Cards(string text)
        {
            var list = new List<Card>();
            foreach (var t in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string r = t.Substring(0, t.Length - 1);
                var rank = r == "A" ? Rank.Ace : r == "K" ? Rank.King : r == "Q" ? Rank.Queen : r == "J" ? Rank.Jack : (Rank)int.Parse(r);
                var suit = (Suit)"SHDC".IndexOf(t[t.Length - 1]);
                list.Add(new Card(rank, suit));
            }
            return list.ToArray();
        }

        static int Score(string text) => PokerHand.Score(Cards(text));

        static void PokerHandRanking()
        {
            Equal(HandCategory.RoyalFlush, PokerHand.Category(Score("AS KS QS JS 10S 2D 3C")), "royal flush");
            Equal(HandCategory.StraightFlush, PokerHand.Category(Score("9H 8H 7H 6H 5H AH 2C")), "straight flush beats the flush");
            Equal(HandCategory.Straight, PokerHand.Category(Score("AH 2C 3D 4S 5H 9C KD")), "the wheel");
            Assert(Score("6H 2C 3D 4S 5H 9C KD") > Score("AH 2C 3D 4S 5H 9C KD"), "six-high straight beats the wheel");
            Equal(HandCategory.FourOfAKind, PokerHand.Category(Score("7S 7H 7D 7C KH KD 2S")), "quads over a full house");
            Assert(Score("7S 7H 7D 7C AH 2D 3S") > Score("7S 7H 7D 7C KH KD 2S"), "quads: the kicker counts");
            Equal(HandCategory.FullHouse, PokerHand.Category(Score("9S 9H 9D 4C 4H 4D 2S")), "two trips make a full house");
            Assert(Score("9S 9H 9D 4C 4H 2D 3S") > Score("8S 8H 8D AC AH 2D 3S"), "full house: trips first");
            Equal(HandCategory.Flush, PokerHand.Category(Score("2D 5D 9D JD KD 3S 4C")), "flush");
            Assert(Score("2D 5D 9D JD KD 3S 4C") > Score("10S JH QD KC AS 2D 3C"), "flush beats straight");
            Assert(Score("KS KH 4D 4C AH 2D 3S") > Score("KS KH 4D 4C QH 2D 3S"), "two pair: kicker");
            Assert(Score("KS KH 4D 4C 2H 2D AS") > Score("KS KH 4D 4C QH 3D 2S"), "best two of three pairs, ace kicker");
            Assert(Score("AS AH 5D 7C 9H JD 2S") > Score("KS KH AD QC JH 9D 2S"), "higher pair wins");
            Equal(Score("2S 3H JD JC 9H 9D AS"), Score("2H 4C JD JC 9H 9D AS"), "the board plays: a split");
            Equal(HandCategory.HighCard, PokerHand.Category(Score("2S 4H 6D 8C 10H QD AS")), "nothing");
        }

        static void HoldemPayouts()
        {
            var wallet = new Wallet(10_000);
            var round = new HoldemRound(new Pcg32(5), wallet);
            // Player makes a straight, dealer a pair of eights: Ante, Play and the Blind's straight bonus all pay.
            Assert(round.StartWith(10, Cards("9S 10H"), Cards("8C 2D"), Cards("JD QC KS 8H 3S")), "start");
            Equal(9_980L, wallet.Balance, "ante and blind staked");
            Assert(round.Raise(4), "raise 4x");
            Equal(HoldemOutcome.Win, round.Outcome, "straight wins");
            Assert(round.DealerQualifies, "a pair qualifies");
            Equal(80L, round.PlayReturn, "play pays 1 to 1");
            Equal(20L, round.AnteReturn, "ante pays 1 to 1");
            Equal(20L, round.BlindReturn, "a straight pays the blind 1 to 1");
            Equal(60L, round.Net, "net");

            // Player's flush against the dealer's ace high: the dealer doesn't qualify, so the Ante only comes back.
            round.StartWith(10, Cards("2H 7H"), Cards("AC 4D"), Cards("9H JH KH 5S 3C"));
            round.Check();
            Equal(3, round.BoardShown, "flop");
            round.Raise(2);
            Equal(HoldemOutcome.Win, round.Outcome, "flush wins");
            Assert(!round.DealerQualifies, "ace high doesn't qualify");
            Equal(10L, round.AnteReturn, "ante pushes");
            Equal(25L, round.BlindReturn, "flush pays the blind 3 to 2");
            Equal(40L, round.PlayReturn, "play pays");

            // Player loses to a dealer who doesn't qualify: the Ante still comes back, the rest is lost.
            round.StartWith(10, Cards("2C 3D"), Cards("AC KD"), Cards("9H JH 5S 7C 4H"));
            round.Check();
            round.Check();
            round.Raise(1);
            Equal(HoldemOutcome.Lose, round.Outcome, "ace-king high beats seven high");
            Equal(10L, round.TotalReturned, "only the ante comes back");

            // A tie: everything comes back.
            round.StartWith(10, Cards("2C 3D"), Cards("2H 3S"), Cards("AH AD KC KS QH"));
            round.Raise(3);
            Equal(HoldemOutcome.Push, round.Outcome, "same hand");
            Equal(0L, round.Net, "push");

            // Folding at the river loses the Ante and Blind.
            long before = wallet.Balance;
            round.StartWith(10, Cards("2C 7D"), Cards("AC AD"), Cards("9H JH 5S KC 4H"));
            round.Check();
            round.Check();
            Assert(!round.CanCheck && round.CanFold, "at the river: bet or fold");
            round.Fold();
            Equal(HoldemOutcome.Fold, round.Outcome, "folded");
            Equal(before - 20, wallet.Balance, "ante and blind lost");

            // A royal flush pays the Blind 500 to 1.
            round.StartWith(10, Cards("AS KS"), Cards("2C 2D"), Cards("QS JS 10S 4H 7C"));
            round.Raise(4);
            Equal(10L + 5_000L, round.BlindReturn, "royal flush on the blind");
        }

        static void HoldemHouseEdge()
        {
            var wallet = new Wallet(long.MaxValue / 4);
            var round = new HoldemRound(new Pcg32(77), wallet);
            const int hands = 150_000;
            const long ante = 100;
            long net = 0;
            int raised = 0;
            for (int i = 0; i < hands; i++)
            {
                round.Start(ante);
                while (round.InProgress)
                {
                    int raise = HoldemStrategy.Decide(round);
                    if (raise > 0)
                    {
                        round.Raise(raise);
                        raised++;
                    }
                    else if (round.CanCheck) round.Check();
                    else round.Fold();
                }
                net += round.Net;
            }
            double edge = net / (double)(hands * ante);
            Console.WriteLine($"     hold'em: {edge:P2} of the ante per hand with good play, raised {raised / (double)hands:P0} of hands");
            Assert(edge < -0.01 && edge > -0.045, $"house edge {edge:P2} is the usual 2-3% of the ante");
        }

        static void BaccaratDrawingRules()
        {
            Coup Deal(string cards)
            {
                var queue = new Queue<Card>(Cards(cards));
                return BaccaratRules.Deal(queue.Dequeue);
            }
            // Deal order: player, banker, player, banker, then third cards.
            var natural = Deal("8S 2H KD 3C 5S 5H");
            Equal(2, natural.Player.Count, "a natural stops the deal");
            Equal(BaccaratSide.Player, natural.Winner, "8 beats 5");
            var stand = Deal("6S 3H KD 2C 9S");
            Equal(2, stand.Player.Count, "player stands on 6");
            Equal(3, stand.Banker.Count, "banker draws on 5 when the player stood");
            Equal(BaccaratSide.Player, stand.Winner, "6 beats 4 (5 + 9)");
            var three = Deal("2S 3H KD QC 8S 9D");
            Equal(3, three.Player.Count, "player draws on 2");
            Equal(2, three.Banker.Count, "banker on 3 stands when the player's third card is an 8");
            var six = Deal("AS 3H 4D 3C 6S 2D");
            Equal(3, six.Banker.Count, "banker on 6 draws against a third card of 6");
            Equal(8L + 8L * 19 / 20, BaccaratRules.Return(BaccaratSide.Banker, 8, BaccaratSide.Banker) + 0, "banker pays less 5%");
            Equal(39L, BaccaratRules.Return(BaccaratSide.Banker, 20, BaccaratSide.Banker), "20 on the banker returns 39");
            Equal(20L, BaccaratRules.Return(BaccaratSide.Player, 20, BaccaratSide.Tie), "a tie returns player bets");
            Equal(180L, BaccaratRules.Return(BaccaratSide.Tie, 20, BaccaratSide.Tie), "tie pays 8 to 1");
            Equal(0L, BaccaratRules.Return(BaccaratSide.Player, 20, BaccaratSide.Banker), "losing bet");
        }

        /// <summary>Every possible six-card deal, weighted by its chance from an infinite shoe: the textbook house edges.</summary>
        static void BaccaratExactEdges()
        {
            var cards = new Card[10];
            var weight = new double[10];
            for (int v = 0; v < 10; v++)
            {
                cards[v] = new Card(v == 0 ? Rank.King : (Rank)v, Suit.Spades);
                weight[v] = v == 0 ? 4 / 13.0 : 1 / 13.0;
            }
            double pPlayer = 0, pBanker = 0, pTie = 0;
            var idx = new int[6];
            for (int n = 0; n < 1_000_000; n++)
            {
                int k = n;
                double w = 1;
                for (int i = 0; i < 6; i++)
                {
                    idx[i] = k % 10;
                    k /= 10;
                    w *= weight[idx[i]];
                }
                int next = 0;
                var coup = BaccaratRules.Deal(() => cards[idx[next++]]);
                // Deals that use fewer than six cards are counted once per value of the unused cards: their weights sum to 1.
                switch (coup.Winner)
                {
                    case BaccaratSide.Player: pPlayer += w; break;
                    case BaccaratSide.Banker: pBanker += w; break;
                    default: pTie += w; break;
                }
            }
            double banker = pBanker * 0.95 - pPlayer, player = pPlayer - pBanker, tie = pTie * 8 - (1 - pTie);
            Console.WriteLine($"     baccarat: banker {-banker:P2}, player {-player:P2}, tie {-tie:P2} house edge");
            Assert(Math.Abs(pTie - 0.0952) < 0.001, "ties 9.5% of the time");
            Assert(Math.Abs(-banker - 0.0106) < 0.0005, "banker edge 1.06%");
            Assert(Math.Abs(-player - 0.0124) < 0.0005, "player edge 1.24%");
            Assert(Math.Abs(-tie - 0.144) < 0.003, "tie edge 14.4%");
        }

        static void VideoPokerHands()
        {
            Equal(VideoPokerHand.JacksOrBetter, JacksOrBetter.Evaluate(Cards("JS JH 4D 7C 9H")), "jacks pay");
            Equal(VideoPokerHand.Nothing, JacksOrBetter.Evaluate(Cards("10S 10H 4D 7C 9H")), "tens don't");
            Equal(VideoPokerHand.JacksOrBetter, JacksOrBetter.Evaluate(Cards("AS AH 4D 7C 9H")), "aces pay");
            Equal(VideoPokerHand.TwoPair, JacksOrBetter.Evaluate(Cards("2S 2H 4D 4C 9H")), "two pair");
            Equal(VideoPokerHand.Straight, JacksOrBetter.Evaluate(Cards("AS 2H 3D 4C 5H")), "wheel");
            Equal(VideoPokerHand.RoyalFlush, JacksOrBetter.Evaluate(Cards("AS KS QS JS 10S")), "royal");
            Equal(800, JacksOrBetter.Pays(VideoPokerHand.RoyalFlush), "royal pays 800");
            Equal(9, JacksOrBetter.Pays(VideoPokerHand.FullHouse), "full pay: 9 for a full house");
            Equal(6, JacksOrBetter.Pays(VideoPokerHand.Flush), "full pay: 6 for a flush");
            // The strategy keeps what it should.
            void Holds(string cards, string expect, string what)
            {
                var h = VideoPokerStrategy.Hold(Cards(cards));
                string got = "";
                foreach (bool b in h) got += b ? "1" : "0";
                Equal(expect, got, what);
            }
            Holds("AS KS QS JS 2H", "11110", "four to a royal");
            Holds("AS KS QS JS 10S", "11111", "a royal flush");
            Holds("10H 10S 4D 7C 9H", "11000", "a low pair");
            Holds("KH 2S 4D 7C 9H", "10000", "one high card");
            Holds("2H 7S 4D 9C 3H", "00000", "nothing at all");
            Holds("5H 6S 7D 8C KH", "11110", "four to an outside straight");
            Holds("QH JH 4D 7C 2S", "11000", "two suited high cards");
        }

        static void VideoPokerPayback()
        {
            var wallet = new Wallet(long.MaxValue / 4);
            var round = new VideoPokerRound(new Pcg32(21), wallet);
            const int hands = 1_000_000;
            long bet = 10, back = 0;
            for (int i = 0; i < hands; i++)
            {
                round.Deal(bet);
                round.SetHolds(VideoPokerStrategy.Hold(round.Hand));
                round.Draw();
                back += round.Returned;
            }
            double rtp = back / (double)(hands * bet);
            Console.WriteLine($"     video poker: {rtp:P2} back with the simple strategy");
            Assert(rtp > 0.98 && rtp < 1.005, $"payback {rtp:P2} is the 99% or so of 9/6 Jacks or Better");
        }

        static void SicBoExactEdges()
        {
            void Edge(SicBoBetKind kind, int number, double expected, string what)
            {
                double e = SicBoRules.Edge(kind, number);
                Assert(Math.Abs(e - expected) < 0.0006, $"{what}: edge {e:P2}, expected {expected:P2}");
            }
            Edge(SicBoBetKind.Small, 0, 0.0278, "small");
            Edge(SicBoBetKind.Big, 0, 0.0278, "big");
            Edge(SicBoBetKind.Odd, 0, 0.0278, "odd");
            Edge(SicBoBetKind.Total, 4, 0.1528, "total 4 at 60 to 1");
            Edge(SicBoBetKind.Total, 10, 0.125, "total 10 at 6 to 1");
            Edge(SicBoBetKind.AnyTriple, 0, 0.1389, "any triple at 30 to 1");
            Edge(SicBoBetKind.Triple, 5, 0.162, "a triple at 180 to 1");
            Edge(SicBoBetKind.Double, 3, 0.1852, "a double at 10 to 1");
            Edge(SicBoBetKind.Single, 2, 0.0787, "single number 1/2/3 to 1");
            Equal(0L, SicBoRules.Return(new SicBoBet(SicBoBetKind.Small, 0, 10), new[] { 2, 2, 2 }), "a triple beats small");
            Equal(40L, SicBoRules.Return(new SicBoBet(SicBoBetKind.Single, 6, 10), new[] { 6, 6, 6 }), "three sixes pay a single 3 to 1");
        }

        static void CrapsRules()
        {
            var wallet = new Wallet(10_000);
            var t = new CrapsTable(new Pcg32(1), wallet);
            t.Bet(CrapsBetKind.Pass, 0, 10);
            t.Bet(CrapsBetKind.Field, 0, 10);
            t.Settle(3, 4);
            Equal(0, t.Point, "a natural 7 on the come-out");
            Equal(10_000L, wallet.Balance, "pass won 10, field lost 10: even");

            t.Bet(CrapsBetKind.Pass, 0, 10);
            t.Settle(2, 2);
            Equal(4, t.Point, "point 4");
            Assert(!t.CanTakeDown(CrapsBetKind.Pass), "the pass bet stays once there's a point");
            Assert(t.Bet(CrapsBetKind.PassOdds, 0, 100), "odds");
            Equal(30L, t.BetOn(CrapsBetKind.PassOdds), "odds capped at 3x on 4");
            t.Bet(CrapsBetKind.Place, 6, 30);
            long before = wallet.Balance;
            var hit = t.Settle(3, 3);
            Equal(before + 35, wallet.Balance, "place 6 pays 7 to 6");
            Equal(30L, t.BetOn(CrapsBetKind.Place, 6), "and stays up");
            before = wallet.Balance;
            t.Settle(1, 3);
            Equal(0, t.Point, "point made");
            Equal(before + 20 + 90, wallet.Balance, "pass 1 to 1, odds 2 to 1 on the 4");

            t.Bet(CrapsBetKind.DontPass, 0, 10);
            t.Settle(4, 1);
            Equal(5, t.Point, "point 5");
            before = wallet.Balance;
            t.Settle(3, 4);
            Equal(before + 20, wallet.Balance, "seven-out: don't pass wins, place 6 lost");
            Equal(0L, t.BetOn(CrapsBetKind.Place, 6), "place bets go on a seven-out");

            t.Bet(CrapsBetKind.DontPass, 0, 10);
            before = wallet.Balance;
            t.Settle(6, 6);
            Equal(before + 10, wallet.Balance, "12 on the come-out pushes don't pass");
            Equal(18L, CrapsTable.PlaceWin(10, 4), "place 4 pays 9 to 5");
        }

        static void CrapsHouseEdges()
        {
            var wallet = new Wallet(long.MaxValue / 4);
            var t = new CrapsTable(new Pcg32(8), wallet);
            const long unit = 60;
            var net = new Dictionary<CrapsBetKind, long>();
            var decisions = new Dictionary<CrapsBetKind, long>();
            for (int roll = 0; roll < 3_000_000; roll++)
            {
                if (t.Point == 0 && t.BetOn(CrapsBetKind.Pass) == 0) t.Bet(CrapsBetKind.Pass, 0, unit);
                if (t.Point > 0 && t.BetOn(CrapsBetKind.PassOdds) == 0) t.Bet(CrapsBetKind.PassOdds, 0, unit * 5);
                if (t.BetOn(CrapsBetKind.Field) == 0) t.Bet(CrapsBetKind.Field, 0, unit);
                if (t.BetOn(CrapsBetKind.Place, 6) == 0) t.Bet(CrapsBetKind.Place, 6, unit);
                foreach (var s in t.Roll())
                {
                    long n = s.StaysUp ? s.Returned : s.Returned - s.Staked;
                    net[s.Kind] = (net.TryGetValue(s.Kind, out long x) ? x : 0) + n;
                    decisions[s.Kind] = (decisions.TryGetValue(s.Kind, out long d) ? d : 0) + 1;
                }
            }
            double E(CrapsBetKind k, long stake) => -net[k] / (double)(decisions[k] * stake);
            double pass = E(CrapsBetKind.Pass, unit), field = E(CrapsBetKind.Field, unit), place = E(CrapsBetKind.Place, unit);
            Console.WriteLine($"     craps: pass {pass:P2}, field {field:P2}, place 6 {place:P2}, odds {-net[CrapsBetKind.PassOdds] / (double)decisions[CrapsBetKind.PassOdds] / unit / 5:P2}");
            Assert(pass > 0.008 && pass < 0.02, "pass line about 1.4%");
            Assert(field > 0.045 && field < 0.066, "field about 5.6%");
            Assert(place > 0.003 && place < 0.03, "place 6 about 1.5%");
        }

        static void EnergyDrainAndRegen()
        {
            var e = new EnergyModel(1f, 40, 8);
            e.Drain(20 * 60);
            Assert(Math.Abs(e.Value - 0.5f) < 0.001f, "half after 20 min");
            e.Drain(3600);
            Assert(e.IsEmpty, "empty");
            e.Regenerate(4 * 3600);
            Assert(Math.Abs(e.Value - 0.5f) < 0.001f, "half after 4h away");
            e.Add(0.75f);
            Equal(1f, e.Value, "clamped");
        }

        static void MusicFollowsBets()
        {
            var small = new MusicIntensity();
            small.OnBetPlaced(10, 10, 500, 10_000);
            var big = new MusicIntensity();
            big.OnBetPlaced(250_000, 10_000, 500_000, 400_000);
            Assert(big.Target > small.Target + 0.4f, $"big bets are louder ({small.Target:F2} vs {big.Target:F2})");
            var allIn = new MusicIntensity();
            allIn.OnBetPlaced(500, 10, 500, 500);
            Assert(allIn.Target > small.Target + 0.3f, "going all-in is intense");
            for (int i = 0; i < 600; i++) big.Update(0.5f);
            Assert(big.Target <= big.Baseline + 0.01f, "cools down when idle");
            Assert(Math.Abs(big.Current - big.Target) < 0.01f, "current follows target");
        }

        static void PersuasionEscalatesAndForgets()
        {
            var t = new PersuasionTracker();
            var lines = new HashSet<string>();
            for (int i = 0; i < 4; i++)
            {
                lines.Add(DealerLines.PersuasionLine(t.Attempts));
                t.OnLeaveAttempt();
            }
            Equal(4, lines.Count, "each attempt gets a stronger line");
            for (int i = 0; i < 12; i++) t.OnHandPlayed();
            Equal(0, t.Attempts, "forgotten after a dozen hands");
        }

        static void ChipFormatting()
        {
            Equal("12,500", ChipFormat.Short(12_500), "short small");
            Equal("125K", ChipFormat.Short(125_000), "K");
            Equal("1.25M", ChipFormat.Short(1_250_000), "M");
            Equal("1,000,000", ChipFormat.Full(1_000_000), "full");
        }

        static void NpcStyleDefaults()
        {
            Equal(NpcStyle.Female, NpcStyles.DefaultFor(PlayerGender.Male), "male -> female staff");
            Equal(NpcStyle.Male, NpcStyles.DefaultFor(PlayerGender.Female), "female -> male staff");
            Equal(NpcStyle.Mixed, NpcStyles.DefaultFor(PlayerGender.Unspecified), "unspecified -> mixed");
            Assert(NpcStyles.IsFemale(NpcStyle.Mixed, 0) != NpcStyles.IsFemale(NpcStyle.Mixed, 1), "mixed alternates");
        }
    }
}
