using System.Globalization;
using System.Text;

namespace Casino.Core
{
    public static class ChipFormat
    {
        /// <summary>How the player's language writes numbers: 1,000.5 / 1.000,5 / 1 000,5 / 1,00,000 (set by the game layer).</summary>
        public static string Group = ",", Decimal = ".";

        /// <summary>Digits in the first group and in the ones after it: 3 and 3, or 3 and 2 for Indian lakh grouping.</summary>
        public static int PrimaryGroup = 3, SecondaryGroup = 3;

        public static string Full(long value)
        {
            if (value < 0) return "-" + Full(-value);
            string digits = value.ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= PrimaryGroup) return digits;
            var sb = new StringBuilder(digits.Length + 8);
            int first = digits.Length - PrimaryGroup;
            // Leading groups of SecondaryGroup digits, then the last PrimaryGroup digits.
            int lead = first % SecondaryGroup;
            if (lead == 0) lead = SecondaryGroup;
            sb.Append(digits, 0, lead);
            for (int i = lead; i < first; i += SecondaryGroup) sb.Append(Group).Append(digits, i, SecondaryGroup);
            sb.Append(Group).Append(digits, first, PrimaryGroup);
            return sb.ToString();
        }

        /// <summary>12,500 / 125K / 1.25M / 3.4B - short enough for chips and buttons.</summary>
        public static string Short(long value)
        {
            long abs = value < 0 ? -value : value;
            string sign = value < 0 ? "-" : "";
            if (abs < 100_000) return sign + Full(abs);
            if (abs < 1_000_000) return sign + Trim(abs / 1_000d) + "K";
            if (abs < 1_000_000_000) return sign + Trim(abs / 1_000_000d) + "M";
            return sign + Trim(abs / 1_000_000_000d) + "B";
        }

        /// <summary>A decimal number with the language's decimal separator (no grouping), e.g. 1.5 / 1,5.</summary>
        public static string Number(double value, string format)
        {
            string s = value.ToString(format, CultureInfo.InvariantCulture);
            return Decimal == "." ? s : s.Replace(".", Decimal);
        }

        static string Trim(double v) => Number(v, v >= 100 ? "0" : v >= 10 ? "0.#" : "0.##");
    }
}
