using System;
using System.Linq;
using UnityEngine;

namespace ClockworkCasino.Core
{
    public enum GameState
    {
        InterRound,
        Setup,
        RulePreview,
        RoundActive,
        Resolve,
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
        public GameState State { get; private set; }
        public int RoundIndex { get; private set; } = 0;
        public float TimerS => _timerS;
        public int ScoreS => _scoreS;
        public int DebtS => _debtS;
        public int CurrentStakeS => _currentStakeS;

        // Internals
        float _timerS;
        int _scoreS;
        int _debtS;
        int _currentStakeS;
        bool _borrowUsedThisRound;
        float _stateTimer;   // for inter-round timing
        int _temporaryExtraCards;

        Rules.RuleDefinition _currentRule;
        int _plannedCardCount;

        void Awake()
        {
            if (_config == null) Debug.LogError("GameConfig not set on GameManager.");
            if (_hud == null)    Debug.LogWarning("UIHud not set yet.");
            if (_dealer == null) Debug.LogWarning("CardDealer not set yet.");
            if (_ruleManager == null) Debug.LogWarning("RuleManager not set yet.");
        }

        void Start()
        {
            ResetRun();
            _hud?.Bind(ContinueToNextRound, TryCashOut);
        }

        void Update()
        {
            if (State == GameState.Ended) return;

            if (State == GameState.RoundActive)
            {
                _timerS -= Time.deltaTime;
                _timerS = Mathf.Max(_timerS, 0f);
            }

            // End conditions
            if (_timerS <= 0f)
            {
                if (_debtS > 0) EndRun(busted: true);
                else EndRun(busted: false);
                return;
            }

            switch (State)
            {
                case GameState.InterRound:
                    _stateTimer += Time.deltaTime;
                    if (_stateTimer >= _config.intermissionWindowSeconds)
                    {
                        TryAdvanceToSetup();
                    }
                    break;

                case GameState.RulePreview:
                    _stateTimer += Time.deltaTime;
                    if (_stateTimer >= _config.rulePreviewSeconds)
                        BeginRound(); // deal & start RoundActive
                    break;

                case GameState.RoundActive:
                    // Decision window equals stake
                    _stateTimer += Time.deltaTime;
                    if (_stateTimer >= _currentStakeS)
                    {
                        // out of time = wrong
                        ResolveRound(correct: false);
                    }
                    break;
            }

            _hud?.SetTimer(Mathf.CeilToInt(_timerS));
            _hud?.SetDebt(_debtS, (int)(_debtS / (float)_config.secondsPerTomorrow * 100f));
            _hud?.SetScore(_scoreS);
            _hud?.SetStake(_currentStakeS);

            bool inter = State == GameState.InterRound;
            _hud?.SetCashOutInteractable(inter);
            _hud?.SetContinueInteractable(inter);
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
                    _borrowUsedThisRound = false;
                    _hud?.ShowInterRound(true);
                    _hud?.SetCashOutInteractable(true);
                    _hud?.SetContinueInteractable(true);
                    break;

                case GameState.Setup:
                    _hud?.ShowInterRound(false);
                    _hud?.SetCashOutInteractable(false);
                    _hud?.SetContinueInteractable(false);

                    RoundIndex++;
                    _borrowUsedThisRound = false;
                    _currentStakeS = GetStakeForRound(RoundIndex);
                    SetupRound();
                    break;

                case GameState.RulePreview:
                    break;

                case GameState.RoundActive:
                    break;

                case GameState.Resolve:
                    TransitionTo(GameState.InterRound);
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
            // Pick & show rule
            _currentRule = _ruleManager.PickRuleForRound(RoundIndex, _currentStakeS);
            _hud?.ShowRule(_currentRule.DisplayText);

            int baseCount = Mathf.Clamp(_config.startCardCount + (RoundIndex / 3), _config.startCardCount, _config.maxCardCount);
            _plannedCardCount = Mathf.Clamp(baseCount + _temporaryExtraCards, _config.startCardCount, _config.maxCardCount);
            _temporaryExtraCards = 0;

            TransitionTo(GameState.RulePreview);
        }

        void BeginRound()
        {
            _dealer.Deal(_plannedCardCount, _currentRule, OnCardChosen);
            TransitionTo(GameState.RoundActive);
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


        void OnCardChosen(int indexChosen, bool isCorrect)
        {
            if (State != GameState.RoundActive) return;
            ResolveRound(isCorrect);
        }

        void ResolveRound(bool correct)
        {
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
            if (State != GameState.InterRound && State != GameState.RoundActive)
            {
                _hud?.ShowMessage("You can only borrow during play or intermission.");
                return;
            }

            if (_config.borrowOncePerRound && _borrowUsedThisRound)
            {
                _hud?.ShowMessage("You’ve already borrowed this round.");
                return;
            }

            int B = _config.borrowPacketSeconds;
            int creditLimit = GetCreditLimit();

            if (_debtS + B > creditLimit)
            {
                _hud?.ShowMessage("No credit left. Pay debt or increase score to raise your limit.");
                return;
            }

            _timerS += B;
            _debtS  += B;
            _borrowUsedThisRound = _config.borrowOncePerRound;

            if (_config.borrowSpikeExtraCards > 0)
                _temporaryExtraCards = Mathf.Clamp(_temporaryExtraCards + _config.borrowSpikeExtraCards, 0, 3);

            _hud?.ShowBorrowed(B, GetCreditLimit() - _debtS);
        }


        public void TryCashOut()
        {
            if (State != GameState.InterRound) return;
            EndRun(busted: false);
        }

        bool CanBorrow()
        {
            if (State != GameState.InterRound) return false;
            if (_config.borrowOncePerRound && _borrowUsedThisRound) return false;

            if (_timerS < _currentStakeS) return false;

            int creditLimit = GetCreditLimit();
            int B = _config.borrowPacketSeconds;
            if (_debtS + B > creditLimit) return false;

            return true;
        }

        int GetCreditLimit()
        {
            float limit = _config.baseCreditSeconds + (_scoreS * _config.creditGrowthPerScore);
            return Mathf.FloorToInt(limit);
        }
    }
}
