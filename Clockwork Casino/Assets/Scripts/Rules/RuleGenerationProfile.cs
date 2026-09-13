using System;
using ClockworkCasino.Rules;

namespace ClockworkCasino.Gameplay
{
    public readonly struct RuleGenerationProfile
    {
        public int CardCount { get; }

        public int TableIndex { get; }

        public float CurseProbability { get; }

        public CurseMode CurseMode { get; }

        public bool ShouldAttemptCurse =>
            CurseMode != CurseMode.None
            && CurseProbability > 0f;

        public RuleGenerationProfile(
            int cardCount,
            int tableIndex,
            float curseProbability,
            CurseMode curseMode)
        {
            CardCount =
                Math.Max(1, cardCount);

            TableIndex =
                Math.Max(0, tableIndex);

            CurseProbability =
                Clamp01(curseProbability);

            CurseMode = curseMode;
        }

        public bool RollForCurse(Random random)
        {
            if (!ShouldAttemptCurse || random == null)
                return false;

            return random.NextDouble()
                   <= CurseProbability;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
        }
    }
}