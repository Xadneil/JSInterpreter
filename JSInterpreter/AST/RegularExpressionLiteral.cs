using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    class RegularExpressionLiteral : IPrimaryExpression
    {
        public readonly string body, flags;

        public RegularExpressionLiteral(string body, string flags)
        {
            this.body = body;
            this.flags = flags;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            throw new NotImplementedException("regular expressions");
        }
    }
}
