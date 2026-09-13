using System.Collections.Generic;
using ClockworkCasino.Cards;
using UnityEngine;

namespace ClockworkCasino.Rules
{
    public static class RuleRuntime
    {
        private static readonly System.Random Random = new();

        public static HashSet<int> EvaluateAndApplyCurses(
            CardData[] cards,
            RuleDefinition rule)
        {
            if (cards == null
                || cards.Length == 0
                || rule == null)
            {
                return new HashSet<int>();
            }

            HashSet<int> initialValidIndices =
                GetInitialValidIndices(cards, rule);

            HashSet<int> cursedIndices =
                ApplyCurses(cards, initialValidIndices, rule);

            var finalCorrectIndices =
                new HashSet<int>(initialValidIndices);

            foreach (int cursedIndex in cursedIndices)
                finalCorrectIndices.Remove(cursedIndex);

            if (finalCorrectIndices.Count == 0)
            {
                AddFallbackCorrectIndices(
                    cards,
                    rule,
                    finalCorrectIndices);
            }

            return finalCorrectIndices;
        }

        public static void EnsureAtLeastOneValid(
            CardData[] cards,
            RuleDefinition rule,
            System.Random random)
        {
            if (cards == null
                || cards.Length == 0
                || rule == null
                || random == null)
            {
                return;
            }

            if (rule.Type == RuleType.SecondHighest
                || rule.Type == RuleType.SecondLowest)
            {
                EnsureAtLeastTwoDistinctValues(
                    cards,
                    random);

                return;
            }

            HashSet<int> validIndices =
                GetInitialValidIndices(
                    cards,
                    rule);

            if (validIndices.Count > 0)
                return;

            switch (rule.Type)
            {
                case RuleType.PickColor:
                    EnsureRequestedColorExists(
                        cards,
                        rule,
                        random);
                    break;

                case RuleType.AvoidSuit:
                    EnsureNonAvoidedSuitExists(
                        cards,
                        rule,
                        random);
                    break;
            }
        }

        private static HashSet<int> GetInitialValidIndices(
            CardData[] cards,
            RuleDefinition rule)
        {
            var result = new HashSet<int>();

            switch (rule.Type)
            {
                case RuleType.Highest:
                    AddIndicesWithHighestValue(
                        cards,
                        result);
                    break;

                case RuleType.Lowest:
                    AddIndicesWithLowestValue(
                        cards,
                        result);
                    break;

                case RuleType.SecondHighest:
                    AddIndicesWithSecondHighestValue(
                        cards,
                        result);
                    break;

                case RuleType.SecondLowest:
                    AddIndicesWithSecondLowestValue(
                        cards,
                        result);
                    break;

                case RuleType.PickColor:
                    AddHighestRequestedColorIndices(
                        cards,
                        rule.Color,
                        result);
                    break;

                case RuleType.AvoidSuit:
                    AddHighestNonAvoidedSuitIndices(
                        cards,
                        rule.AvoidSuit,
                        result);
                    break;
            }

            return result;
        }

        private static void AddIndicesWithHighestValue(
            CardData[] cards,
            HashSet<int> result)
        {
            int highestValue = int.MinValue;

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                highestValue = Mathf.Max(
                    highestValue,
                    cards[index].value);
            }

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].value == highestValue)
                    result.Add(index);
            }
        }

        private static void AddIndicesWithLowestValue(
            CardData[] cards,
            HashSet<int> result)
        {
            int lowestValue = int.MaxValue;

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                lowestValue = Mathf.Min(
                    lowestValue,
                    cards[index].value);
            }

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].value == lowestValue)
                    result.Add(index);
            }
        }

        private static void AddIndicesWithSecondHighestValue(
            CardData[] cards,
            HashSet<int> result)
        {
            List<int> values =
                GetDistinctValues(cards);

            values.Sort(
                (left, right) =>
                    right.CompareTo(left));

            if (values.Count < 2)
            {
                AddIndicesWithHighestValue(
                    cards,
                    result);

                return;
            }

            int targetValue = values[1];

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].value == targetValue)
                    result.Add(index);
            }
        }

        private static void AddIndicesWithSecondLowestValue(
            CardData[] cards,
            HashSet<int> result)
        {
            List<int> values =
                GetDistinctValues(cards);

            values.Sort();

            if (values.Count < 2)
            {
                AddIndicesWithLowestValue(
                    cards,
                    result);

                return;
            }

            int targetValue = values[1];

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].value == targetValue)
                    result.Add(index);
            }
        }

        private static void AddHighestRequestedColorIndices(
            CardData[] cards,
            ColorFilter requestedColor,
            HashSet<int> result)
        {
            bool wantsRed =
                requestedColor == ColorFilter.Red;

            int highestValue = int.MinValue;

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (!MatchesColor(cards[index], wantsRed))
                    continue;

                highestValue = Mathf.Max(
                    highestValue,
                    cards[index].value);
            }

            if (highestValue == int.MinValue)
                return;

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (MatchesColor(cards[index], wantsRed)
                    && cards[index].value == highestValue)
                {
                    result.Add(index);
                }
            }
        }

        private static void AddHighestNonAvoidedSuitIndices(
            CardData[] cards,
            Suit avoidedSuit,
            HashSet<int> result)
        {
            int highestValue = int.MinValue;

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].suit == avoidedSuit)
                    continue;

                highestValue = Mathf.Max(
                    highestValue,
                    cards[index].value);
            }

            if (highestValue == int.MinValue)
                return;

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                if (cards[index].suit != avoidedSuit
                    && cards[index].value == highestValue)
                {
                    result.Add(index);
                }
            }
        }

        private static HashSet<int> ApplyCurses(
            CardData[] cards,
            HashSet<int> initialValidIndices,
            RuleDefinition rule)
        {
            var cursedIndices = new HashSet<int>();

            if (rule.CurseMode == CurseMode.None)
                return cursedIndices;

            float curseProbability =
                Mathf.Clamp01(rule.CurseProbability);

            if (curseProbability <= 0f)
                return cursedIndices;

            if (curseProbability < 1f
                && Random.NextDouble() > curseProbability)
            {
                return cursedIndices;
            }

            var candidates =
                new List<int>(initialValidIndices);

            if (candidates.Count == 0)
                return cursedIndices;

            switch (rule.CurseMode)
            {
                case CurseMode.OneOfValids:
                    CurseOneValidCard(
                        cards,
                        candidates,
                        cursedIndices);
                    break;

                case CurseMode.HalfOfValids:
                    CurseHalfOfValidCards(
                        cards,
                        candidates,
                        cursedIndices);
                    break;

                case CurseMode.AllValids:
                    CurseAllButOneValidCard(
                        cards,
                        candidates,
                        cursedIndices);
                    break;
            }

            return cursedIndices;
        }

        private static void CurseOneValidCard(
            CardData[] cards,
            List<int> candidates,
            HashSet<int> cursedIndices)
        {
            if (candidates.Count <= 1)
                return;

            int selectedIndex =
                candidates[
                    Random.Next(
                        0,
                        candidates.Count)];

            MarkCardCursed(
                cards,
                selectedIndex);

            cursedIndices.Add(selectedIndex);
        }

        private static void CurseHalfOfValidCards(
            CardData[] cards,
            List<int> candidates,
            HashSet<int> cursedIndices)
        {
            if (candidates.Count <= 1)
                return;

            Shuffle(candidates);

            int curseCount = Mathf.Clamp(
                candidates.Count / 2,
                1,
                candidates.Count - 1);

            for (int index = 0;
                 index < curseCount;
                 index++)
            {
                int cardIndex =
                    candidates[index];

                MarkCardCursed(
                    cards,
                    cardIndex);

                cursedIndices.Add(cardIndex);
            }
        }

        private static void CurseAllButOneValidCard(
            CardData[] cards,
            List<int> candidates,
            HashSet<int> cursedIndices)
        {
            if (candidates.Count <= 1)
                return;

            Shuffle(candidates);

            // Intentionally leave one valid card uncursed so the round
            // never becomes impossible purely because of curse application.
            for (int index = 1;
                 index < candidates.Count;
                 index++)
            {
                int cardIndex =
                    candidates[index];

                MarkCardCursed(
                    cards,
                    cardIndex);

                cursedIndices.Add(cardIndex);
            }
        }

        private static void AddFallbackCorrectIndices(
            CardData[] cards,
            RuleDefinition rule,
            HashSet<int> result)
        {
            List<(int Index, int Value)> ranking =
                BuildEligibleRanking(
                    cards,
                    rule,
                    excludeCursed: true);

            if (ranking.Count == 0)
                return;

            int targetValue =
                ranking[0].Value;

            foreach ((int index, int value)
                     in ranking)
            {
                if (value != targetValue)
                    break;

                result.Add(index);
            }
        }

        private static List<(int Index, int Value)>
            BuildEligibleRanking(
                CardData[] cards,
                RuleDefinition rule,
                bool excludeCursed)
        {
            var ranking =
                new List<(int Index, int Value)>();

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                CardData card =
                    cards[index];

                if (excludeCursed && card.cursed)
                    continue;

                if (!IsInRuleDomain(card, rule))
                    continue;

                ranking.Add(
                    (index, card.value));
            }

            ranking.Sort(
                (left, right) =>
                    left.Value.CompareTo(
                        right.Value));

            if (IsHighestFamily(rule.Type))
                ranking.Reverse();

            return ranking;
        }

        private static bool IsInRuleDomain(
            CardData card,
            RuleDefinition rule)
        {
            switch (rule.Type)
            {
                case RuleType.PickColor:
                    {
                        bool wantsRed =
                            rule.Color == ColorFilter.Red;

                        return MatchesColor(
                            card,
                            wantsRed);
                    }

                case RuleType.AvoidSuit:
                    return card.suit != rule.AvoidSuit;

                default:
                    return true;
            }
        }

        private static bool IsHighestFamily(
            RuleType ruleType)
        {
            return ruleType == RuleType.Highest
                   || ruleType == RuleType.SecondHighest
                   || ruleType == RuleType.PickColor
                   || ruleType == RuleType.AvoidSuit;
        }

        private static bool MatchesColor(
            CardData card,
            bool wantsRed)
        {
            bool cardIsRed =
                card.suit == Suit.Hearts
                || card.suit == Suit.Diamonds;

            return cardIsRed == wantsRed;
        }

        private static List<int> GetDistinctValues(
            CardData[] cards)
        {
            var values =
                new HashSet<int>();

            foreach (CardData card in cards)
                values.Add(card.value);

            return new List<int>(values);
        }

        private static void EnsureAtLeastTwoDistinctValues(
            CardData[] cards,
            System.Random random)
        {
            if (GetDistinctValues(cards).Count >= 2)
                return;

            int selectedIndex =
                random.Next(0, cards.Length);

            int originalValue =
                cards[selectedIndex].value;

            int replacementValue;

            do
            {
                replacementValue =
                    random.Next(2, 15);
            }
            while (replacementValue
                   == originalValue);

            cards[selectedIndex].value =
                replacementValue;
        }

        private static void EnsureRequestedColorExists(
            CardData[] cards,
            RuleDefinition rule,
            System.Random random)
        {
            bool wantsRed =
                rule.Color == ColorFilter.Red;

            int selectedIndex =
                random.Next(0, cards.Length);

            cards[selectedIndex].suit =
                wantsRed
                    ? random.Next(0, 2) == 0
                        ? Suit.Hearts
                        : Suit.Diamonds
                    : random.Next(0, 2) == 0
                        ? Suit.Clubs
                        : Suit.Spades;
        }

        private static void EnsureNonAvoidedSuitExists(
            CardData[] cards,
            RuleDefinition rule,
            System.Random random)
        {
            int selectedIndex =
                random.Next(0, cards.Length);

            var availableSuits = new List<Suit>
            {
                Suit.Clubs,
                Suit.Diamonds,
                Suit.Hearts,
                Suit.Spades
            };

            availableSuits.Remove(
                rule.AvoidSuit);

            cards[selectedIndex].suit =
                availableSuits[
                    random.Next(
                        0,
                        availableSuits.Count)];
        }

        private static void MarkCardCursed(
            CardData[] cards,
            int index)
        {
            CardData card = cards[index];
            card.cursed = true;
            cards[index] = card;
        }

        private static void Shuffle<T>(
            IList<T> list)
        {
            for (int index = list.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex =
                    Random.Next(0, index + 1);

                (list[index], list[swapIndex]) =
                    (list[swapIndex], list[index]);
            }
        }
    }
}