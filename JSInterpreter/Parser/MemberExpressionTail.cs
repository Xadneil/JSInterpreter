using JSInterpreter.AST;

namespace JSInterpreter.Parser
{
    public class MemberExpressionTail
    {
        public readonly IExpression Expression;
        public readonly string IdentifierName;
        public readonly MemberExpressionTail Tail;

        public MemberExpressionTail(IExpression expression, MemberExpressionTail tail)
        {
            Expression = expression;
            Tail = tail;
        }

        public MemberExpressionTail(string identifierName, MemberExpressionTail tail)
        {
            IdentifierName = identifierName;
            Tail = tail;
        }
    }
}
