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
        }

        public static Completion values(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var O = @this.ToObject();
            if (O.IsAbrupt()) return O;
            return Completion.NormalCompletion(CreateArrayIterator(O.value as Object, "value"));
        }

        private static Object CreateArrayIterator(Object array, string kind)
        {
            var iterator = Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ArrayIteratorPrototype, new[] { "IteratedObject", "ArrayIteratorNextIndex", "ArrayIterationKind" });
            iterator.SetCustomInternalSlot("IteratedObject", array);
            iterator.SetCustomInternalSlot("ArrayIteratorNextIndex", 0);
            iterator.SetCustomInternalSlot("ArrayIterationKind", kind);
            return iterator;
        }
    }
}
