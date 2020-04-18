﻿using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    enum RelationalOperator
    {
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual,
        Instanceof,
        In
    }

    interface IRelationalExpression : IEqualityExpression
    {
    }

    class RelationalExpression : IRelationalExpression
    {
        public readonly RelationalOperator relationalOperator;
        public readonly IShiftExpression shiftExpression;
        public readonly IRelationalExpression relationalExpression;

        public RelationalExpression(IRelationalExpression relationalExpression, RelationalOperator relationalOperator, IShiftExpression shiftExpression)
        {
            this.relationalExpression = relationalExpression;
            this.relationalOperator = relationalOperator;
            this.shiftExpression = shiftExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var leftValueComp = relationalExpression.Evaluate(interpreter).GetValue();
            if (leftValueComp.IsAbrupt()) return leftValueComp;
            var leftValue = leftValueComp.value;
            var rightValueComp = shiftExpression.Evaluate(interpreter).GetValue();
            if (rightValueComp.IsAbrupt()) return rightValueComp;
            var rightValue = rightValueComp.value;

            Completion r;
            switch (relationalOperator)
            {
                case RelationalOperator.LessThan:
                    r = AbstractRelationalComparison(leftValue, rightValue);
                    if (r.IsAbrupt()) return r;
                    if (r.value == UndefinedValue.Instance) return Completion.NormalCompletion(BooleanValue.False);
                    return r;
                case RelationalOperator.GreaterThan:
                    r = AbstractRelationalComparison(rightValue, leftValue, false);
                    if (r.IsAbrupt()) return r;
                    if (r.value == UndefinedValue.Instance) return Completion.NormalCompletion(BooleanValue.False);
                    return r;
                case RelationalOperator.LessThanOrEqual:
                    r = AbstractRelationalComparison(rightValue, leftValue, false);
                    if (r.IsAbrupt()) return r;
                    if (r.value == UndefinedValue.Instance || r.value == BooleanValue.True) return Completion.NormalCompletion(BooleanValue.False);
                    return Completion.NormalCompletion(BooleanValue.True);
                case RelationalOperator.GreaterThanOrEqual:
                    r = AbstractRelationalComparison(leftValue, rightValue);
                    if (r.IsAbrupt()) return r;
                    if (r.value == UndefinedValue.Instance || r.value == BooleanValue.True) return Completion.NormalCompletion(BooleanValue.False);
                    return Completion.NormalCompletion(BooleanValue.True);
                case RelationalOperator.Instanceof:
                    return InstanceOf(leftValue, rightValue);
                case RelationalOperator.In:
                    if (!(rightValue is Object o))
                        return Completion.ThrowTypeError();
                    var propertyKey = leftValue.ToPropertyKey();
                    if (propertyKey.IsAbrupt()) return propertyKey;
                    return o.HasProperty(propertyKey.Other);
                default:
                    throw new InvalidOperationException($"RelationalExpression.Evaluate: unknown RelationalOperator enum value {(int)relationalOperator}");
            }
        }

        private Completion AbstractRelationalComparison(IValue x, IValue y, bool leftFirst = true)
        {
            IValue px, py;
            if (leftFirst)
            {
                var xComp = x.ToPrimitive();
                if (xComp.IsAbrupt()) return xComp;
                px = xComp.value;
                var yComp = y.ToPrimitive();
                if (yComp.IsAbrupt()) return yComp;
                py = yComp.value;
            }
            else
            {
                var yComp = y.ToPrimitive();
                if (yComp.IsAbrupt()) return yComp;
                py = yComp.value;
                var xComp = x.ToPrimitive();
                if (xComp.IsAbrupt()) return xComp;
                px = xComp.value;
            }
            if (px is StringValue sx && py is StringValue sy)
            {
                return Completion.NormalCompletion((sx.@string.CompareTo(sy.@string) < 0) ? BooleanValue.True : BooleanValue.False);
            }
            else
            {
                var nxComp = px.ToNumber();
                if (nxComp.IsAbrupt()) return nxComp;
                var nx = nxComp.value as NumberValue;
                var nyComp = py.ToNumber();
                if (nyComp.IsAbrupt()) return nyComp;
                var ny = nyComp.value as NumberValue;

                if (double.IsNaN(nx.number) || double.IsNaN(ny.number))
                    return Completion.NormalCompletion(UndefinedValue.Instance);
                return Completion.NormalCompletion((nx.number < ny.number) ? BooleanValue.True : BooleanValue.False);
            }
        }

        private Completion InstanceOf(IValue V, IValue target)
        {
            if (!(target is Object))
                return Completion.ThrowTypeError();
            if (!(target is Callable callable))
                throw new InvalidOperationException("RelationalExpression.InstanceOf: LHS must be a function object.");
            return callable.OrdinaryHasInstance(V);
        }
    }
}
