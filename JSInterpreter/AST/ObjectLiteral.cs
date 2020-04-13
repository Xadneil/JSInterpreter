using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    class ObjectLiteral : IPrimaryExpression
    {
        public readonly IReadOnlyList<IPropertyDefinition> propertyDefinitions;

        public ObjectLiteral(IReadOnlyList<IPropertyDefinition> propertyDefinitions)
        {
            this.propertyDefinitions = propertyDefinitions;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var obj = Utils.ObjectCreate(ObjectPrototype.Instance);
            foreach (var property in propertyDefinitions)
            {
                Completion valueComp;
                IValue value;
                switch (property)
                {
                    case SpreadElement spreadElement:
                        valueComp = spreadElement.assignmentExpression.Evaluate(interpreter);
                        if (valueComp.IsAbrupt()) return valueComp;
                        value = valueComp.value;
                        if (!(value is Object @object))
                            throw new InvalidOperationException($"ObjectLiteral: tried to initialize an object using a spread on a non-object");
                        valueComp = Utils.CopyDataProperties(obj, @object, excludedItems: Utils.EmptyList<string>());
                        if (valueComp.IsAbrupt()) return valueComp;
                        break;
                    case IdentifierReference identifierReference:
                        valueComp = identifierReference.Evaluate(interpreter);
                        if (valueComp.IsAbrupt()) return valueComp;
                        value = valueComp.value;
                        if (Utils.CreateDataPropertyOrThrow(obj, identifierReference.identifier.name, value).IsAbrupt())
                            throw new InvalidOperationException("Spec ! 12.2.6.8 IdentifierReference, step 6");
                        break;
                    case PropertyDefinition propertyDefinition:
                        if (propertyDefinition.assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous)
                        {
                            value = functionExpression.NamedEvaluate(interpreter, propertyDefinition.propertyName).value;
                        }
                        else
                        {
                            valueComp = propertyDefinition.assignmentExpression.Evaluate(interpreter);
                            if (valueComp.IsAbrupt()) return valueComp;
                            value = valueComp.value;
                        }
                        if (Utils.CreateDataPropertyOrThrow(obj, propertyDefinition.propertyName, value).IsAbrupt())
                            throw new InvalidOperationException("Spec ! 12.2.6.8 Assignment, step 7");
                        break;
                }
            }

            return Completion.NormalCompletion(obj);
        }
    }

    interface IPropertyDefinition
    {
    }

    class PropertyDefinition : IPropertyDefinition
    {
        public readonly string propertyName;
        public readonly IAssignmentExpression assignmentExpression;

        public PropertyDefinition(string propertyName, IAssignmentExpression assignmentExpression)
        {
            this.propertyName = propertyName;
            this.assignmentExpression = assignmentExpression;
        }

    }
}
