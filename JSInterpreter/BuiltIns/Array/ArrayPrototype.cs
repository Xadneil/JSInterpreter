using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ArrayPrototype : Object
    {
        public ArrayPrototype(ArrayConstructor constructor, Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));

            DefinePropertyOrThrow("push", new PropertyDescriptor(Utils.CreateBuiltinFunction(push, realm: realm), true, false, true));
        }

        private Completion push(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var OComp = @this.ToObject();
            if (OComp.IsAbrupt()) return OComp;
            var O = OComp.value as Object;

            var lenComp = O!.Get("length");
            if (lenComp.IsAbrupt()) return lenComp;
            var toLenComp = ToLength(lenComp.value!);
            if (toLenComp.IsAbrupt()) return toLenComp;
            long len = toLenComp.Other;

            if (len + arguments.Count > (1L << 53) - 1)
                return Completion.ThrowTypeError("Too many values in the array.");

            for (int i = 0; i < arguments.Count; i++)
            {
                O.Set((len + i).ToString(System.Globalization.CultureInfo.InvariantCulture), arguments[i], true);
            }

            var lenValue = new NumberValue(len + arguments.Count);
            var setComp = O.Set("length", lenValue, true);
            if (setComp.IsAbrupt()) return setComp;

            return Completion.NormalCompletion(lenValue);
        }

        public static Completion values(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var O = @this.ToObject();
            if (O.IsAbrupt()) return O;
            return Completion.NormalCompletion(CreateArrayIterator((O.value as Object)!, "value"));
        }

        private static Object CreateArrayIterator(Object array, string kind)
        {
            var iterator = Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ArrayIteratorPrototype, new[] { "IteratedObject", "ArrayIteratorNextIndex", "ArrayIterationKind" });
            iterator.SetCustomInternalSlot("IteratedObject", array);
            iterator.SetCustomInternalSlot("ArrayIteratorNextIndex", 0);
            iterator.SetCustomInternalSlot("ArrayIterationKind", kind);
            return iterator;
        }

        private static CompletionOr<long> ToLength(IValue value)
        {
            var lenComp = value.ToNumber();
            if (lenComp.IsAbrupt()) return lenComp.WithEmpty<long>();
            var len = (long)(lenComp.value as NumberValue)!.number;
            if (len < 0)
                return Completion.NormalWithStruct(0L);
            return Completion.NormalWithStruct(Math.Min(len, (1L << 53) - 1));
        }

    }
}
