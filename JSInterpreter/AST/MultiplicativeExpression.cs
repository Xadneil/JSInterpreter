using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum MultiplicativeOperator
    {
        Multiply,
        Divide,
        Modulus
    }

    interface IMultiplicativeExpression : IAdditiveExpression
    {
    }

    class MultiplicativeExpression : IMultiplicativeExpression
    {
        public readonly MultiplicativeOperator multiplicativeOperator;
        public readonly IExponentiationExpression exponentiationExpression;
        public readonly IMultiplicativeExpression multiplicativeExpression;

        public MultiplicativeExpression(IMultiplicativeExpression multiplicativeExpression, MultiplicativeOperator multiplicativeOperator, IExponentiationExpression exponentiationExpression)
        {
            this.multiplicativeExpression = multiplicativeExpression;
            this.multiplicativeOperator = multiplicativeOperator;
            this.exponentiationExpression = exponentiationExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var left = multiplicativeExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value;

            var right = exponentiationExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value;

            return Completion.NormalCompletion(Calculate(leftValue, multiplicativeOperator, rightValue));
        }

        public static IValue Calculate(IValue leftValue, MultiplicativeOperator multiplicativeOperator, IValue rightValue)
        {
            var lnum = leftValue.ToNumber().number;
            var rnum = rightValue.ToNumber().number;
            return new NumberValue(multiplicativeOperator switch
            {
                MultiplicativeOperator.Multiply => lnum * rnum,
                MultiplicativeOperator.Divide => lnum / rnum,
                MultiplicativeOperator.Modulus => lnum % rnum,
                _ => throw new InvalidOperationException($"MultiplicativeExpression.Evaluate: unknown MultiplicativeOperator enum value {(int)multiplicativeOperator}")
            });
        }
    }
}
