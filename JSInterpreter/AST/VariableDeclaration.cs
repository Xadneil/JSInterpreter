using System;
using System.Collections.Generic;
using System.Linq;

namespace JSInterpreter.AST
{
    class VariableDeclaration
    {
        public readonly string name;
        public readonly IAssignmentExpression assignmentExpression;

        public VariableDeclaration(string name, IAssignmentExpression assignmentExpression)
        {
            this.name = name;
            this.assignmentExpression = assignmentExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            if (assignmentExpression == null) return Completion.NormalCompletion();
            var lhsComp = interpreter.ResolveBinding(name);
            if (lhsComp.IsAbrupt()) return lhsComp;
            if (!(lhsComp.value is ReferenceValue referenceValue))
                throw new InvalidOperationException("ResolveBinding didn't return a reference");

            Completion value;
            if (assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous)
                value = functionExpression.NamedEvaluate(interpreter, name);
            else
                value = assignmentExpression.Evaluate(interpreter).GetValue();
            if (value.IsAbrupt()) return value;
            return referenceValue.PutValue(value.value);
        }
    }

    class VariableDeclarationList : List<VariableDeclaration>
    {
        public IReadOnlyList<string> BoundNames()
        {
            return this.Select(v => v.name).ToList();
        }

        public IReadOnlyList<IScopedDeclaration> VarScopedDeclarations() => this.Cast<IScopedDeclaration>().ToList();

        public Completion Evaluate(Interpreter interpreter)
        {
            foreach (var v in this)
            {
                var result = v.Evaluate(interpreter);
                if (result.IsAbrupt()) return result;
            }
            return Completion.NormalCompletion();
        }
    }
}
