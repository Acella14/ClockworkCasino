using System.Collections.Generic;
using UnityEngine;

namespace ClockworkCasino.Audio
{
    public enum SfxEvent
    {
        UiClick, UiToggle,
        NewRound, ShowBuyIn, IntermissionOpen, Cashout, EndClean, EndBusted,
        Borrow, BorrowDenied, BorrowOverflow,
        SuddenDeathOn, SuddenDeathClear,
        DeckSlideToPreview, CardSpread, CardDeal, CardFlip,
        SelectStart, SelectGood, SelectBad,
        ChipInsert,
        RoundWin, RoundLose, Timeout,
        RiskStart, RiskSuccess, RiskFail
    }
    public class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private SfxBank _bank;
        [SerializeField] private AudioSource _oneShot;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;

        private readonly Dictionary<SfxEvent, float> _lastPlayTime = new();

        public void Play(SfxEvent evt, float volume = 1f)
        {
            var clip = GetClip(evt);
            if (!clip || !_oneShot) return;
            _oneShot.pitch = 1f;
            _oneShot.PlayOneShot(clip, _masterVolume * Mathf.Clamp01(volume));
        }

        public void PlayVaried(SfxEvent evt, float volume = 1f, float pitchJitter = 0.05f, float minInterval = 0.0f)
        {
            if (minInterval > 0f)
            {
                float now = Time.unscaledTime;
                if (_lastPlayTime.TryGetValue(evt, out var last) && now - last < minInterval) return;
                _lastPlayTime[evt] = now;
            }

            var clip = GetClip(evt);
            if (!clip || !_oneShot) return;

            float p = 1f + Random.Range(-pitchJitter, pitchJitter);
            _oneShot.pitch = p;
            _oneShot.PlayOneShot(clip, _masterVolume * Mathf.Clamp01(volume));
            _oneShot.pitch = 1f;
        }

        private AudioClip GetClip(SfxEvent evt)
        {
            if (_bank == null) return null;
            switch (evt)
            {
                case SfxEvent.UiClick:            return _bank.uiClick;
                case SfxEvent.UiToggle:           return _bank.uiToggle;

                case SfxEvent.NewRound:           return _bank.newRound;
                case SfxEvent.ShowBuyIn:          return _bank.showBuyIn;
                case SfxEvent.IntermissionOpen:   return _bank.intermissionOpen;
                case SfxEvent.Cashout:            return _bank.cashout;
                case SfxEvent.EndClean:           return _bank.endClean;
                case SfxEvent.EndBusted:          return _bank.endBusted;

                case SfxEvent.Borrow:             return _bank.borrow;
                case SfxEvent.BorrowDenied:       return _bank.borrowDenied;
                case SfxEvent.BorrowOverflow:     return _bank.borrowOverflow;

                case SfxEvent.SuddenDeathOn:      return _bank.suddenDeathOn;
                case SfxEvent.SuddenDeathClear:   return _bank.suddenDeathClear;

                case SfxEvent.DeckSlideToPreview: return _bank.deckSlideToPreview;
                case SfxEvent.CardSpread:         return _bank.cardSpread;
                case SfxEvent.CardDeal:           return _bank.cardDeal;
                case SfxEvent.CardFlip:           return _bank.cardFlip;

                case SfxEvent.SelectStart:        return _bank.selectStart;
                case SfxEvent.SelectGood:         return _bank.selectGood;
                case SfxEvent.SelectBad:          return _bank.selectBad;

                case SfxEvent.ChipInsert:         return _bank.chipInsert;

                case SfxEvent.RoundWin:           return _bank.roundWin;
                case SfxEvent.RoundLose:          return _bank.roundLose;
                case SfxEvent.Timeout:            return _bank.timeout;

                case SfxEvent.RiskStart:          return _bank.riskStart;
                case SfxEvent.RiskSuccess:        return _bank.riskSuccess;
                case SfxEvent.RiskFail:           return _bank.riskFail;
            }
            return null;
        }
    }
}
