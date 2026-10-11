using System;

namespace Casino.Core.Player
{
    /// <summary>Neutral age screen: asks for a date of birth without revealing the cut-off.</summary>
    public static class AgeGate
    {
        public const int MinimumAge = 18;

        public static bool TryGetBirthDate(int year, int month, int day, DateTime today, out DateTime birthDate, out string error)
        {
            birthDate = default;
            if (year < today.Year - 120 || year > today.Year)
            {
                error = "Please enter a valid year.";
                return false;
            }
            if (month < 1 || month > 12)
            {
                error = "Please enter a valid month (1-12).";
                return false;
            }
            if (day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                error = "Please enter a valid day.";
                return false;
            }
            birthDate = new DateTime(year, month, day);
            if (birthDate > today.Date)
            {
                error = "That date is in the future.";
                return false;
            }
            error = null;
            return true;
        }

        public static int AgeOn(DateTime birthDate, DateTime today)
        {
            int age = today.Year - birthDate.Year;
            if (today.Month < birthDate.Month || (today.Month == birthDate.Month && today.Day < birthDate.Day)) age--;
            return age;
        }

        public static bool IsOldEnough(DateTime birthDate, DateTime today, int minimumAge = MinimumAge) =>
            AgeOn(birthDate, today) >= minimumAge;
    }
}
