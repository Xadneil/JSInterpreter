using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface IPrimaryExpression : IMemberExpression
    {
    }

    public class ThisExpression : IPrimaryExpression
    {
        public static readonly ThisExpression Instance = new ThisExpression();

        private ThisExpression() { }

        public Completion Evaluate(Interpreter interpreter)
        {
            return interpreter.ResolveThisBinding();
        }
    }

    public class ParenthesizedExpression : IPrimaryExpression
    {
        public readonly IExpression expression;

        public ParenthesizedExpression(IExpression expression)
        {
            this.expression = expression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            return expression.Evaluate(interpreter);
        }
    }
}
