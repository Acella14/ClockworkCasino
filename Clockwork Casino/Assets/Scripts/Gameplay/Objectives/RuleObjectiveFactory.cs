using System;
using System.Collections.Generic;
using ClockworkCasino.Cards;
using ClockworkCasino.Rules;

namespace ClockworkCasino.Gameplay
{
    public static class RuleObjectiveFactory
    {
        private const int MinimumCardValue = 2;
        private const int MaximumCardValue = 14;
        private const int MaximumAttempts = 400;

        public static GeneratedRound Generate(
            RuleGenerationProfile profile,
            RuleDefinition rule,
            Random random)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (random == null)
                throw new ArgumentNullException(nameof(random));

            bool shouldCurse =
                profile.RollForCurse(random);

            CardData[] cards =
                GenerateRandomHand(
                    profile.CardCount,
                    random);

            BuildResult result =
                BuildGeneratedObjective(
                    cards,
                    rule,
                    random,
                    shouldCurse);

            ApplyObjectiveAwareCurses(
                cards,
                result,
                rule,
                shouldCurse,
                random);

            return new GeneratedRound(
                rule,
                cards,
                result.Objective);
        }

        public static RoundObjective BuildCustomSingle(
            string displayText,
            IEnumerable<int> correctIndices)
        {
            return RoundObjective.CreateSingle(
                displayText,
                correctIndices,
                failImmediatelyOnMistake: true);
        }

        private static BuildResult BuildGeneratedObjective(
            CardData[] cards,
            RuleDefinition rule,
            Random random,
            bool shouldCreateCursedDecoy)
        {
            return rule.Family switch
            {
                RuleFamily.LegacyHighest =>
                    BuildHighest(cards, rule, random),

                RuleFamily.LegacyLowest =>
                    BuildLowest(cards, rule, random),

                RuleFamily.LegacySecondHighest =>
                    BuildSecondHighest(cards, rule, random),

                RuleFamily.LegacySecondLowest =>
                    BuildSecondLowest(cards, rule, random),

                RuleFamily.LegacyHighestColor =>
                    BuildHighestColor(cards, rule, random),

                RuleFamily.LegacyAvoidSuit =>
                    BuildAvoidSuit(cards, rule, random),

                RuleFamily.MiddleValue =>
                    BuildMiddleValue(cards, rule, random),

                RuleFamily.HighestColor =>
                    BuildHighestColor(cards, rule, random),

                RuleFamily.LowestColor =>
                    BuildLowestColor(cards, rule, random),

                RuleFamily.TopN =>
                    BuildTopN(cards, rule, random),

                RuleFamily.BottomN =>
                    BuildBottomN(cards, rule, random),

                RuleFamily.TwoClosestValues =>
                    BuildTwoClosestValues(cards, rule, random),

                RuleFamily.TwoFarthestValues =>
                    BuildTwoFarthestValues(cards, rule, random),

                RuleFamily.PairSum =>
                    BuildPairByUniqueTarget(
                        cards,
                        rule,
                        random,
                        shouldCreateCursedDecoy,
                        pair => pair.Sum,
                        target => target >= 5,
                        target =>
                            $"Pick Two Cards That Add to {target}",
                        orderedLowFirst: false),

                RuleFamily.PairDifference =>
                    BuildPairByUniqueTarget(
                        cards,
                        rule,
                        random,
                        shouldCreateCursedDecoy,
                        pair => pair.Difference,
                        target => target >= 2,
                        target =>
                            $"Pick Two Cards {target} Apart",
                        orderedLowFirst: false),

                RuleFamily.SameSuitPair =>
                    BuildSameSuitPair(
                        cards,
                        rule,
                        random,
                        orderedLowFirst: false),

                RuleFamily.ThreeCardSum =>
                    BuildThreeCardSum(
                        cards,
                        rule,
                        random,
                        shouldCreateCursedDecoy),

                RuleFamily.LowestThenHighest =>
                    BuildLowestThenHighest(
                        cards,
                        rule,
                        random),

                RuleFamily.HighestThenLowest =>
                    BuildHighestThenLowest(
                        cards,
                        rule,
                        random),

                RuleFamily.PairSumLowFirst =>
                    BuildPairByUniqueTarget(
                        cards,
                        rule,
                        random,
                        shouldCreateCursedDecoy,
                        pair => pair.Sum,
                        target => target >= 5,
                        target =>
                            $"Pick Two That Add to {target}, Low First",
                        orderedLowFirst: true),

                RuleFamily.SameSuitPairLowFirst =>
                    BuildSameSuitPair(
                        cards,
                        rule,
                        random,
                        orderedLowFirst: true),

                _ =>
                    BuildHighest(cards, rule, random)
            };
        }

        private static BuildResult BuildHighest(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> descending =
                GetIndicesSortedByValue(
                    cards,
                    descending: true);

            return BuildSingleResult(
                rule,
                descending[0],
                Slice(descending, 1, 2));
        }

        private static BuildResult BuildLowest(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> ascending =
                GetIndicesSortedByValue(
                    cards,
                    descending: false);

            return BuildSingleResult(
                rule,
                ascending[0],
                Slice(ascending, 1, 2));
        }

        private static BuildResult BuildSecondHighest(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> descending =
                GetIndicesSortedByValue(
                    cards,
                    descending: true);

            return BuildSingleResult(
                rule,
                descending[1],
                new[] { descending[0] });
        }

        private static BuildResult BuildSecondLowest(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> ascending =
                GetIndicesSortedByValue(
                    cards,
                    descending: false);

            return BuildSingleResult(
                rule,
                ascending[1],
                new[] { ascending[0] });
        }

        private static BuildResult BuildMiddleValue(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> ascending =
                GetIndicesSortedByValue(
                    cards,
                    descending: false);

            int middlePosition =
                ascending.Count / 2;

            return BuildSingleResult(
                rule,
                ascending[middlePosition],
                GetNeighborIndices(
                    ascending,
                    middlePosition));
        }

        private static BuildResult BuildHighestColor(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            return BuildColorExtreme(
                cards,
                rule,
                random,
                wantsHighest: true);
        }

        private static BuildResult BuildLowestColor(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            return BuildColorExtreme(
                cards,
                rule,
                random,
                wantsHighest: false);
        }

        private static BuildResult BuildColorExtreme(
            CardData[] cards,
            RuleDefinition rule,
            Random random,
            bool wantsHighest)
        {
            PrepareDistinctValues(cards, random);

            bool wantsRed =
                rule.Color == ColorFilter.Red;

            EnsureColorCount(
                cards,
                wantsRed,
                minimumCount: 2,
                random);

            List<int> colorIndices =
                GetColorIndices(cards, wantsRed);

            colorIndices.Sort(
                (left, right) =>
                    cards[left].value.CompareTo(
                        cards[right].value));

            if (wantsHighest)
                colorIndices.Reverse();

            return BuildSingleResult(
                rule,
                colorIndices[0],
                Slice(colorIndices, 1, 2));
        }

        private static BuildResult BuildAvoidSuit(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].suit == rule.AvoidSuit)
                {
                    cards[index].suit =
                        PickSuitOtherThan(
                            rule.AvoidSuit,
                            random);

                    break;
                }
            }

            List<int> eligible = new();

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].suit != rule.AvoidSuit)
                    eligible.Add(index);
            }

            eligible.Sort(
                (left, right) =>
                    cards[right].value.CompareTo(
                        cards[left].value));

            return BuildSingleResult(
                rule,
                eligible[0],
                Slice(eligible, 1, 2));
        }

        private static BuildResult BuildTopN(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> descending =
                GetIndicesSortedByValue(
                    cards,
                    descending: true);

            return BuildMultipleResult(
                rule,
                Slice(descending, 0, rule.RequiredCount),
                Slice(descending, rule.RequiredCount, 2));
        }

        private static BuildResult BuildBottomN(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> ascending =
                GetIndicesSortedByValue(
                    cards,
                    descending: false);

            return BuildMultipleResult(
                rule,
                Slice(ascending, 0, rule.RequiredCount),
                Slice(ascending, rule.RequiredCount, 2));
        }

        private static BuildResult BuildTwoClosestValues(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<PairCandidate> pairs =
                GetAllPairs(cards);

            pairs.Sort(
                (left, right) =>
                    left.Difference.CompareTo(
                        right.Difference));

            PairCandidate correct =
                pairs[0];

            PairCandidate decoy =
                FindFirstDisjointPair(
                    pairs,
                    correct,
                    startIndex: 1);

            return BuildPairMultipleResult(
                rule,
                correct,
                decoy);
        }

        private static BuildResult BuildTwoFarthestValues(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<PairCandidate> pairs =
                GetAllPairs(cards);

            pairs.Sort(
                (left, right) =>
                    right.Difference.CompareTo(
                        left.Difference));

            PairCandidate correct =
                pairs[0];

            PairCandidate decoy =
                FindFirstDisjointPair(
                    pairs,
                    correct,
                    startIndex: 1);

            return BuildPairMultipleResult(
                rule,
                correct,
                decoy);
        }

        private static BuildResult BuildPairByUniqueTarget(
            CardData[] cards,
            RuleDefinition rule,
            Random random,
            bool wantsCursedDecoy,
            Func<PairCandidate, int> targetSelector,
            Func<int, bool> targetAllowed,
            Func<int, string> displayTextFactory,
            bool orderedLowFirst)
        {
            for (int attempt = 0;
                 attempt < MaximumAttempts;
                 attempt++)
            {
                RandomizeDistinctValues(cards, random);

                List<PairCandidate> pairs =
                    GetAllPairs(cards);

                Dictionary<int, List<PairCandidate>> byTarget =
                    GroupPairsByTarget(
                        pairs,
                        targetSelector,
                        targetAllowed);

                List<int> usableTargets =
                    GetUsableTargets(
                        byTarget,
                        wantsCursedDecoy);

                if (usableTargets.Count == 0)
                    continue;

                int target =
                    usableTargets[
                        random.Next(0, usableTargets.Count)];

                List<PairCandidate> targetPairs =
                    byTarget[target];

                Shuffle(targetPairs, random);

                PairCandidate correct =
                    targetPairs[0];

                PairCandidate decoy =
                    wantsCursedDecoy
                        ? FindFirstDisjointPair(
                            targetPairs,
                            correct,
                            startIndex: 1)
                        : PairCandidate.Invalid;

                rule.TargetValue = target;
                rule.DifferenceValue = target;
                rule.DisplayText =
                    displayTextFactory(target);

                if (orderedLowFirst)
                {
                    return BuildOrderedPairResult(
                        rule,
                        correct,
                        decoy,
                        cards);
                }

                return BuildPairMultipleResult(
                    rule,
                    correct,
                    decoy);
            }

            throw new InvalidOperationException(
                $"Could not generate rule: {rule.DisplayText}");
        }

        private static BuildResult BuildSameSuitPair(
            CardData[] cards,
            RuleDefinition rule,
            Random random,
            bool orderedLowFirst)
        {
            for (int attempt = 0;
                 attempt < MaximumAttempts;
                 attempt++)
            {
                RandomizeDistinctValues(cards, random);
                RandomizeSuits(cards, random);

                List<PairCandidate> pairs =
                    GetAllPairs(cards)
                        .FindAll(
                            pair =>
                                cards[pair.FirstIndex].suit
                                == cards[pair.SecondIndex].suit);

                if (pairs.Count == 0)
                    continue;

                pairs.Sort(
                    (left, right) =>
                        right.Sum.CompareTo(left.Sum));

                if (CountPairsWithSum(pairs, pairs[0].Sum) != 1)
                    continue;

                PairCandidate correct =
                    pairs[0];

                PairCandidate decoy =
                    FindFirstDisjointPair(
                        pairs,
                        correct,
                        startIndex: 1);

                rule.DisplayText =
                    orderedLowFirst
                        ? "Pick Highest Same-Suit Pair, Low First"
                        : "Pick the Highest Same-Suit Pair";

                if (orderedLowFirst)
                {
                    return BuildOrderedPairResult(
                        rule,
                        correct,
                        decoy,
                        cards);
                }

                return BuildPairMultipleResult(
                    rule,
                    correct,
                    decoy);
            }

            throw new InvalidOperationException(
                "Could not generate same-suit pair rule.");
        }

        private static BuildResult BuildThreeCardSum(
            CardData[] cards,
            RuleDefinition rule,
            Random random,
            bool wantsCursedDecoy)
        {
            for (int attempt = 0;
                 attempt < MaximumAttempts;
                 attempt++)
            {
                RandomizeDistinctValues(cards, random);

                List<TripleCandidate> triples =
                    GetAllTriples(cards);

                Dictionary<int, List<TripleCandidate>> bySum =
                    new();

                foreach (TripleCandidate triple in triples)
                {
                    if (!bySum.TryGetValue(
                            triple.Sum,
                            out List<TripleCandidate> list))
                    {
                        list = new List<TripleCandidate>();
                        bySum[triple.Sum] = list;
                    }

                    list.Add(triple);
                }

                List<int> targets =
                    GetUsableTripleTargets(
                        bySum,
                        wantsCursedDecoy);

                if (targets.Count == 0)
                    continue;

                int target =
                    targets[random.Next(0, targets.Count)];

                List<TripleCandidate> targetTriples =
                    bySum[target];

                Shuffle(targetTriples, random);

                TripleCandidate correct =
                    targetTriples[0];

                TripleCandidate decoy =
                    wantsCursedDecoy
                        ? FindFirstDisjointTriple(
                            targetTriples,
                            correct,
                            startIndex: 1)
                        : TripleCandidate.Invalid;

                rule.TargetValue = target;
                rule.DisplayText =
                    $"Pick Three Cards That Add to {target}";

                return BuildMultipleResult(
                    rule,
                    correct.ToArray(),
                    decoy.IsValid
                        ? decoy.ToArray()
                        : Array.Empty<int>());
            }

            throw new InvalidOperationException(
                "Could not generate three-card-sum rule.");
        }

        private static BuildResult BuildLowestThenHighest(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> ascending =
                GetIndicesSortedByValue(
                    cards,
                    descending: false);

            return BuildOrderedResult(
                rule,
                new[]
                {
                    ascending[0],
                    ascending[ascending.Count - 1]
                },
                GetNeighborIndices(ascending, 1));
        }

        private static BuildResult BuildHighestThenLowest(
            CardData[] cards,
            RuleDefinition rule,
            Random random)
        {
            PrepareDistinctValues(cards, random);

            List<int> descending =
                GetIndicesSortedByValue(
                    cards,
                    descending: true);

            return BuildOrderedResult(
                rule,
                new[]
                {
                    descending[0],
                    descending[descending.Count - 1]
                },
                GetNeighborIndices(descending, 1));
        }
        private static void ApplyObjectiveAwareCurses(
            CardData[] cards,
            BuildResult result,
            RuleDefinition rule,
            bool shouldCurse,
            Random random)
        {
            if (!shouldCurse)
                return;

            HashSet<int> correctSet =
                new(result.CorrectIndices);

            List<int> candidates =
                FilterOutCorrectIndices(
                    result.DecoyIndices,
                    correctSet);

            if (candidates.Count == 0)
            {
                candidates =
                    GetNonAnswerIndices(
                        cards.Length,
                        correctSet);
            }

            if (candidates.Count == 0)
                return;

            Shuffle(candidates, random);

            int curseCount = rule.CurseMode switch
            {
                CurseMode.HalfOfValids =>
                    Math.Max(1, candidates.Count / 2),

                CurseMode.AllValids =>
                    candidates.Count,

                _ =>
                    1
            };

            curseCount =
                Math.Min(
                    curseCount,
                    candidates.Count);

            for (int index = 0;
                 index < curseCount;
                 index++)
            {
                MarkCardCursed(
                    cards,
                    candidates[index]);
            }
        }

        private static BuildResult BuildSingleResult(
            RuleDefinition rule,
            int correctIndex,
            IEnumerable<int> decoys)
        {
            RoundObjective objective =
                RoundObjective.CreateSingle(
                    rule.DisplayText,
                    new[] { correctIndex },
                    rule.FailImmediatelyOnMistake);

            return new BuildResult(
                objective,
                new[] { correctIndex },
                decoys);
        }

        private static BuildResult BuildMultipleResult(
            RuleDefinition rule,
            IEnumerable<int> correctIndices,
            IEnumerable<int> decoys)
        {
            RoundObjective objective =
                RoundObjective.CreateMultiple(
                    rule.DisplayText,
                    correctIndices,
                    rule.AllowDeselection,
                    rule.FailImmediatelyOnMistake);

            return new BuildResult(
                objective,
                correctIndices,
                decoys);
        }

        private static BuildResult BuildOrderedResult(
            RuleDefinition rule,
            IEnumerable<int> correctOrder,
            IEnumerable<int> decoys)
        {
            RoundObjective objective =
                RoundObjective.CreateOrdered(
                    rule.DisplayText,
                    correctOrder,
                    rule.FailImmediatelyOnMistake);

            return new BuildResult(
                objective,
                correctOrder,
                decoys);
        }

        private static BuildResult BuildPairMultipleResult(
            RuleDefinition rule,
            PairCandidate correct,
            PairCandidate decoy)
        {
            return BuildMultipleResult(
                rule,
                correct.ToArray(),
                decoy.IsValid
                    ? decoy.ToArray()
                    : Array.Empty<int>());
        }

        private static BuildResult BuildOrderedPairResult(
            RuleDefinition rule,
            PairCandidate correct,
            PairCandidate decoy,
            CardData[] cards)
        {
            return BuildOrderedResult(
                rule,
                OrderPairLowFirst(cards, correct),
                decoy.IsValid
                    ? decoy.ToArray()
                    : Array.Empty<int>());
        }

        private static CardData[] GenerateRandomHand(
            int cardCount,
            Random random)
        {
            cardCount =
                Math.Max(1, cardCount);

            var cards =
                new CardData[cardCount];

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                cards[index] = new CardData
                {
                    value = random.Next(2, 15),
                    suit = (Suit)random.Next(0, 4),
                    cursed = false
                };
            }

            return cards;
        }

        private static void PrepareDistinctValues(
            CardData[] cards,
            Random random)
        {
            if (cards.Length
                > MaximumCardValue - MinimumCardValue + 1)
            {
                throw new InvalidOperationException(
                    "Too many cards for distinct standard card values.");
            }

            HashSet<int> usedValues = new();
            List<int> badIndices = new();

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                int value = cards[index].value;

                bool valueIsValid =
                    value >= MinimumCardValue
                    && value <= MaximumCardValue;

                if (valueIsValid
                    && usedValues.Add(value))
                {
                    continue;
                }

                badIndices.Add(index);
            }

            List<int> available =
                GetAvailableValues(usedValues);

            Shuffle(available, random);

            for (int index = 0;
                 index < badIndices.Count;
                 index++)
            {
                cards[badIndices[index]].value =
                    available[index];
            }
        }

        private static void RandomizeDistinctValues(
            CardData[] cards,
            Random random)
        {
            List<int> values = new();

            for (int value = MinimumCardValue;
                 value <= MaximumCardValue;
                 value++)
            {
                values.Add(value);
            }

            Shuffle(values, random);

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                cards[index].value = values[index];
                cards[index].cursed = false;
            }
        }

        private static void RandomizeSuits(
            CardData[] cards,
            Random random)
        {
            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                cards[index].suit =
                    (Suit)random.Next(0, 4);
            }
        }

        private static List<int> GetAvailableValues(
            HashSet<int> usedValues)
        {
            List<int> values = new();

            for (int value = MinimumCardValue;
                 value <= MaximumCardValue;
                 value++)
            {
                if (!usedValues.Contains(value))
                    values.Add(value);
            }

            return values;
        }

        private static void EnsureColorCount(
            CardData[] cards,
            bool wantsRed,
            int minimumCount,
            Random random)
        {
            int currentCount =
                CountColor(cards, wantsRed);

            for (int index = 0;
                 index < cards.Length
                 && currentCount < minimumCount;
                 index++)
            {
                if (IsRed(cards[index]) == wantsRed)
                    continue;

                cards[index].suit =
                    PickSuitOfColor(
                        wantsRed,
                        random);

                currentCount++;
            }
        }

        private static int CountColor(
            CardData[] cards,
            bool wantsRed)
        {
            int count = 0;

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (IsRed(cards[index]) == wantsRed)
                    count++;
            }

            return count;
        }

        private static List<int> GetColorIndices(
            CardData[] cards,
            bool wantsRed)
        {
            List<int> result = new();

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (IsRed(cards[index]) == wantsRed)
                    result.Add(index);
            }

            return result;
        }

        private static bool IsRed(
            CardData card)
        {
            return card.suit == Suit.Hearts
                   || card.suit == Suit.Diamonds;
        }

        private static Suit PickSuitOfColor(
            bool wantsRed,
            Random random)
        {
            if (wantsRed)
            {
                return random.NextDouble() < 0.5
                    ? Suit.Hearts
                    : Suit.Diamonds;
            }

            return random.NextDouble() < 0.5
                ? Suit.Clubs
                : Suit.Spades;
        }

        private static Suit PickSuitOtherThan(
            Suit excludedSuit,
            Random random)
        {
            List<Suit> suits = new()
            {
                Suit.Clubs,
                Suit.Diamonds,
                Suit.Hearts,
                Suit.Spades
            };

            suits.Remove(excludedSuit);

            return suits[
                random.Next(0, suits.Count)];
        }

        private static List<int> GetIndicesSortedByValue(
            CardData[] cards,
            bool descending)
        {
            List<int> indices =
                new(cards.Length);

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                indices.Add(index);
            }

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

            return indices;
        }

        private static List<PairCandidate> GetAllPairs(
            CardData[] cards)
        {
            List<PairCandidate> pairs = new();

            for (int left = 0;
                 left < cards.Length - 1;
                 left++)
            {
                for (int right = left + 1;
                     right < cards.Length;
                     right++)
                {
                    pairs.Add(
                        new PairCandidate(
                            left,
                            right,
                            cards[left].value,
                            cards[right].value));
                }
            }

            return pairs;
        }

        private static List<TripleCandidate> GetAllTriples(
            CardData[] cards)
        {
            List<TripleCandidate> triples = new();

            for (int first = 0;
                 first < cards.Length - 2;
                 first++)
            {
                for (int second = first + 1;
                     second < cards.Length - 1;
                     second++)
                {
                    for (int third = second + 1;
                         third < cards.Length;
                         third++)
                    {
                        triples.Add(
                            new TripleCandidate(
                                first,
                                second,
                                third,
                                cards[first].value,
                                cards[second].value,
                                cards[third].value));
                    }
                }
            }

            return triples;
        }

        private static Dictionary<int, List<PairCandidate>>
            GroupPairsByTarget(
                List<PairCandidate> pairs,
                Func<PairCandidate, int> targetSelector,
                Func<int, bool> targetAllowed)
        {
            Dictionary<int, List<PairCandidate>> byTarget = new();

            foreach (PairCandidate pair in pairs)
            {
                int target =
                    targetSelector(pair);

                if (!targetAllowed(target))
                    continue;

                if (!byTarget.TryGetValue(
                        target,
                        out List<PairCandidate> list))
                {
                    list = new List<PairCandidate>();
                    byTarget[target] = list;
                }

                list.Add(pair);
            }

            return byTarget;
        }

        private static List<int> GetUsableTargets(
            Dictionary<int, List<PairCandidate>> byTarget,
            bool wantsCursedDecoy)
        {
            List<int> targets = new();

            foreach (KeyValuePair<int, List<PairCandidate>> entry
                     in byTarget)
            {
                if (entry.Value.Count == 0)
                    continue;

                if (!wantsCursedDecoy)
                {
                    if (entry.Value.Count == 1)
                        targets.Add(entry.Key);

                    continue;
                }

                for (int index = 0;
                     index < entry.Value.Count;
                     index++)
                {
                    if (FindFirstDisjointPair(
                            entry.Value,
                            entry.Value[index],
                            0).IsValid)
                    {
                        targets.Add(entry.Key);
                        break;
                    }
                }
            }

            return targets;
        }

        private static List<int> GetUsableTripleTargets(
            Dictionary<int, List<TripleCandidate>> byTarget,
            bool wantsCursedDecoy)
        {
            List<int> targets = new();

            foreach (KeyValuePair<int, List<TripleCandidate>> entry
                     in byTarget)
            {
                if (!wantsCursedDecoy)
                {
                    if (entry.Value.Count == 1)
                        targets.Add(entry.Key);

                    continue;
                }

                for (int index = 0;
                     index < entry.Value.Count;
                     index++)
                {
                    if (FindFirstDisjointTriple(
                            entry.Value,
                            entry.Value[index],
                            0).IsValid)
                    {
                        targets.Add(entry.Key);
                        break;
                    }
                }
            }

            return targets;
        }

        private static int CountPairsWithSum(
            List<PairCandidate> pairs,
            int sum)
        {
            int count = 0;

            foreach (PairCandidate pair in pairs)
            {
                if (pair.Sum == sum)
                    count++;
            }

            return count;
        }

        private static PairCandidate FindFirstDisjointPair(
            List<PairCandidate> pairs,
            PairCandidate correct,
            int startIndex)
        {
            for (int index = startIndex;
                 index < pairs.Count;
                 index++)
            {
                PairCandidate candidate =
                    pairs[index];

                if (candidate.Equals(correct))
                    continue;

                if (!candidate.SharesIndexWith(correct))
                    return candidate;
            }

            return PairCandidate.Invalid;
        }

        private static TripleCandidate FindFirstDisjointTriple(
            List<TripleCandidate> triples,
            TripleCandidate correct,
            int startIndex)
        {
            for (int index = startIndex;
                 index < triples.Count;
                 index++)
            {
                TripleCandidate candidate =
                    triples[index];

                if (candidate.Equals(correct))
                    continue;

                if (!candidate.SharesIndexWith(correct))
                    return candidate;
            }

            return TripleCandidate.Invalid;
        }

        private static int[] OrderPairLowFirst(
            CardData[] cards,
            PairCandidate pair)
        {
            if (cards[pair.FirstIndex].value
                <= cards[pair.SecondIndex].value)
            {
                return new[]
                {
                    pair.FirstIndex,
                    pair.SecondIndex
                };
            }

            return new[]
            {
                pair.SecondIndex,
                pair.FirstIndex
            };
        }

        private static List<int> GetNeighborIndices(
            List<int> sortedIndices,
            int centerPosition)
        {
            List<int> result = new();

            int before = centerPosition - 1;
            int after = centerPosition + 1;

            if (before >= 0)
                result.Add(sortedIndices[before]);

            if (after < sortedIndices.Count)
                result.Add(sortedIndices[after]);

            return result;
        }

        private static List<int> Slice(
            List<int> source,
            int startIndex,
            int count)
        {
            List<int> result = new();

            if (source == null)
                return result;

            for (int index = startIndex;
                 index < source.Count
                 && result.Count < count;
                 index++)
            {
                result.Add(source[index]);
            }

            return result;
        }

        private static List<int> FilterOutCorrectIndices(
            IEnumerable<int> source,
            HashSet<int> correctSet)
        {
            List<int> result = new();

            if (source == null)
                return result;

            foreach (int index in source)
            {
                if (index < 0)
                    continue;

                if (correctSet.Contains(index))
                    continue;

                if (!result.Contains(index))
                    result.Add(index);
            }

            return result;
        }

        private static List<int> GetNonAnswerIndices(
            int cardCount,
            HashSet<int> answerIndices)
        {
            List<int> result = new();

            for (int index = 0;
                 index < cardCount;
                 index++)
            {
                if (!answerIndices.Contains(index))
                    result.Add(index);
            }

            return result;
        }

        private static void MarkCardCursed(
            CardData[] cards,
            int index)
        {
            if (index < 0 || index >= cards.Length)
                return;

            CardData card = cards[index];
            card.cursed = true;
            cards[index] = card;
        }

        private static void Shuffle<T>(
            IList<T> list,
            Random random)
        {
            for (int index = list.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex =
                    random.Next(0, index + 1);

                (list[index], list[swapIndex]) =
                    (list[swapIndex], list[index]);
            }
        }

        private sealed class BuildResult
        {
            public RoundObjective Objective { get; }

            public IReadOnlyCollection<int> CorrectIndices { get; }

            public IReadOnlyCollection<int> DecoyIndices { get; }

            public BuildResult(
                RoundObjective objective,
                IEnumerable<int> correctIndices,
                IEnumerable<int> decoyIndices)
            {
                Objective = objective;

                CorrectIndices =
                    new HashSet<int>(
                        correctIndices
                        ?? Array.Empty<int>());

                HashSet<int> correctSet =
                    new(CorrectIndices);

                List<int> cleanedDecoys =
                    FilterOutCorrectIndices(
                        decoyIndices,
                        correctSet);

                DecoyIndices =
                    new HashSet<int>(cleanedDecoys);
            }
        }

        private readonly struct PairCandidate
        {
            public int FirstIndex { get; }

            public int SecondIndex { get; }

            public int FirstValue { get; }

            public int SecondValue { get; }

            public int Sum =>
                FirstValue + SecondValue;

            public int Difference =>
                Math.Abs(
                    FirstValue - SecondValue);

            public bool IsValid =>
                FirstIndex >= 0
                && SecondIndex >= 0;

            public PairCandidate(
                int firstIndex,
                int secondIndex,
                int firstValue,
                int secondValue)
            {
                FirstIndex = firstIndex;
                SecondIndex = secondIndex;
                FirstValue = firstValue;
                SecondValue = secondValue;
            }

            public bool SharesIndexWith(
                PairCandidate other)
            {
                return FirstIndex == other.FirstIndex
                       || FirstIndex == other.SecondIndex
                       || SecondIndex == other.FirstIndex
                       || SecondIndex == other.SecondIndex;
            }

            public int[] ToArray()
            {
                return new[]
                {
                    FirstIndex,
                    SecondIndex
                };
            }

            public static PairCandidate Invalid =>
                new(-1, -1, 0, 0);
        }

        private readonly struct TripleCandidate
        {
            public int FirstIndex { get; }

            public int SecondIndex { get; }

            public int ThirdIndex { get; }

            public int Sum { get; }

            public bool IsValid =>
                FirstIndex >= 0
                && SecondIndex >= 0
                && ThirdIndex >= 0;

            public TripleCandidate(
                int firstIndex,
                int secondIndex,
                int thirdIndex,
                int firstValue,
                int secondValue,
                int thirdValue)
            {
                FirstIndex = firstIndex;
                SecondIndex = secondIndex;
                ThirdIndex = thirdIndex;
                Sum = firstValue + secondValue + thirdValue;
            }

            public bool SharesIndexWith(
                TripleCandidate other)
            {
                return FirstIndex == other.FirstIndex
                       || FirstIndex == other.SecondIndex
                       || FirstIndex == other.ThirdIndex
                       || SecondIndex == other.FirstIndex
                       || SecondIndex == other.SecondIndex
                       || SecondIndex == other.ThirdIndex
                       || ThirdIndex == other.FirstIndex
                       || ThirdIndex == other.SecondIndex
                       || ThirdIndex == other.ThirdIndex;
            }

            public int[] ToArray()
            {
                return new[]
                {
                    FirstIndex,
                    SecondIndex,
                    ThirdIndex
                };
            }

            public static TripleCandidate Invalid =>
                new(-1, -1, -1, 0, 0, 0);
        }
    }
}