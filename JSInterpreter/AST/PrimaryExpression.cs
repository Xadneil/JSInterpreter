using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    interface IPrimaryExpression : IMemberExpression
    {
    }

    class ThisExpression : IPrimaryExpression
    {
        public Completion Evaluate(Interpreter interpreter)
        {
            return interpreter.ResolveThisBinding();
        }
    }
}
