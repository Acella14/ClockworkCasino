using System;
using ClockworkCasino.Cards;
using ClockworkCasino.Rules;

namespace ClockworkCasino.Gameplay
{
    public sealed class GeneratedRound
    {
        public RuleDefinition Rule { get; }

        public CardData[] Cards { get; }

        public RoundObjective Objective { get; }

        public string DisplayText =>
            Objective?.DisplayText
            ?? Rule?.DisplayText
            ?? string.Empty;

        public GeneratedRound(
            RuleDefinition rule,
            CardData[] cards,
            RoundObjective objective)
        {
            Rule = rule
                ?? throw new ArgumentNullException(
                    nameof(rule));

            Cards = cards
                ?? throw new ArgumentNullException(
                    nameof(cards));

            Objective = objective
                ?? throw new ArgumentNullException(
                    nameof(objective));
        }
    }
}