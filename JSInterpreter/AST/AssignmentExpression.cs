﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public enum AssignmentOperator
    {
        Multiply,
        Divide,
        Modulus,
        Plus,
        Minus,
        ShiftLeft,
        ShiftRight,
        ShiftRightUnsigned,
        BitwiseAnd,
        BitwiseXor,
        BitwiseOr,
        Exponentiate
    }

    public abstract class AbstractAssignmentExpression : AbstractExpression, IArrayLiteralItem, IArgumentItem
    {
        protected AbstractAssignmentExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class AssignmentExpression : AbstractAssignmentExpression
    {
        public readonly AbstractLeftHandSideExpression leftHandSideExpression;
        public readonly AbstractAssignmentExpression assignmentExpression;

        public AssignmentExpression(AbstractLeftHandSideExpression leftHandSideExpression, AbstractAssignmentExpression assignmentExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.assignmentExpression = assignmentExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            if (!(leftHandSideExpression is ObjectLiteral) && !(leftHandSideExpression is ArrayLiteral))
            {
                var lrefComp = leftHandSideExpression.Evaluate(interpreter);
                if (lrefComp.IsAbrupt()) return lrefComp;
                var lref = lrefComp.value;
                if (!(lref is ReferenceValue referenceValue))
                    throw new InvalidOperationException("AssignmentExpression.Evaluate: left hand side did not return a reference.");
                Completion rval;
                if (assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous && leftHandSideExpression is IdentifierReference)
                    rval = functionExpression.NamedEvaluate(interpreter, referenceValue.referencedName);
                else
                    rval = assignmentExpression.Evaluate(interpreter).GetValue();
                if (rval.IsAbrupt())
                    return rval;
                var comp = referenceValue.PutValue(rval.value!);
                if (comp.IsAbrupt()) return comp;
                return rval;
            }
            throw new NotImplementedException("Assignment to object or array literals is not implemented.");
        }
    }

    public sealed class OperatorAssignmentExpression : AbstractAssignmentExpression
    {
        public readonly AbstractLeftHandSideExpression leftHandSideExpression;
        public readonly AssignmentOperator assignmentOperator;
        public readonly AbstractAssignmentExpression assignmentExpression;

        public OperatorAssignmentExpression(AbstractLeftHandSideExpression leftHandSideExpression, AssignmentOperator assignmentOperator, AbstractAssignmentExpression assignmentExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.assignmentOperator = assignmentOperator;
            this.assignmentExpression = assignmentExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var lref = leftHandSideExpression.Evaluate(interpreter);
            if (!(lref.value is ReferenceValue referenceValue))
                throw new InvalidOperationException("OperatorAssignmentExpression.Evaluate: left hand side did not return a reference.");

            var lvalComp = lref.GetValue();
            if (lvalComp.IsAbrupt()) return lvalComp;
            var lval = lvalComp.value!;

            var rvalComp = assignmentExpression.Evaluate(interpreter).GetValue();
            if (rvalComp.IsAbrupt()) return rvalComp;
            var rval = rvalComp.value!;

            Completion result = assignmentOperator switch
            {
                AssignmentOperator.Multiply => MultiplicativeExpression.Calculate(lval, MultiplicativeOperator.Multiply, rval),
                AssignmentOperator.Divide => MultiplicativeExpression.Calculate(lval, MultiplicativeOperator.Divide, rval),
                AssignmentOperator.Modulus => MultiplicativeExpression.Calculate(lval, MultiplicativeOperator.Modulus, rval),
                AssignmentOperator.Plus => AdditiveExpression.Calculate(lval, AdditiveOperator.Add, rval),
                AssignmentOperator.Minus => AdditiveExpression.Calculate(lval, AdditiveOperator.Subtract, rval),
                AssignmentOperator.ShiftLeft => ShiftExpression.Calculate(lval, ShiftOperator.ShiftLeft, rval),
                AssignmentOperator.ShiftRight => ShiftExpression.Calculate(lval, ShiftOperator.ShiftRight, rval),
                AssignmentOperator.ShiftRightUnsigned => ShiftExpression.Calculate(lval, ShiftOperator.ShiftRightUnsigned, rval),
                AssignmentOperator.BitwiseAnd => IBitwiseExpression.Calculate(lval, BitwiseOperator.And, rval),
                AssignmentOperator.BitwiseXor => IBitwiseExpression.Calculate(lval, BitwiseOperator.Xor, rval),
                AssignmentOperator.BitwiseOr => IBitwiseExpression.Calculate(lval, BitwiseOperator.Or, rval),
                AssignmentOperator.Exponentiate => ExponentiationExpression.Calculate(lval, rval),
                _ => throw new InvalidOperationException($"OperatorAssignmentExpression.Evaluate: invalid AssignmentOperator enum value {(int)assignmentOperator}")
            };

            if (result.IsAbrupt()) return result;

            referenceValue.PutValue(result.value!);
            return result;
        }
    }
}
