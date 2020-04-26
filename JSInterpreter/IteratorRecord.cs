using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace JSInterpreter
{
    public class ObjectIteratorRecord : IEnumerator<Completion>
    {
        private readonly Object iterator;
        private readonly Callable nextMethod;

        public ObjectIteratorRecord(Object iterator, Callable nextMethod)
        {
            this.iterator = iterator;
            this.nextMethod = nextMethod;
        }

        public Completion Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            var returnComp = iterator.GetMethod("return");
            if (returnComp.IsAbrupt())
            {
                Current = returnComp;
                return;
            }
            var @return = returnComp.value;
            if (@return == UndefinedValue.Instance)
                return;
            var innerResult = (@return as Callable).Call(iterator);
            if (innerResult.completionType == CompletionType.Throw)
            {
                Current = innerResult;
                return;
            }
            if (!(innerResult.value is Object))
            {
                Current = Completion.ThrowTypeError("iterator return did not return an object");
            }
        }

        public bool MoveNext()
        {
            Current = nextMethod.Call(iterator);
            if (Current.IsAbrupt()) return false;
            if (!(Current.value is Object o))
            {
                Current = Completion.ThrowTypeError("iterator next did not return an object.");
                return false;
            }

            var doneComp = o.Get("done");
            if (doneComp.IsAbrupt())
            {
                Current = doneComp;
                return false;
            }
            return doneComp.value.ToBoolean() == BooleanValue.False;
        }

        public void Reset()
        {
            throw new NotImplementedException("object iterator records cannot be reset.");
        }
    }
}
