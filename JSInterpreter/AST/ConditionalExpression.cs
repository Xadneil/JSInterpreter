using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractConditionalExpression : AbstractAssignmentExpression
    {
        protected AbstractConditionalExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }
    public sealed class ConditionalExpression : AbstractConditionalExpression
    {
        public readonly AbstractLogicalOrExpression logicalOrExpression;
        public readonly AbstractAssignmentExpression ifTrueAssignmentExpression;
        public readonly AbstractAssignmentExpression ifFalseAssignmentExpression;

        public ConditionalExpression(AbstractLogicalOrExpression logicalOrExpression, AbstractAssignmentExpression ifTrueAssignmentExpression, AbstractAssignmentExpression ifFalseAssignmentExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.logicalOrExpression = logicalOrExpression;
            this.ifTrueAssignmentExpression = ifTrueAssignmentExpression;
            this.ifFalseAssignmentExpression = ifFalseAssignmentExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var conditionComp = logicalOrExpression.Evaluate(interpreter).GetValue();
            if (conditionComp.IsAbrupt()) return conditionComp;
            var condition = conditionComp.value!.ToBoolean();

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
