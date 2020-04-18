using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum AssignmentOperator
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

    interface IAssignmentExpression : IExpression, IArrayLiteralItem, IPropertyDefinition, IArgumentItem
    {
    }

    class AssignmentExpression : IAssignmentExpression
    {
        public readonly ILeftHandSideExpression leftHandSideExpression;
        public readonly IAssignmentExpression assignmentExpression;

        public AssignmentExpression(ILeftHandSideExpression leftHandSideExpression, IAssignmentExpression assignmentExpression)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.assignmentExpression = assignmentExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            if (!(leftHandSideExpression is ObjectLiteral) && !(leftHandSideExpression is ArrayLiteral))
            {
                var lrefComp = leftHandSideExpression.Evaluate(interpreter);
                if (lrefComp.IsAbrupt()) return lrefComp;
                var lref = lrefComp.value;
                if (!(lref is ReferenceValue referenceValue))
                    throw new InvalidOperationException("AssignmentExpression.Evaluate: left hand side did not return a reference.");
                Completion rval;
                if (assignmentExpression is FunctionExpression functionExpression && functionExpression.isAnonymous && (leftHandSideExpression is Identifier || leftHandSideExpression is IdentifierReference))
                    rval = functionExpression.NamedEvaluate(interpreter, referenceValue.referencedName);
                else
                    rval = assignmentExpression.Evaluate(interpreter).GetValue();
                if (rval.IsAbrupt())
                    return rval;
                referenceValue.PutValue(rval.value);
                return rval;
            }
            throw new NotImplementedException("Assignment to object or array literals is not implemented.");
        }
    }

    class OperatorAssignmentExpression : IAssignmentExpression
    {
        public readonly ILeftHandSideExpression leftHandSideExpression;
        public readonly AssignmentOperator assignmentOperator;
        public readonly IAssignmentExpression assignmentExpression;

        public OperatorAssignmentExpression(ILeftHandSideExpression leftHandSideExpression, AssignmentOperator assignmentOperator, IAssignmentExpression assignmentExpression)
        {
            this.leftHandSideExpression = leftHandSideExpression;
            this.assignmentOperator = assignmentOperator;
            this.assignmentExpression = assignmentExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var lref = leftHandSideExpression.Evaluate(interpreter);
            if (!(lref.value is ReferenceValue referenceValue))
                throw new InvalidOperationException("OperatorAssignmentExpression.Evaluate: left hand side did not return a reference.");

            var lvalComp = lref.GetValue();
            if (lvalComp.IsAbrupt()) return lvalComp;
            var lval = lvalComp.value;

            var rvalComp = assignmentExpression.Evaluate(interpreter).GetValue();
            if (rvalComp.IsAbrupt()) return rvalComp;
            var rval = rvalComp.value;

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
                AssignmentOperator.BitwiseAnd => BitwiseExpression.Calculate(lval, BitwiseOperator.And, rval),
                AssignmentOperator.BitwiseXor => BitwiseExpression.Calculate(lval, BitwiseOperator.Xor, rval),
                AssignmentOperator.BitwiseOr => BitwiseExpression.Calculate(lval, BitwiseOperator.Or, rval),
                AssignmentOperator.Exponentiate => ExponentiationExpression.Calculate(lval, rval),
                _ => throw new InvalidOperationException($"OperatorAssignmentExpression.Evaluate: invalid AssignmentOperator enum value {(int)assignmentOperator}")
            };

            if (result.IsAbrupt()) return result;

            referenceValue.PutValue(result.value);
            return result;
        }
    }
}
