using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ClockworkCasino.Core
{
    [CreateAssetMenu(
        fileName = "GameConfig",
        menuName = "ClockworkCasino/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Persistent Life")]
        [FormerlySerializedAs("startTimerSeconds")]
        [FormerlySerializedAs("_startingTimeBankSeconds")]
        [SerializeField, Min(1)]
        private int _startingLifeHours = 24;

        [Header("Round Presentation")]
        [FormerlySerializedAs("_rulePreviewSeconds")]
        [SerializeField, Range(0.25f, 5f)]
        private float _rulePreviewSeconds = 2.25f;

        [FormerlySerializedAs("_buyInPreviewSeconds")]
        [FormerlySerializedAs("_stakePreviewSeconds")]
        [SerializeField, Range(0f, 3f)]
        private float _stakePreviewSeconds = 0.5f;

        [Header("Card Dealing")]
        [FormerlySerializedAs("_dealStaggerSeconds")]
        [SerializeField, Range(0f, 2f)]
        private float _dealStaggerSeconds = 0.1f;

        [FormerlySerializedAs("_dealTravelSeconds")]
        [SerializeField, Range(0.05f, 1f)]
        private float _dealTravelSeconds = 0.25f;

        [FormerlySerializedAs("_previewHalfSpreadWidth")]
        [SerializeField, Min(0f)]
        private float _previewHalfSpreadWidth = 40f;

        [Header("Selection Feedback")]
        [FormerlySerializedAs("_selectionRaiseSeconds")]
        [SerializeField, Range(0.05f, 0.6f)]
        private float _selectionRaiseSeconds = 0.12f;

        [FormerlySerializedAs("_selectionRaisePixels")]
        [SerializeField, Min(0f)]
        private float _selectionRaisePixels = 20f;

        [FormerlySerializedAs("_resultDisplaySeconds")]
        [SerializeField, Range(0.05f, 5f)]
        private float _resultDisplaySeconds = 1f;

        [Header("Table Progression")]
        [SerializeField]
        private TableTier[] _tableTiers =
        {
            new(
                "TABLE I — INVITATION",
                roundsBeforeIntermission: 4,
                stakeHours: 2,
                winHours: 3,
                decisionTimeSeconds: 5.5f,
                cardCount: 3,
                curseProbability: 0f),

            new(
                "TABLE II — THE HOOK",
                roundsBeforeIntermission: 4,
                stakeHours: 3,
                winHours: 5,
                decisionTimeSeconds: 5f,
                cardCount: 4,
                curseProbability: 0.15f),

            new(
                "TABLE III — THE TRAP",
                roundsBeforeIntermission: 4,
                stakeHours: 4,
                winHours: 7,
                decisionTimeSeconds: 4.5f,
                cardCount: 5,
                curseProbability: 0.3f),

            new(
                "TABLE IV — THE HOUSE",
                roundsBeforeIntermission: 4,
                stakeHours: 5,
                winHours: 10,
                decisionTimeSeconds: 4f,
                cardCount: 6,
                curseProbability: 0.45f)
        };

        [Header("Optional Risk Round")]
        [SerializeField, Min(1)]
        private int _riskRewardHours = 8;

        [Header("Redemption Round")]
        [Tooltip(
            "Normal rounds that must be completed after winning redemption " +
            "before another redemption opportunity becomes available.")]
        [SerializeField, Min(1)]
        private int _redemptionCooldownRounds = 5;

        [Tooltip(
            "Extra time awarded above the current table's required stake.")]
        [SerializeField, Min(0)]
        private int _redemptionBonusHours = 1;

        public int StartingLifeHours =>
            Mathf.Max(1, _startingLifeHours);

        public float RulePreviewSeconds =>
            Mathf.Max(0f, _rulePreviewSeconds);

        public float StakePreviewSeconds =>
            Mathf.Max(0f, _stakePreviewSeconds);

        public float DealStaggerSeconds =>
            Mathf.Max(0f, _dealStaggerSeconds);

        public float DealTravelSeconds =>
            Mathf.Max(0.01f, _dealTravelSeconds);

        public float PreviewHalfSpreadWidth =>
            Mathf.Max(0f, _previewHalfSpreadWidth);

        public float SelectionRaiseSeconds =>
            Mathf.Max(0f, _selectionRaiseSeconds);

        public float SelectionRaisePixels =>
            Mathf.Max(0f, _selectionRaisePixels);

        public float ResultDisplaySeconds =>
            Mathf.Max(0f, _resultDisplaySeconds);

        public int RiskRewardHours =>
            Mathf.Max(1, _riskRewardHours);

        public int RedemptionCooldownRounds =>
            Mathf.Max(1, _redemptionCooldownRounds);

        public int RedemptionBonusHours =>
            Mathf.Max(0, _redemptionBonusHours);

        public int GetRedemptionRewardHours(int requiredStakeHours)
        {
            return Mathf.Max(
                1,
                requiredStakeHours + RedemptionBonusHours);
        }

        public TableTier GetTableTier(int tableIndex)
        {
            if (_tableTiers == null || _tableTiers.Length == 0)
                return TableTier.CreateFallback();

            int clampedIndex = Mathf.Clamp(
                tableIndex,
                0,
                _tableTiers.Length - 1);

            return _tableTiers[clampedIndex];
        }

        public int GetNextTableIndex(int currentTableIndex)
        {
            if (_tableTiers == null || _tableTiers.Length == 0)
                return 0;

            return Mathf.Min(
                currentTableIndex + 1,
                _tableTiers.Length - 1);
        }

        private void OnValidate()
        {
            _startingLifeHours = Mathf.Max(
                1,
                _startingLifeHours);

            _riskRewardHours = Mathf.Max(
                1,
                _riskRewardHours);

            _redemptionCooldownRounds = Mathf.Max(
                1,
                _redemptionCooldownRounds);

            _redemptionBonusHours = Mathf.Max(
                0,
                _redemptionBonusHours);

            if (_tableTiers == null || _tableTiers.Length == 0)
            {
                _tableTiers = new[]
                {
                    TableTier.CreateFallback()
                };
            }
        }
    }

    [Serializable]
    public struct TableTier
    {
        [SerializeField]
        private string _displayName;

        [SerializeField, Min(1)]
        private int _roundsBeforeIntermission;

        [SerializeField, Min(1)]
        private int _stakeHours;

        [FormerlySerializedAs("_baseWinHours")]
        [SerializeField, Min(1)]
        private int _winHours;

        [SerializeField, Min(0.5f)]
        private float _decisionTimeSeconds;

        [SerializeField, Range(1, 10)]
        private int _cardCount;

        [SerializeField, Range(0f, 1f)]
        private float _curseProbability;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(_displayName)
                ? "TABLE"
                : _displayName;

        public int RoundsBeforeIntermission =>
            Mathf.Max(1, _roundsBeforeIntermission);

        public int StakeHours =>
            Mathf.Max(1, _stakeHours);

        public int WinHours =>
            Mathf.Max(1, _winHours);

        public float DecisionTimeSeconds =>
            Mathf.Max(0.5f, _decisionTimeSeconds);

        public int CardCount =>
            Mathf.Clamp(_cardCount, 1, 10);

        public float CurseProbability =>
            Mathf.Clamp01(_curseProbability);

        public TableTier(
            string displayName,
            int roundsBeforeIntermission,
            int stakeHours,
            int winHours,
            float decisionTimeSeconds,
            int cardCount,
            float curseProbability)
        {
            _displayName = displayName;
            _roundsBeforeIntermission = roundsBeforeIntermission;
            _stakeHours = stakeHours;
            _winHours = winHours;
            _decisionTimeSeconds = decisionTimeSeconds;
            _cardCount = cardCount;
            _curseProbability = curseProbability;
        }

        public static TableTier CreateFallback()
        {
            return new TableTier(
                "TABLE I — INVITATION",
                roundsBeforeIntermission: 4,
                stakeHours: 2,
                winHours: 3,
                decisionTimeSeconds: 5.5f,
                cardCount: 3,
                curseProbability: 0f);
        }
    }
}