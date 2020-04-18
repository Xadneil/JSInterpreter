using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum UpdateOperation
    {
        Increment,
        Decrement
    }

    interface IUpdateExpression : IUnaryExpression
    {
    }

    class PostfixUpdateExpression : IUpdateExpression
    {
        public readonly ILeftHandSideExpression leftHandSideExpression;
        public readonly UpdateOperation updateOperation;

        public PostfixUpdateExpression(ILeftHandSideExpression leftHandSideExpression, UpdateOperation updateOperation)
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
            if (updateOperation == UpdateOperation.Decrement)
                newValue = new NumberValue(oldValue.number - 1);
            else
                newValue = new NumberValue(oldValue.number + 1);

            var putComp = reference.PutValue(newValue);
            if (putComp.IsAbrupt()) return putComp;

            return Completion.NormalCompletion(oldValue);
        }
    }

    class PrefixUpdateExpression : IUpdateExpression
    {
        public readonly IUnaryExpression unaryExpression;
        public readonly UpdateOperation updateOperation;

        public PrefixUpdateExpression(IUnaryExpression unaryExpression, UpdateOperation updateOperation)
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
            if (updateOperation == UpdateOperation.Decrement)
                newValue = new NumberValue(oldValue.number - 1);
            else
                newValue = new NumberValue(oldValue.number + 1);

            var putComp = reference.PutValue(newValue);
            if (putComp.IsAbrupt()) return putComp;

            return Completion.NormalCompletion(newValue);
        }
    }
}
