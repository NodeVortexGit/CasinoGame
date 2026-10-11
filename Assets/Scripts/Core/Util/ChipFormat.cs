using System.Globalization;

namespace Casino.Core
{
    public static class ChipFormat
    {
        public static string Full(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>12,500 / 125K / 1.25M / 3.4B - short enough for chips and buttons.</summary>
        public static string Short(long value)
        {
            long abs = value < 0 ? -value : value;
            string sign = value < 0 ? "-" : "";
            if (abs < 100_000) return sign + abs.ToString("N0", CultureInfo.InvariantCulture);
            if (abs < 1_000_000) return sign + Trim(abs / 1_000d) + "K";
            if (abs < 1_000_000_000) return sign + Trim(abs / 1_000_000d) + "M";
            return sign + Trim(abs / 1_000_000_000d) + "B";
        }

        static string Trim(double v)
        {
            string fmt = v >= 100 ? "0" : v >= 10 ? "0.#" : "0.##";
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }
    }
}
