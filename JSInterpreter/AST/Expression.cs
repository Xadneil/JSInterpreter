using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractExpression : ParseNode, IHasEvaluate
    {
        protected AbstractExpression(bool isStrictMode) : base(isStrictMode)
        {
        }

        public abstract Completion Evaluate(Interpreter interpreter);
    }
    public sealed class CommaExpression : AbstractExpression
    {
        public readonly IReadOnlyList<AbstractAssignmentExpression> assignmentExpressions;

        public CommaExpression(IReadOnlyList<AbstractAssignmentExpression> assignmentExpressions, bool isStrictMode) : base(isStrictMode)
        {
            this.assignmentExpressions = assignmentExpressions;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            IValue lastValue = UndefinedValue.Instance;
            foreach (var expression in assignmentExpressions)
            {
                var comp = expression.Evaluate(interpreter).GetValue();
                if (comp.IsAbrupt()) return comp;
                lastValue = comp.value!;
            }
            return Completion.NormalCompletion(lastValue);
        }
    }
}
