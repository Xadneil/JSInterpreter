using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class NumberConstructor : Constructor
    {
        public NumberConstructor(FunctionPrototype prototype)
        {
            this.prototype = prototype;
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));

            DefinePropertyOrThrow("POSITIVE_INFINITY", new PropertyDescriptor(new NumberValue(double.PositiveInfinity), false, false, false));
            DefinePropertyOrThrow("NEGATIVE_INFINITY", new PropertyDescriptor(new NumberValue(double.NegativeInfinity), false, false, false));
            DefinePropertyOrThrow("NaN", new PropertyDescriptor(new NumberValue(double.NaN), false, false, false));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            if (arguments.Count == 0)
                return Completion.NormalCompletion(NumberValue.PositiveZero);
            return arguments[0].ToNumber();
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object? newTarget)
        {
            NumberValue value;
            if (arguments.Count == 0)
                value = NumberValue.PositiveZero;
            else
            {
                var comp = arguments[0].ToNumber();
                if (comp.IsAbrupt()) return comp;
                value = (comp.value as NumberValue)!;
            }
            return Completion.NormalCompletion(new NumberObject(value)
            {
                prototype = Interpreter.Instance().CurrentRealm().Intrinsics.NumberPrototype
            });
        }
    }
}
