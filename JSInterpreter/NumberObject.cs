using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class NumberObject : Object
    {
        public readonly NumberValue value;

        public NumberObject(NumberValue value)
        {
            this.value = value;
        }
    }
}
