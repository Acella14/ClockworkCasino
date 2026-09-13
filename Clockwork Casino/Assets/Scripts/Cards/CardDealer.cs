using System;
using System.Collections;
using System.Collections.Generic;
using ClockworkCasino.Audio;
using ClockworkCasino.Core;
using ClockworkCasino.Gameplay;
using ClockworkCasino.Rules;
using ClockworkCasino.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace ClockworkCasino.Cards
{
    public sealed class CardDealer : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private GameConfig _config;

        [FormerlySerializedAs("_sfx")]
        [SerializeField]
        private SfxPlayer _soundEffects;

        [FormerlySerializedAs("_slotOutlines")]
        [SerializeField]
        private SlotOutlines _slotOutlines;

        [Header("Card Table")]
        [FormerlySerializedAs("_cardsLayer")]
        [SerializeField]
        private RectTransform _cardsLayer;

        [FormerlySerializedAs("_previewAnchor")]
        [SerializeField]
        private RectTransform _previewAnchor;

        [FormerlySerializedAs("_deckAnchor")]
        [SerializeField]
        private RectTransform _deckAnchor;

        [FormerlySerializedAs("_cardPrefab")]
        [SerializeField]
        private CardView _cardPrefab;

        [Header("Sequence Timing")]
        [FormerlySerializedAs("_delayAfterSpendBeforeSpawn")]
        [SerializeField, Min(0f)]
        private float _spawnDelaySeconds = 0.1f;

        [FormerlySerializedAs("_groupTravelSeconds")]
        [SerializeField, Min(0f)]
        private float _stackTravelSeconds = 0.35f;

        [FormerlySerializedAs("_pauseAtPreviewStack")]
        [SerializeField, Min(0f)]
        private float _previewStackPauseSeconds = 0.12f;

        [FormerlySerializedAs("_spreadSeconds")]
        [SerializeField, Min(0f)]
        private float _spreadDurationSeconds = 0.15f;

        [FormerlySerializedAs("_pauseAfterDeal")]
        [SerializeField, Min(0f)]
        private float _postDealPauseSeconds = 0.1f;

        [Header("Deck Polish")]
        [FormerlySerializedAs("_deckSpawnRotJitter")]
        [SerializeField, Range(0f, 8f)]
        private float _deckRotationJitterDegrees = 2f;

        [Header("Selection Feedback")]
        [SerializeField]
        private Color _pendingSelectionTint =
            new(0.85f, 0.82f, 0.55f);

        [SerializeField]
        private Color _correctSelectionTint =
            new(0.6f, 0.95f, 0.6f);

        [SerializeField]
        private Color _incorrectSelectionTint =
            new(0.95f, 0.6f, 0.6f);

        [SerializeField]
        private Color _cursedSelectionTint =
            new(0.75f, 0.6f, 0.95f);

        private readonly List<CardView> _cardViews =
            new();

        private readonly System.Random _random =
            new();

        private CardData[] _currentCards =
            Array.Empty<CardData>();

        private CurseVisualMode[] _curseVisualModes =
            Array.Empty<CurseVisualMode>();

        private RuleDefinition _currentRule;
        private RoundObjective _currentObjective;
        private RoundSelectionSession _selectionSession;

        private Action _selectionCompleted;
        private Action<int, bool> _selectionResolved;

        private Coroutine _dealSequenceCoroutine;
        private Coroutine _selectionFeedbackCoroutine;

        private readonly List<Coroutine> _selectionRaiseCoroutines =
            new();

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError(
                    $"{nameof(CardDealer)} requires a GameConfig.",
                    this);
            }

            if (_cardsLayer == null
                || _previewAnchor == null
                || _deckAnchor == null
                || _cardPrefab == null)
            {
                Debug.LogError(
                    $"{nameof(CardDealer)} is missing "
                    + "card-table references.",
                    this);
            }
        }

        public void BeginDealSequence(
            int cardCount,
            RuleDefinition rule,
            Action cardsReady,
            CardData[] forcedCards = null,
            Func<CardData[], HashSet<int>>
                correctnessEvaluator = null,
            Action rulePreviewStarted = null,
            Action rulePreviewFinished = null)
        {
            StopActiveCoroutines();
            DestroyCardViews();

            _slotOutlines?.HideAll();

            _selectionCompleted = null;
            _selectionResolved = null;
            _selectionSession = null;

            _currentRule = rule
                ?? RuleDefinition.Highest();

            try
            {
                if (forcedCards != null
                    && forcedCards.Length > 0)
                {
                    _currentCards =
                        CloneCards(forcedCards);

                    if (correctnessEvaluator != null)
                    {
                        HashSet<int> correctIndices =
                            correctnessEvaluator(
                                _currentCards)
                            ?? new HashSet<int>();

                        _currentObjective =
                            RuleObjectiveFactory
                                .BuildCustomSingle(
                                    _currentRule.DisplayText,
                                    correctIndices);
                    }
                    else
                    {
                        RuleGenerationProfile forcedProfile =
                            new(
                                _currentCards.Length,
                                tableIndex: 0,
                                curseProbability: 0f,
                                CurseMode.None);

                        GeneratedRound forcedRound =
                            RuleObjectiveFactory.Generate(
                                forcedProfile,
                                _currentRule,
                                _random);

                        _currentRule = forcedRound.Rule;
                        _currentCards = forcedRound.Cards;
                        _currentObjective = forcedRound.Objective;
                    }
                }
                else
                {
                    RuleGenerationProfile profile =
                        new(
                            cardCount,
                            tableIndex: 0,
                            _currentRule.CurseProbability,
                            _currentRule.CurseMode);

                    GeneratedRound generatedRound =
                        RuleObjectiveFactory.Generate(
                            profile,
                            _currentRule,
                            _random);

                    _currentRule = generatedRound.Rule;
                    _currentCards = generatedRound.Cards;
                    _currentObjective =
                        generatedRound.Objective;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Failed to generate round for "
                    + $"'{_currentRule.DisplayText}': "
                    + exception.Message,
                    this);

                _currentCards =
                    GenerateRandomHand(cardCount);

                _currentObjective =
                    RoundObjective.CreateSingle(
                        _currentRule.DisplayText,
                        new[] { 0 });
            }

            _curseVisualModes =
                new CurseVisualMode[
                    _currentCards.Length];

            _dealSequenceCoroutine = StartCoroutine(
                DealSequenceRoutine(
                    _currentRule,
                    cardsReady,
                    rulePreviewStarted,
                    rulePreviewFinished));
        }

        public void BindSelectionHandlers(
            Action selectionStarted,
            Action<int, bool> selectionResolved)
        {
            _selectionCompleted =
                selectionStarted;

            _selectionResolved =
                selectionResolved;

            _selectionSession =
                new RoundSelectionSession(
                    _currentObjective);

            for (int index = 0;
                 index < _cardViews.Count;
                 index++)
            {
                _cardViews[index]
                    .ConfigureSelection(
                        index,
                        HandleCardSelected);
            }
        }

        public void RevealCorrectCards()
        {
            DisableCardSelection();

            if (_currentObjective == null)
                return;

            if (_currentObjective.SelectionMode
                == SelectionMode.Ordered)
            {
                for (int orderIndex = 0;
                     orderIndex
                     < _currentObjective
                         .CorrectOrder.Count;
                     orderIndex++)
                {
                    int cardIndex =
                        _currentObjective
                            .CorrectOrder[orderIndex];

                    if (!IsValidCardIndex(cardIndex))
                        continue;

                    _cardViews[cardIndex]
                        .ShowSelectionState(
                            _correctSelectionTint,
                            orderIndex,
                            showOrderNumber: true);
                }

                return;
            }

            foreach (int correctIndex
                     in _currentObjective.ValidIndices)
            {
                if (!IsValidCardIndex(
                        correctIndex))
                {
                    continue;
                }

                _cardViews[correctIndex]
                    .SetTint(
                        _correctSelectionTint);
            }
        }

        public void DisableCardSelection()
        {
            foreach (CardView cardView
                     in _cardViews)
            {
                cardView.SetInteractable(false);
            }
        }

        public void ClearTable()
        {
            StopActiveCoroutines();

            _selectionCompleted = null;
            _selectionResolved = null;

            _selectionSession = null;
            _currentObjective = null;
            _currentRule = null;

            _slotOutlines?.HideAll();

            DestroyCardViews();

            _currentCards =
                Array.Empty<CardData>();

            _curseVisualModes =
                Array.Empty<CurseVisualMode>();
        }

        private void HandleCardSelected(
            int selectedIndex)
        {
            if (_selectionFeedbackCoroutine != null
                || _selectionSession == null
                || !IsValidCardIndex(selectedIndex))
            {
                return;
            }

            SelectionAttemptResult result =
                _selectionSession.TrySelect(
                    selectedIndex);

            switch (result.Status)
            {
                case SelectionAttemptStatus.Ignored:
                    return;

                case SelectionAttemptStatus.Progressed:
                    ShowSelectionProgress(
                        result);
                    return;

                case SelectionAttemptStatus.Deselected:
                    ClearSelectionProgress(
                        selectedIndex);
                    RefreshOrderedMarkers();
                    return;

                case SelectionAttemptStatus
                    .CompletedCorrectly:

                case SelectionAttemptStatus
                    .CompletedIncorrectly:
                    BeginFinalSelectionFeedback(
                        result);
                    return;
            }
        }

        private void ShowSelectionProgress(
            SelectionAttemptResult result)
        {
            bool showOrderNumber =
                _currentObjective.SelectionMode
                == SelectionMode.Ordered;

            _cardViews[result.CardIndex]
                .ShowSelectionState(
                    _pendingSelectionTint,
                    result.SelectionPosition,
                    showOrderNumber);
        }

        private void ClearSelectionProgress(
            int cardIndex)
        {
            if (!IsValidCardIndex(cardIndex))
                return;

            _cardViews[cardIndex]
                .ClearSelectionState();

            _cardViews[cardIndex]
                .ResetFaceTint();
        }

        private void RefreshOrderedMarkers()
        {
            if (_selectionSession == null
                || _currentObjective.SelectionMode
                != SelectionMode.Ordered)
            {
                return;
            }

            for (int index = 0;
                 index < _cardViews.Count;
                 index++)
            {
                _cardViews[index]
                    .ClearSelectionState();

                _cardViews[index]
                    .ResetFaceTint();
            }

            for (int orderIndex = 0;
                 orderIndex
                 < _selectionSession
                     .SelectionOrder.Count;
                 orderIndex++)
            {
                int cardIndex =
                    _selectionSession
                        .SelectionOrder[orderIndex];

                if (!IsValidCardIndex(cardIndex))
                    continue;

                _cardViews[cardIndex]
                    .ShowSelectionState(
                        _pendingSelectionTint,
                        orderIndex,
                        showOrderNumber: true);
            }
        }

        private void BeginFinalSelectionFeedback(
            SelectionAttemptResult result)
        {
            DisableCardSelection();

            _soundEffects?.Play(
                SfxEvent.SelectStart);

            // GameManager uses this callback to pause
            // the decision timer. It is deliberately
            // invoked only when the full objective has
            // been completed, not after the first card.
            _selectionCompleted?.Invoke();

            _selectionFeedbackCoroutine =
                StartCoroutine(
                    SelectionFeedbackRoutine(
                        result.CardIndex,
                        result.WasCorrect));
        }

        private IEnumerator SelectionFeedbackRoutine(
            int finalSelectedIndex,
            bool wasCorrect)
        {
            float raisePixels =
                _config != null
                    ? _config.SelectionRaisePixels
                    : 20f;

            float raiseSeconds =
                _config != null
                    ? _config.SelectionRaiseSeconds
                    : 0.12f;

            float resultDisplaySeconds =
                _config != null
                    ? _config.ResultDisplaySeconds
                    : 0.25f;

            yield return RaiseSelectedCardsRoutine(
                finalSelectedIndex,
                raisePixels,
                raiseSeconds);

            if (wasCorrect)
            {
                _soundEffects?.Play(
                    SfxEvent.SelectGood);

                ShowSuccessfulSelection();
            }
            else
            {
                _soundEffects?.Play(
                    SfxEvent.SelectBad);

                ShowFailedSelection(
                    finalSelectedIndex);
            }

            if (resultDisplaySeconds > 0f)
            {
                yield return new WaitForSeconds(
                    resultDisplaySeconds);
            }

            _selectionFeedbackCoroutine = null;

            Action<int, bool> callback =
                _selectionResolved;

            callback?.Invoke(
                finalSelectedIndex,
                wasCorrect);
        }

        private IEnumerator RaiseSelectedCardsRoutine(
    int fallbackSelectedIndex,
    float raisePixels,
    float raiseSeconds)
        {
            List<int> selectedCardIndices =
                GetSelectedCardIndicesForFeedback(
                    fallbackSelectedIndex);

            if (selectedCardIndices.Count == 0)
                yield break;

            var context =
                new SelectionRaiseContext
                {
                    ExpectedCount =
                        selectedCardIndices.Count
                };

            _selectionRaiseCoroutines.Clear();

            for (int index = 0;
                 index < selectedCardIndices.Count;
                 index++)
            {
                Coroutine coroutine = StartCoroutine(
                    RaiseSingleSelectedCardRoutine(
                        selectedCardIndices[index],
                        raisePixels,
                        raiseSeconds,
                        context));

                _selectionRaiseCoroutines.Add(coroutine);
            }

            yield return new WaitUntil(
                () => context.CompletedCount
                      >= context.ExpectedCount);

            _selectionRaiseCoroutines.Clear();
        }

        private IEnumerator RaiseSingleSelectedCardRoutine(
            int cardIndex,
            float raisePixels,
            float raiseSeconds,
            SelectionRaiseContext context)
        {
            if (!IsValidCardIndex(cardIndex))
            {
                context.CompletedCount++;
                yield break;
            }

            yield return _cardViews[cardIndex].Raise(
                raisePixels,
                raiseSeconds);

            context.CompletedCount++;
        }

        private List<int> GetSelectedCardIndicesForFeedback(
            int fallbackSelectedIndex)
        {
            var result = new List<int>();

            if (_selectionSession != null)
            {
                IReadOnlyList<int> selectionOrder =
                    _selectionSession.SelectionOrder;

                for (int index = 0;
                     index < selectionOrder.Count;
                     index++)
                {
                    int cardIndex =
                        selectionOrder[index];

                    if (!IsValidCardIndex(cardIndex))
                        continue;

                    if (result.Contains(cardIndex))
                        continue;

                    result.Add(cardIndex);
                }
            }

            if (result.Count == 0
                && IsValidCardIndex(fallbackSelectedIndex))
            {
                result.Add(fallbackSelectedIndex);
            }

            return result;
        }

        private int GetSelectionPositionForFeedback(
            int cardIndex,
            int fallbackPosition)
        {
            if (_selectionSession == null)
                return fallbackPosition;

            IReadOnlyList<int> selectionOrder =
                _selectionSession.SelectionOrder;

            for (int index = 0;
                 index < selectionOrder.Count;
                 index++)
            {
                if (selectionOrder[index] == cardIndex)
                    return index;
            }

            return fallbackPosition;
        }


        private void ShowSuccessfulSelection()
        {
            if (_selectionSession == null)
                return;

            for (int orderIndex = 0;
                 orderIndex
                 < _selectionSession
                     .SelectionOrder.Count;
                 orderIndex++)
            {
                int cardIndex =
                    _selectionSession
                        .SelectionOrder[orderIndex];

                if (!IsValidCardIndex(cardIndex))
                    continue;

                bool showOrderNumber =
                    _currentObjective.SelectionMode
                    == SelectionMode.Ordered;

                _cardViews[cardIndex]
                    .ShowSelectionState(
                        _correctSelectionTint,
                        orderIndex,
                        showOrderNumber);
            }
        }

        private void ShowFailedSelection(
            int finalSelectedIndex)
        {
            RevealCorrectCards();

            List<int> selectedCardIndices =
                GetSelectedCardIndicesForFeedback(
                    finalSelectedIndex);

            bool showOrderNumber =
                _currentObjective != null
                && _currentObjective.SelectionMode
                == SelectionMode.Ordered;

            for (int index = 0;
                 index < selectedCardIndices.Count;
                 index++)
            {
                int cardIndex =
                    selectedCardIndices[index];

                if (!IsValidCardIndex(cardIndex))
                    continue;

                int selectionPosition =
                    GetSelectionPositionForFeedback(
                        cardIndex,
                        index);

                _cardViews[cardIndex]
                    .ShowSelectionState(
                        _incorrectSelectionTint,
                        Mathf.Max(0, selectionPosition),
                        showOrderNumber);
            }
        }

        private IEnumerator DealSequenceRoutine(
            RuleDefinition rule,
            Action cardsReady,
            Action rulePreviewStarted,
            Action rulePreviewFinished)
        {
            Camera canvasCamera =
                GetCanvasCamera(_cardsLayer);

            if (_spawnDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(
                    _spawnDelaySeconds);
            }

            Vector2 deckPosition =
                WorldToLocalPosition(
                    _cardsLayer,
                    _deckAnchor.position,
                    canvasCamera);

            SpawnCardsAtDeck(deckPosition);

            Vector2 previewPosition =
                WorldToLocalPosition(
                    _cardsLayer,
                    _previewAnchor.position,
                    canvasCamera);

            _soundEffects?.Play(
                SfxEvent.DeckSlideToPreview);

            foreach (CardView cardView
                     in _cardViews)
            {
                RectTransform cardRectTransform =
                    cardView.GetComponent<
                        RectTransform>();

                StartCoroutine(
                    MoveAnchoredPosition(
                        cardRectTransform,
                        cardRectTransform
                            .anchoredPosition,
                        previewPosition,
                        _stackTravelSeconds));
            }

            if (_stackTravelSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    _stackTravelSeconds);
            }

            if (_previewStackPauseSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    _previewStackPauseSeconds);
            }

            yield return SpreadCards(
                previewPosition);

            rulePreviewStarted?.Invoke();

            float rulePreviewSeconds =
                _config != null
                    ? _config.RulePreviewSeconds
                    : 3f;

            if (rulePreviewSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    rulePreviewSeconds);
            }

            rulePreviewFinished?.Invoke();

            List<Vector2> slotPositions =
                _slotOutlines != null
                    ? _slotOutlines
                        .GetLocalSlotCenters(
                            _cardViews.Count,
                            _cardsLayer,
                            canvasCamera)
                    : new List<Vector2>();

            if (slotPositions.Count
                < _cardViews.Count)
            {
                Debug.LogError(
                    $"{nameof(CardDealer)} needs "
                    + $"at least {_cardViews.Count} "
                    + "configured card slots.",
                    this);

                _dealSequenceCoroutine = null;
                yield break;
            }

            float dealStaggerSeconds =
                _config != null
                    ? _config.DealStaggerSeconds
                    : 0.1f;

            float dealTravelSeconds =
                _config != null
                    ? _config.DealTravelSeconds
                    : 0.25f;

            for (int index = 0;
                 index < _cardViews.Count;
                 index++)
            {
                _soundEffects?.PlayVaried(
                    SfxEvent.CardDeal,
                    volume: 1f,
                    pitchJitter: 0.06f,
                    minInterval: 0.02f);

                RectTransform cardRectTransform =
                    _cardViews[index]
                        .GetComponent<
                            RectTransform>();

                StartCoroutine(
                    MoveAnchoredPosition(
                        cardRectTransform,
                        cardRectTransform
                            .anchoredPosition,
                        slotPositions[index],
                        dealTravelSeconds));

                if (dealStaggerSeconds > 0f)
                {
                    yield return new WaitForSeconds(
                        dealStaggerSeconds);
                }
            }

            if (dealTravelSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    dealTravelSeconds);
            }

            if (_postDealPauseSeconds > 0f)
            {
                yield return new WaitForSeconds(
                    _postDealPauseSeconds);
            }

            RevealCardFaces(rule);

            _dealSequenceCoroutine = null;
            cardsReady?.Invoke();
        }

        private void SpawnCardsAtDeck(
            Vector2 deckPosition)
        {
            for (int index = 0;
                 index < _currentCards.Length;
                 index++)
            {
                CardView cardView =
                    Instantiate(
                        _cardPrefab,
                        _cardsLayer);

                cardView.ShowFaceDown();

                RectTransform cardRectTransform =
                    cardView.GetComponent<
                        RectTransform>();

                cardRectTransform.anchoredPosition =
                    deckPosition;

                float rotationJitter =
                    _deckRotationJitterDegrees > 0f
                        ? UnityEngine.Random.Range(
                            -_deckRotationJitterDegrees,
                            _deckRotationJitterDegrees)
                        : 0f;

                cardRectTransform.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        rotationJitter);

                _cardViews.Add(cardView);
            }
        }

        private IEnumerator SpreadCards(
            Vector2 previewPosition)
        {
            int cardCount =
                _cardViews.Count;

            Vector2[] startingPositions =
                new Vector2[cardCount];

            Vector2[] targetPositions =
                new Vector2[cardCount];

            float halfSpreadWidth =
                _config != null
                    ? _config.PreviewHalfSpreadWidth
                    : 40f;

            _soundEffects?.Play(
                SfxEvent.CardSpread);

            for (int index = 0;
                 index < cardCount;
                 index++)
            {
                RectTransform cardRectTransform =
                    _cardViews[index]
                        .GetComponent<
                            RectTransform>();

                startingPositions[index] =
                    cardRectTransform
                        .anchoredPosition;

                float normalizedPosition =
                    cardCount == 1
                        ? 0f
                        : (index
                           - (cardCount - 1) * 0.5f)
                          / ((cardCount - 1)
                             * 0.5f);

                targetPositions[index] =
                    previewPosition
                    + new Vector2(
                        normalizedPosition
                        * halfSpreadWidth,
                        0f);
            }

            if (_spreadDurationSeconds > 0f)
            {
                float elapsedSeconds = 0f;

                while (elapsedSeconds
                       < _spreadDurationSeconds)
                {
                    elapsedSeconds +=
                        Time.deltaTime;

                    float normalizedTime =
                        Mathf.Clamp01(
                            elapsedSeconds
                            / _spreadDurationSeconds);

                    float easedTime =
                        1f
                        - Mathf.Pow(
                            1f - normalizedTime,
                            2f);

                    for (int index = 0;
                         index < cardCount;
                         index++)
                    {
                        RectTransform
                            cardRectTransform =
                                _cardViews[index]
                                    .GetComponent<
                                        RectTransform>();

                        cardRectTransform
                                .anchoredPosition =
                            Vector2.Lerp(
                                startingPositions[index],
                                targetPositions[index],
                                easedTime);

                        cardRectTransform
                                .localRotation =
                            Quaternion.identity;
                    }

                    yield return null;
                }
            }

            for (int index = 0;
                 index < cardCount;
                 index++)
            {
                RectTransform cardRectTransform =
                    _cardViews[index]
                        .GetComponent<
                            RectTransform>();

                cardRectTransform.anchoredPosition =
                    targetPositions[index];

                cardRectTransform.localRotation =
                    Quaternion.identity;
            }
        }

        private void RevealCardFaces(
            RuleDefinition rule)
        {
            for (int index = 0;
                 index < _currentCards.Length;
                 index++)
            {
                _soundEffects?.PlayVaried(
                    SfxEvent.CardFlip,
                    volume: 1f,
                    pitchJitter: 0.05f,
                    minInterval: 0.02f);

                CardData card =
                    _currentCards[index];

                CurseVisualMode visualMode =
                    card.cursed
                        ? CurseVisualMode.ColorReversedSuit
                        : CurseVisualMode.None;

                _curseVisualModes[index] =
                    visualMode;

                _cardViews[index].ShowFaceUp(
                    card,
                    visualMode);
            }
        }

        private CardData[] GenerateRandomHand(
            int cardCount)
        {
            cardCount = Mathf.Max(
                1,
                cardCount);

            var cards =
                new CardData[cardCount];

            for (int index = 0;
                 index < cards.Length;
                 index++)
            {
                cards[index] = new CardData
                {
                    value = _random.Next(
                        2,
                        15),

                    suit = (Suit)_random.Next(
                        0,
                        4),

                    cursed = false
                };
            }

            return cards;
        }

        private bool IsValidCardIndex(
            int cardIndex)
        {
            return cardIndex >= 0
                   && cardIndex
                   < _cardViews.Count;
        }

        private static CardData[] CloneCards(
            CardData[] source)
        {
            var clone =
                new CardData[source.Length];

            Array.Copy(
                source,
                clone,
                source.Length);

            return clone;
        }

        private void StopActiveCoroutines()
        {
            if (_dealSequenceCoroutine != null)
            {
                StopCoroutine(
                    _dealSequenceCoroutine);

                _dealSequenceCoroutine = null;
            }

            if (_selectionFeedbackCoroutine != null)
            {
                StopCoroutine(
                    _selectionFeedbackCoroutine);

                _selectionFeedbackCoroutine = null;
            }

            for (int index = 0;
                 index < _selectionRaiseCoroutines.Count;
                 index++)
            {
                Coroutine coroutine =
                    _selectionRaiseCoroutines[index];

                if (coroutine != null)
                    StopCoroutine(coroutine);
            }

            _selectionRaiseCoroutines.Clear();
        }

        private void DestroyCardViews()
        {
            foreach (CardView cardView
                     in _cardViews)
            {
                if (cardView != null)
                {
                    Destroy(
                        cardView.gameObject);
                }
            }

            _cardViews.Clear();
        }

        private static Camera GetCanvasCamera(
            RectTransform rectTransform)
        {
            Canvas canvas =
                rectTransform
                    .GetComponentInParent<
                        Canvas>();

            if (canvas == null
                || canvas.renderMode
                == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        private static Vector2 WorldToLocalPosition(
            RectTransform target,
            Vector3 worldPosition,
            Camera canvasCamera)
        {
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    target,
                    RectTransformUtility
                        .WorldToScreenPoint(
                            canvasCamera,
                            worldPosition),
                    canvasCamera,
                    out Vector2 localPosition);

            return localPosition;
        }

        private static IEnumerator
            MoveAnchoredPosition(
                RectTransform rectTransform,
                Vector2 startingPosition,
                Vector2 targetPosition,
                float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                rectTransform.anchoredPosition =
                    targetPosition;

                rectTransform.localRotation =
                    Quaternion.identity;

                yield break;
            }

            float elapsedSeconds = 0f;

            while (elapsedSeconds
                   < durationSeconds)
            {
                elapsedSeconds +=
                    Time.deltaTime;

                float normalizedTime =
                    Mathf.Clamp01(
                        elapsedSeconds
                        / durationSeconds);

                float easedTime =
                    1f
                    - Mathf.Pow(
                        1f - normalizedTime,
                        2f);

                rectTransform.anchoredPosition =
                    Vector2.Lerp(
                        startingPosition,
                        targetPosition,
                        easedTime);

                rectTransform.localRotation =
                    Quaternion.identity;

                yield return null;
            }

            rectTransform.anchoredPosition =
                targetPosition;

            rectTransform.localRotation =
                Quaternion.identity;
        }

        private sealed class SelectionRaiseContext
        {
            public int ExpectedCount;
            public int CompletedCount;
        }
    }
}