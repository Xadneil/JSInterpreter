using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class NumberPrototype : Object
    {
        public NumberPrototype(NumberConstructor constructor, Realm realm)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));
        }
    }
}
