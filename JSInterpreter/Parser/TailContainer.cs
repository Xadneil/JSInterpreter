namespace JSInterpreter.Parser
{
    public class TailContainer<T>
    {
        public T RHS;
        public TailContainer<T> Tail;

        public TailContainer(T rhs, TailContainer<T> tail)
        {
            RHS = rhs;
            Tail = tail;
        }
    }
}
