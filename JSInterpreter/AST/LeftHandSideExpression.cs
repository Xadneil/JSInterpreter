using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public abstract class AbstractLeftHandSideExpression : AbstractUpdateExpression, IForInOfInitializer
    {
        protected AbstractLeftHandSideExpression(bool isStrictMode) : base(isStrictMode)
        {
        }
    }

    public abstract class AbstractNewExpression : AbstractLeftHandSideExpression
    {
        protected AbstractNewExpression(bool isStrictMode) : base(isStrictMode)
        {
        }

        public Completion EvaluateNew(Interpreter interpreter, Arguments arguments)
        {
            var constructorComp = Evaluate(interpreter).GetValue();
            if (constructorComp.IsAbrupt()) return constructorComp;
            var constructor = constructorComp.value;

            var argumentValues = arguments.ArgumentListEvaluation();
            if (argumentValues.IsAbrupt()) return argumentValues;

            if (!(constructor is Constructor @object))
                return Completion.ThrowTypeError("EvaluateNew: the expression is not a constructor.");
            return @object.Construct(argumentValues.Other);
        }
    }

    public sealed class NewExpression : AbstractNewExpression
    {
        public readonly AbstractNewExpression newExpression;

        public NewExpression(AbstractNewExpression newExpression, bool isStrictMode) : base(isStrictMode)
        {
            this.newExpression = newExpression;
        }

        public override Completion Evaluate(Interpreter interpreter)
        {
            return newExpression.EvaluateNew(interpreter, new Arguments(Utils.EmptyList<IArgumentItem>()));
        }
    }
}
