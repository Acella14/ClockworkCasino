using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkCasino.UI
{
    public class UIHud : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] TMP_Text _timerText;
        [SerializeField] TMP_Text _debtText;
        [SerializeField] TMP_Text _tomorrowPctText;
        [SerializeField] TMP_Text _scoreText;
        [SerializeField] TMP_Text _stakeText;
        [SerializeField] TMP_Text _ruleText;
        [SerializeField] TMP_Text _messageText;

        [Header("Panels / Buttons")]
        [SerializeField] GameObject _interRoundPanel;
        [SerializeField] Button _continueButton;
        [SerializeField] Button _cashOutButton;

        public void Bind(System.Action onContinue, System.Action onCashOut)
        {
            _continueButton.onClick.RemoveAllListeners();
            _continueButton.onClick.AddListener(() => onContinue?.Invoke());

            _cashOutButton.onClick.RemoveAllListeners();
            _cashOutButton.onClick.AddListener(() => onCashOut?.Invoke());
        }

        public void SetTimer(int seconds)             { if (_timerText) _timerText.text = $"Time {seconds}s"; }
        public void SetDebt(int debtSeconds, int tomorrowPct)
        {
            if (_debtText) _debtText.text = $"Debt {debtSeconds}s";
            if (_tomorrowPctText) _tomorrowPctText.text = $"Tomorrow {Mathf.Clamp(tomorrowPct,0,100)}% spent";
        }
        public void SetScore(int s)                   { if (_scoreText) _scoreText.text = $"Score {s}s"; }
        public void SetStake(int stakeS)              { if (_stakeText) _stakeText.text = stakeS > 0 ? $"Stake {stakeS}s" : ""; }
        public void ShowRule(string text)             { if (_ruleText) _ruleText.text = text; }
        public void ShowMessage(string text)          { if (_messageText) _messageText.text = text; }

        public void ShowInterRound(bool show)         { if (_interRoundPanel) _interRoundPanel.SetActive(show); }
        public void SetContinueInteractable(bool can) { if (_continueButton) _continueButton.interactable = can; }
        public void SetCashOutInteractable(bool can)  { if (_cashOutButton) _cashOutButton.interactable = can; }

        public void ShowResult(bool correct, int surplusToScore, int debtPaidOrAdded)
        {
            if (!_messageText) return;
            _messageText.text = correct
                ? (surplusToScore > 0 ? $"Cleared debt, +{surplusToScore}s to score" : "Paid debt")
                : $"Debt +{debtPaidOrAdded}s";
        }

        public void ShowBorrowed(int seconds, int remainingCredit)
        {
            if (_messageText) _messageText.text = $"Borrowed +{seconds}s (credit left {remainingCredit}s)";
        }

        public void ShowEndScreen(bool busted, int score, int debt)
        {
            if (_messageText) _messageText.text = busted
                ? $"BUST — Debt remained: {debt}s. Final Score: {score}s"
                : $"Clean finish. Final Score: {score}s";
            ShowInterRound(true);
            SetCashOutInteractable(false);
            SetContinueInteractable(false);
        }
    }
}
