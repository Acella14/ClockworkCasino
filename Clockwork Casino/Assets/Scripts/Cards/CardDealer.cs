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
        [Header("UI")]
        [SerializeField] private RectTransform _slotsContainer;
        [SerializeField] private RectTransform _cardsLayer;
        [SerializeField] private RectTransform _previewAnchor;
        [SerializeField] private CardView _cardPrefab;

        CardData[] _currentCards = Array.Empty<CardData>();
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

        // Orchestrator: handles preview fan, dealing, and flip
        public void BeginPreviewAndDeal(
            int count,
            RuleDefinition rule,
            float previewSeconds,
            float dealStagger,
            float dealTravelSeconds,
            float fanRadius,
            Action onFlipComplete,
            CardData[] forcedCards = null,
            Func<CardData[], HashSet<int>> computeCorrectness = null
        )
        {
            StopAllCoroutines();
            Clear();

            Debug.Log($"[Dealer] Rule picked: {rule.Type} / curse={rule.CurseMode} / p={rule.CurseProbability}");
            _customCorrectness = computeCorrectness;

            // 1) Build hand
            bool isForced = forcedCards != null && forcedCards.Length > 0;
            if (isForced)
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
                        suit  = (Suit)_rng.Next(0, 4),
                        cursed = false
                    };
                }

                // Only ensure solvability for normal (non-forced) hands
                RuleRuntime.EnsureAtLeastOneValid(_currentCards, rule, _rng);
            }

            // 2) Compute correct set
            if (_customCorrectness != null)
            {
                // RISK MODE: no curses; use provided evaluator
                _finalCorrect = _customCorrectness(_currentCards);

                // Defensive: a risk challenge should always produce at least one correct
                if (_finalCorrect == null || _finalCorrect.Count == 0)
                    Debug.LogWarning("[Dealer] Risk challenge evaluator returned empty set. Check the challenge’s GenerateHand/Evaluate.");
            }
            else
            {
                // NORMAL MODE: runtime pipeline (with curses)
                _finalCorrect = RuleRuntime.GetCorrectAfterCurses(_currentCards, rule);
            }

            int cursedCount = 0;
            for (int i = 0; i < _currentCards.Length; i++) if (_currentCards[i].cursed) cursedCount++;
            Debug.Log($"[Dealer] Correct indices: {_finalCorrect.Count}, cursed on cards: {cursedCount}");

            // 3) Create fanned previews & deal to slots
            var cam = CanvasCamOf(_cardsLayer);
            for (int i = 0; i < count; i++)
            {
                var view = Instantiate(_cardPrefab, _cardsLayer);
                view.SetFaceDown();

                var rt = view.GetComponent<RectTransform>();
                if (rt)
                {
                    Vector2 baseScreen = WorldToScreen(_previewAnchor.position, cam);
                    float angle = Mathf.Lerp(-15f, 15f, count == 1 ? 0.5f : i / (count - 1f));
                    float dx = Mathf.Cos(angle * Mathf.Deg2Rad) * fanRadius;
                    float dy = Mathf.Sin(angle * Mathf.Deg2Rad) * fanRadius * 0.35f;

                    Vector3 startWorld = ScreenToWorldIn(_cardsLayer, baseScreen + new Vector2(dx, dy), cam);
                    rt.position = startWorld;
                    rt.localRotation = Quaternion.Euler(0, 0, angle * 0.2f);
                }
                _views.Add(view);
            }

            StartCoroutine(CoDealToSlotsThenFlip(previewSeconds, dealStagger, dealTravelSeconds, onFlipComplete));
        }

        IEnumerator CoDealToSlotsThenFlip(float previewSeconds, float dealStagger, float dealTravelSeconds, Action onFlipComplete)
        {
            int n = _views.Count;
            float totalDealing = Mathf.Max(0f, (n - 1) * dealStagger + dealTravelSeconds);
            float leadWait = Mathf.Max(0f, previewSeconds - totalDealing);

            yield return new WaitForSeconds(leadWait);

            // ===== 1) Build slot positions from the layout container =====
            var slotPositions = new List<Vector2>(n);

            // Size probes to match the card prefab so spacing is correct
            var prefabRT = _cardPrefab.GetComponent<RectTransform>();
            Vector2 cardSize = prefabRT ? prefabRT.sizeDelta : new Vector2(120, 160);

            var temps = new List<RectTransform>(n);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("SlotProbe", typeof(RectTransform), typeof(LayoutElement));
                var rt = go.GetComponent<RectTransform>();
                var le = go.GetComponent<LayoutElement>();
                le.preferredWidth = cardSize.x;
                le.preferredHeight = cardSize.y;

                rt.SetParent(_slotsContainer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = cardSize;
                temps.Add(rt);
            }

            var cam = CanvasCamOf(_cardsLayer);
            var slotWorlds = new List<Vector3>(n);


            LayoutRebuilder.ForceRebuildLayoutImmediate(_slotsContainer);

            foreach (var t in temps)
            {
                var centerW = CenterWorld(t);
                var centerScreen = WorldToScreen(centerW, cam);
                var endWorld = ScreenToWorldIn(_cardsLayer, centerScreen, cam);
                slotWorlds.Add(endWorld);
            }
            foreach (var t in temps) Destroy(t.gameObject);

            // ===== 2) Reparent cards to CardsLayer BEFORE flying =====
            for (int i = 0; i < n; i++)
            {
                var rt = _views[i].GetComponent<RectTransform>();
                rt.SetParent(_cardsLayer, worldPositionStays: true);
            }

            // ===== 3) Fly each card to its slot =====
            for (int i = 0; i < n; i++)
            {
                var rt = _views[i].GetComponent<RectTransform>();
                Vector3 start = rt.position;
                Vector3 end   = slotWorlds[i];
                StartCoroutine(Fly(rt, start, end, dealTravelSeconds));
                yield return new WaitForSeconds(dealStagger);
            }

            // Wait for the last travel to finish
            yield return new WaitForSeconds(dealTravelSeconds);

            // ===== 4) Flip all & enable clicks =====
            for (int i = 0; i < n; i++)
            {
                bool isCorrect = _finalCorrect.Contains(i);
                _views[i].SetFaceUp(_currentCards[i], isCorrect);
            }

            onFlipComplete?.Invoke();
        }

        IEnumerator Fly(RectTransform rt, Vector3 from, Vector3 to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - a, 2f);
                rt.position = Vector3.Lerp(from, to, e);
                rt.localRotation = Quaternion.identity;
                yield return null;
            }
            rt.position = to;
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
            Clear();
        }

        void OnCardClicked(int index, bool isCorrect)
        {
            foreach (var v in _views) v.SetInteractable(false);
            _onPickBegan?.Invoke();
            StartCoroutine(CoSelectionFeedbackThenResolve(index, isCorrect));
        }

        public void HandleCardChosen(int index, bool isCorrect)
        {
            StartCoroutine(CoSelectionFeedbackThenResolve(index, isCorrect));
        }

        IEnumerator CoSelectionFeedbackThenResolve(int index, bool isCorrect)
        {
            // stop further clicks
            foreach (var v in _views) v.SetInteractable(false);

            var green  = new Color(0.6f, 0.95f, 0.6f);
            var red    = new Color(0.95f, 0.6f, 0.6f);
            var purple = new Color(0.75f, 0.6f, 0.95f);

            var gm      = FindFirstObjectByType<ClockworkCasino.Core.GameManager>();
            float raise = gm ? gm.Config().selectRaisePixels    : 20f;
            float rSec  = gm ? gm.Config().selectRaiseSeconds   : 0.12f;
            float dwell = gm ? gm.Config().resultFlashSeconds   : 0.25f;

            // raise the chosen card with no color change yet
            yield return _views[index].StartCoroutine(_views[index].RaiseOnly(raise, rSec));

            // reveal at the apex
            if (!isCorrect)
            {
                // tint all correct answers green
                foreach (var ci in _finalCorrect)
                {
                    if (ci >= 0 && ci < _views.Count && ci != index)
                        _views[ci].SetTint(green);
                }
            }

            // tint chosen card (green if correct, red or purple if wrong+cursed)
            bool cursed = _currentCards[index].cursed;
            _views[index].SetTint(isCorrect ? green : (cursed ? purple : red));

            yield return new WaitForSeconds(dwell);

            _onPickResolved?.Invoke(index, isCorrect);
        }
        
        public void RevealCorrectOnTimeout()
        {
            var red = new Color(0.95f, 0.6f, 0.6f);
            foreach (var v in _views) v.SetInteractable(false);
            foreach (var ci in _finalCorrect)
            {
                if (ci >= 0 && ci < _views.Count)
                    _views[ci].SetTint(red);
            }
        }

        public void BindPickHandlers(Action onPickBegan, Action<int, bool> onPickResolved)
        {
            _onPickBegan = onPickBegan;
            _onPickResolved = onPickResolved;

            for (int i = 0; i < _views.Count; i++)
            {
                int idx = i;
                bool isCorrect = _finalCorrect.Contains(idx);

                _views[i].Bind(
                    idx,
                    _currentCards[idx],
                    isCorrect,
                    (clickedIndex, correct) => OnCardClicked(clickedIndex, correct)
                );
            }
        }


        Vector2 WorldToLocalIn(RectTransform target, Vector3 worldPos)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            var cam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                target,
                RectTransformUtility.WorldToScreenPoint(cam, worldPos),
                cam,
                out var local);
            return local;
        }

        Vector2 CenterOfRectWorld(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }
        
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
