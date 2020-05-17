using JSInterpreter.AST;

namespace JSInterpreter.Parser
{
    public class MemberExpressionTail
    {
        public AbstractExpression? Expression { get; private set; }
        public string? IdentifierName { get; private set; }
        public MemberExpressionTail? Tail { get; private set; }

        public MemberExpressionTail(AbstractExpression expression, MemberExpressionTail? tail)
        {
            Expression = expression;
            Tail = tail;
        }

        public MemberExpressionTail(string identifierName, MemberExpressionTail? tail)
        {
            IdentifierName = identifierName;
            Tail = tail;
        }
    }
}
