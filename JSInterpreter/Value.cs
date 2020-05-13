using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public interface IValue : IReferenceable
    {
        Completion ToObject();
        Completion ToNumber();
        Completion ToJsString();

        public CompletionOr<string> ToPropertyKey()
        {
            var key = ToPrimitive(PrimitiveHint.String);
            if (key.IsAbrupt()) return key.WithEmpty<string>();
            return Completion.NormalWith((key.value.ToJsString().value as StringValue).@string);
        }

        public BooleanValue ToBoolean()
        {
            return (this switch
            {
                UndefinedValue _ => false,
                NullValue _ => false,
                BooleanValue b => b.boolean,
                NumberValue n => n.number != 0 && !double.IsNaN(n.number),
                StringValue s => s.@string.Length > 0,
                Object _ => true,
                _ => throw new InvalidOperationException("IValue.ToBoolean: unknown conversion")
            }) ? BooleanValue.True : BooleanValue.False;
        }

        public enum PrimitiveHint
        {
            Default, String, Number
        }

        private Completion OrdinaryToPrimitive(PrimitiveHint hint)
        {
            if (!(this is Object obj))
                throw new InvalidOperationException("Spec 7.1.1.1 step 1");
            string[] methodNames = hint switch
            {
                PrimitiveHint.String => new[] { "toString", "valueOf" },
                PrimitiveHint.Number => new[] { "valueOf", "toString" },
                _ => throw new InvalidOperationException("Spec 7.1.1.1 step 2")
            };

            foreach (var name in methodNames)
            {
                var methodComp = obj.Get(name);
                if (methodComp.IsAbrupt()) return methodComp;
                var method = methodComp.value;
                if (method is Callable callable)
                {
                    var resultComp = callable.Call(this);
                    if (resultComp.IsAbrupt()) return resultComp;
                    if (!(resultComp.value is Object)) return resultComp;
                }
            }
            return Completion.ThrowTypeError("ToPrimitive didn't find a toString or valueOf method");
        }

        public Completion ToPrimitive(PrimitiveHint hint = PrimitiveHint.Default)
        {
            if (this is Object)
            {
                //TODO: spec 7.1.1 exoticToPrim
                if (hint == PrimitiveHint.Default)
                    hint = PrimitiveHint.Number;
                return OrdinaryToPrimitive(hint);
            }
            return Completion.NormalCompletion(this);
        }

        public Completion RequireObjectCoercible()
        {
            return this switch
            {
                UndefinedValue _ => Completion.ThrowTypeError("undefined is not an object"),
                NullValue _ => Completion.ThrowTypeError("null is not an object"),
                _ => Completion.NormalCompletion(this)
            };
        }
    }

    public class NumberValue : IValue
    {
        internal static NumberValue One = new NumberValue(1);
        internal static NumberValue PositiveZero = new NumberValue(0);
        internal static NumberValue DoubleNaN = new NumberValue(double.NaN);
        public readonly double number;

        public NumberValue(double number)
        {
            this.number = number;
        }

        public bool IsPrimitive()
        {
            return true;
        }

        public Completion ToJsString()
        {
            return Completion.NormalCompletion(new StringValue(number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public Completion ToNumber()
        {
            return Completion.NormalCompletion(this);
        }

        public Completion ToObject()
        {
            return Completion.NormalCompletion(new NumberObject(this));
        }
    }

    public class BooleanValue : IValue
    {
        public readonly bool boolean;
        public static readonly BooleanValue True = new BooleanValue(true);
        public static readonly BooleanValue False = new BooleanValue(false);

        private readonly Completion wrapperObject;
        private readonly Completion cachedNumber;
        private readonly Completion cachedString;

        private BooleanValue(bool boolean)
        {
            this.boolean = boolean;
            wrapperObject = Completion.NormalCompletion(new BooleanObject(this));
            cachedNumber = Completion.NormalCompletion(new NumberValue(boolean ? 1 : 0));
            cachedString = Completion.NormalCompletion(new StringValue(boolean ? "true" : "false"));
        }

        public bool IsPrimitive()
        {
            return true;
        }

        public Completion ToObject()
        {
            return wrapperObject;
        }

        public Completion ToJsString()
        {
            return cachedString;
        }

        public Completion ToNumber()
        {
            return cachedNumber;
        }
    }

    public class StringValue : IValue
    {
        public readonly string @string;
        public readonly static StringValue Empty = new StringValue("");

        public StringValue(string @string)
        {
            this.@string = @string;
        }

        public bool IsPrimitive()
        {
            return true;
        }

        public Completion ToObject()
        {
            return Completion.NormalCompletion(new StringObject(this));
        }

        public Completion ToJsString()
        {
            return Completion.NormalCompletion(this);
        }

        public Completion ToNumber()
        {
            if (string.IsNullOrWhiteSpace(@string))
                return Completion.NormalCompletion(NumberValue.PositiveZero);
            var trimmed = @string.Trim().ToUpperInvariant();
            if (trimmed.StartsWith("0X", StringComparison.InvariantCulture) &&
                int.TryParse(
                    trimmed.Substring(2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int hexResult))
                return Completion.NormalCompletion(new NumberValue(hexResult));
            if (!double.TryParse(@string.Trim(), out double result))
                return Completion.NormalCompletion(NumberValue.DoubleNaN);
            return Completion.NormalCompletion(new NumberValue(result));
        }
    }

    public class NullValue : IValue
    {
        public readonly static NullValue Instance = new NullValue();

        private readonly NumberValue cachedNumber = new NumberValue(0);
        private readonly StringValue cachedString = new StringValue("null");

        private NullValue() { }

        public bool IsPrimitive()
        {
            return false;
        }

        public Completion ToObject()
        {
            return Completion.ThrowTypeError("Cannot convert null into an object");
        }

        public Completion ToJsString()
        {
            return Completion.NormalCompletion(cachedString);
        }

        public Completion ToNumber()
        {
            return Completion.NormalCompletion(cachedNumber);
        }
    }

    public class UndefinedValue : IValue
    {
        public readonly static UndefinedValue Instance = new UndefinedValue();

        private readonly NumberValue cachedNumber = new NumberValue(double.NaN);
        private readonly StringValue cachedString = new StringValue("undefined");

        private UndefinedValue() { }

        public bool IsPrimitive()
        {
            return false;
        }

        public Completion ToObject()
        {
            return Completion.ThrowTypeError("Cannot convert undefined into an object");
        }

        public Completion ToJsString()
        {
            return Completion.NormalCompletion(cachedString);
        }

        public Completion ToNumber()
        {
            return Completion.NormalCompletion(cachedNumber);
        }
    }

    public interface IReferenceable
    {
        bool IsPrimitive();
    }

    public class ReferenceValue : IValue
    {
        public readonly IReferenceable baseValue;
        public readonly string referencedName;
        public readonly bool strict;

        public ReferenceValue(IReferenceable baseValue, string referencedName, bool strict)
        {
            this.baseValue = baseValue;
            this.referencedName = referencedName;
            this.strict = strict;
        }

        public bool IsPrimitive()
        {
            return false;
        }

        public Completion ToJsString()
        {
            throw new NotImplementedException("references are not real values, no JS string can be created.");
        }

        public Completion ToObject()
        {
            throw new NotImplementedException("references are not real values, no object can be created.");
        }

        public Completion ToNumber()
        {
            throw new NotImplementedException("references are not real values, no number can be created.");
        }

        public bool HasPrimitiveBase()
        {
            return baseValue.IsPrimitive();
        }

        public bool IsPropertyReference()
        {
            return baseValue is Object || baseValue.IsPrimitive();
        }

        public bool IsUnresolvableReference()
        {
            return baseValue == UndefinedValue.Instance;
        }

        public Completion PutValue(IValue W)
        {
            var @base = baseValue;
            if (IsUnresolvableReference())
            {
                if (strict)
                    return Completion.ThrowReferenceError($"Cannot set {referencedName} on unresolvable reference");
                var globalObj = Interpreter.Instance().RunningExecutionContext().Realm.GlobalObject;
                return globalObj.Set(referencedName, W, false);
            }
            else if (IsPropertyReference())
            {
                if (HasPrimitiveBase())
                {
                    @base = (@base as IValue).ToObject().value;
                }
                var obj = @base as Object;
                var success = obj.InternalSet(referencedName, W, GetThisValue());
                if (success.IsAbrupt()) return success;
                if (success.Other == false && strict)
                    return Completion.ThrowTypeError($"PutValue for {referencedName} failed");
                return Completion.NormalCompletion();
            }
            if (!(@base is EnvironmentRecord envRec))
                throw new InvalidOperationException("ReferenceValue.PutValue: baseValue is not a recognized IReferenceable");
            return envRec.SetMutableBinding(referencedName, W, strict);
        }

        public virtual IValue GetThisValue()
        {
            if (!IsPropertyReference())
                throw new InvalidOperationException("ReferenceValue.GetThisValue: only allowed on property references");
            return baseValue as IValue;
        }

        public Completion InitializeReferencedBinding(IValue W)
        {
            if (IsUnresolvableReference())
                throw new InvalidOperationException("ReferenceValue.InitializeReferencedBinding: strict mode operation on unresolvable reference");
            if (!(baseValue is EnvironmentRecord envRec))
                throw new InvalidOperationException("ReferenceValue.InitializeReferencedBinding: must be an environment record");
            return envRec.InitializeBinding(referencedName, W);
        }
    }

    public class SuperReferenceValue : ReferenceValue
    {
        public readonly IValue thisValue;

        public SuperReferenceValue(IReferenceable baseValue, string referencedName, bool strict, IValue thisValue) : base(baseValue, referencedName, strict)
        {
            this.thisValue = thisValue;
        }

        public override IValue GetThisValue()
        {
            return thisValue;
        }
    }
}
