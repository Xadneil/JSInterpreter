using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    class ArrayObject : Object
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

        public override Completion DefineOwnProperty(string P, PropertyDescriptor Desc)
        {
            if (P == "length")
                return ArraySetLength(Desc);
            else if (int.TryParse(P, out int index))
            {
                var oldLenDescComp = OrdinaryGetOwnProperty("length");
                if (oldLenDescComp.IsAbrupt() || oldLenDescComp.propertyDescriptor == null || !(oldLenDescComp.propertyDescriptor.Value is NumberValue))
                    throw new InvalidOperationException("Spec 9.4.2.1 Step 3b");
                var oldLenDesc = oldLenDescComp.propertyDescriptor;
                var oldLen = (int)(oldLenDesc.Value as NumberValue).number;

                if (index >= oldLen && oldLenDesc.writeable.HasValue && oldLenDesc.writeable.Value)
                    return Completion.NormalCompletion(BooleanValue.False);
                var succeeded = OrdinaryDefineOwnProperty(P, Desc).value;
                if (succeeded == BooleanValue.False)
                    return Completion.NormalCompletion(BooleanValue.False);
                if (index >= oldLen)
                {
                    oldLenDesc.Value = new NumberValue(index + 1);
                    var s = OrdinaryDefineOwnProperty("length", oldLenDesc);
                    if (s.IsAbrupt() || s.value != BooleanValue.True)
                        throw new InvalidOperationException("Spec 9.4.2.1 step 3hiii");
                }
                return Completion.NormalCompletion(BooleanValue.True);
            }
            return OrdinaryDefineOwnProperty(P, Desc);
        }

        private Completion ArraySetLength(PropertyDescriptor Desc)
        {
            if (Desc.Value == null)
            {
                return OrdinaryDefineOwnProperty("length", Desc);
            }
            var newLenDesc = new PropertyDescriptor(Desc.Value, Desc.writeable, Desc.enumerable, Desc.configurable);
            var newLen = (int)(Desc.Value as NumberValue).number;
            var oldLenDescComp = OrdinaryGetOwnProperty("length");
            if (oldLenDescComp.IsAbrupt() || oldLenDescComp.propertyDescriptor == null || !(oldLenDescComp.propertyDescriptor.Value is NumberValue))
                throw new InvalidOperationException("Spec 9.4.2.4 Step 8");
            var oldLenDesc = oldLenDescComp.propertyDescriptor;
            var oldLen = (int)(oldLenDesc.Value as NumberValue).number;
            if (oldLen >= newLen)
                return OrdinaryDefineOwnProperty("length", newLenDesc);
            if (oldLenDesc.writeable.HasValue && !oldLenDesc.writeable.Value)
                return Completion.NormalCompletion(BooleanValue.False);
            bool newWritable;
            if (!newLenDesc.writeable.HasValue || newLenDesc.writeable.Value == true)
                newWritable = true;
            else
            {
                newWritable = false;
                newLenDesc.writeable = true;
            }
            var succeeded = OrdinaryDefineOwnProperty("length", newLenDesc).value as BooleanValue;
            if (succeeded == BooleanValue.False)
                return Completion.NormalCompletion(BooleanValue.False);
            while (newLen < oldLen)
            {
                oldLen--;
                var deleteSucceeded = Delete(oldLen.ToString()).value as BooleanValue;
                if (deleteSucceeded == BooleanValue.False)
                {
                    newLenDesc.Value = new NumberValue(oldLen + 1);
                    if (!newWritable) newLenDesc.writeable = false;
                    OrdinaryDefineOwnProperty("length", newLenDesc);
                    return Completion.NormalCompletion(BooleanValue.False);
                }
            }
            if (!newWritable)
                return OrdinaryDefineOwnProperty("length", new PropertyDescriptor(Value: null, writeable: false, enumerable: null, configurable: null));
            return Completion.NormalCompletion(BooleanValue.True);
        }
    }
}
