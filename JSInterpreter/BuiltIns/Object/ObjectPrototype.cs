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

            DefinePropertyOrThrow("getPrototypeOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(getPrototypeOf, Utils.EmptyList<string>(), realm), true, false, true));
            DefinePropertyOrThrow("isPrototypeOf", new PropertyDescriptor(Utils.CreateBuiltinFunction(isPrototypeOf, Utils.EmptyList<string>(), realm), true, false, true));
            DefinePropertyOrThrow("preventExtensions", new PropertyDescriptor(Utils.CreateBuiltinFunction(preventExtensions, Utils.EmptyList<string>(), realm), true, false, true));
            DefinePropertyOrThrow("toString", new PropertyDescriptor(Utils.CreateBuiltinFunction(ToObjectString, Utils.EmptyList<string>(), realm), true, false, true));

#warning add remaining Object Prototype properties
        }

        private Completion getPrototypeOf(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argCheck = Utils.CheckArguments(arguments, 1);
            if (argCheck.IsAbrupt()) return argCheck;
            var O = arguments[0].ToObject();
            if (O.IsAbrupt()) return O;
            return (O.value as Object).GetPrototypeOf();
        }

        private Completion preventExtensions(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argCheck = Utils.CheckArguments(arguments, 1);
            if (argCheck.IsAbrupt()) return argCheck;
            if (!(arguments[0] is Object O))
                return Completion.NormalCompletion(arguments[0]);
            var status = O.PreventExtensions();
            if (status == false)
                return Completion.ThrowTypeError("Could not prevent extensions");
            return Completion.NormalCompletion(O);
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
                V = comp.value as Object;
            }
        }

        private Completion ToObjectString(IValue @this, IReadOnlyList<IValue> arguments)
        {
            if (@this == UndefinedValue.Instance)
                return Completion.NormalCompletion(new StringValue("[object Undefined]"));
            if (@this == NullValue.Instance)
                return Completion.NormalCompletion(new StringValue("[object Null]"));
            var O = @this.ToObject().value as Object;
            string builtinTag;
            if (O is ArrayObject)
                builtinTag = "Array";
            else if (O is StringObject)
                builtinTag = "String";
            else if (O.HasInternalSlot("ParameterMap"))
                builtinTag = "Arguments";
            else if (O is Callable || O.HasInternalSlot("Call"))
                builtinTag = "Function";
            else if (O is ErrorConstructor || O.HasInternalSlot("ErrorData"))
                builtinTag = "Error";
            else if (O is BooleanObject || O.HasInternalSlot("BooleanData"))
                builtinTag = "Boolean";
            else if (O is NumberObject || O.HasInternalSlot("NumberData"))
                builtinTag = "Number";
            //TODO date object
            else if (/*O is NativeError || */O.HasInternalSlot("DateValue"))
                builtinTag = "Date";
            //TODO regexpmatcher object
            else if (O is ErrorConstructor || O.HasInternalSlot("RegExpMatcher"))
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
