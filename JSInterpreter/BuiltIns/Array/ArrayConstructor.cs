using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ArrayConstructor : Constructor
    {
        public ArrayConstructor(FunctionPrototype prototype)
        {
            this.prototype = prototype;
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            throw new NotImplementedException();
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget)
        {
            if (arguments.Count == 0)
                return Completion.NormalCompletion(ArrayObject.ArrayCreate(0));
            var argComp = arguments[0].ToNumber();
            if (argComp.IsAbrupt()) return argComp;
            var arg = argComp.value as NumberValue;
            return Completion.NormalCompletion(ArrayObject.ArrayCreate(arg.number));
        }
    }
}
