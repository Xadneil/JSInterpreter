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
#warning add boolean prototype
            //prototype = Interpreter.Instance().CurrentRealm().Intrinsics.BooleanPrototype;
        }
    }
}
