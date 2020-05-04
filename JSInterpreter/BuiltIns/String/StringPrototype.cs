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
            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(toString, Utils.EmptyList<string>(), realm), true, false, false));
            DefinePropertyOrThrow("valueOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(valueOf, Utils.EmptyList<string>(), realm), true, false, false));
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
