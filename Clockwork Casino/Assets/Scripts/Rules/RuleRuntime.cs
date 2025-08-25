using System;
using System.Collections.Generic;
using ClockworkCasino.Cards;
using UnityEngine;

namespace ClockworkCasino.Rules
{
    public static class RuleRuntime
    {
        static System.Random _rng = new();

        /*
        /// Compute the final set of correct indices for a rule, and also mark which cards are cursed.
        /// Algorithm:
        /// 1) Build initial "valid" set from the rule.
        /// 2) Apply curse mode (mark some CardData.cursed = true).
        /// 3) Remove cursed from valid set.
        /// 4) If no valid remain, FALL BACK to next best non-cursed indices (according to the same ranking rule).
        */

        public static HashSet<int> GetCorrectAfterCurses(CardData[] cards, RuleDefinition rule)
        {
            var initial = GetInitialValids(cards, rule);
            var cursedIndices = ApplyCurses(cards, initial, rule);

            // Remove cursed ones from the valid set
            var final = new HashSet<int>(initial);
            foreach (var ci in cursedIndices) final.Remove(ci);

            // make sure at least one valid remains
            if (final.Count == 0)
            {
                // Compute rankings depending on rule type
                var ranking = GetRanking(cards, rule, excludeCursed: true);
                if (ranking.Count > 0)
                {
                    if (IsHighestFamily(rule.Type))
                    {
                        int targetVal = ranking[0].value;
                        foreach (var r in ranking) { if (r.value == targetVal) final.Add(r.index); else break; }
                    }
                    else if (IsLowestFamily(rule.Type))
                    {
                        int targetVal = ranking[ranking.Count - 1].value;
                        for (int i = ranking.Count - 1; i >= 0; i--)
                        {
                            if (ranking[i].value == targetVal) final.Add(ranking[i].index);
                            else break;
                        }
                    }
                }
            }

            return final;
        }

        // --- helpers ---

        static bool IsHighestFamily(RuleType t) =>
            t == RuleType.Highest || t == RuleType.SecondHighest || t == RuleType.PickColor || t == RuleType.AvoidSuit;

        static bool IsLowestFamily(RuleType t) =>
            t == RuleType.Lowest || t == RuleType.SecondLowest;


        static HashSet<int> GetInitialValids(CardData[] cards, RuleDefinition rule)
        {
            var result = new HashSet<int>();

            switch (rule.Type)
            {
                case RuleType.Highest:
                    {
                        int max = int.MinValue;
                        for (int i = 0; i < cards.Length; i++) if (cards[i].value > max) max = cards[i].value;
                        for (int i = 0; i < cards.Length; i++) if (cards[i].value == max) result.Add(i);
                        break;
                    }
                case RuleType.Lowest:
                    {
                        int min = int.MaxValue;
                        for (int i = 0; i < cards.Length; i++) if (cards[i].value < min) min = cards[i].value;
                        for (int i = 0; i < cards.Length; i++) if (cards[i].value == min) result.Add(i);
                        break;
                    }
                case RuleType.SecondHighest:
                    {
                        // find distinct values sorted desc
                        var vals = DistinctValues(cards);
                        vals.Sort((a, b) => b.CompareTo(a));
                        if (vals.Count >= 2)
                        {
                            int target = vals[1];
                            for (int i = 0; i < cards.Length; i++) if (cards[i].value == target) result.Add(i);
                        }
                        else
                        {
                            // if not enough distinct values, fallback to highest
                            return GetInitialValids(cards, RuleType.Highest);
                        }
                        break;
                    }
                case RuleType.SecondLowest:
                    {
                        var vals = DistinctValues(cards);
                        vals.Sort();
                        if (vals.Count >= 2)
                        {
                            int target = vals[1];
                            for (int i = 0; i < cards.Length; i++) if (cards[i].value == target) result.Add(i);
                        }
                        else
                        {
                            // fallback to lowest
                            return GetInitialValids(cards, RuleType.Lowest);
                        }
                        break;
                    }
                case RuleType.PickColor:
                    {
                        bool wantRed = rule.Color == ColorFilter.Red;
                        var filtered = new List<int>();
                        for (int i = 0; i < cards.Length; i++)
                        {
                            bool isRed = (cards[i].suit == Suit.Hearts || cards[i].suit == Suit.Diamonds);
                            if (isRed == wantRed) filtered.Add(i);
                        }
                        if (filtered.Count == 0) break; // nothing valid (edge case)

                        int max = int.MinValue;
                        foreach (var i in filtered) if (cards[i].value > max) max = cards[i].value;
                        foreach (var i in filtered) if (cards[i].value == max) result.Add(i);
                        break;
                    }
                case RuleType.AvoidSuit:
                    {
                        var filtered = new List<int>();
                        for (int i = 0; i < cards.Length; i++)
                        {
                            if (cards[i].suit != rule.AvoidSuit) filtered.Add(i);
                        }
                        if (filtered.Count == 0) break;

                        int max = int.MinValue;
                        foreach (var i in filtered) if (cards[i].value > max) max = cards[i].value;
                        foreach (var i in filtered) if (cards[i].value == max) result.Add(i);
                        break;
                    }
            }

            return result;
        }

        static HashSet<int> GetInitialValids(CardData[] cards, RuleType t)
        {
            return GetInitialValids(cards, new RuleDefinition { Type = t });
        }

        static List<int> DistinctValues(CardData[] cards)
        {
            var set = new HashSet<int>();
            for (int i = 0; i < cards.Length; i++) set.Add(cards[i].value);
            return new List<int>(set);
        }

        // Returns a ranking list based on the rule (for fallback). Excludes cursed if requested.
        // For Highest-like: ranking[0] is best (largest). For Lowest-like: ranking.Last() is best (smallest).
        static List<(int index, int value)> GetRanking(CardData[] cards, RuleDefinition rule, bool excludeCursed)
        {
            var indices = new List<int>();

            switch (rule.Type)
            {
                case RuleType.PickColor:
                {
                    bool wantRed = rule.Color == ColorFilter.Red;
                    for (int i = 0; i < cards.Length; i++)
                    {
                        bool isRed = (cards[i].suit == Suit.Hearts || cards[i].suit == Suit.Diamonds);
                        if (isRed == wantRed) indices.Add(i);
                    }
                    break;
                }
                case RuleType.AvoidSuit:
                {
                    for (int i = 0; i < cards.Length; i++)
                        if (cards[i].suit != rule.AvoidSuit) indices.Add(i);
                    break;
                }
                default:
                {
                    for (int i = 0; i < cards.Length; i++) indices.Add(i);
                    break;
                }
            }

            var list = new List<(int index, int value)>();
            foreach (var i in indices)
            {
                if (excludeCursed && cards[i].cursed) continue;
                list.Add((i, cards[i].value));
            }

            list.Sort((a, b) => a.value.CompareTo(b.value));
            // For Highest-family I want desc order (best first)
            if (IsHighestFamily(rule.Type))
                list.Reverse();

            return list;
        }

        // Apply curses according to policy and mark CardData.cursed=true on selected indices.
        // Returns the set of cursed indices
        static HashSet<int> ApplyCurses(CardData[] cards, HashSet<int> initialValids, RuleDefinition rule)
        {
            var cursed = new HashSet<int>();
            if (rule.CurseMode == CurseMode.None) return cursed;

            // Basic chance gate
            if (rule.CurseProbability > 0f && rule.CurseProbability < 1f)
            {
                double roll = _rng.NextDouble();
                if (roll > rule.CurseProbability) return cursed;
            }

            // candidates for cursing
            var candidates = new List<int>();
            switch (rule.CurseMode)
            {
                case CurseMode.OneOfValids:
                case CurseMode.HalfOfValids:
                case CurseMode.AllValids:
                    candidates.AddRange(initialValids);
                    break;
            }

            if (candidates.Count == 0) return cursed;

            switch (rule.CurseMode)
            {
                case CurseMode.OneOfValids:
                {
                    int pick = _rng.Next(0, candidates.Count);
                    int idx = candidates[pick];
                    MarkCursed(cards, idx);
                    cursed.Add(idx);
                    break;
                }
                case CurseMode.HalfOfValids:
                {
                    // Shuffle
                    Shuffle(candidates);
                    int take = Mathf.Max(1, candidates.Count / 2);
                    for (int k = 0; k < take; k++)
                    {
                        int idx = candidates[k];
                        MarkCursed(cards, idx);
                        cursed.Add(idx);
                    }
                    break;
                }
                case CurseMode.AllValids:
                {
                    foreach (var idx in candidates)
                    {
                        MarkCursed(cards, idx);
                        cursed.Add(idx);
                    }
                    break;
                }
            }

            return cursed;
        }

        static void MarkCursed(CardData[] cards, int idx)
        {
            var cd = cards[idx];
            cd.cursed = true;
            cards[idx] = cd;
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
