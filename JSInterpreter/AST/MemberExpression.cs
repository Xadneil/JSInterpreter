using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    interface IMemberExpression : INewExpression
    {
    }

    class IndexMemberExpression : IMemberExpression
    {
        public readonly IMemberExpression indexedMemberExpression;
        public readonly IExpression indexerExpression;

        public IndexMemberExpression(IMemberExpression indexedMemberExpression, IExpression indexerExpression)
        {
            this.indexedMemberExpression = indexedMemberExpression;
            this.indexerExpression = indexerExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = indexedMemberExpression.Evaluate(interpreter).GetValue();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value;
            var propertyNameComp = indexerExpression.Evaluate(interpreter).GetValue();
            if (propertyNameComp.IsAbrupt()) return propertyNameComp;
            var propertyNameValue = propertyNameComp.value;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            var propertyKey = propertyNameValue.ToPropertyKey();
            if (propertyKey.IsAbrupt()) return propertyKey;
            //TODO detect strict mode
            return Completion.NormalCompletion(new ReferenceValue(baseValue, propertyKey.Other, strict: true));
        }
    }

    class DotMemberExpression : IMemberExpression
    {
        public readonly IMemberExpression dotMemberExpression;
        public readonly string dotIdentifierName;

        public DotMemberExpression(IMemberExpression dotMemberExpression, string dotIdentifierName)
        {
            this.dotMemberExpression = dotMemberExpression;
            this.dotIdentifierName = dotIdentifierName;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var baseValueComp = dotMemberExpression.Evaluate(interpreter).GetValue();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            //TODO detect strict mode
            return Completion.NormalCompletion(new ReferenceValue(baseValue, dotIdentifierName, strict: true));
        }
    }

    internal static class SuperHelper
    {
        public static Completion MakeSuperPropertyReference(IValue actualThis, string propertyKey, bool strict)
        {
            var env = Interpreter.Instance().GetThisEnvironment();
            if (!env.HasSuperBinding())
                throw new InvalidOperationException("SuperHelper.MakeSuperPropertyReference: Interpreter.GetThisEnvironment has no super binding");
            var baseValueComp = ((FunctionEnvironmentRecord)env).GetSuperBase();
            if (baseValueComp.IsAbrupt()) return baseValueComp;
            var baseValue = baseValueComp.value;
            var coercible = baseValue.RequireObjectCoercible();
            if (coercible.IsAbrupt()) return coercible;
            return Completion.NormalCompletion(new SuperReferenceValue(baseValue, propertyKey, strict, actualThis));
        }
    }

    class SuperIndexMemberExpression : IMemberExpression
    {
        public readonly IExpression superIndexerExpression;

        public SuperIndexMemberExpression(IExpression superIndexerExpression)
        {
            this.superIndexerExpression = superIndexerExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var envBase = interpreter.GetThisEnvironment();
            if (!(envBase is FunctionEnvironmentRecord env))
                throw new InvalidOperationException("Invalid This Environment type for Super");

            var actualThisComp = env.GetThisBinding();
            if (actualThisComp.IsAbrupt()) return actualThisComp;
            var actualThis = actualThisComp.value;

            var propertyNameComp = superIndexerExpression.Evaluate(interpreter).GetValue();
            if (propertyNameComp.IsAbrupt()) return propertyNameComp;
            var propertyNameValue = propertyNameComp.value;

            var propertyKey = propertyNameValue.ToPropertyKey();
            if (propertyKey.IsAbrupt()) return propertyKey;
            //TODO detect strict mode
            return SuperHelper.MakeSuperPropertyReference(actualThis, propertyKey.Other, true);
        }
    }

    class SuperDotMemberExpression : IMemberExpression
    {
        public readonly string superDotIdentifierName;

        public SuperDotMemberExpression(string superDotIdentifierName)
        {
            this.superDotIdentifierName = superDotIdentifierName;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var envBase = interpreter.GetThisEnvironment();
            if (!(envBase is FunctionEnvironmentRecord env))
                throw new InvalidOperationException("Invalid This Environment type for Super");

            var actualThisComp = env.GetThisBinding();
            if (actualThisComp.IsAbrupt()) return actualThisComp;
            var actualThis = actualThisComp.value;

            //TODO detect strict mode
            return SuperHelper.MakeSuperPropertyReference(actualThis, superDotIdentifierName, true);
        }
    }

    class NewMemberExpression : IMemberExpression
    {
        public readonly IMemberExpression newMemberExpression;
        public readonly Arguments newArguments;

        public NewMemberExpression(IMemberExpression newMemberExpression, Arguments newArguments)
        {
            this.newMemberExpression = newMemberExpression;
            this.newArguments = newArguments;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            var constructorComp = newMemberExpression.Evaluate(interpreter).GetValue();
            if (constructorComp.IsAbrupt()) return constructorComp;
            var constructor = constructorComp.value;

            var argumentValues = newArguments.ArgumentListEvaluation();
            if (argumentValues.IsAbrupt()) return argumentValues;

            if (!(constructor is FunctionObject @object))
                return Completion.ThrowTypeError("NewMemberExpression: Constructor is not an object.");
            return @object.Construct(argumentValues.Other);
        }
    }
}
