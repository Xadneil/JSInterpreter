﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public enum MultiplicativeOperator
    {
        Multiply,
        Divide,
        Modulus
    }

    public abstract class AbstractMultiplicativeExpression : AbstractAdditiveExpression
    {
        protected AbstractMultiplicativeExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class MultiplicativeExpression : AbstractMultiplicativeExpression
    {
        public readonly MultiplicativeOperator multiplicativeOperator;
        public readonly AbstractExponentiationExpression exponentiationExpression;
        public readonly AbstractMultiplicativeExpression multiplicativeExpression;

        public MultiplicativeExpression(AbstractMultiplicativeExpression multiplicativeExpression, MultiplicativeOperator multiplicativeOperator, AbstractExponentiationExpression exponentiationExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.multiplicativeExpression = multiplicativeExpression;
            this.multiplicativeOperator = multiplicativeOperator;
            this.exponentiationExpression = exponentiationExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var left = multiplicativeExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value!;

            var right = exponentiationExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value!;

            return Calculate(leftValue, multiplicativeOperator, rightValue);
        }

        public static Completion Calculate(IValue leftValue, MultiplicativeOperator multiplicativeOperator, IValue rightValue)
        {
            var lnumComp = leftValue.ToNumber();
            if (lnumComp.IsAbrupt()) return lnumComp;
            var rnumComp = rightValue.ToNumber();
            if (rnumComp.IsAbrupt()) return rnumComp;

            double lnum = (lnumComp.value as NumberValue)!.number;
            double rnum = (rnumComp.value as NumberValue)!.number;

            return Completion.NormalCompletion(new NumberValue(multiplicativeOperator switch
            {
                MultiplicativeOperator.Multiply => lnum * rnum,
                MultiplicativeOperator.Divide => lnum / rnum,
                MultiplicativeOperator.Modulus => lnum % rnum,
                _ => throw new InvalidOperationException($"MultiplicativeExpression.Evaluate: unknown MultiplicativeOperator enum value {(int)multiplicativeOperator}")
            }));
        }
    }
}
