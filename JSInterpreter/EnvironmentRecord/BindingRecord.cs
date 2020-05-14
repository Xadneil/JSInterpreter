namespace JSInterpreter
{
    public class BindingRecord
    {
        public readonly bool mutable;
        public readonly bool? canDelete;
        public readonly bool? strict;
        public IValue? Value;

        public static BindingRecord Mutable(bool canDelete)
        {
            return new BindingRecord(true, canDelete, null);
        }

        public static BindingRecord Immutable(bool strict)
        {
            return new BindingRecord(false, null, strict);
        }

        private BindingRecord(bool mutable, bool? canDelete, bool? strict)
        {
            this.mutable = mutable;
            this.canDelete = canDelete;
            this.strict = strict;
        }
    }
}
