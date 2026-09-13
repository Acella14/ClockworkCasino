using System.Collections.Generic;
using ClockworkCasino.Core;
using UnityEngine;

namespace ClockworkCasino.Rules
{
    public sealed class RuleManager : MonoBehaviour
    {
        private enum RuleId
        {
            Highest,
            Lowest,
            MiddleValue,
            HighestRed,
            HighestBlack,
            TwoHighest,
            TwoLowest,

            SecondHighest,
            SecondLowest,
            TwoClosestValues,
            TwoFarthestValues,
            PairSum,
            PairDifference,
            LowestRed,
            LowestBlack,

            SameSuitPair,
            LowestThenHighest,
            HighestThenLowest,

            ThreeCardSum,
            PairSumLowFirst,
            SameSuitPairLowFirst
        }

        private static readonly RuleId[] TableZeroRules =
        {
            RuleId.Highest,
            RuleId.Lowest,
            RuleId.MiddleValue,
            RuleId.HighestRed,
            RuleId.HighestBlack,
            RuleId.TwoHighest,
            RuleId.TwoLowest
        };

        private static readonly RuleId[] TableOneRules =
        {
            RuleId.SecondHighest,
            RuleId.SecondLowest,
            RuleId.TwoClosestValues,
            RuleId.TwoFarthestValues,
            RuleId.PairSum,
            RuleId.PairDifference,
            RuleId.LowestRed,
            RuleId.LowestBlack
        };

        private static readonly RuleId[] TableTwoRules =
        {
            RuleId.SameSuitPair,
            RuleId.LowestThenHighest,
            RuleId.HighestThenLowest,
            RuleId.TwoClosestValues,
            RuleId.TwoFarthestValues,
            RuleId.PairSum,
            RuleId.PairDifference
        };

        private static readonly RuleId[] FinalTableRules =
        {
            RuleId.SecondHighest,
            RuleId.SecondLowest,
            RuleId.TwoClosestValues,
            RuleId.TwoFarthestValues,
            RuleId.PairSum,
            RuleId.PairDifference,
            RuleId.LowestRed,
            RuleId.LowestBlack,

            RuleId.SameSuitPair,
            RuleId.LowestThenHighest,
            RuleId.HighestThenLowest,

            RuleId.ThreeCardSum,
            RuleId.PairSumLowFirst,
            RuleId.SameSuitPairLowFirst
        };

        [SerializeField]
        private GameConfig _config;

        private readonly System.Random _random = new();
        private readonly List<RuleId> _shuffleBag = new();

        private int _activeBagTableIndex = -1;
        private RuleId? _lastRule;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(RuleManager)} requires a GameConfig.",
                    this);
            }
        }

        public void ResetForNewRun()
        {
            _shuffleBag.Clear();
            _activeBagTableIndex = -1;
            _lastRule = null;
        }

        public RuleDefinition PickRuleForTable(
            int tableIndex)
        {
            if (_activeBagTableIndex != tableIndex
                || _shuffleBag.Count == 0)
            {
                RefillShuffleBag(tableIndex);
            }

            RuleId selectedRuleId =
                DrawNextRuleId();

            RuleDefinition rule =
                CreateRule(selectedRuleId);

            ApplyTableCursePolicy(
                rule,
                tableIndex);

            return rule;
        }

        private RuleId DrawNextRuleId()
        {
            int finalIndex =
                _shuffleBag.Count - 1;

            RuleId selectedRuleId =
                _shuffleBag[finalIndex];

            _shuffleBag.RemoveAt(finalIndex);
            _lastRule = selectedRuleId;

            return selectedRuleId;
        }

        private void RefillShuffleBag(
            int tableIndex)
        {
            _shuffleBag.Clear();
            _activeBagTableIndex = tableIndex;

            AddRules(
                GetRulesForTable(tableIndex));

            RemoveRulesThatDoNotFitTable(tableIndex);

            if (_shuffleBag.Count == 0)
                AddRules(TableZeroRules);

            Shuffle(_shuffleBag);
            PreventBoundaryRepeat();
        }

        private IEnumerable<RuleId> GetRulesForTable(
            int tableIndex)
        {
            if (tableIndex <= 0)
                return TableZeroRules;

            if (tableIndex == 1)
                return TableOneRules;

            if (tableIndex == 2)
                return TableTwoRules;

            return FinalTableRules;
        }

        private void RemoveRulesThatDoNotFitTable(
            int tableIndex)
        {
            if (_config == null)
                return;

            TableTier table =
                _config.GetTableTier(tableIndex);

            int cardCount =
                table.CardCount;

            _shuffleBag.RemoveAll(
                ruleId =>
                    ruleId == RuleId.ThreeCardSum
                    && cardCount < 3);
        }

        private void AddRules(
            IEnumerable<RuleId> rules)
        {
            foreach (RuleId rule in rules)
                _shuffleBag.Add(rule);
        }

        private void ApplyTableCursePolicy(
            RuleDefinition rule,
            int tableIndex)
        {
            if (rule == null || _config == null)
                return;

            TableTier table =
                _config.GetTableTier(tableIndex);

            if (table.CurseProbability <= 0f)
            {
                rule.CurseMode = CurseMode.None;
                rule.CurseProbability = 0f;
                return;
            }

            rule.CurseMode =
                PickCurseMode(tableIndex);

            rule.CurseProbability =
                table.CurseProbability;
        }

        private CurseMode PickCurseMode(
            int tableIndex)
        {
            if (tableIndex <= 1)
                return CurseMode.OneOfValids;

            if (tableIndex == 2)
            {
                return _random.NextDouble() < 0.8
                    ? CurseMode.OneOfValids
                    : CurseMode.HalfOfValids;
            }

            double roll = _random.NextDouble();

            if (roll < 0.15)
                return CurseMode.AllValids;

            if (roll < 0.60)
                return CurseMode.HalfOfValids;

            return CurseMode.OneOfValids;
        }

        private RuleDefinition CreateRule(
            RuleId ruleId)
        {
            return ruleId switch
            {
                RuleId.Highest =>
                    RuleDefinition.Highest(),

                RuleId.Lowest =>
                    RuleDefinition.Lowest(),

                RuleId.MiddleValue =>
                    RuleDefinition.MiddleValue(),

                RuleId.HighestRed =>
                    RuleDefinition.HighestRed(),

                RuleId.HighestBlack =>
                    RuleDefinition.HighestBlack(),

                RuleId.TwoHighest =>
                    RuleDefinition.TwoHighest(),

                RuleId.TwoLowest =>
                    RuleDefinition.TwoLowest(),

                RuleId.SecondHighest =>
                    RuleDefinition.SecondHighest(),

                RuleId.SecondLowest =>
                    RuleDefinition.SecondLowest(),

                RuleId.TwoClosestValues =>
                    RuleDefinition.TwoClosestValues(),

                RuleId.TwoFarthestValues =>
                    RuleDefinition.TwoFarthestValues(),

                RuleId.PairSum =>
                    RuleDefinition.PairSum(),

                RuleId.PairDifference =>
                    RuleDefinition.PairDifference(),

                RuleId.LowestRed =>
                    RuleDefinition.LowestRed(),

                RuleId.LowestBlack =>
                    RuleDefinition.LowestBlack(),

                RuleId.SameSuitPair =>
                    RuleDefinition.SameSuitPair(),

                RuleId.LowestThenHighest =>
                    RuleDefinition.LowestThenHighest(),

                RuleId.HighestThenLowest =>
                    RuleDefinition.HighestThenLowest(),

                RuleId.ThreeCardSum =>
                    RuleDefinition.ThreeCardSum(),

                RuleId.PairSumLowFirst =>
                    RuleDefinition.PairSumLowFirst(),

                RuleId.SameSuitPairLowFirst =>
                    RuleDefinition.SameSuitPairLowFirst(),

                _ =>
                    RuleDefinition.Highest()
            };
        }

        private void PreventBoundaryRepeat()
        {
            if (!_lastRule.HasValue
                || _shuffleBag.Count <= 1
                || _shuffleBag[_shuffleBag.Count - 1]
                != _lastRule.Value)
            {
                return;
            }

            int swapIndex =
                _random.Next(
                    0,
                    _shuffleBag.Count - 1);

            int lastIndex =
                _shuffleBag.Count - 1;

            RuleId temp =
                _shuffleBag[lastIndex];

            _shuffleBag[lastIndex] =
                _shuffleBag[swapIndex];

            _shuffleBag[swapIndex] =
                temp;
        }

        private void Shuffle<T>(
            IList<T> list)
        {
            for (int index = list.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex =
                    _random.Next(0, index + 1);

                T temp = list[index];
                list[index] = list[swapIndex];
                list[swapIndex] = temp;
            }
        }
    }
}