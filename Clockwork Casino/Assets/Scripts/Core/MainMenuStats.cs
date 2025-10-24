using UnityEngine;
using TMPro;
using ClockworkCasino.Persistence;

namespace ClockworkCasino.UI
{
    public class MainMenuStats : MonoBehaviour
    {
        [Header("Config (optional for formatting)")]
        [SerializeField] private ClockworkCasino.Core.GameConfig _config;  // for secondsPerTomorrow (defaults to 60 if null)

        [Header("UI")]
        [SerializeField] private TMP_Text _currentBuiltUpText;
        [SerializeField] private TMP_Text _highestBuiltUpText;
        [SerializeField] private TMP_Text _totalDeathsText;

        void OnEnable() => Refresh();

        public void Refresh()
        {
            int spt = _config ? _config.secondsPerTomorrow : 60;

            int current = PlayerProgress.CurrentBuiltUpSeconds;
            int highest = PlayerProgress.HighestBuiltUpSeconds;
            int deaths  = PlayerProgress.TotalDeaths;

            if (_currentBuiltUpText)
                _currentBuiltUpText.text = PlayerProgress.FormatLife(current, spt);

            if (_highestBuiltUpText)
                _highestBuiltUpText.text = PlayerProgress.FormatLife(highest, spt);

            if (_totalDeathsText)
                _totalDeathsText.text = deaths.ToString();
        }
    }
}
