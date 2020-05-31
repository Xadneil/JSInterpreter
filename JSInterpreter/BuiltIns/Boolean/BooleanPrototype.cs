using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class BooleanPrototype : BooleanObject
    {
        public BooleanPrototype(BooleanConstructor constructor, Realm realm) : base(BooleanValue.False, null)
        {
            this.prototype = realm.Intrinsics.ObjectPrototype;
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));

            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(toString, realm), true, false, true));
            DefinePropertyOrThrow("valueOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(valueOf, realm), true, false, true));
        }

        private static CompletionOr<BooleanValue?> thisBooleanValue(IValue value)
        {
            if (value is BooleanValue b)
                return Completion.NormalWith(b);
            if (value is BooleanObject o)
                return Completion.NormalWith(o.Value);
            return Completion.ThrowTypeError("Must operate on a Boolean value or object").WithEmpty<BooleanValue?>();
        }

        private static Completion toString(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var b = thisBooleanValue(thisValue);
            if (b.IsAbrupt()) return b;
            return Completion.NormalCompletion(new StringValue(b.Other!.boolean ? "true" : "false"));
        }

        private static Completion valueOf(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var b = thisBooleanValue(thisValue);
            if (b.IsAbrupt()) return b;
            return Completion.NormalCompletion(b.Other!);
        }
    }
}
