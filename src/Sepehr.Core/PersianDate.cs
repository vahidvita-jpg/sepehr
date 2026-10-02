using System;
using System.Text;

namespace Sepehr.Core
{
    /// <summary>Solar Hijri conversion (jalaali algorithm). Verified against known dates, 1990-2060 round trip.</summary>
    public static class PersianDate
    {
        static readonly int[] Breaks = { -61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178 };
        public static readonly string[] MonthNames = { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

        static void JalCal(int jy, out int gy, out int march, out int leap)
        {
            int bl = Breaks.Length;
            gy = jy + 621;
            int leapJ = -14, jp = Breaks[0], jump = 0, jm;
            if (jy < jp || jy >= Breaks[bl - 1]) throw new ArgumentOutOfRangeException("jy");
            for (int i = 1; i < bl; i++)
            {
                jm = Breaks[i];
                jump = jm - jp;
                if (jy < jm) break;
                leapJ += (jump / 33) * 8 + ((jump % 33) / 4);
                jp = jm;
            }
            int n = jy - jp;
            leapJ += (n / 33) * 8 + (((n % 33) + 3) / 4);
            if ((jump % 33) == 4 && jump - n == 4) leapJ += 1;
            int leapG = (gy / 4) - (((gy / 100) + 1) * 3 / 4) - 150;
            march = 20 + leapJ - leapG;
            if (jump - n < 6) n = n - jump + ((jump + 4) / 33) * 33;
            leap = ((n + 1) % 33 - 1) % 4;
            if (leap == -1) leap = 4;
        }

        static int G2D(int gy, int gm, int gd)
        {
            int d = ((gy + (gm - 8) / 6 + 100100) * 1461) / 4 + (153 * ((gm + 9) % 12) + 2) / 5 + gd - 34840408;
            d = d - (((gy + 100100 + (gm - 8) / 6) / 100) * 3) / 4 + 752;
            return d;
        }

        static void D2G(int jdn, out int gy, out int gm, out int gd)
        {
            int j = 4 * jdn + 139361631;
            j = j + (((4 * jdn + 183187720) / 146097) * 3 / 4) * 4 - 3908;
            int i = ((j % 1461) / 4) * 5 + 308;
            gd = ((i % 153) / 5) + 1;
            gm = ((i / 153) % 12) + 1;
            gy = j / 1461 - 100100 + (8 - gm) / 6;
        }

        public static void ToJalali(int gy, int gm, int gd, out int jy, out int jm, out int jd)
        {
            int jdn = G2D(gy, gm, gd);
            int g, march, leap;
            D2G(jdn, out g, out gm, out gd);
            jy = g - 621;
            JalCal(jy, out g, out march, out leap);
            int jdn1f = G2D(g, 3, march);
            int k = jdn - jdn1f;
            if (k >= 0)
            {
                if (k <= 185) { jm = 1 + k / 31; jd = (k % 31) + 1; return; }
                k -= 186;
            }
            else
            {
                jy -= 1; k += 179;
                if (leap == 1) k += 1;
            }
            jm = 7 + k / 30;
            jd = (k % 30) + 1;
        }

        public static DateTime ToGregorian(int jy, int jm, int jd)
        {
            int gy, march, leap;
            JalCal(jy, out gy, out march, out leap);
            int jdn = G2D(gy, 3, march) + (jm - 1) * 31 - (jm / 7) * (jm - 7) + jd - 1;
            int y, m, d;
            D2G(jdn, out y, out m, out d);
            return new DateTime(y, m, d);
        }

        /// <summary>"1405/07/10"</summary>
        public static string Format(DateTime dt)
        {
            int y, m, d;
            ToJalali(dt.Year, dt.Month, dt.Day, out y, out m, out d);
            return y.ToString("0000") + "/" + m.ToString("00") + "/" + d.ToString("00");
        }

        public static string LongFormat(DateTime dt)
        {
            int y, m, d;
            ToJalali(dt.Year, dt.Month, dt.Day, out y, out m, out d);
            return d + " " + MonthNames[m - 1] + " " + y;
        }

        public static string WeekdayName(DateTime dt)
        {
            switch (dt.DayOfWeek)
            {
                case DayOfWeek.Saturday: return "شنبه";
                case DayOfWeek.Sunday: return "یکشنبه";
                case DayOfWeek.Monday: return "دوشنبه";
                case DayOfWeek.Tuesday: return "سه‌شنبه";
                case DayOfWeek.Wednesday: return "چهارشنبه";
                case DayOfWeek.Thursday: return "پنجشنبه";
                default: return "جمعه";
            }
        }

        /// <summary>0 = Saturday ... 6 = Friday (matches database day_of_week).</summary>
        public static int DayIndex(DateTime dt) { return ((int)dt.DayOfWeek + 1) % 7; }

        public static string ToPersianDigits(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s) sb.Append(ch >= '0' && ch <= '9' ? (char)('۰' + (ch - '0')) : ch);
            return sb.ToString();
        }

        public static string NormalizeDigits(string s)
        {
            if (s == null) return null;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
                else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
                else sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
