using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractMemberExpression : AbstractNewExpression
    {
        protected AbstractMemberExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public sealed class IndexMemberExpression : AbstractMemberExpression
    {
        public readonly AbstractMemberExpression indexedMemberExpression;
        public readonly AbstractExpression indexerExpression;

        public IndexMemberExpression(AbstractMemberExpression indexedMemberExpression, AbstractExpression indexerExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.indexedMemberExpression = indexedMemberExpression;
            this.indexerExpression = indexerExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = indexedMemberExpression.Evaluate(interpreter).GetValue();
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

    public sealed class DotMemberExpression : AbstractMemberExpression
    {
        public readonly AbstractMemberExpression dotMemberExpression;
        public readonly string dotIdentifierName;

        public DotMemberExpression(AbstractMemberExpression dotMemberExpression, string dotIdentifierName, bool isStrictMode) : base(isStrictMode)
        {
            this.dotMemberExpression = dotMemberExpression;
            this.dotIdentifierName = dotIdentifierName;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = dotMemberExpression.Evaluate(interpreter).GetValue();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value!;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            return Completion.NormalCompletion(new ReferenceValue(baseValue, dotIdentifierName, IsStrictMode));
        }
    }

    public static class SuperHelper
    {
        public static Completion MakeSuperPropertyReference(IValue actualThis, string propertyKey, bool strict)
        {
            var env = Interpreter.Instance().GetThisEnvironment();
            if (!env.HasSuperBinding())
                throw new InvalidOperationException("SuperHelper.MakeSuperPropertyReference: Interpreter.GetThisEnvironment has no super binding");
            var baseValueComp = ((FunctionEnvironmentRecord)env).GetSuperBase();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value!;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            return Completion.NormalCompletion(new SuperReferenceValue(baseValue, propertyKey, strict, actualThis));
        }
    }

    public sealed class SuperIndexMemberExpression : AbstractMemberExpression
    {
        public readonly AbstractExpression superIndexerExpression;

        public SuperIndexMemberExpression(AbstractExpression superIndexerExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.superIndexerExpression = superIndexerExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var envBase = interpreter.GetThisEnvironment();
            if (!(envBase is FunctionEnvironmentRecord env))
                throw new InvalidOperationException("Invalid This Environment type for Super");

            var actualThisComp = env.GetThisBinding();
            if (actualThisComp.IsAbrupt()) return actualThisComp;
            var actualThis = actualThisComp.value!;

            var propertyNameComp = superIndexerExpression.Evaluate(interpreter).GetValue();
            if (propertyNameComp.IsAbrupt()) return propertyNameComp;
            var propertyNameValue = propertyNameComp.value!;

            var propertyKey = propertyNameValue.ToPropertyKey();
            if (propertyKey.IsAbrupt()) return propertyKey;
            return SuperHelper.MakeSuperPropertyReference(actualThis, propertyKey.Other!, IsStrictMode);
        }
    }

    public sealed class SuperDotMemberExpression : AbstractMemberExpression
    {
        public readonly string superDotIdentifierName;

        public SuperDotMemberExpression(string superDotIdentifierName, bool isStrictMode) : base(isStrictMode)
        {
            this.superDotIdentifierName = superDotIdentifierName;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            var envBase = interpreter.GetThisEnvironment();
            if (!(envBase is FunctionEnvironmentRecord env))
                throw new InvalidOperationException("Invalid This Environment type for Super");

            var actualThisComp = env.GetThisBinding();
            if (actualThisComp.IsAbrupt()) return actualThisComp;
            var actualThis = actualThisComp.value!;
            return SuperHelper.MakeSuperPropertyReference(actualThis, superDotIdentifierName, IsStrictMode);
        }
    }

    public sealed class NewMemberExpression : AbstractMemberExpression
    {
        public readonly AbstractMemberExpression newMemberExpression;
        public readonly Arguments newArguments;

        public NewMemberExpression(AbstractMemberExpression newMemberExpression, Arguments newArguments, bool isStrictMode) : base(isStrictMode)
        {
            this.newMemberExpression = newMemberExpression;
            this.newArguments = newArguments;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return newMemberExpression.EvaluateNew(interpreter, newArguments);
        }
    }
}
