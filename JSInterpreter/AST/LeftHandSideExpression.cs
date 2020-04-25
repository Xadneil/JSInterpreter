using System;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter.AST
{
    public interface ILeftHandSideExpression : IUpdateExpression, IForInOfInitializer
    {
    }

    public interface INewExpression : ILeftHandSideExpression
    {
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

    public class NewExpression : INewExpression
    {
        public readonly INewExpression newExpression;

        public NewExpression(INewExpression newExpression)
        {
            this.newExpression = newExpression;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            return newExpression.EvaluateNew(interpreter, new Arguments(Utils.EmptyList<IArgumentItem>()));
        }
    }
}
