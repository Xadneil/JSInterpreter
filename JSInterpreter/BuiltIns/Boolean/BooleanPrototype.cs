using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class BooleanPrototype : BooleanObject
    {
        public BooleanPrototype(BooleanConstructor constructor, Realm realm) : base(BooleanValue.False, null)
        {
            DefinePropertyOrThrow("constructor", new PropertyDescriptor(constructor, true, false, true));
            this.prototype = realm.Intrinsics.ObjectPrototype;
        }
    }
}
