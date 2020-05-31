using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    static class Extensions
    {
        public static Completion At(this IReadOnlyList<IValue> arguments, int index)
        {
            if (arguments.Count <= index)
            {
                return Completion.ThrowTypeError($"At least {index + 1} arguments are required");
            }
            return Completion.NormalCompletion(arguments[index]);
        }

        public static IValue At(this IReadOnlyList<IValue> arguments, int index, IValue defaultValue)
        {
            if (arguments.Count <= index)
            {
                return defaultValue;
            }
            return arguments[index];
        }

        public static bool IsHexDigit(this char c) => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        public static int ToHexValue(this char c) => char.IsDigit(c) ? c - '0' : (char.ToUpperInvariant(c) - 'A' + 10);
    }
}
