using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ErrorPrototype : Object
    {
        public ErrorPrototype(ErrorConstructor constructor)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));
        }
    }
}
