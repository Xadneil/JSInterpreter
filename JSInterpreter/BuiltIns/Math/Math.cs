using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.BuiltIns.Math
{
    public class Math : Object
    {
        public Math(ObjectPrototype prototype, Realm realm)
        {
            this.prototype = prototype;

            DefinePropertyOrThrow("E", new PropertyDescriptor(new NumberValue(System.Math.E), false, false, false));
            DefinePropertyOrThrow("LN2", new PropertyDescriptor(new NumberValue(System.Math.Log(2)), false, false, false));

            DefinePropertyOrThrow("abs", new PropertyDescriptor(Utils.CreateBuiltinFunction(abs, realm: realm), true, false, true));
            DefinePropertyOrThrow("ceil", new PropertyDescriptor(Utils.CreateBuiltinFunction(ceil, realm: realm), true, false, true));
            DefinePropertyOrThrow("floor", new PropertyDescriptor(Utils.CreateBuiltinFunction(floor, realm: realm), true, false, true));
            DefinePropertyOrThrow("log", new PropertyDescriptor(Utils.CreateBuiltinFunction(log, realm: realm), true, false, true));
            DefinePropertyOrThrow("min", new PropertyDescriptor(Utils.CreateBuiltinFunction(min, realm: realm), true, false, true));
            DefinePropertyOrThrow("pow", new PropertyDescriptor(Utils.CreateBuiltinFunction(pow, realm: realm), true, false, true));
        }

        public static Completion abs(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var xComp = arguments.At(0);
            if (xComp.IsAbrupt()) return xComp;
            xComp = xComp.value!.ToNumber();
            if (xComp.IsAbrupt()) return xComp;
            var x = (xComp.value as NumberValue)!.number;
            return Completion.NormalCompletion(new NumberValue(System.Math.Abs(x)));
        }

        public static Completion ceil(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var xComp = arguments.At(0);
            if (xComp.IsAbrupt()) return xComp;
            xComp = xComp.value!.ToNumber();
            if (xComp.IsAbrupt()) return xComp;
            var x = (xComp.value as NumberValue)!.number;
            return Completion.NormalCompletion(new NumberValue(System.Math.Ceiling(x)));
        }

        public static Completion floor(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var xComp = arguments.At(0);
            if (xComp.IsAbrupt()) return xComp;
            xComp = xComp.value!.ToNumber();
            if (xComp.IsAbrupt()) return xComp;
            var x = (xComp.value as NumberValue)!.number;
            return Completion.NormalCompletion(new NumberValue(System.Math.Floor(x)));
        }

        public static Completion log(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var xComp = arguments.At(0);
            if (xComp.IsAbrupt()) return xComp;
            xComp = xComp.value!.ToNumber();
            if (xComp.IsAbrupt()) return xComp;
            var x = (xComp.value as NumberValue)!.number;
            return Completion.NormalCompletion(new NumberValue(System.Math.Log(x)));
        }

        public static Completion min(IValue @this, IReadOnlyList<IValue> arguments)
        {
            double minValue = double.PositiveInfinity;
            foreach (var arg in arguments)
            {
                var xComp = arguments.At(0);
                if (xComp.IsAbrupt()) return xComp;
                xComp = xComp.value!.ToNumber();
                if (xComp.IsAbrupt()) return xComp;
                var x = (xComp.value as NumberValue)!.number;

                if (double.IsNaN(x))
                    return Completion.NormalCompletion(NumberValue.DoubleNaN);

                if (x < minValue)
                    minValue = x;
            }

            return Completion.NormalCompletion(new NumberValue(minValue));
        }

        public static Completion pow(IValue @this, IReadOnlyList<IValue> arguments)
        {
            var argComp = Utils.CheckArguments(arguments, 2);
            if (argComp.IsAbrupt()) return argComp;

            var xComp = arguments.At(0);
            if (xComp.IsAbrupt()) return xComp;
            xComp = xComp.value!.ToNumber();
            if (xComp.IsAbrupt()) return xComp;
            var x = (xComp.value as NumberValue)!.number;

            var yComp = arguments.At(1);
            if (yComp.IsAbrupt()) return yComp;
            yComp = yComp.value!.ToNumber();
            if (yComp.IsAbrupt()) return yComp;
            var y = (yComp.value as NumberValue)!.number;

            return Completion.NormalCompletion(new NumberValue(System.Math.Pow(x, y)));
        }

    }
}
