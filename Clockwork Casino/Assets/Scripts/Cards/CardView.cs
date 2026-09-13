using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ClockworkCasino.Cards
{
    public enum CurseVisualMode
    {
        None,
        ColorReversedSuit,
        Stealth
    }

    public sealed class CardView : MonoBehaviour
    {
        [Header("Core")]
        [FormerlySerializedAs("_button")]
        [SerializeField]
        private Button _button;

        [FormerlySerializedAs("_bg")]
        [SerializeField]
        private Image _background;

        [FormerlySerializedAs("_frontRoot")]
        [SerializeField]
        private GameObject _frontRoot;

        [FormerlySerializedAs("_backRoot")]
        [SerializeField]
        private GameObject _backRoot;

        [Header("Rank")]
        [FormerlySerializedAs("_rankTL")]
        [SerializeField]
        private TMP_Text _topLeftRank;

        [FormerlySerializedAs("_rankBR")]
        [SerializeField]
        private TMP_Text _bottomRightRank;

        [FormerlySerializedAs("_rankSizeSingle")]
        [SerializeField]
        private float _singleCharacterRankSize = 64f;

        [FormerlySerializedAs("_rankSizeDouble")]
        [SerializeField]
        private float _doubleCharacterRankSize = 54f;

        [FormerlySerializedAs("_rankTightenForDouble")]
        [SerializeField]
        private float _doubleCharacterSpacing = -4f;

        [FormerlySerializedAs("_rankOffsetDoubleX")]
        [SerializeField]
        private float _doubleCharacterHorizontalOffset = 8f;

        [Header("Suit")]
        [FormerlySerializedAs("_suitCenter")]
        [SerializeField]
        private Image _centerSuit;

        [FormerlySerializedAs("_suitSprites")]
        [SerializeField]
        private Sprite[] _normalSuitSprites = new Sprite[4];

        [FormerlySerializedAs("_suitSpritesCursed")]
        [SerializeField]
        private Sprite[] _reversedColorSuitSprites =
            new Sprite[4];

        [Header("Selection State")]
        [Tooltip(
            "Optional. Shows 1, 2, 3, etc. for ordered rules.")]
        [SerializeField]
        private TMP_Text _selectionOrderText;

        [Header("Colors")]
        [FormerlySerializedAs("_baseColor")]
        [SerializeField]
        private Color _faceUpBackgroundColor =
            Color.white;

        [FormerlySerializedAs("_faceDownColor")]
        [SerializeField]
        private Color _faceDownBackgroundColor =
            Color.white;

        [FormerlySerializedAs("_rankColorBlack")]
        [SerializeField]
        private Color _blackRankColor =
            new Color32(20, 20, 20, 255);

        [FormerlySerializedAs("_rankColorRed")]
        [SerializeField]
        private Color _redRankColor =
            new Color32(190, 35, 35, 255);

        private RectTransform _rectTransform;

        private Vector2 _topLeftRankBasePosition;
        private Vector2 _bottomRightRankBasePosition;

        private Vector3 _suitBaseScale =
            Vector3.one;

        private Quaternion _suitBaseRotation =
            Quaternion.identity;

        private Vector2 _suitBasePosition;

        private int _cardIndex;
        private Action<int> _selectionCallback;

        private void Awake()
        {
            _rectTransform =
                GetComponent<RectTransform>();

            CacheBaseTextPositions();
            CacheBaseSuitTransform();

            if (_button != null)
            {
                _button.onClick.AddListener(
                    HandleButtonClicked);
            }

            ShowFaceDown();
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(
                    HandleButtonClicked);
            }
        }

        public void ShowFaceDown()
        {
            SetFaceVisible(faceUp: false);
            ClearSelection();
            ClearSelectionState();

            if (_background != null)
            {
                _background.color =
                    _faceDownBackgroundColor;
            }
        }

        public void ShowFaceUp(
            CardData card,
            CurseVisualMode curseVisualMode)
        {
            SetFaceVisible(faceUp: true);
            ClearSelection();
            ClearSelectionState(resetTint: true);

            string rankText =
                GetRankText(card.value);

            Color symbolColor =
                GetSymbolColor(card);

            ApplyRank(
                _topLeftRank,
                rankText,
                symbolColor,
                isTopLeft: true);

            ApplyRank(
                _bottomRightRank,
                rankText,
                symbolColor,
                isTopLeft: false);

            ApplySuit(card);
        }

        public void ConfigureSelection(
            int cardIndex,
            Action<int> selectionCallback)
        {
            _cardIndex = cardIndex;
            _selectionCallback = selectionCallback;

            SetInteractable(true);
        }

        public void ClearSelection()
        {
            _selectionCallback = null;
            SetInteractable(false);
        }

        public void SetInteractable(bool canSelect)
        {
            if (_button != null)
                _button.interactable = canSelect;
        }

        public void SetTint(Color tint)
        {
            if (_background != null)
                _background.color = tint;
        }

        public void ResetFaceTint()
        {
            if (_background != null)
            {
                _background.color =
                    _faceUpBackgroundColor;
            }
        }

        public void ShowSelectionState(
            Color selectionTint,
            int selectionPosition,
            bool showOrderNumber)
        {
            SetTint(selectionTint);

            if (_selectionOrderText == null)
                return;

            bool shouldShowOrder =
                showOrderNumber
                && selectionPosition >= 0;

            _selectionOrderText.gameObject.SetActive(
                shouldShowOrder);

            _selectionOrderText.text =
                shouldShowOrder
                    ? (selectionPosition + 1).ToString()
                    : string.Empty;
        }

        public void ClearSelectionState(
            bool resetTint = false)
        {
            if (_selectionOrderText != null)
            {
                _selectionOrderText.text =
                    string.Empty;

                _selectionOrderText.gameObject.SetActive(
                    false);
            }

            if (resetTint)
                ResetFaceTint();
        }

        public IEnumerator Raise(
            float distancePixels,
            float durationSeconds)
        {
            if (_rectTransform == null)
                yield break;

            Vector2 startingPosition =
                _rectTransform.anchoredPosition;

            Vector2 targetPosition =
                startingPosition
                + new Vector2(
                    0f,
                    distancePixels);

            if (durationSeconds <= 0f)
            {
                _rectTransform.anchoredPosition =
                    targetPosition;

                yield break;
            }

            float elapsedSeconds = 0f;

            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.deltaTime;

                float normalizedTime =
                    Mathf.Clamp01(
                        elapsedSeconds / durationSeconds);

                _rectTransform.anchoredPosition =
                    Vector2.Lerp(
                        startingPosition,
                        targetPosition,
                        normalizedTime);

                yield return null;
            }

            _rectTransform.anchoredPosition =
                targetPosition;
        }

        private void CacheBaseTextPositions()
        {
            if (_topLeftRank != null)
            {
                _topLeftRankBasePosition =
                    _topLeftRank.rectTransform
                        .anchoredPosition;
            }

            if (_bottomRightRank != null)
            {
                _bottomRightRankBasePosition =
                    _bottomRightRank.rectTransform
                        .anchoredPosition;
            }
        }

        private void CacheBaseSuitTransform()
        {
            if (_centerSuit == null)
                return;

            RectTransform suitRectTransform =
                _centerSuit.rectTransform;

            _suitBaseScale =
                suitRectTransform.localScale;

            _suitBaseRotation =
                suitRectTransform.localRotation;

            _suitBasePosition =
                suitRectTransform.anchoredPosition;
        }

        private void ApplyRank(
            TMP_Text rankComponent,
            string rankText,
            Color rankColor,
            bool isTopLeft)
        {
            if (rankComponent == null)
                return;

            bool hasTwoCharacters =
                rankText.Length > 1;

            rankComponent.enableAutoSizing = false;

            rankComponent.fontSize = hasTwoCharacters
                ? _doubleCharacterRankSize
                : _singleCharacterRankSize;

            rankComponent.characterSpacing =
                hasTwoCharacters
                    ? _doubleCharacterSpacing
                    : 0f;

            rankComponent.text = rankText;
            rankComponent.color = rankColor;

            Vector2 basePosition = isTopLeft
                ? _topLeftRankBasePosition
                : _bottomRightRankBasePosition;

            float direction =
                isTopLeft ? 1f : -1f;

            float horizontalOffset =
                hasTwoCharacters
                    ? direction
                      * _doubleCharacterHorizontalOffset
                    : 0f;

            rankComponent.rectTransform
                .anchoredPosition =
                basePosition
                + new Vector2(
                    horizontalOffset,
                    0f);
        }

        private void ApplySuit(
            CardData card)
        {
            if (_centerSuit == null)
                return;

            RectTransform suitRectTransform =
                _centerSuit.rectTransform;

            suitRectTransform.localScale =
                _suitBaseScale;

            suitRectTransform.localRotation =
                _suitBaseRotation;

            suitRectTransform.anchoredPosition =
                _suitBasePosition;

            int suitIndex =
                Mathf.Clamp(
                    (int)card.suit,
                    0,
                    3);

            Sprite selectedSprite =
                card.cursed
                    ? GetSpriteAt(
                        _reversedColorSuitSprites,
                        suitIndex)
                    : GetSpriteAt(
                        _normalSuitSprites,
                        suitIndex);

            if (selectedSprite == null)
            {
                selectedSprite =
                    GetSpriteAt(
                        _normalSuitSprites,
                        suitIndex);
            }

            _centerSuit.sprite = selectedSprite;
            _centerSuit.enabled =
                selectedSprite != null;

            _centerSuit.color =
                GetSymbolColor(card);
        }

        private Color GetSymbolColor(
            CardData card)
        {
            bool isNormallyRed =
                card.suit == Suit.Hearts
                || card.suit == Suit.Diamonds;

            if (card.cursed)
                isNormallyRed = !isNormallyRed;

            return isNormallyRed
                ? _redRankColor
                : _blackRankColor;
        }

        private void SetFaceVisible(bool faceUp)
        {
            if (_frontRoot != null)
                _frontRoot.SetActive(faceUp);

            if (_backRoot != null)
                _backRoot.SetActive(!faceUp);
        }

        private void HandleButtonClicked()
        {
            _selectionCallback?.Invoke(
                _cardIndex);
        }

        private static Sprite GetSpriteAt(
            Sprite[] sprites,
            int index)
        {
            if (sprites == null
                || index < 0
                || index >= sprites.Length)
            {
                return null;
            }

            return sprites[index];
        }

        private static string GetRankText(int value)
        {
            return value switch
            {
                14 => "A",
                13 => "K",
                12 => "Q",
                11 => "J",
                _ => Mathf.Clamp(
                    value,
                    2,
                    10).ToString()
            };
        }
    }
}