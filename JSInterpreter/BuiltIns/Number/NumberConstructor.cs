using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class NumberConstructor : Constructor
    {
        public NumberConstructor(FunctionPrototype prototype, Realm realm)
        {
            this.prototype = prototype;
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));

            DefinePropertyOrThrow("isFinite", new PropertyDescriptor(Utils.CreateBuiltinFunction(isFinite, realm), true, false, false));
            DefinePropertyOrThrow("isInteger", new PropertyDescriptor(Utils.CreateBuiltinFunction(isInteger, realm), true, false, false));
            DefinePropertyOrThrow("isNaN", new PropertyDescriptor(Utils.CreateBuiltinFunction(isNaN, realm), true, false, false));
            DefinePropertyOrThrow("isSafeInteger", new PropertyDescriptor(Utils.CreateBuiltinFunction(isSafeInteger, realm), true, false, false));
            DefinePropertyOrThrow("parseFloat", new PropertyDescriptor(Utils.CreateBuiltinFunction(GlobalObjectProperties.parseFloat, realm), true, false, false));
            DefinePropertyOrThrow("parseInt", new PropertyDescriptor(Utils.CreateBuiltinFunction(GlobalObjectProperties.parseInt, realm), true, false, false));

            DefinePropertyOrThrow("EPSILON", new PropertyDescriptor(new NumberValue(2.2204460492503130808472633361816E-16), false, false, false));
            DefinePropertyOrThrow("MAX_SAFE_INTEGER", new PropertyDescriptor(new NumberValue((1 << 53) - 1), false, false, false));
            DefinePropertyOrThrow("MAX_VALUE", new PropertyDescriptor(new NumberValue(double.MaxValue), false, false, false));
            DefinePropertyOrThrow("MIN_SAFE_INTEGER", new PropertyDescriptor(new NumberValue(-((1 << 53) - 1)), false, false, false));
            DefinePropertyOrThrow("MIN_VALUE", new PropertyDescriptor(new NumberValue(double.Epsilon), false, false, false));
            DefinePropertyOrThrow("NaN", new PropertyDescriptor(new NumberValue(double.NaN), false, false, false));
            DefinePropertyOrThrow("NEGATIVE_INFINITY", new PropertyDescriptor(new NumberValue(double.NegativeInfinity), false, false, false));
            DefinePropertyOrThrow("POSITIVE_INFINITY", new PropertyDescriptor(new NumberValue(double.PositiveInfinity), false, false, false));
        }

        private static Completion isFinite(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var number = arguments.At(0, UndefinedValue.Instance);
            if (!(number is NumberValue n))
                return Completion.NormalCompletion(BooleanValue.False);
            if (double.IsNaN(n.number) || double.IsInfinity(n.number))
                return Completion.NormalCompletion(BooleanValue.False);
            return Completion.NormalCompletion(BooleanValue.True);
        }

        private static Completion isInteger(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var number = arguments.At(0, UndefinedValue.Instance);
            if (!(number is NumberValue n))
                return Completion.NormalCompletion(BooleanValue.False);
            if (double.IsNaN(n.number) || double.IsInfinity(n.number))
                return Completion.NormalCompletion(BooleanValue.False);
            if ((int)n.number != n.number)
                return Completion.NormalCompletion(BooleanValue.False);
            return Completion.NormalCompletion(BooleanValue.True);
        }

        private static Completion isNaN(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var number = arguments.At(0, UndefinedValue.Instance);
            if (!(number is NumberValue n))
                return Completion.NormalCompletion(BooleanValue.False);
            if (double.IsNaN(n.number))
                return Completion.NormalCompletion(BooleanValue.True);
            return Completion.NormalCompletion(BooleanValue.False);
        }

        private static Completion isSafeInteger(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            var number = arguments.At(0, UndefinedValue.Instance);
            if (!(number is NumberValue n))
                return Completion.NormalCompletion(BooleanValue.False);
            if (double.IsNaN(n.number) || double.IsInfinity(n.number))
                return Completion.NormalCompletion(BooleanValue.False);
            if ((int)n.number != n.number)
                return Completion.NormalCompletion(BooleanValue.False);
            if (Math.Abs((int)n.number) <= (1 << 53) - 1)
                return Completion.NormalCompletion(BooleanValue.True);
            return Completion.NormalCompletion(BooleanValue.False);
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
