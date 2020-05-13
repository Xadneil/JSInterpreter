using JSInterpreter.AST;

namespace JSInterpreter.Parser
{
    public class CallExpressionTail
    {
        public Arguments? Arguments { get; private set; }
        public IExpression? Expression { get; private set; }
        public string? IdentifierName { get; private set; }
        public CallExpressionTail? Tail { get; private set; }

        public CallExpressionTail(Arguments arguments, CallExpressionTail? tail)
        {
            Arguments = arguments;
            Tail = tail;
        }

        public CallExpressionTail(IExpression expression, CallExpressionTail? tail)
        {
            Expression = expression;
            Tail = tail;
        }

        public CallExpressionTail(string identifierName, CallExpressionTail? tail)
        {
            IdentifierName = identifierName;
            Tail = tail;
        }
    }
}
