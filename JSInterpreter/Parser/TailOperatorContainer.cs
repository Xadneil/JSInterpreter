using System;

namespace JSInterpreter.Parser
{
    public class TailOperatorContainer<T, O> where O : Enum
    {
        public T RHS;
        public O Op;
        public TailOperatorContainer<T, O> Tail;

        public TailOperatorContainer(T rhs, O op, TailOperatorContainer<T, O> tail)
        {
            RHS = rhs;
            Op = op;
            Tail = tail;
        }
    }
}
