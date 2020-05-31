using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JSInterpreter
{
    public class ArrayPrototype : Object
    {
        public ArrayPrototype(ArrayConstructor constructor, Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));

            DefinePropertyOrThrow("join", new PropertyDescriptor(Utils.CreateBuiltinFunction(join, realm: realm), true, false, true));
            DefinePropertyOrThrow("push", new PropertyDescriptor(Utils.CreateBuiltinFunction(push, realm: realm), true, false, true));
            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(toString, realm: realm), true, false, true));
            DefinePropertyOrThrow("values", new PropertyDescriptor(Utils.CreateBuiltinFunction(values, realm: realm), true, false, true));
        }

        private static Completion join(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var oComp = thisValue.ToObject();
            if (oComp.IsAbrupt()) return oComp;
            var O = (oComp.value as Object)!;
            var getComp = O.Get("length");
            if (getComp.IsAbrupt()) return getComp;
            var lenComp = getComp.value!.ToLength();
            if (lenComp.IsAbrupt()) return lenComp;
            var len = lenComp.Other;
            var separator = arguments.At(0, UndefinedValue.Instance);
            string sep;
            if (separator == UndefinedValue.Instance)
                sep = ",";
            else
            {
                var comp = separator.ToJsString();
                if (comp.IsAbrupt()) return comp;
                sep = (comp.value as StringValue)!.@string;
            }
            var R = new StringBuilder();
            for (int k = 0; k < len; k++)
            {
                if (k > 0)
                    R.Append(sep);
                var elementComp = O.Get(k.ToString(CultureInfo.InvariantCulture));
                if (elementComp.IsAbrupt()) return elementComp;
                var element = elementComp.value!;
                string next;
                if (element == UndefinedValue.Instance || element == NullValue.Instance)
                    next = "";
                else
                {
                    var comp = element.ToJsString();
                    if (comp.IsAbrupt()) return comp;
                    next = (comp.value as StringValue)!.@string;
                }
                R.Append(next);
            }
            return Completion.NormalCompletion(new StringValue(R.ToString()));
        }

        private Completion push(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var OComp = @this.ToObject();
            if (OComp.IsAbrupt()) return OComp;
            var O = OComp.value as Object;

            var lenComp = O!.Get("length");
            if (lenComp.IsAbrupt()) return lenComp;
            var toLenComp = lenComp.value!.ToLength();
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

        private static Completion toString(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var array = thisValue.ToObject();
            if (array.IsAbrupt()) return array;
            var func = (array.value as Object)!.Get("join");
            if (func.IsAbrupt()) return func;
            if (func.value is Callable c)
                return c.Call(array.value!);
            else
                return ObjectPrototype.toString(func.value!, new List<IValue>(1) { array.value! });
        }

        public static Completion values(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var O = @this.ToObject();
            if (O.IsAbrupt()) return O;
            return Completion.NormalCompletion(CreateArrayIterator((O.value as Object)!, "value"));
        }

        private static Object CreateArrayIterator(Object array, string kind)
        {
            var iterator = Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ArrayIteratorPrototype, () => new ArrayIterator(array, 0, kind));
            return iterator;
        }

        private class ArrayIterator : Object
        {
            public Object IteratedObject { get; private set; }
            public int ArrayIteratorNextIndex { get; private set; }
            public string ArrayIterationKind { get; private set; }

            public ArrayIterator(Object iteratedObject, int arrayIteratorNextIndex, string arrayIterationKind)
            {
                IteratedObject = iteratedObject;
                ArrayIteratorNextIndex = arrayIteratorNextIndex;
                ArrayIterationKind = arrayIterationKind;
            }
        }
    }
}
