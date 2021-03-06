﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace JSInterpreter.AST
{
    public sealed class VariableDeclaration : ParseNode, IScopedDeclaration
    {
        public readonly string name;
        public readonly AbstractAssignmentExpression? assignmentExpression;

        public VariableDeclaration(string name, AbstractAssignmentExpression? assignmentExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.name = name;
            this.assignmentExpression = assignmentExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            if (assignmentExpression == null) return Completion.NormalCompletion();
            var lhsComp = interpreter.ResolveBinding(name, IsStrictMode);
            if (lhsComp.IsAbrupt()) return lhsComp;
            if (!(lhsComp.value is ReferenceValue referenceValue))
                throw new InvalidOperationException("ResolveBinding didn't return a reference");

            Completion value;
            if (assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous)
                value = functionExpression.NamedEvaluate(interpreter, name);
            else
                value = assignmentExpression.Evaluate(interpreter).GetValue();
            if (value.IsAbrupt()) return value;
            return referenceValue.PutValue(value.value!);
        }

        public IReadOnlyList<string> BoundNames()
        {
            return new List<string>(1) { name };
        }
    }

    public class VariableDeclarationList : List<VariableDeclaration>
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
