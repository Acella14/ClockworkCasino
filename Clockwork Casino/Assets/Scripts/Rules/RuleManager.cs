using UnityEngine;
using System;
using System.Collections.Generic;
using ClockworkCasino.Cards;

namespace ClockworkCasino.Rules
{
    public class RuleManager : MonoBehaviour
    {
        private ClockworkCasino.Core.GameConfig _config;
        System.Random _rng = new();

        void Awake()
        {
            var gm = FindFirstObjectByType<ClockworkCasino.Core.GameManager>();
            _config = gm ? gm.Config() : null;
        }

        // CLEAN variants (no curses)
        private readonly List<Func<RuleDefinition>> _clean = new()
        {
            () => RuleDefinition.Highest(),
            () => RuleDefinition.Lowest(),
            () => RuleDefinition.SecondHighest(),
            () => RuleDefinition.SecondLowest(),
            () => RuleDefinition.PickRed(),
            () => RuleDefinition.PickBlack(),
            () => RuleDefinition.Avoid(Suit.Spades),
            () => RuleDefinition.Avoid(Suit.Hearts),
        };

        // CURSED variants (same rules but with curses active)
        private readonly List<Func<RuleDefinition>> _cursed = new()
        {
            () => RuleDefinition.Highest(CurseMode.OneOfValids),
            () => RuleDefinition.Highest(CurseMode.HalfOfValids),
            () => RuleDefinition.Lowest(CurseMode.OneOfValids),
            () => RuleDefinition.SecondHighest(CurseMode.OneOfValids),
            () => RuleDefinition.SecondLowest(CurseMode.HalfOfValids),
            () => RuleDefinition.PickRed(CurseMode.OneOfValids),
            () => RuleDefinition.PickBlack(CurseMode.OneOfValids),
            () => RuleDefinition.Avoid(Suit.Spades, CurseMode.AllValids),
            () => RuleDefinition.Avoid(Suit.Hearts, CurseMode.OneOfValids),
        };

        public RuleDefinition PickRuleForRound(int roundIndex, int stakeSeconds)
        {
            bool allowCursed = _config != null && roundIndex >= _config.minRoundForCurses;
            if (!allowCursed) return PickRandom(_clean);

            float w = Mathf.Clamp01(_config.cursedRuleWeight);
            bool pickCursed = _rng.NextDouble() < w;

            var rule = pickCursed ? PickRandom(_cursed) : PickRandom(_clean);
            Debug.Log($"[RuleManager] round={roundIndex} allowCursed={allowCursed} w={w} pickCursed={pickCursed} -> {rule.Type}/{rule.CurseMode}");
            return rule;
        }

        private RuleDefinition PickRandom(List<Func<RuleDefinition>> pool)
        {
            if (pool == null || pool.Count == 0) return RuleDefinition.Highest();
            int i = _rng.Next(0, pool.Count);
            return pool[i]();
        }
    }
}
