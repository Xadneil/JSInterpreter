using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum AdditiveOperator
    {
        Add,
        Subtract
    }

    interface IAdditiveExpression : IShiftExpression
    {
    }

    class AdditiveExpression : IAdditiveExpression
    {
        public readonly AdditiveOperator additiveOperator;
        public readonly IMultiplicativeExpression multiplicativeExpression;
        public readonly IAdditiveExpression additiveExpression;

        public AdditiveExpression(IAdditiveExpression additiveExpression, AdditiveOperator additiveOperator, IMultiplicativeExpression multiplicativeExpression)
        {
            this.additiveExpression = additiveExpression;
            this.additiveOperator = additiveOperator;
            this.multiplicativeExpression = multiplicativeExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var left = additiveExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value;

            var right = multiplicativeExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value;
            //TODO handle dates

            return Completion.NormalCompletion(Calculate(leftValue, additiveOperator, rightValue));
        }

        public static IValue Calculate(IValue leftValue, AdditiveOperator additiveOperator, IValue rightValue)
        {
            bool leftIsNumber, rightIsNumber;
            NumberValue lnum, rnum;
            try { lnum = leftValue.ToNumber(); leftIsNumber = true; } catch { leftIsNumber = false; lnum = null; }
            try { rnum = rightValue.ToNumber(); rightIsNumber = true; } catch { rightIsNumber = false; rnum = null; }

            if (!leftIsNumber || !rightIsNumber)
            {
                if (additiveOperator == AdditiveOperator.Subtract)
                    throw new InvalidOperationException("AdditiveExpression.Evaluate: cannot subtract values that cannot be converted to numbers");
                return new StringValue(leftValue.ToString() + rightValue.ToString());
            }

            return new NumberValue(additiveOperator switch
            {
                AdditiveOperator.Add => lnum.number + rnum.number,
                AdditiveOperator.Subtract => lnum.number - rnum.number,
                _ => throw new InvalidOperationException($"AdditiveExpression.Evaluate: unknown AdditiveOperator enum value {(int)additiveOperator}")
            });
        }
    }
}
