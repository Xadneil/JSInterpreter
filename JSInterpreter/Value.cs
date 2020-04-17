using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    interface IValue : IReferenceable
    {
        Completion ToObject();
        NumberValue ToNumber();
        public BooleanValue ToBoolean()
        {
            return (this switch
            {
                UndefinedValue _ => false,
                NullValue _ => false,
                BooleanValue b => b.boolean,
                NumberValue n => n.number != 0 && n.number != double.NaN,
                StringValue s => s.@string.Length > 0,
                Object _ => true,
                _ => throw new InvalidOperationException("IValue.ToBoolean: unknown conversion")
            }) ? BooleanValue.True : BooleanValue.False;
        }

        enum PrimitiveHint
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
            return Completion.ThrowTypeError();
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
                UndefinedValue _ => Completion.ThrowTypeError(),
                NullValue _ => Completion.ThrowTypeError(),
                _ => Completion.NormalCompletion(this)
            };
        }
    }

    class NumberValue : IValue
    {
        public readonly double number;

        public NumberValue(double number)
        {
            this.number = number;
        }

        public bool IsPrimitive()
        {
            return true;
        }

        public override string ToString()
        {
            return number.ToString();
        }

        public NumberValue ToNumber()
        {
            return this;
        }

        public Completion ToObject()
        {
            return Completion.NormalCompletion(new NumberObject(this));
        }
    }

    class BooleanValue : IValue
    {
        public readonly bool boolean;
        public static BooleanValue True = new BooleanValue(true);
        public static BooleanValue False = new BooleanValue(false);

        private readonly Object wrapperObject;

        private BooleanValue(bool boolean)
        {
            this.boolean = boolean;
            wrapperObject = new Object();
            //TODO: create boolean wrapper object
        }

        public bool IsPrimitive()
        {
            return true;
        }

        public Completion ToObject()
        {
            throw new NotImplementedException("BooleanValue.ToObject");
            //return wrapperObject;
        }

        public override string ToString()
        {
            return boolean.ToString();
        }

        public NumberValue ToNumber()
        {
            return new NumberValue(boolean ? 1 : 0);
        }
    }

    class StringValue : IValue
    {
        public readonly string @string;

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

        public override string ToString()
        {
            return @string;
        }

        public NumberValue ToNumber()
        {
            return new NumberValue(double.Parse(@string));
        }
    }

    class NullValue : IValue
    {
        public readonly static NullValue Instance = new NullValue();
        private NullValue() { }

        public bool IsPrimitive()
        {
            return false;
        }

        public Completion ToObject()
        {
            return Completion.ThrowTypeError();
        }

        public override string ToString()
        {
            return "null";
        }

        public NumberValue ToNumber()
        {
            return new NumberValue(0);
        }
    }

    class UndefinedValue : IValue
    {
        public readonly static UndefinedValue Instance = new UndefinedValue();
        private UndefinedValue() { }

        public bool IsPrimitive()
        {
            return false;
        }

        public Completion ToObject()
        {
            return Completion.ThrowTypeError();
        }

        public override string ToString()
        {
            return "undefined";
        }

        public NumberValue ToNumber()
        {
            return new NumberValue(double.NaN);
        }
    }

    interface IReferenceable
    {
        bool IsPrimitive();
    }

    class ReferenceValue : IValue
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

        public Completion ToObject()
        {
            throw new NotImplementedException();
        }

        public NumberValue ToNumber()
        {
            throw new NotImplementedException();
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
                    return Completion.ThrowReferenceError();
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
                    return Completion.ThrowTypeError();
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

    class SuperReferenceValue : ReferenceValue
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
