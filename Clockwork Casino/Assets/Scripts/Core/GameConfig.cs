using UnityEngine;

namespace ClockworkCasino.Core
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ClockworkCasino/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Run")]
        [Min(1)] public int startTimerSeconds = 30;

        [Header("Round Flow")]
        [Range(0.25f, 5f)] public float rulePreviewSeconds = 3f;

        [Header("Intermissions")]
        [Min(2)] public int intermissionEveryNRounds = 5;
        [Range(0.5f, 10f)] public float intermissionWindowSeconds = 2.0f;

        [Header("Borrowing")]
        [Min(1)] public int borrowPacketSeconds = 10;
        [Min(0)] public int baseCreditSeconds = 10;
        [Tooltip("Credit grows by score * growth. E.g. 0.5 -> +1s limit per 2 score seconds.")]
        [Range(0f, 5f)] public float creditGrowthPerScore = 0.5f;
        [Tooltip("Can borrow at most once per round.")]
        public bool borrowOncePerRound = true;

        [Header("Tomorrow Scaling (UI only)")]
        [Tooltip("60 seconds of debt = 100% tomorrow by design.")]
        [Min(1)] public int secondsPerTomorrow = 60;

        [Header("Stake Progression (by round index)")]
        [Tooltip("Stake equals decision window. Define by round bands.")]
        public StakeBand[] stakeBands = new[]
        {
            new StakeBand(1, 3, 4),
            new StakeBand(4, 6, 5),
            new StakeBand(7, 9, 6),
            new StakeBand(10, 99, 7),
        };

        [Header("Cards")]
        [Range(3, 10)] public int startCardCount = 3;
        [Range(3, 10)] public int maxCardCount = 6;
        [Tooltip("Cards added temporarily on borrow (optional). 0 = off")]
        [Range(0, 3)] public int borrowSpikeExtraCards = 1;

        [Header("Curses")]
        [Min(1)] public int minRoundForCurses = 5;
        [Range(0f, 1f)] public float cursedRuleWeight = 0.5f; // 0 = never pick cursed; 1 = always pick cursed (after minRound)

    }

    [System.Serializable]
    public struct StakeBand
    {
        public int RoundMinInclusive;
        public int RoundMaxInclusive;
        public int StakeSeconds;

        public StakeBand(int min, int max, int stake)
        {
            RoundMinInclusive = min;
            RoundMaxInclusive = max;
            StakeSeconds = stake;
        }

        public bool Matches(int roundIndex)
            => roundIndex >= RoundMinInclusive && roundIndex <= RoundMaxInclusive;
    }
}
