using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public enum AdditiveOperator
    {
        Add,
        Subtract
    }

    public interface IAdditiveExpression : IShiftExpression
    {
    }

    public class AdditiveExpression : IAdditiveExpression
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
            var leftValue = left.value!;

            var right = multiplicativeExpression.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value!;

            return Calculate(leftValue, additiveOperator, rightValue);
        }

        public static Completion Calculate(IValue leftValue, AdditiveOperator additiveOperator, IValue rightValue)
        {
            IValue leftForToNumber, rightForToNumber;
            if (additiveOperator == AdditiveOperator.Add)
            {
                var lprim = leftValue.ToPrimitive();
                if (lprim.IsAbrupt()) return lprim;
                var rprim = rightValue.ToPrimitive();
                if (rprim.IsAbrupt()) return rprim;

                if (lprim.value is StringValue || rprim.value is StringValue)
                {
                    var lstr = lprim.value!.ToJsString();
                    if (lstr.IsAbrupt()) return lstr;
                    var rstr = rprim.value!.ToJsString();
                    if (rstr.IsAbrupt()) return rstr;
                    return Completion.NormalCompletion(new StringValue((lstr.value! as StringValue)!.@string + (rstr.value! as StringValue)!.@string));
                }
                leftForToNumber = lprim.value!;
                rightForToNumber = rprim.value!;
            }
            else
            {
                leftForToNumber = leftValue;
                rightForToNumber = rightValue;
            }

            var lnum = leftForToNumber.ToNumber();
            if (lnum.IsAbrupt()) return lnum;
            var rnum = rightForToNumber.ToNumber();
            if (rnum.IsAbrupt()) return rnum;
            if (additiveOperator == AdditiveOperator.Add)
            {
                return Completion.NormalCompletion(new NumberValue((lnum.value! as NumberValue)!.number + (rnum.value! as NumberValue)!.number));
            }
            else
            {
                return Completion.NormalCompletion(new NumberValue((lnum.value! as NumberValue)!.number - (rnum.value! as NumberValue)!.number));
            }
        }
    }
}
