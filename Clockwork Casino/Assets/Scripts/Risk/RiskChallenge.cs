using System.Collections.Generic;
using UnityEngine;
using ClockworkCasino.Cards;

namespace ClockworkCasino.Risk
{
    public abstract class RiskChallenge : ScriptableObject
    {
        [Header("Risk Metadata")]
        public string Title = "Risk Challenge";
        [Min(1)] public int CardCount = 5;
        [Range(1f, 30f)] public float TimeSeconds = 6f;

        // Generate a fresh hand that satisfies the challenge’s constraint(s).
        public abstract CardData[] GenerateHand(System.Random rng);

        // Return the set of correct indices for the given hand.
        public abstract HashSet<int> Evaluate(CardData[] hand);
    }
}
