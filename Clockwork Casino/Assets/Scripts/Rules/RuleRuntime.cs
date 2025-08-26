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
                        int targetVal = ranking[0].value;
                        foreach (var r in ranking)
                        {
                            if (r.value == targetVal) final.Add(r.index);
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

            bool guard = rule.CurseMode == CurseMode.OneOfValids || rule.CurseMode == CurseMode.HalfOfValids;
            if (guard && candidates.Count == 1)
                return cursed; // leave the only valid un-cursed

            switch (rule.CurseMode)
            {
                case CurseMode.OneOfValids:
                    {
                        // candidates.Count >= 2 guaranteed by the guard above
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
                        // Clamp so at least one valid remains un-cursed
                        int take = Mathf.Clamp(candidates.Count / 2, 1, candidates.Count - 1);
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
                        // If the rule's domain == initial valids (e.g., all non-avoids have the same value),
                        // cursing ALL valids would trap the player. Leave one un-cursed in that case.
                        bool isFilteredRule = rule.Type == RuleType.AvoidSuit || rule.Type == RuleType.PickColor;

                        int domainCount = 0;
                        if (rule.Type == RuleType.AvoidSuit)
                        {
                            for (int i = 0; i < cards.Length; i++)
                                if (cards[i].suit != rule.AvoidSuit) domainCount++;
                        }
                        else if (rule.Type == RuleType.PickColor)
                        {
                            bool wantRed = rule.Color == ColorFilter.Red;
                            for (int i = 0; i < cards.Length; i++)
                            {
                                bool isRed = cards[i].suit == Suit.Hearts || cards[i].suit == Suit.Diamonds;
                                if (isRed == wantRed) domainCount++;
                            }
                        }
                        else
                        {
                            // Unfiltered rules (Highest/Lowest/etc.): domain is all cards
                            domainCount = cards.Length;
                        }

                        if (isFilteredRule && candidates.Count == domainCount && domainCount > 0)
                        {
                            // Leave one of the initial valids un-cursed to keep the round solvable.
                            Shuffle(candidates);
                            for (int k = 1; k < candidates.Count; k++)
                            {
                                int idx = candidates[k];
                                MarkCursed(cards, idx);
                                cursed.Add(idx);
                            }
                        }
                        else
                        {
                            foreach (var idx in candidates)
                            {
                                MarkCursed(cards, idx);
                                cursed.Add(idx);
                            }
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




        //Invalid round protection
        public static HashSet<int> ComputeInitialValids(CardData[] cards, RuleDefinition rule)
            => GetInitialValids(cards, rule);

        // Mutates the given hand just enough so that at least one initial valid exists for the rule.
        public static void EnsureAtLeastOneValid(CardData[] cards, RuleDefinition rule, System.Random rng)
        {
            var valids = GetInitialValids(cards, rule);
            if (valids.Count > 0) return; // already solvable

            switch (rule.Type)
            {
                case RuleType.AvoidSuit:
                {
                    // If all cards are the avoided suit, flip ONE to a non-avoided suit.
                    int i = rng.Next(0, cards.Length);
                    if (cards[i].suit == rule.AvoidSuit)
                    {
                        // pick any of the other three suits
                        Suit[] all = { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades };
                        var options = new System.Collections.Generic.List<Suit>();
                        foreach (var s in all) if (s != rule.AvoidSuit) options.Add(s);
                        cards[i].suit = options[rng.Next(0, options.Count)];
                    }
                    break;
                }

                case RuleType.PickColor:
                {
                    // If no cards of the requested color exist, flip ONE card to that color.
                    bool wantRed = rule.Color == ColorFilter.Red;
                    int i = rng.Next(0, cards.Length);
                    if (wantRed)
                        cards[i].suit = (rng.Next(0, 2) == 0) ? Suit.Hearts : Suit.Diamonds;
                    else
                        cards[i].suit = (rng.Next(0, 2) == 0) ? Suit.Clubs  : Suit.Spades;
                    break;
                }

                case RuleType.SecondHighest:
                case RuleType.SecondLowest:
                {
                    // Ensure at least two distinct values exist.
                    var seen = new System.Collections.Generic.HashSet<int>();
                    for (int k = 0; k < cards.Length; k++) seen.Add(cards[k].value);

                    if (seen.Count < 2)
                    {
                        int idx = rng.Next(0, cards.Length);
                        int newVal = cards[idx].value;
                        // Pick any value != current “one-and-only” value
                        int guard = 0;
                        do { newVal = rng.Next(2, 15); } while (newVal == cards[idx].value && ++guard < 32);
                        cards[idx].value = newVal;
                    }
                    break;
                }

                // Highest / Lowest always have a valid set by definition so nothing to do
                default: break;
            }
        }
    }
}
