using System;

namespace JSInterpreter.Parser
{
    public class TailOperatorContainer<TRHS, TOp> where TOp : Enum
    {
        public TRHS RHS { get; private set; }
        public TOp Op { get; private set; }
        public TailOperatorContainer<TRHS, TOp> Tail { get; private set; }

        public TailOperatorContainer(TRHS rhs, TOp op, TailOperatorContainer<TRHS, TOp> tail)
        {
            RHS = rhs;
            Op = op;
            Tail = tail;
        }
    }
}
