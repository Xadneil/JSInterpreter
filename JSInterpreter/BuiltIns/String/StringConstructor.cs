using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class StringConstructor : Constructor
    {
        public StringConstructor(FunctionPrototype prototype)
        {
            this.prototype = prototype;
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            if (arguments.Count == 0)
            {
                return Completion.NormalCompletion(StringValue.Empty);
            }

            var arg = arguments[0].ToJsString();
            if (arg.IsAbrupt()) return arg;

            return Completion.NormalCompletion(arg.value);
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget)
        {
            var comp = InternalCall(this, arguments);
            if (comp.IsAbrupt()) return comp;
            if (newTarget == null)
                return comp;
            return Completion.NormalCompletion(new StringObject(comp.value as StringValue));
        }
    }
}
