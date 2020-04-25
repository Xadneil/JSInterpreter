using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface IConditionalExpression : IAssignmentExpression
    {
    }
    public class ConditionalExpression : IConditionalExpression
    {
        public readonly ILogicalOrExpression logicalOrExpression;
        public readonly IAssignmentExpression ifTrueAssignmentExpression;
        public readonly IAssignmentExpression ifFalseAssignmentExpression;

        public ConditionalExpression(ILogicalOrExpression logicalOrExpression, IAssignmentExpression ifTrueAssignmentExpression, IAssignmentExpression ifFalseAssignmentExpression)
        {
            this.logicalOrExpression = logicalOrExpression;
            this.ifTrueAssignmentExpression = ifTrueAssignmentExpression;
            this.ifFalseAssignmentExpression = ifFalseAssignmentExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var conditionComp = logicalOrExpression.Evaluate(interpreter).GetValue();
            if (conditionComp.IsAbrupt()) return conditionComp;
            var condition = conditionComp.value.ToBoolean();

            if (condition.boolean)
            {
                return ifTrueAssignmentExpression.Evaluate(interpreter).GetValue();
            }
            else
            {
                return ifFalseAssignmentExpression.Evaluate(interpreter).GetValue();
            }
        }
    }
}
