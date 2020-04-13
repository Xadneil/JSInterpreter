using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    interface ICallExpression : ILeftHandSideExpression
    {
    }

    class MemberCallExpression : ICallExpression
    {
        public readonly IMemberExpression memberExpression;
        public readonly Arguments arguments;

        public MemberCallExpression(IMemberExpression memberExpression, Arguments arguments)
        {
            this.memberExpression = memberExpression;
            this.arguments = arguments;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var @ref = memberExpression.Evaluate(interpreter);
            var funcComp = @ref.GetValue();
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value;

            if (@ref.value is ReferenceValue referenceValue && !referenceValue.baseValue.IsPrimitive() && referenceValue.referencedName == "eval")
            {
                throw new NotImplementedException("MemberCallExpression.Evaluate: eval is not implemented.");
            }
            //TODO: support tail calls
            return Utils.EvaluateCall(func, @ref.value, arguments, tailCall: false);
        }
    }

    class SuperCall : ICallExpression
    {
        public readonly Arguments arguments;

        public SuperCall(Arguments arguments)
        {
            this.arguments = arguments;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var newTargetValue = interpreter.GetNewTarget();
            if (!(newTargetValue is Object newTarget))
                throw new InvalidOperationException("Spec 12.3.5.1 super step 2");
            var funcComp = GetSuperConstructor(interpreter);
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value as FunctionObject;

            var (completion, argList) = arguments.ArgumentListEvaluation();
            if (completion.IsAbrupt()) return completion;

            var result = func.Construct(argList, newTarget);
            if (result.IsAbrupt()) return result;
            var thisERBase = interpreter.GetThisEnvironment();
            if (!(thisERBase is FunctionEnvironmentRecord thisER))
                throw new InvalidOperationException("Invalid This Environment type for Super");
            return thisER.BindThisValue(result.value);
        }

        private Completion GetSuperConstructor(Interpreter interpreter)
        {
            var envRecBase = interpreter.GetThisEnvironment();
            if (!(envRecBase is FunctionEnvironmentRecord envRec))
                throw new InvalidOperationException("Spec 12.3.5.2 step 2");
            var activeFunction = envRec.FunctionObject;
            var superConstructor = activeFunction.GetPrototypeOf().value;
            if (!(superConstructor is Constructor))
                return Completion.ThrowTypeError("Super call: The prototype is not a constructor");
            return Completion.NormalCompletion(superConstructor);
        }
    }

    class RecursiveCallExpression : ICallExpression
    {
        public readonly ICallExpression callExpression;
        public readonly Arguments arguments;

        public RecursiveCallExpression(ICallExpression callExpression, Arguments arguments)
        {
            this.callExpression = callExpression;
            this.arguments = arguments;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var @ref = callExpression.Evaluate(interpreter);
            var funcComp = @ref.GetValue();
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value;
            //TODO: support tail calls
            return Utils.EvaluateCall(func, @ref.value, arguments, tailCall: false);
        }
    }

    class IndexCallExpression : ICallExpression
    {
        public readonly ICallExpression callExpression;
        public readonly IExpression indexerExpression;

        public IndexCallExpression(ICallExpression callExpression, IExpression indexerExpression)
        {
            this.callExpression = callExpression;
            this.indexerExpression = indexerExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = callExpression.Evaluate(interpreter).GetValue();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value;
            var propertyNameComp = indexerExpression.Evaluate(interpreter).GetValue();
            if (propertyNameComp.IsAbrupt()) return propertyNameComp;
            var propertyNameValue = propertyNameComp.value;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            //TODO detect strict mode
            return Completion.NormalCompletion(new ReferenceValue(baseValue, propertyNameValue.ToString(), strict: true));
        }
    }

    class DotCallExpression : ICallExpression
    {
        public readonly ICallExpression callExpression;
        public readonly string dotIdentifierName;

        public DotCallExpression(ICallExpression callExpression, string dotIdentifierName)
        {
            this.callExpression = callExpression;
            this.dotIdentifierName = dotIdentifierName;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = callExpression.Evaluate(interpreter).GetValue();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            //TODO detect strict mode
            return Completion.NormalCompletion(new ReferenceValue(baseValue, dotIdentifierName, strict: true));
        }
    }
}