using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public enum UpdateOperator
    {
        Increment,
        Decrement
    }

    public interface IUpdateExpression : IUnaryExpression
    {
    }

    public class PostfixUpdateExpression : IUpdateExpression
    {
        public readonly ILeftHandSideExpression leftHandSideExpression;
        public readonly UpdateOperator updateOperation;

        public PostfixUpdateExpression(ILeftHandSideExpression leftHandSideExpression, UpdateOperator updateOperation)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.updateOperation = updateOperation;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var lhs = leftHandSideExpression.Evaluate(interpreter).GetValue();
            if (lhs.IsAbrupt()) return lhs;

            var oldValueComp = lhs.value.ToNumber();
            if (oldValueComp.IsAbrupt()) return oldValueComp;
            var oldValue = oldValueComp.value as NumberValue;

            if (!(lhs.value is ReferenceValue reference))
                throw new InvalidOperationException("PostfixUpdateExpression.Evaluate: leftHandSideExpression did not return a reference");

            NumberValue newValue;
            if (updateOperation == UpdateOperator.Decrement)
                newValue = new NumberValue(oldValue.number - 1);
            else
                newValue = new NumberValue(oldValue.number + 1);

            var putComp = reference.PutValue(newValue);
            if (putComp.IsAbrupt()) return putComp;

            return Completion.NormalCompletion(oldValue);
        }
    }

    public class PrefixUpdateExpression : IUpdateExpression
    {
        public readonly IUnaryExpression unaryExpression;
        public readonly UpdateOperator updateOperation;

        public PrefixUpdateExpression(IUnaryExpression unaryExpression, UpdateOperator updateOperation)
        {
            this.unaryExpression = unaryExpression;
            this.updateOperation = updateOperation;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var lhs = unaryExpression.Evaluate(interpreter).GetValue();
            if (lhs.IsAbrupt()) return lhs;

            var oldValueComp = lhs.value.ToNumber();
            if (oldValueComp.IsAbrupt()) return oldValueComp;
            var oldValue = oldValueComp.value as NumberValue;

            if (!(lhs.value is ReferenceValue reference))
                throw new InvalidOperationException("PrefixUpdateExpression.Evaluate: unaryExpression did not return a reference");

            NumberValue newValue;
            if (updateOperation == UpdateOperator.Decrement)
                newValue = new NumberValue(oldValue.number - 1);
            else
                newValue = new NumberValue(oldValue.number + 1);

            var putComp = reference.PutValue(newValue);
            if (putComp.IsAbrupt()) return putComp;

            return Completion.NormalCompletion(newValue);
        }
    }
}
