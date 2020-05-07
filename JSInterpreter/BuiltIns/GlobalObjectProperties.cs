using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter
{
    public static class GlobalObjectProperties
    {
        public static Completion isFinite(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var numComp = arguments[0].ToNumber();
            if (numComp.IsAbrupt()) return numComp;
            var num = (numComp.value as NumberValue).number;
            if (double.IsNaN(num) || double.IsInfinity(num))
                return Completion.NormalCompletion(BooleanValue.False);
            return Completion.NormalCompletion(BooleanValue.True);
        }

        public static Completion isNaN(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var numComp = arguments[0].ToNumber();
            if (numComp.IsAbrupt()) return numComp;
            var num = (numComp.value as NumberValue).number;
            if (double.IsNaN(num))
                return Completion.NormalCompletion(BooleanValue.True);
            return Completion.NormalCompletion(BooleanValue.False);
        }

        public static Completion parseFloat(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var inputStringComp = arguments[0].ToJsString();
            if (inputStringComp.IsAbrupt()) return inputStringComp;
            var inputString = (inputStringComp.value as StringValue).@string;
            var trimmedString = inputString.TrimStart();

            var sign = 1;
            if (trimmedString.Length > 0)
            {
                if (trimmedString[0] == '-')
                {
                    sign = -1;
                    trimmedString = trimmedString.Substring(1);
                }
                else if (trimmedString[0] == '+')
                {
                    trimmedString = trimmedString.Substring(1);
                }
            }

            if (trimmedString.StartsWith("Infinity", StringComparison.InvariantCulture))
            {
                return Completion.NormalCompletion(new NumberValue(sign * double.PositiveInfinity));
            }

            if (trimmedString.StartsWith("NaN", StringComparison.InvariantCulture))
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }

            var separator = (char)0;

            bool isNan = true;
            decimal number = 0;
            var i = 0;
            for (; i < trimmedString.Length; i++)
            {
                var c = trimmedString[i];
                if (c == '.')
                {
                    i++;
                    separator = '.';
                    break;
                }

                if (c == 'e' || c == 'E')
                {
                    i++;
                    separator = 'e';
                    break;
                }

                var digit = c - '0';

                if (digit >= 0 && digit <= 9)
                {
                    isNan = false;
                    number = number * 10 + digit;
                }
                else
                {
                    break;
                }
            }

            decimal pow = 0.1m;

            if (separator == '.')
            {
                for (; i < trimmedString.Length; i++)
                {
                    var c = trimmedString[i];

                    var digit = c - '0';

                    if (digit >= 0 && digit <= 9)
                    {
                        isNan = false;
                        number += digit * pow;
                        pow *= 0.1m;
                    }
                    else if (c == 'e' || c == 'E')
                    {
                        i++;
                        separator = 'e';
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            var exp = 0;
            var expSign = 1;

            if (separator == 'e')
            {
                if (i < trimmedString.Length)
                {
                    if (trimmedString[i] == '-')
                    {
                        expSign = -1;
                        i++;
                    }
                    else if (trimmedString[i] == '+')
                    {
                        i++;
                    }
                }

                for (; i < trimmedString.Length; i++)
                {
                    var c = trimmedString[i];

                    var digit = c - '0';

                    if (digit >= 0 && digit <= 9)
                    {
                        exp = exp * 10 + digit;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (isNan)
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }

            for (var k = 1; k <= exp; k++)
            {
                if (expSign > 0)
                {
                    number *= 10;
                }
                else
                {
                    number /= 10;
                }
            }

            return Completion.NormalCompletion(new NumberValue((double)(sign * number)));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public static Completion parseInt(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;
            var inputStringComp = arguments[0].ToJsString();
            if (inputStringComp.IsAbrupt()) return inputStringComp;
            var inputString = (inputStringComp.value as StringValue).@string;
            var s = inputString.Trim();

            int R = 0;
            if (arguments.Count > 1)
            {
                var radixComp = arguments[1].ToNumber();
                if (radixComp.IsAbrupt()) return radixComp;
                R = (int)(radixComp.value as NumberValue).number;
            }

            var sign = 1;
            if (!string.IsNullOrEmpty(s))
            {
                if (s[0] == '-')
                {
                    sign = -1;
                }

                if (s[0] == '-' || s[0] == '+')
                {
                    s = s.Substring(1);
                }
            }

            var stripPrefix = true;

            if (R == 0)
            {
                if (s.Length >= 2 && s.StartsWith("0x", StringComparison.InvariantCulture) || s.StartsWith("0X", StringComparison.InvariantCulture))
                {
                    R = 16;
                }
                else
                {
                    R = 10;
                }
            }
            else if (R < 2 || R > 36)
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }
            else if (R != 16)
            {
                stripPrefix = false;
            }

            if (stripPrefix && s.Length >= 2 && s.StartsWith("0x", StringComparison.InvariantCulture) || s.StartsWith("0X", StringComparison.InvariantCulture))
            {
                s = s.Substring(2);
            }

            try
            {
                return Completion.NormalCompletion(new NumberValue(sign * Parse(s, R)));
            }
            catch
            {
                return Completion.NormalCompletion(new NumberValue(double.NaN));
            }
        }

        private static double Parse(string number, int radix)
        {
            if (number.Length == 0)
            {
                return double.NaN;
            }

            double result = 0;
            double pow = 1;
            for (int i = number.Length - 1; i >= 0; i--)
            {
                double index = double.NaN;
                char digit = number[i];

                if (digit >= '0' && digit <= '9')
                {
                    index = digit - '0';
                }
                else if (digit >= 'a' && digit <= 'z')
                {
                    index = digit - 'a' + 10;
                }
                else if (digit >= 'A' && digit <= 'Z')
                {
                    index = digit - 'A' + 10;
                }

                if (double.IsNaN(index) || index >= radix)
                {
                    return Parse(number.Substring(0, i), radix);
                }

                result += index * pow;
                pow *= radix;
            }

            return result;
        }
    }
}
