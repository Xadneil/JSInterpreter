using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class NumberObject : Object
    {
        public readonly double value;

        public NumberObject(NumberValue value)
        {
            this.value = value.number;
            prototype = Interpreter.Instance().CurrentRealm().Intrinsics.NumberPrototype;
        }
    }
}
