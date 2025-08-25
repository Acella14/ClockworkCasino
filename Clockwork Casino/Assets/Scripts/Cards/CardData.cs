using System;

namespace ClockworkCasino.Cards
{
    [Serializable]
    public struct CardData
    {
        public int value;
        public Suit suit;
        public bool cursed;
    }

    public enum Suit { Clubs, Diamonds, Hearts, Spades }
}
