using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    abstract class Constructor : Callable
    {
        public abstract Completion InternalConstruct(IReadOnlyList<IValue> arguments, Object newTarget);
    }
}
