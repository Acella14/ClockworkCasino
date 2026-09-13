using System.Collections;
using System.Collections.Generic;
using ClockworkCasino.Audio;
using ClockworkCasino.Cards;
using ClockworkCasino.Persistence;
using ClockworkCasino.Risk;
using ClockworkCasino.Rules;
using ClockworkCasino.UI;
using UnityEngine;

namespace ClockworkCasino.Core
{
    public enum GameState
    {
        Ready,
        InterRound,
        Setup,
        RulePreview,
        RoundActive,
        RiskPreview,
        RiskActive,
        Ended
    }

    public sealed class GameManager : MonoBehaviour
    {
        private enum RiskRoundMode
        {
            None,
            Optional,
            Redemption
        }

        [Header("Dependencies")]
        [SerializeField]
        private GameConfig _config;

        [SerializeField]
        private UIHud _hud;

        [SerializeField]
        private HudEffects _hudEffects;

        [SerializeField]
        private SfxPlayer _soundEffects;

        [SerializeField]
        private CardDealer _cardDealer;

        [SerializeField]
        private RuleManager _ruleManager;

        [Header("Risk Challenges")]
        [SerializeField]
        private RiskChallenge[] _riskChallenges;

        public GameConfig Config => _config;

        public GameState State { get; private set; }

        public int CurrentTableIndex =>
            _currentTableIndex;

        public int CurrentStakeHours =>
            _currentStakeHours;

        public int CurrentWinningsHours =>
            _currentWinningsHours;

        public bool RedemptionAvailable =>
            _redemptionAvailable;

        public int RedemptionRoundsRemaining =>
            _redemptionRoundsRemaining;

        private readonly System.Random _random = new();

        private int _currentTableIndex;
        private int _roundsCompletedAtCurrentTable;

        private int _currentStakeHours;
        private int _currentWinningsHours;

        private int _redemptionRoundsRemaining;
        private bool _redemptionAvailable;

        private float _roundTimeRemaining;
        private float _riskTimeRemaining;
        private float _lifeRefreshTimer;

        private bool _roundTimerPaused;
        private bool _riskTimerPaused;

        private bool _roundResolutionInProgress;
        private bool _riskTakenThisIntermission;

        private RuleDefinition _currentRule;

        private RiskRoundMode _activeRiskMode;
        private RiskChallenge _activeRiskChallenge;
        private CardData[] _riskHand;

        private Coroutine _stakePreviewCoroutine;
        private Coroutine _roundTimeoutCoroutine;

        private void Awake()
        {
            bool missingReference = false;

            if (_config == null)
            {
                Debug.LogError(
                    "GameConfig is not assigned.",
                    this);

                missingReference = true;
            }

            if (_hud == null)
            {
                Debug.LogError(
                    "UIHud is not assigned.",
                    this);

                missingReference = true;
            }

            if (_hudEffects == null)
            {
                Debug.LogError(
                    "HudEffects is not assigned.",
                    this);

                missingReference = true;
            }

            if (_cardDealer == null)
            {
                Debug.LogError(
                    "CardDealer is not assigned.",
                    this);

                missingReference = true;
            }

            if (_ruleManager == null)
            {
                Debug.LogError(
                    "RuleManager is not assigned.",
                    this);

                missingReference = true;
            }

            if (missingReference)
                enabled = false;
        }

        private void Start()
        {
            _hud.InitializeControls(
                ContinueToNextTable,
                LeaveCasino,
                StartRiskRound);

            bool diedWhileAway =
                PlayerProgress.ResolveOfflineDeathIfNeeded(
                    _config.StartingLifeHours);

            ResetRun(diedWhileAway);
        }

        private void Update()
        {
            if (State != GameState.Ended)
                TickPersistentLifeClock();

            switch (State)
            {
                case GameState.RoundActive:
                    TickStandardRound();
                    break;

                case GameState.RiskActive:
                    TickRiskRound();
                    break;
            }
        }

        private void OnDestroy()
        {
            _hud?.ClearControlBindings();
            SaveLifeCheckpoint();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
                SaveLifeCheckpoint();
        }

        private void OnApplicationQuit()
        {
            SaveLifeCheckpoint();
        }

        public void StartRunFromReady()
        {
            if (State != GameState.Ready)
                return;

            if (PlayerProgress.ResolveOfflineDeathIfNeeded(
                    _config.StartingLifeHours))
            {
                RefreshHud();

                _hud.SetMessage(
                    "Your time expired. "
                    + $"You begin again with "
                    + $"{_config.StartingLifeHours} hours.");

                return;
            }

            _hud.SetMessage(string.Empty);
            TransitionTo(GameState.Setup);
        }

        public void ContinueToNextTable()
        {
            if (State != GameState.InterRound
                || _roundResolutionInProgress)
            {
                return;
            }

            _currentTableIndex =
                _config.GetNextTableIndex(
                    _currentTableIndex);

            _roundsCompletedAtCurrentTable = 0;

            TransitionTo(GameState.Setup);
        }

        public void LeaveCasino()
        {
            if (State != GameState.InterRound
                || _roundResolutionInProgress)
            {
                return;
            }

            _soundEffects?.Play(SfxEvent.Cashout);

            EndRun(
                died: false,
                reason: string.Empty);
        }

        public void StartRiskRound()
        {
            if (State != GameState.InterRound
                || _riskTakenThisIntermission
                || _roundResolutionInProgress)
            {
                return;
            }

            _riskTakenThisIntermission = true;

            BeginRiskRound(
                RiskRoundMode.Optional,
                _config.RiskRewardHours);
        }

        public void ResetRun(
            bool diedWhileAway = false)
        {
            StopManagedCoroutines();

            _cardDealer.ClearTable();
            _hudEffects.ClearTableChips();
            _hud.HideRoundEconomy();
            _hudEffects.StopHeartbeat();
            _hudEffects.SetRiskVignette(false);

            _ruleManager.ResetForNewRun();

            _currentTableIndex = 0;
            _roundsCompletedAtCurrentTable = 0;

            _currentStakeHours = 0;
            _currentWinningsHours = 0;

            _redemptionAvailable = true;
            _redemptionRoundsRemaining = 0;

            _roundTimeRemaining = 0f;
            _riskTimeRemaining = 0f;

            _roundTimerPaused = false;
            _riskTimerPaused = false;

            _roundResolutionInProgress = false;
            _riskTakenThisIntermission = false;

            _currentRule = null;

            _activeRiskMode = RiskRoundMode.None;
            _activeRiskChallenge = null;
            _riskHand = null;

            TransitionTo(
                GameState.Ready,
                forceTransition: true);

            _hud.SetMessage(
                diedWhileAway
                    ? "Your time expired while you were away. "
                      + $"You begin again with "
                      + $"{_config.StartingLifeHours} hours."
                    : "Welcome to the Clockwork Casino.");
        }

        private void TransitionTo(
            GameState nextState,
            bool forceTransition = false)
        {
            if (!forceTransition && State == nextState)
                return;

            GameState previousState = State;

            ExitState(
                previousState,
                nextState);

            State = nextState;

            EnterState(nextState);
            RefreshStatePresentation();
        }

        private void ExitState(
            GameState previousState,
            GameState nextState)
        {
            bool wasRiskState =
                IsRiskState(previousState);

            bool remainsRiskState =
                IsRiskState(nextState);

            if (wasRiskState && !remainsRiskState)
            {
                _hudEffects.StopHeartbeat();
                _hudEffects.SetRiskVignette(false);
            }
        }

        private void EnterState(
            GameState state)
        {
            switch (state)
            {
                case GameState.Ready:
                    _hud.UnfreezeRoundClock();
                    break;

                case GameState.InterRound:
                    _currentStakeHours = 0;
                    _currentWinningsHours = 0;

                    _hud.HideRoundEconomy();
                    _hudEffects.ClearPotentialWinningsChips();

                    _hud.UnfreezeRoundClock();

                    _soundEffects?.Play(
                        SfxEvent.IntermissionOpen);
                    break;

                case GameState.Setup:
                    _hud.UnfreezeRoundClock();
                    BeginStandardRoundSetup();
                    break;

                case GameState.RulePreview:
                    _roundTimerPaused = false;
                    _hud.UnfreezeRoundClock();
                    break;

                case GameState.RoundActive:
                    _roundTimerPaused = false;
                    _hud.UnfreezeRoundClock();
                    break;

                case GameState.RiskPreview:
                    _riskTimerPaused = false;
                    _hud.UnfreezeRoundClock();
                    _hudEffects.SetRiskVignette(true);
                    break;

                case GameState.RiskActive:
                    _riskTimerPaused = false;
                    _hud.UnfreezeRoundClock();
                    _hudEffects.StartHeartbeat();
                    break;

                case GameState.Ended:
                    _roundTimerPaused = true;
                    _riskTimerPaused = true;
                    _hud.FreezeRoundClock();
                    break;
            }
        }

        private void RefreshStatePresentation()
        {
            RefreshHud();

            if (State == GameState.Ended)
                return;

            bool isIntermission =
                State == GameState.InterRound;

            _hud.SetIntermissionVisible(isIntermission);
            RefreshIntermissionActions();

            switch (State)
            {
                case GameState.Ready:
                case GameState.Setup:
                case GameState.InterRound:
                    _hud.ShowRoundClock(false);
                    break;

                case GameState.RulePreview:
                case GameState.RiskPreview:
                    _hud.ShowRoundClockFull();
                    break;

                case GameState.RoundActive:
                case GameState.RiskActive:
                    _hud.ShowRoundClock(true);
                    break;
            }
        }

        private void RefreshHud()
        {
            int remainingHours =
                PlayerProgress.GetRemainingHours(
                    _config.StartingLifeHours);

            TableTier table =
                _config.GetTableTier(
                    _currentTableIndex);

            _hud.SetTimeBank(remainingHours);

            string difficultyText = State switch
            {
                GameState.RiskPreview
                or GameState.RiskActive
                    when _activeRiskMode
                         == RiskRoundMode.Redemption
                    => "REDEMPTION",

                GameState.RiskPreview
                or GameState.RiskActive
                    => "RISK",

                _ => table.DisplayName
            };

            _hud.SetDifficulty(difficultyText);
        }

        private void RefreshIntermissionActions()
        {
            bool canUseIntermission =
                State == GameState.InterRound
                && !_roundResolutionInProgress;

            bool canStartRisk =
                canUseIntermission
                && !_riskTakenThisIntermission
                && HasRiskChallenges();

            _hud.SetIntermissionActions(
                canContinue: canUseIntermission,
                canLeave: canUseIntermission,
                canStartRisk: canStartRisk);
        }

        private void BeginStandardRoundSetup()
        {
            TableTier table =
                _config.GetTableTier(
                    _currentTableIndex);

            _currentStakeHours = table.StakeHours;
            _currentWinningsHours = table.WinHours;

            _hud.HideRoundEconomy();
            _hudEffects.ClearTableChips();

            int remainingHours =
                PlayerProgress.GetRemainingHours(
                    _config.StartingLifeHours);

            if (remainingHours <= 0)
            {
                EndRun(
                    died: true,
                    reason: "You ran out of time.");

                return;
            }

            bool committedStake =
                PlayerProgress.TryCommitHours(
                    _currentStakeHours,
                    _config.StartingLifeHours,
                    requireTimeRemainingAfterCommit: true);

            if (!committedStake)
            {
                HandleInsufficientTimeForStake(
                    table.StakeHours);

                return;
            }

            _currentRule =
                _ruleManager.PickRuleForTable(
                    _currentTableIndex);

            _soundEffects?.Play(SfxEvent.NewRound);

            RefreshHud();

            if (_stakePreviewCoroutine != null)
                StopCoroutine(_stakePreviewCoroutine);

            _stakePreviewCoroutine = StartCoroutine(
                StakePreviewRoutine(table));
        }

        private void HandleInsufficientTimeForStake(
            int requiredStakeHours)
        {
            if (_redemptionAvailable)
            {
                int redemptionReward =
                    _config.GetRedemptionRewardHours(
                        requiredStakeHours);

                _redemptionAvailable = false;
                _redemptionRoundsRemaining = 0;

                BeginRiskRound(
                    RiskRoundMode.Redemption,
                    redemptionReward);

                return;
            }

            string cooldownMessage =
                _redemptionRoundsRemaining > 0
                    ? $" Redemption would return in "
                      + $"{_redemptionRoundsRemaining} "
                      + "more rounds."
                    : string.Empty;

            EndRun(
                died: true,
                reason:
                    $"You could not cover the "
                    + $"{requiredStakeHours}h bet."
                    + cooldownMessage);
        }

        private IEnumerator StakePreviewRoutine(
            TableTier table)
        {
            _hud.SetMessage(string.Empty);

            if (_config.StakePreviewSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    _config.StakePreviewSeconds);
            }

            _stakePreviewCoroutine = null;

            if (State != GameState.Setup)
                yield break;

            _hudEffects.PlaceWagerChips(
                _currentStakeHours,
                () => HandleWagerPlacementFinished(table));
        }

        private void HandleWagerPlacementFinished(
            TableTier table)
        {
            if (State != GameState.Setup)
                return;

            _hud.ShowRoundEconomy(
                _currentStakeHours,
                _currentWinningsHours);

            _hudEffects.ShowPotentialWinnings(
                _currentWinningsHours);

            BeginStandardDeal(table);
        }

        private void BeginStandardDeal(
            TableTier table)
        {
            if (State != GameState.Setup)
                return;

            _cardDealer.BeginDealSequence(
                table.CardCount,
                _currentRule,
                cardsReady: HandleStandardCardsReady,
                rulePreviewStarted:
                    () => _hud.SetRule(
                        _currentRule.DisplayText),
                rulePreviewFinished:
                    () => _hud.SetRule(string.Empty));

            TransitionTo(GameState.RulePreview);
        }

        private void BeginRiskRound(
            RiskRoundMode riskMode,
            int rewardHours)
        {
            if (!TryCreateRiskChallenge())
            {
                if (riskMode == RiskRoundMode.Redemption)
                {
                    EndRun(
                        died: true,
                        reason:
                            "No redemption challenge "
                            + "was available.");
                }
                else
                {
                    _hud.SetMessage(
                        "No risk challenges are configured.");

                    _riskTakenThisIntermission = false;
                    RefreshIntermissionActions();
                }

                return;
            }

            _activeRiskMode = riskMode;

            _currentStakeHours = 0;
            _currentWinningsHours =
                Mathf.Max(1, rewardHours);

            _riskTimeRemaining = Mathf.Max(
                1f,
                _activeRiskChallenge.TimeSeconds);

            _soundEffects?.Play(SfxEvent.RiskStart);

            _cardDealer.ClearTable();
            _hudEffects.ClearTableChips();

            _hud.ShowRoundEconomy(
                wagerHours: 0,
                winningsHours: _currentWinningsHours);

            _hudEffects.ShowPotentialWinnings(
                _currentWinningsHours);

            if (riskMode == RiskRoundMode.Redemption)
            {
                _hud.SetMessage(
                    $"REDEMPTION — WIN +"
                    + $"{_currentWinningsHours}h. "
                    + "LOSE AND DIE.");
            }

            var riskDisplayRule = new RuleDefinition
            {
                Type = RuleType.Highest,
                DisplayText = _activeRiskChallenge.Title,
                CurseMode = CurseMode.None,
                CurseProbability = 0f
            };

            TransitionTo(GameState.RiskPreview);

            _cardDealer.BeginDealSequence(
                _riskHand.Length,
                riskDisplayRule,
                cardsReady: HandleRiskCardsReady,
                forcedCards: _riskHand,
                correctnessEvaluator:
                    _activeRiskChallenge.Evaluate,
                rulePreviewStarted:
                    () => _hud.SetRule(
                        _activeRiskChallenge.Title),
                rulePreviewFinished:
                    () => _hud.SetRule(string.Empty));
        }

        private bool TryCreateRiskChallenge()
        {
            if (!HasRiskChallenges())
                return false;

            List<RiskChallenge> usableChallenges =
                GetUsableRiskChallenges();

            Shuffle(
                usableChallenges,
                _random);

            foreach (RiskChallenge challenge
                     in usableChallenges)
            {
                CardData[] generatedHand =
                    challenge.GenerateHand(_random);

                if (generatedHand == null
                    || generatedHand.Length == 0)
                {
                    continue;
                }

                _activeRiskChallenge = challenge;
                _riskHand = generatedHand;
                return true;
            }

            _activeRiskChallenge = null;
            _riskHand = null;
            return false;
        }

        private bool HasRiskChallenges()
        {
            if (_riskChallenges == null)
                return false;

            for (int index = 0;
                 index < _riskChallenges.Length;
                 index++)
            {
                if (_riskChallenges[index] != null)
                    return true;
            }

            return false;
        }

        private List<RiskChallenge> GetUsableRiskChallenges()
        {
            var usableChallenges =
                new List<RiskChallenge>();

            if (_riskChallenges == null)
                return usableChallenges;

            for (int index = 0;
                 index < _riskChallenges.Length;
                 index++)
            {
                if (_riskChallenges[index] != null)
                {
                    usableChallenges.Add(
                        _riskChallenges[index]);
                }
            }

            return usableChallenges;
        }

        private void HandleStandardCardsReady()
        {
            if (State != GameState.RulePreview)
                return;

            _cardDealer.BindSelectionHandlers(
                PauseCurrentDecisionTimer,
                HandleStandardCardResolved);

            TableTier table =
                _config.GetTableTier(
                    _currentTableIndex);

            _roundTimeRemaining =
                table.DecisionTimeSeconds;

            TransitionTo(GameState.RoundActive);
        }

        private void HandleRiskCardsReady()
        {
            if (State != GameState.RiskPreview)
                return;

            _cardDealer.BindSelectionHandlers(
                PauseCurrentDecisionTimer,
                HandleRiskCardResolved);

            TransitionTo(GameState.RiskActive);
        }

        private void TickStandardRound()
        {
            if (_roundTimerPaused
                || _roundResolutionInProgress)
            {
                return;
            }

            TableTier table =
                _config.GetTableTier(
                    _currentTableIndex);

            _roundTimeRemaining = Mathf.Max(
                0f,
                _roundTimeRemaining - Time.deltaTime);

            _hud.SetRoundClock(
                _roundTimeRemaining,
                table.DecisionTimeSeconds);

            if (_roundTimeRemaining <= 0f)
                BeginRoundTimeout();
        }

        private void TickRiskRound()
        {
            if (_riskTimerPaused
                || _roundResolutionInProgress)
            {
                return;
            }

            _riskTimeRemaining = Mathf.Max(
                0f,
                _riskTimeRemaining - Time.deltaTime);

            float totalRiskSeconds =
                Mathf.Max(
                    1f,
                    _activeRiskChallenge != null
                        ? _activeRiskChallenge.TimeSeconds
                        : 1f);

            _hud.SetRoundClock(
                _riskTimeRemaining,
                totalRiskSeconds);

            if (_riskTimeRemaining > 0f)
                return;

            string reason =
                _activeRiskMode == RiskRoundMode.Redemption
                    ? "Redemption ran out of time."
                    : "The risk clock reached zero.";

            EndRun(
                died: true,
                reason: reason);
        }

        private void PauseCurrentDecisionTimer()
        {
            _hud.FreezeRoundClock();

            if (State == GameState.RoundActive)
                _roundTimerPaused = true;

            if (State == GameState.RiskActive)
                _riskTimerPaused = true;
        }

        private void BeginRoundTimeout()
        {
            if (_roundTimeoutCoroutine != null
                || _roundResolutionInProgress)
            {
                return;
            }

            _roundTimerPaused = true;
            _hud.FreezeRoundClock();

            _roundTimeoutCoroutine = StartCoroutine(
                RoundTimeoutRoutine());
        }

        private IEnumerator RoundTimeoutRoutine()
        {
            _soundEffects?.Play(SfxEvent.Timeout);
            _cardDealer.RevealCorrectCards();

            if (_config.ResultDisplaySeconds > 0f)
            {
                yield return new WaitForSeconds(
                    _config.ResultDisplaySeconds);
            }

            _roundTimeoutCoroutine = null;

            ResolveStandardRound(wasCorrect: false);
        }

        private void HandleStandardCardResolved(
            int selectedIndex,
            bool wasCorrect)
        {
            if (State != GameState.RoundActive)
                return;

            ResolveStandardRound(wasCorrect);
        }

        private void ResolveStandardRound(
            bool wasCorrect)
        {
            if (_roundResolutionInProgress)
                return;

            _roundResolutionInProgress = true;
            _roundTimerPaused = true;

            _cardDealer.ClearTable();
            _hud.HideRoundEconomy();

            if (wasCorrect)
            {
                _soundEffects?.Play(SfxEvent.RoundWin);

                int returnedHours =
                    _currentStakeHours
                    + _currentWinningsHours;

                PlayerProgress.AddHours(
                    returnedHours,
                    _config.StartingLifeHours);

                _hud.ShowRoundResult(
                    wasCorrect: true,
                    winningsHours:
                        _currentWinningsHours);

                _hudEffects.ResolveWagerWin(
                    FinishStandardRoundResolution);
            }
            else
            {
                _soundEffects?.Play(SfxEvent.RoundLose);

                _hud.ShowRoundResult(
                    wasCorrect: false,
                    winningsHours: 0);

                _hudEffects.ResolveWagerLoss(
                    FinishStandardRoundResolution);
            }
        }

        private void FinishStandardRoundResolution()
        {
            _currentStakeHours = 0;
            _currentWinningsHours = 0;
            _roundResolutionInProgress = false;

            RefreshHud();

            if (PlayerProgress.HasExpired(
                    _config.StartingLifeHours))
            {
                EndRun(
                    died: true,
                    reason: "You ran out of time.");

                return;
            }

            CompleteStandardRound();
        }

        private void CompleteStandardRound()
        {
            _roundsCompletedAtCurrentTable++;

            AdvanceRedemptionCooldown();

            TableTier table =
                _config.GetTableTier(
                    _currentTableIndex);

            if (_roundsCompletedAtCurrentTable
                >= table.RoundsBeforeIntermission)
            {
                _riskTakenThisIntermission = false;
                TransitionTo(GameState.InterRound);
            }
            else
            {
                TransitionTo(GameState.Setup);
            }
        }

        private void AdvanceRedemptionCooldown()
        {
            if (_redemptionAvailable
                || _redemptionRoundsRemaining <= 0)
            {
                return;
            }

            _redemptionRoundsRemaining--;

            if (_redemptionRoundsRemaining > 0)
                return;

            _redemptionRoundsRemaining = 0;
            _redemptionAvailable = true;

            _hud.SetMessage(
                "REDEMPTION IS AVAILABLE AGAIN.");
        }

        private void HandleRiskCardResolved(
            int selectedIndex,
            bool wasCorrect)
        {
            if (State != GameState.RiskActive
                || _roundResolutionInProgress)
            {
                return;
            }

            _roundResolutionInProgress = true;
            _riskTimerPaused = true;

            _cardDealer.ClearTable();
            _hud.HideRoundEconomy();

            if (!wasCorrect)
            {
                _soundEffects?.Play(SfxEvent.RiskFail);

                string reason =
                    _activeRiskMode
                    == RiskRoundMode.Redemption
                        ? "Redemption failed."
                        : "The risk round belonged "
                          + "to the house.";

                EndRun(
                    died: true,
                    reason: reason);

                return;
            }

            _soundEffects?.Play(SfxEvent.RiskSuccess);

            int rewardHours =
                _currentWinningsHours;

            PlayerProgress.AddHours(
                rewardHours,
                _config.StartingLifeHours);

            RiskRoundMode completedMode =
                _activeRiskMode;

            if (completedMode
                == RiskRoundMode.Redemption)
            {
                _redemptionAvailable = false;

                _redemptionRoundsRemaining =
                    _config.RedemptionCooldownRounds;

                _hud.ShowRedemptionSuccess(
                    rewardHours,
                    _redemptionRoundsRemaining);
            }
            else
            {
                _hud.ShowRiskSuccess(rewardHours);
            }

            _hudEffects.PlayRiskReward(
                rewardHours,
                () =>
                {
                    _roundResolutionInProgress = false;
                    _activeRiskMode = RiskRoundMode.None;

                    _currentStakeHours = 0;
                    _currentWinningsHours = 0;

                    RefreshHud();

                    if (completedMode
                        == RiskRoundMode.Redemption)
                    {
                        TransitionTo(GameState.Setup);
                    }
                    else
                    {
                        TransitionTo(GameState.InterRound);
                    }
                });
        }

        private void TickPersistentLifeClock()
        {
            _lifeRefreshTimer += Time.unscaledDeltaTime;

            if (_lifeRefreshTimer < 1f)
                return;

            _lifeRefreshTimer = 0f;

            RefreshHud();

            if (!PlayerProgress.HasExpired(
                    _config.StartingLifeHours))
            {
                return;
            }

            if (State == GameState.Ready)
            {
                PlayerProgress.RecordDeath(
                    _config.StartingLifeHours);

                RefreshHud();

                _hud.SetMessage(
                    "Your time expired. "
                    + $"You begin again with "
                    + $"{_config.StartingLifeHours} hours.");

                return;
            }

            EndRun(
                died: true,
                reason: "Your remaining time expired.");
        }

        private void EndRun(
            bool died,
            string reason)
        {
            if (State == GameState.Ended)
                return;

            StopManagedCoroutines();

            _cardDealer.ClearTable();
            _hudEffects.ClearTableChips();

            _roundResolutionInProgress = false;

            if (died)
            {
                _currentStakeHours = 0;
                _currentWinningsHours = 0;

                PlayerProgress.RecordDeath(
                    _config.StartingLifeHours);

                _soundEffects?.Play(
                    SfxEvent.EndBusted);
            }
            else
            {
                _soundEffects?.Play(
                    SfxEvent.EndClean);
            }

            _activeRiskMode = RiskRoundMode.None;

            TransitionTo(GameState.Ended);
            RefreshHud();

            int remainingHours =
                PlayerProgress.GetRemainingHours(
                    _config.StartingLifeHours);

            _hud.ShowEndScreen(
                died,
                remainingHours,
                reason);
        }

        private void StopManagedCoroutines()
        {
            if (_stakePreviewCoroutine != null)
            {
                StopCoroutine(_stakePreviewCoroutine);
                _stakePreviewCoroutine = null;
            }

            if (_roundTimeoutCoroutine != null)
            {
                StopCoroutine(_roundTimeoutCoroutine);
                _roundTimeoutCoroutine = null;
            }
        }

        private void SaveLifeCheckpoint()
        {
            if (_config == null)
                return;

            PlayerProgress.Checkpoint(
                _config.StartingLifeHours);
        }

        private static bool IsRiskState(
            GameState state)
        {
            return state == GameState.RiskPreview
                   || state == GameState.RiskActive;
        }

        private static void Shuffle<T>(
            IList<T> list,
            System.Random random)
        {
            for (int index = list.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex =
                    random.Next(0, index + 1);

                (list[index], list[swapIndex]) =
                    (list[swapIndex], list[index]);
            }
        }
    }
}