using System;
using System.Collections.Generic;
using UnityEngine;
using ClockworkCasino.Rules;

namespace ClockworkCasino.Cards
{
    public class CardDealer : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform _cardParent;
        [SerializeField] private CardView _cardPrefab;

        CardData[] _currentCards = Array.Empty<CardData>();
        HashSet<int> _finalCorrect = new();
        Action<int, bool> _onChosen;

        System.Random _rng = new();

        public void Deal(int count, RuleDefinition rule, Action<int, bool> onChosen)
        {
            Clear();
            _onChosen = onChosen;

            // Generate random cards
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

            // Compute final correct set and mark curses inside _currentCards
            _finalCorrect = RuleRuntime.GetCorrectAfterCurses(_currentCards, rule);

            // Spawn views
            for (int i = 0; i < count; i++)
            {
                var view = Instantiate(_cardPrefab, _cardParent);
                bool isCorrect = _finalCorrect.Contains(i);
                view.Bind(i, _currentCards[i], isCorrect, OnCardClicked);
            }
        }

        void OnCardClicked(int index, bool isCorrect)
        {
            _onChosen?.Invoke(index, isCorrect);
            _onChosen = null;
        }

        public void Clear()
        {
            if (_cardParent == null) return;
            for (int i = _cardParent.childCount - 1; i >= 0; i--)
                Destroy(_cardParent.GetChild(i).gameObject);
        }
    }
}
