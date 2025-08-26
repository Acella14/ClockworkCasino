using System.Collections.Generic;
using UnityEngine;
using ClockworkCasino.Cards;

namespace ClockworkCasino.Risk
{
    [CreateAssetMenu(
        fileName = "SevenAteNineChallenge",
        menuName = "ClockworkCasino/Risk/Which One Ate 9?"
    )]
    public class SevenAteNineChallenge : RiskChallenge
    {
        public override CardData[] GenerateHand(System.Random rng)
        {
            int n = Mathf.Max(3, CardCount);

            int sevenIndex = rng.Next(0, n);

            // Ensure at least one 9 exists (different slot if possible)
            int nineIndex = (n > 1) ? (sevenIndex + rng.Next(1, n)) % n : -1;

            var cards = new List<CardData>(n);
            for (int i = 0; i < n; i++)
            {
                int value;
                if (i == sevenIndex) value = 7;
                else if (i == nineIndex) value = 9;
                else
                {
                    do { value = rng.Next(2, 15); } while (value == 7); // exactly one 7
                }

                cards.Add(new CardData
                {
                    value = value,
                    suit  = (Suit)rng.Next(0, 4),
                    cursed = false
                });
            }
            return cards.ToArray();
        }

        public override HashSet<int> Evaluate(CardData[] hand)
        {
            var result = new HashSet<int>();
            for (int i = 0; i < hand.Length; i++)
                if (hand[i].value == 7) result.Add(i);
            return result; // exactly one 7
        }
    }
}
