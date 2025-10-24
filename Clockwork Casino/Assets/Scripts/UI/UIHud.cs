using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ClockworkCasino.Rules;

namespace ClockworkCasino.UI
{
    public class UIHud : MonoBehaviour
    {
        [SerializeField] private ClockworkCasino.Audio.SfxPlayer _sfxPlayer;

        [Header("Texts")]
        [SerializeField] TMP_Text _timerText;
        [SerializeField] TMP_Text _debtText;
        [SerializeField] TMP_Text _tomorrowPctText;
        [SerializeField] TMP_Text _scoreText;
        [SerializeField] TMP_Text _stakeText;
        [SerializeField] TMP_Text _ruleText;
        [SerializeField] TMP_Text _messageText;
        [SerializeField] TMP_Text _difficultyText;

        [Header("Difficulty Colors")]
        [SerializeField] Color _diffEasy   = new Color32( 90, 200, 120, 255);
        [SerializeField] Color _diffMedium = new Color32(255, 190,  70, 255);
        [SerializeField] Color _diffHard   = new Color32(255,  95,  95, 255);
        [SerializeField] Color _diffRisk   = new Color32(186,  85, 211, 255);

        [Header("Panels / Buttons")]
        [SerializeField] GameObject _interRoundPanel;
        [SerializeField] Button _continueButton;
        [SerializeField] Button _cashOutButton;
        [SerializeField] Button _riskButton;

        [Header("Options")]
        [SerializeField] UnityEngine.UI.Toggle _autoBorrowToggle;

        [Header("Danger UI")]
        [SerializeField] Color _debtNormalColor = Color.white;
        [SerializeField] Color _debtLowColor    = new Color(1f, 0.92f, 0.02f);
        [SerializeField] Color _debtHighColor   = new Color(1f, 0.55f, 0.00f);
        [SerializeField] Color _debtDangerColor = new Color(0.92f, 0.12f, 0.10f);

        [Header("Timer/Bar Visuals")]
        [SerializeField] Image _debtFill;
        [SerializeField] Image _roundRingFill;

        [Header("Chip Spend (visual)")]
        [SerializeField] RectTransform _chipHolder;
        [SerializeField] RectTransform _fxLayer;
        [SerializeField] RectTransform _roundTimerEntry;
        [SerializeField] RectTransform _roundTimerSink;

        [SerializeField] RectTransform _chipWhiteRef;
        [SerializeField] RectTransform _chipRedRef;
        [SerializeField] RectTransform _chipBlueRef;

        [SerializeField, Min(0f)] float _chipFlyToEntrySeconds = 0.25f;
        [SerializeField, Min(0f)] float _chipFlyToSinkSeconds = 0.20f;

        [SerializeField, Min(0f)] float _chipSpawnInterval = 0.08f;

        [SerializeField] GameObject _deductPopupPrefab;
        [SerializeField] RectTransform _deductSpawnAnchor;


        [Header("Risk FX")]
        [SerializeField] Volume _globalVolume;
        [SerializeField, Range(0f, 1f)] float _riskVignetteIntensity = 0.2f;
        [SerializeField, Min(0f)] float _vignetteFadeSeconds = 0.25f;

        [SerializeField] AudioSource _sfx;
        [SerializeField] AudioClip _heartbeatClip;
        [SerializeField, Min(0f)] float _heartbeatInterval = 0.6f;
        [SerializeField, Range(0f, 1f)] float _heartbeatVolume = 1f;

        Coroutine _vigCo;
        Coroutine _heartbeatCo;


        Coroutine _stakeAnim;

        bool _roundFrozen = false;

        public void FreezeRoundClock() { _roundFrozen = true; }
        public void UnfreezeRoundClock() { _roundFrozen = false; }


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

        public void SetDebtPercent(int pct)
        {
            pct = Mathf.Clamp(pct, 0, 100);
            // Single label: "Debt NN%"
            if (_tomorrowPctText) _tomorrowPctText.text = $"Debt {pct}%";
            if (_debtText) _debtText.text = "";
            if (_debtFill) _debtFill.fillAmount = pct / 100f;
        }

        public void SetScoreLife(int scoreSeconds, int secondsPerTomorrow)
        {
            if (_scoreText) _scoreText.text = $"Time Gained: {FormatLife(scoreSeconds, secondsPerTomorrow)}";
        }

        string FormatLife(int gameSeconds, int secondsPerTomorrow)
        {
            secondsPerTomorrow = Mathf.Max(1, secondsPerTomorrow);
            float totalMinutes = gameSeconds * (24f * 60f) / secondsPerTomorrow;
            int mins = Mathf.RoundToInt(totalMinutes);
            int hours = mins / 60;
            int rem = mins % 60;
            return $"{hours}h {rem}m";
        }

        public void Bind(System.Action onContinue, System.Action onCashOut, System.Action onRisk)
        {
            if (_continueButton)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(() => _sfxPlayer?.Play(ClockworkCasino.Audio.SfxEvent.UiClick));
                _continueButton.onClick.AddListener(() => onContinue?.Invoke());
            }

            if (_cashOutButton)
            {
                _cashOutButton.onClick.RemoveAllListeners();
                _cashOutButton.onClick.AddListener(() => _sfxPlayer?.Play(ClockworkCasino.Audio.SfxEvent.UiClick));
                _cashOutButton.onClick.AddListener(() => onCashOut?.Invoke());
            }

            if (_riskButton)
            {
                _riskButton.onClick.RemoveAllListeners();
                _riskButton.onClick.AddListener(() => _sfxPlayer?.Play(ClockworkCasino.Audio.SfxEvent.UiClick));
                if (onRisk != null)
                    _riskButton.onClick.AddListener(() => onRisk?.Invoke());
                _riskButton.gameObject.SetActive(true);
            }
        }

        // ==== Global time (bank) ====
        public void SetGlobalTimeBank(int seconds)
        {
            if (_timerText) _timerText.text = $"{seconds}";
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


        public void AnimateSpendWithChips(int oldBank, int newBank, int stake, System.Action onDone)
        {
            // Show ring empty and bank text at old value
            UnfreezeRoundClock();
            ShowRoundRing(true);
            SetRoundRingNormalized(0f);
            SetGlobalTimeBank(oldBank);

            StopCoroutineIfRunning(ref _stakeAnim);
            _stakeAnim = StartCoroutine(CoSpendWithChips(oldBank, newBank, stake, onDone));
        }

        void SpawnDeductPopup(int amount)
        {
            if (_deductPopupPrefab == null || _deductSpawnAnchor == null) return;

            var go = Instantiate(_deductPopupPrefab, _deductSpawnAnchor);
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }

            var tmp = go.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null) tmp.text = "-" + Mathf.Abs(amount);
        }

        void StopCoroutineIfRunning(ref Coroutine c)
        {
            if (c != null) { StopCoroutine(c); c = null; }
        }

        IEnumerator CoSpendWithChips(int oldBank, int newBank, int stake, System.Action onDone)
        {
            stake = Mathf.Max(0, stake);
            if (stake == 0)
            {
                SetGlobalTimeBank(newBank);
                SetRoundRingNormalized(1f);
                onDone?.Invoke();
                yield break;
            }

            // Break stake into chips (10s, then 5s, then 1s)
            List<(RectTransform src, int value)> plan = new();
            int remaining = stake;
            int tens = remaining / 10; remaining -= tens * 10;
            int fives = remaining / 5; remaining -= fives * 5;
            int ones = remaining;

            for (int i = 0; i < tens; i++) plan.Add((_chipBlueRef, 10));
            for (int i = 0; i < fives; i++) plan.Add((_chipRedRef, 5));
            for (int i = 0; i < ones; i++) plan.Add((_chipWhiteRef, 1));

            // Path setup (compute once)
            var canvas = _fxLayer.GetComponentInParent<Canvas>();
            var cam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

            Vector3 entryWorld = RectCenterWorld(_roundTimerEntry);
            Vector2 entryScreen = RectTransformUtility.WorldToScreenPoint(cam, entryWorld);
            Vector3 entryInFx = ScreenToWorldIn(_fxLayer, entryScreen, cam);

            Vector3 sinkWorld = RectCenterWorld(_roundTimerSink);
            Vector2 sinkScreen = RectTransformUtility.WorldToScreenPoint(cam, sinkWorld);
            Vector3 sinkInFx = ScreenToWorldIn(_fxLayer, sinkScreen, cam);

            var ctx = new ChipSpendContext
            {
                ShownBank = oldBank,
                SpentSoFar = 0,
                ChipsDone = 0,
                Stake = stake,
                EntryInFx = entryInFx,
                SinkInFx = sinkInFx,
                Cam = cam,
                FxLayer = _fxLayer
            };

            for (int i = 0; i < plan.Count; i++)
            {
                var (srcRef, value) = plan[i];
                StartCoroutine(CoOneChip(srcRef, value, i * _chipSpawnInterval, ctx));
            }

            yield return new WaitUntil(() => ctx.ChipsDone >= plan.Count);

            SetGlobalTimeBank(newBank);
            SetRoundRingNormalized(1f);
            onDone?.Invoke();
        }

        public void BindAutoBorrowToggle(System.Action<bool> onChanged)
        {
            if (_autoBorrowToggle == null) return;
            _autoBorrowToggle.onValueChanged.RemoveAllListeners();
            _autoBorrowToggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            _autoBorrowToggle.onValueChanged.AddListener(v =>
            {
                _sfxPlayer?.Play(ClockworkCasino.Audio.SfxEvent.UiToggle);
                onChanged?.Invoke(v);
            });
        }
        public void SetAutoBorrowUI(bool on)
        {
            if (_autoBorrowToggle != null) _autoBorrowToggle.isOn = on;
        }

        public void SetDebtMeter(int debtSeconds, int capSeconds, bool suddenDeath)
        {
            capSeconds = Mathf.Max(1, capSeconds);
            float pct = debtSeconds / (float)capSeconds;
            float pct01 = Mathf.Clamp01(pct);

            if (_debtFill)
            {
                _debtFill.fillAmount = pct01;

                if (suddenDeath)
                {
                    _debtFill.color = _debtDangerColor;
                }
                else
                {
                    float t = pct01 * pct01 * (3f - 2f * pct01);
                    _debtFill.color = Color.Lerp(_debtLowColor, _debtHighColor, t);
                }
            }

            if (_tomorrowPctText)
            {
                if (debtSeconds <= capSeconds)
                    _tomorrowPctText.text = $"{Mathf.Clamp(Mathf.RoundToInt(pct * 100f), 0, 100)}%";
                else
                    _tomorrowPctText.text = $"100% (+{debtSeconds - capSeconds}s)";
            }

            if (_debtText) _debtText.text = "";
        }


        public void SetSuddenDeathWarning(bool on)
        {
            if (_debtFill) _debtFill.color = on ? _debtDangerColor : _debtNormalColor;
            if (on) ShowMessage("SUDDEN DEATH — a wrong pick ends the run.");
        }

        public void SetDifficulty(RoundDifficulty diff)
        {
            if (!_difficultyText) return;

            switch (diff)
            {
                case RoundDifficulty.Easy:
                    _difficultyText.text  = "EASY";
                    _difficultyText.color = _diffEasy;
                    break;
                case RoundDifficulty.Medium:
                    _difficultyText.text  = "MEDIUM";
                    _difficultyText.color = _diffMedium;
                    break;
                case RoundDifficulty.Hard:
                    _difficultyText.text  = "HARD";
                    _difficultyText.color = _diffHard;
                    break;
            }
        }

        public void SetDifficultyRisk()
        {
            if (!_difficultyText) return;
            _difficultyText.text  = "RISK";
            _difficultyText.color = _diffRisk;
        }

        class ChipSpendContext
        {
            public int ShownBank;
            public int SpentSoFar;
            public int ChipsDone;
            public int Stake;
            public Vector3 EntryInFx;
            public Vector3 SinkInFx;
            public Camera Cam;
            public RectTransform FxLayer;
        }

        // One chip: wait spawnDelay -> fly ENTRY -> fly SINK -> update HUD
        IEnumerator CoOneChip(RectTransform srcRef, int value, float spawnDelay, ChipSpendContext ctx)
        {
            if (spawnDelay > 0f) yield return new WaitForSeconds(spawnDelay);

            Vector3 srcWorld = RectCenterWorld(srcRef);
            Vector2 srcScreen = RectTransformUtility.WorldToScreenPoint(ctx.Cam, srcWorld);
            Vector3 startInFx = ScreenToWorldIn(ctx.FxLayer, srcScreen, ctx.Cam);

            var clone = Instantiate(srcRef.gameObject, ctx.FxLayer).GetComponent<RectTransform>();
            clone.position = startInFx;
            clone.localRotation = Quaternion.identity;
            clone.localScale = Vector3.one;

            // Leg 1: to ENTRY
            if (_chipFlyToEntrySeconds > 0f)
            {
                float t1 = 0f;
                while (t1 < _chipFlyToEntrySeconds)
                {
                    t1 += Time.deltaTime;
                    float a1 = Mathf.Clamp01(t1 / _chipFlyToEntrySeconds);
                    float e1 = 1f - Mathf.Pow(1f - a1, 2f);
                    clone.position = Vector3.Lerp(startInFx, ctx.EntryInFx, e1);
                    yield return null;
                }
            }
            else clone.position = ctx.EntryInFx;

            // Leg 2: to SINK
            if (_chipFlyToSinkSeconds > 0f)
            {
                float t2 = 0f;
                while (t2 < _chipFlyToSinkSeconds)
                {
                    t2 += Time.deltaTime;
                    float a2 = Mathf.Clamp01(t2 / _chipFlyToSinkSeconds);
                    float e2 = 1f - Mathf.Pow(1f - a2, 2f);
                    clone.position = Vector3.Lerp(ctx.EntryInFx, ctx.SinkInFx, e2);
                    yield return null;
                }
            }
            else clone.position = ctx.SinkInFx;
            _sfxPlayer?.PlayVaried(ClockworkCasino.Audio.SfxEvent.ChipInsert, 1f, 0.07f, 0.015f);

            Destroy(clone.gameObject);

            ctx.SpentSoFar += value;
            float pct = Mathf.Clamp01(ctx.SpentSoFar / (float)ctx.Stake);
            SetRoundRingNormalized(pct);

            ctx.ShownBank -= value;
            SetGlobalTimeBank(ctx.ShownBank);

            SpawnDeductPopup(value);

            ctx.ChipsDone++;
        }





        Vector3 RectCenterWorld(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return (c[0] + c[2]) * 0.5f;
        }
        Vector3 ScreenToWorldIn(RectTransform target, Vector2 screenPt, Camera cam)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(target, screenPt, cam, out var world);
            return world;
        }

        public void SetRiskVignette(bool on)
        {
            if (_vigCo != null) StopCoroutine(_vigCo);
            _vigCo = StartCoroutine(CoVignette(on));
        }

        public void StartHeartbeatLoop()
        {
            StopHeartbeatLoop();
            if (_sfx != null && _heartbeatClip != null)
                _heartbeatCo = StartCoroutine(CoHeartbeatLoop());
        }

        public void StopHeartbeatLoop()
        {
            if (_heartbeatCo != null) { StopCoroutine(_heartbeatCo); _heartbeatCo = null; }
        }

        IEnumerator CoVignette(bool on)
        {
            if (_globalVolume == null || _globalVolume.profile == null) yield break;

            if (!_globalVolume.profile.TryGet<Vignette>(out var vig))
                vig = _globalVolume.profile.Add<Vignette>(true);

            float start = vig.intensity.value;
            float end = on ? _riskVignetteIntensity : 0f;
            float dur = Mathf.Max(0.0001f, _vignetteFadeSeconds);

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - a, 2f);
                vig.intensity.Override(Mathf.Lerp(start, end, e));
                yield return null;
            }
            vig.intensity.Override(end);
        }

        IEnumerator CoHeartbeatLoop()
        {
            while (true)
            {
                _sfx.PlayOneShot(_heartbeatClip, _heartbeatVolume);
                yield return new WaitForSeconds(_heartbeatInterval);
            }
        }



        public void SetDebt(int debtSeconds, int tomorrowPct)
        {
            if (_debtText) _debtText.text = $"Debt {debtSeconds}s";
            if (_tomorrowPctText) _tomorrowPctText.text = $"Tomorrow {Mathf.Clamp(tomorrowPct, 0, 100)}% spent";
        }
        public void SetScore(int s) { if (_scoreText) _scoreText.text = $"Score {s}s"; }
        public void SetStake(int stakeS) { if (_stakeText) _stakeText.text = stakeS > 0 ? $"Stake {stakeS}s" : ""; }
        public void ShowRule(string text) { if (_ruleText) _ruleText.text = text; }
        public void ShowMessage(string text) { if (_messageText) _messageText.text = text; }

        public void ShowInterRound(bool show) { if (_interRoundPanel) _interRoundPanel.SetActive(show); }
        public void SetContinueInteractable(bool can) { if (_continueButton) _continueButton.interactable = can; }
        public void SetCashOutInteractable(bool can) { if (_cashOutButton) _cashOutButton.interactable = can; }
        public void SetRiskInteractable(bool can) { if (_riskButton) _riskButton.interactable = can; }

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

        public void ShowEndScreen(bool busted, int scoreSeconds, int debtSeconds, int secondsPerTomorrow)
        {
            if (_messageText) _messageText.text = busted
                ? $"BUST — Debt remained: {Mathf.Clamp(debtSeconds, 0, int.MaxValue)}s. Final Time: {FormatLife(scoreSeconds, secondsPerTomorrow)}"
                : $"Clean finish. Final Time: {FormatLife(scoreSeconds, secondsPerTomorrow)}";
            ShowInterRound(true);
            SetCashOutInteractable(false);
            SetContinueInteractable(false);
            SetRiskInteractable(false);
        }
    }
    
    
    
}
