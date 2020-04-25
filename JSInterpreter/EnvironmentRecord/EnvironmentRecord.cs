namespace JSInterpreter
{
    public abstract class EnvironmentRecord : IReferenceable
    {
        public abstract BooleanCompletion HasBinding(string name);

        public abstract Completion CreateImmutableBinding(string name, bool strict);

        public abstract Completion CreateMutableBinding(string name, bool deletable);

        public abstract Completion InitializeBinding(string name, IValue value);

        public abstract Completion SetMutableBinding(string name, IValue value, bool strict);

        public abstract Completion GetBindingValue(string name, bool strict);

        public abstract BooleanCompletion DeleteBinding(string name);

        public abstract bool HasThisBinding();

        public abstract bool HasSuperBinding();

        public bool IsPrimitive()
        {
            return false;
        }
    }
}
