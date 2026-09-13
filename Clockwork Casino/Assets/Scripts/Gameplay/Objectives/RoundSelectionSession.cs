using System;
using System.Collections.Generic;
using System.Linq;

namespace ClockworkCasino.Gameplay
{
    public enum SelectionAttemptStatus
    {
        Ignored,
        Progressed,
        Deselected,
        CompletedCorrectly,
        CompletedIncorrectly
    }

    public readonly struct SelectionAttemptResult
    {
        public SelectionAttemptStatus Status { get; }

        public int CardIndex { get; }

        public int SelectionPosition { get; }

        public int SelectedCount { get; }

        public int RequiredCount { get; }

        public bool RoundCompleted =>
            Status == SelectionAttemptStatus.CompletedCorrectly
            || Status == SelectionAttemptStatus.CompletedIncorrectly;

        public bool WasCorrect =>
            Status == SelectionAttemptStatus.CompletedCorrectly;

        public SelectionAttemptResult(
            SelectionAttemptStatus status,
            int cardIndex,
            int selectionPosition,
            int selectedCount,
            int requiredCount)
        {
            Status = status;
            CardIndex = cardIndex;
            SelectionPosition = selectionPosition;
            SelectedCount = selectedCount;
            RequiredCount = requiredCount;
        }
    }

    /// <summary>
    /// Owns the runtime selection state for one objective.
    /// It contains no visual or Unity-specific behavior.
    /// </summary>
    public sealed class RoundSelectionSession
    {
        private readonly RoundObjective _objective;
        private readonly List<int> _selectionOrder = new();
        private readonly HashSet<int> _selectedIndices = new();

        public RoundObjective Objective =>
            _objective;

        public IReadOnlyList<int> SelectionOrder =>
            _selectionOrder;

        public IReadOnlyCollection<int> SelectedIndices =>
            _selectedIndices;

        public bool HasStarted =>
            _selectionOrder.Count > 0;

        public bool IsComplete { get; private set; }

        public bool WasCorrect { get; private set; }

        public RoundSelectionSession(
            RoundObjective objective)
        {
            _objective = objective
                ?? throw new ArgumentNullException(
                    nameof(objective));
        }

        public SelectionAttemptResult TrySelect(
            int cardIndex)
        {
            if (IsComplete || cardIndex < 0)
            {
                return BuildResult(
                    SelectionAttemptStatus.Ignored,
                    cardIndex,
                    selectionPosition: -1);
            }

            return _objective.SelectionMode switch
            {
                SelectionMode.Single =>
                    HandleSingleSelection(cardIndex),

                SelectionMode.Multiple =>
                    HandleMultipleSelection(cardIndex),

                SelectionMode.Ordered =>
                    HandleOrderedSelection(cardIndex),

                _ =>
                    BuildResult(
                        SelectionAttemptStatus.Ignored,
                        cardIndex,
                        selectionPosition: -1)
            };
        }

        public void Reset()
        {
            _selectionOrder.Clear();
            _selectedIndices.Clear();

            IsComplete = false;
            WasCorrect = false;
        }

        private SelectionAttemptResult HandleSingleSelection(
            int cardIndex)
        {
            AddSelection(cardIndex);

            bool isCorrect =
                _objective.IsValidIndex(cardIndex);

            Complete(isCorrect);

            return BuildResult(
                isCorrect
                    ? SelectionAttemptStatus
                        .CompletedCorrectly
                    : SelectionAttemptStatus
                        .CompletedIncorrectly,
                cardIndex,
                selectionPosition: 0);
        }

        private SelectionAttemptResult HandleMultipleSelection(
            int cardIndex)
        {
            if (_selectedIndices.Contains(cardIndex))
            {
                if (!_objective.AllowDeselection)
                {
                    return BuildResult(
                        SelectionAttemptStatus.Ignored,
                        cardIndex,
                        FindSelectionPosition(cardIndex));
                }

                int removedPosition =
                    FindSelectionPosition(cardIndex);

                _selectedIndices.Remove(cardIndex);
                _selectionOrder.Remove(cardIndex);

                return BuildResult(
                    SelectionAttemptStatus.Deselected,
                    cardIndex,
                    removedPosition);
            }

            if (!_objective.IsValidIndex(cardIndex))
            {
                if (_objective.FailImmediatelyOnMistake)
                {
                    AddSelection(cardIndex);
                    Complete(wasCorrect: false);

                    return BuildResult(
                        SelectionAttemptStatus
                            .CompletedIncorrectly,
                        cardIndex,
                        _selectionOrder.Count - 1);
                }

                return BuildResult(
                    SelectionAttemptStatus.Ignored,
                    cardIndex,
                    selectionPosition: -1);
            }

            AddSelection(cardIndex);

            if (_selectedIndices.Count
                < _objective.RequiredSelectionCount)
            {
                return BuildResult(
                    SelectionAttemptStatus.Progressed,
                    cardIndex,
                    _selectionOrder.Count - 1);
            }

            bool selectedCorrectSet =
                _selectedIndices.Count
                    == _objective.RequiredSelectionCount
                && _selectedIndices.All(
                    _objective.IsValidIndex);

            Complete(selectedCorrectSet);

            return BuildResult(
                selectedCorrectSet
                    ? SelectionAttemptStatus
                        .CompletedCorrectly
                    : SelectionAttemptStatus
                        .CompletedIncorrectly,
                cardIndex,
                _selectionOrder.Count - 1);
        }

        private SelectionAttemptResult HandleOrderedSelection(
            int cardIndex)
        {
            int selectionPosition =
                _selectionOrder.Count;

            bool isExpected =
                _objective.IsExpectedOrderedIndex(
                    selectionPosition,
                    cardIndex);

            if (!isExpected)
            {
                if (_objective.FailImmediatelyOnMistake)
                {
                    AddSelection(cardIndex);
                    Complete(wasCorrect: false);

                    return BuildResult(
                        SelectionAttemptStatus
                            .CompletedIncorrectly,
                        cardIndex,
                        selectionPosition);
                }

                return BuildResult(
                    SelectionAttemptStatus.Ignored,
                    cardIndex,
                    selectionPosition);
            }

            AddSelection(cardIndex);

            if (_selectionOrder.Count
                < _objective.RequiredSelectionCount)
            {
                return BuildResult(
                    SelectionAttemptStatus.Progressed,
                    cardIndex,
                    selectionPosition);
            }

            Complete(wasCorrect: true);

            return BuildResult(
                SelectionAttemptStatus.CompletedCorrectly,
                cardIndex,
                selectionPosition);
        }

        private void AddSelection(int cardIndex)
        {
            _selectionOrder.Add(cardIndex);
            _selectedIndices.Add(cardIndex);
        }

        private void Complete(bool wasCorrect)
        {
            IsComplete = true;
            WasCorrect = wasCorrect;
        }

        private int FindSelectionPosition(int cardIndex)
        {
            return _selectionOrder.IndexOf(cardIndex);
        }

        private SelectionAttemptResult BuildResult(
            SelectionAttemptStatus status,
            int cardIndex,
            int selectionPosition)
        {
            return new SelectionAttemptResult(
                status,
                cardIndex,
                selectionPosition,
                _selectedIndices.Count,
                _objective.RequiredSelectionCount);
        }
    }
}