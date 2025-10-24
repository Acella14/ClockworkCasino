using UnityEngine;

namespace ClockworkCasino.Persistence
{
    public static class PlayerProgress
    {
        private const string KEY_CURRENT  = "cc.currentBuiltUpS";
        private const string KEY_HIGHEST  = "cc.highestBuiltUpS";
        private const string KEY_DEATHS   = "cc.totalDeaths";

        public static int CurrentBuiltUpSeconds => PlayerPrefs.GetInt(KEY_CURRENT, 0);
        public static int HighestBuiltUpSeconds => PlayerPrefs.GetInt(KEY_HIGHEST, 0);
        public static int TotalDeaths           => PlayerPrefs.GetInt(KEY_DEATHS, 0);

        // When a clean finish happens (cash out)
        public static void AddCashoutGain(int gainedSeconds)
        {
            if (gainedSeconds <= 0) return;

            int newCurrent = CurrentBuiltUpSeconds + gainedSeconds;
            PlayerPrefs.SetInt(KEY_CURRENT, newCurrent);

            if (newCurrent > HighestBuiltUpSeconds)
                PlayerPrefs.SetInt(KEY_HIGHEST, newCurrent);

            PlayerPrefs.Save();
        }

        // When the player dies
        public static void OnDeath()
        {
            PlayerPrefs.SetInt(KEY_CURRENT, 0);
            PlayerPrefs.SetInt(KEY_DEATHS, TotalDeaths + 1);
            PlayerPrefs.Save();
        }

        // Shared formatter for “time gained” display (maps game seconds -> real time via secondsPerTomorrow)
        public static string FormatLife(int gameSeconds, int secondsPerTomorrow)
        {
            secondsPerTomorrow = Mathf.Max(1, secondsPerTomorrow);
            float totalMinutes = gameSeconds * (24f * 60f) / secondsPerTomorrow;
            int mins  = Mathf.RoundToInt(totalMinutes);
            int hours = mins / 60;
            int rem   = mins % 60;
            return $"{hours}h {rem}m";
        }

        // Dev helper to clear everything
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KEY_CURRENT);
            PlayerPrefs.DeleteKey(KEY_HIGHEST);
            PlayerPrefs.DeleteKey(KEY_DEATHS);
            PlayerPrefs.Save();
        }
    }
}
