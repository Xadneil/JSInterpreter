using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public class Identifier
    {
        public readonly string name;

        public Identifier(string name)
        {
            this.name = name;
        }

        public IReadOnlyList<string> BoundNames()
        {
            return new[] { name };
        }

        public Completion BindingInitialization(IValue value, LexicalEnvironment environment)
        {
            if (environment != null)
            {
                environment.EnvironmentRecord.InitializeBinding(name, value);
                return Completion.NormalCompletion(UndefinedValue.Instance);
            }
            var lhsComp = Interpreter.Instance().ResolveBinding(name);
            if (lhsComp.IsAbrupt()) return lhsComp;
            var lhs = lhsComp.value as ReferenceValue;
            return lhs!.PutValue(value);
        }
    }
    public sealed class IdentifierReference : AbstractPrimaryExpression, IPropertyDefinition
    {
        public readonly Identifier identifier;

        public IdentifierReference(Identifier identifier, bool isStrictMode) : base(isStrictMode)
        {
            this.identifier = identifier;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return interpreter.ResolveBinding(identifier.name);
        }

        public Completion PropertyDefinitionEvaluation(Object @object, bool enumerable)
        {
            var valueComp = Evaluate(Interpreter.Instance()).GetValue();
            if (valueComp.IsAbrupt()) return valueComp;
            var value = valueComp.value!;
            if (Utils.CreateDataPropertyOrThrow(@object, identifier.name, value).IsAbrupt())
                throw new InvalidOperationException("Spec ! 12.2.6.8 IdentifierReference, step 6");
            return Completion.NormalCompletion();
        }
    }
}
