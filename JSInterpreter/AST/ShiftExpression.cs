using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public enum ShiftOperator
    {
        ShiftLeft,
        ShiftRight,
        ShiftRightUnsigned
    }

    public abstract class AbstractShiftExpression : AbstractRelationalExpression
    {
        protected AbstractShiftExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class ShiftExpression : AbstractShiftExpression
    {
        public readonly ShiftOperator shiftOperator;
        public readonly AbstractAdditiveExpression additiveExpression;
        public readonly AbstractShiftExpression shiftExpression;

        public ShiftExpression(AbstractShiftExpression shiftExpression, ShiftOperator shiftOperator, AbstractAdditiveExpression additiveExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.shiftExpression = shiftExpression;
            this.shiftOperator = shiftOperator;
            this.additiveExpression = additiveExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var left = shiftExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value!;

            var right = additiveExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value!;

            return Calculate(leftValue, shiftOperator, rightValue);
        }

        public static Completion Calculate(IValue leftValue, ShiftOperator shiftOperator, IValue rightValue)
        {
            var lnumComp = leftValue.ToNumber();
            if (lnumComp.IsAbrupt()) return lnumComp;
            var rnumComp = rightValue.ToNumber();
            if (rnumComp.IsAbrupt()) return rnumComp;

            int lnum = (int)(lnumComp.value as NumberValue)!.number;
            uint rnum = (uint)(rnumComp.value as NumberValue)!.number;

            var shiftCount = (int)(rnum & 0x1F);
            return Completion.NormalCompletion(new NumberValue(shiftOperator switch
            {
                ShiftOperator.ShiftRight => lnum >> shiftCount,
                ShiftOperator.ShiftRightUnsigned => ((uint)lnum >> shiftCount) & (0b1 << shiftCount),
                ShiftOperator.ShiftLeft => lnum << shiftCount,
                _ => throw new InvalidOperationException($"ShiftExpression.Evaluate: unknown ShiftOperator enum value {(int)shiftOperator}")
            }));
        }
    }
}
