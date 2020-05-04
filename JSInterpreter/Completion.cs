using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public enum CompletionType
    {
        Normal,
        Break,
        Continue,
        Return,
        Throw
    }

    public class Completion
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

        public static Completion ThrowTypeError(string message)
        {
            Completion errorInstance = NewErrorInstance(message, i => i.TypeErrorConstructor);
            if (errorInstance.IsAbrupt()) return errorInstance;
            return Throw(errorInstance.value);
        }

        public static Completion ThrowReferenceError(string message)
        {
            Completion errorInstance = NewErrorInstance(message, i => i.ReferenceErrorConstructor);
            if (errorInstance.IsAbrupt()) return errorInstance;
            return Throw(errorInstance.value);
        }

        public static Completion ThrowSyntaxError(string message)
        {
            Completion errorInstance = NewErrorInstance(message, i => i.SyntaxErrorConstructor);
            if (errorInstance.IsAbrupt()) return errorInstance;
            return Throw(errorInstance.value);
        }

        public static Completion ThrowRangeError(string message)
        {
            Completion errorInstance = NewErrorInstance(message, i => i.RangeErrorConstructor);
            if (errorInstance.IsAbrupt()) return errorInstance;
            return Throw(errorInstance.value);
        }

        private static Completion Throw(IValue error)
        {
            return new Completion(CompletionType.Throw, error, null);
        }

        private static Completion NewErrorInstance<T>(string message, Func<Intrinsics, T> constructorFunc) where T : ErrorConstructor
        {
            var constructor = constructorFunc(Interpreter.Instance().CurrentRealm().Intrinsics);
            var errorInstance = constructor.InternalConstruct(new[] { new StringValue($"{constructor.Name}: {message}{Environment.NewLine}{StackTrace}") }, null);
            return errorInstance;
        }

        private static string StackTrace
        {
            get
            {
                var st = new System.Diagnostics.StackTrace(2, fNeedFileInfo: true);
                var sb = new StringBuilder();
                foreach (var frame in st.GetFrames())
                {
                    if (!System.IO.File.Exists(frame.GetFileName()))
                        break;
                    sb.Append("   at ");
                    sb.Append(frame.GetMethod().DeclaringType);
                    sb.Append(".");
                    sb.Append(frame.GetMethod().Name);
                    sb.Append(": ");
                    sb.AppendLine(frame.GetFileLineNumber().ToString());
                }
                return sb.ToString();
            }
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
                return Completion.ThrowReferenceError($"cannot get {reference.referencedName} from unresolvable reference");
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
            return new BooleanCompletion(completionType, value, target, default);
        }
    }

    public class CompletionOr<T> : Completion
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

    public class BooleanCompletion : CompletionOr<bool>
    {
        public BooleanCompletion(CompletionType completionType, IValue value, string target) : base(completionType, value, target)
        {
        }

        public BooleanCompletion(CompletionType completionType, IValue value, string target, bool other) : base(completionType, value, target, other)
        {
        }

        public static implicit operator BooleanCompletion(bool b)
        {
            return new BooleanCompletion(CompletionType.Normal, b ? BooleanValue.True : BooleanValue.False, null, b);
        }
    }
}
