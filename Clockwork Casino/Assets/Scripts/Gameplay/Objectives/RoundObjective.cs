using System;
using System.Collections.Generic;
using System.Linq;

namespace ClockworkCasino.Gameplay
{
    public enum SelectionMode
    {
        Single,
        Multiple,
        Ordered
    }

    /// <summary>
    /// Describes what the player must select to complete a card round.
    ///
    /// The objective contains no Unity presentation or input behavior.
    /// It can therefore be used by click selection, a moving selector,
    /// memory rounds, dragging, or future table formats.
    /// </summary>
    public sealed class RoundObjective
    {
        private readonly HashSet<int> _validIndices;
        private readonly int[] _correctOrder;

        public string DisplayText { get; }

        public SelectionMode SelectionMode { get; }

        public int RequiredSelectionCount { get; }

        /// <summary>
        /// Used by unordered single and multiple-selection objectives.
        /// Any index in this collection is considered a valid target.
        /// </summary>
        public IReadOnlyCollection<int> ValidIndices =>
            _validIndices;

        /// <summary>
        /// Used only by ordered objectives.
        /// </summary>
        public IReadOnlyList<int> CorrectOrder =>
            _correctOrder;

        /// <summary>
        /// When true, clicking an already selected card removes it.
        /// Intended for unordered multi-selection rules.
        /// </summary>
        public bool AllowDeselection { get; }

        /// <summary>
        /// When true, an invalid click immediately fails the round.
        /// This preserves the fast, clutch-friendly behavior of the
        /// current game.
        /// </summary>
        public bool FailImmediatelyOnMistake { get; }

        private RoundObjective(
            string displayText,
            SelectionMode selectionMode,
            int requiredSelectionCount,
            IEnumerable<int> validIndices,
            IEnumerable<int> correctOrder,
            bool allowDeselection,
            bool failImmediatelyOnMistake)
        {
            DisplayText =
                displayText ?? string.Empty;

            SelectionMode = selectionMode;

            _validIndices = validIndices != null
                ? new HashSet<int>(validIndices)
                : new HashSet<int>();

            _correctOrder = correctOrder != null
                ? correctOrder.ToArray()
                : Array.Empty<int>();

            RequiredSelectionCount = Math.Max(
                1,
                requiredSelectionCount);

            AllowDeselection = allowDeselection;
            FailImmediatelyOnMistake =
                failImmediatelyOnMistake;

            Validate();
        }

        public static RoundObjective CreateSingle(
            string displayText,
            IEnumerable<int> validIndices,
            bool failImmediatelyOnMistake = true)
        {
            return new RoundObjective(
                displayText,
                SelectionMode.Single,
                requiredSelectionCount: 1,
                validIndices,
                correctOrder: null,
                allowDeselection: false,
                failImmediatelyOnMistake);
        }

        public static RoundObjective CreateMultiple(
            string displayText,
            IEnumerable<int> requiredIndices,
            bool allowDeselection = true,
            bool failImmediatelyOnMistake = true)
        {
            var indices = requiredIndices != null
                ? new HashSet<int>(requiredIndices)
                : new HashSet<int>();

            return new RoundObjective(
                displayText,
                SelectionMode.Multiple,
                requiredSelectionCount:
                    Math.Max(1, indices.Count),
                validIndices: indices,
                correctOrder: null,
                allowDeselection,
                failImmediatelyOnMistake);
        }

        public static RoundObjective CreateMultiple(
            string displayText,
            IEnumerable<int> validIndices,
            int requiredSelectionCount,
            bool allowDeselection = true,
            bool failImmediatelyOnMistake = true)
        {
            return new RoundObjective(
                displayText,
                SelectionMode.Multiple,
                requiredSelectionCount,
                validIndices,
                correctOrder: null,
                allowDeselection,
                failImmediatelyOnMistake);
        }

        public static RoundObjective CreateOrdered(
            string displayText,
            IEnumerable<int> correctOrder,
            bool failImmediatelyOnMistake = true)
        {
            int[] order = correctOrder?.ToArray()
                ?? Array.Empty<int>();

            return new RoundObjective(
                displayText,
                SelectionMode.Ordered,
                requiredSelectionCount:
                    Math.Max(1, order.Length),
                validIndices: order,
                correctOrder: order,
                allowDeselection: false,
                failImmediatelyOnMistake);
        }

        public bool IsValidIndex(int cardIndex)
        {
            return _validIndices.Contains(cardIndex);
        }

        public bool IsExpectedOrderedIndex(
            int selectionPosition,
            int cardIndex)
        {
            return SelectionMode == SelectionMode.Ordered
                && selectionPosition >= 0
                && selectionPosition < _correctOrder.Length
                && _correctOrder[selectionPosition]
                    == cardIndex;
        }

        private void Validate()
        {
            switch (SelectionMode)
            {
                case SelectionMode.Single:
                    if (_validIndices.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "A single-selection objective requires "
                            + "at least one valid card index.");
                    }

                    break;

                case SelectionMode.Multiple:
                    if (_validIndices.Count
                        < RequiredSelectionCount)
                    {
                        throw new InvalidOperationException(
                            "A multiple-selection objective cannot "
                            + "require more cards than its valid set "
                            + "contains.");
                    }

                    break;

                case SelectionMode.Ordered:
                    if (_correctOrder.Length == 0)
                    {
                        throw new InvalidOperationException(
                            "An ordered objective requires at least "
                            + "one card index.");
                    }

                    if (_correctOrder.Length
                        != RequiredSelectionCount)
                    {
                        throw new InvalidOperationException(
                            "An ordered objective's required count "
                            + "must match its sequence length.");
                    }

                    break;
            }
        }
    }
}