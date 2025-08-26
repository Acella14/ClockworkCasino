using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ClockworkCasino.Cards;
using ClockworkCasino.Rules;

namespace ClockworkCasino.Tests
{
    public class RuleRuntimeTests
    {
        static CardData C(int v, Suit s, bool cursed = false) => new CardData { value = v, suit = s, cursed = cursed };

        static HashSet<int> OracleInitialValids(CardData[] cards, RuleDefinition rule)
        {
            var result = new HashSet<int>();
            switch (rule.Type)
            {
                case RuleType.Highest:
                {
                    int max = cards.Max(c => c.value);
                    for (int i = 0; i < cards.Length; i++) if (cards[i].value == max) result.Add(i);
                    break;
                }
                case RuleType.Lowest:
                {
                    int min = cards.Min(c => c.value);
                    for (int i = 0; i < cards.Length; i++) if (cards[i].value == min) result.Add(i);
                    break;
                }
                case RuleType.SecondHighest:
                {
                    var distinct = cards.Select(c => c.value).Distinct().OrderByDescending(x => x).ToList();
                    if (distinct.Count >= 2)
                    {
                        int target = distinct[1];
                        for (int i = 0; i < cards.Length; i++) if (cards[i].value == target) result.Add(i);
                    }
                    else
                    {
                        // fallback to highest
                        return OracleInitialValids(cards, RuleDefinition.Highest());
                    }
                    break;
                }
                case RuleType.SecondLowest:
                {
                    var distinct = cards.Select(c => c.value).Distinct().OrderBy(x => x).ToList();
                    if (distinct.Count >= 2)
                    {
                        int target = distinct[1];
                        for (int i = 0; i < cards.Length; i++) if (cards[i].value == target) result.Add(i);
                    }
                    else
                    {
                        // fallback to lowest
                        return OracleInitialValids(cards, RuleDefinition.Lowest());
                    }
                    break;
                }
                case RuleType.PickColor:
                {
                    bool wantRed = rule.Color == ColorFilter.Red;
                    var filtered = Enumerable.Range(0, cards.Length)
                        .Where(i => IsRed(cards[i]) == wantRed)
                        .ToList();
                    if (filtered.Count == 0) return result; // impossible this round per current rules
                    int max = filtered.Max(i => cards[i].value);
                    foreach (var i in filtered) if (cards[i].value == max) result.Add(i);
                    break;
                }
                case RuleType.AvoidSuit:
                {
                    var filtered = Enumerable.Range(0, cards.Length)
                        .Where(i => cards[i].suit != rule.AvoidSuit)
                        .ToList();
                    if (filtered.Count == 0) return result; // impossible per current rules
                    int max = filtered.Max(i => cards[i].value);
                    foreach (var i in filtered) if (cards[i].value == max) result.Add(i);
                    break;
                }
            }
            return result;
        }

        static bool IsRed(CardData c) => c.suit == Suit.Hearts || c.suit == Suit.Diamonds;

        // --- Deterministic unit tests (no curses) ---

        [Test]
        public void Highest_BasicAndTies()
        {
            var cards = new[] { C(5, Suit.Hearts), C(13, Suit.Spades), C(13, Suit.Diamonds), C(7, Suit.Clubs) };
            var rule = RuleDefinition.Highest();
            var expect = OracleInitialValids(cards, rule);
            var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            CollectionAssert.AreEquivalent(expect, got);
        }

        [Test]
        public void Lowest_BasicAndTies()
        {
            var cards = new[] { C(2, Suit.Hearts), C(13, Suit.Spades), C(13, Suit.Diamonds), C(2, Suit.Clubs) };
            var rule = RuleDefinition.Lowest();
            var expect = OracleInitialValids(cards, rule);
            var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            CollectionAssert.AreEquivalent(expect, got);
        }

        [Test]
        public void SecondHighest_Duplicates()
        {
            var cards = new[] { C(2, Suit.Hearts), C(10, Suit.Spades), C(10, Suit.Diamonds), C(8, Suit.Clubs) };
            var rule = RuleDefinition.SecondHighest();
            var expect = OracleInitialValids(cards, rule);
            var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            CollectionAssert.AreEquivalent(expect, got);
        }

        [Test]
        public void SecondLowest_NotEnoughDistinct_FallsBackToLowest()
        {
            var cards = new[] { C(7, Suit.Hearts), C(7, Suit.Spades), C(7, Suit.Diamonds) };
            var rule = RuleDefinition.SecondLowest();
            var expect = OracleInitialValids(cards, rule); // falls back inside oracle
            var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            CollectionAssert.AreEquivalent(expect, got);
        }

        [Test]
        public void PickColor_Red_WhenPresent()
        {
            var cards = new[] { C(9, Suit.Clubs), C(12, Suit.Diamonds), C(11, Suit.Hearts), C(12, Suit.Clubs) };
            var rule = RuleDefinition.PickRed();
            var expect = OracleInitialValids(cards, rule);
            var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            CollectionAssert.AreEquivalent(expect, got);
        }

        [Test]
        public void PickColor_Red_WhenAbsent_IsImpossible_CurrentBehavior()
        {
            var cards = new[] { C(9, Suit.Clubs), C(12, Suit.Spades), C(11, Suit.Clubs) };
            var rule = RuleDefinition.PickRed();
            var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            Assert.That(got.Count, Is.EqualTo(0), "Per current implementation, PickColor with no matches yields no corrects → impossible round.");
        }

        [Test]
        public void AvoidSuit_AllAreAvoided_IsImpossible_CurrentBehavior()
        {
            var cards = new[] { C(5, Suit.Spades), C(9, Suit.Spades), C(12, Suit.Spades) };
            var rule = RuleDefinition.Avoid(Suit.Spades);
            var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            Assert.That(got.Count, Is.EqualTo(0), "Per current implementation, AvoidSuit when all cards are avoided yields no corrects.");
        }

        // --- Property tests (no curses): oracle equivalence across random hands ---

        [Test]
        public void NoCurses_EqualsOracle_Fuzz()
        {
            var rules = new[]
            {
                RuleDefinition.Highest(),
                RuleDefinition.Lowest(),
                RuleDefinition.SecondHighest(),
                RuleDefinition.SecondLowest(),
                RuleDefinition.PickRed(),
                RuleDefinition.PickBlack(),
                RuleDefinition.Avoid(Suit.Spades),
                RuleDefinition.Avoid(Suit.Hearts)
            };

            var rng = new Random(12345);
            for (int trial = 0; trial < 1000; trial++)
            {
                int n = rng.Next(3, 7); // 3..6 cards
                var cards = new CardData[n];
                for (int i = 0; i < n; i++)
                    cards[i] = C(rng.Next(2, 15), (Suit)rng.Next(0, 4));

                foreach (var rule in rules)
                {
                    var expect = OracleInitialValids(cards, rule);
                    var got = RuleRuntime.GetCorrectAfterCurses(cards, rule);
                    Assert.That(got.SetEquals(expect), Is.True,
                        $"Mismatch for {rule.Type} on hand [{string.Join(",", cards.Select(c => c.value+"/"+c.suit))}]\nExpect: {{{string.Join(",", expect)}}} Got: {{{string.Join(",", got)}}}");
                }
            }
        }

        // --- Curses invariants: things that must always hold, regardless of randomness ---

        [Test]
        public void Curses_Invariants_Fuzz()
        {
            var rules = new Func<RuleDefinition>[]
            {
                () => RuleDefinition.Highest(CurseMode.OneOfValids),
                () => RuleDefinition.Highest(CurseMode.HalfOfValids),
                () => RuleDefinition.Highest(CurseMode.AllValids),
                () => RuleDefinition.Lowest(CurseMode.OneOfValids),
                () => RuleDefinition.SecondHighest(CurseMode.OneOfValids),
                () => RuleDefinition.SecondLowest(CurseMode.HalfOfValids),
                () => RuleDefinition.PickRed(CurseMode.OneOfValids),
                () => RuleDefinition.PickBlack(CurseMode.OneOfValids),
                () => RuleDefinition.Avoid(Suit.Spades, CurseMode.AllValids),
                () => RuleDefinition.Avoid(Suit.Hearts, CurseMode.OneOfValids),
            };

            var rng = new Random(67890);
            for (int trial = 0; trial < 1000; trial++)
            {
                int n = rng.Next(3, 7);
                var baseCards = new CardData[n];
                for (int i = 0; i < n; i++) baseCards[i] = C(rng.Next(2, 15), (Suit)rng.Next(0, 4));

                foreach (var mk in rules)
                {
                    var rule = mk();

                    var hand = CloneResetCurses(baseCards);

                    // Compute initial valids (oracle, same semantics as runtime)
                    var initial = OracleInitialValids(hand, new RuleDefinition
                    {
                        Type = rule.Type,
                        Color = rule.Color,
                        AvoidSuit = rule.AvoidSuit,
                        CurseMode = CurseMode.None
                    });

                    var result = RuleRuntime.GetCorrectAfterCurses(hand, rule);

                    // Only examine curses applied during THIS rule (on this fresh hand)
                    var cursedIdx = Enumerable.Range(0, hand.Length).Where(i => hand[i].cursed).ToList();
                    foreach (var ci in cursedIdx)
                        Assert.IsTrue(initial.Contains(ci), $"Cursed index {ci} not in initial valids for {rule.Type}/{rule.CurseMode}.");

                    // If there exists at least one non-cursed card and there were any initial valids,
                    // final should be non-empty (except the documented everyone-cursed edge case)
                    bool anyNonCursed = hand.Any(c => !c.cursed);
                    bool initialHad = initial.Count > 0;
                    bool everyoneWasCursed = hand.All(c => c.cursed);
                    if (initialHad && anyNonCursed && !everyoneWasCursed)
                        Assert.That(result.Count, Is.GreaterThan(0), $"Final set empty though non-cursed options exist for {rule.Type}/{rule.CurseMode}.");
                }
            }
        }

        [Test]
        public void AllEqualValues_AllValidsOnHighest_CanYieldNoAnswer_CurrentBehavior()
        {
            var cards = new[] {
                new CardData { value = 10, suit = Suit.Spades },
                new CardData { value = 10, suit = Suit.Hearts },
                new CardData { value = 10, suit = Suit.Diamonds }
            };
            var rule = RuleDefinition.Highest(CurseMode.AllValids);
            var result = RuleRuntime.GetCorrectAfterCurses(cards, rule);
            Assert.That(result.Count, Is.EqualTo(0),
                "AllValids on all-equal Highest leaves no non-cursed fallback; consider preventing this in rule selection or curse application.");
        }

        static CardData[] CloneResetCurses(CardData[] src)
        {
            var clone = new CardData[src.Length];
            for (int i = 0; i < src.Length; i++)
                clone[i] = new CardData { value = src[i].value, suit = src[i].suit, cursed = false };
            return clone;
        }
    }
}
