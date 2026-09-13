using ClockworkCasino.Core;
using ClockworkCasino.Persistence;
using TMPro;
using UnityEngine;

namespace ClockworkCasino.UI
{
    public sealed class MainMenuStats : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private GameConfig _config;

        [Header("UI")]
        [SerializeField]
        private TMP_Text _currentBuiltUpText;

        [SerializeField]
        private TMP_Text _highestBuiltUpText;

        [SerializeField]
        private TMP_Text _totalDeathsText;

        [SerializeField]
        private TMP_Text _statusText;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
                SaveCheckpoint();
        }

        private void OnApplicationQuit()
        {
            SaveCheckpoint();
        }

        public void Refresh()
        {
            int startingLifeHours = _config != null
                ? _config.StartingLifeHours
                : PlayerProgress.DefaultStartingLifeHours;

            bool diedWhileAway =
                PlayerProgress.ResolveOfflineDeathIfNeeded(
                    startingLifeHours);

            int currentHours =
                PlayerProgress.GetRemainingHours(
                    startingLifeHours);

            int highestHours =
                PlayerProgress.GetHighestLifeHours(
                    startingLifeHours);

            if (_currentBuiltUpText != null)
            {
                _currentBuiltUpText.text =
                    PlayerProgress.FormatHours(
                        currentHours);
            }

            if (_highestBuiltUpText != null)
            {
                _highestBuiltUpText.text =
                    PlayerProgress.FormatHours(
                        highestHours);
            }

            if (_totalDeathsText != null)
            {
                _totalDeathsText.text =
                    PlayerProgress.TotalDeaths.ToString();
            }

            if (_statusText != null)
            {
                _statusText.text = diedWhileAway
                    ? "Your time expired while you were away. "
                      + $"You begin again with "
                      + $"{startingLifeHours} hours."
                    : string.Empty;
            }
        }

        private void SaveCheckpoint()
        {
            int startingLifeHours = _config != null
                ? _config.StartingLifeHours
                : PlayerProgress.DefaultStartingLifeHours;

            PlayerProgress.Checkpoint(startingLifeHours);
        }
    }
}