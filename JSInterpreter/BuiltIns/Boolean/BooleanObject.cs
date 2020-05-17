using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class BooleanObject : Object
    {
        public BooleanValue Value;

        public BooleanObject(BooleanValue value)
        {
            Value = value;
            prototype = Interpreter.Instance().CurrentRealm().Intrinsics.BooleanPrototype;
        }

        protected BooleanObject(BooleanValue value, Object? prototype)
        {
            Value = value;
            this.prototype = prototype;
        }
    }
}
