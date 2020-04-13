using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    interface IExponentiationExpression : IMultiplicativeExpression
    {
    }

    class ExponentiationExpression : IExponentiationExpression
    {
        public readonly IUpdateExpression updateExpression;
        public readonly IExponentiationExpression exponentiationExpression;

        public ExponentiationExpression(IUpdateExpression updateExpression, IExponentiationExpression exponentiationExpression)
        {
            this.updateExpression = updateExpression;
            this.exponentiationExpression = exponentiationExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var left = updateExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value;

            var right = exponentiationExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value;

            return Completion.NormalCompletion(Calculate(leftValue, rightValue));
        }

        public static IValue Calculate(IValue leftValue, IValue rightValue)
        {
            var @base = leftValue.ToNumber();
            var exponent = rightValue.ToNumber();
            return new NumberValue(Math.Pow(@base.number, exponent.number));
        }
    }
}
