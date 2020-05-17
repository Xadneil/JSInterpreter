using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractBitwiseAndExpression : AbstractBitwiseXorExpression
    {
        protected AbstractBitwiseAndExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public enum BitwiseOperator
    {
        And, Xor, Or
    }

    public interface IBitwiseExpression
    {
        public Completion EvaluateBitwise(Interpreter interpreter)
        {
            var left = Left.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value!;

            var right = Right.Evaluate(interpreter).GetValue();
            if (right.IsAbrupt()) return right;
            var rightValue = right.value!;

            return Calculate(leftValue, BitwiseOperator, rightValue);
        }

        public static Completion Calculate(IValue leftValue, BitwiseOperator bitwiseOperator, IValue rightValue)
        {
            var leftComp = leftValue.ToNumber();
            if (leftComp.IsAbrupt()) return leftComp;
            var rightComp = rightValue.ToNumber();
            if (rightComp.IsAbrupt()) return rightComp;

            int left = (int)(leftComp.value as NumberValue)!.number;
            int right = (int)(rightComp.value as NumberValue)!.number;

            return Completion.NormalCompletion(new NumberValue(bitwiseOperator switch
            {
                BitwiseOperator.And => left & right,
                BitwiseOperator.Xor => left ^ right,
                BitwiseOperator.Or => left | right,
                _ => throw new InvalidOperationException($"BitwiseExpression.Evaluate: invalid BitwiseOperator enum value {(int)bitwiseOperator}")
            }));
        }

        protected BitwiseOperator BitwiseOperator { get; }
        protected AbstractExpression Left { get; }
        protected AbstractExpression Right { get; }
    }

    public sealed class BitwiseAndExpression : AbstractBitwiseAndExpression, IBitwiseExpression
    {
        public readonly AbstractEqualityExpression equalityExpression;
        public readonly AbstractBitwiseAndExpression bitwiseAndExpression;

        public BitwiseAndExpression(AbstractBitwiseAndExpression bitwiseAndExpression, AbstractEqualityExpression equalityExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.bitwiseAndExpression = bitwiseAndExpression;
            this.equalityExpression = equalityExpression;
        }

        BitwiseOperator IBitwiseExpression.BitwiseOperator => BitwiseOperator.And;
        AbstractExpression IBitwiseExpression.Left => bitwiseAndExpression;
        AbstractExpression IBitwiseExpression.Right => equalityExpression;

        public override Completion Evaluate(Interpreter interpreter)
        {
            return ((IBitwiseExpression)this).EvaluateBitwise(interpreter);
        }
    }

    public abstract class AbstractBitwiseXorExpression : AbstractBitwiseOrExpression
    {
        protected AbstractBitwiseXorExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class BitwiseXorExpression : AbstractBitwiseXorExpression, IBitwiseExpression
    {
        public readonly AbstractBitwiseAndExpression bitwiseAndExpression;
        public readonly AbstractBitwiseXorExpression bitwiseXorExpression;

        public BitwiseXorExpression(AbstractBitwiseXorExpression bitwiseXorExpression, AbstractBitwiseAndExpression bitwiseAndExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.bitwiseXorExpression = bitwiseXorExpression;
            this.bitwiseAndExpression = bitwiseAndExpression;
        }

        BitwiseOperator IBitwiseExpression.BitwiseOperator => BitwiseOperator.Xor;
        AbstractExpression IBitwiseExpression.Left => bitwiseXorExpression;
        AbstractExpression IBitwiseExpression.Right => bitwiseAndExpression;

        public override Completion Evaluate(Interpreter interpreter)
        {
            return ((IBitwiseExpression)this).EvaluateBitwise(interpreter);
        }
    }

    public abstract class AbstractBitwiseOrExpression : AbstractLogicalAndExpression
    {
        protected AbstractBitwiseOrExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class BitwiseOrExpression : AbstractBitwiseOrExpression, IBitwiseExpression
    {
        public readonly AbstractBitwiseXorExpression bitwiseXorExpression;
        public readonly AbstractBitwiseOrExpression bitwiseOrExpression;

        public BitwiseOrExpression(AbstractBitwiseOrExpression bitwiseOrExpression, AbstractBitwiseXorExpression bitwiseXorExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.bitwiseOrExpression = bitwiseOrExpression;
            this.bitwiseXorExpression = bitwiseXorExpression;
        }

        BitwiseOperator IBitwiseExpression.BitwiseOperator => BitwiseOperator.Or;
        AbstractExpression IBitwiseExpression.Left => bitwiseOrExpression;
        AbstractExpression IBitwiseExpression.Right => bitwiseXorExpression;

        public override Completion Evaluate(Interpreter interpreter)
        {
            return ((IBitwiseExpression)this).EvaluateBitwise(interpreter);
        }
    }
}
