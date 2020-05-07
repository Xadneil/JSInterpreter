using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ArrayConstructor : Constructor
    {
        public ArrayConstructor(FunctionPrototype prototype)
        {
            this.prototype = prototype;

        }

        internal void InitPrototypeProperty(ArrayPrototype prototype)
        {
            DefinePropertyOrThrow("prototype", new PropertyDescriptor(prototype, true, false, false));
        }

        public override Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments)
        {
            throw new NotImplementedException();
        }

        public override Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget)
        {
            if (newTarget == null)
                newTarget = this;
            var proto = Utils.GetPrototypeFromConstructor(newTarget, i => i.ArrayPrototype);

            if (proto.IsAbrupt()) return proto;

            if (arguments.Count == 0)
            {
                return Completion.NormalCompletion(ArrayObject.ArrayCreate(0, proto.value as Object));
            }
            else if (arguments.Count == 1)
            {
                var array = ArrayObject.ArrayCreate(0, proto.value as Object);

                int intLen;

                if (!(arguments[0] is NumberValue))
                {
                    var defineStatus = Utils.CreateDataProperty(array, "0", arguments[0]);
                    if (!defineStatus.Other)
                        throw new InvalidOperationException("CreateDataProperty [0] in array constructor failed. Spec 22.1.1.2 step 6b");
                    intLen = 1;
                }
                else
                {
                    intLen = (int)(arguments[0] as NumberValue).number;
                    if (intLen != (arguments[0] as NumberValue).number || intLen < 0)
                        return Completion.ThrowRangeError("length argument cannot be used as a length");
                }
                array.Set("length", new NumberValue(intLen), true);
                return Completion.NormalCompletion(array);
            }
            else
            {
                var array = ArrayObject.ArrayCreate(arguments.Count, proto.value as Object);
                int k = 0;
                foreach (var arg in arguments)
                {
                    var defineStatus = Utils.CreateDataProperty(array, k.ToString(), arg);
                    if (!defineStatus.Other)
                        throw new InvalidOperationException("CreateDataProperty in array constructor failed. Spec 22.1.1.3 step 8d");
                    k++;
                }
                if ((array.Get("length").value as NumberValue).number != arguments.Count)
                    throw new InvalidOperationException("array length is invalid. Spec 22.1.1.3 step 9");
                return Completion.NormalCompletion(array);
            }
        }
    }
}
