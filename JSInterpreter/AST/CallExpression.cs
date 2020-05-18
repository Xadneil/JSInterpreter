using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractCallExpression : AbstractLeftHandSideExpression
    {
        protected AbstractCallExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class MemberCallExpression : AbstractCallExpression
    {
        public readonly AbstractMemberExpression memberExpression;
        public readonly Arguments arguments;

        public MemberCallExpression(AbstractMemberExpression memberExpression, Arguments arguments, bool isStrictMode) : base(isStrictMode)
        {
            this.memberExpression = memberExpression;
            this.arguments = arguments;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var @ref = memberExpression.Evaluate(interpreter);
            var funcComp = @ref.GetValue();
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value!;

            if (func == UndefinedValue.Instance && @ref.value is ReferenceValue r)
            {
                return Completion.ThrowReferenceError($"Cannot call undefined method {r.referencedName } on a {r.baseValue.GetType().Name}");
            }

            if (@ref.value is ReferenceValue referenceValue && !referenceValue.baseValue.IsPrimitive() && referenceValue.referencedName == "eval")
            {
                if (func == interpreter.CurrentRealm().Intrinsics.Eval)
                {
                    var argList = arguments.ArgumentListEvaluation();
                    if (argList.IsAbrupt()) return argList;
                    if (!argList.Other.Any())
                        return Completion.NormalCompletion(UndefinedValue.Instance);
                    var evalText = argList.Other![0];
                    var evalRealm = interpreter.CurrentRealm();
                    //TODO HostEnsureCanCompileStrings
                    return GlobalObjectProperties.PerformEval(evalText, evalRealm, IsStrictMode, true);
                }
            }
            //TODO: support tail calls
            return Utils.EvaluateCall(func, @ref.value!, arguments, tailCall: false);
        }
    }

    public sealed class SuperCall : AbstractCallExpression
    {
        public readonly Arguments arguments;

        public SuperCall(Arguments arguments, bool isStrictMode) : base(isStrictMode)
        {
            this.arguments = arguments;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var newTargetValue = interpreter.GetNewTarget();
            if (!(newTargetValue is Object newTarget))
                throw new InvalidOperationException("Spec 12.3.5.1 super step 2");
            var funcComp = GetSuperConstructor(interpreter);
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value as Constructor;

            var argList = arguments.ArgumentListEvaluation();
            if (argList.IsAbrupt()) return argList;

            var result = func!.Construct(argList.Other, newTarget);
            if (result.IsAbrupt()) return result;
            var thisERBase = interpreter.GetThisEnvironment();
            if (!(thisERBase is FunctionEnvironmentRecord thisER))
                throw new InvalidOperationException("Invalid This Environment type for Super");
            return thisER.BindThisValue(result.value!);
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

    public sealed class RecursiveCallExpression : AbstractCallExpression
    {
        public readonly AbstractCallExpression callExpression;
        public readonly Arguments arguments;

        public RecursiveCallExpression(AbstractCallExpression callExpression, Arguments arguments, bool isStrictMode) : base(isStrictMode)
        {
            this.callExpression = callExpression;
            this.arguments = arguments;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var @ref = callExpression.Evaluate(interpreter);
            var funcComp = @ref.GetValue();
            if (funcComp.IsAbrupt()) return funcComp;
            var func = funcComp.value!;

            if (func == UndefinedValue.Instance && @ref.value is ReferenceValue r)
                return Completion.ThrowReferenceError($"Cannot call undefined method {r.referencedName } on a {r.baseValue.GetType().Name}");

            //TODO: support tail calls
            return Utils.EvaluateCall(func, @ref.value!, arguments, tailCall: false);
        }
    }

    public sealed class IndexCallExpression : AbstractCallExpression
    {
        public readonly AbstractCallExpression callExpression;
        public readonly AbstractExpression indexerExpression;

        public IndexCallExpression(AbstractCallExpression callExpression, AbstractExpression indexerExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.callExpression = callExpression;
            this.indexerExpression = indexerExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = callExpression.Evaluate(interpreter).GetValue();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value!;
            var propertyNameComp = indexerExpression.Evaluate(interpreter).GetValue();
            if (propertyNameComp.IsAbrupt()) return propertyNameComp;
            var propertyNameValue = propertyNameComp.value!;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            var propertyKey = propertyNameValue.ToPropertyKey();
            if (propertyKey.IsAbrupt()) return propertyKey;
            return Completion.NormalCompletion(new ReferenceValue(baseValue, propertyKey.Other!, IsStrictMode));
        }
    }

    public sealed class DotCallExpression : AbstractCallExpression
    {
        public readonly AbstractCallExpression callExpression;
        public readonly string dotIdentifierName;

        public DotCallExpression(AbstractCallExpression callExpression, string dotIdentifierName, bool isStrictMode) : base(isStrictMode)
        {
            this.callExpression = callExpression;
            this.dotIdentifierName = dotIdentifierName;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = callExpression.Evaluate(interpreter).GetValue();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value!;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            return Completion.NormalCompletion(new ReferenceValue(baseValue, dotIdentifierName, IsStrictMode));
        }
    }
}