using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class StringObject : Object
    {
        public readonly StringValue value;

        public StringObject(StringValue value)
        {
            this.value = value;
        }
    }
}
