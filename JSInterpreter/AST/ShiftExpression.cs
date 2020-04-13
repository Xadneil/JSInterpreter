using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum ShiftOperator
    {
        ShiftLeft,
        ShiftRight,
        ShiftRightUnsigned
    }

    interface IShiftExpression : IRelationalExpression
    {
    }

    class ShiftExpression : IShiftExpression
    {
        public readonly ShiftOperator shiftOperator;
        public readonly IAdditiveExpression additiveExpression;
        public readonly IShiftExpression shiftExpression;

        public ShiftExpression(IShiftExpression shiftExpression, ShiftOperator shiftOperator, IAdditiveExpression additiveExpression)
        {
            this.shiftExpression = shiftExpression;
            this.shiftOperator = shiftOperator;
            this.additiveExpression = additiveExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var left = shiftExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value;

            var right = additiveExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value;

            return Completion.NormalCompletion(Calculate(leftValue, shiftOperator, rightValue));
        }

        public static IValue Calculate(IValue leftValue, ShiftOperator shiftOperator, IValue rightValue)
        {
            var lnum = (int)leftValue.ToNumber().number;
            var rnum = (uint)rightValue.ToNumber().number;
            var shiftCount = (int)(rnum & 0x1F);
            return new NumberValue(shiftOperator switch
            {
                ShiftOperator.ShiftRight => lnum >> shiftCount,
                ShiftOperator.ShiftRightUnsigned => ((uint)lnum >> shiftCount) & (0b1 << shiftCount),
                ShiftOperator.ShiftLeft => lnum << shiftCount,
                _ => throw new InvalidOperationException($"ShiftExpression.Evaluate: unknown ShiftOperator enum value {(int)shiftOperator}")
            });
        }
    }
}
