using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class ObjectPrototype : Object
    {
        public static ObjectPrototype Instance = new ObjectPrototype();

        private ObjectPrototype() { }
    }
}
