// CardView.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkCasino.Cards
{
    public class CardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Button _button;
        [SerializeField] private Image _bg;

        [SerializeField] private Color _baseColor = Color.white;
        [SerializeField] private Color _faceDownColor = new Color(0.9f,0.9f,0.9f);
        [SerializeField] private Color _cursedBaseTint = new Color(1.00f, 0.90f, 0.90f);

        RectTransform _rt;
        Vector2 _startAnchoredPos;

        int _index;
        bool _isCorrect;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (_rt) _startAnchoredPos = _rt.anchoredPosition;
        }

        public void SetTint(Color c)
        {
            if (_bg) _bg.color = c;
        }

        public void Bind(int index, CardData data, bool isCorrect, Action<int, bool> onClicked)
        {
            _index = index;
            _isCorrect = isCorrect;

            SetFaceUp(data);
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() =>
            {
                onClicked?.Invoke(_index, _isCorrect);
            });
        }

        public void SetFaceDown()
        {
            if (_label) _label.text = "■";
            if (_bg) _bg.color = _faceDownColor;
            if (_button) _button.interactable = false;
        }

        public void SetFaceUp(CardData data, bool? isCorrect = null)
        {
            string suitChar = data.suit switch
            {
                Suit.Clubs => "♣",
                Suit.Diamonds => "♦",
                Suit.Hearts => "♥",
                Suit.Spades => "♠",
                _ => "?"
            };

            if (_label)
            {
                _label.text = $"{data.value}{suitChar}";
                if (data.cursed) _label.text += " ×";
            }

            if (_bg) _bg.color = data.cursed ? _cursedBaseTint : _baseColor;

            if (isCorrect.HasValue) _isCorrect = isCorrect.Value;
            if (_button) _button.interactable = true;
        }

        public void SetInteractable(bool canClick)
        {
            if (_button) _button.interactable = canClick;
        }

        public IEnumerator RaiseThenFlash(float raisePixels, float raiseSeconds, Color flash, float flashSeconds)
        {
            // raise
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
            // flash
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
                Vector2 to   = from + new Vector2(0f, raisePixels);
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
