using System;
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

        int _index;
        bool _isCorrect;
        Action<int, bool> _onChosen;

        public void Bind(int index, CardData data, bool isCorrect, Action<int, bool> onChosen)
        {
            _index = index;
            _isCorrect = isCorrect;
            _onChosen = onChosen;

            string suitChar = data.suit switch
            {
                Suit.Clubs => "♣",
                Suit.Diamonds => "♦",
                Suit.Hearts => "♥",
                Suit.Spades => "♠",
                _ => "?"
            };

            _label.text = $"{data.value}{suitChar}";
            if (data.cursed) _label.text += " ×";

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onChosen?.Invoke(_index, _isCorrect));

            // placeholder visuals
            _bg.color = data.cursed ? new Color(0.7f, 0.3f, 0.3f) : Color.white;
        }
    }
}
