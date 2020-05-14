using System;

namespace JSInterpreter
{
    public enum ThisBindingStatus
    {
        Lexical,
        Initialized,
        Uninitialized
    }

    public class FunctionEnvironmentRecord : DeclarativeEnvironmentRecord
    {
        public IValue? ThisValue { get; private set; }
        public ThisBindingStatus ThisBindingStatus { get; private set; }
        public FunctionObject FunctionObject { get; private set; }
        public IValue HomeObject { get; private set; } = UndefinedValue.Instance;
        public IValue NewTarget { get; private set; } = UndefinedValue.Instance;

        public FunctionEnvironmentRecord(FunctionObject F, IValue newTarget)
        {
            FunctionObject = F;
            if (F.ThisMode == ThisMode.Lexical)
                ThisBindingStatus = ThisBindingStatus.Lexical;
            else
                ThisBindingStatus = ThisBindingStatus.Uninitialized;
            HomeObject = F.HomeObject ?? UndefinedValue.Instance;
            NewTarget = newTarget;
        }

        public Completion BindThisValue(IValue value)
        {
            if (ThisBindingStatus == ThisBindingStatus.Lexical)
                throw new InvalidOperationException("FunctionEnvironmentRecord.BindThisValue: ThisBindingStatus must not be Lexical");
            if (ThisBindingStatus == ThisBindingStatus.Initialized)
                return Completion.ThrowReferenceError("'this' value is already initialized");
            ThisValue = value;
            ThisBindingStatus = ThisBindingStatus.Initialized;
            return Completion.NormalCompletion(value);
        }

        public override bool HasThisBinding()
        {
            return ThisBindingStatus != ThisBindingStatus.Lexical;
        }

        public override bool HasSuperBinding()
        {
            if (ThisBindingStatus == ThisBindingStatus.Lexical)
                return false;
            return HomeObject != UndefinedValue.Instance;
        }

        public Completion GetThisBinding()
        {
            if (ThisBindingStatus == ThisBindingStatus.Lexical)
                throw new InvalidOperationException("Spec 8.1.1.3.4 step 2");
            if (ThisBindingStatus == ThisBindingStatus.Uninitialized)
                return Completion.ThrowReferenceError("'this' value is not initialized");
            return Completion.NormalCompletion(ThisValue);
        }

        public Completion GetSuperBase()
        {
            if (HomeObject == UndefinedValue.Instance) return Completion.NormalCompletion(UndefinedValue.Instance);
            if (!(HomeObject is Object o))
                throw new InvalidOperationException("Spec 8.1.1.3.5 step 4");
            return o.GetPrototypeOf();
        }
    }
}
