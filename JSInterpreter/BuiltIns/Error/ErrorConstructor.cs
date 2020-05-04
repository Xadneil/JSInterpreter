using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ErrorConstructor : Constructor
    {
        public readonly string Name;
        private readonly Func<Intrinsics, Object> dispatchPrototype;

        public ErrorConstructor(FunctionPrototype prototype, string name)
        {
            this.prototype = prototype;
            Name = name;
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));
            DefinePropertyOrThrow("name", new PropertyDescriptor(new StringValue(name), false, false, true));

            dispatchPrototype = name switch
            {
                "RangeError" => i => i.RangeErrorPrototype,
                "ReferenceError" => i => i.ReferenceErrorPrototype,
                "SyntaxError" => i => i.SyntaxErrorPrototype,
                "TypeError" => i => i.TypeErrorPrototype,
                "URIError" => i => i.URIErrorPrototype,
                "Error" => i => i.ErrorPrototype,
                _ => throw new InvalidOperationException($"Invalid error constructor name {name}")
            };
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            return InternalConstruct(arguments, thisValue as Object);
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget)
        {
            var OComp = Utils.OrdinaryCreateFromConstructor(newTarget ?? this, dispatchPrototype);
            if (OComp.IsAbrupt()) return OComp;
            var O = OComp.value as Object;
            if (arguments.Count > 0 && arguments[0] != UndefinedValue.Instance)
            {
                var msg = arguments[0].ToJsString();
                if (msg.IsAbrupt()) return msg;
                O.DefinePropertyOrThrow("message", new PropertyDescriptor(msg.value, true, false, true));
            }
            return Completion.NormalCompletion(O);
        }
    }
}
