namespace JSInterpreter.Parser
{
    public class TailContainer<T>
    {
        public T RHS { get; private set; }
        public TailContainer<T>? Tail { get; private set; }

        public TailContainer(T rhs, TailContainer<T>? tail)
        {
            RHS = rhs;
            Tail = tail;
        }
    }
}
