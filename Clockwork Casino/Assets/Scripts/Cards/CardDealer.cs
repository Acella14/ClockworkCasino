using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ClockworkCasino.Rules;

namespace ClockworkCasino.Cards
{
    public class CardDealer : MonoBehaviour
    {
        [Header("UI Anchors")]
        [SerializeField] private ClockworkCasino.Audio.SfxPlayer _sfx;
        [SerializeField] private ClockworkCasino.UI.SlotOutlines _slotOutlines;
        [SerializeField] private RectTransform _slotsContainer;
        [SerializeField] private RectTransform _cardsLayer;
        [SerializeField] private RectTransform _previewAnchor;
        [SerializeField] private RectTransform _deckAnchor;
        [SerializeField] private CardView _cardPrefab;

        [Header("Timing (Dealer controls the whole pre-round sequence)")]
        [SerializeField, Min(0f)] private float _delayAfterSpendBeforeSpawn = 0.10f; // pause after HUD spend-fill, before cards appear
        [SerializeField, Min(0f)] private float _groupTravelSeconds        = 0.35f; // deck -> preview (stack)
        [SerializeField, Min(0f)] private float _pauseAtPreviewStack       = 0.12f; // pause AFTER stack arrives at preview
        [SerializeField, Min(0f)] private float _spreadSeconds             = 0.15f; // stack -> horizontal spread (no rotation)
        [SerializeField, Min(0f)] private float _pauseAfterDeal            = 0.10f; // pause after final card lands, before flip

        [Header("Deck Spawn Polish (optional)")]
        [SerializeField, Range(0f, 8f)] private float _deckSpawnRotJitter = 2f;

        // Runtime
        CardData[] _currentCards = Array.Empty<CardData>();
        private CurseVisualMode[] _modePerCard;
        HashSet<int> _finalCorrect = new();
        Action _onPickBegan;
        Action<int, bool> _onPickResolved;

        readonly List<CardView> _views = new();
        System.Random _rng = new();
        private Func<CardData[], HashSet<int>> _customCorrectness;

        public void Clear()
        {
            foreach (var v in _views) if (v) Destroy(v.gameObject);
            _views.Clear();
        }

        bool IsColorSensitiveRule(RuleDefinition rule)
        {
            var t = rule.DisplayText?.ToLowerInvariant() ?? string.Empty;
            return t.Contains("red") || t.Contains("black") || t.Contains("color");
        }


        public void BeginPreviewAndDeal(
            int count,
            RuleDefinition rule,
            float rulePreviewSeconds,
            float dealStagger,
            float dealTravelSeconds,
            float halfSpreadWidth,
            Action onFlipComplete,
            CardData[] forcedCards = null,
            Func<CardData[], HashSet<int>> computeCorrectness = null,
            Action onRulePreviewBegin = null,
            Action onRulePreviewEnd = null
        )
        {
            StopAllCoroutines();
            Clear();

            _customCorrectness = computeCorrectness;

            // Build hand
            if (forcedCards != null && forcedCards.Length > 0)
            {
                _currentCards = forcedCards;
                count = forcedCards.Length;
            }
            else
            {
                _currentCards = new CardData[count];
                for (int i = 0; i < count; i++)
                {
                    _currentCards[i] = new CardData
                    {
                        value = _rng.Next(2, 15),
                        suit = (Suit)_rng.Next(0, 4),
                        cursed = false
                    };
                }
                RuleRuntime.EnsureAtLeastOneValid(_currentCards, rule, _rng);
            }

            // Correct set
            _finalCorrect = computeCorrectness != null
                ? computeCorrectness(_currentCards)
                : RuleRuntime.GetCorrectAfterCurses(_currentCards, rule);

            _modePerCard = new CurseVisualMode[count];

            StartCoroutine(CoSequence(
                rule,
                rulePreviewSeconds,
                dealStagger,
                dealTravelSeconds,
                halfSpreadWidth,
                onFlipComplete,
                onRulePreviewBegin,
                onRulePreviewEnd
            ));
        }

        IEnumerator CoSequence(
            RuleDefinition rule,
            float rulePreviewSeconds,
            float dealStagger,
            float dealTravelSeconds,
            float halfSpreadWidth,
            Action onFlipComplete,
            Action onRulePreviewBegin,
            Action onRulePreviewEnd
        )
        {
            var cam = CanvasCamOf(_cardsLayer);

            if (_delayAfterSpendBeforeSpawn > 0f)
                yield return new WaitForSeconds(_delayAfterSpendBeforeSpawn);

            // 1) Spawn on deck
            Vector2 deckLocal = WorldToLocalIn(_cardsLayer, _deckAnchor.position, cam);

            int n = _currentCards.Length;
            for (int i = 0; i < n; i++)
            {
                var view = Instantiate(_cardPrefab, _cardsLayer);
                view.SetFaceDown();

                var rt = view.GetComponent<RectTransform>();
                if (rt)
                {
                    rt.anchoredPosition = deckLocal;

                    float jitter = (_deckSpawnRotJitter > 0f)
                        ? UnityEngine.Random.Range(-_deckSpawnRotJitter, _deckSpawnRotJitter)
                        : 0f;
                    rt.localRotation = Quaternion.Euler(0, 0, jitter);
                }
                _views.Add(view);
            }

            // 2) Move whole stack to preview
            Vector2 previewLocal = WorldToLocalIn(_cardsLayer, _previewAnchor.position, cam);

            _sfx?.Play(ClockworkCasino.Audio.SfxEvent.DeckSlideToPreview);
            for (int i = 0; i < n; i++)
            {
                var rt = _views[i].GetComponent<RectTransform>();
                Vector2 from = rt.anchoredPosition;
                StartCoroutine(FlyAnchored(rt, from, previewLocal, _groupTravelSeconds));
            }
            if (_groupTravelSeconds > 0f)
                yield return new WaitForSeconds(_groupTravelSeconds);

            // 3) Pause at stacked preview
            if (_pauseAtPreviewStack > 0f)
                yield return new WaitForSeconds(_pauseAtPreviewStack);

            // 4) Spread horizontally around preview center
            var startLocal  = new Vector2[n];
            var targetLocal = new Vector2[n];

            _sfx?.Play(ClockworkCasino.Audio.SfxEvent.CardSpread);

            for (int i = 0; i < n; i++)
            {
                var rt = _views[i].GetComponent<RectTransform>();
                startLocal[i] = rt.anchoredPosition;

                float tNorm = (n == 1) ? 0f : (i - (n - 1) * 0.5f) / ((n - 1) * 0.5f);
                float dx = tNorm * halfSpreadWidth;
                targetLocal[i] = previewLocal + new Vector2(dx, 0f);
            }

            float st = 0f;
            while (st < _spreadSeconds)
            {
                st += Time.deltaTime;
                float a = Mathf.Clamp01(st / _spreadSeconds);
                float e = 1f - Mathf.Pow(1f - a, 2f);

                for (int i = 0; i < n; i++)
                {
                    var rt = _views[i].GetComponent<RectTransform>();
                    rt.anchoredPosition = Vector2.Lerp(startLocal[i], targetLocal[i], e);
                    rt.localRotation = Quaternion.identity;
                }
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                var rt = _views[i].GetComponent<RectTransform>();
                rt.anchoredPosition = targetLocal[i];
                rt.localRotation = Quaternion.identity;
            }

            _slotOutlines?.SetCount(n);

            // 5) RULE PREVIEW WINDOW
            onRulePreviewBegin?.Invoke();
            if (rulePreviewSeconds > 0f)
                yield return new WaitForSeconds(rulePreviewSeconds);
            onRulePreviewEnd?.Invoke();

            // 6) Compute slot positions
            var prefabRT = _cardPrefab.GetComponent<RectTransform>();
            Vector2 size = prefabRT ? prefabRT.sizeDelta : new Vector2(120, 160);

            var temps = new List<RectTransform>(n);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("SlotProbe", typeof(RectTransform), typeof(LayoutElement));
                var rt = go.GetComponent<RectTransform>();
                var le = go.GetComponent<LayoutElement>();
                le.preferredWidth  = size.x;
                le.preferredHeight = size.y;

                rt.SetParent(_slotsContainer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = size;
                temps.Add(rt);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_slotsContainer);

            var slotLocals = new List<Vector2>(n);
            foreach (var tRt in temps)
            {
                var corners = new Vector3[4];
                tRt.GetWorldCorners(corners);
                var centerW = (corners[0] + corners[2]) * 0.5f;

                slotLocals.Add(WorldToLocalIn(_cardsLayer, centerW, cam));
            }
            foreach (var t in temps) Destroy(t.gameObject);

            // 7) Deal to slots
            for (int i = 0; i < n; i++)
            {
                _sfx?.PlayVaried(ClockworkCasino.Audio.SfxEvent.CardDeal, 1f, 0.06f, 0.02f);
                var rt = _views[i].GetComponent<RectTransform>();
                Vector2 from = rt.anchoredPosition;
                Vector2 to = slotLocals[i];
                StartCoroutine(FlyAnchored(rt, from, to, dealTravelSeconds));
                if (dealStagger > 0f) yield return new WaitForSeconds(dealStagger);
            }
            if (dealTravelSeconds > 0f)
                yield return new WaitForSeconds(dealTravelSeconds);

            // 8) Brief dwell with face-down cards on the table
            if (_pauseAfterDeal > 0f)
                yield return new WaitForSeconds(_pauseAfterDeal);

            // 9) Flip up & complete
            bool colorSensitive = IsColorSensitiveRule(rule);
            int currCardsLength = _currentCards.Length;

            for (int i = 0; i < currCardsLength; i++)
            {
                _sfx?.PlayVaried(ClockworkCasino.Audio.SfxEvent.CardFlip, 1f, 0.05f, 0.02f);
                bool isCorrect = _finalCorrect.Contains(i);

                var cd = _currentCards[i];
                CurseVisualMode mode =
                    !cd.cursed ? CurseVisualMode.None :
                    colorSensitive ? CurseVisualMode.Stealth :
                    CurseVisualMode.ColorReversedSuit;

                _modePerCard[i] = mode;

                _views[i].SetFaceUp(cd, isCorrect, mode);
            }

            onFlipComplete?.Invoke();
        }

        Vector2 WorldToLocalIn(RectTransform target, Vector3 world, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                target,
                RectTransformUtility.WorldToScreenPoint(cam, world),
                cam,
                out var local);
            return local;
        }

        IEnumerator FlyAnchored(RectTransform rt, Vector2 from, Vector2 to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - a, 2f); // ease-out
                rt.anchoredPosition = Vector2.Lerp(from, to, e);
                rt.localRotation = Quaternion.identity;
                yield return null;
            }
            rt.anchoredPosition = to;
            rt.localRotation = Quaternion.identity;
        }

        public void FlipAllImmediate()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                bool isCorrect = _finalCorrect.Contains(i);
                _views[i].SetFaceUp(_currentCards[i], isCorrect);
            }
        }

        public void OnExternalDisableClicks()
        {
            foreach (var v in _views) v.SetInteractable(false);
        }

        public void OnExternalClear()
        {
            _slotOutlines?.HideAll();
            Clear();
        }

        void OnCardClicked(int index, bool isCorrect)
        {
            foreach (var v in _views) v.SetInteractable(false);
            _sfx?.Play(ClockworkCasino.Audio.SfxEvent.SelectStart);
            _onPickBegan?.Invoke();
            StartCoroutine(CoSelectionFeedbackThenResolve(index, isCorrect));
        }

        public void HandleCardChosen(int index, bool isCorrect)
        {
            StartCoroutine(CoSelectionFeedbackThenResolve(index, isCorrect));
        }

        IEnumerator CoSelectionFeedbackThenResolve(int index, bool isCorrect)
        {
            foreach (var v in _views) v.SetInteractable(false);

            var green  = new Color(0.6f, 0.95f, 0.6f);
            var red    = new Color(0.95f, 0.6f, 0.6f);
            var purple = new Color(0.75f, 0.6f, 0.95f);

            var gm      = FindFirstObjectByType<ClockworkCasino.Core.GameManager>();
            float raise = gm ? gm.Config().selectRaisePixels  : 20f;
            float rSec  = gm ? gm.Config().selectRaiseSeconds : 0.12f;
            float dwell = gm ? gm.Config().resultFlashSeconds : 0.25f;

            // raise first, no color yet
            yield return _views[index].StartCoroutine(_views[index].RaiseOnly(raise, rSec));

            // reveal at apex
            if (!isCorrect)
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.SelectBad);
                foreach (var ci in _finalCorrect)
                {
                    if (ci >= 0 && ci < _views.Count && ci != index)
                        _views[ci].SetTint(green);
                }
            }
            else
            {
                _sfx?.Play(ClockworkCasino.Audio.SfxEvent.SelectGood);
            }

            bool cursed = _currentCards[index].cursed;
            _views[index].SetTint(isCorrect ? green : (cursed ? purple : red));

            yield return new WaitForSeconds(dwell);

            _onPickResolved?.Invoke(index, isCorrect);
        }

        public void RevealCorrectOnTimeout()
        {
            var green  = new Color(0.6f, 0.95f, 0.6f);
            foreach (var v in _views) v.SetInteractable(false);
            foreach (var ci in _finalCorrect)
            {
                if (ci >= 0 && ci < _views.Count)
                    _views[ci].SetTint(green);
            }
        }

        public void BindPickHandlers(Action onPickBegan, Action<int, bool> onPickResolved)
        {
            _onPickBegan    = onPickBegan;
            _onPickResolved = onPickResolved;

            for (int i = 0; i < _views.Count; i++)
            {
                int idx = i;
                bool isCorrect = _finalCorrect.Contains(idx);
                var cd = _currentCards[idx];
                var mode = (_modePerCard != null && idx < _modePerCard.Length) ? _modePerCard[idx] : CurseVisualMode.None;

                _views[i].Bind(
                    idx,
                    cd,
                    isCorrect,
                    mode,
                    (clickedIndex, correct) => OnCardClicked(clickedIndex, correct)
                );
            }
        }

        // ===== Helpers =====
        Camera CanvasCamOf(RectTransform any)
        {
            var canvas = any.GetComponentInParent<Canvas>();
            return (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        }

        Vector3 ScreenToWorldIn(RectTransform target, Vector2 screenPt, Camera cam)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(target, screenPt, cam, out var world);
            return world;
        }

        Vector2 WorldToScreen(Vector3 world, Camera cam)
        {
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        Vector3 CenterWorld(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return (c[0] + c[2]) * 0.5f;
        }
    }
}
