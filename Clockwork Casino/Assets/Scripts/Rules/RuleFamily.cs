namespace ClockworkCasino.Rules
{
    public enum RuleFamily
    {
        LegacyHighest,
        LegacyLowest,
        LegacySecondHighest,
        LegacySecondLowest,
        LegacyHighestColor,
        LegacyAvoidSuit,

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
        SameSuitPairLowFirst
    }
}