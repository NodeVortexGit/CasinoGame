using System;
using System.Collections.Generic;
using System.Text;

namespace Casino.Core.Region
{
    /// <summary>
    /// Offline latitude/longitude -> ISO country code lookup over Natural Earth 1:50m borders (public domain),
    /// pre-packed by Tools/build_geo.py. No network involved, so the result cannot be spoofed with a VPN.
    /// </summary>
    public sealed class CountryLookup
    {
        sealed class Country
        {
            public string Code;
            public short MinX, MinY, MaxX, MaxY;
            public short[][] Rings; // interleaved x,y in hundredths of a degree
        }

        readonly List<Country> countries;

        CountryLookup(List<Country> countries) => this.countries = countries;

        public int CountryCount => countries.Count;

        public static CountryLookup Load(byte[] data)
        {
            if (data == null || data.Length < 8 || Encoding.ASCII.GetString(data, 0, 4) != "CGEO")
                throw new FormatException("Not a CGEO country border file.");
            int pos = 4;
            int version = ReadU16(data, ref pos);
            if (version != 1) throw new FormatException("Unsupported CGEO version " + version);
            int count = ReadU16(data, ref pos);
            var list = new List<Country>(count);
            for (int c = 0; c < count; c++)
            {
                var country = new Country { Code = Encoding.ASCII.GetString(data, pos, 2) };
                pos += 2;
                country.MinX = ReadI16(data, ref pos);
                country.MinY = ReadI16(data, ref pos);
                country.MaxX = ReadI16(data, ref pos);
                country.MaxY = ReadI16(data, ref pos);
                int ringCount = ReadU16(data, ref pos);
                country.Rings = new short[ringCount][];
                for (int r = 0; r < ringCount; r++)
                {
                    int points = ReadU16(data, ref pos);
                    var ring = new short[points * 2];
                    for (int i = 0; i < ring.Length; i++) ring[i] = ReadI16(data, ref pos);
                    country.Rings[r] = ring;
                }
                list.Add(country);
            }
            return new CountryLookup(list);
        }

        /// <summary>
        /// Country containing the point, or the nearest country within <paramref name="maxDistanceKm"/> (for spots
        /// just off a simplified coastline). Null if nothing is close, e.g. open sea.
        /// </summary>
        public string Find(double latitude, double longitude, double maxDistanceKm = 30)
        {
            double x = longitude * 100.0, y = latitude * 100.0;
            foreach (var c in countries)
            {
                if (x < c.MinX || x > c.MaxX || y < c.MinY || y > c.MaxY) continue;
                if (Contains(c, x, y)) return c.Code;
            }

            // Not inside any polygon: nearest border within range. Distances in hundredths of a degree,
            // with longitude scaled by cos(latitude) so they are roughly isotropic.
            double kmPerUnit = 1.1132;
            double maxUnits = maxDistanceKm / kmPerUnit;
            double lonScale = Math.Max(0.05, Math.Cos(latitude * Math.PI / 180.0));
            string best = null;
            double bestDist = maxUnits * maxUnits;
            foreach (var c in countries)
            {
                double padX = maxUnits / lonScale;
                if (x < c.MinX - padX || x > c.MaxX + padX || y < c.MinY - maxUnits || y > c.MaxY + maxUnits) continue;
                foreach (var ring in c.Rings)
                {
                    int n = ring.Length / 2;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        double d = SegmentDistanceSq(x * lonScale, y, ring[2 * j] * lonScale, ring[2 * j + 1],
                            ring[2 * i] * lonScale, ring[2 * i + 1]);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = c.Code;
                        }
                    }
                }
            }
            return best;
        }

        static bool Contains(Country c, double x, double y)
        {
            bool inside = false;
            foreach (var ring in c.Rings)
            {
                int n = ring.Length / 2;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = ring[2 * i], yi = ring[2 * i + 1], xj = ring[2 * j], yj = ring[2 * j + 1];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
                }
            }
            return inside;
        }

        static double SegmentDistanceSq(double px, double py, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay;
            double len = dx * dx + dy * dy;
            double t = len > 0 ? ((px - ax) * dx + (py - ay) * dy) / len : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            double ex = ax + t * dx - px, ey = ay + t * dy - py;
            return ex * ex + ey * ey;
        }

        static int ReadU16(byte[] d, ref int pos)
        {
            int v = d[pos] | (d[pos + 1] << 8);
            pos += 2;
            return v;
        }

        static short ReadI16(byte[] d, ref int pos) => (short)ReadU16(d, ref pos);
    }
}
