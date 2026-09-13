using System;
using ClockworkCasino.Cards;
using ClockworkCasino.Gameplay;

namespace ClockworkCasino.Rules
{
    [Serializable]
    public sealed class RuleDefinition
    {
        public RuleType Type;

        public RuleFamily Family;

        public string DisplayText;

        public Suit AvoidSuit;

        public ColorFilter Color;

        public int RequiredCount = 1;

        public int TargetValue;

        public int DifferenceValue;

        public CurseMode CurseMode =
            CurseMode.None;

        public float CurseProbability;

        public SelectionMode SelectionMode =
            SelectionMode.Single;

        public bool AllowDeselection = true;

        public bool FailImmediatelyOnMistake = true;

        public static RuleDefinition Highest(
            CurseMode curse = CurseMode.None)
        {
            return CreateSingle(
                RuleType.Highest,
                RuleFamily.LegacyHighest,
                "Pick the Highest",
                curse);
        }

        public static RuleDefinition Lowest(
            CurseMode curse = CurseMode.None)
        {
            return CreateSingle(
                RuleType.Lowest,
                RuleFamily.LegacyLowest,
                "Pick the Lowest",
                curse);
        }

        public static RuleDefinition SecondHighest(
            CurseMode curse = CurseMode.None)
        {
            return CreateSingle(
                RuleType.SecondHighest,
                RuleFamily.LegacySecondHighest,
                "Pick the Second Highest",
                curse);
        }

        public static RuleDefinition SecondLowest(
            CurseMode curse = CurseMode.None)
        {
            return CreateSingle(
                RuleType.SecondLowest,
                RuleFamily.LegacySecondLowest,
                "Pick the Second Lowest",
                curse);
        }

        public static RuleDefinition PickRed(
            CurseMode curse = CurseMode.None)
        {
            RuleDefinition rule =
                CreateSingle(
                    RuleType.PickColor,
                    RuleFamily.LegacyHighestColor,
                    "Pick the Highest RED",
                    curse);

            rule.Color = ColorFilter.Red;
            return rule;
        }

        public static RuleDefinition PickBlack(
            CurseMode curse = CurseMode.None)
        {
            RuleDefinition rule =
                CreateSingle(
                    RuleType.PickColor,
                    RuleFamily.LegacyHighestColor,
                    "Pick the Highest BLACK",
                    curse);

            rule.Color = ColorFilter.Black;
            return rule;
        }

        public static RuleDefinition Avoid(
            Suit suit,
            CurseMode curse = CurseMode.None)
        {
            RuleDefinition rule =
                CreateSingle(
                    RuleType.AvoidSuit,
                    RuleFamily.LegacyAvoidSuit,
                    $"Avoid {suit} — Pick Highest",
                    curse);

            rule.AvoidSuit = suit;
            return rule;
        }

        public static RuleDefinition MiddleValue()
        {
            return CreateSingle(
                RuleType.MiddleValue,
                RuleFamily.MiddleValue,
                "Pick the Middle Value");
        }

        public static RuleDefinition HighestRed()
        {
            RuleDefinition rule =
                CreateSingle(
                    RuleType.HighestColor,
                    RuleFamily.HighestColor,
                    "Pick the Highest RED");

            rule.Color = ColorFilter.Red;
            return rule;
        }

        public static RuleDefinition HighestBlack()
        {
            RuleDefinition rule =
                CreateSingle(
                    RuleType.HighestColor,
                    RuleFamily.HighestColor,
                    "Pick the Highest BLACK");

            rule.Color = ColorFilter.Black;
            return rule;
        }

        public static RuleDefinition LowestRed()
        {
            RuleDefinition rule =
                CreateSingle(
                    RuleType.LowestColor,
                    RuleFamily.LowestColor,
                    "Pick the Lowest RED");

            rule.Color = ColorFilter.Red;
            return rule;
        }

        public static RuleDefinition LowestBlack()
        {
            RuleDefinition rule =
                CreateSingle(
                    RuleType.LowestColor,
                    RuleFamily.LowestColor,
                    "Pick the Lowest BLACK");

            rule.Color = ColorFilter.Black;
            return rule;
        }

        public static RuleDefinition TwoHighest()
        {
            return CreateMultiple(
                RuleType.TopN,
                RuleFamily.TopN,
                "Pick the Two Highest",
                requiredCount: 2);
        }

        public static RuleDefinition TwoLowest()
        {
            return CreateMultiple(
                RuleType.BottomN,
                RuleFamily.BottomN,
                "Pick the Two Lowest",
                requiredCount: 2);
        }

        public static RuleDefinition TwoClosestValues()
        {
            return CreateMultiple(
                RuleType.TwoClosestValues,
                RuleFamily.TwoClosestValues,
                "Pick the Two Closest Values",
                requiredCount: 2);
        }

        public static RuleDefinition TwoFarthestValues()
        {
            return CreateMultiple(
                RuleType.TwoFarthestValues,
                RuleFamily.TwoFarthestValues,
                "Pick the Two Farthest Values",
                requiredCount: 2);
        }

        public static RuleDefinition PairSum()
        {
            return CreateMultiple(
                RuleType.PairSum,
                RuleFamily.PairSum,
                "Pick Two Cards That Add Up",
                requiredCount: 2);
        }

        public static RuleDefinition PairDifference()
        {
            return CreateMultiple(
                RuleType.PairDifference,
                RuleFamily.PairDifference,
                "Pick Two Cards with a Difference",
                requiredCount: 2);
        }

        public static RuleDefinition SameSuitPair()
        {
            return CreateMultiple(
                RuleType.SameSuitPair,
                RuleFamily.SameSuitPair,
                "Pick the Highest Same-Suit Pair",
                requiredCount: 2);
        }

        public static RuleDefinition ThreeCardSum()
        {
            return CreateMultiple(
                RuleType.ThreeCardSum,
                RuleFamily.ThreeCardSum,
                "Pick Three Cards That Add Up",
                requiredCount: 3);
        }

        public static RuleDefinition LowestThenHighest()
        {
            return CreateOrdered(
                RuleType.LowestThenHighest,
                RuleFamily.LowestThenHighest,
                "Pick the Lowest, then Highest",
                requiredCount: 2);
        }

        public static RuleDefinition HighestThenLowest()
        {
            return CreateOrdered(
                RuleType.HighestThenLowest,
                RuleFamily.HighestThenLowest,
                "Pick the Highest, then Lowest",
                requiredCount: 2);
        }

        public static RuleDefinition PairSumLowFirst()
        {
            return CreateOrdered(
                RuleType.PairSumLowFirst,
                RuleFamily.PairSumLowFirst,
                "Pick Sum Pair, Low First",
                requiredCount: 2);
        }

        public static RuleDefinition SameSuitPairLowFirst()
        {
            return CreateOrdered(
                RuleType.SameSuitPairLowFirst,
                RuleFamily.SameSuitPairLowFirst,
                "Pick Same Suit Pair, Low First",
                requiredCount: 2);
        }

        private static RuleDefinition CreateSingle(
            RuleType ruleType,
            RuleFamily family,
            string displayText,
            CurseMode curse = CurseMode.None)
        {
            return new RuleDefinition
            {
                Type = ruleType,
                Family = family,
                DisplayText = displayText,
                RequiredCount = 1,
                CurseMode = curse,
                CurseProbability =
                    curse == CurseMode.None
                        ? 0f
                        : 1f,
                SelectionMode = SelectionMode.Single,
                AllowDeselection = false,
                FailImmediatelyOnMistake = true
            };
        }

        private static RuleDefinition CreateMultiple(
            RuleType ruleType,
            RuleFamily family,
            string displayText,
            int requiredCount)
        {
            return new RuleDefinition
            {
                Type = ruleType,
                Family = family,
                DisplayText = displayText,
                RequiredCount = requiredCount,
                CurseMode = CurseMode.None,
                CurseProbability = 0f,
                SelectionMode = SelectionMode.Multiple,
                AllowDeselection = true,
                FailImmediatelyOnMistake = true
            };
        }

        private static RuleDefinition CreateOrdered(
            RuleType ruleType,
            RuleFamily family,
            string displayText,
            int requiredCount)
        {
            return new RuleDefinition
            {
                Type = ruleType,
                Family = family,
                DisplayText = displayText,
                RequiredCount = requiredCount,
                CurseMode = CurseMode.None,
                CurseProbability = 0f,
                SelectionMode = SelectionMode.Ordered,
                AllowDeselection = false,
                FailImmediatelyOnMistake = true
            };
        }
    }

    public enum RuleType
    {
        Highest,
        Lowest,
        SecondHighest,
        SecondLowest,
        PickColor,
        AvoidSuit,

        MiddleValue,
        HighestColor,
        LowestColor,

        TopN,
        BottomN,
        TwoClosestValues,
        TwoFarthestValues,

        PairSum,
        PairDifference,
        SameSuitPair,

        ThreeCardSum,

        LowestThenHighest,
        HighestThenLowest,
        PairSumLowFirst,
        SameSuitPairLowFirst,

        // Kept for old references/tests.
        MedianValue,
        TwoHighest,
        TwoLowest,
        AllRed,
        AllBlack,
        AscendingThree,
        DescendingThree,
        PairClosestToTarget,
        HighestPairUnderTarget,
        LowestPairOverTarget,
        HigherRedThenLowerBlack,
        HigherBlackThenLowerRed
    }

    public enum ColorFilter
    {
        Red,
        Black
    }

    public enum CurseMode
    {
        None,
        OneOfValids,
        HalfOfValids,
        AllValids
    }
}