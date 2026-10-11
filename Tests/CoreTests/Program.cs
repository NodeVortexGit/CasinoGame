using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Casino.Core;
using Casino.Core.Blackjack;
using Casino.Core.Economy;
using Casino.Core.Physics;
using Casino.Core.Player;
using Casino.Core.Region;
using Casino.Core.Roulette;
using Casino.Core.Slots;

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
            Equal(2, SlotMachine.LinePay(new[] { C, C, L, L, L }, out _, out _), "cherry pair");
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
