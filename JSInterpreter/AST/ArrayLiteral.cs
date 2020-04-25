using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public class ArrayLiteral : IPrimaryExpression
    {
        public readonly IReadOnlyList<IArrayLiteralItem> arrayLiteralItems;

        public ArrayLiteral(IReadOnlyList<IArrayLiteralItem> arrayLiteralItems)
        {
            this.arrayLiteralItems = arrayLiteralItems;
        }

        public Completion Evaluate(Interpreter interpreter)
        {
            if (arrayLiteralItems.Count == 1 && arrayLiteralItems[0] is Elision e)
            {
                var array = ArrayObject.ArrayCreate(0);
                array.Set("length", new NumberValue(e.width), false);
                return Completion.NormalCompletion(array);
            }
            else
            {
                var array = ArrayObject.ArrayCreate(0);
                var len = ArrayAccumulate(array, 0);
                if (len.IsAbrupt()) return len;
                array.Set("length", len.value, false);
                return Completion.NormalCompletion(array);
            }
        }

        private Completion ArrayAccumulate(ArrayObject array, int nextIndex)
        {
            foreach (var item in arrayLiteralItems)
            {
                Completion valueComp;
                BooleanCompletion status;
                IValue value;
                switch (item)
                {
                    case IAssignmentExpression assignmentExpression:
                        valueComp = assignmentExpression.Evaluate(Interpreter.Instance()).GetValue();
                        if (valueComp.IsAbrupt()) return valueComp;
                        value = valueComp.value;
                        status = Utils.CreateDataProperty(array, nextIndex.ToString(), value);
                        if (status.IsAbrupt() || !status.Other)
                            throw new InvalidOperationException("Spec 12.2.5.2, Assignment, step 5");
                        nextIndex++;
                        break;
                    case Elision elision:
                        nextIndex += elision.width;
                        break;
                    case SpreadElement spreadElement:
                        valueComp = spreadElement.assignmentExpression.Evaluate(Interpreter.Instance()).GetValue();
                        if (valueComp.IsAbrupt()) return valueComp;
                        value = valueComp.value;
                        if (!(value is Object @object))
                            throw new InvalidOperationException($"ArrayLiteral: tried to initialize an array using a spread on a non-object");
                        var iteratorComp = @object.GetIterator();
                        if (iteratorComp.IsAbrupt()) return iteratorComp;
                        var iterator = iteratorComp.Other;
                        while (true)
                        {
                            var next = iterator.MoveNext();
                            if (iterator.Current.IsAbrupt()) return iterator.Current;
                            if (!next)
                                break;
                            status = Utils.CreateDataProperty(array, nextIndex.ToString(), iterator.Current.value);
                            if (status.IsAbrupt() || !status.Other)
                                throw new InvalidOperationException("Spec 12.2.5.2, Spread, step 4e");
                            nextIndex++;
                        }
                        break;
                }
            }
            return Completion.NormalCompletion(new NumberValue(nextIndex));
        }
    }

    public interface IArrayLiteralItem
    {
    }
}
