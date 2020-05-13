using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public abstract class Constructor : Callable
    {
        public abstract Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object? newTarget);

        public Completion Construct(IReadOnlyList<IValue>? arguments = null, Object? newTarget = null)
        {
            if (newTarget == null)
                newTarget = this;
            if (arguments == null)
                arguments = Utils.EmptyList<IValue>();
            return InternalConstruct(arguments, newTarget);
        }
    }
}
