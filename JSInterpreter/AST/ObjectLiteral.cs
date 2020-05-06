using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public class ObjectLiteral : IPrimaryExpression
    {
        public readonly IReadOnlyList<IPropertyDefinition> propertyDefinitions;

        public ObjectLiteral(IReadOnlyList<IPropertyDefinition> propertyDefinitions)
        {
            this.propertyDefinitions = propertyDefinitions;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var obj = Utils.ObjectCreate(interpreter.CurrentRealm().Intrinsics.ObjectPrototype);
            foreach (var property in propertyDefinitions)
            {
                var comp = property.PropertyDefinitionEvaluation(obj, true);
                if (comp.IsAbrupt()) return comp;
            }

            return Completion.NormalCompletion(obj);
        }
    }

    public interface IPropertyDefinition
    {
        Completion PropertyDefinitionEvaluation(Object @object, bool enumerable);
    }

    public class PropertyDefinition : IPropertyDefinition
    {
        public readonly string propertyName;
        public readonly IAssignmentExpression assignmentExpression;

        public PropertyDefinition(string propertyName, IAssignmentExpression assignmentExpression)
        {
            this.propertyName = propertyName;
            this.assignmentExpression = assignmentExpression;
        }

        public Completion PropertyDefinitionEvaluation(Object @object, bool enumerable)
        {
            IValue value;
            Completion valueComp;
            if (assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous)
            {
                value = functionExpression.NamedEvaluate(Interpreter.Instance(), propertyName).value;
            }
            else
            {
                valueComp = assignmentExpression.Evaluate(Interpreter.Instance()).GetValue();
                if (valueComp.IsAbrupt()) return valueComp;
                value = valueComp.value;
            }
            if (Utils.CreateDataPropertyOrThrow(@object, propertyName, value).IsAbrupt())
                throw new InvalidOperationException("Spec ! 12.2.6.8 Assignment, step 7");
            return Completion.NormalCompletion();
        }
    }
}
