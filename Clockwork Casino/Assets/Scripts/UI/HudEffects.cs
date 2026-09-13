using System;
using System.Collections;
using System.Collections.Generic;
using ClockworkCasino.Audio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClockworkCasino.UI
{
    public sealed class HudEffects : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private SfxPlayer _soundEffects;

        [Header("Chip Layer")]
        [SerializeField]
        private RectTransform _effectsLayer;

        [Header("Bank Chip Templates")]
        [SerializeField]
        private RectTransform _oneHourChipTemplate;

        [SerializeField]
        private RectTransform _fiveHourChipTemplate;

        [SerializeField]
        private RectTransform _tenHourChipTemplate;

        [Header("Table Displays")]
        [SerializeField]
        private RectTransform _wagerChipHolder;

        [SerializeField]
        private RectTransform _winningsChipHolder;

        [Header("Chip Destinations")]
        [SerializeField]
        private RectTransform _timeBankAnchor;

        [SerializeField]
        private RectTransform _houseCollectionAnchor;

        [Header("Table Chip Appearance")]
        [Tooltip(
            "Displayed chip scale relative to the full-size bank chips. " +
            "A 120x120 bank chip becomes visually 48x48 at 0.4.")]
        [SerializeField, Range(0.1f, 1f)]
        private float _tableChipScale = 0.4f;

        [Header("Shared Chip Layout")]
        [Tooltip(
            "Center-to-center spacing. Values below the displayed chip " +
            "width create intentional overlap.")]
        [SerializeField, Min(0f)]
        private float _chipSpacing = 26f;

        [SerializeField, Min(1)]
        private int _maximumChipsPerRow = 6;

        [SerializeField, Range(0f, 1f)]
        private float _rowSpacingMultiplier = 0.35f;

        [Header("Chip Motion")]
        [SerializeField, Min(0f)]
        private float _placementTravelSeconds = 0.32f;

        [SerializeField, Min(0f)]
        private float _resolutionTravelSeconds = 0.2f;

        [SerializeField, Min(0f)]
        private float _chipSpawnIntervalSeconds = 0.06f;

        [Header("Risk Vignette")]
        [SerializeField]
        private Volume _globalVolume;

        [SerializeField, Range(0f, 1f)]
        private float _riskVignetteIntensity = 0.2f;

        [SerializeField, Min(0f)]
        private float _vignetteFadeSeconds = 0.25f;

        [Header("Risk Heartbeat")]
        [SerializeField]
        private AudioSource _heartbeatAudioSource;

        [SerializeField]
        private AudioClip _heartbeatClip;

        [SerializeField, Min(0.01f)]
        private float _heartbeatIntervalSeconds = 0.6f;

        [SerializeField, Range(0f, 1f)]
        private float _heartbeatVolume = 1f;

        private readonly List<RectTransform> _wagerChips = new();
        private readonly List<RectTransform> _winningsChips = new();
        private readonly List<GameObject> _spawnedChips = new();
        private readonly List<Coroutine> _chipCoroutines = new();

        private Coroutine _chipSequenceCoroutine;
        private Coroutine _vignetteCoroutine;
        private Coroutine _heartbeatCoroutine;

        private Vector3 TableChipScaleVector =>
            Vector3.one * Mathf.Clamp(
                _tableChipScale,
                0.1f,
                1f);

        private void OnDisable()
        {
            ClearTableChips();
            StopHeartbeat();

            if (_vignetteCoroutine != null)
            {
                StopCoroutine(_vignetteCoroutine);
                _vignetteCoroutine = null;
            }

            SetVignetteImmediately(0f);
        }

        public void ShowPotentialWinnings(int winningsHours)
        {
            StopCurrentChipMotion();
            ClearWinningsChipsImmediately();

            List<ChipInstruction> plan =
                BuildChipPlan(winningsHours);

            if (!CanDisplayChips()
                || _winningsChipHolder == null)
            {
                return;
            }

            for (int index = 0;
                 index < plan.Count;
                 index++)
            {
                RectTransform chip = CreateChip(
                    plan[index].Template,
                    _winningsChipHolder,
                    TableChipScaleVector);

                if (chip == null)
                    continue;

                chip.position = GetDisplayTargetWorldPosition(
                    _winningsChipHolder,
                    index,
                    plan.Count);

                _winningsChips.Add(chip);
            }
        }

        public void ClearPotentialWinningsChips()
        {
            ClearWinningsChipsImmediately();
        }

        public void PlaceWagerChips(
            int stakeHours,
            Action completed)
        {
            StopCurrentChipMotion();
            ClearWagerChipsImmediately();

            List<ChipInstruction> plan =
                BuildChipPlan(stakeHours);

            if (!CanDisplayChips()
                || _wagerChipHolder == null
                || plan.Count == 0)
            {
                completed?.Invoke();
                return;
            }

            _chipSequenceCoroutine = StartCoroutine(
                PlaceWagerRoutine(plan, completed));
        }

        public void ResolveWagerWin(Action completed)
        {
            StopCurrentChipMotion();

            _chipSequenceCoroutine = StartCoroutine(
                ResolveWagerWinRoutine(completed));
        }

        public void ResolveWagerLoss(Action completed)
        {
            StopCurrentChipMotion();

            _chipSequenceCoroutine = StartCoroutine(
                ResolveWagerLossRoutine(completed));
        }

        public void PlayRiskReward(
            int rewardHours,
            Action completed)
        {
            if (_winningsChips.Count == 0)
                ShowPotentialWinnings(rewardHours);

            StopCurrentChipMotion();

            _chipSequenceCoroutine = StartCoroutine(
                MoveWinningsToBankRoutine(completed));
        }

        public void ClearTableChips()
        {
            StopCurrentChipMotion();

            ClearWagerChipsImmediately();
            ClearWinningsChipsImmediately();

            for (int index = _spawnedChips.Count - 1;
                 index >= 0;
                 index--)
            {
                GameObject chipObject =
                    _spawnedChips[index];

                if (chipObject != null)
                    Destroy(chipObject);
            }

            _spawnedChips.Clear();
        }

        public void SetRiskVignette(bool isEnabled)
        {
            if (_vignetteCoroutine != null)
            {
                StopCoroutine(_vignetteCoroutine);
                _vignetteCoroutine = null;
            }

            _vignetteCoroutine = StartCoroutine(
                VignetteRoutine(isEnabled));
        }

        public void StartHeartbeat()
        {
            StopHeartbeat();

            if (_heartbeatAudioSource == null
                || _heartbeatClip == null)
            {
                return;
            }

            _heartbeatCoroutine = StartCoroutine(
                HeartbeatRoutine());
        }

        public void StopHeartbeat()
        {
            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
            }

            if (_heartbeatAudioSource != null)
                _heartbeatAudioSource.Stop();
        }

        private IEnumerator PlaceWagerRoutine(
            IReadOnlyList<ChipInstruction> plan,
            Action completed)
        {
            var context = new AnimationContext
            {
                ExpectedCount = plan.Count
            };

            for (int index = 0;
                 index < plan.Count;
                 index++)
            {
                RectTransform chip = CreateChip(
                    plan[index].Template,
                    plan[index].Template,
                    Vector3.one);

                if (chip == null)
                {
                    context.CompletedCount++;
                    continue;
                }

                _wagerChips.Add(chip);

                Vector3 targetPosition =
                    GetDisplayTargetWorldPosition(
                        _wagerChipHolder,
                        index,
                        plan.Count);

                StartChipCoroutine(
                    MoveChipRoutine(
                        chip,
                        targetPosition,
                        TableChipScaleVector,
                        index * _chipSpawnIntervalSeconds,
                        _placementTravelSeconds,
                        destroyAtDestination: false,
                        context));
            }

            yield return new WaitUntil(
                () => context.CompletedCount
                      >= context.ExpectedCount);

            FinishChipSequence();
            completed?.Invoke();
        }

        private IEnumerator ResolveWagerWinRoutine(
            Action completed)
        {
            int expectedCount =
                _wagerChips.Count
                + _winningsChips.Count;

            if (expectedCount == 0
                || _timeBankAnchor == null)
            {
                ClearWagerChipsImmediately();
                ClearWinningsChipsImmediately();

                FinishChipSequence();
                completed?.Invoke();
                yield break;
            }

            var context = new AnimationContext
            {
                ExpectedCount = expectedCount
            };

            Vector3 bankPosition =
                GetRectCenterWorld(_timeBankAnchor);

            for (int index = 0;
                 index < _wagerChips.Count;
                 index++)
            {
                StartChipCoroutine(
                    MoveChipRoutine(
                        _wagerChips[index],
                        bankPosition,
                        Vector3.one,
                        index * 0.015f,
                        _resolutionTravelSeconds,
                        destroyAtDestination: true,
                        context));
            }

            for (int index = 0;
                 index < _winningsChips.Count;
                 index++)
            {
                StartChipCoroutine(
                    MoveChipRoutine(
                        _winningsChips[index],
                        bankPosition,
                        Vector3.one,
                        0.04f
                        + index * 0.015f,
                        _resolutionTravelSeconds,
                        destroyAtDestination: true,
                        context));
            }

            _wagerChips.Clear();
            _winningsChips.Clear();

            yield return new WaitUntil(
                () => context.CompletedCount
                      >= context.ExpectedCount);

            FinishChipSequence();
            completed?.Invoke();
        }

        private IEnumerator ResolveWagerLossRoutine(
            Action completed)
        {
            int expectedCount =
                _wagerChips.Count
                + _winningsChips.Count;

            if (expectedCount == 0
                || _houseCollectionAnchor == null)
            {
                ClearWagerChipsImmediately();
                ClearWinningsChipsImmediately();

                FinishChipSequence();
                completed?.Invoke();
                yield break;
            }

            var context = new AnimationContext
            {
                ExpectedCount = expectedCount
            };

            Vector3 housePosition =
                GetRectCenterWorld(
                    _houseCollectionAnchor);

            for (int index = 0;
                 index < _wagerChips.Count;
                 index++)
            {
                StartChipCoroutine(
                    MoveChipRoutine(
                        _wagerChips[index],
                        housePosition,
                        Vector3.zero,
                        index * 0.015f,
                        _resolutionTravelSeconds,
                        destroyAtDestination: true,
                        context));
            }

            for (int index = 0;
                 index < _winningsChips.Count;
                 index++)
            {
                StartChipCoroutine(
                    MoveChipRoutine(
                        _winningsChips[index],
                        housePosition,
                        Vector3.zero,
                        index * 0.015f,
                        _resolutionTravelSeconds,
                        destroyAtDestination: true,
                        context));
            }

            _wagerChips.Clear();
            _winningsChips.Clear();

            yield return new WaitUntil(
                () => context.CompletedCount
                      >= context.ExpectedCount);

            FinishChipSequence();
            completed?.Invoke();
        }

        private IEnumerator MoveWinningsToBankRoutine(
            Action completed)
        {
            if (_winningsChips.Count == 0
                || _timeBankAnchor == null)
            {
                ClearWinningsChipsImmediately();

                FinishChipSequence();
                completed?.Invoke();
                yield break;
            }

            var context = new AnimationContext
            {
                ExpectedCount = _winningsChips.Count
            };

            Vector3 bankPosition =
                GetRectCenterWorld(_timeBankAnchor);

            for (int index = 0;
                 index < _winningsChips.Count;
                 index++)
            {
                StartChipCoroutine(
                    MoveChipRoutine(
                        _winningsChips[index],
                        bankPosition,
                        Vector3.one,
                        index * 0.015f,
                        _resolutionTravelSeconds,
                        destroyAtDestination: true,
                        context));
            }

            _winningsChips.Clear();

            yield return new WaitUntil(
                () => context.CompletedCount
                      >= context.ExpectedCount);

            FinishChipSequence();
            completed?.Invoke();
        }

        private IEnumerator MoveChipRoutine(
            RectTransform chip,
            Vector3 targetPosition,
            Vector3 targetScale,
            float delaySeconds,
            float travelSeconds,
            bool destroyAtDestination,
            AnimationContext context)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            if (chip == null)
            {
                context.CompletedCount++;
                yield break;
            }

            Vector3 startingPosition = chip.position;
            Vector3 startingScale = chip.localScale;

            if (travelSeconds <= 0f)
            {
                chip.position = targetPosition;
                chip.localScale = targetScale;
            }
            else
            {
                float elapsedSeconds = 0f;

                while (elapsedSeconds < travelSeconds)
                {
                    elapsedSeconds += Time.deltaTime;

                    float normalizedTime = Mathf.Clamp01(
                        elapsedSeconds / travelSeconds);

                    float easedTime =
                        1f - Mathf.Pow(
                            1f - normalizedTime,
                            2f);

                    chip.position = Vector3.Lerp(
                        startingPosition,
                        targetPosition,
                        easedTime);

                    chip.localScale = Vector3.Lerp(
                        startingScale,
                        targetScale,
                        easedTime);

                    yield return null;
                }

                chip.position = targetPosition;
                chip.localScale = targetScale;
            }

            _soundEffects?.PlayVaried(
                SfxEvent.ChipInsert,
                volume: 1f,
                pitchJitter: 0.07f,
                minInterval: 0.015f);

            if (destroyAtDestination)
                DestroyTrackedChip(chip);

            context.CompletedCount++;
        }

        private RectTransform CreateChip(
            RectTransform template,
            RectTransform sourceAnchor,
            Vector3 initialScale)
        {
            if (template == null
                || sourceAnchor == null
                || _effectsLayer == null)
            {
                return null;
            }

            RectTransform chip = Instantiate(
                    template.gameObject,
                    _effectsLayer)
                .GetComponent<RectTransform>();

            chip.position =
                GetRectCenterWorld(sourceAnchor);

            chip.localRotation = Quaternion.identity;
            chip.localScale = initialScale;
            chip.SetAsLastSibling();

            _spawnedChips.Add(chip.gameObject);

            return chip;
        }

        private List<ChipInstruction> BuildChipPlan(
            int totalHours)
        {
            var plan = new List<ChipInstruction>();

            int remainingHours = Mathf.Max(
                0,
                totalHours);

            int tenHourChipCount = remainingHours / 10;
            remainingHours -= tenHourChipCount * 10;

            int fiveHourChipCount = remainingHours / 5;
            remainingHours -= fiveHourChipCount * 5;

            int oneHourChipCount = remainingHours;

            for (int index = 0;
                 index < tenHourChipCount;
                 index++)
            {
                plan.Add(
                    new ChipInstruction(
                        _tenHourChipTemplate));
            }

            for (int index = 0;
                 index < fiveHourChipCount;
                 index++)
            {
                plan.Add(
                    new ChipInstruction(
                        _fiveHourChipTemplate));
            }

            for (int index = 0;
                 index < oneHourChipCount;
                 index++)
            {
                plan.Add(
                    new ChipInstruction(
                        _oneHourChipTemplate));
            }

            return plan;
        }

        private Vector3 GetDisplayTargetWorldPosition(
            RectTransform holder,
            int index,
            int totalChipCount)
        {
            int chipsPerRow = Mathf.Max(
                1,
                _maximumChipsPerRow);

            int row = index / chipsPerRow;
            int column = index % chipsPerRow;

            Vector3 localOffset = new(
                column * _chipSpacing,
                -row * (_chipSpacing * _rowSpacingMultiplier),
                0f);

            return holder.TransformPoint(localOffset);
        }

        private bool CanDisplayChips()
        {
            return _effectsLayer != null
                && _oneHourChipTemplate != null
                && _fiveHourChipTemplate != null
                && _tenHourChipTemplate != null;
        }

        private void StartChipCoroutine(
            IEnumerator routine)
        {
            Coroutine coroutine =
                StartCoroutine(routine);

            _chipCoroutines.Add(coroutine);
        }

        private void StopCurrentChipMotion()
        {
            if (_chipSequenceCoroutine != null)
            {
                StopCoroutine(_chipSequenceCoroutine);
                _chipSequenceCoroutine = null;
            }

            foreach (Coroutine coroutine in _chipCoroutines)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }

            _chipCoroutines.Clear();
        }

        private void ClearWagerChipsImmediately()
        {
            ClearTrackedChipList(_wagerChips);
        }

        private void ClearWinningsChipsImmediately()
        {
            ClearTrackedChipList(_winningsChips);
        }

        private void ClearTrackedChipList(
            List<RectTransform> chips)
        {
            foreach (RectTransform chip in chips)
            {
                if (chip != null)
                    DestroyTrackedChip(chip);
            }

            chips.Clear();
        }

        private void DestroyTrackedChip(
            RectTransform chip)
        {
            if (chip == null)
                return;

            _spawnedChips.Remove(chip.gameObject);
            Destroy(chip.gameObject);
        }

        private void FinishChipSequence()
        {
            _chipSequenceCoroutine = null;
            _chipCoroutines.Clear();
        }

        private static Vector3 GetRectCenterWorld(
            RectTransform rectTransform)
        {
            if (rectTransform == null)
                return Vector3.zero;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            return (corners[0] + corners[2]) * 0.5f;
        }

        private IEnumerator VignetteRoutine(
            bool isEnabled)
        {
            if (!TryGetVignette(
                    out Vignette vignette))
            {
                _vignetteCoroutine = null;
                yield break;
            }

            float startingIntensity =
                vignette.intensity.value;

            float targetIntensity = isEnabled
                ? _riskVignetteIntensity
                : 0f;

            float durationSeconds = Mathf.Max(
                0.0001f,
                _vignetteFadeSeconds);

            float elapsedSeconds = 0f;

            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.deltaTime;

                float normalizedTime = Mathf.Clamp01(
                    elapsedSeconds / durationSeconds);

                float easedTime =
                    1f - Mathf.Pow(
                        1f - normalizedTime,
                        2f);

                vignette.intensity.Override(
                    Mathf.Lerp(
                        startingIntensity,
                        targetIntensity,
                        easedTime));

                yield return null;
            }

            vignette.intensity.Override(targetIntensity);
            _vignetteCoroutine = null;
        }

        private IEnumerator HeartbeatRoutine()
        {
            while (true)
            {
                _heartbeatAudioSource.PlayOneShot(
                    _heartbeatClip,
                    _heartbeatVolume);

                yield return new WaitForSeconds(
                    _heartbeatIntervalSeconds);
            }
        }

        private bool TryGetVignette(
            out Vignette vignette)
        {
            vignette = null;

            if (_globalVolume == null
                || _globalVolume.profile == null)
            {
                return false;
            }

            if (!_globalVolume.profile.TryGet(
                    out vignette))
            {
                vignette =
                    _globalVolume.profile
                        .Add<Vignette>(true);
            }

            return vignette != null;
        }

        private void SetVignetteImmediately(
            float intensity)
        {
            if (TryGetVignette(
                    out Vignette vignette))
            {
                vignette.intensity.Override(
                    Mathf.Clamp01(intensity));
            }
        }

        private readonly struct ChipInstruction
        {
            public RectTransform Template { get; }

            public ChipInstruction(
                RectTransform template)
            {
                Template = template;
            }
        }

        private sealed class AnimationContext
        {
            public int ExpectedCount;
            public int CompletedCount;
        }
    }
}