using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
        [SerializeField] Button _riskButton;

        [Header("New: Timer Visuals")]
        [SerializeField] Image _globalTimeFill;
        [SerializeField] Image _roundRingFill;
        [SerializeField, Min(1)] int _globalTimeCapSeconds = 40;

        Coroutine _stakeAnim;

        bool _roundFrozen = false;

        public void FreezeRoundClock()  { _roundFrozen = true; }
        public void UnfreezeRoundClock(){ _roundFrozen = false; }


        void Awake()
        {
            if (_roundRingFill)
            {
                _roundRingFill.gameObject.SetActive(false);
                _roundRingFill.fillAmount = 0f;
            }
        }

        public void ShowRoundRing(bool show)
        {
            if (_roundRingFill) _roundRingFill.gameObject.SetActive(show);
        }

        public void ShowRoundFullForPreview(float totalSeconds)
        {
            ShowRoundRing(true);
            SetRoundRingNormalized(1f);
        }

        public void KillSpendAnimation()
        {
            if (_stakeAnim != null)
            {
                StopCoroutine(_stakeAnim);
                _stakeAnim = null;
            }
        }

        public void Bind(System.Action onContinue, System.Action onCashOut, System.Action onRisk)
        {
            if (_continueButton)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(() => onContinue?.Invoke());
            }

            if (_cashOutButton)
            {
                _cashOutButton.onClick.RemoveAllListeners();
                _cashOutButton.onClick.AddListener(() => onCashOut?.Invoke());
            }

            if (_riskButton)
            {
                _riskButton.onClick.RemoveAllListeners();
                if (onRisk != null)
                    _riskButton.onClick.AddListener(() => onRisk?.Invoke());
                _riskButton.gameObject.SetActive(true);
            }
        }

        // ==== Global time (bank) ====
        public void SetGlobalTimeBank(int seconds)
        {
            if (_timerText) _timerText.text = $"Time {seconds}s";
            if (_globalTimeFill)
            {
                float denom = Mathf.Max(1, _globalTimeCapSeconds);
                _globalTimeFill.fillAmount = Mathf.Clamp01(seconds / denom);
            }
        }

        public void AnimateSpendToRoundClock(int oldBank, int newBank, int stake, float animSeconds, System.Action onDone)
        {
            UnfreezeRoundClock();
            ShowRoundRing(true);
            SetRoundRingNormalized(0f);

            if (_stakeAnim != null) StopCoroutine(_stakeAnim);
            ShowRoundRing(true);
            SetRoundRingNormalized(0f);
            _stakeAnim = StartCoroutine(CoSpendToRoundClock(oldBank, newBank, stake, animSeconds, onDone));
        }

        IEnumerator CoSpendToRoundClock(int oldBank, int newBank, int stake, float animSeconds, System.Action onDone)
        {
            // set the conceptual “capacity” for the round (so 1.0 means stake seconds)
            SetRoundClock(remaining: stake, total: stake);
            SetRoundRingNormalized(0f);

            float t = 0f;
            while (t < animSeconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / animSeconds);

                int shownBank = Mathf.RoundToInt(Mathf.Lerp(oldBank, newBank, a));
                SetGlobalTimeBank(shownBank);

                SetRoundRingNormalized(a);
                yield return null;
            }

            SetGlobalTimeBank(newBank);
            SetRoundRingNormalized(1f);
            onDone?.Invoke();
        }


        // ==== Round clock (radial) ====
        public void SetRoundClock(float remaining, float total)
        {
            if (_roundFrozen) return;
            remaining = Mathf.Max(0f, remaining);
            total = Mathf.Max(0.0001f, total);
            SetRoundRingNormalized(remaining / total);
        }

        void SetRoundRingNormalized(float pct01)
        {
            if (_roundRingFill)
                _roundRingFill.fillAmount = Mathf.Clamp01(pct01);
        }

        // ==== Existing UI you already had ====
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
        public void SetRiskInteractable(bool can)     { if (_riskButton) _riskButton.interactable = can; }

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
            SetRiskInteractable(false);
        }
    }
}
