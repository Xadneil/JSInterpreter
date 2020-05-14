using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface IExponentiationExpression : IMultiplicativeExpression
    {
    }

    public class ExponentiationExpression : IExponentiationExpression
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
            var leftValue = left.value!;

            var right = exponentiationExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value!;

            return Calculate(leftValue, rightValue);
        }

        public static Completion Calculate(IValue leftValue, IValue rightValue)
        {
            var baseComp = leftValue.ToNumber();
            if (baseComp.IsAbrupt()) return baseComp;
            var exponentComp = rightValue.ToNumber();
            if (exponentComp.IsAbrupt()) return exponentComp;


            double @base = (baseComp.value as NumberValue)!.number;
            double exponent = (exponentComp.value as NumberValue)!.number;

            return Completion.NormalCompletion(new NumberValue(Math.Pow(@base, exponent)));
        }
    }
}
