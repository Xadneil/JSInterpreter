using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JSInterpreter
{
    public class DateConstructor : Constructor
    {
        internal static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string[] DefaultFormats = {
            "yyyy-MM-ddTHH:mm:ss.FFF",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-dd",
            "yyyy-MM",
            "yyyy"
        };

        private static readonly string[] SecondaryFormats = {
            // Formats used in DatePrototype toString methods
            "ddd MMM dd yyyy HH:mm:ss 'GMT'K",
            "ddd MMM dd yyyy",
            "HH:mm:ss 'GMT'K",

            // standard formats
            "yyyy-M-dTH:m:s.FFFK",
            "yyyy/M/dTH:m:s.FFFK",
            "yyyy-M-dTH:m:sK",
            "yyyy/M/dTH:m:sK",
            "yyyy-M-dTH:mK",
            "yyyy/M/dTH:mK",
            "yyyy-M-d H:m:s.FFFK",
            "yyyy/M/d H:m:s.FFFK",
            "yyyy-M-d H:m:sK",
            "yyyy/M/d H:m:sK",
            "yyyy-M-d H:mK",
            "yyyy/M/d H:mK",
            "yyyy-M-dK",
            "yyyy/M/dK",
            "yyyy-MK",
            "yyyy/MK",
            "yyyyK",
            "THH:mm:ss.FFFK",
            "THH:mm:ssK",
            "THH:mmK",
            "THHK"
        };

        public DateConstructor(FunctionPrototype prototype)
        {
            this.prototype = prototype;
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));

        }

        private static Completion Parse(IValue thisObj, IReadOnlyList<IValue> arguments)
        {
            var argComp = arguments.At(0);
            if (argComp.IsAbrupt()) return argComp;
            var dateComp = argComp.value!.ToJsString();
            if (dateComp.IsAbrupt()) return dateComp;
            var date = (dateComp.value as StringValue)!.@string;

            if (!DateTime.TryParseExact(date, DefaultFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
            {
                if (!DateTime.TryParseExact(date, SecondaryFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
                {
                    if (!DateTime.TryParse(date, Interpreter.Instance().Culture, DateTimeStyles.AdjustToUniversal, out result))
                    {
                        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
                        {
                            // unrecognized dates should return NaN (15.9.4.2)
                            return Completion.NormalCompletion(NumberValue.DoubleNaN);
                        }
                    }
                }
            }

            return Completion.NormalCompletion(new NumberValue(FromDateTime(result)));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            return DatePrototype.toString(InternalConstruct(Utils.EmptyList<IValue>(), (thisValue as Object)!).value!, Utils.EmptyList<IValue>());
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object? newTarget)
        {
            if (arguments.Count == 0)
            {
                return Completion.NormalCompletion(Construct(DateTime.UtcNow));
            }
            else if (arguments.Count == 1)
            {
                if (arguments[0] is DateInstance date)
                {
                    return Completion.NormalCompletion(Construct(date.PrimitiveValue));
                }

                var vComp = arguments[0].ToPrimitive();
                if (vComp.IsAbrupt()) return vComp;
                var v = vComp.value!;
                if (v is StringValue)
                {
                    var parse = Parse(UndefinedValue.Instance, new[] { v });
                    return Completion.NormalCompletion(Construct(((NumberValue)parse.value!).number));
                }

                var number = v.ToNumber();
                if (number.IsAbrupt()) return number;
                var numberValue = (number.value as NumberValue)!.number;

                return Completion.NormalCompletion(Construct(TimeClip(numberValue)));
            }
            else
            {
                var yComp = arguments.At(0);
                if (yComp.IsAbrupt()) return yComp;
                yComp = yComp.value!.ToNumber();
                if (yComp.IsAbrupt()) return yComp;
                var y = (yComp.value as NumberValue)!.number;

                var mComp = arguments.At(1);
                if (mComp.IsAbrupt()) return mComp;
                mComp = mComp.value!.ToNumber();
                if (mComp.IsAbrupt()) return mComp;
                var m = (mComp.value as NumberValue)!.number;

                var dtComp = arguments.At(2, NumberValue.One);
                if (dtComp.IsAbrupt()) return dtComp;
                dtComp = dtComp.value!.ToNumber();
                if (dtComp.IsAbrupt()) return dtComp;
                var dt = (dtComp.value as NumberValue)!.number;

                var hComp = arguments.At(3, NumberValue.PositiveZero);
                if (hComp.IsAbrupt()) return hComp;
                hComp = hComp.value!.ToNumber();
                if (hComp.IsAbrupt()) return hComp;
                var h = (hComp.value as NumberValue)!.number;

                var minComp = arguments.At(4, NumberValue.PositiveZero);
                if (minComp.IsAbrupt()) return minComp;
                minComp = minComp.value!.ToNumber();
                if (minComp.IsAbrupt()) return minComp;
                var min = (minComp.value as NumberValue)!.number;

                var sComp = arguments.At(5, NumberValue.PositiveZero);
                if (sComp.IsAbrupt()) return sComp;
                sComp = sComp.value!.ToNumber();
                if (sComp.IsAbrupt()) return sComp;
                var s = (sComp.value as NumberValue)!.number;

                var milliComp = arguments.At(6, NumberValue.PositiveZero);
                if (milliComp.IsAbrupt()) return milliComp;
                milliComp = milliComp.value!.ToNumber();
                if (milliComp.IsAbrupt()) return milliComp;
                var milli = (milliComp.value as NumberValue)!.number;

                var yInteger = (int)y;
                if (!double.IsNaN(y) && 0 <= yInteger && yInteger <= 99)
                {
                    y += 1900;
                }

                var finalDate = DatePrototype.MakeDate(
                    DatePrototype.MakeDay(y, m, dt),
                    DatePrototype.MakeTime(h, min, s, milli));

                return Completion.NormalCompletion(Construct(TimeClip(DatePrototype.Utc(finalDate))));
            }
        }

        public static DateInstance Construct(DateTimeOffset value)
        {
            return Construct(value.UtcDateTime);
        }

        public static DateInstance Construct(DateTime value)
        {
            var instance = new DateInstance()
            {
                prototype = Interpreter.Instance().CurrentRealm().Intrinsics.DatePrototype,
                PrimitiveValue = FromDateTime(value)
            };

            return instance;
        }

        public static DateInstance Construct(double time)
        {
            var instance = new DateInstance()
            {
                prototype = Interpreter.Instance().CurrentRealm().Intrinsics.DatePrototype,
                PrimitiveValue = TimeClip(time)
            };

            return instance;
        }

        public static double TimeClip(double time)
        {
            if (double.IsInfinity(time) || double.IsNaN(time))
            {
                return double.NaN;
            }

            if (System.Math.Abs(time) > 8640000000000000)
            {
                return double.NaN;
            }

            return (int)time;
        }

        public static double FromDateTime(DateTime dt)
        {
            var convertToUtcAfter = (dt.Kind == DateTimeKind.Unspecified);

            var dateAsUtc = dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            var result = (dateAsUtc - Epoch).TotalMilliseconds;

            if (convertToUtcAfter)
            {
                result = DatePrototype.Utc(result);
            }

            return System.Math.Floor(result);
        }
    }
}
