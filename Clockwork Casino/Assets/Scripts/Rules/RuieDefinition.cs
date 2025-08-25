using System;
using ClockworkCasino.Cards;

namespace ClockworkCasino.Rules
{
    [Serializable]
    public class RuleDefinition
    {
        public RuleType Type;
        public string DisplayText;

        // Optional parameters (used by some rules)
        public Suit AvoidSuit;     // AvoidSuit
        public ColorFilter Color;  // PickRed / PickBlack

        // Cursing policy for this round
        public CurseMode CurseMode = CurseMode.None;
        public float CurseProbability = 0f; // 0..1 chance to apply the curse mode

        // Factories
        public static RuleDefinition Highest(CurseMode curse = CurseMode.None) =>
            new RuleDefinition { Type = RuleType.Highest, DisplayText = "Pick the Highest", CurseMode = curse };

        public static RuleDefinition Lowest(CurseMode curse = CurseMode.None) =>
            new RuleDefinition { Type = RuleType.Lowest, DisplayText = "Pick the Lowest", CurseMode = curse };

        public static RuleDefinition SecondHighest(CurseMode curse = CurseMode.None) =>
            new RuleDefinition { Type = RuleType.SecondHighest, DisplayText = "Pick the Second Highest", CurseMode = curse };

        public static RuleDefinition SecondLowest(CurseMode curse = CurseMode.None) =>
            new RuleDefinition { Type = RuleType.SecondLowest, DisplayText = "Pick the Second Lowest", CurseMode = curse };

        public static RuleDefinition PickRed(CurseMode curse = CurseMode.None) =>
            new RuleDefinition { Type = RuleType.PickColor, Color = ColorFilter.Red, DisplayText = "Pick a RED (highest wins)", CurseMode = curse };

        public static RuleDefinition PickBlack(CurseMode curse = CurseMode.None) =>
            new RuleDefinition { Type = RuleType.PickColor, Color = ColorFilter.Black, DisplayText = "Pick a BLACK (highest wins)", CurseMode = curse };

        public static RuleDefinition Avoid(Suit s, CurseMode curse = CurseMode.None) =>
            new RuleDefinition { Type = RuleType.AvoidSuit, AvoidSuit = s, DisplayText = $"Avoid {s} (pick highest among others)", CurseMode = curse };
    }

    public enum RuleType
    {
        Highest,
        Lowest,
        SecondHighest,
        SecondLowest,
        PickColor,
        AvoidSuit
    }

    public enum ColorFilter { Red, Black }

    public enum CurseMode
    {
        None,
        OneOfValids,   // randomly curse one of the currently valid indices
        HalfOfValids,  // curse about half of valids (rounded down)
        AllValids      // curse all valids; engine falls back to next best non-cursed
    }
}
