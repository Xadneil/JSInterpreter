using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum UnaryOperator
    {
        Delete,
        Void,
        Typeof,
        Plus,
        Negate,
        BitwiseNot,
        BooleanNot
    }

    interface IUnaryExpression : IExponentiationExpression
    {
    }

    class OperatorUnaryExpression : IUnaryExpression
    {
        public readonly UnaryOperator unaryOperator;
        public readonly IUnaryExpression unaryExpression;

        public OperatorUnaryExpression(UnaryOperator unaryOperator, IUnaryExpression unaryExpression)
        {
            this.unaryOperator = unaryOperator;
            this.unaryExpression = unaryExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            Completion comp;
            switch (unaryOperator)
            {
                case UnaryOperator.Delete:
                    return EvaluateDelete(interpreter);
                case UnaryOperator.Void:
                    unaryExpression.Evaluate(interpreter).GetValue();
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                case UnaryOperator.Typeof:
                    return EvaluateTypeof(interpreter);
                case UnaryOperator.Plus:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    return Completion.NormalCompletion(comp.value.ToNumber());
                case UnaryOperator.Negate:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    return Completion.NormalCompletion(new NumberValue(-comp.value.ToNumber().number));
                case UnaryOperator.BitwiseNot:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    return Completion.NormalCompletion(new NumberValue(~(int)comp.value.ToNumber().number));
                case UnaryOperator.BooleanNot:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    return Completion.NormalCompletion(comp.value.ToBoolean().boolean ? BooleanValue.False : BooleanValue.True);
                default:
                    throw new InvalidOperationException($"OperatorUnaryExpression.Evaluate: Unknown UnaryOperator enum value {(int)unaryOperator}");
            }
        }

        private Completion EvaluateDelete(Interpreter interpreter)
        {
            var refComp = unaryExpression.Evaluate(interpreter);
            if (refComp.IsAbrupt()) return refComp;
            var @ref = refComp.value;
            if (!(@ref is ReferenceValue reference))
                return Completion.NormalCompletion(BooleanValue.True);
            if (reference.IsUnresolvableReference())
            {
                if (reference.strict)
                    throw new InvalidOperationException("OperatorUnaryExpression.EvaluateDelete: cannot delete an unresolved member in strict mode");
                return Completion.NormalCompletion(BooleanValue.True);
            }
            if (reference.IsPropertyReference())
            {
                if (reference is SuperReferenceValue)
                    return Completion.ThrowReferenceError("OperatorUnaryExpression.EvaluateDelete: cannot delete from super");
                var baseObj = ((IValue)reference.baseValue).ToObject().value as Object;
                var deleteStatus = baseObj.Delete(reference.referencedName);
                if (deleteStatus.IsAbrupt()) return deleteStatus;
                var success = deleteStatus.value as BooleanValue;
                if (success == BooleanValue.False && reference.strict)
                    return Completion.ThrowTypeError("OperatorUnaryExpression.EvaluateDelete: delete failed in strict mode");
                return Completion.NormalCompletion(success);
            }
            if (!(reference.baseValue is EnvironmentRecord envRec))
                throw new InvalidOperationException("OperatorUnaryExpression.EvaluateDelete: unrecognized IReferenceable");
            return envRec.DeleteBinding(reference.referencedName);
        }

        private Completion EvaluateTypeof(Interpreter interpreter)
        {
            var valComp = unaryExpression.Evaluate(interpreter);
            if (valComp.value is ReferenceValue reference)
            {
                if (reference.IsUnresolvableReference()) return Completion.NormalCompletion(new StringValue("undefined"));
            }
            var val = valComp.GetValue();
            if (val.IsAbrupt()) return val;
            return Completion.NormalCompletion(new StringValue(val.value switch
            {
                UndefinedValue _ => "undefined",
                NullValue _ => "object",
                BooleanValue _ => "boolean",
                NumberValue _ => "number",
                StringValue _ => "string",
                FunctionObject _ => "function",
                Object _ => "object",
                _ => throw new InvalidOperationException("OperatorUnaryExpression.EvaluateTypeof: unknown type"),
            }));
        }
    }
}
