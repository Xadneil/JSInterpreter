using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public enum UnaryOperator
    {
        Delete,
        Void,
        Typeof,
        Plus,
        Negate,
        BitwiseNot,
        LogicalNot
    }

    public abstract class AbstractUnaryExpression : AbstractExponentiationExpression
    {
        protected AbstractUnaryExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class OperatorUnaryExpression : AbstractUnaryExpression
    {
        public readonly UnaryOperator unaryOperator;
        public readonly AbstractUnaryExpression unaryExpression;

        public OperatorUnaryExpression(UnaryOperator unaryOperator, AbstractUnaryExpression unaryExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.unaryOperator = unaryOperator;
            this.unaryExpression = unaryExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            Completion comp, comp2;
            switch (unaryOperator)
            {
                case UnaryOperator.Delete:
                    var deleteComp = EvaluateDelete(interpreter);
                    if (deleteComp.IsAbrupt()) return deleteComp;
                    return Completion.NormalCompletion(deleteComp.Other ? BooleanValue.True : BooleanValue.False);
                case UnaryOperator.Void:
                    unaryExpression.Evaluate(interpreter).GetValue();
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                case UnaryOperator.Typeof:
                    return EvaluateTypeof(interpreter);
                case UnaryOperator.Plus:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    return comp.value!.ToNumber();
                case UnaryOperator.Negate:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    comp2 = comp.value!.ToNumber();
                    if (comp2.IsAbrupt()) return comp2;
                    return Completion.NormalCompletion(new NumberValue(-(comp2.value as NumberValue)!.number));
                case UnaryOperator.BitwiseNot:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    comp2 = comp.value!.ToNumber();
                    if (comp2.IsAbrupt()) return comp2;
                    return Completion.NormalCompletion(new NumberValue(~(int)(comp2.value as NumberValue)!.number));
                case UnaryOperator.LogicalNot:
                    comp = unaryExpression.Evaluate(interpreter).GetValue();
                    if (comp.IsAbrupt()) return comp;
                    return Completion.NormalCompletion(comp.value!.ToBoolean().boolean ? BooleanValue.False : BooleanValue.True);
                default:
                    throw new InvalidOperationException($"OperatorUnaryExpression.Evaluate: Unknown UnaryOperator enum value {(int)unaryOperator}");
            }
        }

        private BooleanCompletion EvaluateDelete(Interpreter interpreter)
        {
            var refComp = unaryExpression.Evaluate(interpreter);
            if (refComp.IsAbrupt()) return refComp.WithEmptyBool();
            var @ref = refComp.value;
            if (!(@ref is ReferenceValue reference))
                return true;
            if (reference.IsUnresolvableReference())
            {
                if (reference.strict)
                    throw new InvalidOperationException("OperatorUnaryExpression.EvaluateDelete: cannot delete an unresolved member in strict mode");
                return true;
            }
            if (reference.IsPropertyReference())
            {
                if (reference is SuperReferenceValue)
                    return Completion.ThrowReferenceError("OperatorUnaryExpression.EvaluateDelete: cannot delete from super").WithEmptyBool();
                var baseObj = ((IValue)reference.baseValue).ToObject().value as Object;
                var deleteStatus = baseObj!.InternalDelete(reference.referencedName);
                if (deleteStatus.IsAbrupt()) return deleteStatus;
                var success = deleteStatus.Other;
                if (success == false && reference.strict)
                    return Completion.ThrowTypeError("OperatorUnaryExpression.EvaluateDelete: delete failed in strict mode").WithEmptyBool();
                return success;
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
