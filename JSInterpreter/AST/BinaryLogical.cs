using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractLogicalAndExpression : AbstractLogicalOrExpression
    {
        protected AbstractLogicalAndExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class LogicalAndExpression : AbstractLogicalAndExpression
    {
        public readonly AbstractBitwiseOrExpression bitwiseOrExpression;
        public readonly AbstractLogicalAndExpression logicalAndExpression;

        public LogicalAndExpression(AbstractLogicalAndExpression logicalAndExpression, AbstractBitwiseOrExpression bitwiseOrExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.logicalAndExpression = logicalAndExpression;
            this.bitwiseOrExpression = bitwiseOrExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var left = logicalAndExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value!;
            if (!leftValue.ToBoolean().boolean)
                return Completion.NormalCompletion(leftValue);
            return bitwiseOrExpression.Evaluate(interpreter).GetValue();
        }
    }

    public abstract class AbstractLogicalOrExpression : AbstractConditionalExpression
    {
        protected AbstractLogicalOrExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class LogicalOrExpression : AbstractLogicalOrExpression
    {
        public readonly AbstractLogicalAndExpression logicalAndExpression;
        public readonly AbstractLogicalOrExpression logicalOrExpression;

        public LogicalOrExpression(AbstractLogicalOrExpression logicalOrExpression, AbstractLogicalAndExpression logicalAndExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.logicalOrExpression = logicalOrExpression;
            this.logicalAndExpression = logicalAndExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var left = logicalOrExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value!;
            if (leftValue.ToBoolean().boolean)
                return Completion.NormalCompletion(leftValue);
            return logicalAndExpression.Evaluate(interpreter).GetValue();
        }
    }
}
