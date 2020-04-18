using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum EqualityOperator
    {
        Equals,
        NotEquals,
        StrictEquals,
        StrictNotEquals
    }

    interface IEqualityExpression : IBitwiseAndExpression
    {
    }

    class EqualityExpression : IEqualityExpression
    {
        public readonly EqualityOperator equalityOperator;
        public readonly IRelationalExpression relationalExpression;
        public readonly IEqualityExpression equalityExpression;

        public EqualityExpression(IEqualityExpression equalityExpression, EqualityOperator equalityOperator, IRelationalExpression relationalExpression)
        {
            this.equalityExpression = equalityExpression;
            this.equalityOperator = equalityOperator;
            this.relationalExpression = relationalExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var leftComp = equalityExpression.Evaluate(interpreter).GetValue();
            if (leftComp.IsAbrupt()) return leftComp;
            var left = leftComp.value;

            var rightComp = relationalExpression.Evaluate(interpreter).GetValue();
            if (rightComp.IsAbrupt()) return rightComp;
            var right = rightComp.value;

            return Completion.NormalCompletion(equalityOperator switch
            {
                EqualityOperator.Equals => AbstractEquality(left, right),
                EqualityOperator.NotEquals => !AbstractEquality(left, right),
                EqualityOperator.StrictEquals => StrictAbstractEquality(left, right),
                EqualityOperator.StrictNotEquals => !StrictAbstractEquality(left, right),
                _ => throw new InvalidOperationException($"EqualityExpression.Evaluate: invalid EqualityOperator enum value {(int)equalityOperator}")
            } ? BooleanValue.True : BooleanValue.False);
        }

        private bool AbstractEquality(IValue x, IValue y)
        {
            if (x.GetType() == y.GetType())
                return StrictAbstractEquality(x, y);
            if ((x == NullValue.Instance && y == UndefinedValue.Instance) || (x == UndefinedValue.Instance && y == NullValue.Instance))
                return true;
            if (x is NumberValue && y is StringValue) return AbstractEquality(x, y.ToNumber().value);
            if (x is StringValue && y is NumberValue) return AbstractEquality(x.ToNumber().value, y);
            if (x is BooleanValue) return AbstractEquality(x.ToNumber().value, y);
            if (y is BooleanValue) return AbstractEquality(x, y.ToNumber().value);
            if ((x is StringValue || x is NumberValue) && y is Object) return AbstractEquality(x, y.ToPrimitive().value);
            if (x is Object && (y is StringValue || y is NumberValue)) return AbstractEquality(x.ToPrimitive().value, y);
            return false;
        }

        public static bool StrictAbstractEquality(IValue x, IValue y)
        {
            if (x.GetType() != y.GetType()) return false;
            if (x is NumberValue nX)
            {
                var nY = y as NumberValue;
                if (double.IsNaN(nX.number) || double.IsNaN(nY.number))
                    return false;
                return nX.number == nY.number;
            }
            return SameValueNonNumber(x, y);
        }

        private static bool SameValueNonNumber(IValue x, IValue y)
        {
            if (x is UndefinedValue) return true;
            if (x is NullValue) return true;
            if (x is StringValue xs) return xs.@string == (y as StringValue).@string;
            if (x is BooleanValue) return x == y; // BooleanValues are two singletons, reference equality should work
            return x == y;
        }
    }
}
