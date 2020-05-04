using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ObjectConstructor : Constructor
    {
        public ObjectConstructor()
        {
            prototype = new ObjectPrototype(this);
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));

            DefinePropertyOrThrow("length", new PropertyDescriptor(new NumberValue(1), false, false, false));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            if (arguments.Count == 0)
            {
                return Construct(arguments);
            }

            if (arguments[0] == NullValue.Instance || arguments[0] == UndefinedValue.Instance)
            {
                return Construct(arguments);
            }

            return arguments[0].ToObject();
        }

        public Completion Construct(IReadOnlyList<IValue> arguments)
        {
            return InternalConstruct(arguments, this);
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget)
        {
            if (arguments.Count == 0)
            {
                return Completion.NormalCompletion(Utils.ObjectCreate(Interpreter.Instance().CurrentRealm().Intrinsics.ObjectPrototype));
            }
            return InternalCall(this, arguments);
        }
    }
}
