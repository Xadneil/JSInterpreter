namespace JSInterpreter.AST
{
    public class SpreadElement : IArrayLiteralItem, IPropertyDefinition, IArgumentItem
    {
        public readonly IAssignmentExpression assignmentExpression;

        public SpreadElement(IAssignmentExpression assignmentExpression)
        {
            this.assignmentExpression = assignmentExpression;
        }
    }

}
