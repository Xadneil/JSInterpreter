using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    static class Extensions
    {
        public static Completion At(this IReadOnlyList<IValue> arguments, int index, IValue defaultValue = null)
        {
            if (arguments.Count <= index)
            {
                if (defaultValue != null)
                    return Completion.NormalCompletion(defaultValue);
                return Completion.ThrowTypeError($"At least {index + 1} arguments are required");
            }
            return Completion.NormalCompletion(arguments[index]);
        }
    }
}
