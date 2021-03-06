﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSInterpreter.AST
{
    public sealed class ArrayLiteral : AbstractPrimaryExpression
    {
        public readonly IReadOnlyList<IArrayLiteralItem> arrayLiteralItems;

        public ArrayLiteral(IReadOnlyList<IArrayLiteralItem> arrayLiteralItems, bool isStrictMode) : base(isStrictMode)
        {
            this.arrayLiteralItems = arrayLiteralItems;
        }

        public override Completion Evaluate(Interpreter interpreter)
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
                array.Set("length", len.value!, false);
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
                    case AbstractAssignmentExpression assignmentExpression:
                        valueComp = assignmentExpression.Evaluate(Interpreter.Instance()).GetValue();
                        if (valueComp.IsAbrupt()) return valueComp;
                        value = valueComp.value!;
                        status = Utils.CreateDataProperty(array, nextIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), value);
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
                        value = valueComp.value!;
                        if (!(value is Object @object))
                            throw new InvalidOperationException($"ArrayLiteral: tried to initialize an array using a spread on a non-object");
                        var iteratorComp = @object.GetIterator();
                        if (iteratorComp.IsAbrupt()) return iteratorComp;
                        var iteratorRecord = iteratorComp.Other!;
                        while (true)
                        {
                            var next = iteratorRecord.IteratorStep();
                            if (next.IsAbrupt()) return next;
                            if (next.value == BooleanValue.False)
                                break;
                            var nextValue = IteratorRecord.IteratorValue(next.value!);
                            if (nextValue.IsAbrupt()) return nextValue;
                            status = Utils.CreateDataProperty(array, nextIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), nextValue.value!);
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
