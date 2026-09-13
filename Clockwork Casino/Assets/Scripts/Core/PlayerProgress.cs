using System;
using System.Globalization;
using UnityEngine;

namespace ClockworkCasino.Persistence
{
    public static class PlayerProgress
    {
        public const int DefaultStartingLifeHours = 24;

        private const long TicksPerHour = TimeSpan.TicksPerHour;

        private const string ExpiryUtcTicksKey =
            "cc.life.expiryUtcTicks";

        private const string HighestLifeHoursKey =
            "cc.life.highestHours";

        private const string TotalDeathsKey =
            "cc.totalDeaths";

        private const string IntroCompletedKey =
            "cc.introCompleted";

        // Previous version's keys, used for automatic migration.
        private const string LegacyRemainingSecondsKey =
            "cc.life.remainingSeconds";

        private const string LegacyHighestSecondsKey =
            "cc.life.highestSeconds";

        public static int TotalDeaths =>
            PlayerPrefs.GetInt(TotalDeathsKey, 0);

        public static bool HasCompletedIntro =>
            PlayerPrefs.GetInt(IntroCompletedKey, 0) == 1;

        public static int CurrentBuiltUpSeconds =>
            GetRemainingHours() * 3600;

        public static int HighestBuiltUpSeconds =>
            GetHighestLifeHours() * 3600;

        public static void MarkIntroCompleted()
        {
            PlayerPrefs.SetInt(IntroCompletedKey, 1);
            PlayerPrefs.Save();
        }

        public static int GetRemainingHours(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            EnsureInitialized(startingLifeHours);

            long expiryTicks = ReadLong(
                ExpiryUtcTicksKey,
                DateTime.UtcNow.Ticks);

            long remainingTicks =
                expiryTicks - DateTime.UtcNow.Ticks;

            if (remainingTicks <= 0)
                return 0;

            return Mathf.Max(
                1,
                (int)Math.Ceiling(
                    remainingTicks / (double)TicksPerHour));
        }

        public static int GetHighestLifeHours(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            EnsureInitialized(startingLifeHours);

            return Mathf.Max(
                startingLifeHours,
                PlayerPrefs.GetInt(
                    HighestLifeHoursKey,
                    startingLifeHours));
        }

        public static bool HasExpired(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            EnsureInitialized(startingLifeHours);

            long expiryTicks = ReadLong(
                ExpiryUtcTicksKey,
                DateTime.UtcNow.Ticks);

            return expiryTicks <= DateTime.UtcNow.Ticks;
        }

        public static bool ResolveOfflineDeathIfNeeded(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            EnsureInitialized(startingLifeHours);

            if (!HasExpired(startingLifeHours))
                return false;

            RecordDeath(startingLifeHours);
            return true;
        }

        public static bool TryCommitHours(
            int hours,
            int startingLifeHours = DefaultStartingLifeHours,
            bool requireTimeRemainingAfterCommit = false)
        {
            hours = Mathf.Max(0, hours);

            if (hours == 0)
                return true;

            int availableHours =
                GetRemainingHours(startingLifeHours);

            bool canAfford = requireTimeRemainingAfterCommit
                ? availableHours > hours
                : availableHours >= hours;

            if (!canAfford)
                return false;

            long expiryTicks = ReadLong(
                ExpiryUtcTicksKey,
                DateTime.UtcNow.Ticks);

            long updatedExpiryTicks =
                expiryTicks - hours * TicksPerHour;

            WriteLong(
                ExpiryUtcTicksKey,
                updatedExpiryTicks);

            PlayerPrefs.Save();
            return true;
        }

        public static void AddHours(
            int hours,
            int startingLifeHours = DefaultStartingLifeHours)
        {
            if (hours <= 0)
                return;

            EnsureInitialized(startingLifeHours);

            long expiryTicks = ReadLong(
                ExpiryUtcTicksKey,
                DateTime.UtcNow.Ticks);

            expiryTicks += hours * TicksPerHour;

            WriteLong(
                ExpiryUtcTicksKey,
                expiryTicks);

            int remainingHours =
                GetRemainingHours(startingLifeHours);

            int previousHighest =
                GetHighestLifeHours(startingLifeHours);

            if (remainingHours > previousHighest)
            {
                PlayerPrefs.SetInt(
                    HighestLifeHoursKey,
                    remainingHours);
            }

            PlayerPrefs.Save();
        }

        public static bool TrySpendHours(
            int hours,
            int startingLifeHours = DefaultStartingLifeHours)
        {
            if (hours <= 0)
                return true;

            // Purchases cannot consume the player's final hour.
            return TryCommitHours(
                hours,
                startingLifeHours,
                requireTimeRemainingAfterCommit: true);
        }

        public static void RecordDeath(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            startingLifeHours = Mathf.Max(
                1,
                startingLifeHours);

            PlayerPrefs.SetInt(
                TotalDeathsKey,
                TotalDeaths + 1);

            long resetExpiryTicks =
                DateTime.UtcNow.Ticks
                + startingLifeHours * TicksPerHour;

            WriteLong(
                ExpiryUtcTicksKey,
                resetExpiryTicks);

            int previousHighest = PlayerPrefs.GetInt(
                HighestLifeHoursKey,
                startingLifeHours);

            PlayerPrefs.SetInt(
                HighestLifeHoursKey,
                Mathf.Max(
                    previousHighest,
                    startingLifeHours));

            // Intro completion is intentionally preserved.
            PlayerPrefs.Save();
        }

        public static void Checkpoint(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            EnsureInitialized(startingLifeHours);
            PlayerPrefs.Save();
        }

        public static string FormatHours(int hours)
        {
            return $"{Mathf.Max(0, hours)}h";
        }

        public static string FormatDuration(double totalSeconds)
        {
            int wholeHours = totalSeconds <= 0d
                ? 0
                : (int)Math.Ceiling(totalSeconds / 3600d);

            return FormatHours(wholeHours);
        }

        public static string FormatLife(
            int totalSeconds,
            int ignoredConversionValue)
        {
            return FormatDuration(totalSeconds);
        }

        public static double GetRemainingLifeSeconds(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            EnsureInitialized(startingLifeHours);

            long expiryTicks = ReadLong(
                ExpiryUtcTicksKey,
                DateTime.UtcNow.Ticks);

            long remainingTicks =
                Math.Max(
                    0,
                    expiryTicks - DateTime.UtcNow.Ticks);

            return remainingTicks
                   / (double)TimeSpan.TicksPerSecond;
        }

        public static double GetHighestLifeSeconds(
            int startingLifeHours = DefaultStartingLifeHours)
        {
            return GetHighestLifeHours(startingLifeHours)
                   * 3600d;
        }

        public static void AddCashoutGain(int gainedSeconds)
        {
            int gainedHours = gainedSeconds <= 0
                ? 0
                : Mathf.CeilToInt(gainedSeconds / 3600f);

            AddHours(gainedHours);
        }

        public static void OnDeath()
        {
            RecordDeath();
        }

        public static void ResetAll(
            int startingLifeHours = DefaultStartingLifeHours,
            bool preserveIntroCompletion = false)
        {
            bool introWasCompleted = HasCompletedIntro;

            PlayerPrefs.DeleteKey(ExpiryUtcTicksKey);
            PlayerPrefs.DeleteKey(HighestLifeHoursKey);
            PlayerPrefs.DeleteKey(TotalDeathsKey);
            PlayerPrefs.DeleteKey(IntroCompletedKey);

            PlayerPrefs.Save();

            EnsureInitialized(startingLifeHours);

            if (preserveIntroCompletion && introWasCompleted)
                MarkIntroCompleted();
        }

        private static void EnsureInitialized(
            int startingLifeHours)
        {
            startingLifeHours = Mathf.Max(
                1,
                startingLifeHours);

            bool changed = false;

            if (!PlayerPrefs.HasKey(ExpiryUtcTicksKey))
            {
                double remainingSeconds =
                    ReadLegacyRemainingSeconds(
                        startingLifeHours);

                long expiryTicks =
                    DateTime.UtcNow.Ticks
                    + (long)(
                        Math.Max(0d, remainingSeconds)
                        * TimeSpan.TicksPerSecond);

                WriteLong(
                    ExpiryUtcTicksKey,
                    expiryTicks);

                changed = true;
            }

            if (!PlayerPrefs.HasKey(HighestLifeHoursKey))
            {
                int highestHours =
                    ReadLegacyHighestHours(
                        startingLifeHours);

                PlayerPrefs.SetInt(
                    HighestLifeHoursKey,
                    highestHours);

                changed = true;
            }

            if (!PlayerPrefs.HasKey(TotalDeathsKey))
            {
                PlayerPrefs.SetInt(TotalDeathsKey, 0);
                changed = true;
            }

            if (changed)
                PlayerPrefs.Save();
        }

        private static double ReadLegacyRemainingSeconds(
            int startingLifeHours)
        {
            if (!PlayerPrefs.HasKey(LegacyRemainingSecondsKey))
                return startingLifeHours * 3600d;

            return ReadDouble(
                LegacyRemainingSecondsKey,
                startingLifeHours * 3600d);
        }

        private static int ReadLegacyHighestHours(
            int startingLifeHours)
        {
            if (!PlayerPrefs.HasKey(LegacyHighestSecondsKey))
                return startingLifeHours;

            double highestSeconds = ReadDouble(
                LegacyHighestSecondsKey,
                startingLifeHours * 3600d);

            return Mathf.Max(
                startingLifeHours,
                (int)Math.Ceiling(
                    highestSeconds / 3600d));
        }

        private static double ReadDouble(
            string key,
            double fallback)
        {
            string storedValue = PlayerPrefs.GetString(
                key,
                fallback.ToString(
                    "R",
                    CultureInfo.InvariantCulture));

            return double.TryParse(
                storedValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsedValue)
                    ? parsedValue
                    : fallback;
        }

        private static long ReadLong(
            string key,
            long fallback)
        {
            string storedValue = PlayerPrefs.GetString(
                key,
                fallback.ToString(
                    CultureInfo.InvariantCulture));

            return long.TryParse(
                storedValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long parsedValue)
                    ? parsedValue
                    : fallback;
        }

        private static void WriteLong(
            string key,
            long value)
        {
            PlayerPrefs.SetString(
                key,
                value.ToString(
                    CultureInfo.InvariantCulture));
        }
    }
}