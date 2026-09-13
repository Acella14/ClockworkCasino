using System;
using System.Collections.Generic;
using System.Linq;
using ClockworkCasino.Cards;
using ClockworkCasino.Gameplay;
using ClockworkCasino.Rules;
using NUnit.Framework;

namespace ClockworkCasino.Tests
{
    public sealed class RuleObjectiveFactoryGenerationTests
    {
        private const int TrialsPerRule = 200;

        [Test]
        public void EveryActiveRule_GeneratesValidPlayableRounds_WithoutCurses()
        {
            foreach (Func<RuleDefinition> createRule
                     in CreateActiveRuleFactories())
            {
                for (int trial = 0;
                     trial < TrialsPerRule;
                     trial++)
                {
                    RuleDefinition rule =
                        createRule();

                    GeneratedRound round =
                        GenerateRound(
                            rule,
                            cardCount: GetCardCountForRule(rule),
                            curseProbability: 0f,
                            CurseMode.None,
                            seed: 10000 + trial);

                    AssertGeneratedRoundIsStructurallyValid(
                        round);

                    AssertObjectiveMatchesRuleSemantics(
                        round);
                }
            }
        }

        [Test]
        public void EveryActiveRule_WithForcedCurses_NeverCursesCorrectAnswers()
        {
            foreach (Func<RuleDefinition> createRule
                     in CreateActiveRuleFactories())
            {
                for (int trial = 0;
                     trial < TrialsPerRule;
                     trial++)
                {
                    RuleDefinition rule =
                        createRule();

                    GeneratedRound round =
                        GenerateRound(
                            rule,
                            cardCount: GetCardCountForRule(rule),
                            curseProbability: 1f,
                            CurseMode.AllValids,
                            seed: 20000 + trial);

                    AssertGeneratedRoundIsStructurallyValid(
                        round);

                    AssertNoCorrectCardsAreCursed(
                        round);

                    AssertObjectiveMatchesRuleSemantics(
                        round);
                }
            }
        }

        [Test]
        public void ActiveRuleFactories_DoNotIncludeRemovedRuleFamilies()
        {
            List<RuleFamily> activeFamilies =
                CreateActiveRuleFactories()
                    .Select(factory => factory().Family)
                    .ToList();

            Assert.That(
                activeFamilies,
                Has.No.Member(
                    RuleFamily.LegacyAvoidSuit),
                "Avoid-suit rules are intentionally not active right now.");

            Assert.That(
                activeFamilies,
                Has.No.Member(
                    RuleFamily.LegacyHighestColor),
                "Legacy PickRed/PickBlack should not be active; use HighestColor instead.");
        }

        [Test]
        public void PairSum_GeneratesExactTargetPair()
        {
            for (int trial = 0;
                 trial < TrialsPerRule;
                 trial++)
            {
                GeneratedRound round =
                    GenerateRound(
                        RuleDefinition.PairSum(),
                        cardCount: 5,
                        curseProbability: 0f,
                        CurseMode.None,
                        seed: 30000 + trial);

                int[] correct =
                    round.Objective.ValidIndices.ToArray();

                Assert.That(
                    correct.Length,
                    Is.EqualTo(2));

                int sum =
                    round.Cards[correct[0]].value
                    + round.Cards[correct[1]].value;

                Assert.That(
                    sum,
                    Is.EqualTo(
                        round.Rule.TargetValue));
            }
        }

        [Test]
        public void PairDifference_GeneratesExactDifferencePair()
        {
            for (int trial = 0;
                 trial < TrialsPerRule;
                 trial++)
            {
                GeneratedRound round =
                    GenerateRound(
                        RuleDefinition.PairDifference(),
                        cardCount: 5,
                        curseProbability: 0f,
                        CurseMode.None,
                        seed: 40000 + trial);

                int[] correct =
                    round.Objective.ValidIndices.ToArray();

                Assert.That(
                    correct.Length,
                    Is.EqualTo(2));

                int difference =
                    Math.Abs(
                        round.Cards[correct[0]].value
                        - round.Cards[correct[1]].value);

                Assert.That(
                    difference,
                    Is.EqualTo(
                        round.Rule.DifferenceValue));
            }
        }

        [Test]
        public void OrderedRules_HaveCorrectOrderLength()
        {
            foreach (Func<RuleDefinition> createRule
                     in new Func<RuleDefinition>[]
                     {
                         () => RuleDefinition.LowestThenHighest(),
                         () => RuleDefinition.HighestThenLowest(),
                         () => RuleDefinition.PairSumLowFirst(),
                         () => RuleDefinition.SameSuitPairLowFirst()
                     })
            {
                for (int trial = 0;
                     trial < TrialsPerRule;
                     trial++)
                {
                    RuleDefinition rule =
                        createRule();

                    GeneratedRound round =
                        GenerateRound(
                            rule,
                            cardCount: 6,
                            curseProbability: 1f,
                            CurseMode.OneOfValids,
                            seed: 50000 + trial);

                    Assert.That(
                        round.Objective.SelectionMode,
                        Is.EqualTo(
                            SelectionMode.Ordered));

                    Assert.That(
                        round.Objective.CorrectOrder.Count,
                        Is.EqualTo(
                            rule.RequiredCount));

                    AssertNoCorrectCardsAreCursed(
                        round);

                    AssertObjectiveMatchesRuleSemantics(
                        round);
                }
            }
        }

        [Test]
        public void PairSumLowFirst_GeneratesExactTargetPair_InLowFirstOrder()
        {
            for (int trial = 0;
                 trial < TrialsPerRule;
                 trial++)
            {
                GeneratedRound round =
                    GenerateRound(
                        RuleDefinition.PairSumLowFirst(),
                        cardCount: 6,
                        curseProbability: 1f,
                        CurseMode.OneOfValids,
                        seed: 60000 + trial);

                AssertOrderedPairSumLowFirst(
                    round);

                AssertNoCorrectCardsAreCursed(
                    round);
            }
        }

        [Test]
        public void SameSuitPairLowFirst_GeneratesSameSuitPair_InLowFirstOrder()
        {
            for (int trial = 0;
                 trial < TrialsPerRule;
                 trial++)
            {
                GeneratedRound round =
                    GenerateRound(
                        RuleDefinition.SameSuitPairLowFirst(),
                        cardCount: 6,
                        curseProbability: 1f,
                        CurseMode.OneOfValids,
                        seed: 70000 + trial);

                AssertOrderedSameSuitLowFirst(
                    round);

                AssertNoCorrectCardsAreCursed(
                    round);
            }
        }

        private static GeneratedRound GenerateRound(
            RuleDefinition rule,
            int cardCount,
            float curseProbability,
            CurseMode curseMode,
            int seed)
        {
            var profile =
                new RuleGenerationProfile(
                    cardCount,
                    tableIndex: 0,
                    curseProbability,
                    curseMode);

            return RuleObjectiveFactory.Generate(
                profile,
                rule,
                new Random(seed));
        }

        private static IReadOnlyList<Func<RuleDefinition>>
            CreateActiveRuleFactories()
        {
            return new Func<RuleDefinition>[]
            {
                () => RuleDefinition.Highest(),
                () => RuleDefinition.Lowest(),
                () => RuleDefinition.MiddleValue(),
                () => RuleDefinition.HighestRed(),
                () => RuleDefinition.HighestBlack(),
                () => RuleDefinition.TwoHighest(),
                () => RuleDefinition.TwoLowest(),

                () => RuleDefinition.SecondHighest(),
                () => RuleDefinition.SecondLowest(),
                () => RuleDefinition.TwoClosestValues(),
                () => RuleDefinition.TwoFarthestValues(),
                () => RuleDefinition.PairSum(),
                () => RuleDefinition.PairDifference(),
                () => RuleDefinition.LowestRed(),
                () => RuleDefinition.LowestBlack(),

                () => RuleDefinition.SameSuitPair(),
                () => RuleDefinition.LowestThenHighest(),
                () => RuleDefinition.HighestThenLowest(),

                () => RuleDefinition.ThreeCardSum(),
                () => RuleDefinition.PairSumLowFirst(),
                () => RuleDefinition.SameSuitPairLowFirst()
            };
        }

        private static int GetCardCountForRule(
            RuleDefinition rule)
        {
            switch (rule.Family)
            {
                case RuleFamily.ThreeCardSum:
                    return 6;

                case RuleFamily.PairSumLowFirst:
                case RuleFamily.SameSuitPairLowFirst:
                case RuleFamily.SameSuitPair:
                    return 6;

                default:
                    return 5;
            }
        }

        private static void AssertGeneratedRoundIsStructurallyValid(
            GeneratedRound round)
        {
            Assert.That(
                round,
                Is.Not.Null);

            Assert.That(
                round.Cards,
                Is.Not.Null);

            Assert.That(
                round.Cards.Length,
                Is.GreaterThan(0));

            Assert.That(
                round.Rule,
                Is.Not.Null);

            Assert.That(
                round.Objective,
                Is.Not.Null);

            Assert.That(
                round.Objective.RequiredSelectionCount,
                Is.GreaterThan(0));

            IEnumerable<int> answerIndices =
                GetAnswerIndices(round.Objective);

            foreach (int index in answerIndices)
            {
                Assert.That(
                    index,
                    Is.GreaterThanOrEqualTo(0));

                Assert.That(
                    index,
                    Is.LessThan(
                        round.Cards.Length));
            }
        }

        private static void AssertNoCorrectCardsAreCursed(
            GeneratedRound round)
        {
            foreach (int index in GetAnswerIndices(round.Objective))
            {
                Assert.That(
                    round.Cards[index].cursed,
                    Is.False,
                    $"Correct card index {index} was cursed for rule "
                    + $"'{round.Objective.DisplayText}'.");
            }
        }

        private static IEnumerable<int> GetAnswerIndices(
            RoundObjective objective)
        {
            if (objective.SelectionMode == SelectionMode.Ordered)
                return objective.CorrectOrder;

            return objective.ValidIndices;
        }

        private static void AssertObjectiveMatchesRuleSemantics(
            GeneratedRound round)
        {
            switch (round.Rule.Family)
            {
                case RuleFamily.LegacyHighest:
                    AssertSingleEquals(
                        round,
                        GetHighestIndex(round.Cards));
                    break;

                case RuleFamily.LegacyLowest:
                    AssertSingleEquals(
                        round,
                        GetLowestIndex(round.Cards));
                    break;

                case RuleFamily.LegacySecondHighest:
                    AssertSingleEquals(
                        round,
                        GetNthIndex(
                            round.Cards,
                            descending: true,
                            n: 1));
                    break;

                case RuleFamily.LegacySecondLowest:
                    AssertSingleEquals(
                        round,
                        GetNthIndex(
                            round.Cards,
                            descending: false,
                            n: 1));
                    break;

                case RuleFamily.MiddleValue:
                    AssertSingleEquals(
                        round,
                        GetNthIndex(
                            round.Cards,
                            descending: false,
                            n: round.Cards.Length / 2));
                    break;

                case RuleFamily.HighestColor:
                    AssertSingleEquals(
                        round,
                        GetColorExtremeIndex(
                            round.Cards,
                            round.Rule.Color,
                            wantsHighest: true));
                    break;

                case RuleFamily.LowestColor:
                    AssertSingleEquals(
                        round,
                        GetColorExtremeIndex(
                            round.Cards,
                            round.Rule.Color,
                            wantsHighest: false));
                    break;

                case RuleFamily.TopN:
                    AssertSetEquals(
                        round,
                        GetTopNIndices(
                            round.Cards,
                            round.Rule.RequiredCount));
                    break;

                case RuleFamily.BottomN:
                    AssertSetEquals(
                        round,
                        GetBottomNIndices(
                            round.Cards,
                            round.Rule.RequiredCount));
                    break;

                case RuleFamily.TwoClosestValues:
                    AssertSetEquals(
                        round,
                        GetClosestPair(round.Cards));
                    break;

                case RuleFamily.TwoFarthestValues:
                    AssertSetEquals(
                        round,
                        GetFarthestPair(round.Cards));
                    break;

                case RuleFamily.PairSum:
                    AssertPairSum(round);
                    break;

                case RuleFamily.PairDifference:
                    AssertPairDifference(round);
                    break;

                case RuleFamily.SameSuitPair:
                    AssertSameSuitPair(round);
                    break;

                case RuleFamily.ThreeCardSum:
                    AssertThreeCardSum(round);
                    break;

                case RuleFamily.LowestThenHighest:
                    AssertOrderedEquals(
                        round,
                        new[]
                        {
                            GetLowestIndex(round.Cards),
                            GetHighestIndex(round.Cards)
                        });
                    break;

                case RuleFamily.HighestThenLowest:
                    AssertOrderedEquals(
                        round,
                        new[]
                        {
                            GetHighestIndex(round.Cards),
                            GetLowestIndex(round.Cards)
                        });
                    break;

                case RuleFamily.PairSumLowFirst:
                    AssertOrderedPairSumLowFirst(round);
                    break;

                case RuleFamily.SameSuitPairLowFirst:
                    AssertOrderedSameSuitLowFirst(round);
                    break;
            }
        }

        private static void AssertSingleEquals(
            GeneratedRound round,
            int expectedIndex)
        {
            Assert.That(
                round.Objective.SelectionMode,
                Is.EqualTo(
                    SelectionMode.Single));

            Assert.That(
                round.Objective.ValidIndices,
                Is.EquivalentTo(
                    new[] { expectedIndex }));
        }

        private static void AssertSetEquals(
            GeneratedRound round,
            IEnumerable<int> expectedIndices)
        {
            Assert.That(
                round.Objective.ValidIndices,
                Is.EquivalentTo(
                    expectedIndices));
        }

        private static void AssertOrderedEquals(
            GeneratedRound round,
            IEnumerable<int> expectedOrder)
        {
            Assert.That(
                round.Objective.CorrectOrder,
                Is.EqualTo(
                    expectedOrder.ToArray()));
        }

        private static void AssertPairSum(
            GeneratedRound round)
        {
            int[] indices =
                round.Objective.ValidIndices.ToArray();

            Assert.That(indices.Length, Is.EqualTo(2));

            Assert.That(
                round.Cards[indices[0]].value
                + round.Cards[indices[1]].value,
                Is.EqualTo(
                    round.Rule.TargetValue));
        }

        private static void AssertPairDifference(
            GeneratedRound round)
        {
            int[] indices =
                round.Objective.ValidIndices.ToArray();

            Assert.That(indices.Length, Is.EqualTo(2));

            Assert.That(
                Math.Abs(
                    round.Cards[indices[0]].value
                    - round.Cards[indices[1]].value),
                Is.EqualTo(
                    round.Rule.DifferenceValue));
        }

        private static void AssertThreeCardSum(
            GeneratedRound round)
        {
            int[] indices =
                round.Objective.ValidIndices.ToArray();

            Assert.That(indices.Length, Is.EqualTo(3));

            int sum = 0;

            for (int index = 0;
                 index < indices.Length;
                 index++)
            {
                sum += round.Cards[indices[index]].value;
            }

            Assert.That(
                sum,
                Is.EqualTo(
                    round.Rule.TargetValue));
        }

        private static void AssertSameSuitPair(
            GeneratedRound round)
        {
            int[] indices =
                round.Objective.ValidIndices.ToArray();

            Assert.That(indices.Length, Is.EqualTo(2));

            Assert.That(
                round.Cards[indices[0]].suit,
                Is.EqualTo(
                    round.Cards[indices[1]].suit));
        }

        private static void AssertOrderedPairSumLowFirst(
            GeneratedRound round)
        {
            int[] order =
                round.Objective.CorrectOrder.ToArray();

            Assert.That(order.Length, Is.EqualTo(2));

            Assert.That(
                round.Cards[order[0]].value,
                Is.LessThanOrEqualTo(
                    round.Cards[order[1]].value));

            Assert.That(
                round.Cards[order[0]].value
                + round.Cards[order[1]].value,
                Is.EqualTo(
                    round.Rule.TargetValue));
        }

        private static void AssertOrderedSameSuitLowFirst(
            GeneratedRound round)
        {
            int[] order =
                round.Objective.CorrectOrder.ToArray();

            Assert.That(order.Length, Is.EqualTo(2));

            Assert.That(
                round.Cards[order[0]].suit,
                Is.EqualTo(
                    round.Cards[order[1]].suit));

            Assert.That(
                round.Cards[order[0]].value,
                Is.LessThanOrEqualTo(
                    round.Cards[order[1]].value));
        }

        private static int GetHighestIndex(CardData[] cards)
        {
            return GetNthIndex(
                cards,
                descending: true,
                n: 0);
        }

        private static int GetLowestIndex(CardData[] cards)
        {
            return GetNthIndex(
                cards,
                descending: false,
                n: 0);
        }

        private static int GetNthIndex(
            CardData[] cards,
            bool descending,
            int n)
        {
            List<int> indices =
                Enumerable.Range(0, cards.Length).ToList();

            indices.Sort(
                (left, right) =>
                {
                    int comparison =
                        cards[left].value.CompareTo(
                            cards[right].value);

                    if (descending)
                        comparison = -comparison;

                    return comparison != 0
                        ? comparison
                        : left.CompareTo(right);
                });

            return indices[n];
        }

        private static int GetColorExtremeIndex(
            CardData[] cards,
            ColorFilter color,
            bool wantsHighest)
        {
            bool wantsRed =
                color == ColorFilter.Red;

            List<int> indices =
                Enumerable.Range(0, cards.Length)
                    .Where(index =>
                        IsRed(cards[index]) == wantsRed)
                    .ToList();

            indices.Sort(
                (left, right) =>
                    cards[left].value.CompareTo(
                        cards[right].value));

            if (wantsHighest)
                indices.Reverse();

            return indices[0];
        }

        private static IEnumerable<int> GetTopNIndices(
            CardData[] cards,
            int count)
        {
            return Enumerable.Range(0, cards.Length)
                .OrderByDescending(index => cards[index].value)
                .ThenBy(index => index)
                .Take(count);
        }

        private static IEnumerable<int> GetBottomNIndices(
            CardData[] cards,
            int count)
        {
            return Enumerable.Range(0, cards.Length)
                .OrderBy(index => cards[index].value)
                .ThenBy(index => index)
                .Take(count);
        }

        private static IEnumerable<int> GetClosestPair(
            CardData[] cards)
        {
            return GetBestPairByDifference(
                cards,
                wantsLowest: true);
        }

        private static IEnumerable<int> GetFarthestPair(
            CardData[] cards)
        {
            return GetBestPairByDifference(
                cards,
                wantsLowest: false);
        }

        private static IEnumerable<int> GetBestPairByDifference(
            CardData[] cards,
            bool wantsLowest)
        {
            int bestLeft = -1;
            int bestRight = -1;
            int bestDifference =
                wantsLowest ? int.MaxValue : int.MinValue;

            for (int left = 0;
                 left < cards.Length - 1;
                 left++)
            {
                for (int right = left + 1;
                     right < cards.Length;
                     right++)
                {
                    int difference =
                        Math.Abs(
                            cards[left].value
                            - cards[right].value);

                    bool isBetter =
                        wantsLowest
                            ? difference < bestDifference
                            : difference > bestDifference;

                    if (!isBetter)
                        continue;

                    bestDifference = difference;
                    bestLeft = left;
                    bestRight = right;
                }
            }

            return new[] { bestLeft, bestRight };
        }

        private static bool IsRed(CardData card)
        {
            return card.suit == Suit.Hearts
                   || card.suit == Suit.Diamonds;
        }
    }
}