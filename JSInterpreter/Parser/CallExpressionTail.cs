using JSInterpreter.AST;

namespace JSInterpreter.Parser
{
    public class CallExpressionTail
    {
        public readonly Arguments Arguments;
        public readonly IExpression Expression;
        public readonly string IdentifierName;
        public readonly CallExpressionTail Tail;

        public CallExpressionTail(Arguments arguments, CallExpressionTail tail)
        {
            Arguments = arguments;
            Tail = tail;
        }

        public CallExpressionTail(IExpression expression, CallExpressionTail tail)
        {
            Expression = expression;
            Tail = tail;
        }

        public CallExpressionTail(string identifierName, CallExpressionTail tail)
        {
            IdentifierName = identifierName;
            Tail = tail;
        }
    }
}
