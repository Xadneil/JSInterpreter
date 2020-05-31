using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class BooleanConstructor : Constructor
    {
        public BooleanConstructor(FunctionPrototype prototype)
        {
            this.prototype = prototype;
        }

        public void InitPrototypeProperty(BooleanPrototype prototype)
        {
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var b = arguments.At(0, UndefinedValue.Instance).ToBoolean();
            return Completion.NormalCompletion(b);

        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object? newTarget)
        {
            var booleanLiteral = InternalCall(UndefinedValue.Instance, arguments);
            if (booleanLiteral.IsAbrupt()) return booleanLiteral;
            var O = new BooleanObject((booleanLiteral.value as BooleanValue)!);
            return Completion.NormalCompletion(O);
        }
    }
}
