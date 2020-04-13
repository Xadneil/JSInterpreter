using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class ArrayPrototype : Object
    {
        private static ArrayPrototype instance;
        public static ArrayPrototype Instance
        {
            get
            {
                if (instance == null)
                    return instance = new ArrayPrototype();
                return instance;
            }
        }
    }
}
