using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface IExpression : IHasEvaluate
    {
    }
    public class CommaExpression : IExpression
    {
        public readonly IReadOnlyList<IAssignmentExpression> assignmentExpressions;

        public CommaExpression(IReadOnlyList<IAssignmentExpression> assignmentExpressions)
        {
            this.assignmentExpressions = assignmentExpressions;
        }

        public Completion Evaluate(Interpreter interpreter)
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
