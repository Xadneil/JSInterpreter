using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    enum CompletionType
    {
        Normal,
        Break,
        Continue,
        Return,
        Throw
    }

    class Completion
    {
        public readonly CompletionType completionType;
        public readonly IValue value;
        public readonly string target;

        public Completion(CompletionType completionType, IValue value, string target)
        {
            this.completionType = completionType;
            this.value = value;
            this.target = target;
        }

        public static Completion NormalCompletion(IValue value)
        {
            return new Completion(CompletionType.Normal, value, null);
        }

        public static Completion NormalCompletion()
        {
            return new Completion(CompletionType.Normal, null, null);
        }

        public static Completion Throw(Object error)
        {
            return new Completion(CompletionType.Throw, error, null);
        }

        public static Completion ThrowTypeError()
        {
            return new Completion(CompletionType.Throw, new NativeError(), null);
        }

        public static Completion ThrowTypeError(string message)
        {
            return new Completion(CompletionType.Throw, new NativeError(message), null);
        }

        public static Completion ThrowReferenceError()
        {
            return new Completion(CompletionType.Throw, new NativeError(), null);
        }

        public static Completion ThrowReferenceError(string message)
        {
            return new Completion(CompletionType.Throw, new NativeError(message), null);
        }

        public bool IsAbrupt()
        {
            return completionType != CompletionType.Normal;
        }

        public Completion UpdateEmpty(IValue possiblevalue)
        {
            if ((completionType == CompletionType.Return || completionType == CompletionType.Throw) && value == null)
                throw new InvalidOperationException("Spec 6.2.3.4");
            if (value != null) return this;
            return new Completion(completionType, possiblevalue, target);
        }

        public Completion GetValue()
        {
            if (IsAbrupt()) return this;
            if (!(value is ReferenceValue reference))
                return this;

            var @base = reference.baseValue;
            if (reference.IsUnresolvableReference())
                return Completion.ThrowReferenceError();
            if (reference.IsPropertyReference())
            {
                if (reference.HasPrimitiveBase())
                {
                    @base = (@base as IValue).ToObject().value;
                }
                var obj = @base as Object;
                return obj.InternalGet(reference.referencedName, reference.GetThisValue());
            }
            if (!(@base is EnvironmentRecord envRec))
                throw new InvalidOperationException("Completion.GetValue: baseValue is not a recognized IReferenceable");
            return envRec.GetBindingValue(reference.referencedName, reference.strict);
        }

        public static CompletionOr<T> NormalWith<T>(T other)
        {
            return new CompletionOr<T>(CompletionType.Normal, null, null, other);
        }

        public CompletionOr<T> WithEmpty<T>()
        {
            return new CompletionOr<T>(completionType, value, target);
        }

        public BooleanCompletion WithEmptyBool()
        {
            return new BooleanCompletion(CompletionType.Normal, null, null, default);
        }
    }

    class CompletionOr<T> : Completion
    {
        public readonly T Other;

        public CompletionOr(CompletionType completionType, IValue value, string target) : base(completionType, value, target)
        {
        }

        public CompletionOr(CompletionType completionType, IValue value, string target, T other) : base(completionType, value, target)
        {
            Other = other;
        }
    }

    class BooleanCompletion : CompletionOr<bool>
    {
        public BooleanCompletion(CompletionType completionType, IValue value, string target) : base(completionType, value, target)
        {
        }

        public BooleanCompletion(CompletionType completionType, IValue value, string target, bool other) : base(completionType, value, target, other)
        {
        }

        public static implicit operator BooleanCompletion(bool b)
        {
            return new BooleanCompletion(CompletionType.Normal, null, null, b);
        }
    }
}
