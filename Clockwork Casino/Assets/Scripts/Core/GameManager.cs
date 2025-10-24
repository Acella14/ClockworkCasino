using System;
using System.Linq;
using UnityEngine;
using System.Collections;
using ClockworkCasino.Rules;
using ClockworkCasino.Persistence;

namespace ClockworkCasino.Core
{
    public enum GameState
    {
        Ready,
        InterRound,
        Setup,
        RulePreview,
        RoundActive,
        Resolve,
        RiskPreview,
        RiskActive,
        Ended
    }

    public class GameManager : MonoBehaviour
    {
        [Header("Config & UI Hooks")]
        [SerializeField] private GameConfig _config;

        [SerializeField] private UI.UIHud _hud;
        [SerializeField] private ClockworkCasino.Audio.SfxPlayer _sfx;
        [SerializeField] private Cards.CardDealer _dealer;
        [SerializeField] private Rules.RuleManager _ruleManager;

        // Public read-onlys
        public GameConfig Config() => _config;
        public GameState State { get; private set; }
        public int RoundIndex { get; private set; } = 0;
        public float TimerS => _timerS;
        public int ScoreS => _scoreS;
        public int DebtS => _debtS;
        public int CurrentStakeS => _currentStakeS;

        // Internals
        RoundDifficulty _currentDifficulty = RoundDifficulty.Easy;
        float _timerS;
        float _roundTimerS;
        int _scoreS;
        int _debtS;
        int _currentStakeS;
        bool _borrowUsedThisRound;
        float _stateTimer;
        int _temporaryExtraCards;
        bool _riskTakenThisIntermission;

        bool _freezeRoundTick;
        bool _freezeRiskTick;

        bool _capGraceArmed;
        bool _capGraceUsed;
        bool _autoBorrowOn;


        Rules.RuleDefinition _currentRule;
        int _plannedCardCount;

        // RISK
        [SerializeField] private ClockworkCasino.Risk.RiskChallenge[] _riskChallenges;

        float _riskTimerS;
        System.Random _rng = new();
        ClockworkCasino.Risk.RiskChallenge _activeRisk;
        ClockworkCasino.Cards.CardData[] _riskHand;



        void Awake()
        {
            if (_config == null) Debug.LogError("GameConfig not set on GameManager.");
            if (_hud == null) Debug.LogWarning("UIHud not set yet.");
            if (_dealer == null) Debug.LogWarning("CardDealer not set yet.");
            if (_ruleManager == null) Debug.LogWarning("RuleManager not set yet.");
        }

        public void StartRunFromReady()
        {
            if (State != GameState.Ready) return;

            _hud?.ShowMessage(string.Empty);

            int firstStake = GetStakeForRound(1);
            if (_timerS < firstStake)
            {
                _hud?.ShowMessage($"Need {firstStake}s to start. Borrow (Q) first.");
                return;
            }

            TransitionTo(GameState.Setup);
        }

        void Start()
        {
            ResetRun();
            _hud?.Bind(ContinueToNextRound, TryCashOut, StartRiskRound);
            _hud?.BindAutoBorrowToggle(OnAutoBorrowChanged);
            _autoBorrowOn = _config.autoBorrowEnabled;
            _hud?.SetAutoBorrowUI(_autoBorrowOn);
        }

        void OnAutoBorrowChanged(bool on)
        {
            _autoBorrowOn = on;
        }

        void Update()
        {
            if (State == GameState.Ended) return;

            // --- Risk round countdown ---
            if (State == GameState.RiskActive && !_freezeRiskTick)
            {
                _riskTimerS -= Time.deltaTime;
                if (_riskTimerS <= 0f) { EndRun(busted: true); return; }
            }

            // --- Normal round countdown ---
            if (State == GameState.RoundActive && !_freezeRoundTick)
            {
                _roundTimerS -= Time.deltaTime;
                if (_roundTimerS <= 0f)
                {
                    _freezeRoundTick = true;
                    _hud?.FreezeRoundClock();

                    StartCoroutine(CoHandleRoundTimeout());
                    return;
                }
            }


            // --- State bookkeeping ---
            switch (State)
            {
                case GameState.InterRound:
                    if (_config.intermissionWindowSeconds > 0f)
                    {
                        _stateTimer += Time.deltaTime;
                        if (_stateTimer >= _config.intermissionWindowSeconds)
                            TryAdvanceToSetup();
                    }
                    else
                    {
                        _stateTimer = 0f;
                    }
                    break;

                case GameState.RulePreview:
                    _stateTimer += Time.deltaTime;
                    break;

                case GameState.RoundActive:
                    _stateTimer += Time.deltaTime;
                    break;

                case GameState.RiskPreview:
                    _stateTimer += Time.deltaTime;
                    break;
            }

            // --- HUD updates ---

            switch (State)
            {
                case GameState.RoundActive: _hud?.SetRoundClock(_roundTimerS, _currentStakeS); break;
                case GameState.RiskActive: _hud?.SetRoundClock(_riskTimerS, Mathf.Max(1f, _activeRisk?.TimeSeconds ?? 1f)); break;
                case GameState.RulePreview: _hud?.ShowRoundFullForPreview(_currentStakeS); break;
                case GameState.RiskPreview: _hud?.ShowRoundFullForPreview(Mathf.Max(1f, _activeRisk?.TimeSeconds ?? 1f)); break;
                case GameState.Resolve: _hud?.ShowRoundRing(true); break;
                default: _hud?.ShowRoundRing(false); break;
            }

            if (State == GameState.RiskPreview || State == GameState.RiskActive)
            {
                _hud?.SetDifficultyRisk();
            }
            else
            {
                _hud?.SetDifficulty(_currentDifficulty);
            }

            int cap = Mathf.Max(1, _config.secondsPerTomorrow);
            _hud?.SetDebtMeter(_debtS, cap, _capGraceArmed);
            _hud?.SetScoreLife(_scoreS, cap);
            _hud?.SetStake(_currentStakeS);

            bool inter = State == GameState.InterRound;
            _hud?.SetContinueInteractable(inter);
            _hud?.SetCashOutInteractable(inter && _debtS == 0);
            _hud?.SetRiskInteractable(inter && !_riskTakenThisIntermission && _debtS == 0);
        }

        IEnumerator CoHandleRoundTimeout()
        {
            _sfx?.Play(ClockworkCasino.Audio.SfxEvent.Timeout);
            _dealer?.RevealCorrectOnTimeout();
            float dwell = _config ? _config.resultFlashSeconds : 0.25f;
            yield return new WaitForSeconds(dwell);

            _debtS += _currentStakeS;
            _hud?.ShowResult(false, 0, _currentStakeS);
            _dealer.OnExternalClear();

            HandlePostRoundGraceCheck();
            if (State == GameState.Ended) yield break;

            if (IsIntermissionRound(RoundIndex))
                TransitionTo(GameState.InterRound);
            else
                TryAdvanceToSetup();
        }


        void ResetRun()
        {
            _timerS = Mathf.Max(0, _config.startTimerSeconds);
            _scoreS = 0;

            _debtS = Mathf.Min(_config.startTimerSeconds, _config.secondsPerTomorrow);

            RoundIndex = 0;
            _currentStakeS = 0;
            _borrowUsedThisRound = false;
            _temporaryExtraCards = 0;
            _capGraceArmed = false;
            _capGraceUsed = false;

            _hud?.SetGlobalTimeBank(Mathf.CeilToInt(_timerS));
            _hud?.ShowMessage("Welcome to the Clockwork Casino.");
            TransitionTo(GameState.Ready);
        }

        void TransitionTo(GameState next)
        {
            var prev = State;
            _stateTimer = 0f;
            State = next;

            if (State == GameState.RiskPreview) _hud?.SetRiskVignette(true);
            if (State == GameState.RiskActive) _hud?.StartHeartbeatLoop();
            if ((prev == GameState.RiskPreview || prev == GameState.RiskActive) &&
                (State != GameState.RiskPreview && State != GameState.RiskActive))
            {
                _hud?.StopHeartbeatLoop();
                _hud?.SetRiskVignette(false);
            }

            switch (State)
            {
                case GameState.Ready:
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();

                    _hud?.ShowInterRound(false);

                    _riskTakenThisIntermission = false;
                    break;

                case GameState.InterRound:
                    _sfx?.Play(ClockworkCasino.Audio.SfxEvent.IntermissionOpen);
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();

                    _borrowUsedThisRound = false;
                    _hud?.ShowInterRound(true);
                    _hud?.SetContinueInteractable(true);
                    break;

                case GameState.Setup:
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();

                    _hud?.ShowInterRound(false);
                    _hud?.SetCashOutInteractable(false);
                    _hud?.SetContinueInteractable(false);
                    _hud?.SetRiskInteractable(false);

                    _riskTakenThisIntermission = false;

                    RoundIndex++;
                    _borrowUsedThisRound = false;
                    _currentStakeS = GetStakeForRound(RoundIndex);
                    _currentDifficulty = _ruleManager.GetDifficultyForStake(_currentStakeS);
                    _sfx?.Play(ClockworkCasino.Audio.SfxEvent.NewRound);
                    SetupRound();
                    break;

                case GameState.RulePreview:
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();
                    break;

                case GameState.RiskPreview:
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();
                    _hud?.ShowInterRound(false);
                    break;

                case GameState.RiskActive:
                    break;

                case GameState.Resolve:
                    break;

                case GameState.Ended:
                    break;
            }
        }


        int GetStakeForRound(int roundIndex)
        {
            foreach (var band in _config.stakeBands)
                if (band.Matches(roundIndex))
                    return band.StakeSeconds;
            return _config.stakeBands.Last().StakeSeconds;
        }

        bool IsIntermissionRound(int roundIndex)
        {
            int n = Mathf.Max(2, _config.intermissionEveryNRounds);
            return (roundIndex % n) == 0;
        }

        void SetupRound()
        {
            _currentRule = _ruleManager.PickRuleForRound(RoundIndex, _currentStakeS);

            int baseCount = Mathf.Clamp(_config.startCardCount + (RoundIndex / 3), _config.startCardCount, _config.maxCardCount);
            _plannedCardCount = Mathf.Clamp(baseCount + _temporaryExtraCards, _config.startCardCount, _config.maxCardCount);
            _temporaryExtraCards = 0;

            _dealer.OnExternalClear();

            StartCoroutine(CoBuyInThenDeal());
        }

        IEnumerator CoBuyInThenDeal()
        {
            // 1) Briefly show buy-in & payout before charging
            int payout = Mathf.Max(0, Mathf.RoundToInt(_currentStakeS * Mathf.Max(1f, _config.winPayoutMultiplier)));
            _sfx?.Play(ClockworkCasino.Audio.SfxEvent.ShowBuyIn);
            _hud?.ShowMessage($"Buy-in: {_currentStakeS}s - Win: {payout}s");
            float t = 0f, dwell = Mathf.Max(0.1f, _config.buyInPreviewSeconds);
            while (t < dwell) { t += Time.deltaTime; yield return null; }

            // 2) If short, pause here (no intermission UI), try Auto-Borrow (same as manual packets)
            while (Mathf.CeilToInt(_timerS) < _currentStakeS && State == GameState.Setup)
            {
                if (_autoBorrowOn)
                {
                    // auto-borrow repeatedly until we can pay OR borrowing is impossible
                    if (!AttemptBorrow(silent:false, ignoreOnce:true)) break;
                    else continue;
                }

                int need = _currentStakeS - Mathf.CeilToInt(_timerS);
                _hud?.ShowMessage($"<Color=Red>Need {need} more seconds to buy in. Borrow to continue.</Color>");
                yield return null; // wait one frame; player can press Q to borrow
            }

            // 3) If still short (blocked by credit/grace), stop here
            if (Mathf.CeilToInt(_timerS) < _currentStakeS || State != GameState.Setup)
                yield break;

            // 4) Spend, then deal & proceed to RulePreview/RoundActive
            int oldBank = Mathf.CeilToInt(_timerS);
            _timerS = Mathf.Max(0f, _timerS - _currentStakeS);
            int newBank = Mathf.CeilToInt(_timerS);

            _hud?.AnimateSpendWithChips(oldBank, newBank, _currentStakeS, onDone: () =>
            {
                _dealer.BeginPreviewAndDeal(
                    _plannedCardCount,
                    _currentRule,
                    _config.rulePreviewSeconds,
                    _config.dealStaggerPerCard,
                    _config.dealTravelSeconds,
                    _config.fanRadius,
                    onFlipComplete: () =>
                    {
                        _dealer.BindPickHandlers(
                            onPickBegan: () =>
                            {
                                _hud?.FreezeRoundClock();
                                if (State == GameState.RoundActive) _freezeRoundTick = true;
                                if (State == GameState.RiskActive)  _freezeRiskTick  = true;
                            },
                            onPickResolved: OnCardResolved
                        );

                        _roundTimerS = _currentStakeS;
                        TransitionTo(GameState.RoundActive);
                    },
                    forcedCards: null,
                    computeCorrectness: null,
                    onRulePreviewBegin: () => _hud?.ShowRule(_currentRule.DisplayText),
                    onRulePreviewEnd:   () => _hud?.ShowRule(string.Empty)
                );

                TransitionTo(GameState.RulePreview);
            });
        }

        bool AttemptBorrow(bool silent, bool ignoreOnce)
        {
            if (State == GameState.Ended) return false;

            bool inRound = (State == GameState.RoundActive);
            if (!ignoreOnce && _config.borrowOncePerRound && _borrowUsedThisRound)
            {
                if (!silent) _hud?.ShowMessage("You’ve already borrowed this round.");
                return false;
            }

            int packet = Mathf.Max(1, _config.borrowPacketSeconds);
            int cap    = Mathf.Max(1, _config.secondsPerTomorrow);
            int projected = _debtS + packet;

            // Normal borrow under cap
            if (projected <= cap)
            {
                _timerS += packet;
                _debtS  += packet;
                _hud?.SetGlobalTimeBank(Mathf.CeilToInt(_timerS));

                if (_config.borrowSpikeExtraCards > 0)
                    _temporaryExtraCards = Mathf.Clamp(_temporaryExtraCards + _config.borrowSpikeExtraCards, 0, 3);

                if (inRound && _config.borrowOncePerRound) _borrowUsedThisRound = true;
                if (!silent) _hud?.ShowBorrowed(packet, cap - _debtS);
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.Borrow);

                MaybeArmCapGrace();
                return true;
            }

            // Overflow into Sudden Death if allowed and grace not used yet
            if (_config.allowOverflowBorrow && !_capGraceUsed)
            {
                _timerS += packet;
                _debtS  += packet;
                _hud?.SetGlobalTimeBank(Mathf.CeilToInt(_timerS));

                if (_config.borrowSpikeExtraCards > 0)
                    _temporaryExtraCards = Mathf.Clamp(_temporaryExtraCards + _config.borrowSpikeExtraCards, 0, 3);

                if (inRound && _config.borrowOncePerRound) _borrowUsedThisRound = true;

                _capGraceArmed = true;
                _hud?.SetSuddenDeathWarning(true);
                if (!silent) _hud?.ShowMessage($"Borrowed +{packet}s (OVER LIMIT). SUDDEN DEATH — win or die.");
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.BorrowOverflow);

                return true;
            }

            // Blocked
            if (!silent)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt((_debtS / (float)cap) * 100f), 0, 100);
                _hud?.ShowMessage($"No credit left (Debt {pct}%).");
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.BorrowDenied);
            }
            return false;
        }



        public void StartRiskRound()
        {
            if (State != GameState.InterRound)
            {
                return;
            }
            if (_debtS > 0)
            {
                _hud?.ShowMessage("Risk locked while in debt. Clear your debt first.");
                return;
            }
            if (_riskChallenges == null || _riskChallenges.Length == 0)
            {
                return;
            }

            _sfx?.Play(ClockworkCasino.Audio.SfxEvent.RiskStart);

            _riskTakenThisIntermission = true;
            _hud?.SetRiskInteractable(false);
            _hud?.ShowMessage("Starting RISK round...");

            _hud?.UnfreezeRoundClock();
            _hud?.KillSpendAnimation();

            _dealer.OnExternalClear();

            // Pick a challenge
            _activeRisk = _riskChallenges[_rng.Next(0, _riskChallenges.Length)];
            _riskHand = _activeRisk.GenerateHand(_rng);
            _riskTimerS = Mathf.Max(1f, _activeRisk.TimeSeconds);

            _hud?.ShowRule(_activeRisk.Title);

            // Use dealer with forced hand + custom correctness (no curses)
            TransitionTo(GameState.RiskPreview);
            _dealer.BeginPreviewAndDeal(
                _activeRisk.CardCount,
                new Rules.RuleDefinition { Type = Rules.RuleType.Highest, DisplayText = _activeRisk.Title, CurseMode = Rules.CurseMode.None },
                _config.rulePreviewSeconds,
                _config.dealStaggerPerCard,
                _config.dealTravelSeconds,
                _config.fanRadius,
                onFlipComplete: () =>
                {
                    _dealer.BindPickHandlers(
                        onPickBegan: () =>
                        {
                            _hud?.FreezeRoundClock();
                            if (State == GameState.RoundActive) _freezeRoundTick = true;
                            if (State == GameState.RiskActive) _freezeRiskTick = true;
                        },
                        onPickResolved: OnRiskCardResolved
                    );

                    TransitionTo(GameState.RiskActive);
                },
                forcedCards: _riskHand,
                computeCorrectness: (hand) => _activeRisk.Evaluate(hand)
            );
        }


        public void ContinueToNextRound()
        {
            if (State != GameState.InterRound)
            {
                _hud?.ShowMessage("You can only continue between rounds.");
                return;
            }

            TryAdvanceToSetup();
        }

        void TryAdvanceToSetup()
        {
            TransitionTo(GameState.Setup);
        }


        void OnCardResolved(int indexChosen, bool isCorrect)
        {
            if (State != GameState.RoundActive) return;

            // apply outcome
            if (isCorrect)
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.RoundWin);
                int winnings = Mathf.Max(0, Mathf.RoundToInt(_currentStakeS * Mathf.Max(1f, _config.winPayoutMultiplier)));
                int pay = Mathf.Min(winnings, _debtS);
                _debtS -= pay;
                int surplus = winnings - pay;
                _scoreS += surplus;
                _hud?.ShowResult(true, surplus, pay);
            }
            else
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.RoundLose);
                _debtS += _currentStakeS;
                _hud?.ShowResult(false, 0, _currentStakeS);
            }

            _dealer.OnExternalClear();

            HandlePostRoundGraceCheck();
            if (State == GameState.Ended) return;

            if (IsIntermissionRound(RoundIndex))
                TransitionTo(GameState.InterRound);
            else
                TryAdvanceToSetup();
        }

        void OnRiskCardResolved(int indexChosen, bool isCorrect)
        {
            if (State != GameState.RiskActive) return;

            if (isCorrect)
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.RiskSuccess);
                _scoreS = Mathf.Max(0, _scoreS * 2);
                _hud?.ShowMessage($"RISK WON! Score doubled to {_scoreS}.");
                _dealer.OnExternalClear();
                TransitionTo(GameState.InterRound);
            }
            else
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.RiskFail);
                EndRun(busted: true);
            }
        }

        void ResolveRound(bool correct)
        {
            if (State != GameState.RoundActive) return;

            // If we got here via timeout, freeze the visual ring so it stops
            _freezeRoundTick = true;
            _hud?.FreezeRoundClock();

            if (correct)
            {
                int winnings = _currentStakeS;
                int pay = Mathf.Min(winnings, _debtS);
                _debtS -= pay;
                int surplus = winnings - pay;
                _scoreS += surplus;
                _hud?.ShowResult(true, surplus, pay);
            }
            else
            {
                _debtS += _currentStakeS;
                _hud?.ShowResult(false, 0, _currentStakeS);
            }

            _dealer.OnExternalClear();

            if (IsIntermissionRound(RoundIndex))
                TransitionTo(GameState.InterRound);
            else
                TryAdvanceToSetup();
        }


        void EndRun(bool busted)
        {
            _hud?.StopHeartbeatLoop();
            _hud?.SetRiskVignette(false);
            _dealer?.OnExternalClear();

            State = GameState.Ended;
            _hud?.ShowEndScreen(busted, _scoreS, _debtS, _config.secondsPerTomorrow);

            if (busted)
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.EndBusted);
                PlayerProgress.OnDeath();
            }
            else
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.EndClean);
                PlayerProgress.AddCashoutGain(_scoreS);
            }
        }

        // ========= Borrow / Cash Out public API (called by UI) =========

        public void TryBorrow()
        {
            // In Setup/InterRound/Ready allow multiple packets (ignore once-per-round)
            bool ignoreOnce = (State != GameState.RoundActive);
            AttemptBorrow(silent:false, ignoreOnce:ignoreOnce);
        }


        public void TryCashOut()
        {
            if (State != GameState.InterRound) return;
            if (_debtS > 0)
            {
                _hud?.ShowMessage("You can only cash out when your debt is cleared.");
                return;
            }
            EndRun(busted: false);
        }
        
        void MaybeArmCapGrace()
        {
            int cap = Mathf.Max(1, _config.secondsPerTomorrow);
            if (_debtS >= cap)
            {
                if (_capGraceUsed) { EndRun(busted: true); }
                else if (!_capGraceArmed)
                {
                    _capGraceArmed = true;
                    _hud?.SetSuddenDeathWarning(true);
                    _sfx?.Play(ClockworkCasino.Audio.SfxEvent.SuddenDeathOn);
                    _hud?.ShowMessage("SUDDEN DEATH — a wrong pick ends the run.");
                }
            }
        }

        void HandlePostRoundGraceCheck()
        {
            if (!_capGraceArmed) return;
            int cap = Mathf.Max(1, _config.secondsPerTomorrow);

            if (_debtS >= cap) EndRun(busted: true);
            else
            {
                _capGraceArmed = false;
                _capGraceUsed = true;
                _hud?.SetSuddenDeathWarning(false);
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.SuddenDeathClear);
            }
        }
        
    }
}
