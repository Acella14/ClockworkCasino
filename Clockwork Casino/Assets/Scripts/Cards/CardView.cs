using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkCasino.Cards
{
    public enum CurseVisualMode { None, ColorReversedSuit, Stealth }

    public class CardView : MonoBehaviour
    {
        [Header("Core UI refs")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _bg;
        [SerializeField] private GameObject _frontRoot;
        [SerializeField] private GameObject _backRoot;

        [Header("Face elements")]
        [SerializeField] private TMP_Text _rankTL;
        [SerializeField] private TMP_Text _rankBR;
        [SerializeField] float _rankSizeSingle = 64f;
        [SerializeField] float _rankSizeDouble = 54f;
        [SerializeField] float _rankTightenForDouble = -4f;
        [SerializeField] float _rankOffsetDoubleX = 8f;
        Vector2 _rankTLBasePos, _rankBRBasePos;
        [SerializeField] private Image _suitCenter;

        [Header("Suit sprites (normal, enum order: Clubs, Diamonds, Hearts, Spades)")]
        [SerializeField] private Sprite[] _suitSprites = new Sprite[4];

        [Header("Suit sprites (cursed, color-reversed)")]
        [SerializeField] private Sprite[] _suitSpritesCursed = new Sprite[4];

        Vector3 _suitBaseScale = Vector3.one;
        Quaternion _suitBaseRot = Quaternion.identity;
        Vector2 _suitBasePos = Vector2.zero;

        [Header("Colors")]
        [SerializeField] private Color _baseColor = Color.white;
        [SerializeField] private Color _faceDownColor = Color.white;
        [SerializeField] private Color _cursedBaseTint = new Color(1.00f, 0.90f, 0.90f);
        [SerializeField] private Color _rankColorBlack = new Color32(20, 20, 20, 255);
        [SerializeField] private Color _rankColorRed = new Color32(190, 35, 35, 255);

        RectTransform _rt;
        Vector2 _startAnchoredPos;

        int _index;
        bool _isCorrect;
        CardData _data;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (_rt) _startAnchoredPos = _rt.anchoredPosition;

            if (_rankTL) _rankTLBasePos = _rankTL.rectTransform.anchoredPosition;
            if (_rankBR) _rankBRBasePos = _rankBR.rectTransform.anchoredPosition;

            if (_suitCenter != null)
            {
                var rt = _suitCenter.rectTransform;
                _suitBaseScale = rt.localScale;
                _suitBaseRot   = rt.localRotation;
                _suitBasePos   = rt.anchoredPosition;
            }

            ToggleFace(true);
        }

        public void SetTint(Color c) { if (_bg) _bg.color = c; }

        public void Bind(int index, CardData data, bool isCorrect, CurseVisualMode curseMode, Action<int, bool> onClicked)
        {
            _index = index;
            _isCorrect = isCorrect;
            _data = data;

            SetFaceUp(_data, isCorrect, curseMode);

            if (_button)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onClicked?.Invoke(_index, _isCorrect));
                _button.interactable = true;
            }
        }

        public void SetFaceDown()
        {
            ToggleFace(false);
            if (_button) _button.interactable = false;
            if (_bg) _bg.color = _faceDownColor;
        }

        public void SetFaceUp(CardData data, bool? isCorrect = null, CurseVisualMode curseMode = CurseVisualMode.ColorReversedSuit)
        {
            _data = data;
            if (isCorrect.HasValue) _isCorrect = isCorrect.Value;

            ToggleFace(true);

            if (_bg) _bg.color = _baseColor;

            // Rank text
            string rank = RankString(data.value);
            if (_rankTL) _rankTL.text = rank;
            if (_rankBR) _rankBR.text = rank;

            bool redSuit = (data.suit == Suit.Hearts || data.suit == Suit.Diamonds);
            var rankColor = redSuit ? _rankColorRed : _rankColorBlack;
            if (_rankTL) _rankTL.color = rankColor;
            if (_rankBR) _rankBR.color = rankColor;

            ApplyRank(_rankTL, rank, rankColor, true);
            ApplyRank(_rankBR, rank, rankColor, false);

            if (_suitCenter)
            {
                var srt = _suitCenter.rectTransform;
                srt.localScale    = _suitBaseScale;
                srt.localRotation = _suitBaseRot;
                srt.anchoredPosition = _suitBasePos;
                _suitCenter.color  = Color.white;
            }

            // Choose sprite & subtlety based on curse mode
            if (_suitCenter)
            {
                int idx = Mathf.Clamp((int)data.suit, 0, 3);
                Sprite sprite = null;

                if (data.cursed)
                {
                    if (curseMode == CurseVisualMode.ColorReversedSuit)
                    {
                        if (_suitSpritesCursed != null && _suitSpritesCursed.Length >= 4)
                            sprite = _suitSpritesCursed[idx];
                    }
                    else if (curseMode == CurseVisualMode.Stealth)
                    {
                        if (_suitSprites != null && _suitSprites.Length >= 4)
                            sprite = _suitSprites[idx];

                        // Subtle discrepancies
                        var srt = _suitCenter.rectTransform;
                        srt.localScale    = _suitBaseScale * 0.92f; // slightly smaller
                        srt.localRotation = Quaternion.Euler(0, 0, -7f); // slight tilt
                        srt.anchoredPosition = _suitBasePos + new Vector2(1.5f, 0f); // px nudge
                        _suitCenter.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                    }
                }

                // Fallbacks for non-cursed or sprite not found
                if (sprite == null)
                {
                    if (_suitSprites != null && _suitSprites.Length >= 4)
                        sprite = _suitSprites[idx];
                }

                _suitCenter.sprite = sprite;
                _suitCenter.enabled = sprite != null;
            }

            if (_button) _button.interactable = true;
        }

        void ApplyRank(TMP_Text t, string rank, Color color, bool isTopLeft)
        {
            if (!t) return;
            bool isDouble = rank.Length > 1;
            t.enableAutoSizing = false;
            t.fontSize = isDouble ? _rankSizeDouble : _rankSizeSingle;
            t.characterSpacing = isDouble ? _rankTightenForDouble : 0f;
            t.text = rank;
            t.color = color;

            var rt = t.rectTransform;
            Vector2 basePos = isTopLeft ? _rankTLBasePos : _rankBRBasePos;
            float dir = isTopLeft ? +1f : -1f;
            float x = isDouble ? dir * _rankOffsetDoubleX : 0;
            rt.anchoredPosition = basePos + new Vector2(x, 0f);
        }

        void ToggleFace(bool faceUp)
        {
            if (_frontRoot) _frontRoot.SetActive(faceUp);
            if (_backRoot) _backRoot.SetActive(!faceUp);
        }

        string RankString(int value)
        {
            switch (value)
            {
                case 14: return "A";
                case 13: return "K";
                case 12: return "Q";
                case 11: return "J";
                default: return Mathf.Clamp(value, 2, 10).ToString();
            }
        }

        public void SetInteractable(bool canClick)
        {
            if (_button) _button.interactable = canClick;
        }

        // === Anim helpers (unchanged) ===
        public IEnumerator RaiseThenFlash(float raisePixels, float raiseSeconds, Color flash, float flashSeconds)
        {
            if (_rt)
            {
                Vector2 from = _rt.anchoredPosition;
                Vector2 to = from + new Vector2(0f, raisePixels);
                float t = 0f;
                while (t < raiseSeconds)
                {
                    t += Time.deltaTime;
                    float a = Mathf.Clamp01(t / raiseSeconds);
                    _rt.anchoredPosition = Vector2.Lerp(from, to, a);
                    yield return null;
                }
            }
            if (_bg)
            {
                Color orig = _bg.color;
                _bg.color = flash;
                yield return new WaitForSeconds(flashSeconds);
                _bg.color = orig;
            }
        }

        public IEnumerator RaiseOnly(float raisePixels, float raiseSeconds)
        {
            if (_rt)
            {
                Vector2 from = _rt.anchoredPosition;
                Vector2 to = from + new Vector2(0f, raisePixels);
                float t = 0f;
                while (t < raiseSeconds)
                {
                    t += Time.deltaTime;
                    float a = Mathf.Clamp01(t / raiseSeconds);
                    _rt.anchoredPosition = Vector2.Lerp(from, to, a);
                    yield return null;
                }
            }
        }

        public void ResetPosition()
        {
            if (_rt) _rt.anchoredPosition = _startAnchoredPos;
        }
    }
}

