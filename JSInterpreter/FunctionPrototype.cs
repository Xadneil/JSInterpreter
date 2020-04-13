using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class FunctionPrototype : Object
    {
        public static FunctionPrototype Instance = new FunctionPrototype();

        private FunctionPrototype()
        {
            SetPrototypeOf(ObjectPrototype.Instance);
            DefineOwnProperty("length", new PropertyDescriptor(new NumberValue(0), null, null, null));
            DefineOwnProperty("name", new PropertyDescriptor(new StringValue(""), null, null, null));
        }
    }
}
