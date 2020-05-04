using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class FunctionPrototype : Object
    {
        public FunctionPrototype(ObjectPrototype objectPrototype)
        {
            prototype = objectPrototype;
        }

        public void DefineDeferredProperties(Realm realm)
        {
        }
    }
}
