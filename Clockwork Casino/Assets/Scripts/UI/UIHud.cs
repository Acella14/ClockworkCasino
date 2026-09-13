using System;
using ClockworkCasino.Audio;
using ClockworkCasino.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ClockworkCasino.UI
{
    public sealed class UIHud : MonoBehaviour
    {
        [Header("Dependencies")]
        [FormerlySerializedAs("_soundEffects")]
        [SerializeField]
        private SfxPlayer _soundEffects;

        [Header("Primary Gameplay Information")]
        [FormerlySerializedAs("_tomorrowText")]
        [FormerlySerializedAs("_timeBankText")]
        [SerializeField]
        private TMP_Text _timeBankText;

        [FormerlySerializedAs("_tableText")]
        [FormerlySerializedAs("_difficultyText")]
        [SerializeField]
        private TMP_Text _difficultyText;

        [FormerlySerializedAs("_ruleText")]
        [SerializeField]
        private TMP_Text _ruleText;

        [FormerlySerializedAs("_stakeText")]
        [SerializeField]
        private TMP_Text _wagerText;

        [FormerlySerializedAs("_nextWinText")]
        [SerializeField]
        private TMP_Text _possibleWinningsText;

        [Header("Transient Message")]
        [FormerlySerializedAs("_messageText")]
        [SerializeField]
        private TMP_Text _messageText;

        [Header("Intermission")]
        [FormerlySerializedAs("_intermissionPanel")]
        [SerializeField]
        private GameObject _intermissionPanel;

        [FormerlySerializedAs("_continueButton")]
        [SerializeField]
        private Button _continueButton;

        [FormerlySerializedAs("_cashOutButton")]
        [SerializeField]
        private Button _leaveButton;

        [FormerlySerializedAs("_riskButton")]
        [SerializeField]
        private Button _riskButton;

        [Header("Round Clock")]
        [FormerlySerializedAs("_roundClockFill")]
        [SerializeField]
        private Image _roundClockFill;

        private Action _continueRequested;
        private Action _leaveRequested;
        private Action _riskRequested;

        private bool _roundClockFrozen;

        private void Awake()
        {
            ShowRoundClock(false);
            SetRoundProgress(0f);
        }

        private void OnDestroy()
        {
            ClearControlBindings();
        }

        public void InitializeControls(
            Action continueRequested,
            Action leaveRequested,
            Action riskRequested)
        {
            ClearControlBindings();

            _continueRequested = continueRequested;
            _leaveRequested = leaveRequested;
            _riskRequested = riskRequested;

            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(
                    HandleContinueClicked);
            }

            if (_leaveButton != null)
            {
                _leaveButton.onClick.AddListener(
                    HandleLeaveClicked);
            }

            if (_riskButton != null)
            {
                _riskButton.onClick.AddListener(
                    HandleRiskClicked);

                _riskButton.gameObject.SetActive(
                    _riskRequested != null);
            }
        }

        public void ClearControlBindings()
        {
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(
                    HandleContinueClicked);
            }

            if (_leaveButton != null)
            {
                _leaveButton.onClick.RemoveListener(
                    HandleLeaveClicked);
            }

            if (_riskButton != null)
            {
                _riskButton.onClick.RemoveListener(
                    HandleRiskClicked);
            }

            _continueRequested = null;
            _leaveRequested = null;
            _riskRequested = null;
        }

        public void SetTimeBank(int remainingHours)
        {
            if (_timeBankText != null)
            {
                _timeBankText.text =
                    PlayerProgress.FormatHours(remainingHours);
            }
        }

        public void SetDifficulty(string difficultyText)
        {
            if (_difficultyText != null)
            {
                _difficultyText.text =
                    difficultyText ?? string.Empty;
            }
        }

        public void SetRule(string ruleText)
        {
            if (_ruleText != null)
            {
                _ruleText.text =
                    ruleText ?? string.Empty;
            }
        }

        public void SetWager(int wagerHours)
        {
            if (_wagerText == null)
                return;

            _wagerText.text = wagerHours > 0
                ? $"{wagerHours}H"
                : string.Empty;
        }

        public void SetPossibleWinnings(int winningsHours)
        {
            if (_possibleWinningsText == null)
                return;

            _possibleWinningsText.text = winningsHours > 0
                ? $"{winningsHours}H"
                : string.Empty;
        }

        public void ShowRoundEconomy(
            int wagerHours,
            int winningsHours)
        {
            SetWager(wagerHours);
            SetPossibleWinnings(winningsHours);
        }

        public void HideRoundEconomy()
        {
            SetWager(0);
            SetPossibleWinnings(0);
        }

        public void SetMessage(string message)
        {
            if (_messageText != null)
            {
                _messageText.text =
                    message ?? string.Empty;
            }
        }

        public void ShowRoundResult(
            bool wasCorrect,
            int winningsHours)
        {
            SetMessage(
                wasCorrect
                    ? $"WIN — +{winningsHours}H"
                    : "LOSS — the house keeps the bet.");
        }

        public void ShowRiskSuccess(int rewardHours)
        {
            SetMessage($"RISK WON — +{rewardHours}H");
        }

        public void ShowRedemptionSuccess(
            int rewardHours,
            int cooldownRounds)
        {
            SetMessage(
                $"REDEEMED — +{rewardHours}H\n"
                + $"Next redemption in "
                + $"{cooldownRounds} rounds.");
        }

        public void FreezeRoundClock()
        {
            _roundClockFrozen = true;
        }

        public void UnfreezeRoundClock()
        {
            _roundClockFrozen = false;
        }

        public void SetRoundClock(
            float remainingSeconds,
            float totalSeconds)
        {
            if (_roundClockFrozen)
                return;

            totalSeconds = Mathf.Max(
                0.0001f,
                totalSeconds);

            remainingSeconds = Mathf.Max(
                0f,
                remainingSeconds);

            SetRoundProgress(
                remainingSeconds / totalSeconds);
        }

        public void SetRoundProgress(float normalizedProgress)
        {
            if (_roundClockFill != null)
            {
                _roundClockFill.fillAmount =
                    Mathf.Clamp01(normalizedProgress);
            }
        }

        public void ShowRoundClock(bool isVisible)
        {
            if (_roundClockFill != null)
            {
                _roundClockFill.gameObject.SetActive(
                    isVisible);
            }
        }

        public void ShowRoundClockFull()
        {
            ShowRoundClock(true);
            SetRoundProgress(1f);
        }

        public void SetIntermissionVisible(bool isVisible)
        {
            if (_intermissionPanel != null)
            {
                _intermissionPanel.SetActive(isVisible);
            }
        }

        public void SetIntermissionActions(
            bool canContinue,
            bool canLeave,
            bool canStartRisk)
        {
            if (_continueButton != null)
                _continueButton.interactable = canContinue;

            if (_leaveButton != null)
                _leaveButton.interactable = canLeave;

            if (_riskButton != null)
                _riskButton.interactable = canStartRisk;
        }

        public void ShowEndScreen(
            bool died,
            int remainingHours,
            string reason)
        {
            if (died)
            {
                SetMessage(
                    $"{reason}\n"
                    + "You begin again with "
                    + PlayerProgress.FormatHours(
                        remainingHours)
                    + ".");
            }
            else
            {
                SetMessage(
                    "You leave the casino.\n"
                    + "Time remaining: "
                    + PlayerProgress.FormatHours(
                        remainingHours));
            }

            SetWager(0);
            SetPossibleWinnings(0);

            SetIntermissionVisible(true);
            SetIntermissionActions(false, false, false);
        }

        private void HandleContinueClicked()
        {
            _soundEffects?.Play(SfxEvent.UiClick);
            _continueRequested?.Invoke();
        }

        private void HandleLeaveClicked()
        {
            _soundEffects?.Play(SfxEvent.UiClick);
            _leaveRequested?.Invoke();
        }

        private void HandleRiskClicked()
        {
            _soundEffects?.Play(SfxEvent.UiClick);
            _riskRequested?.Invoke();
        }
    }
}