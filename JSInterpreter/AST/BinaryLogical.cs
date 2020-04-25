using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface ILogicalAndExpression : ILogicalOrExpression
    {
    }

    public class LogicalAndExpression : ILogicalAndExpression
    {
        public readonly IBitwiseOrExpression bitwiseOrExpression;
        public readonly ILogicalAndExpression logicalAndExpression;

        public LogicalAndExpression(ILogicalAndExpression logicalAndExpression, IBitwiseOrExpression bitwiseOrExpression)
        {
            this.logicalAndExpression = logicalAndExpression;
            this.bitwiseOrExpression = bitwiseOrExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var left = logicalAndExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value;
            if (!leftValue.ToBoolean().boolean)
                return Completion.NormalCompletion(leftValue);
            return bitwiseOrExpression.Evaluate(interpreter).GetValue();
        }
    }

    public interface ILogicalOrExpression : IConditionalExpression
    {
    }

    public class LogicalOrExpression : ILogicalOrExpression
    {
        public readonly ILogicalAndExpression logicalAndExpression;
        public readonly ILogicalOrExpression logicalOrExpression;

        public LogicalOrExpression(ILogicalOrExpression logicalOrExpression, ILogicalAndExpression logicalAndExpression)
        {
            this.logicalOrExpression = logicalOrExpression;
            this.logicalAndExpression = logicalAndExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var left = logicalOrExpression.Evaluate(interpreter).GetValue();
            if (left.IsAbrupt()) return left;
            var leftValue = left.value;
            if (leftValue.ToBoolean().boolean)
                return Completion.NormalCompletion(leftValue);
            return logicalAndExpression.Evaluate(interpreter).GetValue();
        }
    }
}
