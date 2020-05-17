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

    public abstract class AbstractUpdateExpression : AbstractUnaryExpression
    {
        protected AbstractUpdateExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class PostfixUpdateExpression : AbstractUpdateExpression
    {
        public readonly AbstractLeftHandSideExpression leftHandSideExpression;
        public readonly UpdateOperator updateOperation;

        public PostfixUpdateExpression(AbstractLeftHandSideExpression leftHandSideExpression, UpdateOperator updateOperation, bool isStrictMode) : base(isStrictMode)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.updateOperation = updateOperation;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var lhs = leftHandSideExpression.Evaluate(interpreter);
            if (lhs.IsAbrupt()) return lhs;

            var lhsValue = lhs.GetValue();
            if (lhsValue.IsAbrupt()) return lhsValue;

            var oldValueComp = lhsValue.value!.ToNumber();
            if (oldValueComp.IsAbrupt()) return oldValueComp;
            var oldValue = oldValueComp.value as NumberValue;

            if (!(lhs.value is ReferenceValue reference))
                throw new InvalidOperationException("PostfixUpdateExpression.Evaluate: leftHandSideExpression did not return a reference");

            NumberValue newValue;
            if (updateOperation == UpdateOperator.Decrement)
                newValue = new NumberValue(oldValue!.number - 1);
            else
                newValue = new NumberValue(oldValue!.number + 1);

            var putComp = reference.PutValue(newValue);
            if (putComp.IsAbrupt()) return putComp;

            return Completion.NormalCompletion(oldValue);
        }
    }

    public sealed class PrefixUpdateExpression : AbstractUpdateExpression
    {
        public readonly AbstractUnaryExpression unaryExpression;
        public readonly UpdateOperator updateOperation;

        public PrefixUpdateExpression(AbstractUnaryExpression unaryExpression, UpdateOperator updateOperation, bool isStrictMode) : base(isStrictMode)
        {
            this.unaryExpression = unaryExpression;
            this.updateOperation = updateOperation;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var lhs = unaryExpression.Evaluate(interpreter);
            if (lhs.IsAbrupt()) return lhs;

            var lhsValue = lhs.GetValue();
            if (lhsValue.IsAbrupt()) return lhsValue;

            var oldValueComp = lhsValue.value!.ToNumber();
            if (oldValueComp.IsAbrupt()) return oldValueComp;
            var oldValue = oldValueComp.value as NumberValue;

            if (!(lhs.value is ReferenceValue reference))
                throw new InvalidOperationException("PrefixUpdateExpression.Evaluate: unaryExpression did not return a reference");

            NumberValue newValue;
            if (updateOperation == UpdateOperator.Decrement)
                newValue = new NumberValue(oldValue!.number - 1);
            else
                newValue = new NumberValue(oldValue!.number + 1);

            var putComp = reference.PutValue(newValue);
            if (putComp.IsAbrupt()) return putComp;

            return Completion.NormalCompletion(newValue);
        }
    }
}
