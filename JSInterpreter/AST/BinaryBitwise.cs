using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface IBitwiseAndExpression : IBitwiseXorExpression
    {
    }

    public enum BitwiseOperator
    {
        And, Xor, Or
    }

    public abstract class BitwiseExpression
    {
        public Completion Evaluate(Interpreter interpreter)
        {
            var left = Left.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value;

            var right = Right.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value;

            return Calculate(leftValue, BitwiseOperator, rightValue);
        }

        public static Completion Calculate(IValue leftValue, BitwiseOperator bitwiseOperator, IValue rightValue)
        {
            var leftComp = leftValue.ToNumber();
            if (leftComp.IsAbrupt()) return leftComp;
            var rightComp = rightValue.ToNumber();
            if (rightComp.IsAbrupt()) return rightComp;

            int left = (int)(leftComp.value as NumberValue).number;
            int right = (int)(rightComp.value as NumberValue).number;

            return Completion.NormalCompletion(new NumberValue(bitwiseOperator switch
            {
                BitwiseOperator.And => left & right,
                BitwiseOperator.Xor => left ^ right,
                BitwiseOperator.Or => left | right,
                _ => throw new InvalidOperationException($"BitwiseExpression.Evaluate: invalid BitwiseOperator enum value {(int)bitwiseOperator}")
            }));
        }

        protected abstract BitwiseOperator BitwiseOperator { get; }
        protected abstract IExpression Left { get; }
        protected abstract IExpression Right { get; }
    }

    public class BitwiseAndExpression : BitwiseExpression, IBitwiseAndExpression
    {
        public readonly IEqualityExpression equalityExpression;
        public readonly IBitwiseAndExpression bitwiseAndExpression;

        public BitwiseAndExpression(IBitwiseAndExpression bitwiseAndExpression, IEqualityExpression equalityExpression)
        {
            this.bitwiseAndExpression = bitwiseAndExpression;
            this.equalityExpression = equalityExpression;
        }

        protected override BitwiseOperator BitwiseOperator => BitwiseOperator.And;

        protected override IExpression Left => bitwiseAndExpression;

        protected override IExpression Right => equalityExpression;
    }

    public interface IBitwiseXorExpression : IBitwiseOrExpression
    {
    }

    public class BitwiseXorExpression : BitwiseExpression, IBitwiseXorExpression
    {
        public readonly IBitwiseAndExpression bitwiseAndExpression;
        public readonly IBitwiseXorExpression bitwiseXorExpression;

        public BitwiseXorExpression(IBitwiseXorExpression bitwiseXorExpression, IBitwiseAndExpression bitwiseAndExpression)
        {
            this.bitwiseXorExpression = bitwiseXorExpression;
            this.bitwiseAndExpression = bitwiseAndExpression;
        }

        protected override BitwiseOperator BitwiseOperator => BitwiseOperator.Xor;

        protected override IExpression Left => bitwiseXorExpression;

        protected override IExpression Right => bitwiseAndExpression;
    }

    public interface IBitwiseOrExpression : ILogicalAndExpression
    {
    }

    public class BitwiseOrExpression : BitwiseExpression, IBitwiseOrExpression
    {
        public readonly IBitwiseXorExpression bitwiseXorExpression;
        public readonly IBitwiseOrExpression bitwiseOrExpression;

        public BitwiseOrExpression(IBitwiseOrExpression bitwiseOrExpression, IBitwiseXorExpression bitwiseXorExpression)
        {
            this.bitwiseOrExpression = bitwiseOrExpression;
            this.bitwiseXorExpression = bitwiseXorExpression;
        }

        protected override BitwiseOperator BitwiseOperator => BitwiseOperator.Or;

        protected override IExpression Left => bitwiseOrExpression;

        protected override IExpression Right => bitwiseXorExpression;
    }
}
