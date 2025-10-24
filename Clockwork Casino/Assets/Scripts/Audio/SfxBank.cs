using UnityEngine;

namespace ClockworkCasino.Audio
{
    [CreateAssetMenu(fileName = "SfxBank", menuName = "ClockworkCasino/SFX Bank")]
    public class SfxBank : ScriptableObject
    {
        [Header("UI")]
        public AudioClip uiClick;
        public AudioClip uiToggle;

        [Header("Flow")]
        public AudioClip newRound;
        public AudioClip showBuyIn;
        public AudioClip intermissionOpen;
        public AudioClip cashout;
        public AudioClip endClean;
        public AudioClip endBusted;

        [Header("Borrow")]
        public AudioClip borrow;
        public AudioClip borrowDenied;
        public AudioClip borrowOverflow;

        [Header("Sudden Death")]
        public AudioClip suddenDeathOn;
        public AudioClip suddenDeathClear;

        [Header("Cards / Dealing")]
        public AudioClip deckSlideToPreview;
        public AudioClip cardSpread;
        public AudioClip cardDeal;
        public AudioClip cardFlip;

        [Header("Selection")]
        public AudioClip selectStart;
        public AudioClip selectGood;
        public AudioClip selectBad;

        [Header("Chips / Meter")]
        public AudioClip chipInsert;

        [Header("Round Results / Timers")]
        public AudioClip roundWin;
        public AudioClip roundLose;
        public AudioClip timeout;        // when a round times out

        [Header("Risk")]
        public AudioClip riskStart;
        public AudioClip riskSuccess;
        public AudioClip riskFail;
    }
}
