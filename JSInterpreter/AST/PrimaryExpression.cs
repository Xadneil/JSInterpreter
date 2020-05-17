using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractPrimaryExpression : AbstractMemberExpression
    {
        protected AbstractPrimaryExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class ThisExpression : AbstractPrimaryExpression
    {
        private static readonly ThisExpression strictInstance = new ThisExpression(true);
        private static readonly ThisExpression nonStrictInstance = new ThisExpression(false);
        public static ThisExpression Instance(bool isStrictMode) => isStrictMode ? strictInstance : nonStrictInstance;

        private ThisExpression(bool isStrictMode) : base(isStrictMode) { }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return interpreter.ResolveThisBinding();
        }
    }

    public sealed class ParenthesizedExpression : AbstractPrimaryExpression
    {
        public readonly AbstractExpression expression;

        public ParenthesizedExpression(AbstractExpression expression, bool isStrictMode) : base(isStrictMode)
        {
            this.expression = expression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return expression.Evaluate(interpreter);
        }
    }
}
