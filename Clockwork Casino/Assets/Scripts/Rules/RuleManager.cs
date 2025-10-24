using UnityEngine;
using System;
using System.Collections.Generic;
using ClockworkCasino.Cards;

namespace ClockworkCasino.Rules
{
    public enum RoundDifficulty { Easy, Medium, Hard }
    public class RuleManager : MonoBehaviour
    {
        private ClockworkCasino.Core.GameConfig _config;
        private readonly System.Random _rng = new();

        [Header("Difficulty thresholds (by stake seconds)")]
        [SerializeField] private int _easyMaxStake = 4;  // stake <= this => Easy
        [SerializeField] private int _hardMinStake = 8;  // stake >= this => Hard (else Medium)

        void Awake()
        {
            var gm = FindFirstObjectByType<ClockworkCasino.Core.GameManager>();
            _config = gm ? gm.Config() : null;
        }

        private readonly List<Func<RuleDefinition>> _easyBase = new()
        {
            () => RuleDefinition.Highest(),
            () => RuleDefinition.Lowest(),
            () => RuleDefinition.PickRed(),
            () => RuleDefinition.PickBlack(),
        };

        private readonly List<Func<RuleDefinition>> _mediumBase = new()
        {
            () => RuleDefinition.SecondHighest(),
            () => RuleDefinition.SecondLowest(),
            () => RuleDefinition.Avoid(Suit.Spades),
            () => RuleDefinition.Avoid(Suit.Hearts),
        };

        private readonly List<Func<RuleDefinition>> _hardBase = new()
        {
            () => RuleDefinition.SecondHighest(),
            () => RuleDefinition.SecondLowest(),
            () => RuleDefinition.Avoid(Suit.Spades),
            () => RuleDefinition.Avoid(Suit.Hearts),
        };

        public RuleDefinition PickRuleForRound(int roundIndex, int stakeSeconds)
        {
            var pool = PoolForStake(stakeSeconds);
            var rule = PickRandom(pool);

            bool allowCursed = _config != null && roundIndex >= _config.minRoundForCurses;
            float prob = Mathf.Clamp01(_config ? _config.cursedRuleWeight : 0f);

            if (allowCursed && prob > 0f)
            {
                rule.CurseMode = PickCurseModeFor(stakeSeconds);
                rule.CurseProbability = prob;
            }
            else
            {
                rule.CurseMode = CurseMode.None;
                rule.CurseProbability = 0f;
            }

            return rule;
        }

        private List<Func<RuleDefinition>> PoolForStake(int stake)
        {
            if (stake <= _easyMaxStake) return _easyBase;
            if (stake >= _hardMinStake) return _hardBase;
            return _mediumBase;
        }

        private CurseMode PickCurseModeFor(int stake)
        {
            if (stake >= _hardMinStake)
            {
                double r = _rng.NextDouble();
                if (r < 0.10) return CurseMode.AllValids;
                if (r < 0.70) return CurseMode.HalfOfValids;
                return CurseMode.OneOfValids;
            }
            if (stake > _easyMaxStake)
            {
                return _rng.NextDouble() < 0.25 ? CurseMode.HalfOfValids : CurseMode.OneOfValids;
            }
            return CurseMode.OneOfValids;
        }

        private RuleDefinition PickRandom(List<Func<RuleDefinition>> pool)
        {
            if (pool == null || pool.Count == 0) return RuleDefinition.Highest();
            int i = _rng.Next(0, pool.Count);
            return pool[i]();
        }

        public RoundDifficulty GetDifficultyForStake(int stakeSeconds)
        {
            if (stakeSeconds <= _easyMaxStake) return RoundDifficulty.Easy;
            if (stakeSeconds >= _hardMinStake) return RoundDifficulty.Hard;
            return RoundDifficulty.Medium;
        }
    }
}
