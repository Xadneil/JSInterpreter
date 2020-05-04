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
                return Completion.NormalCompletion(new StringObject(StringValue.Empty));
            }

            var arg = arguments[0].ToJsString();
            if (arg.IsAbrupt()) return arg;

            return Completion.NormalCompletion(new StringObject(arg.value as StringValue));
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget)
        {
            return InternalCall(this, arguments);
        }
    }
}
