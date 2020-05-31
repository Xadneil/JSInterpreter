using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace JSInterpreter
{
    public class NumberPrototype : Object
    {
        public NumberPrototype(NumberConstructor constructor, Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));

            DefinePropertyOrThrow("toExponential", new PropertyDescriptor(Utils.CreateBuiltinFunction(toExponential, realm), true, false, true));
            DefinePropertyOrThrow("toFixed", new PropertyDescriptor(Utils.CreateBuiltinFunction(toFixed, realm), true, false, true));
            DefinePropertyOrThrow("toLocaleString", new PropertyDescriptor(Utils.CreateBuiltinFunction(toLocaleString, realm), true, false, true));
            DefinePropertyOrThrow("toPrecision", new PropertyDescriptor(Utils.CreateBuiltinFunction(toPrecision, realm), true, false, true));
            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(toString, realm), true, false, true));
            DefinePropertyOrThrow("valueOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(valueOf, realm), true, false, true));
        }

        private static CompletionOr<NumberValue?> thisNumberValue(IValue value)
        {
            if (value is NumberValue n)
                return Completion.NormalWith(n);
            if (value is NumberObject o)
                return Completion.NormalWith(o.value);
            return Completion.ThrowTypeError("Must operate on a Number value or object").WithEmpty<NumberValue?>();
        }

        private static Completion toExponential(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var fractionDigits = arguments.At(0, UndefinedValue.Instance);
            var xComp = thisNumberValue(thisValue);
            if (xComp.IsAbrupt()) return xComp;
            var x = xComp.Other!.number;
            var fracNumComp = fractionDigits.ToNumber();
            if (fracNumComp.IsAbrupt()) return fracNumComp;
            var f = (int)(fracNumComp.value as NumberValue)!.number;
            if (fractionDigits == UndefinedValue.Instance)
                f = 0;
            if (double.IsNaN(x))
                return Completion.NormalCompletion(new StringValue("NaN"));
            var s = "";
            if (x < 0)
            {
                s = "-";
                x = -x;
            }
            if (double.IsPositiveInfinity(x))
                return Completion.NormalCompletion(new StringValue(s + "Infinity"));

            if (f < 0 || f > 100)
                return Completion.ThrowRangeError("fractionDigits argument must be between 0 and 100");
            //string m;
            //int e;
            //if (x == 0)
            //{
            //    m = new string('0', f + 1);
            //    e = 0;
            //}
            //else
            //{
            //    int n;
            //    if (fractionDigits != UndefinedValue.Instance)
            //    {

            //    }
            //    else
            //    {

            //    }
            //    m = n.ToString(CultureInfo.InvariantCulture);
            //}
            //if (f != 0)
            //{
            //    var a = m[0..1];
            //    var b = m[1..];
            //    m = $"{a}.{b}";
            //}
            //string c, d;
            //if (e == 0)
            //{
            //    c = "+";
            //    d = "0";
            //}
            //else
            //{
            //    if (e > 0)
            //        c = "+";
            //    else
            //    {
            //        c = "-";
            //        e = -e;
            //    }
            //    d = e.ToString(CultureInfo.InvariantCulture);
            //}
            //m = m + "e" + c + d;
            //return Completion.NormalCompletion(new StringValue(s + m));

            if (f > 0)
                return Completion.NormalCompletion(new StringValue(x.ToString($"0.{new string('#', f)}e+0", CultureInfo.InvariantCulture)));
            return Completion.NormalCompletion(new StringValue(x.ToString($"0.{new string('#', 100)}e+0", CultureInfo.InvariantCulture)));
        }

        private static Completion toFixed(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            throw new NotImplementedException();
        }

        private static Completion toLocaleString(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            throw new NotImplementedException();
        }

        private static Completion toPrecision(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            throw new NotImplementedException();
        }

        public static Completion toString(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var xComp = thisNumberValue(thisValue);
            if (xComp.IsAbrupt()) return xComp;
            var x = xComp.Other!;
            int radixNumber;
            if (arguments.Count == 0)
                radixNumber = 10;
            else if (arguments[0] == UndefinedValue.Instance)
                radixNumber = 10;
            else
            {
                var comp = arguments[0].ToNumber();
                if (comp.IsAbrupt()) return comp;
                radixNumber = (int)(comp.value as NumberValue)!.number;
            }

            if (radixNumber < 2 || radixNumber > 36)
                return Completion.ThrowRangeError("radix argument must be between 2 and 36");
            if (double.IsNaN(x.number))
            {
                return Completion.NormalCompletion(new StringValue("NaN"));
            }

            if (x.number == 0)
            {
                return Completion.NormalCompletion(new StringValue("0"));
            }

            if (double.IsPositiveInfinity(x.number) || x.number >= double.MaxValue)
            {
                return Completion.NormalCompletion(new StringValue("Infinity"));
            }

            if (radixNumber == 10)
                return Completion.NormalCompletion(new StringValue(x.number.ToString(CultureInfo.InvariantCulture)));

            var integer = (long)x.number;
            var fraction = x.number - integer;

            string result = ToBase(integer, radixNumber);
            if (fraction != 0)
            {
                result += "." + ToFractionBase(fraction, radixNumber);
            }

            return Completion.NormalCompletion(new StringValue(result));
        }

        public static string ToBase(long n, int radix)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (n == 0)
            {
                return "0";
            }

            var result = new StringBuilder();
            while (n > 0)
            {
                var digit = (int)(n % radix);
                n /= radix;
                result.Insert(0, digits[digit]);
            }

            return result.ToString();
        }

        public static string ToFractionBase(double n, int radix)
        {
            // based on the repeated multiplication method
            // http://www.mathpath.org/concepts/Num/frac.htm

            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (n == 0)
            {
                return "0";
            }

            var result = new StringBuilder();
            while (n > 0 && result.Length < 50) // arbitrary limit
            {
                var c = n * radix;
                var d = (int)c;
                n = c - d;

                result.Append(digits[d]);
            }

            return result.ToString();
        }

        private static Completion valueOf(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var comp = thisNumberValue(thisValue);
            if (comp.IsAbrupt()) return comp;
            return Completion.NormalCompletion(comp.Other!);
        }
    }
}
