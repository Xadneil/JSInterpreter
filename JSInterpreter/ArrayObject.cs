using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ArrayObject : Object
    {
        public static ArrayObject ArrayCreate(double lengthInteger, Object proto = null)
        {
            var length = (int)lengthInteger;
            if (proto == null)
                proto = ArrayPrototype.Instance;
            var A = new ArrayObject();
            A.prototype = proto;
            A.IsExtensible = true;
            A.OrdinaryDefineOwnProperty("length", new PropertyDescriptor(new NumberValue(length), true, false, false));
            return A;
        }

        public override BooleanCompletion DefineOwnProperty(string P, PropertyDescriptor Desc)
        {
            if (P == "length")
                return ArraySetLength(Desc);
            else if (int.TryParse(P, out int index))
            {
                var oldLenDescComp = OrdinaryGetOwnProperty("length");
                if (oldLenDescComp.IsAbrupt() || oldLenDescComp.Other == null || !(oldLenDescComp.Other.Value is NumberValue))
                    throw new InvalidOperationException("Spec 9.4.2.1 Step 3b");
                var oldLenDesc = oldLenDescComp.Other;
                var oldLen = (int)(oldLenDesc.Value as NumberValue).number;

                if (index >= oldLen && oldLenDesc.Writable.HasValue && oldLenDesc.Writable.Value)
                    return false;
                var succeeded = OrdinaryDefineOwnProperty(P, Desc).Other;
                if (succeeded == false)
                    return false;
                if (index >= oldLen)
                {
                    oldLenDesc.Value = new NumberValue(index + 1);
                    var s = OrdinaryDefineOwnProperty("length", oldLenDesc);
                    if (s.IsAbrupt() || s.Other == false)
                        throw new InvalidOperationException("Spec 9.4.2.1 step 3hiii");
                }
                return true;
            }
            return OrdinaryDefineOwnProperty(P, Desc);
        }

        private BooleanCompletion ArraySetLength(PropertyDescriptor Desc)
        {
            if (Desc.Value == null)
            {
                return OrdinaryDefineOwnProperty("length", Desc);
            }
            var newLenDesc = new PropertyDescriptor(Desc.Value, Desc.Writable, Desc.Enumerable, Desc.Configurable);
            var newLen = (int)(Desc.Value as NumberValue).number;
            var oldLenDescComp = OrdinaryGetOwnProperty("length");
            if (oldLenDescComp.IsAbrupt() || oldLenDescComp.Other == null || !(oldLenDescComp.Other.Value is NumberValue))
                throw new InvalidOperationException("Spec 9.4.2.4 Step 8");
            var oldLenDesc = oldLenDescComp.Other;
            var oldLen = (int)(oldLenDesc.Value as NumberValue).number;
            if (oldLen >= newLen)
                return OrdinaryDefineOwnProperty("length", newLenDesc);
            if (oldLenDesc.Writable.HasValue && !oldLenDesc.Writable.Value)
                return false;
            bool newWritable;
            if (!newLenDesc.Writable.HasValue || newLenDesc.Writable.Value == true)
                newWritable = true;
            else
            {
                newWritable = false;
                newLenDesc.Writable = true;
            }
            var succeeded = OrdinaryDefineOwnProperty("length", newLenDesc).Other;
            if (succeeded == false)
                return false;
            while (newLen < oldLen)
            {
                oldLen--;
                var deleteSucceeded = Delete(oldLen.ToString()).Other;
                if (deleteSucceeded == false)
                {
                    newLenDesc.Value = new NumberValue(oldLen + 1);
                    if (!newWritable) newLenDesc.Writable = false;
                    OrdinaryDefineOwnProperty("length", newLenDesc);
                    return false;
                }
            }
            if (!newWritable)
                return OrdinaryDefineOwnProperty("length", new PropertyDescriptor(value: null, writable: false, enumerable: null, configurable: null));
            return true;
        }
    }
}
