using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public sealed class RegularExpressionLiteral : AbstractPrimaryExpression
    {
        public readonly string body, flags;

        public RegularExpressionLiteral(string body, string flags, bool isStrictMode) : base(isStrictMode)
        {
            this.body = body;
            this.flags = flags;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            throw new NotImplementedException("regular expressions");
        }
    }
}
