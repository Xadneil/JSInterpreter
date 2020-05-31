using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class NumberObject : Object
    {
        public readonly NumberValue value;

        public NumberObject(NumberValue value)
        {
            this.value = value;
            prototype = Interpreter.Instance().CurrentRealm().Intrinsics.NumberPrototype;
        }
    }
}
