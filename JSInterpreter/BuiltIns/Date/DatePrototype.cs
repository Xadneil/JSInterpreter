using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace JSInterpreter
{
    public class DatePrototype : Object
    {
        private const double MinYear = -1000000.0;
        private const double MaxYear = -MinYear;
        private const double MinMonth = -10000000.0;
        private const double MaxMonth = -MinMonth;

        public const int HoursPerDay = 24;
        public const int MinutesPerHour = 60;
        public const int MsPerSecond = 1000;
        public const int MsPerMinute = 60000;
        public const int MsPerHour = 3600000;
        public const long MsPerDay = 86400000;

        private static readonly int[] kDaysInMonths = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        private readonly struct Date
        {
            public Date(int year, int month, int day)
            {
                Year = year;
                Month = month;
                Day = day;
            }

            public readonly int Year;
            public readonly int Month;
            public readonly int Day;

            public void Deconstruct(out int year, out int month, out int day)
            {
                year = Year;
                month = Month;
                day = Day;
            }
        }

        public DatePrototype(DateConstructor constructor, Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));
            DefinePropertyOrThrow("getTimezoneOffset", new PropertyDescriptor(Utils.CreateBuiltinFunction(getTimeZoneOffset, realm: realm), true, false, true));
            DefinePropertyOrThrow("getDate", new PropertyDescriptor(Utils.CreateBuiltinFunction(getDate, realm: realm), true, false, true));
            DefinePropertyOrThrow("getHours", new PropertyDescriptor(Utils.CreateBuiltinFunction(getHours, realm: realm), true, false, true));
            DefinePropertyOrThrow("getMinutes", new PropertyDescriptor(Utils.CreateBuiltinFunction(getMinutes, realm: realm), true, false, true));
            DefinePropertyOrThrow("getMonth", new PropertyDescriptor(Utils.CreateBuiltinFunction(getMonth, realm: realm), true, false, true));
            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(toString, realm: realm), true, false, true));
            DefinePropertyOrThrow("valueOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(valueOf, realm: realm), true, false, true));

        }

        public static Completion valueOf(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (!(@this is DateInstance d))
                return Completion.ThrowTypeError("this is not a Date object");
            if (double.IsNaN(d.PrimitiveValue))
                return Completion.NormalCompletion(new StringValue("Invalid Date"));
            return Completion.NormalCompletion(new NumberValue(d.PrimitiveValue));
        }

        public static Completion toString(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (!(@this is DateInstance d))
                return Completion.ThrowTypeError("this is not a Date object");
            if (double.IsNaN(d.PrimitiveValue))
                return Completion.NormalCompletion(new StringValue("Invalid Date"));

            var clrDate = d.ToDateTime();
            if (clrDate.IsAbrupt()) return clrDate;

            var t = ToLocalTime(clrDate.Other);
            return Completion.NormalCompletion(new StringValue(t.ToString("ddd MMM dd yyyy HH:mm:ss ", CultureInfo.InvariantCulture) + TimeZoneString(t)));
        }

        public static Completion getTimeZoneOffset(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (!(@this is DateInstance d))
                return Completion.ThrowTypeError("this is not a Date object");
            var t = d.PrimitiveValue;
            if (!IsFinite(t))
                return Completion.NormalCompletion(NumberValue.DoubleNaN);
            return Completion.NormalCompletion(new NumberValue((int)(t - LocalTime(t)) / MsPerMinute));
        }

        public static Completion getDate(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (!(@this is DateInstance d))
                return Completion.ThrowTypeError("this is not a Date object");
            var t = d.PrimitiveValue;
            if (!IsFinite(t))
                return Completion.NormalCompletion(NumberValue.DoubleNaN);
            return Completion.NormalCompletion(new NumberValue(DateFromTime(LocalTime(t))));
        }

        public static Completion getHours(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (!(@this is DateInstance d))
                return Completion.ThrowTypeError("this is not a Date object");
            var t = d.PrimitiveValue;
            if (!IsFinite(t))
                return Completion.NormalCompletion(NumberValue.DoubleNaN);
            return Completion.NormalCompletion(new NumberValue(HourFromTime(LocalTime(t))));
        }

        public static Completion getMinutes(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (!(@this is DateInstance d))
                return Completion.ThrowTypeError("this is not a Date object");
            var t = d.PrimitiveValue;
            if (!IsFinite(t))
                return Completion.NormalCompletion(NumberValue.DoubleNaN);
            return Completion.NormalCompletion(new NumberValue(MinFromTime(LocalTime(t))));
        }

        public static Completion getMonth(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (!(@this is DateInstance d))
                return Completion.ThrowTypeError("this is not a Date object");
            var t = d.PrimitiveValue;
            if (!IsFinite(t))
                return Completion.NormalCompletion(NumberValue.DoubleNaN);
            return Completion.NormalCompletion(new NumberValue(MonthFromTime(LocalTime(t))));
        }

        public static int MinFromTime(double t)
        {
            var minutes = System.Math.Floor(t / MsPerMinute) % MinutesPerHour;

            if (minutes < 0)
            {
                minutes += MinutesPerHour;
            }

            return (int)minutes;
        }

        public static int HourFromTime(double t)
        {
            var hours = System.Math.Floor(t / MsPerHour) % HoursPerDay;

            if (hours < 0)
            {
                hours += HoursPerDay;
            }

            return (int)hours;
        }

        public static int DateFromTime(double t)
        {
            var monthFromTime = MonthFromTime(t);
            var dayWithinYear = DayWithinYear(t);

            if (monthFromTime == 0)
            {
                return dayWithinYear + 1;
            }

            if (monthFromTime == 1)
            {
                return dayWithinYear - 30;
            }

            if (monthFromTime == 2)
            {
                return dayWithinYear - 58 - InLeapYear(t);
            }

            if (monthFromTime == 3)
            {
                return dayWithinYear - 89 - InLeapYear(t);
            }

            if (monthFromTime == 4)
            {
                return dayWithinYear - 119 - InLeapYear(t);
            }

            if (monthFromTime == 5)
            {
                return dayWithinYear - 150 - InLeapYear(t);
            }

            if (monthFromTime == 6)
            {
                return dayWithinYear - 180 - InLeapYear(t);
            }

            if (monthFromTime == 7)
            {
                return dayWithinYear - 211 - InLeapYear(t);
            }

            if (monthFromTime == 8)
            {
                return dayWithinYear - 242 - InLeapYear(t);
            }

            if (monthFromTime == 9)
            {
                return dayWithinYear - 272 - InLeapYear(t);
            }

            if (monthFromTime == 10)
            {
                return dayWithinYear - 303 - InLeapYear(t);
            }

            if (monthFromTime == 11)
            {
                return dayWithinYear - 333 - InLeapYear(t);
            }

            throw new InvalidOperationException($"Don't know how to deal with MonthFromTime value {monthFromTime}");
        }

        public static int MonthFromTime(double t)
        {
            var dayWithinYear = DayWithinYear(t);
            var inLeapYear = InLeapYear(t);

            if (dayWithinYear < 31)
            {
                return 0;
            }

            if (dayWithinYear < 59 + inLeapYear)
            {
                return 1;
            }

            if (dayWithinYear < 90 + inLeapYear)
            {
                return 2;
            }

            if (dayWithinYear < 120 + inLeapYear)
            {
                return 3;
            }

            if (dayWithinYear < 151 + inLeapYear)
            {
                return 4;
            }

            if (dayWithinYear < 181 + inLeapYear)
            {
                return 5;
            }

            if (dayWithinYear < 212 + inLeapYear)
            {
                return 6;
            }

            if (dayWithinYear < 243 + inLeapYear)
            {
                return 7;
            }

            if (dayWithinYear < 273 + inLeapYear)
            {
                return 8;
            }

            if (dayWithinYear < 304 + inLeapYear)
            {
                return 9;
            }

            if (dayWithinYear < 334 + inLeapYear)
            {
                return 10;
            }

            if (dayWithinYear < 365 + inLeapYear)
            {
                return 11;
            }

            throw new InvalidOperationException($"MonthFromTime is not working, cannot reason about DayWithinYear {dayWithinYear}");
        }

        public static int DayWithinYear(double t)
        {
            return Day(t) - DayFromYear(YearFromTime(t));
        }

        public static int Day(double t)
        {
            return (int)System.Math.Floor(t / MsPerDay);
        }

        private static double LocalTime(double t)
        {
            if (!IsFinite(t))
            {
                return double.NaN;
            }

            return (long)(t + LocalTza + DaylightSavingTa((long)t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool AreFinite(double value1, double value2)
        {
            return IsFinite(value1) && IsFinite(value2);
        }

        private static bool AreFinite(double value1, double value2, double value3)
        {
            return IsFinite(value1) && IsFinite(value2) && IsFinite(value3);
        }

        private static bool AreFinite(double value1, double value2, double value3, double value4)
        {
            return IsFinite(value1) && IsFinite(value2) && IsFinite(value3) && IsFinite(value4);
        }

        public static double MakeDate(double day, double time)
        {
            if (!AreFinite(day, time))
            {
                return double.NaN;
            }

            return day * MsPerDay + time;
        }

        public static double MakeDay(double year, double month, double date)
        {
            if (year < MinYear || year > MaxYear || month < MinMonth || month > MaxMonth || !AreFinite(year, month, date))
            {
                return double.NaN;
            }

            var y = (long)(int)year;
            var m = (long)(int)month;
            y += m / 12;
            m %= 12;
            if (m < 0)
            {
                m += 12;
                y -= 1;
            }

            // kYearDelta is an arbitrary number such that:
            // a) kYearDelta = -1 (mod 400)
            // b) year + kYearDelta > 0 for years in the range defined by
            //    ECMA 262 - 15.9.1.1, i.e. upto 100,000,000 days on either side of
            //    Jan 1 1970. This is required so that we don't run into integer
            //    division of negative numbers.
            // c) there shouldn't be an overflow for 32-bit integers in the following
            //    operations.
            const int kYearDelta = 399999;
            const int kBaseDay =
                365 * (1970 + kYearDelta) + (1970 + kYearDelta) / 4 -
                (1970 + kYearDelta) / 100 + (1970 + kYearDelta) / 400;

            long dayFromYear = 365 * (y + kYearDelta) + (y + kYearDelta) / 4 -
                                (y + kYearDelta) / 100 + (y + kYearDelta) / 400 - kBaseDay;

            if ((y % 4 != 0) || (y % 100 == 0 && y % 400 != 0))
            {
                var dayFromMonth = new[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
                dayFromYear += dayFromMonth[m];
            }
            else
            {
                var dayFromMonthLeapYear = new[] { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335 };
                dayFromYear += dayFromMonthLeapYear[m];
            }
            return dayFromYear - 1 + (int)date;
        }

        public static double MakeTime(double hour, double min, double sec, double ms)
        {
            if (!AreFinite(hour, min, sec, ms))
            {
                return double.NaN;
            }

            var h = (int)hour;
            var m = (int)min;
            var s = (int)sec;
            var milli = (int)ms;
            var t = h * MsPerHour + m * MsPerMinute + s * MsPerSecond + milli;

            return t;
        }

        private static string TimeZoneString(DateTimeOffset t)
        {
            return t.ToString("'GMT'K", CultureInfo.InvariantCulture).Replace(":", "", StringComparison.InvariantCulture);
        }

        public static DateTimeOffset ToLocalTime(DateTime t)
        {
            var localTimeZone = Interpreter.Instance().LocalTimeZone;
            return t.Kind switch
            {
                DateTimeKind.Local => new DateTimeOffset(TimeZoneInfo.ConvertTime(t.ToUniversalTime(), localTimeZone), localTimeZone.GetUtcOffset(t)),
                DateTimeKind.Utc => new DateTimeOffset(TimeZoneInfo.ConvertTime(t, localTimeZone), localTimeZone.GetUtcOffset(t)),
                _ => t,
            };
        }

        public static long LocalTza => (long)Interpreter.Instance().LocalTimeZone.BaseUtcOffset.TotalMilliseconds;

        private static Date YearMonthDayFromTime(double t) => YearMonthDayFromDays((long)(t / 1000 / 60 / 60 / 24));

        private static Date YearMonthDayFromDays(long days)
        {
            const int kDaysIn4Years = 4 * 365 + 1;
            const int kDaysIn100Years = 25 * kDaysIn4Years - 1;
            const int kDaysIn400Years = 4 * kDaysIn100Years + 1;
            const int kDays1970to2000 = 30 * 365 + 7;
            const int kDaysOffset =
                1000 * kDaysIn400Years + 5 * kDaysIn400Years - kDays1970to2000;
            const int kYearsOffset = 400000;


            days += kDaysOffset;
            var year = 400 * (days / kDaysIn400Years) - kYearsOffset;
            days %= kDaysIn400Years;

            days--;
            var yd1 = days / kDaysIn100Years;
            days %= kDaysIn100Years;
            year += 100 * yd1;

            days++;
            var yd2 = days / kDaysIn4Years;
            days %= kDaysIn4Years;
            year += 4 * yd2;

            days--;
            var yd3 = days / 365;
            days %= 365;
            year += yd3;

            var is_leap = (yd1 == 0 || yd2 != 0) && yd3 == 0;

            days += is_leap ? 1 : 0;
            var month = 0;
            var day = 0;

            // Check if the date is after February.
            if (days >= 31 + 28 + (is_leap ? 1 : 0))
            {
                days -= 31 + 28 + (is_leap ? 1 : 0);
                // Find the date starting from March.
                for (int i = 2; i < 12; i++)
                {
                    if (days < kDaysInMonths[i])
                    {
                        month = i;
                        day = (int)(days + 1);
                        break;
                    }

                    days -= kDaysInMonths[i];
                }
            }
            else
            {
                // Check January and February.
                if (days < 31)
                {
                    month = 0;
                    day = (int)(days + 1);
                }
                else
                {
                    month = 1;
                    day = (int)(days - 31 + 1);
                }
            }

            return new Date((int)year, month, day);
        }

        public static int YearFromTime(double t)
        {
            var (year, _, _) = YearMonthDayFromTime(t);
            return year;
        }

        public static int DayFromYear(double y)
        {
            return (int)(365 * (y - 1970)
                          + Math.Floor((y - 1969) / 4)
                          - Math.Floor((y - 1901) / 100)
                          + Math.Floor((y - 1601) / 400));
        }

        public static long TimeFromYear(double y)
        {
            return MsPerDay * DayFromYear(y);
        }

        public static int DaysInYear(double y)
        {
            if (y % 4 != 0)
            {
                return 365;
            }

            if (y % 4 == 0 && y % 100 != 0)
            {
                return 366;
            }

            if (y % 100 == 0 && y % 400 != 0)
            {
                return 365;
            }

            if (y % 400 == 0)
            {
                return 366;
            }

            return 365;
        }

        public static int InLeapYear(double t)
        {
            var daysInYear = DaysInYear(YearFromTime(t));

            if (daysInYear == 365)
            {
                return 0;
            }

            if (daysInYear == 366)
            {
                return 1;
            }

            throw new ArgumentException("Invalid number of days in year", nameof(t));
        }

        public static double DaylightSavingTa(double t)
        {
            if (double.IsNaN(t))
            {
                return t;
            }

            var year = YearFromTime(t);
            var timeInYear = t - TimeFromYear(year);

            if (double.IsInfinity(timeInYear) || double.IsNaN(timeInYear))
            {
                return 0;
            }

            if (year < 9999 && year > -9999 && year != 0)
            {
                // in DateTimeOffset range so we can use it
            }
            else
            {
                // use similar leap-ed year
                var isLeapYear = InLeapYear((long)t) == 1;
                year = isLeapYear ? 2000 : 1999;
            }

            var dateTime = new DateTime(year, 1, 1).AddMilliseconds(timeInYear);

            return Interpreter.Instance().LocalTimeZone.IsDaylightSavingTime(dateTime) ? MsPerHour : 0;
        }

        public static double Utc(double t)
        {
            return t - LocalTza - DaylightSavingTa(t - LocalTza);
        }

    }
}