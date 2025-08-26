using System;
using System.Linq;
using UnityEngine;
using System.Collections;

namespace ClockworkCasino.Core
{
    public enum GameState
    {
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

        void Start()
        {
            ResetRun();
            _hud?.Bind(ContinueToNextRound, TryCashOut, StartRiskRound);
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
                    _stateTimer += Time.deltaTime;
                    if (_stateTimer >= _config.intermissionWindowSeconds)
                        TryAdvanceToSetup();
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
            _hud?.SetGlobalTimeBank(Mathf.CeilToInt(_timerS));

            switch (State)
            {
                case GameState.RoundActive:
                    _hud?.SetRoundClock(_roundTimerS, _currentStakeS); 
                    break;

                case GameState.RiskActive:
                    _hud?.SetRoundClock(_riskTimerS, Mathf.Max(1f, _activeRisk?.TimeSeconds ?? 1f));
                    break;

                case GameState.RulePreview:
                    _hud?.ShowRoundFullForPreview(_currentStakeS);
                    break;

                case GameState.RiskPreview:
                    _hud?.ShowRoundFullForPreview(Mathf.Max(1f, _activeRisk?.TimeSeconds ?? 1f));
                    break;

                case GameState.Setup:
                    break;

                case GameState.Resolve:
                    _hud?.ShowRoundRing(true);
                    break;

                default:
                    _hud?.ShowRoundRing(false);
                    break;
            }


            _hud?.SetDebt(_debtS, (int)(_debtS / (float)_config.secondsPerTomorrow * 100f));
            _hud?.SetScore(_scoreS);
            _hud?.SetStake(_currentStakeS);

            bool inter = State == GameState.InterRound;
            _hud?.SetCashOutInteractable(inter);
            _hud?.SetContinueInteractable(inter);
            _hud?.SetRiskInteractable(inter && !_riskTakenThisIntermission);
        }

        IEnumerator CoHandleRoundTimeout()
        {
            // Ask dealer to reveal correct cards in RED (no raise)
            _dealer?.RevealCorrectOnTimeout();

            // Small dwell so the player can read it
            float dwell = _config ? _config.resultFlashSeconds : 0.25f;
            yield return new WaitForSeconds(dwell);

            // Apply "wrong" outcome, clear, transition
            _debtS += _currentStakeS;
            _hud?.ShowResult(false, 0, _currentStakeS);

            _dealer.OnExternalClear();

            if (IsIntermissionRound(RoundIndex))
                TransitionTo(GameState.InterRound);
            else
                TransitionTo(GameState.Setup);
        }


        void ResetRun()
        {
            _timerS = _config.startTimerSeconds;
            _scoreS = 0;
            _debtS = 0;
            RoundIndex = 0;
            _currentStakeS = 0;
            _borrowUsedThisRound = false;
            _temporaryExtraCards = 0;

            _hud?.ShowMessage("Welcome to the Clockwork Casino.");
            TransitionTo(GameState.Setup);
        }

        void TransitionTo(GameState next)
        {
            _stateTimer = 0f;
            State = next;

            switch (State)
            {
                case GameState.InterRound:
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();

                    _borrowUsedThisRound = false;
                    _hud?.ShowInterRound(true);
                    _hud?.SetCashOutInteractable(true);
                    _hud?.SetContinueInteractable(true);
                    _hud?.SetRiskInteractable(!_riskTakenThisIntermission);
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
                    SetupRound();
                    break;

                case GameState.RulePreview:
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();
                    break;

                case GameState.RiskPreview:
                    _freezeRoundTick = _freezeRiskTick = false;
                    _hud?.UnfreezeRoundClock();
                    break;

                case GameState.RoundActive:
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
            _hud?.ShowRule(_currentRule.DisplayText);

            int baseCount = Mathf.Clamp(_config.startCardCount + (RoundIndex / 3), _config.startCardCount, _config.maxCardCount);
            _plannedCardCount = Mathf.Clamp(baseCount + _temporaryExtraCards, _config.startCardCount, _config.maxCardCount);
            _temporaryExtraCards = 0;

            // spend
            int oldBank = Mathf.CeilToInt(_timerS);
            _timerS = Mathf.Max(0f, _timerS - _currentStakeS);
            int newBank = Mathf.CeilToInt(_timerS);

            _dealer.OnExternalClear();

            _hud?.AnimateSpendToRoundClock(oldBank, newBank, _currentStakeS, animSeconds: 0.6f, onDone: () =>
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
                        // bind single-time pick handlers
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
                    }
                );

                TransitionTo(GameState.RulePreview);
            });
        }


        public void StartRiskRound()
        {
            if (State != GameState.InterRound)
            {
                _hud?.ShowMessage("Risk is only available between rounds.");
                return;
            }
            if (_riskChallenges == null || _riskChallenges.Length == 0)
            {
                _hud?.ShowMessage("No risk challenges configured.");
                return;
            }

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
                            if (State == GameState.RiskActive)  _freezeRiskTick  = true;
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
            // Check we have enough time for the next stake
            int nextStake = GetStakeForRound(RoundIndex + 1);
            if (_timerS < nextStake)
            {
                _hud?.ShowMessage($"Not enough time to continue (need {nextStake}s). Borrow or cash out.");
                return;
            }

            TransitionTo(GameState.Setup);
        }


        void OnCardResolved(int indexChosen, bool isCorrect)
        {
            if (State != GameState.RoundActive) return;

            // apply outcome
            if (isCorrect)
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
                TransitionTo(GameState.Setup);
        }

        void OnRiskCardResolved(int indexChosen, bool isCorrect)
        {
            if (State != GameState.RiskActive) return;

            if (isCorrect)
            {
                _scoreS = Mathf.Max(0, _scoreS * 2);
                _hud?.ShowMessage($"RISK WON! Score doubled to {_scoreS}.");
                _dealer.OnExternalClear();
                TransitionTo(GameState.InterRound);
            }
            else
            {
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
                TransitionTo(GameState.Setup);
        }


        void EndRun(bool busted)
        {
            State = GameState.Ended;
            _hud?.ShowEndScreen(busted, _scoreS, _debtS);
        }

        // ========= Borrow / Cash Out public API (called by UI) =========

        public void TryBorrow()
        {
            if (State == GameState.Ended)
                return;

            if (_config.borrowOncePerRound && _borrowUsedThisRound)
            {
                _hud?.ShowMessage("You’ve already borrowed this round.");
                return;
            }

            int packet = _config.borrowPacketSeconds;
            int cap = Mathf.Max(1, _config.secondsPerTomorrow);
            int projectedDebt = _debtS + packet;

            // allow only if Tomorrow% < 100% (projected debt stays ≤ cap)
            if (projectedDebt > cap)
            {
                int pct = Mathf.RoundToInt((_debtS / (float)cap) * 100f);
                _hud?.ShowMessage($"No credit left (Tomorrow {pct}%).");
                return;
            }

            // Apply borrow
            _timerS += packet;
            _debtS += packet;

            if (_config.borrowSpikeExtraCards > 0)
                _temporaryExtraCards = Mathf.Clamp(_temporaryExtraCards + _config.borrowSpikeExtraCards, 0, 3);

            _borrowUsedThisRound = _config.borrowOncePerRound;

            // HUD feedback (remaining credit = seconds until 100%)
            _hud?.ShowBorrowed(packet, cap - _debtS);
        }


        public void TryCashOut()
        {
            if (State != GameState.InterRound) return;
            EndRun(busted: false);
        }
        
        
    }
}
