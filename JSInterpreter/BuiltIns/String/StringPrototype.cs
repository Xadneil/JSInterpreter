using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class StringPrototype : Object
    {
        public StringPrototype(StringConstructor constructor, Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));
            DefinePropertyOrThrow("charCodeAt", new PropertyDescriptor(Utils.CreateBuiltinFunction(charCodeAt, Utils.EmptyList<string>(), realm), true, false, false));
            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(toString, Utils.EmptyList<string>(), realm), true, false, false));
            DefinePropertyOrThrow("valueOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(valueOf, Utils.EmptyList<string>(), realm), true, false, false));
        }

        private Completion charCodeAt(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;

            var O = @this.RequireObjectCoercible();
            if (O.IsAbrupt()) return O;
            var SComp = O.value.ToJsString();
            if (SComp.IsAbrupt()) return SComp;
            var S = (SComp.value as StringValue).@string;
            var positionComp = arguments[0].ToNumber();
            if (positionComp.IsAbrupt()) return positionComp;
            var position = (int)(positionComp.value as NumberValue).number;
            if (position < 0 || position >= S.Length)
                return Completion.NormalCompletion(NumberValue.DoubleNaN);
            return Completion.NormalCompletion(new NumberValue(S[position]));
        }

        public static Completion toString(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (@this is StringValue)
                return Completion.NormalCompletion(@this);
            if (@this is StringObject s)
                return Completion.NormalCompletion(s.value);
            return Completion.ThrowTypeError("Must be called on a string");
        }

        public static Completion valueOf(IValue @this, IReadOnlyList<IValue> arguments)
        {
            return toString(@this, arguments);
        }
    }
}
