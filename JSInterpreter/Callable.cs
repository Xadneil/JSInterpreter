using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public abstract class Callable : Object
    {
        public abstract Completion InternalCall(IValue thisValue, IReadOnlyList<IValue> arguments);

        public Completion Call(IValue thisValue, IReadOnlyList<IValue>? arguments = null)
        {
            return InternalCall(thisValue, arguments ?? Utils.EmptyList<IValue>());
        }

        public Completion OrdinaryHasInstance(IValue O)
        {
            if (!(O is Object obj))
                return Completion.NormalCompletion(BooleanValue.False);
            var PComp = Get("prototype");
            if (PComp.IsAbrupt()) return PComp;
            var P = PComp.value;
            if (!(P is Object protoObj))
                return Completion.ThrowTypeError("prototype is not an object");
            while (true)
            {
                var objComp = obj.GetPrototypeOf();
                if (objComp.IsAbrupt()) return objComp;
                obj = (objComp.value as Object)!;
                if (obj == null)
                    return Completion.NormalCompletion(BooleanValue.False);
                if (obj == protoObj)
                    return Completion.NormalCompletion(BooleanValue.True);
            }
        }
    }
}
