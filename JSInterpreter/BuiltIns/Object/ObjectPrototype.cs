using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ObjectPrototype : Object
    {
        private readonly ObjectConstructor objectConstructor;

        public ObjectPrototype(ObjectConstructor objectConstructor)
        {
            this.objectConstructor = objectConstructor;

        }

        public void DefineDeferredProperties(Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(objectConstructor, true, false, true));

            DefinePropertyOrThrow("hasOwnProperty", new PropertyDescriptor(Utils.CreateBuiltinFunction(hasOwnProperty, realm: realm), true, false, true));
            DefinePropertyOrThrow("isPrototypeOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(isPrototypeOf, realm: realm), true, false, true));
            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(ToObjectString, realm: realm), true, false, true));
            DefinePropertyOrThrow("valueOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(valueOf, realm: realm), true, false, true));
        }

        private Completion valueOf(IValue @this, IReadOnlyList<IValue> arguments)
        {
            return @this.ToObject();
        }

        private Completion hasOwnProperty(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 1);
            if (argComp.IsAbrupt()) return argComp;

            var P = arguments[0].ToPropertyKey();
            if (P.IsAbrupt()) return P;
            var O = @this.ToObject();
            if (O.IsAbrupt()) return O;
            return (O.value as Object)!.HasOwnProperty(P.Other!);
        }

        private Completion isPrototypeOf(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argCheck = Utils.CheckArguments(arguments, 1);
            if (argCheck.IsAbrupt()) return argCheck;
            if (!(arguments[0] is Object V))
                return Completion.NormalCompletion(BooleanValue.False);
            var comp = @this.ToObject();
            if (comp.IsAbrupt()) return comp;
            var O = comp.value as Object;
            while (true)
            {
                comp = V.GetPrototypeOf();
                if (comp.IsAbrupt()) return comp;
                if (comp.value == NullValue.Instance)
                    return Completion.NormalCompletion(BooleanValue.False);
                if (O == comp.value)
                    return Completion.NormalCompletion(BooleanValue.True);
                V = (comp.value as Object)!;
            }
        }

        private Completion ToObjectString(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (@this == UndefinedValue.Instance)
                return Completion.NormalCompletion(new StringValue("[object Undefined]"));
            if (@this == NullValue.Instance)
                return Completion.NormalCompletion(new StringValue("[object Null]"));
            var O = (@this.ToObject().value as Object)!;
            string builtinTag;
            if (O is ArrayObject)
                builtinTag = "Array";
            else if (O is StringObject)
                builtinTag = "String";
            else if (O is MappedArguments)
                builtinTag = "Arguments";
            else if (O is Callable)
                builtinTag = "Function";
            else if (O is ErrorConstructor)
                builtinTag = "Error";
            else if (O is BooleanObject)
                builtinTag = "Boolean";
            else if (O is NumberObject)
                builtinTag = "Number";
            else if (O is DateObject)
                builtinTag = "Date";
            else if (O is RegExpObject)
                builtinTag = "RegExp";
            else
                builtinTag = "Object";
            var tag = O.Get("@@toStringTag");
            if (tag.IsAbrupt()) return tag;
            if (tag.value is StringValue s)
                builtinTag = s.@string;
            return Completion.NormalCompletion(new StringValue($"[object {builtinTag}]"));
        }
    }
}
