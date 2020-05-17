using System;

namespace JSInterpreter.AST
{
    public class SpreadElement : IArrayLiteralItem, IPropertyDefinition, IArgumentItem
    {
        public readonly AbstractAssignmentExpression assignmentExpression;

        public SpreadElement(AbstractAssignmentExpression assignmentExpression)
        {
            this.assignmentExpression = assignmentExpression;
        }

        public Completion PropertyDefinitionEvaluation(Object @object, bool enumerable)
        {
            var valueComp = assignmentExpression.Evaluate(Interpreter.Instance());
            if (valueComp.IsAbrupt()) return valueComp;
            var value = valueComp.value;
            if (!(value is Object source))
                throw new InvalidOperationException($"ObjectLiteral: tried to initialize an object using a spread on a non-object");
            valueComp = Utils.CopyDataProperties(@object, source, excludedItems: Utils.EmptyList<string>());
            if (valueComp.IsAbrupt()) return valueComp;
            return Completion.NormalCompletion();
        }
    }

}
